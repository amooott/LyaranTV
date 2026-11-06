using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LyaranTV
{
    /// <summary>
    /// Ouverture de LyaranTV : un générique de chargement, puis l'écran
    /// « Qui regarde ? » façon plateforme de streaming. Le profil choisi ouvre
    /// l'application avec un fondu et un léger zoom, dans l'esprit Netflix.
    /// </summary>
    public sealed partial class MainWindow
    {
        const double LaunchProgressWidth = 300;

        Grid launchOverlay;
        Grid launchBrandStage;
        Grid launchProfileStage;
        WrapPanel launchProfileCards;
        TextBlock launchProfileError;
        TextBlock launchPercentText;
        TextBlock launchStatusText;
        Border launchProgressFill;
        DispatcherTimer launchProgressTimer;
        DispatcherTimer launchStatusTimer;
        FrameworkElement launchProfileFocus;

        /// <summary>
        /// Vrai pendant le générique et le choix du profil : l'interface en
        /// dessous est prête, mais elle attend que la personne entre.
        /// </summary>
        bool LaunchOverlayVisible { get { return launchOverlay != null; } }

        void ShowLaunchExperience()
        {
            if (launchOverlay != null || rootGrid == null || appClosing) return;
            startupSplashShown = true;

            bool animate = !UiMotion.Reduced;
            var overlay = new Grid { Background = Brush(Back), ClipToBounds = true };

            // Fond cinématographique : deux nappes sombres, un halo d'accent en
            // haut à droite, la seconde teinte en bas à gauche, puis une vignette.
            overlay.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(LightMix(Back, Panel, 0.55), 0),
                    new GradientStop(Back, 0.52),
                    new GradientStop(LightMix(Back, Color.FromRgb(0, 0, 0), 0.62), 1)
                }, new Point(0.18, 0), new Point(0.82, 1))
            });

            var accentHalo = new Ellipse
            {
                Width = 1080, Height = 1080,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -600, -280, 0),
                Fill = new RadialGradientBrush(WithAlpha(Accent, 60), WithAlpha(Accent, 0)),
                Opacity = 0
            };
            overlay.Children.Add(accentHalo);

            var secondHalo = new Ellipse
            {
                Width = 900, Height = 900,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(-430, 0, 0, -560),
                Fill = new RadialGradientBrush(WithAlpha(Secondary, 34), WithAlpha(Secondary, 0)),
                Opacity = 0
            };
            overlay.Children.Add(secondHalo);

            overlay.Children.Add(new Border
            {
                Background = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.44), GradientOrigin = new Point(0.5, 0.44),
                    RadiusX = 0.95, RadiusY = 0.95,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.5),
                        new GradientStop(WithAlpha(Back, 190), 1)
                    }
                },
                IsHitTestVisible = false
            });

            var brandStage = BuildLaunchBrandStage(animate);
            launchBrandStage = brandStage;
            overlay.Children.Add(brandStage);

            launchOverlay = overlay;
            System.Windows.Controls.Panel.SetZIndex(overlay, 940);
            rootGrid.Children.Add(overlay);

            // L'application respire derrière le générique : elle apparaît en
            // fondu au moment où le voile se lève.
            if (mainScroll != null) mainScroll.Opacity = 0;

            if (animate)
            {
                accentHalo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0.95, TimeSpan.FromMilliseconds(980))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
                secondHalo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0.85, TimeSpan.FromMilliseconds(1100))
                {
                    BeginTime = TimeSpan.FromMilliseconds(120),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });

                // Dérive très lente des halos : le fond vit sans jamais distraire.
                var drift = new TranslateTransform(0, 0);
                accentHalo.RenderTransform = drift;
                drift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -46, TimeSpan.FromSeconds(13))
                {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
                drift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 34, TimeSpan.FromSeconds(16))
                {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
            }
            else
            {
                accentHalo.Opacity = 0.95;
                secondHalo.Opacity = 0.85;
            }

            StartLaunchProgress(animate);
        }

        Grid BuildLaunchBrandStage(bool animate)
        {
            var stage = new Grid();
            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            var logo = new Image
            {
                Source = CreateLogoMark(), Width = 124, Height = 110, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var logoScale = new ScaleTransform(0.86, 0.86);
            logo.RenderTransform = logoScale;
            logo.Effect = new DropShadowEffect { BlurRadius = 58, ShadowDepth = 0, Opacity = 0.55, Color = Accent };
            content.Children.Add(logo);

            var letters = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 18, 0, 0)
            };
            var letterBlocks = new List<TextBlock>();
            foreach (char letter in "LyaranTV")
            {
                var block = Text(letter.ToString(), 38, Brush(TextPrimary), true);
                block.Margin = new Thickness(1, 0, 1, 0);
                block.Opacity = 0;
                block.RenderTransform = new TranslateTransform(0, 12);
                letters.Children.Add(block);
                letterBlocks.Add(block);
            }
            content.Children.Add(letters);

            var tagline = Text("CINÉMA  ·  DIRECT  ·  SÉRIES", 10, Brush(Accent), true);
            tagline.HorizontalAlignment = HorizontalAlignment.Center;
            tagline.Margin = new Thickness(0, 10, 0, 0);
            tagline.Opacity = 0;
            content.Children.Add(tagline);

            var progress = new Grid { Width = LaunchProgressWidth, Margin = new Thickness(0, 34, 0, 0), Opacity = 0 };
            progress.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            progress.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var track = new Border
            {
                Height = 3, CornerRadius = new CornerRadius(2),
                Background = Brush(WithAlpha(TextPrimary, 22)), ClipToBounds = true
            };
            launchProgressFill = new Border
            {
                Width = 0, Height = 3, CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(Accent, LightMix(Accent, Secondary, 0.72), new Point(0, 0), new Point(1, 0))
            };
            track.Child = launchProgressFill;
            Grid.SetRow(track, 0);
            progress.Children.Add(track);

            var line = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            launchStatusText = Text("Initialisation de l’interface", 10.5, Brush(Muted), false);
            launchStatusText.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(launchStatusText);
            launchPercentText = Text("0 %", 10.5, Brush(Muted), false);
            launchPercentText.HorizontalAlignment = HorizontalAlignment.Right;
            launchPercentText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(launchPercentText, 1);
            line.Children.Add(launchPercentText);
            Grid.SetRow(line, 1);
            progress.Children.Add(line);

            content.Children.Add(progress);
            stage.Children.Add(content);

            if (!animate)
            {
                logo.Opacity = 1;
                tagline.Opacity = 1;
                progress.Opacity = 1;
                foreach (var block in letterBlocks) block.Opacity = 1;
                return stage;
            }

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var when = TimeSpan.FromMilliseconds(110);

            logo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(620)) { BeginTime = when, EasingFunction = easeOut });
            var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.34 };
            logoScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.86, 1, TimeSpan.FromMilliseconds(1080)) { BeginTime = when, EasingFunction = pop });
            logoScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.86, 1, TimeSpan.FromMilliseconds(1080)) { BeginTime = when, EasingFunction = pop });

            for (int i = 0; i < letterBlocks.Count; i++)
            {
                var block = letterBlocks[i];
                var drop = (TranslateTransform)block.RenderTransform;
                var start = TimeSpan.FromMilliseconds(360 + i * 54);
                block.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(340)) { BeginTime = start, EasingFunction = easeOut });
                drop.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(430)) { BeginTime = start, EasingFunction = easeOut });
            }

            tagline.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
            {
                BeginTime = TimeSpan.FromMilliseconds(860), EasingFunction = easeOut
            });
            progress.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360))
            {
                BeginTime = TimeSpan.FromMilliseconds(920), EasingFunction = easeOut
            });
            return stage;
        }

        void StartLaunchProgress(bool animate)
        {
            if (launchStatusTimer != null) { launchStatusTimer.Stop(); launchStatusTimer = null; }
            if (launchProgressTimer != null) { launchProgressTimer.Stop(); launchProgressTimer = null; }

            string[] messages =
            {
                "Initialisation de l’interface",
                "Lecture de vos réglages",
                "Préparation de votre bibliothèque",
                "Application de votre thème",
                "Presque prêt"
            };
            int messageIndex = 0;
            launchStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(560) };
            launchStatusTimer.Tick += delegate
            {
                if (launchOverlay == null) { launchStatusTimer.Stop(); return; }
                messageIndex = (messageIndex + 1) % messages.Length;
                if (launchStatusText == null) return;
                launchStatusText.Text = messages[messageIndex];
                if (!UiMotion.Reduced)
                {
                    launchStatusText.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation(0.28, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                }
            };
            launchStatusTimer.Start();

            // Faux chargement maîtrisé : la barre progresse régulièrement puis
            // laisse place au choix du profil. Aucun travail réel n'est bloqué.
            bool fast = SkipProfilePickerForTest || settings.HideProfilePicker;
            double duration = animate ? (fast ? 620 : 1760) : 320;
            var watch = Stopwatch.StartNew();
            launchProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            launchProgressTimer.Tick += delegate
            {
                if (launchOverlay == null) { launchProgressTimer.Stop(); return; }
                double t = Math.Min(1.0, watch.Elapsed.TotalMilliseconds / duration);
                double eased = 1 - Math.Pow(1 - t, 2.4);
                SetLaunchProgress(eased * 100);
                if (t < 1) return;
                launchProgressTimer.Stop();
                launchProgressTimer = null;
                var wait = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(animate ? (fast ? 160 : 280) : 110) };
                wait.Tick += delegate { wait.Stop(); OnLaunchProgressComplete(); };
                wait.Start();
            };
            launchProgressTimer.Start();
        }

        void SetLaunchProgress(double percent)
        {
            double value = Math.Max(0, Math.Min(100, percent));
            if (launchProgressFill != null) launchProgressFill.Width = LaunchProgressWidth * value / 100.0;
            if (launchPercentText != null) launchPercentText.Text = ((int)Math.Round(value)) + " %";
        }

        void OnLaunchProgressComplete()
        {
            if (appClosing || launchOverlay == null) return;
            if (launchStatusTimer != null) { launchStatusTimer.Stop(); launchStatusTimer = null; }
            bool showPicker = !settings.HideProfilePicker && !SkipProfilePickerForTest;
            if (showPicker) ShowLaunchProfileStage();
            else DismissLaunchOverlay();
        }

        void ShowLaunchProfileStage()
        {
            if (launchOverlay == null || launchProfileStage != null) return;
            bool animate = !UiMotion.Reduced;
            var stage = BuildLaunchProfileStage(animate);
            launchProfileStage = stage;
            stage.Opacity = animate ? 0 : 1;
            launchOverlay.Children.Add(stage);

            if (!animate)
            {
                if (launchBrandStage != null) launchBrandStage.Visibility = Visibility.Collapsed;
                FocusLaunchProfile();
                return;
            }

            if (launchBrandStage != null)
            {
                launchBrandStage.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
                var hide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
                hide.Tick += delegate
                {
                    hide.Stop();
                    if (launchBrandStage == null) return;
                    launchBrandStage.Visibility = Visibility.Collapsed;
                    launchBrandStage.IsHitTestVisible = false;
                };
                hide.Start();
            }

            var slide = new TranslateTransform(0, 20);
            stage.RenderTransform = slide;
            stage.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
            {
                BeginTime = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(540))
            {
                BeginTime = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            var focusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(580) };
            focusTimer.Tick += delegate { focusTimer.Stop(); FocusLaunchProfile(); };
            focusTimer.Start();
        }

        Grid BuildLaunchProfileStage(bool animate)
        {
            var stage = new Grid();
            stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            stage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Marque discrète, en haut à gauche : le titre reste le sujet.
            var top = new Grid { Margin = new Thickness(40, 24, 40, 0) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            brand.Children.Add(new Image
            {
                Source = CreateLogoMark(), Width = 30, Height = 27, Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 10, 0)
            });
            brand.Children.Add(Text("LyaranTV", 16, Brush(TextPrimary), true));
            top.Children.Add(brand);
            var version = Text("v" + AppInfo.Version, 10, Brush(WithAlpha(TextPrimary, 110)), false);
            version.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(version, 1);
            top.Children.Add(version);
            Grid.SetRow(top, 0);
            stage.Children.Add(top);

            var center = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 1120
            };

            // Titre très espacé, à la Netflix : de fines espaces entre les lettres.
            var title = Text("Q\u2009U\u2009I\u2002\u2002R\u2009E\u2009G\u2009A\u2009R\u2009D\u2009E\u2002\u2002?", 35, Brush(TextPrimary), true);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.TextAlignment = TextAlignment.Center;
            title.Opacity = animate ? 0 : 1;
            title.RenderTransform = new TranslateTransform(0, 14);
            center.Children.Add(title);

            launchProfileError = Text("", 12, Brush(Danger), false);
            launchProfileError.HorizontalAlignment = HorizontalAlignment.Center;
            launchProfileError.Margin = new Thickness(0, 9, 0, 0);
            launchProfileError.Visibility = Visibility.Collapsed;
            center.Children.Add(launchProfileError);

            double windowHeight = ActualHeight > 200 ? ActualHeight : 860;
            var scroll = new ScrollViewer
            {
                MaxHeight = Math.Max(250, windowHeight - 380),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 16, 0, 0),
                Padding = new Thickness(0, 8, 0, 8),
                Focusable = false
            };
            launchProfileCards = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 1120 };
            scroll.Content = launchProfileCards;
            center.Children.Add(scroll);

            Grid.SetRow(center, 1);
            stage.Children.Add(center);

            // Bas d'écran : seulement la sortie de secours, alignée à droite.
            var bottom = new Grid { Margin = new Thickness(40, 0, 40, 22) };
            var quit = TextAction("Quitter LyaranTV", delegate { Close(); }, "Fermer l’application");
            quit.HorizontalAlignment = HorizontalAlignment.Right;
            bottom.Children.Add(quit);
            Grid.SetRow(bottom, 2);
            stage.Children.Add(bottom);

            RenderLaunchProfileCards(animate);

            if (animate)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                title.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
                {
                    BeginTime = TimeSpan.FromMilliseconds(240), EasingFunction = ease
                });
                ((TranslateTransform)title.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(240), EasingFunction = ease });
            }

            return stage;
        }

        void RenderLaunchProfileCards(bool animate)
        {
            if (launchProfileCards == null) return;
            launchProfileCards.Children.Clear();
            launchProfileFocus = null;
            int index = 0;
            foreach (UserProfile profile in profiles.ToList())
            {
                if (profile == null) continue;
                Button card = BuildLaunchProfileCard(profile, index, animate);
                launchProfileCards.Children.Add(card);
                if (launchProfileFocus == null || profile == activeProfile) launchProfileFocus = card;
                index++;
            }
            launchProfileCards.Children.Add(BuildLaunchAddProfileCard(index, animate));
        }

        Button BuildLaunchProfileCard(UserProfile profile, int index, bool animate)
        {
            Color color;
            if (!TryParseHexColor(profile.AvatarColor, out color)) color = Accent;

            // Avatar : dégradé de la couleur du profil, initiale, reflet et anneau.
            var avatarHost = new Grid { Width = 148, Height = 148, HorizontalAlignment = HorizontalAlignment.Center };
            var avatarScale = new ScaleTransform(1, 1);
            var avatarShift = new TranslateTransform(0, 0);
            var avatarGroup = new TransformGroup();
            avatarGroup.Children.Add(avatarScale);
            avatarGroup.Children.Add(avatarShift);
            avatarHost.RenderTransform = avatarGroup;
            avatarHost.RenderTransformOrigin = new Point(0.5, 0.5);

            // Photo importée, illustration prédéfinie ou initiale : le même
            // composant sert dans le portail de lancement et dans les réglages.
            avatarHost.Children.Add(ProfileAvatars.Build(profile, 148, 15, color, true));

            // Reflet qui traverse la photo au survol : un détail discret, mais
            // qui rend la carte vivante sans attirer l'œil.
            var sheenHost = new Grid { Width = 148, Height = 148, IsHitTestVisible = false, ClipToBounds = true };
            var sheenShift = new TranslateTransform(-190, 0);
            var sheen = new Border
            {
                Width = 58, HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(56, 255, 255, 255), 0.5),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                }, new Point(0, 0), new Point(1, 0)),
                RenderTransform = sheenShift
            };
            sheenHost.Children.Add(sheen);
            RoundClip(sheenHost, 15);
            avatarHost.Children.Add(sheenHost);

            // L'anneau épouse exactement l'avatar : même rayon intérieur, deux
            // pixels de bord. L'ancien décalage de six pixels laissait des
            // croissants sombres aux coins supérieurs.
            var ring = new Border
            {
                Margin = new Thickness(-2), CornerRadius = new CornerRadius(17),
                BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent,
                IsHitTestVisible = false
            };
            avatarHost.Children.Add(ring);

            if (profile.IsChild)
            {
                var childBadge = new Border
                {
                    Child = Text("ENFANT", 8.5, Brushes.White, true),
                    Background = Brush(Color.FromArgb(200, 8, 9, 15)),
                    BorderBrush = Brush(WithAlpha(Colors.White, 70)), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 2, 6, 3),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(8)
                };
                avatarHost.Children.Add(childBadge);
            }
            if (!String.IsNullOrWhiteSpace(profile.PinProtected))
            {
                var lockIcon = Icon(Glyphs.Lock, 12, Brushes.White);
                lockIcon.HorizontalAlignment = HorizontalAlignment.Center;
                lockIcon.VerticalAlignment = VerticalAlignment.Center;
                var lockBadge = new Border
                {
                    Width = 27, Height = 27, CornerRadius = new CornerRadius(14),
                    Background = Brush(Color.FromArgb(214, 8, 9, 15)),
                    BorderBrush = Brush(WithAlpha(Colors.White, 70)), BorderThickness = new Thickness(1),
                    Child = lockIcon,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 8, 8, 0)
                };
                avatarHost.Children.Add(lockBadge);
            }

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(avatarHost);

            var name = Text(profile.Name, 16, Brush(TextPrimary), true);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextAlignment = TextAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.MaxWidth = 168;
            name.Margin = new Thickness(0, 13, 0, 0);
            stack.Children.Add(name);

            var last = Text(profile == activeProfile ? "DERNIER PROFIL" : "", 9, Brush(Accent), true);
            last.HorizontalAlignment = HorizontalAlignment.Center;
            last.Margin = new Thickness(0, 4, 0, 0);
            last.Height = 14;
            stack.Children.Add(last);

            var button = new Button
            {
                Content = stack, Width = 188, Margin = new Thickness(7, 0, 7, 0),
                Padding = new Thickness(8, 8, 8, 10),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, FocusVisualStyle = null,
                Template = RoundedButtonTemplate(20, false, false),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Regarder en tant que " + profile.Name,
                Tag = avatarShift
            };

            bool pointerOver = false, keyboardFocus = false;
            Action updateHover = delegate
            {
                bool on = pointerOver || keyboardFocus;
                if (UiMotion.Reduced)
                {
                    ring.BorderBrush = on ? Brush(Accent) : Brushes.Transparent;
                    return;
                }
                var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                avatarScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(on ? 1.065 : 1.0, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });
                avatarScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(on ? 1.065 : 1.0, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });
                ring.BorderBrush = on ? Brush(Accent) : Brushes.Transparent;
                ring.Effect = on ? new DropShadowEffect { BlurRadius = 28, ShadowDepth = 0, Opacity = 0.55, Color = color } : null;
                name.Foreground = on ? Brushes.White : Brush(TextPrimary);
                if (on)
                {
                    sheenShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-190, 210, TimeSpan.FromMilliseconds(620))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                    });
                }
                else
                {
                    sheenShift.BeginAnimation(TranslateTransform.XProperty, null);
                    sheenShift.X = -190;
                }
            };

            button.MouseEnter += delegate { pointerOver = true; updateHover(); UiSound.Navigate(); };
            button.MouseLeave += delegate { pointerOver = false; updateHover(); };
            button.GotKeyboardFocus += delegate { keyboardFocus = true; updateHover(); };
            button.LostKeyboardFocus += delegate { keyboardFocus = false; updateHover(); };
            button.Click += delegate { SelectLaunchProfile(profile, button); };

            AnimateLaunchCard(button, index, animate);
            return button;
        }

        Button BuildLaunchAddProfileCard(int index, bool animate)
        {
            var square = new Grid { Width = 148, Height = 148, HorizontalAlignment = HorizontalAlignment.Center };
            var dash = new Rectangle
            {
                RadiusX = 15, RadiusY = 15, StrokeThickness = 1.4,
                Stroke = Brush(WithAlpha(TextPrimary, 55)), Fill = Brushes.Transparent,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            square.Children.Add(dash);
            var plus = Icon(Glyphs.Add, 32, Brush(Muted));
            plus.HorizontalAlignment = HorizontalAlignment.Center;
            plus.VerticalAlignment = VerticalAlignment.Center;
            square.Children.Add(plus);

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(square);
            var label = Text("Ajouter un profil", 14, Brush(Muted), false);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 13, 0, 0);
            stack.Children.Add(label);
            var spacer = Text("", 9, Brush(Muted), false);
            spacer.Height = 14;
            spacer.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(spacer);

            var button = new Button
            {
                Content = stack, Width = 188, Margin = new Thickness(7, 0, 7, 0),
                Padding = new Thickness(8, 8, 8, 10),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, FocusVisualStyle = null,
                Template = RoundedButtonTemplate(20, false, false),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Créer un nouveau profil"
            };
            button.MouseEnter += delegate { dash.Stroke = Brush(Accent); plus.Foreground = Brush(TextPrimary); label.Foreground = Brush(TextPrimary); };
            button.MouseLeave += delegate { dash.Stroke = Brush(WithAlpha(TextPrimary, 55)); plus.Foreground = Brush(Muted); label.Foreground = Brush(Muted); };
            button.GotKeyboardFocus += delegate { dash.Stroke = Brush(Accent); plus.Foreground = Brush(TextPrimary); label.Foreground = Brush(TextPrimary); };
            button.LostKeyboardFocus += delegate { dash.Stroke = Brush(WithAlpha(TextPrimary, 55)); plus.Foreground = Brush(Muted); label.Foreground = Brush(Muted); };
            button.Click += delegate { AddProfileFromLaunch(); };

            AnimateLaunchCard(button, index, animate);
            return button;
        }

        void AnimateLaunchCard(FrameworkElement card, int index, bool animate)
        {
            if (card == null) return;
            if (!animate)
            {
                card.Opacity = 1;
                return;
            }
            card.Opacity = 0;
            var scale = new ScaleTransform(0.92, 0.92);
            var shift = new TranslateTransform(0, 28);
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(shift);
            card.RenderTransform = group;
            card.RenderTransformOrigin = new Point(0.5, 0.5);

            var delay = TimeSpan.FromMilliseconds(180 + index * 90);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
            {
                BeginTime = delay, EasingFunction = ease
            });
            shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(440))
            {
                BeginTime = delay, EasingFunction = ease
            });
            var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.28 };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(540))
            {
                BeginTime = delay, EasingFunction = pop
            });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(540))
            {
                BeginTime = delay, EasingFunction = pop
            });
        }

        void FocusLaunchProfile()
        {
            if (launchProfileFocus == null && launchProfileCards != null && launchProfileCards.Children.Count > 0)
                launchProfileFocus = launchProfileCards.Children[0] as FrameworkElement;
            if (launchProfileFocus == null) return;
            try
            {
                launchProfileFocus.Focus();
                launchProfileFocus.BringIntoView();
            }
            catch { }
        }

        void AddProfileFromLaunch()
        {
            if (!RequireParentalUnlock()) return;
            var created = ShowProfileDialog(null);
            if (created == null) return;
            if (profiles.Any(p => String.Equals(p.Name, created.Name, StringComparison.OrdinalIgnoreCase)))
            {
                ShowLaunchProfileError("Un profil porte déjà ce nom.");
                return;
            }
            // Comme à la création depuis les réglages : un compte IPTV est
            // obligatoire et reste rattaché à ce profil.
            var account = ShowAccountOnboarding(created);
            if (account == null)
            {
                ShowLaunchProfileError("Création annulée : un compte IPTV est requis.");
                return;
            }
            allSources.Add(account);
            SaveSources();
            profiles.Add(created);
            ProfileStore.Save(profiles);
            ShowLaunchProfileError("");
            RenderLaunchProfileCards(!UiMotion.Reduced);
            FocusLaunchProfile();
        }

        void SelectLaunchProfile(UserProfile profile, FrameworkElement card)
        {
            if (profile == null || launchProfileStage == null) return;
            if (!String.IsNullOrWhiteSpace(profile.PinProtected))
            {
                string pin = PromptPin("Profil protégé", "Saisissez le code PIN du profil « " + profile.Name + " ».");
                if (pin == null) return;
                if (!ProfileStore.VerifyPin(profile, pin))
                {
                    ShowLaunchProfileError("Code PIN incorrect. Réessayez.");
                    ShakeLaunchCard(card);
                    return;
                }
            }
            ShowLaunchProfileError("");
            EnterLaunchProfile(profile, card);
        }

        void ShowLaunchProfileError(string message)
        {
            if (launchProfileError == null) return;
            if (String.IsNullOrWhiteSpace(message))
            {
                launchProfileError.Text = "";
                launchProfileError.Visibility = Visibility.Collapsed;
                return;
            }
            launchProfileError.Text = message;
            launchProfileError.Visibility = Visibility.Visible;
            if (!UiMotion.Reduced)
            {
                launchProfileError.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            }
        }

        void ShakeLaunchCard(FrameworkElement card)
        {
            if (card == null || UiMotion.Reduced) return;
            var shift = card.Tag as TranslateTransform;
            if (shift == null) return;
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(380) };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(-12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(-7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
            shift.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        void EnterLaunchProfile(UserProfile profile, FrameworkElement card)
        {
            if (profile == null || appClosing) return;

            // Les données du profil précédent sont enregistrées avant de basculer :
            // favoris, reprise et historique restent bien séparés.
            try { SaveState(); } catch { }
            activeProfile = profile;
            ProfileScope.Current = profile.Name;
            settings.ActiveProfile = profile.Name;
            parentalUnlocked = false;
            LoadProfileState();
            SaveSettings();
            RebuildSidebar();
            UpdateStatusBar();
            if (channels.Count > 0) ShowStartPage();
            else ShowHome();
            UiSound.Confirm();

            bool animate = !UiMotion.Reduced;
            if (!animate)
            {
                RemoveLaunchOverlay();
                RevealAppContent(false);
                ShowToast("Bienvenue, " + profile.Name + ".", false, true);
                return;
            }

            // Zoom très court sur l'avatar choisi, puis fondu du voile : la
            // transition garde le regard sur la personne qui entre.
            if (card != null)
            {
                var group = card.RenderTransform as TransformGroup;
                var scale = group != null && group.Children.Count > 0 ? group.Children[0] as ScaleTransform : null;
                if (scale != null)
                {
                    var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale.ScaleX, 1.16, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease });
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale.ScaleY, 1.16, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease });
                }
            }

            var overlay = launchOverlay;
            if (overlay != null)
            {
                overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(460))
                {
                    BeginTime = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                });
            }
            RevealAppContent(true);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(660) };
            timer.Tick += delegate
            {
                timer.Stop();
                if (appClosing) return;
                RemoveLaunchOverlay();
                ShowToast("Bienvenue, " + profile.Name + ".", false, true);
            };
            timer.Start();
        }

        void DismissLaunchOverlay()
        {
            if (launchOverlay == null) return;
            bool animate = !UiMotion.Reduced;
            if (!animate)
            {
                RemoveLaunchOverlay();
                RevealAppContent(false);
                return;
            }
            var overlay = launchOverlay;
            overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            });
            RevealAppContent(true);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            timer.Tick += delegate { timer.Stop(); if (!appClosing) RemoveLaunchOverlay(); };
            timer.Start();
        }

        void RevealAppContent(bool animate)
        {
            if (mainScroll == null) return;
            if (!animate || UiMotion.Reduced)
            {
                mainScroll.Opacity = 1;
                return;
            }
            mainScroll.Opacity = 0;
            mainScroll.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(560))
            {
                BeginTime = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        void RemoveLaunchOverlay()
        {
            if (launchProgressTimer != null) { launchProgressTimer.Stop(); launchProgressTimer = null; }
            if (launchStatusTimer != null) { launchStatusTimer.Stop(); launchStatusTimer = null; }
            if (launchOverlay != null && rootGrid != null)
            {
                try { rootGrid.Children.Remove(launchOverlay); } catch { }
            }
            launchOverlay = null;
            launchBrandStage = null;
            launchProfileStage = null;
            launchProfileCards = null;
            launchProfileError = null;
            launchProfileFocus = null;
            if (mainScroll != null) mainScroll.Opacity = 1;
        }
    }
}
