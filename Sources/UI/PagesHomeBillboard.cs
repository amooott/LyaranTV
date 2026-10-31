using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// Bannière d'accueil plein cadre : un contenu mis en avant, sa photo, ses
    /// informations et une rotation douce entre plusieurs propositions. C'est la
    /// pièce centrale de l'interface, pensée comme l'écran d'un service de streaming.
    /// </summary>
    public sealed partial class MainWindow
    {
        DispatcherTimer homeHeroTimer;
        readonly List<Channel> homeHeroItems = new List<Channel>();
        int homeHeroIndex;
        AsyncImage homeHeroImage, homeHeroLogo;
        Border homeHeroPosterFrame;
        TextBlock homeHeroKicker, homeHeroTitle, homeHeroMeta, homeHeroDescription, homeHeroGlyph;
        StackPanel homeHeroActions, homeHeroDots;
        Grid homeHeroGrid;
        Channel homeHeroChannel;

        const double BillboardHeight = 486;

        FrameworkElement BuildHomeBillboard()
        {
            homeHeroItems.Clear();
            foreach (var item in BuildResumeItems(6)) AddHomeHeroItem(item);
            if (homeHeroItems.Count < 3)
            {
                foreach (var item in HomeHeroCandidates().Take(5)) AddHomeHeroItem(item);
            }
            if (homeHeroItems.Count == 0)
            {
                foreach (var item in VisibleChannels.Where(c => c.Kind == "Chaînes").Take(3)) AddHomeHeroItem(item);
            }

            homeHeroGrid = new Grid { Height = BillboardHeight, ClipToBounds = true, Background = Brush(Back) };
            homeHeroGrid.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(Accent, 74), 0),
                    new GradientStop(Color.FromRgb(9, 10, 15), 0.55),
                    new GradientStop(Back, 1)
                }, new Point(0.1, 0), new Point(0.9, 1))
            });

            // Fond dessiné immédiatement : même avant le chargement de la photo,
            // la bannière a une présence visuelle et ne montre jamais un cadre noir.
            var fallbackBackdrop = new Grid { IsHitTestVisible = false };
            fallbackBackdrop.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(24, 20, 42), 0),
                    new GradientStop(Color.FromRgb(14, 14, 22), 0.55),
                    new GradientStop(Color.FromRgb(8, 9, 13), 1)
                }, new Point(0, 0), new Point(1, 1))
            });
            fallbackBackdrop.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 640, Height = 640, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -260, -120, 0),
                Fill = new RadialGradientBrush(WithAlpha(Accent, 90), WithAlpha(Accent, 0))
            });
            fallbackBackdrop.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 420, Height = 420, Stroke = Brush(WithAlpha(Accent, 52)), StrokeThickness = 1,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 90, 40), IsHitTestVisible = false
            });
            homeHeroGrid.Children.Add(fallbackBackdrop);

            homeHeroImage = new AsyncImage { Stretch = Stretch.UniformToFill, Opacity = 0, IsHitTestVisible = false };
            homeHeroGrid.Children.Add(homeHeroImage);
            // Voile sombre sur la photo : même une affiche claire reste cinéma.
            homeHeroGrid.Children.Add(new Border { Background = Brush(Color.FromArgb(96, 4, 5, 8)), IsHitTestVisible = false });

            homeHeroGlyph = new TextBlock
            {
                FontSize = 118, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Opacity = 0.075,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 56, 30), MaxWidth = 480, TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap, LineHeight = 108, IsHitTestVisible = false
            };
            homeHeroGrid.Children.Add(homeHeroGlyph);

            homeHeroLogo = new AsyncImage
            {
                Stretch = Stretch.Uniform, MaxWidth = 250, MaxHeight = 130, Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 100, 40), IsHitTestVisible = false
            };
            homeHeroGrid.Children.Add(homeHeroLogo);

            // Affiche portrait : quand seule une affiche est disponible, elle est
            // présentée entière à droite plutôt que découpée en bandeau.
            homeHeroPosterFrame = new Border
            {
                Width = 232, Height = 348, CornerRadius = new CornerRadius(13), ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 78, 18), BorderBrush = Brush(Color.FromArgb(78, 255, 255, 255)),
                BorderThickness = new Thickness(1), Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect { BlurRadius = 38, ShadowDepth = 14, Opacity = 0.55, Color = Colors.Black },
                Child = new Grid()
            };
            homeHeroGrid.Children.Add(homeHeroPosterFrame);

            // Voile gauche puis fondu vers le bas : le texte reste lisible quelle
            // que soit la photo, et la page se fond dans le fond de l'application.
            homeHeroGrid.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(Back, 246), 0),
                    new GradientStop(WithAlpha(Back, 186), 0.38),
                    new GradientStop(WithAlpha(Back, 40), 0.72),
                    new GradientStop(WithAlpha(Back, 0), 1)
                }, new Point(0, 0.5), new Point(1, 0.5))
            });
            homeHeroGrid.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.3),
                    new GradientStop(WithAlpha(Back, 110), 0.72),
                    new GradientStop(Back, 1)
                }, new Point(0.5, 0), new Point(0.5, 1))
            });
            homeHeroGrid.Children.Add(new Border
            {
                Height = 1, VerticalAlignment = VerticalAlignment.Bottom,
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(Accent, 0), 0),
                    new GradientStop(WithAlpha(Accent, 120), 0.5),
                    new GradientStop(WithAlpha(Accent, 0), 1)
                }, new Point(0, 0.5), new Point(1, 0.5))
            });

            var content = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(38, 30, 38, 48), MaxWidth = 760
            };

            var kickerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 9) };
            kickerRow.Children.Add(new Border
            {
                Width = 22, Height = 2, Background = Brush(Accent), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 9, 0)
            });
            homeHeroKicker = Text("", 10, Brush(Accent), true);
            homeHeroKicker.TextWrapping = TextWrapping.NoWrap;
            kickerRow.Children.Add(homeHeroKicker);
            content.Children.Add(kickerRow);

            homeHeroTitle = Text("", 43, Brushes.White, true);
            homeHeroTitle.LineHeight = 46;
            homeHeroTitle.MaxWidth = 720;
            homeHeroTitle.Margin = new Thickness(0, 0, 0, 10);
            homeHeroTitle.Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 3, Opacity = 0.35, Color = Colors.Black };
            content.Children.Add(homeHeroTitle);

            homeHeroMeta = Text("", 12, Brush(Color.FromArgb(225, 255, 255, 255)), true);
            homeHeroMeta.MaxWidth = 700;
            homeHeroMeta.Margin = new Thickness(0, 0, 0, 8);
            content.Children.Add(homeHeroMeta);

            homeHeroDescription = Text("", 13, Brush(Color.FromArgb(200, 255, 255, 255)), false);
            homeHeroDescription.MaxWidth = 640;
            homeHeroDescription.LineHeight = 19;
            homeHeroDescription.MaxHeight = 58;
            homeHeroDescription.Margin = new Thickness(0, 0, 0, 18);
            content.Children.Add(homeHeroDescription);

            homeHeroActions = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(homeHeroActions);
            homeHeroGrid.Children.Add(content);

            homeHeroDots = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 38, 30)
            };
            homeHeroGrid.Children.Add(homeHeroDots);

            // Flèches du défilé : on parcourt les recommandations sans attendre
            // la rotation automatique, comme sur un carrousel de streaming.
            // Deux pastilles rondes en haut à droite : discrètes, toujours accessibles
            // et jamais posées sur le titre ni sur les boutons d'action.
            var previous = BillboardArrow(Glyphs.ChevronLeft, false);
            previous.HorizontalAlignment = HorizontalAlignment.Right;
            previous.VerticalAlignment = VerticalAlignment.Top;
            previous.Margin = new Thickness(0, 24, 66, 0);
            previous.Click += delegate { StepHomeHero(-1); };
            System.Windows.Controls.Panel.SetZIndex(previous, 40);
            homeHeroGrid.Children.Add(previous);
            var next = BillboardArrow(Glyphs.ChevronRight, true);
            next.HorizontalAlignment = HorizontalAlignment.Right;
            next.VerticalAlignment = VerticalAlignment.Top;
            next.Margin = new Thickness(0, 24, 16, 0);
            next.Click += delegate { StepHomeHero(1); };
            System.Windows.Controls.Panel.SetZIndex(next, 40);
            homeHeroGrid.Children.Add(next);
            homeHeroNextButton = next;
            homeHeroPreviousButton = previous;

            var frame = new Border { Child = homeHeroGrid, ClipToBounds = true };

            if (homeHeroItems.Count > 0)
            {
                int start = 0;
                Channel last = LastPlayedChannel();
                if (last != null)
                {
                    int found = homeHeroItems.FindIndex(c => String.Equals(c.Id, last.Id, StringComparison.OrdinalIgnoreCase));
                    if (found >= 0) start = found;
                }
                SetHomeHero(start, false);
            }
            else
            {
                homeHeroTitle.Text = "Tout commence\npar votre playlist.";
                homeHeroKicker.Text = "LYARANTV";
                homeHeroMeta.Text = "IPTV, films, séries et guide TV réunis";
                homeHeroDescription.Text = "Ajoutez une playlist M3U, une connexion Xtream ou un portail MAC pour retrouver vos chaînes, vos films et vos séries dans une interface pensée pour le grand écran.";
                homeHeroGlyph.Text = "LYARAN";
                homeHeroGlyph.Opacity = 0.05;
                homeHeroActions.Children.Add(BillboardButton(Glyphs.Add, "Ajouter une playlist", AddPlaylist, true));
                homeHeroActions.Children.Add(BillboardButton(Glyphs.Play, "Découvrir la démo", LoadDemo, false));
            }
            return frame;
        }

        void AddHomeHeroItem(Channel channel)
        {
            if (channel == null) return;
            if (homeHeroItems.Any(c => String.Equals(c.Id, channel.Id, StringComparison.OrdinalIgnoreCase))) return;
            homeHeroItems.Add(channel);
        }

        void SetHomeHero(int index, bool userDriven)
        {
            if (homeHeroItems.Count == 0)
            {
                UpdateHomeHeroDots();
                return;
            }
            if (index < 0 || index >= homeHeroItems.Count) index = 0;
            homeHeroIndex = index;
            Channel channel = homeHeroItems[index];
            homeHeroChannel = channel;

            string kicker;
            if (channel.Kind == "Chaînes") kicker = "EN DIRECT";
            else if (!String.IsNullOrWhiteSpace(channel.Season) || channel.Kind == "Épisodes") kicker = "REPRENDRE LA SÉRIE";
            else if (GetSavedPosition(channel) > 1) kicker = "REPRENDRE LA LECTURE";
            else if (channel.Kind == "Films") kicker = "FILM";
            else if (channel.Kind == "Séries") kicker = "SÉRIE";
            else kicker = "À REGARDER";
            if (!String.IsNullOrWhiteSpace(channel.Genres)) kicker += "  ·  " + TextUtil.Ellipsis(channel.Genres, 38).ToUpperInvariant();
            else if (!String.IsNullOrWhiteSpace(channel.Group)) kicker += "  ·  " + TextUtil.Ellipsis(channel.Group, 34).ToUpperInvariant();
            homeHeroKicker.Text = kicker;
            homeHeroTitle.Text = channel.Name;
            homeHeroGlyph.Text = TextUtil.Ellipsis(channel.Name.ToUpperInvariant(), 28);

            var meta = new List<string>();
            if (!String.IsNullOrWhiteSpace(channel.Year)) meta.Add(channel.Year);
            if (channel.TmdbRating > 0) meta.Add("★ " + channel.TmdbRating.ToString("0.0", CultureInfo.InvariantCulture));
            if (!String.IsNullOrWhiteSpace(channel.Season))
            {
                string episode = "S" + channel.Season;
                if (!String.IsNullOrWhiteSpace(channel.Episode)) episode += " : E" + channel.Episode;
                meta.Add(episode);
            }
            if (!String.IsNullOrWhiteSpace(channel.Runtime)) meta.Add(channel.Runtime);
            if (channel.Kind == "Chaînes")
            {
                var now = CurrentProgram(channel);
                if (now != null) meta.Add("À l'antenne · " + now.Title);
                else meta.Add(String.IsNullOrWhiteSpace(channel.Group) ? "Direct" : channel.Group);
            }
            if (meta.Count == 0) meta.Add(KindLabel(channel.Kind));
            homeHeroMeta.Text = String.Join("   ·   ", meta.ToArray());

            string description = String.IsNullOrWhiteSpace(channel.Overview) ? channel.Description : channel.Overview;
            if (String.IsNullOrWhiteSpace(description)) description = "Aucun synopsis disponible. Ouvrez la fiche pour en savoir plus et lancer la lecture.";
            homeHeroDescription.Text = TextUtil.Ellipsis(description, 260);

            bool hasBackdrop = !String.IsNullOrWhiteSpace(channel.BackdropUrl);
            bool hasPoster = !String.IsNullOrWhiteSpace(channel.PosterUrl) || !String.IsNullOrWhiteSpace(channel.StillUrl);
            // Bannière authentique : le visuel large du contenu quand il existe,
            // sinon son affiche recadrée en bandeau. Le défilé n'affiche plus un
            // simple dégradé mais bien la vraie image du film ou de la série.
            string image = hasBackdrop ? channel.BackdropUrl
                : (!String.IsNullOrWhiteSpace(channel.StillUrl) ? channel.StillUrl : (hasPoster ? channel.PosterUrl : ""));
            bool hasImage = !String.IsNullOrWhiteSpace(image);
            // Sans photo panoramique, l'affiche verticale ne doit pas être
            // étirée : elle sert de fond flou accordé à l'œuvre, et sa version
            // nette reste présentée à droite comme sur une fiche de film.
            bool posterOnly = hasImage && !hasBackdrop && !String.IsNullOrWhiteSpace(channel.PosterUrl);
            homeHeroImage.Url = image;
            homeHeroImage.Effect = posterOnly ? (Effect)new System.Windows.Media.Effects.BlurEffect { Radius = 34 } : null;
            homeHeroImage.Stretch = Stretch.UniformToFill;
            homeHeroImage.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
            bool content = channel.Kind == "Films" || channel.Kind == "Séries"
                || channel.Kind == "Épisodes" || !String.IsNullOrWhiteSpace(channel.Season);
            // L'affiche sert déjà de bannière : la vignette portrait ferait doublon.
            // Quand aucun visuel n'est encore connu, la jaquette dessinée (ou le
            // logo de la chaîne) prend sa place plutôt que de laisser un vide.
            homeHeroPosterFrame.Child = content && (!hasImage || posterOnly) ? (UIElement)CoverArt(channel, 232, 348, false) : new Grid();
            homeHeroPosterFrame.Visibility = content && (!hasImage || posterOnly) ? Visibility.Visible : Visibility.Collapsed;
            homeHeroLogo.Url = hasImage ? null : channel.Logo;
            homeHeroLogo.Visibility = hasImage || content || String.IsNullOrWhiteSpace(channel.Logo) ? Visibility.Collapsed : Visibility.Visible;
            homeHeroGlyph.Visibility = String.IsNullOrWhiteSpace(channel.Name) ? Visibility.Collapsed : Visibility.Visible;

            if (hasImage)
            {
                homeHeroImage.Opacity = 0;
                UiMotion.Animate(homeHeroImage, UIElement.OpacityProperty, posterOnly ? 0.52 : 0.86, 420);
            }
            if (homeHeroLogo.Visibility == Visibility.Visible)
            {
                homeHeroLogo.Opacity = 0;
                UiMotion.Animate(homeHeroLogo, UIElement.OpacityProperty, 0.95, 320);
            }

            // La force de l'image : un léger zoom, comme une photo qui s'éveille.
            if (!UiMotion.Reduced && homeHeroImage.Visibility == Visibility.Visible)
            {
                var scale = new ScaleTransform(1.0, 1.0);
                homeHeroImage.RenderTransformOrigin = new Point(0.5, 0.5);
                homeHeroImage.RenderTransform = scale;
                var grow = new DoubleAnimation(1.06, 1.0, TimeSpan.FromSeconds(7))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            }

            homeHeroActions.Children.Clear();
            double saved = GetSavedPosition(channel);
            bool series = channel.IsSeries || (channel.Kind == "Séries" && String.IsNullOrWhiteSpace(channel.Url));
            bool needsInfo = series || (channel.Kind == "Films" && String.IsNullOrWhiteSpace(channel.Url));
            string primaryLabel = series ? "Voir les épisodes"
                : saved > 1 ? "Reprendre · " + TextUtil.FormatPosition(TimeSpan.FromSeconds(saved))
                : "Lecture";
            Channel captured = channel;
            double capturedSaved = saved;
            bool capturedInfo = needsInfo;
            var play = BillboardButton(Glyphs.Play, primaryLabel, delegate
            {
                if (capturedInfo) ShowChannelInfo(captured);
                else Play(captured, homeHeroItems, capturedSaved, false);
            }, true);
            homeHeroActions.Children.Add(play);

            if (!needsInfo && channel.Kind != "Chaînes")
            {
                homeHeroActions.Children.Add(BillboardButton(Glyphs.Info, "Plus d'infos", delegate { ShowChannelInfo(captured); }, false));
            }
            if (channel.Kind == "Séries" || channel.IsSeries)
            {
                homeHeroActions.Children.Add(BillboardButton(Glyphs.List, "Épisodes", delegate { LoadSeriesEpisodes(captured); }, false));
            }

            var favorite = new Button
            {
                Content = Icon(favorites.Contains(channel.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite, 18, Brushes.White),
                Width = 46, Height = 46, Margin = new Thickness(2, 0, 0, 0), Padding = new Thickness(0),
                Background = Brush(Color.FromArgb(68, 18, 20, 26)),
                BorderBrush = Brush(Color.FromArgb(120, 255, 255, 255)), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, FocusVisualStyle = null, Template = RoundedButtonTemplate(23),
                ToolTip = favorites.Contains(channel.Id) ? "Retirer de ma liste" : "Ajouter à ma liste"
            };
            favorite.Click += delegate { ToggleFavorite(captured); SetHomeHero(homeHeroIndex, true); };
            homeHeroActions.Children.Add(favorite);

            UpdateHomeHeroDots();
            if (!UiMotion.Reduced)
            {
                var slide = new TranslateTransform(0, 10);
                contentOfHero().RenderTransform = slide;
                contentOfHero().BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(280)));
                slide.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            }
            if (userDriven) StartHomeHeroTimer();
        }

        FrameworkElement contentOfHero()
        {
            // La pile de texte est le dernier enfant avant les points.
            foreach (UIElement child in homeHeroGrid.Children)
            {
                var panel = child as StackPanel;
                if (panel != null && panel.Children.Contains(homeHeroTitle)) return panel;
            }
            return homeHeroTitle;
        }

        Button homeHeroNextButton, homeHeroPreviousButton;

        /// <summary>Flèche ronde du défilé, discrète puis nette au survol.</summary>
        Button BillboardArrow(string glyph, bool forward)
        {
            var button = new Button
            {
                Content = Icon(glyph, 18, Brushes.White), Width = 42, Height = 42, Padding = new Thickness(0),
                Background = Brush(Color.FromArgb(150, 8, 9, 13)),
                BorderBrush = Brush(Color.FromArgb(76, 255, 255, 255)), BorderThickness = new Thickness(1),
                Template = RoundedButtonTemplate(21), Cursor = Cursors.Hand, FocusVisualStyle = null,
                IsTabStop = false, Opacity = 0.62, ToolTip = forward ? "Recommandation suivante" : "Recommandation précédente"
            };
            button.MouseEnter += delegate
            {
                UiMotion.Animate(button, UIElement.OpacityProperty, 1, 120);
                button.Background = Brush(Color.FromArgb(214, 18, 19, 26));
            };
            button.MouseLeave += delegate
            {
                UiMotion.Animate(button, UIElement.OpacityProperty, 0.62, 160);
                button.Background = Brush(Color.FromArgb(150, 8, 9, 13));
            };
            return button;
        }

        /// <summary>Passe à la recommandation suivante ou précédente du carrousel.</summary>
        void StepHomeHero(int delta)
        {
            if (homeHeroItems.Count == 0 || delta == 0) return;
            int next = homeHeroIndex + delta;
            if (next < 0) next = homeHeroItems.Count - 1;
            if (next >= homeHeroItems.Count) next = 0;
            SetHomeHero(next, true);
        }

        void UpdateHomeHeroDots()
        {
            if (homeHeroDots == null) return;
            homeHeroDots.Children.Clear();
            for (int i = 0; i < homeHeroItems.Count; i++)
            {
                int captured = i;
                bool active = i == homeHeroIndex;
                var dot = new Button
                {
                    Width = active ? 24 : 8, Height = 8, Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 6, 0), Background = Brush(active ? Colors.White : Color.FromArgb(90, 255, 255, 255)),
                    BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FocusVisualStyle = null,
                    Template = RoundedButtonTemplate(4), ToolTip = homeHeroItems[i].Name
                };
                dot.Click += delegate { SetHomeHero(captured, true); };
                homeHeroDots.Children.Add(dot);
            }
        }

        Button BillboardButton(string glyph, string label, Action click, bool light)
        {
            var content = IconText(glyph, label, light ? Brush(Color.FromRgb(9, 10, 14)) : Brushes.White, 13);
            var button = new Button
            {
                Content = content, Height = 46, Padding = new Thickness(20, 4, 20, 4), Margin = new Thickness(0, 0, 10, 0),
                Background = light ? Brushes.White : Brush(Color.FromArgb(92, 22, 24, 32)),
                BorderBrush = light ? Brushes.White : Brush(Color.FromArgb(120, 255, 255, 255)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, FocusVisualStyle = null,
                Template = RoundedButtonTemplate(8), Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.25, Color = Colors.Black }
            };
            if (click != null) button.Click += delegate { click(); };
            return button;
        }

        void StartHomeHeroTimer()
        {
            StopHomeHeroTimer();
            if (UiMotion.Reduced || homeHeroItems.Count < 2) return;
            homeHeroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9.5) };
            homeHeroTimer.Tick += delegate
            {
                if (page != "Accueil" || homeHeroItems.Count < 2) { StopHomeHeroTimer(); return; }
                int next = homeHeroIndex + 1;
                if (next >= homeHeroItems.Count) next = 0;
                SetHomeHero(next, false);
            };
            homeHeroTimer.Start();
        }

        void StopHomeHeroTimer()
        {
            if (homeHeroTimer == null) return;
            homeHeroTimer.Stop();
            homeHeroTimer = null;
        }

        FrameworkElement BuildHomeIntroRow(string greeting)
        {
            var dock = new DockPanel { LastChildFill = true, Margin = new Thickness(32, 24, 32, 8) };
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            if (channels.Count > 0)
            {
                var surprise = Chip("Au hasard", false, SurpriseMe);
                surprise.Margin = new Thickness(0, 0, 8, 0);
                surprise.ToolTip = "Laisser LyaranTV choisir un contenu pour vous";
                right.Children.Add(surprise);
                var browse = Chip("Parcourir la bibliothèque", false, delegate { Nav("Bibliothèque"); });
                browse.Margin = new Thickness(0, 0, 8, 0);
                browse.ToolTip = "Ouvrir la bibliothèque complète";
                right.Children.Add(browse);
                if (loadedEpg.Count > 0)
                {
                    var guide = Chip("Guide TV", false, delegate { Nav("Guide EPG"); });
                    guide.Margin = new Thickness(0);
                    right.Children.Add(guide);
                }
            }
            DockPanel.SetDock(right, Dock.Right);
            dock.Children.Add(right);

            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            var heading = Text(greeting, 22, Brush(TextPrimary), true);
            pageTitle = heading;
            line.Children.Add(heading);
            if (activeSource != null)
            {
                var badge = Badge(activeSource.Name.ToUpperInvariant(), Brush(WithAlpha(Accent, 30)), Brush(Accent));
                badge.Margin = new Thickness(11, 0, 0, 0);
                line.Children.Add(badge);
            }
            labels.Children.Add(line);
            var visible = VisibleChannels.ToList();
            string stats = TextUtil.HumanCount(visible.Count(c => c.Kind == "Chaînes")) + " chaînes"
                + "   ·   " + TextUtil.HumanCount(visible.Count(c => c.Kind == "Films")) + " films"
                + "   ·   " + TextUtil.HumanCount(visible.Count(c => c.Kind == "Séries")) + " séries"
                + (favorites.Count > 0 ? "   ·   " + TextUtil.HumanCount(favorites.Count) + " favoris" : "")
                + (loadedEpg.Count > 0 ? "   ·   " + TextUtil.HumanCount(loadedEpg.Count) + " programmes TV" : "");
            labels.Children.Add(Text(stats, 11, Brush(Muted), false));
            dock.Children.Add(labels);
            return dock;
        }

        void SurpriseMe()
        {
            var pool = VisibleChannels
                .Where(c => (c.Kind == "Films" || (c.Kind == "Séries" && String.IsNullOrWhiteSpace(c.Season)) || c.Kind == "Chaînes")
                    && !hiddenChannels.Contains(c.Id))
                .ToList();
            if (pool.Count == 0) { ShowToast("Aucun contenu à proposer pour le moment.", false, false); return; }
            var random = new Random();
            Channel pick = pool[random.Next(pool.Count)];
            if (pick.Kind == "Chaînes") Play(pick, null, 0, false);
            else ShowChannelInfo(pick);
        }

        FrameworkElement BuildLiveShelf()
        {
            var live = VisibleChannels.Where(c => c.Kind == "Chaînes").Take(20).ToList();
            if (live.Count == 0) return null;
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 4, 0, 10) };
            var guide = Chip("Guide TV", false, delegate { Nav("Guide EPG"); });
            guide.Margin = new Thickness(0);
            DockPanel.SetDock(guide, Dock.Right);
            header.Children.Add(guide);
            var labels = new StackPanel();
            var kicker = Text("EN DIRECT", 9, Brush(Accent), true);
            kicker.Margin = new Thickness(0, 0, 0, 2);
            labels.Children.Add(kicker);
            labels.Children.Add(Text("Chaînes à regarder maintenant", 19, Brush(TextPrimary), true));
            labels.Children.Add(Text("Le direct et le programme en cours, sans quitter l'accueil.", 11, Brush(Muted), false));
            header.Children.Add(labels);
            section.Children.Add(header);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var channel in live) row.Children.Add(LiveTile(channel));
            section.Children.Add(WrapLibraryScroller(row));
            return section;
        }

        Border LiveTile(Channel channel)
        {
            var grid = new Grid { Height = 150, ClipToBounds = true };
            RoundClip(grid, 12);
            grid.Children.Add(new Border
            {
                Background = new LinearGradientBrush(WithAlpha(Accent, 55), Color.FromRgb(17, 19, 26), 42)
            });
            if (!String.IsNullOrWhiteSpace(channel.Logo))
                grid.Children.Add(new AsyncImage { Url = channel.Logo, Stretch = Stretch.Uniform, Margin = new Thickness(44, 26, 44, 26), Opacity = 0.92 });
            grid.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.3),
                    new GradientStop(Color.FromArgb(168, 5, 6, 9), 1)
                }, new Point(0.5, 0), new Point(0.5, 1))
            });
            var top = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(10, 9, 0, 0) };
            top.Children.Add(new Border
            {
                Background = Brush(Color.FromRgb(224, 46, 60)), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 2, 7, 3),
                Child = Text("● DIRECT", 8.5, Brushes.White, true)
            });
            grid.Children.Add(top);
            if (!String.IsNullOrWhiteSpace(channel.Number))
            {
                var number = FoodBadge(channel.Number);
                number.HorizontalAlignment = HorizontalAlignment.Right;
                number.VerticalAlignment = VerticalAlignment.Top;
                number.Margin = new Thickness(0, 9, 9, 0);
                grid.Children.Add(number);
            }
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(11, 0, 11, 10) };
            labels.Children.Add(Text(channel.Name, 12.5, Brushes.White, true));
            var now = CurrentProgram(channel);
            string sub = now != null ? now.Title : (String.IsNullOrWhiteSpace(channel.Group) ? "En direct" : channel.Group);
            labels.Children.Add(Text(TextUtil.Ellipsis(sub, 42), 10, Brush(Color.FromArgb(200, 255, 255, 255)), false));
            if (now != null && now.Duration.TotalSeconds > 1)
            {
                var progress = new Border
                {
                    Height = 3, CornerRadius = new CornerRadius(2), Background = Brush(Color.FromArgb(70, 255, 255, 255)),
                    Margin = new Thickness(0, 7, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch
                };
                double fraction = Math.Max(0.02, Math.Min(1, (DateTimeOffset.Now - now.Start).TotalSeconds / now.Duration.TotalSeconds));
                var fill = new Border
                {
                    Height = 3, CornerRadius = new CornerRadius(2), Background = Brush(Color.FromRgb(224, 46, 60)),
                    HorizontalAlignment = HorizontalAlignment.Left, Width = 196 * fraction
                };
                var bar = new Grid { Margin = new Thickness(0, 7, 0, 0) };
                bar.Children.Add(progress); bar.Children.Add(fill);
                labels.Children.Add(bar);
            }
            grid.Children.Add(labels);

            var card = new Border
            {
                Child = grid, Width = 218, Height = 150, Margin = new Thickness(0, 0, 12, 14),
                CornerRadius = new CornerRadius(12), ClipToBounds = true, Background = Brush(Panel),
                BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                Focusable = true, ToolTip = channel.Group + " · " + channel.Name
            };
            var lift = new TranslateTransform();
            card.RenderTransform = lift;
            card.MouseEnter += delegate
            {
                card.BorderBrush = Brush(Accent);
                card.Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 6, Opacity = 0.35, Color = Colors.Black };
                if (!UiMotion.Reduced) lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-3, TimeSpan.FromMilliseconds(130)));
                else lift.Y = -3;
            };
            card.MouseLeave += delegate
            {
                card.BorderBrush = Brush(BorderColor);
                card.Effect = null;
                if (!UiMotion.Reduced) lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(130)));
                else lift.Y = 0;
            };
            card.MouseLeftButtonUp += delegate { Play(channel, currentView); };
            var menu = CreateContextMenu();
            menu.Items.Add(MenuItem("Regarder le direct", delegate { Play(channel, currentView); }, Glyphs.Play));
            menu.Items.Add(MenuItem(favorites.Contains(channel.Id) ? "Retirer des favoris" : "Ajouter aux favoris", delegate { ToggleFavorite(channel); }, Glyphs.Favorite));
            menu.Items.Add(MenuItem("Voir le guide", delegate { Nav("Guide EPG"); }, Glyphs.Guide));
            card.ContextMenu = menu;
            return card;
        }

        Border FoodBadge(string number)
        {
            return new Border
            {
                Background = Brush(Color.FromArgb(180, 6, 7, 10)), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 2, 7, 3), Child = Text(number, 9, Brushes.White, true)
            };
        }
    }
}
