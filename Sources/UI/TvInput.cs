using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LyaranTV
{
    /// <summary>
    /// Navigation « salon » : manette Xbox, télécommande multimédia et clavier
    /// virtuel. Le focus se déplace spatialement (flèches / croix directionnelle)
    /// au lieu de suivre l'ordre de tabulation, ce qui rend l'interface utilisable
    /// sans souris. Le clavier virtuel s'ouvre dans la recherche dès qu'un de ces
    /// périphériques est utilisé.
    /// </summary>
    public sealed partial class MainWindow
    {
        // ------------------------------------------------------------ XInput

        [StructLayout(LayoutKind.Sequential)]
        struct XInputGamepad
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct XInputState
        {
            public uint dwPacketNumber;
            public XInputGamepad Gamepad;
        }

        [DllImport("xinput9_1_0.dll")]
        static extern int XInputGetState(int index, out XInputState state);

        const ushort PadUp = 0x0001, PadDown = 0x0002, PadLeft = 0x0004, PadRight = 0x0008;
        const ushort PadStart = 0x0010, PadBack = 0x0020, PadLeftThumb = 0x0040, PadRightThumb = 0x0080;
        const ushort PadLB = 0x0100, PadRB = 0x0200, PadA = 0x1000, PadB = 0x2000, PadX = 0x4000, PadY = 0x8000;

        readonly ushort[] padPrevious = new ushort[4];
        DispatcherTimer gamepadTimer;
        bool gamepadUnavailable;
        bool pointerInput = true;
        DateTime lastStickTime = DateTime.MinValue;
        int lastStickDirection;

        Grid virtualKeyboardOverlay;
        TextBlock virtualKeyboardPreview;
        TextBox virtualKeyboardTarget;

        bool TvMode { get { return settings != null && settings.TvNavigation; } }

        void InitializeTvInput()
        {
            // La souris reprend la main dès qu'elle bouge : le clavier virtuel ne
            // s'impose pas à un utilisateur classique.
            PreviewMouseDown += delegate { pointerInput = true; };
            PreviewMouseMove += delegate { pointerInput = true; };
            PreviewKeyDown += HandleTvPreviewKeys;

            gamepadTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(45) };
            gamepadTimer.Tick += delegate { PollGamepads(); };
            gamepadTimer.Start();
        }

        void EnableTvNavigation()
        {
            if (TvMode) return;
            settings.TvNavigation = true;
            SettingsStore.Save(settings);
            ShowToast("Navigation manette / télécommande activée.", false, true);
        }

        void PollGamepads()
        {
            if (gamepadUnavailable) return;
            bool connected = false;
            for (int index = 0; index < 4; index++)
            {
                XInputState state;
                try
                {
                    if (XInputGetState(index, out state) != 0) { padPrevious[index] = 0; continue; }
                }
                catch (DllNotFoundException) { gamepadUnavailable = true; return; }
                catch (EntryPointNotFoundException) { gamepadUnavailable = true; return; }
                catch { return; }

                connected = true;
                ushort buttons = state.Gamepad.wButtons;
                ushort pressed = (ushort)(buttons & ~padPrevious[index]);
                padPrevious[index] = buttons;
                if (pressed != 0)
                {
                    pointerInput = false;
                    EnableTvNavigation();
                    HandleGamepadButtons(pressed);
                }

                // Stick gauche : répétition lente pour rester précis dans une grille.
                int dx = state.Gamepad.sThumbLX;
                int dy = state.Gamepad.sThumbLY;
                int direction = 0;
                if (Math.Abs(dx) > 19000 || Math.Abs(dy) > 19000)
                {
                    if (Math.Abs(dx) > Math.Abs(dy)) direction = dx > 0 ? 1 : 3;
                    else direction = dy > 0 ? 0 : 2;
                }
                if (direction == 0)
                {
                    lastStickDirection = 0;
                }
                else if (direction != lastStickDirection
                    || (DateTime.UtcNow - lastStickTime).TotalMilliseconds > 190)
                {
                    lastStickDirection = direction;
                    lastStickTime = DateTime.UtcNow;
                    pointerInput = false;
                    EnableTvNavigation();
                    MoveFocusSpatially(direction);
                }
            }
            if (connected && !TvMode) { /* détection silencieuse */ }
        }

        void HandleGamepadButtons(ushort pressed)
        {
            // Pendant le choix du profil, la manette ne sert qu'à parcourir les
            // cartes et à entrer : les autres commandes restent inactives.
            if (launchOverlay != null)
            {
                if ((pressed & PadUp) != 0) MoveFocusSpatially(0);
                if ((pressed & PadDown) != 0) MoveFocusSpatially(2);
                if ((pressed & PadLeft) != 0) MoveFocusSpatially(3);
                if ((pressed & PadRight) != 0) MoveFocusSpatially(1);
                if ((pressed & PadA) != 0) ActivateFocusedElement();
                return;
            }
            if ((pressed & PadUp) != 0) MoveFocusSpatially(0);
            if ((pressed & PadDown) != 0) MoveFocusSpatially(2);
            if ((pressed & PadLeft) != 0) MoveFocusSpatially(3);
            if ((pressed & PadRight) != 0) MoveFocusSpatially(1);
            if ((pressed & PadA) != 0) ActivateFocusedElement();
            if ((pressed & PadB) != 0) TvBack();
            if ((pressed & PadY) != 0) FocusSearchForTv();
            if ((pressed & PadStart) != 0) ToggleFullScreen();
            if ((pressed & PadBack) != 0) TvBack();
            if ((pressed & PadX) != 0)
            {
                if (page == "Lecture") ToggleChannelDrawer();
                else TogglePlaybackIfPlaying();
            }
            if ((pressed & PadLeftThumb) != 0) TogglePlaybackIfPlaying();
            if ((pressed & PadRightThumb) != 0 && page == "Lecture") ToggleChannelDrawer();
            if ((pressed & PadLB) != 0 && page == "Lecture") SwitchChannel(-1);
            if ((pressed & PadRB) != 0 && page == "Lecture") SwitchChannel(1);
        }

        void TogglePlaybackIfPlaying()
        {
            if (page == "Lecture" && player != null) TogglePlayback();
        }

        // --------------------------------------------------- touches distantes

        static bool IsRemoteKey(Key key)
        {
            return key == Key.BrowserBack || key == Key.BrowserForward
                || key == Key.MediaPlayPause || key == Key.MediaStop
                || key == Key.MediaNextTrack || key == Key.MediaPreviousTrack
                || key == Key.BrowserHome || key == Key.BrowserRefresh;
        }

        void HandleTvPreviewKeys(object sender, KeyEventArgs e)
        {
            if (IsRemoteKey(e.Key))
            {
                // Pendant le choix du profil, les touches de la télécommande ne
                // pilotent pas l'interface en arrière-plan.
                if (launchOverlay != null) { e.Handled = true; return; }
                pointerInput = false;
                EnableTvNavigation();
                if (e.Key == Key.BrowserBack) { TvBack(); e.Handled = true; return; }
                if (e.Key == Key.BrowserForward) { if (page == "Lecture") SwitchChannel(1); e.Handled = true; return; }
                if (e.Key == Key.BrowserHome) { Nav("Accueil"); e.Handled = true; return; }
                if (e.Key == Key.BrowserRefresh) { RefreshActiveSource(); e.Handled = true; return; }
                if (e.Key == Key.MediaPlayPause) { TogglePlaybackIfPlaying(); e.Handled = true; return; }
                if (e.Key == Key.MediaStop) { if (page == "Lecture") PlayerBack(); e.Handled = true; return; }
                if (e.Key == Key.MediaNextTrack) { if (page == "Lecture") SwitchChannel(1); e.Handled = true; return; }
                if (e.Key == Key.MediaPreviousTrack) { if (page == "Lecture") SwitchChannel(-1); e.Handled = true; return; }
            }

            if (!TvMode || page == "Lecture") return;
            bool typing = Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is PasswordBox;
            if (typing && virtualKeyboardOverlay == null) return;

            if (e.Key == Key.Left) { MoveFocusSpatially(3); e.Handled = true; }
            else if (e.Key == Key.Right) { MoveFocusSpatially(1); e.Handled = true; }
            else if (e.Key == Key.Up) { MoveFocusSpatially(0); e.Handled = true; }
            else if (e.Key == Key.Down) { MoveFocusSpatially(2); e.Handled = true; }
            else if (e.Key == Key.Enter && !(Keyboard.FocusedElement is ButtonBase))
            {
                ActivateFocusedElement();
                e.Handled = true;
            }
        }

        void FocusSearchForTv()
        {
            if (searchBox == null) return;
            searchBox.Focus();
            if (TvMode) ShowVirtualKeyboard(searchBox);
        }

        // ------------------------------------------------- navigation spatiale

        static readonly DependencyProperty TvFocusScannedProperty = DependencyProperty.RegisterAttached(
            "TvFocusScanned", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        void MoveFocusSpatially(int direction)
        {
            try
            {
                var candidates = new List<FrameworkElement>();
                // Le focus reste dans la couche active : générique, clavier
                // virtuel ou interface normale, jamais derrière un voile.
                DependencyObject scope = launchOverlay != null
                    ? (DependencyObject)launchOverlay
                    : (virtualKeyboardOverlay != null ? (DependencyObject)virtualKeyboardOverlay : (DependencyObject)this);
                CollectFocusable(scope, candidates, 0);
                if (candidates.Count == 0) return;

                var focused = Keyboard.FocusedElement as FrameworkElement;
                if (focused == null || !candidates.Contains(focused))
                {
                    candidates[0].Focus();
                    return;
                }

                Rect current = BoundsOf(focused);
                FrameworkElement best = null;
                double bestScore = Double.MaxValue;
                double currentX = current.Left + current.Width / 2;
                double currentY = current.Top + current.Height / 2;

                foreach (var candidate in candidates)
                {
                    if (candidate == focused) continue;
                    Rect rect = BoundsOf(candidate);
                    if (rect.Width < 2 || rect.Height < 2) continue;
                    double x = rect.Left + rect.Width / 2;
                    double y = rect.Top + rect.Height / 2;
                    double dx = x - currentX;
                    double dy = y - currentY;
                    double primary, perpendicular;
                    if (direction == 1) { if (dx <= 6) continue; primary = dx; perpendicular = Math.Abs(dy); }
                    else if (direction == 3) { if (dx >= -6) continue; primary = -dx; perpendicular = Math.Abs(dy); }
                    else if (direction == 2) { if (dy <= 6) continue; primary = dy; perpendicular = Math.Abs(dx); }
                    else { if (dy >= -6) continue; primary = -dy; perpendicular = Math.Abs(dx); }
                    double score = primary + perpendicular * 1.65;
                    if (score < bestScore) { bestScore = score; best = candidate; }
                }
                if (best != null) { best.Focus(); best.BringIntoView(); }
            }
            catch { }
        }

        void CollectFocusable(DependencyObject root, List<FrameworkElement> list, int depth)
        {
            if (root == null || depth > 60) return;
            var element = root as FrameworkElement;
            if (element != null && element != this && element.Focusable && element.IsVisible && element.IsEnabled
                && element.IsHitTestVisible && element.ActualWidth >= 3 && element.ActualHeight >= 3)
            {
                list.Add(element);
            }
            int count = 0;
            try { count = VisualTreeHelper.GetChildrenCount(root); } catch { }
            for (int index = 0; index < count; index++)
                CollectFocusable(VisualTreeHelper.GetChild(root, index), list, depth + 1);
        }

        Rect BoundsOf(FrameworkElement element)
        {
            try
            {
                Point origin = element.TranslatePoint(new Point(0, 0), this);
                return new Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
            }
            catch { return new Rect(0, 0, 0, 0); }
        }

        void ActivateFocusedElement()
        {
            var focused = Keyboard.FocusedElement as FrameworkElement;
            if (focused == null) return;
            var button = focused as ButtonBase;
            if (button != null)
            {
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                return;
            }
            if (focused is TextBox || focused is PasswordBox)
            {
                ShowVirtualKeyboard(focused as TextBox);
                return;
            }
            try
            {
                var source = PresentationSource.FromVisual(this);
                if (source == null) return;
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
                focused.RaiseEvent(args);
            }
            catch { }
        }

        void TvBack()
        {
            // Le choix du profil ne se referme pas : il faut entrer ou quitter.
            if (launchOverlay != null) return;
            if (virtualKeyboardOverlay != null) { HideVirtualKeyboard(); return; }
            if (detailOverlay != null) { CloseDetailOverlay(); return; }
            if (fullScreen) { ExitFullScreen(); return; }
            if (page == "Lecture") { PlayerBack(); return; }
            NavBack();
        }

        // ----------------------------------------------------- clavier virtuel

        static readonly string[] KeyboardRows =
        {
            "AZERTYUIOP",
            "QSDFGHJKLM",
            "WXCVBN1234",
            "567890.,'-"
        };

        void ShowVirtualKeyboard(TextBox target)
        {
            if (target == null) return;
            virtualKeyboardTarget = target;
            if (virtualKeyboardOverlay != null)
            {
                UpdateVirtualKeyboardPreview();
                return;
            }

            var overlay = new Grid { Background = Brush(Color.FromArgb(168, 4, 5, 10)) };
            overlay.PreviewMouseDown += delegate { pointerInput = true; };

            var card = new Border
            {
                Width = 860, MaxWidth = 860, CornerRadius = new CornerRadius(20),
                Background = Brush(Panel), BorderBrush = Brush(WithAlpha(Accent, 90)), BorderThickness = new Thickness(1),
                Padding = new Thickness(24, 20, 24, 20), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = ModalShadow()
            };
            var stack = new StackPanel();
            card.Child = stack;

            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = Text("Clavier à l’écran", 18, Brush(TextPrimary), true);
            title.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(title);
            var close = IconButton(Glyphs.Close, "Fermer le clavier · Échap", HideVirtualKeyboard, false);
            close.Margin = new Thickness(0);
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            stack.Children.Add(header);

            virtualKeyboardPreview = Text("", 20, Brush(TextPrimary), false);
            virtualKeyboardPreview.TextTrimming = TextTrimming.CharacterEllipsis;
            virtualKeyboardPreview.MinHeight = 30;
            virtualKeyboardPreview.VerticalAlignment = VerticalAlignment.Center;
            var preview = new Border
            {
                Child = virtualKeyboardPreview, MinHeight = 52, CornerRadius = new CornerRadius(12),
                Background = Brush(Raised), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 14)
            };
            stack.Children.Add(preview);

            foreach (string row in KeyboardRows)
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                foreach (char character in row)
                {
                    string value = character.ToString();
                    var key = KeyboardKey(value, delegate { AppendVirtualKeyboard(value); });
                    line.Children.Add(key);
                }
                line.Margin = new Thickness(0, 0, 0, 7);
                stack.Children.Add(line);
            }

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
            var space = KeyboardKey("Espace", delegate { AppendVirtualKeyboard(" "); });
            space.Width = 150;
            actions.Children.Add(space);
            var backspace = KeyboardKey("Effacer", delegate { BackspaceVirtualKeyboard(); });
            backspace.Width = 110;
            actions.Children.Add(backspace);
            var clear = KeyboardKey("Tout effacer", delegate
            {
                if (virtualKeyboardTarget != null) virtualKeyboardTarget.Text = "";
                UpdateVirtualKeyboardPreview();
            });
            clear.Width = 130;
            actions.Children.Add(clear);
            var search = Button(Glyphs.Search, "Rechercher", delegate
            {
                if (virtualKeyboardTarget == null) return;
                if (virtualKeyboardTarget == channelDrawerFilter)
                {
                    HideVirtualKeyboard();
                    return;
                }
                string query = virtualKeyboardTarget.Text.Trim();
                if (query.Length == 0) return;
                // Le clavier peut éditer un autre champ (filtre du tiroir) :
                // on recopie vers la recherche principale avant de valider.
                if (virtualKeyboardTarget != searchBox && searchBox != null)
                {
                    suppressSearch = true;
                    searchBox.Text = virtualKeyboardTarget.Text;
                    suppressSearch = false;
                }
                HideVirtualKeyboard();
                OpenSearch();
            }, true);
            search.Height = 42;
            search.Margin = new Thickness(4, 0, 0, 0);
            actions.Children.Add(search);
            stack.Children.Add(actions);

            var hint = Text("Flèches ou croix directionnelle pour choisir · A / Entrée pour valider · B / Échap pour fermer", 11, Brush(Muted), false);
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.Margin = new Thickness(0, 12, 0, 0);
            stack.Children.Add(hint);

            overlay.Children.Add(card);
            virtualKeyboardOverlay = overlay;
            System.Windows.Controls.Panel.SetZIndex(overlay, 960);
            rootGrid.Children.Add(overlay);
            UpdateVirtualKeyboardPreview();
            PreviewTextInput += VirtualKeyboardTextInput;

            // Focus sur la première touche : la croix directionnelle prend le relais.
            var firstKey = FindFirstKey(overlay);
            if (firstKey != null) firstKey.Focus();
        }

        Button KeyboardKey(string label, Action click)
        {
            var key = new Button
            {
                Content = Text(label, label.Length > 2 ? 12 : 15, Brush(TextPrimary), label.Length <= 2),
                MinWidth = label.Length > 2 ? 80 : 52, Height = 44, Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(3, 0, 3, 0), Background = Brush(Raised), BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, FocusVisualStyle = null,
                Template = RoundedButtonTemplate(9), HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            key.Click += delegate { if (click != null) click(); };
            return key;
        }

        Button FindFirstKey(DependencyObject root)
        {
            var button = root as Button;
            if (button != null && button.Focusable) return button;
            int count = 0;
            try { count = VisualTreeHelper.GetChildrenCount(root); } catch { }
            for (int index = 0; index < count; index++)
            {
                var found = FindFirstKey(VisualTreeHelper.GetChild(root, index));
                if (found != null) return found;
            }
            return null;
        }

        void AppendVirtualKeyboard(string value)
        {
            if (virtualKeyboardTarget == null) return;
            virtualKeyboardTarget.Text = (virtualKeyboardTarget.Text ?? "") + value;
            virtualKeyboardTarget.CaretIndex = virtualKeyboardTarget.Text.Length;
            UpdateVirtualKeyboardPreview();
        }

        void BackspaceVirtualKeyboard()
        {
            if (virtualKeyboardTarget == null) return;
            string text = virtualKeyboardTarget.Text ?? "";
            if (text.Length > 0) virtualKeyboardTarget.Text = text.Substring(0, text.Length - 1);
            virtualKeyboardTarget.CaretIndex = virtualKeyboardTarget.Text.Length;
            UpdateVirtualKeyboardPreview();
        }

        void UpdateVirtualKeyboardPreview()
        {
            if (virtualKeyboardPreview == null) return;
            string text = virtualKeyboardTarget == null ? "" : (virtualKeyboardTarget.Text ?? "");
            virtualKeyboardPreview.Text = text.Length == 0 ? "Rechercher…" : text;
            virtualKeyboardPreview.Foreground = text.Length == 0 ? Brush(Muted) : Brush(TextPrimary);
        }

        void VirtualKeyboardTextInput(object sender, TextCompositionEventArgs e)
        {
            if (virtualKeyboardOverlay == null || String.IsNullOrEmpty(e.Text)) return;
            AppendVirtualKeyboard(e.Text);
            e.Handled = true;
        }

        void HideVirtualKeyboard()
        {
            if (virtualKeyboardOverlay == null) return;
            var target = virtualKeyboardTarget;
            PreviewTextInput -= VirtualKeyboardTextInput;
            try { rootGrid.Children.Remove(virtualKeyboardOverlay); } catch { }
            virtualKeyboardOverlay = null;
            virtualKeyboardPreview = null;
            virtualKeyboardTarget = null;
            try
            {
                if (target != null && target.IsVisible) target.Focus();
                else if (searchBox != null) searchBox.Focus();
            }
            catch { }
        }
    }
}
