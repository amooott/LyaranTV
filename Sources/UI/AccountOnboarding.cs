using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace LyaranTV
{
    /// <summary>
    /// Étape obligatoire à la création d'un profil : connecter au moins un
    /// compte IPTV. Chaque profil garde ensuite ses propres comptes, sans
    /// mélange avec les autres personnes de la maison.
    /// </summary>
    public sealed partial class MainWindow
    {
        /// <summary>
        /// Ouvre l'assistant de connexion. Retourne le compte créé, ou null si
        /// la personne annule — dans ce cas le profil n'est pas créé.
        /// </summary>
        PlaylistSource ShowAccountOnboarding(UserProfile profile)
        {
            if (profile == null) return null;
            PlaylistSource created = null;

            var dialog = new Window
            {
                Title = "Connecter un compte IPTV",
                Width = 840, Height = 566, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                WindowStyle = WindowStyle.None, Background = Brushes.Transparent, AllowsTransparency = true,
                FontFamily = new FontFamily("Inter, Segoe UI"), ShowInTaskbar = false
            };

            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel),
                BorderBrush = Brush(WithAlpha(Accent, 120)), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24), ClipToBounds = true,
                Opacity = 0, RenderTransformOrigin = new Point(0.5, 0.5),
                Effect = ModalShadow()
            };
            var frameScale = new ScaleTransform(0.97, 0.97);
            frame.RenderTransform = frameScale;
            dialog.Content = frame;

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            frame.Child = layout;

            // ------------------------------------------------------------------
            // Panneau visuel : halo de démarrage, logo et progression.
            // ------------------------------------------------------------------
            var visual = new Grid { ClipToBounds = true };
            visual.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(Accent, 120), 0),
                    new GradientStop(WithAlpha(Back, 250), 0.62),
                    new GradientStop(WithAlpha(Secondary, 54), 1)
                }, new Point(0, 0), new Point(1, 1))
            });
            var halo = new Border
            {
                Width = 420, Height = 420, CornerRadius = new CornerRadius(210),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Background = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.5, RadiusY = 0.5,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(WithAlpha(Accent, 96), 0),
                        new GradientStop(WithAlpha(Secondary, 34), 0.5),
                        new GradientStop(WithAlpha(Back, 0), 1)
                    }
                },
                Opacity = 0.5, IsHitTestVisible = false
            };
            visual.Children.Add(halo);
            var ring = new System.Windows.Shapes.Ellipse
            {
                Width = 230, Height = 230, Stroke = Brush(WithAlpha(Colors.White, 26)), StrokeThickness = 1,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            visual.Children.Add(ring);

            var brand = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var logo = new Image
            {
                Source = CreateLogoMark(), Width = 88, Height = 78, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                Effect = new DropShadowEffect { BlurRadius = 34, ShadowDepth = 0, Opacity = 0.55, Color = Accent }
            };
            brand.Children.Add(logo);
            var name = Text("LyaranTV", 22, Brush(TextPrimary), true);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.Margin = new Thickness(0, 12, 0, 0);
            brand.Children.Add(name);
            var tag = Text("UN COMPTE PAR PROFIL", 9.5, Brush(Accent), true);
            tag.HorizontalAlignment = HorizontalAlignment.Center;
            tag.Margin = new Thickness(0, 7, 0, 0);
            brand.Children.Add(tag);
            visual.Children.Add(brand);

            var steps = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 30)
            };
            steps.Children.Add(StepDot(true));
            steps.Children.Add(StepDot(false));
            steps.Children.Add(StepDot(false));
            visual.Children.Add(steps);

            Grid.SetColumn(visual, 0);
            layout.Children.Add(visual);

            // ------------------------------------------------------------------
            // Formulaire : choix du type de compte.
            // ------------------------------------------------------------------
            var right = new Grid { Margin = new Thickness(30, 26, 30, 24) };
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var headStack = new StackPanel();
            headStack.Children.Add(Text("Étape 2 sur 2", 10, Brush(Accent), true));
            headStack.Children.Add(Text("Connectez votre compte", 24, Brush(TextPrimary), true));
            var sub = Text("Le profil « " + profile.Name + " » garde ses propres comptes IPTV. Aucun contenu n'est partagé avec les autres profils.", 12, Brush(Muted), false);
            sub.TextWrapping = TextWrapping.Wrap;
            sub.MaxWidth = 430;
            sub.Margin = new Thickness(0, 6, 0, 0);
            headStack.Children.Add(sub);
            head.Children.Add(headStack);
            var close = IconButton(Glyphs.Close, "Annuler la création du profil", delegate { dialog.DialogResult = false; }, false);
            close.Width = 32; close.Height = 30; close.Margin = new Thickness(0);
            close.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(close, 1);
            head.Children.Add(close);
            Grid.SetRow(head, 0);
            right.Children.Add(head);

            var tiles = new UniformGrid { Columns = 2, Rows = 2, Margin = new Thickness(0, 20, 0, 12) };
            var tileList = new System.Collections.Generic.List<FrameworkElement>();
            Action<PlaylistSource> finish = delegate(PlaylistSource source)
            {
                if (source == null) return;
                source.Profile = profile.Name;
                created = source;
                dialog.DialogResult = true;
            };
            tiles.Children.Add(AccountTile(Glyphs.Link, "M3U / M3U8", "Un lien ou un fichier de playlist.", delegate
            {
                var source = ShowSourceDialog(null, dialog, "M3U");
                if (source != null) finish(source);
            }, tileList));
            tiles.Children.Add(AccountTile(Glyphs.Admin, "Xtream Codes", "Serveur, identifiant et mot de passe.", delegate
            {
                var source = ShowSourceDialog(null, dialog, "Xtream");
                if (source != null) finish(source);
            }, tileList));
            tiles.Children.Add(AccountTile(Glyphs.TV, "Portail MAC", "Portail Stalker et adresse MAC.", delegate
            {
                var source = ShowSourceDialog(null, dialog, "Portail MAC");
                if (source != null) finish(source);
            }, tileList));
            tiles.Children.Add(AccountTile(Glyphs.Play, "Démonstration", "Découvrir LyaranTV sans compte.", delegate
            {
                finish(new PlaylistSource { Name = "Démo LyaranTV", Type = "Démo", IsDemo = true });
            }, tileList));
            Grid.SetRow(tiles, 1);
            right.Children.Add(tiles);

            var footer = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 8, 0, 0) };
            var cancel = TextAction("Annuler la création", delegate { dialog.DialogResult = false; }, "Le profil ne sera pas créé sans compte IPTV");
            cancel.Margin = new Thickness(0);
            DockPanel.SetDock(cancel, Dock.Left);
            footer.Children.Add(cancel);
            var note = Text("Un compte est requis pour créer un profil.", 11, Brush(Accent), false);
            note.HorizontalAlignment = HorizontalAlignment.Right;
            note.VerticalAlignment = VerticalAlignment.Center;
            footer.Children.Add(note);
            Grid.SetRow(footer, 2);
            right.Children.Add(footer);

            Grid.SetColumn(right, 1);
            layout.Children.Add(right);

            // Entrée dans le dialogue : fondu, zoom et apparition décalée des tuiles.
            if (!UiMotion.Reduced)
            {
                frame.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
                var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };
                frameScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(360)) { EasingFunction = pop });
                frameScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(360)) { EasingFunction = pop });
                halo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.42, 0.85, TimeSpan.FromMilliseconds(2200))
                {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
                for (int i = 0; i < tileList.Count; i++)
                {
                    var tile = tileList[i];
                    tile.Opacity = 0;
                    var shift = new TranslateTransform(0, 14);
                    tile.RenderTransform = shift;
                    var start = TimeSpan.FromMilliseconds(120 + i * 70);
                    tile.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)) { BeginTime = start });
                    shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(340))
                    {
                        BeginTime = start, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                }
            }
            else
            {
                frame.Opacity = 1;
                foreach (var tile in tileList) tile.Opacity = 1;
            }

            // Échap annule proprement : aucun profil sans compte.
            dialog.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { dialog.DialogResult = false; e.Handled = true; }
            };

            GlassRun(dialog, delegate { return dialog.ShowDialog(); });
            return created;
        }

        Border StepDot(bool active)
        {
            return new Border
            {
                Width = active ? 22 : 7, Height = 7, CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 6, 0),
                Background = active ? AccentGradientBrush() : Brush(WithAlpha(Colors.White, 46))
            };
        }

        /// <summary>Tuile de type de compte : icône, titre, description, survol animé.</summary>
        Border AccountTile(string glyph, string title, string description, Action click, System.Collections.Generic.List<FrameworkElement> animate)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var iconHost = new Border
            {
                Width = 38, Height = 38, CornerRadius = new CornerRadius(11), VerticalAlignment = VerticalAlignment.Top,
                Background = AccentSoftGradientBrush(), BorderBrush = Brush(WithAlpha(Accent, 90)), BorderThickness = new Thickness(1),
                Child = Icon(glyph, 17, Brush(Accent))
            };
            Grid.SetColumn(iconHost, 0);
            grid.Children.Add(iconHost);
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(Text(title, 14, Brush(TextPrimary), true));
            var desc = Text(description, 11, Brush(Muted), false);
            desc.TextWrapping = TextWrapping.Wrap;
            desc.Margin = new Thickness(0, 3, 0, 0);
            stack.Children.Add(desc);
            Grid.SetColumn(stack, 1);
            grid.Children.Add(stack);

            var button = new Button
            {
                Content = grid, Height = 104, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(14, 10, 12, 10),
                Background = Brush(Raised), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, FocusVisualStyle = null, Template = RoundedButtonTemplate(13),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center
            };
            button.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, title);
            button.Click += delegate { click(); };
            button.MouseEnter += delegate { iconHost.Background = AccentGradientBrush(); };
            button.MouseLeave += delegate { iconHost.Background = AccentSoftGradientBrush(); };
            var host = new Border { Child = button, Margin = new Thickness(0, 0, 10, 10) };
            if (animate != null) animate.Add(host);
            return host;
        }
    }
}
