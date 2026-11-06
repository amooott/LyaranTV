using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace LyaranTV
{
    /// <summary>
    /// Affichage des programmes et des étapes de découverte dans l'esprit du
    /// générique de lancement : halos doux, lignes fines, typographie calme et
    /// mouvements lents. Les cartes remplacent les anciens blocs gris.
    /// </summary>
    public sealed partial class MainWindow
    {
        /// <summary>Carte « à l'antenne » : verre teinté, logo en filigrane et progression vivante.</summary>
        Border BuildLiveProgramCard(Channel channel, EpgProgram program)
        {
            var root = new Grid { Height = 126, ClipToBounds = true };
            RoundClip(root, 14);

            var background = new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(Panel, 248), 0),
                    new GradientStop(WithAlpha(Back, 244), 0.58),
                    new GradientStop(WithAlpha(Panel, 240), 1)
                }, new Point(0, 0), new Point(1, 1))
            };
            root.Children.Add(background);

            var halo = new Border
            {
                Width = 190, Height = 190, CornerRadius = new CornerRadius(95),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, -46, 0), IsHitTestVisible = false,
                Background = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.5, RadiusY = 0.5,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(WithAlpha(Accent, 46), 0),
                        new GradientStop(WithAlpha(Secondary, 16), 0.5),
                        new GradientStop(WithAlpha(Back, 0), 1)
                    }
                },
                Opacity = 0.8
            };
            root.Children.Add(halo);

            if (!String.IsNullOrWhiteSpace(channel.Logo))
            {
                root.Children.Add(new AsyncImage
                {
                    Url = channel.Logo, Width = 82, Height = 82, Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 18, 0), Opacity = 0.16, IsHitTestVisible = false
                });
            }

            var content = new StackPanel { Margin = new Thickness(15, 12, 15, 12) };
            var top = new DockPanel { LastChildFill = true };
            var live = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            live.Children.Add(LiveDot());
            var liveText = Text("EN DIRECT", 9.5, Brush(Accent), true);
            liveText.Margin = new Thickness(7, 0, 0, 0);
            live.Children.Add(liveText);
            DockPanel.SetDock(live, Dock.Left);
            top.Children.Add(live);
            var time = Text(program.TimeRange, 10.5, Brush(Muted), false);
            time.HorizontalAlignment = HorizontalAlignment.Right;
            top.Children.Add(time);
            content.Children.Add(top);

            var title = Text(program.Title, 14.5, Brush(TextPrimary), true);
            title.Margin = new Thickness(0, 7, 0, 0);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;
            content.Children.Add(title);

            var channelName = Text(channel.Name, 10.5, Brush(Muted), false);
            channelName.Margin = new Thickness(0, 2, 0, 0);
            channelName.TextTrimming = TextTrimming.CharacterEllipsis;
            content.Children.Add(channelName);

            var progress = StyledProgressBar();
            progress.Height = 4;
            progress.Maximum = Math.Max(1, program.Duration.TotalSeconds);
            progress.Value = Math.Max(0, Math.Min(progress.Maximum, (DateTimeOffset.Now - program.Start).TotalSeconds));
            progress.Margin = new Thickness(0, 10, 0, 0);
            content.Children.Add(progress);
            root.Children.Add(content);

            var card = new Border
            {
                Child = root, Width = 286, Height = 126, Margin = new Thickness(0, 0, 12, 12),
                Background = Brushes.Transparent, BorderBrush = Brush(WithAlpha(Accent, 54)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14),
                Cursor = Cursors.Hand, ClipToBounds = true,
                Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 8, Opacity = 0.24, Color = Colors.Black }
            };
            AddHover(card, Brush(WithAlpha(Panel, 248)), Brush(WithAlpha(Raised, 250)),
                Brush(WithAlpha(Accent, 54)), Brush(WithAlpha(Accent, 150)));
            card.MouseLeftButtonUp += delegate { Play(channel, null, 0, false); };
            card.ToolTip = program.Description;
            return card;
        }

        Border LiveDot()
        {
            var dot = new Border
            {
                Width = 7, Height = 7, CornerRadius = new CornerRadius(4),
                Background = Brush(Color.FromRgb(236, 72, 86)),
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Opacity = 0.8, Color = Color.FromRgb(236, 72, 86) }
            };
            if (!UiMotion.Reduced)
            {
                dot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.45, 1, TimeSpan.FromMilliseconds(900))
                {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
            }
            return dot;
        }

        /// <summary>
        /// Ligne de guide : un filet vertical, l'heure, le titre et, pour le
        /// programme en cours, une progression vivante. Plus de pavé gris.
        /// </summary>
        Border BuildGuideProgramRow(Channel channel, EpgProgram program, bool live, Action click)
        {
            var grid = new Grid { MinHeight = 46, Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var rail = new Border
            {
                Width = 3, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 6, 0, 6),
                Background = live ? Brush(Success) : Brush(WithAlpha(Accent, 80))
            };
            Grid.SetColumn(rail, 0);
            grid.Children.Add(rail);

            var time = Text(program.TimeRange, 11.5, live ? Brush(Success) : Brush(Accent), true);
            time.TextWrapping = TextWrapping.NoWrap;
            time.VerticalAlignment = VerticalAlignment.Center;
            time.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(time, 1);
            grid.Children.Add(time);

            var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
            titlePanel.Children.Add(Text(program.Title, 12.5, Brush(TextPrimary), live));
            if (!String.IsNullOrWhiteSpace(program.Description))
            {
                var desc = Text(TextUtil.Ellipsis(program.Description, 220), 12, Brush(Muted), false);
                desc.TextWrapping = TextWrapping.NoWrap;
                desc.TextTrimming = TextTrimming.CharacterEllipsis;
                desc.Margin = new Thickness(0, 1, 0, 0);
                titlePanel.Children.Add(desc);
            }
            if (live)
            {
                var progress = StyledProgressBar();
                progress.Height = 3;
                progress.Maximum = Math.Max(1, program.Duration.TotalSeconds);
                progress.Value = Math.Max(0, Math.Min(progress.Maximum, (DateTimeOffset.Now - program.Start).TotalSeconds));
                progress.Margin = new Thickness(0, 7, 0, 0);
                titlePanel.Children.Add(progress);
            }
            Grid.SetColumn(titlePanel, 2);
            grid.Children.Add(titlePanel);

            if (live)
            {
                var badge = new Border
                {
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Background = Brush(WithAlpha(Success, 26)), BorderBrush = Brush(WithAlpha(Success, 80)),
                    BorderThickness = new Thickness(1), Child = Text("EN COURS", 9, Brush(Success), true)
                };
                Grid.SetColumn(badge, 3);
                grid.Children.Add(badge);
            }

            var row = new Border
            {
                Child = grid, Background = live ? Brush(WithAlpha(Success, 16)) : Brush(Raised),
                BorderBrush = live ? Brush(WithAlpha(Success, 56)) : Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9), Padding = new Thickness(8, 3, 10, 3),
                Cursor = channel == null ? Cursors.Arrow : Cursors.Hand,
                ToolTip = String.IsNullOrWhiteSpace(program.Description) ? program.TimeRange : program.Description
            };
            if (channel != null && click != null)
            {
                row.MouseEnter += delegate
                {
                    row.Background = Brush(WithAlpha(Accent, 18));
                    row.BorderBrush = Brush(WithAlpha(Accent, 100));
                };
                row.MouseLeave += delegate
                {
                    row.Background = live ? Brush(WithAlpha(Success, 16)) : Brush(Raised);
                    row.BorderBrush = live ? Brush(WithAlpha(Success, 56)) : Brush(BorderColor);
                };
                row.MouseLeftButtonUp += delegate { click(); };
            }
            return row;
        }

        /// <summary>
        /// Étape de découverte : grande numérotation, halo et ligne de progression.
        /// L'ensemble rappelle l'écran de chargement sans le copier.
        /// </summary>
        FrameworkElement BuildFeatureTile(string number, string glyph, string title, string description)
        {
            var root = new Grid { Height = 164, ClipToBounds = true };
            RoundClip(root, 16);
            root.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(Panel, 246), 0),
                    new GradientStop(WithAlpha(Back, 242), 0.64),
                    new GradientStop(WithAlpha(Panel, 238), 1)
                }, new Point(0, 0), new Point(1, 1))
            });
            var halo = new Border
            {
                Width = 220, Height = 220, CornerRadius = new CornerRadius(110),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -100, -70, 0), IsHitTestVisible = false,
                Background = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.5, RadiusY = 0.5,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(WithAlpha(Accent, 54), 0),
                        new GradientStop(WithAlpha(Secondary, 18), 0.55),
                        new GradientStop(WithAlpha(Back, 0), 1)
                    }
                },
                Opacity = 0.9
            };
            root.Children.Add(halo);

            var content = new Grid { Margin = new Thickness(18, 15, 18, 14) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var numberText = Text(number, 32, Brush(WithAlpha(TextPrimary, 54)), true);
            numberText.VerticalAlignment = VerticalAlignment.Top;
            head.Children.Add(numberText);
            var iconHost = new Border
            {
                Width = 38, Height = 38, CornerRadius = new CornerRadius(11),
                Background = AccentSoftGradientBrush(), BorderBrush = Brush(WithAlpha(Accent, 90)),
                BorderThickness = new Thickness(1), Child = Icon(glyph, 17, Brush(Accent)),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(iconHost, 1);
            head.Children.Add(iconHost);
            Grid.SetRow(head, 0);
            content.Children.Add(head);

            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            labels.Children.Add(Text(title, 15, Brush(TextPrimary), true));
            var desc = Text(description, 11.5, Brush(Muted), false);
            desc.TextWrapping = TextWrapping.Wrap;
            desc.Margin = new Thickness(0, 4, 0, 0);
            desc.MaxWidth = 230;
            labels.Children.Add(desc);
            Grid.SetRow(labels, 1);
            content.Children.Add(labels);

            var track = new Border
            {
                Height = 2, CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 12, 0, 0),
                Background = Brush(WithAlpha(TextPrimary, 24)), ClipToBounds = true
            };
            var fill = new Border
            {
                Height = 2, HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(WithAlpha(Accent, 90), Accent, 0)
            };
            double index = 1;
            Double.TryParse(number, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out index);
            double step = Math.Max(0.28, Math.Min(1, index / 3.0));
            fill.Width = 0;
            track.Child = fill;
            Grid.SetRow(track, 2);
            content.Children.Add(track);
            root.Children.Add(content);

            var tile = new Border
            {
                Child = root, Width = 286, Height = 164, Margin = new Thickness(0, 0, 12, 12),
                Background = Brushes.Transparent, BorderBrush = Brush(WithAlpha(BorderColor, 170)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16),
                Cursor = Cursors.Hand, ClipToBounds = true,
                Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 8, Opacity = 0.2, Color = Colors.Black }
            };
            AddHover(tile, Brush(WithAlpha(Panel, 246)), Brush(WithAlpha(Raised, 250)),
                Brush(WithAlpha(BorderColor, 170)), Brush(WithAlpha(Accent, 140)));
            if (!UiMotion.Reduced)
            {
                var grow = new DoubleAnimation(0, 250 * step, TimeSpan.FromMilliseconds(720))
                {
                    BeginTime = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                fill.BeginAnimation(FrameworkElement.WidthProperty, grow);
            }
            else fill.Width = 250 * step;
            return tile;
        }
    }
}
