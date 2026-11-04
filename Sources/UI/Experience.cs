using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace LyaranTV
{
    /// <summary>
    /// Finitions d'expérience : écran de démarrage, transitions et petits
    /// mouvements qui donnent à LyaranTV l'allure d'une application de salon.
    /// </summary>
    public sealed partial class MainWindow
    {
        bool startupSplashShown;
        /// <summary>Vrai tant que le générique de lancement n'est pas terminé.</summary>
        bool splashRunning;

        /// <summary>
        /// Vrai tant que le générique n'a pas fini : soit il est affiché, soit il
        /// n'a pas encore été lancé. Au démarrage le chargement de la playlist
        /// part avant le générique, d'où ce second cas.
        /// </summary>
        bool SplashPending { get { return splashRunning || !startupSplashShown || launchOverlay != null; } }

        /// <summary>
        /// Générique de lancement : halo qui s'ouvre, logo qui prend sa place,
        /// nom écrit lettre à lettre, puis fondu vers l'interface. Il couvre la
        /// fenêtre entière, en fenêtré comme en plein écran, s'efface au clic et
        /// se coupe si l'utilisateur a demandé moins d'animations.
        /// </summary>
        void ShowStartupSplash()
        {
            if (startupSplashShown || rootGrid == null) return;
            startupSplashShown = true;
            // Animations réduites : la marque s'affiche d'un coup, sans mouvement.
            bool animate = !UiMotion.Reduced;


            var overlay = new Grid { Background = Brush(Back), IsHitTestVisible = true, ClipToBounds = true };

            // Halo qui s'ouvre derrière la marque : la lumière vient de l'accent,
            // la seconde teinte du dégradé donne la profondeur.
            var halo = new Border
            {
                Width = 640, Height = 640, CornerRadius = new CornerRadius(320),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Background = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(WithAlpha(Accent, 42), 0),
                        new GradientStop(WithAlpha(Secondary, 18), 0.42),
                        new GradientStop(WithAlpha(Back, 0), 1)
                    }
                },
                Opacity = 0
            };
            overlay.Children.Add(halo);

            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            var logo = new Image
            {
                Source = CreateLogoMark(), Width = 128, Height = 114, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var logoScale = new ScaleTransform(0.86, 0.86);
            logo.RenderTransform = logoScale;
            logo.Effect = new DropShadowEffect { BlurRadius = 54, ShadowDepth = 0, Opacity = 0.55, Color = Accent };
            content.Children.Add(logo);

            // Nom écrit lettre à lettre, comme une signature qui se pose.
            var letters = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            var letterBlocks = new List<TextBlock>();
            foreach (char letter in "LyaranTV")
            {
                var block = Text(letter.ToString(), 40, Brush(TextPrimary), true);
                block.Opacity = 0;
                block.RenderTransform = new TranslateTransform(0, 10);
                letters.Children.Add(block);
                letterBlocks.Add(block);
            }
            content.Children.Add(letters);

            var tagline = Text("CINÉMA  ·  DIRECT  ·  SÉRIES", 10, Brush(Accent), true);
            tagline.HorizontalAlignment = HorizontalAlignment.Center;
            tagline.Margin = new Thickness(0, 9, 0, 0);
            tagline.Opacity = 0;
            content.Children.Add(tagline);

            var track = new Border
            {
                Width = 168, Height = 2.5, CornerRadius = new CornerRadius(2), Background = Brush(WithAlpha(TextPrimary, 22)),
                Margin = new Thickness(0, 26, 0, 0), HorizontalAlignment = HorizontalAlignment.Center,
                ClipToBounds = true, Opacity = 0
            };
            var fill = new Border
            {
                Width = 0, Height = 2.5, HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(WithAlpha(Accent, 70), Accent, 0)
            };
            track.Child = fill;
            content.Children.Add(track);

            overlay.Children.Add(content);

            // Fenêtre dédiée posée sur la fenêtre principale : elle s'affiche en
            // fenêtré comme en plein écran et ne peut pas être recouverte par
            // les reconstructions de page du démarrage.
            var splash = new Window
            {
                // Owner garantit que le générique reste au-dessus de la fenêtre
                // principale, sans lui voler le focus.
                Owner = this,
                WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false, ShowActivated = false, Focusable = false,
                Background = Brush(Back), Content = overlay, Title = "LyaranTV"
            };
            try
            {
                splash.Left = Left;
                splash.Top = Top;
                splash.Width = ActualWidth > 0 ? ActualWidth : Width;
                splash.Height = ActualHeight > 0 ? ActualHeight : Height;
            }
            catch { }

            // Fenêtre strictement décorative : transparente aux clics
            // (WS_EX_TRANSPARENT) et sans activation. Sans cela, elle interceptait
            // les boutons du lecteur et les raccourcis — dont « C » — tant
            // qu'elle restait affichée.
            splash.IsHitTestVisible = false;
            overlay.IsHitTestVisible = false;
            splash.SourceInitialized += delegate
            {
                try
                {
                    var handle = new System.Windows.Interop.WindowInteropHelper(splash).Handle;
                    const int extendedStyle = -20;
                    int style = GetWindowLong(handle, extendedStyle);
                    SetWindowLong(handle, extendedStyle, style | 0x00000020 | 0x08000000);
                }
                catch { }
            };
            bool dismissed = false;

            if (!animate)
            {
                halo.Opacity = 1; logo.Opacity = 1; tagline.Opacity = 1; track.Opacity = 1; fill.Width = 168;
                foreach (var block in letterBlocks) block.Opacity = 1;
            }
            else
            {
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var when = TimeSpan.FromMilliseconds(120);

            halo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(760)) { EasingFunction = easeOut });
            logo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(620)) { BeginTime = when, EasingFunction = easeOut });
            var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
            logoScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.86, 1, TimeSpan.FromMilliseconds(1050)) { BeginTime = when, EasingFunction = pop });
            logoScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.86, 1, TimeSpan.FromMilliseconds(1050)) { BeginTime = when, EasingFunction = pop });

            for (int i = 0; i < letterBlocks.Count; i++)
            {
                var block = letterBlocks[i];
                var drop = (TranslateTransform)block.RenderTransform;
                var start = TimeSpan.FromMilliseconds(400 + i * 52);
                block.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(340)) { BeginTime = start, EasingFunction = easeOut });
                drop.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(420)) { BeginTime = start, EasingFunction = easeOut });
            }

            tagline.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)) { BeginTime = TimeSpan.FromMilliseconds(880), EasingFunction = easeOut });
            track.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(960) });
            fill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(0, 168, TimeSpan.FromMilliseconds(640)) { BeginTime = TimeSpan.FromMilliseconds(960), EasingFunction = easeOut });
            }

            Action dismiss = delegate
            {
                if (dismissed) return;
                dismissed = true;
                // Le fondu porte sur le contenu, pas sur la fenêtre : animer
                // Window.Opacity demande AllowsTransparency et laissait sinon
                // le générique ouvert, au-dessus de l'application.
                overlay.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
                var guard = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(360) };
                guard.Tick += delegate { guard.Stop(); splashRunning = false; try { splash.Close(); } catch { } };
                guard.Start();
            };
            splashRunning = true;
            splash.Show();

            // Un clic dans l'application passe le générique immédiatement.
            PreviewMouseDown += delegate { dismiss(); };

            var close = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(animate ? 1550 : 520) };
            close.Tick += delegate { close.Stop(); dismiss(); };
            close.Start();

            // Filet de sécurité : la fenêtre du générique ne doit jamais survivre
            // au démarrage, même si une animation échoue.
            var failsafe = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            failsafe.Tick += delegate { failsafe.Stop(); splashRunning = false; try { if (!dismissed) dismiss(); splash.Close(); } catch { } };
            failsafe.Start();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        Grid busyOverlay;
        TextBlock busyOverlayLabel;

        /// <summary>
        /// Voile de travail : couvre l'interface pendant une opération longue
        /// (sauvegarde, restauration, export d'un gros catalogue) pour que
        /// l'application ne paraisse jamais figée. Le libellé indique l'étape.
        /// </summary>
        internal void ShowBusy(string message)
        {
            if (rootGrid == null) return;
            if (busyOverlay != null)
            {
                if (busyOverlayLabel != null) busyOverlayLabel.Text = String.IsNullOrWhiteSpace(message) ? "Traitement en cours…" : message;
                return;
            }
            var overlay = new Grid
            {
                Background = Brush(Color.FromArgb(158, 7, 8, 12)),
                IsHitTestVisible = true, Cursor = Cursors.Wait
            };
            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var halo = new Border
            {
                Width = 54, Height = 54, CornerRadius = new CornerRadius(27),
                Background = Brush(WithAlpha(Accent, 26)), BorderBrush = Brush(WithAlpha(Accent, 120)),
                BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center
            };
            var dot = new Border
            {
                Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
                Background = Brush(Accent), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            if (!UiMotion.Reduced)
            {
                var pulse = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(720))
                {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                dot.BeginAnimation(UIElement.OpacityProperty, pulse);
            }
            halo.Child = dot;
            content.Children.Add(halo);
            var label = Text(String.IsNullOrWhiteSpace(message) ? "Traitement en cours…" : message, 12.5, Brush(TextPrimary), true);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 14, 0, 0);
            content.Children.Add(label);
            var track = new Border
            {
                Width = 220, Height = 3, CornerRadius = new CornerRadius(2),
                Background = Brush(WithAlpha(TextPrimary, 24)), Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center, ClipToBounds = true
            };
            var fill = new Border
            {
                Width = 70, Height = 3, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(WithAlpha(Accent, 90), Accent, 0)
            };
            track.Child = fill;
            if (!UiMotion.Reduced)
            {
                var slide = new DoubleAnimation(-70, 220, TimeSpan.FromMilliseconds(1150))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                fill.BeginAnimation(FrameworkElement.WidthProperty, slide);
            }
            content.Children.Add(track);
            overlay.Children.Add(content);
            System.Windows.Controls.Panel.SetZIndex(overlay, 900);
            rootGrid.Children.Add(overlay);
            busyOverlay = overlay;
            busyOverlayLabel = label;
            Mouse.OverrideCursor = Cursors.Wait;
        }

        internal void HideBusy()
        {
            if (busyOverlay == null) return;
            try { rootGrid.Children.Remove(busyOverlay); } catch { }
            busyOverlay = null;
            busyOverlayLabel = null;
            Mouse.OverrideCursor = null;
        }

        /// <summary>Exécute une opération courte derrière le voile de travail.</summary>
        internal void RunBusy(string message, Action work)
        {
            if (work == null) return;
            ShowBusy(message);
            Dispatcher.BeginInvoke(new Action(delegate
            {
                try { work(); }
                finally { HideBusy(); }
            }), DispatcherPriority.Background);
        }

        /// <summary>Petit éclat sur une carte mise en avant : ombre et liseré accentué.</summary>
        void Glow(Border border, Color tint, double blur)
        {
            if (border == null || UiMotion.Reduced) return;
            border.Effect = new DropShadowEffect { BlurRadius = blur, ShadowDepth = 8, Opacity = 0.42, Color = tint };
        }
    }
}
