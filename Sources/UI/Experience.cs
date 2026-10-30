using System;
using System.Windows;
using System.Windows.Controls;
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

        /// <summary>
        /// Voile de démarrage : logo, halo animé puis fondu vers l'interface.
        /// Il ne dure qu'un peu plus d'une seconde et se coupe si l'utilisateur
        /// a demandé moins d'animations.
        /// </summary>
        void ShowStartupSplash()
        {
            if (startupSplashShown || rootGrid == null) return;
            startupSplashShown = true;

            var overlay = new Grid { Background = Brush(Back), IsHitTestVisible = true };
            overlay.Children.Add(new Border
            {
                Background = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.42), GradientOrigin = new Point(0.5, 0.42),
                    RadiusX = 0.7, RadiusY = 0.9,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(WithAlpha(Accent, 34), 0),
                        new GradientStop(WithAlpha(Back, 0), 0.52),
                        new GradientStop(Back, 1)
                    }
                }
            });

            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var brandLine = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var logo = new Image
            {
                Source = CreateLogoMark(), Width = 74, Height = 66, Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center
            };
            brandLine.Children.Add(logo);
            var wordmark = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var name = Text("LyaranTV", 36, Brush(TextPrimary), true);
            name.TextWrapping = TextWrapping.NoWrap;
            name.HorizontalAlignment = HorizontalAlignment.Left;
            wordmark.Children.Add(name);
            var tagline = Text("CINÉMA  ·  DIRECT  ·  SÉRIES", 10, Brush(Accent), true);
            tagline.HorizontalAlignment = HorizontalAlignment.Left;
            tagline.Margin = new Thickness(2, 3, 0, 0);
            wordmark.Children.Add(tagline);
            brandLine.Children.Add(wordmark);
            content.Children.Add(brandLine);

            var status = Text("Préparation de votre espace", 10.5, Brush(Muted), false);
            status.HorizontalAlignment = HorizontalAlignment.Center;
            status.Margin = new Thickness(0, 22, 0, 0);
            content.Children.Add(status);

            var track = new Border
            {
                Width = 210, Height = 3, CornerRadius = new CornerRadius(2), Background = Brush(WithAlpha(TextPrimary, 26)),
                Margin = new Thickness(0, 13, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, ClipToBounds = true
            };
            var fill = new Border
            {
                Width = 0, Height = 3, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(WithAlpha(Accent, 120), Accent, 0)
            };
            track.Child = fill;
            content.Children.Add(track);
            overlay.Children.Add(content);
            System.Windows.Controls.Panel.SetZIndex(overlay, 500);
            rootGrid.Children.Add(overlay);

            if (!UiMotion.Reduced)
            {
                var rise = new TranslateTransform(0, 10);
                content.RenderTransform = rise;
                content.Opacity = 0;
                content.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
                rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(450))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
                fill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(0, 210, TimeSpan.FromMilliseconds(1050))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                });
                var ease = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(800)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                logo.BeginAnimation(UIElement.OpacityProperty, ease);
            }

            var close = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(UiMotion.Reduced ? 280 : 1250) };
            close.Tick += delegate
            {
                close.Stop();
                if (!UiMotion.Reduced)
                {
                    var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(280))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                    };
                    fade.Completed += delegate { try { rootGrid.Children.Remove(overlay); } catch { } };
                    overlay.BeginAnimation(UIElement.OpacityProperty, fade);
                }
                else
                {
                    try { rootGrid.Children.Remove(overlay); } catch { }
                }
            };
            close.Start();
        }

        /// <summary>Petit éclat sur une carte mise en avant : ombre et liseré accentué.</summary>
        void Glow(Border border, Color tint, double blur)
        {
            if (border == null || UiMotion.Reduced) return;
            border.Effect = new DropShadowEffect { BlurRadius = blur, ShadowDepth = 8, Opacity = 0.42, Color = tint };
        }
    }
}
