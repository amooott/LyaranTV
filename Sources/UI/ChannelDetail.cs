using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LyaranTV
{
    /// <summary>
    /// Fiche d'un contenu, intégrée à la navigation comme la page d'un titre
    /// Netflix : grande image, affiche, actions, synopsis puis informations
    /// complémentaires. Elle remplace l'ancienne boîte de dialogue modale.
    /// </summary>
    public sealed partial class MainWindow
    {
        Channel detailChannel;
        Grid detailOverlay;

        void ShowChannelInfo(Channel channel)
        {
            if (channel == null) return;
            if (fullScreen) ExitFullScreen();
            detailChannel = channel;
            // Comme la fiche d'un titre sur une plateforme de streaming : un
            // panneau flottant par-dessus la page, pas une page entière.
            ShowChannelDetailOverlay(channel);
        }

        /// <summary>
        /// Panneau de fiche : largeur confortable, hauteur bornée et défilement
        /// interne. La page sous-jacente reste visible autour, ce qui donne le
        /// rendu « quick view » de Netflix.
        /// </summary>
        void ShowChannelDetailOverlay(Channel channel)
        {
            if (rootGrid == null || channel == null) return;
            CloseDetailOverlay();

            var backdrop = new Grid { Background = Brush(Color.FromArgb(196, 3, 4, 9)) };
            backdrop.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.OriginalSource == backdrop) CloseDetailOverlay();
            };

            double windowWidth = ActualWidth > 200 ? ActualWidth : 1380;
            double windowHeight = ActualHeight > 200 ? ActualHeight : 860;
            var card = new Border
            {
                Width = Math.Min(1020, Math.Max(640, windowWidth - 170)),
                MaxHeight = Math.Max(440, windowHeight - 110),
                CornerRadius = new CornerRadius(22),
                Background = Brush(Panel), BorderBrush = Brush(WithAlpha(Accent, 60)),
                BorderThickness = new Thickness(1), ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 46, ShadowDepth = 16, Opacity = 0.55, Color = Colors.Black }
            };

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = BuildChannelDetailContent(channel, true)
            };
            card.Child = scroll;

            var host = new Grid();
            host.Children.Add(card);
            var close = IconButton(Glyphs.Close, "Fermer la fiche · Échap", CloseDetailOverlay, false);
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.VerticalAlignment = VerticalAlignment.Top;
            close.Margin = new Thickness(0, 14, 14, 0);
            close.Background = Brush(Color.FromArgb(170, 8, 9, 14));
            close.BorderBrush = Brush(WithAlpha(Colors.White, 40));
            System.Windows.Controls.Panel.SetZIndex(close, 20);
            host.Children.Add(close);

            backdrop.Children.Add(host);
            detailOverlay = backdrop;
            System.Windows.Controls.Panel.SetZIndex(backdrop, 700);
            rootGrid.Children.Add(backdrop);

            if (!UiMotion.Reduced)
            {
                backdrop.Opacity = 0;
                backdrop.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(170))
                {
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                });
            }
            SetStatus(channel.Name + " · " + KindLabel(channel.Kind) + ".");
        }

        void CloseDetailOverlay()
        {
            if (detailOverlay == null) return;
            try { rootGrid.Children.Remove(detailOverlay); } catch { }
            detailOverlay = null;
        }

        void BuildChannelDetailPage(Channel channel)
        {
            if (channel == null) return;
            SetContent(BuildChannelDetailContent(channel, false));
        }

        FrameworkElement BuildChannelDetailContent(Channel channel, bool modal)
        {
            var root = new StackPanel { Margin = new Thickness(0) };
            root.Children.Add(BuildChannelDetailHero(channel, modal));

            var body = new StackPanel
            {
                Margin = modal ? new Thickness(26, 18, 26, 30) : new Thickness(34, 22, 34, 40),
                MaxWidth = 1120,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // « À propos » réunit le synopsis et les informations utiles dans une
            // seule surface calme, au lieu d'une mosaïque de petites cartes.
            string overview = String.IsNullOrWhiteSpace(channel.Overview) ? channel.Description : channel.Overview;
            var about = new StackPanel();
            about.Children.Add(Text("À PROPOS", 12.5, Brush(Accent), true));
            bool hasOverview = !String.IsNullOrWhiteSpace(overview);
            var text = Text(hasOverview ? overview : "Aucun synopsis n’est disponible pour ce contenu.", 13.5,
                hasOverview ? Brush(TextPrimary) : Brush(Muted), false);
            text.Margin = new Thickness(0, 7, 0, 0);
            text.LineHeight = 21;
            text.MaxWidth = 900;
            text.HorizontalAlignment = HorizontalAlignment.Left;
            about.Children.Add(text);

            var facts = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
            AddDetailFact(facts, "PAYS", channel.Countries);
            AddDetailFact(facts, "CLASSIFICATION", channel.Certification);
            AddDetailFact(facts, "TITRE ORIGINAL", channel.OriginalTitle);
            if (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode))
            {
                string line = "Saison " + (String.IsNullOrWhiteSpace(channel.Season) ? "1" : channel.Season);
                if (!String.IsNullOrWhiteSpace(channel.Episode)) line += "  ·  Épisode " + channel.Episode;
                AddDetailFact(facts, "SAISON / ÉPISODE", line);
            }
            if (!String.IsNullOrWhiteSpace(channel.Group)) AddDetailFact(facts, "CATÉGORIE", channel.Group);
            if (facts.Children.Count > 0) about.Children.Add(facts);
            body.Children.Add(Card(about, 20));

            // Informations TMDb : même chargement progressif que l'ancienne fiche,
            // mais dans la page et sans interrompre la navigation.
            bool canLoadTmdbDetails = !String.IsNullOrWhiteSpace(channel.TmdbId) && TmdbClient.IsConfigured(settings);
            var detailBody = new StackPanel();
            var detailStatus = Text("", 12.5, Brush(Muted), false);
            var detailStack = new StackPanel();
            detailStack.Children.Add(Text("INFORMATIONS", 12.5, Brush(Accent), true));
            detailStatus.Margin = new Thickness(0, 4, 0, 4);
            detailStack.Children.Add(detailStatus);
            detailStack.Children.Add(detailBody);
            if (canLoadTmdbDetails || !String.IsNullOrWhiteSpace(channel.Cast) || !String.IsNullOrWhiteSpace(channel.Director))
                body.Children.Add(Card(detailStack, 18));

            Action<TmdbResult> renderDetails = delegate(TmdbResult data)
            {
                detailBody.Children.Clear();
                if (data == null) return;
                if (!String.IsNullOrWhiteSpace(data.OriginalTitle)
                    && !String.Equals(TmdbTitleCleaner.CleanTitle(data.OriginalTitle), TmdbTitleCleaner.CleanTitle(channel.Name), StringComparison.OrdinalIgnoreCase))
                    AddMetadataLine(detailBody, "Titre original", data.OriginalTitle);
                AddMetadataLine(detailBody, "Durée", data.Runtime);
                AddMetadataLine(detailBody, "Réalisation / création", data.Director);
                AddMetadataLine(detailBody, "Pays", data.Countries);
                AddMetadataLine(detailBody, "Classification", data.Certification);
                AddMetadataLine(detailBody, "Distribution", data.Cast);
                if (!String.IsNullOrWhiteSpace(data.TrailerUrl))
                {
                    var trailer = Button(Glyphs.Play, "Voir la bande-annonce", delegate
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(data.TrailerUrl) { UseShellExecute = true }); }
                        catch { ShowError("Impossible d’ouvrir la bande-annonce."); }
                    }, false);
                    trailer.Margin = new Thickness(0, 8, 0, 0);
                    detailBody.Children.Add(trailer);
                }
                detailStatus.Text = String.IsNullOrWhiteSpace(data.TmdbId) ? ""
                    : "Métadonnées fournies par TMDb · " + data.VoteCount.ToString("N0", CultureInfo.GetCultureInfo("fr-FR")) + " votes";
            };
            renderDetails(new TmdbResult
            {
                TmdbId = channel.TmdbId ?? "", MediaType = channel.Kind == "Séries" ? "tv" : "movie",
                Title = channel.Name ?? "", OriginalTitle = channel.OriginalTitle ?? "", Runtime = channel.Runtime ?? "",
                Certification = channel.Certification ?? "", TrailerUrl = channel.TrailerUrl ?? "", Cast = channel.Cast ?? "",
                Director = channel.Director ?? "", Countries = channel.Countries ?? "", VoteCount = channel.VoteCount
            });

            var now = CurrentProgram(channel);
            var next = NextProgram(channel);
            if (now != null || next != null)
            {
                var epg = new StackPanel();
                epg.Children.Add(Text("PROGRAMME", 12.5, Brush(Accent), true));
                if (now != null)
                {
                    var current = Text("À l'antenne · " + now.Title, 14, Brush(TextPrimary), true);
                    current.Margin = new Thickness(0, 6, 0, 0);
                    epg.Children.Add(current);
                    epg.Children.Add(Text(now.TimeRange + "  ·  " + TextUtil.FormatDuration(now.Duration), 12.5, Brush(Muted), false));
                    if (!String.IsNullOrWhiteSpace(now.Description))
                    {
                        var desc = Text(TextUtil.Ellipsis(now.Description, 320), 12.5, Brush(Muted), false);
                        desc.Margin = new Thickness(0, 4, 0, 0);
                        epg.Children.Add(desc);
                    }
                }
                if (next != null)
                {
                    var nextText = Text("À suivre · " + next.Title + "  ·  " + next.TimeRange, 12.5, Brush(TextPrimary), false);
                    nextText.Margin = new Thickness(0, 8, 0, 0);
                    epg.Children.Add(nextText);
                }
                body.Children.Add(Card(epg, 18));
            }

            if (!String.IsNullOrWhiteSpace(channel.TmdbId))
            {
                var credit = Text(TmdbClient.Attribution, 12.5, Brush(Muted), false);
                credit.Margin = new Thickness(0, 0, 0, 10);
                credit.TextWrapping = TextWrapping.Wrap;
                body.Children.Add(credit);
            }

            root.Children.Add(body);
            SetStatus(channel.Name + " · " + KindLabel(channel.Kind) + ".");
            return root;
        }

        FrameworkElement BuildChannelDetailHero(Channel channel, bool modal)
        {
            // Un épisode n'a souvent qu'un aperçu 16:9 : il tient le rôle du fond.
            string preferredBackdrop = !String.IsNullOrWhiteSpace(channel.BackdropUrl) ? channel.BackdropUrl : channel.StillUrl;
            if (String.IsNullOrWhiteSpace(preferredBackdrop)) preferredBackdrop = channel.PosterUrl;
            bool hasBackdrop = !String.IsNullOrWhiteSpace(preferredBackdrop);

            var hero = new Grid
            {
                // En panneau flottant, l'image reste un aperçu compact façon Netflix :
                // la fiche ne prend jamais toute la fenêtre.
                Height = modal ? 280 : 400, ClipToBounds = true,
                Background = Brush(HeroBase)
            };
            if (hasBackdrop)
            {
                var backdropImage = new AsyncImage { Url = preferredBackdrop, Stretch = Stretch.UniformToFill };
                hero.Children.Add(backdropImage);
                // Nappe teintée par l'affiche elle-même : la fiche prend la couleur
                // de l'œuvre, comme un éclairage de scène, au lieu de répéter
                // l'accent de l'application d'une fiche à l'autre.
                var tintWash = new Border { IsHitTestVisible = false, Opacity = 0 };
                hero.Children.Add(tintWash);
                backdropImage.DominantColorReady = delegate(Color tint)
                {
                    if (tint.A == 0) return;
                    tintWash.Background = new RadialGradientBrush
                    {
                        Center = new Point(0.18, 0.12), GradientOrigin = new Point(0.18, 0.12),
                        RadiusX = 1.15, RadiusY = 1.15,
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(WithAlpha(tint, 104), 0),
                            new GradientStop(WithAlpha(tint, 26), 0.55),
                            new GradientStop(WithAlpha(tint, 0), 1)
                        }
                    };
                    tintWash.BeginAnimation(UIElement.OpacityProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
                        {
                            EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                        });
                };
            }
            else
            {
                hero.Children.Add(new Border
                {
                    Background = new LinearGradientBrush(new GradientStopCollection
                    {
                        new GradientStop(WithAlpha(Accent, 58), 0),
                        new GradientStop(LightMix(HeroBase, Accent, 0.16), 0.45),
                        new GradientStop(HeroBase, 1)
                    }, new Point(0.12, 0), new Point(0.9, 1))
                });
                // Halo secondaire et filigrane du titre : la fiche reste
                // cinématographique même sans image de fond.
                hero.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 620, Height = 620, HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -260, -120, 0),
                    Fill = new RadialGradientBrush(WithAlpha(Secondary, 46), WithAlpha(Secondary, 0))
                });
                var watermark = Text(TextUtil.DisplayTitle(channel.Name), 64, Brush(Color.FromArgb(26, 255, 255, 255)), true);
                watermark.HorizontalAlignment = HorizontalAlignment.Right;
                watermark.VerticalAlignment = VerticalAlignment.Center;
                watermark.TextAlignment = TextAlignment.Right;
                watermark.TextWrapping = TextWrapping.Wrap;
                watermark.MaxWidth = 520;
                watermark.Margin = new Thickness(0, 0, 56, 0);
                hero.Children.Add(watermark);
            }

            // Assombrissement du haut, du bas et du côté du texte : le titre reste
            // lisible quelle que soit l'image, y compris sur une affiche claire.
            hero.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(150, 3, 3, 8), 0),
                    new GradientStop(Color.FromArgb(20, 3, 3, 8), 0.35),
                    new GradientStop(Color.FromArgb(80, 3, 3, 8), 0.68),
                    new GradientStop(Color.FromArgb(246, Panel.R, Panel.G, Panel.B), 1)
                }, new Point(0.5, 0), new Point(0.5, 1))
            });
            hero.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(210, 3, 3, 8), 0),
                    new GradientStop(Color.FromArgb(45, 3, 3, 8), 0.62),
                    new GradientStop(Color.FromArgb(0, 3, 3, 8), 1)
                }, new Point(0, 0.5), new Point(1, 0.5))
            });

            var back = PlayerButton(Glyphs.Back, modal ? "Fermer la fiche" : "Retour",
                delegate { if (modal) CloseDetailOverlay(); else NavBack(); }, false, 38);
            back.HorizontalAlignment = HorizontalAlignment.Left;
            back.VerticalAlignment = VerticalAlignment.Top;
            back.Margin = new Thickness(modal ? 20 : 28, modal ? 18 : 22, 0, 0);
            hero.Children.Add(back);

            var content = new Grid
            {
                Margin = modal ? new Thickness(26, 0, 26, 20) : new Thickness(34, 0, 34, 26),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(modal ? 148 : 190) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var poster = new Border
            {
                Width = modal ? 136 : 178, Height = modal ? 202 : 264, CornerRadius = new CornerRadius(12),
                Background = Brush(Color.FromRgb(18, 18, 26)),
                BorderBrush = Brush(Color.FromArgb(95, 255, 255, 255)), BorderThickness = new Thickness(1),
                ClipToBounds = true, VerticalAlignment = VerticalAlignment.Bottom,
                Child = PosterVisual(channel, modal ? 136 : 178, modal ? 202 : 264)
            };
            RoundClip(poster, 12);
            Grid.SetColumn(poster, 0);
            content.Children.Add(poster);

            var copy = new StackPanel { Margin = new Thickness(22, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
            var eyebrowParts = new List<string>();
            string kindLabel = KindLabel(channel.Kind);
            if (!String.IsNullOrWhiteSpace(channel.Group)
                && !channel.Group.StartsWith("Saison", StringComparison.OrdinalIgnoreCase))
            {
                // Le groupe contient souvent déjà le type (« Films · Drame ») :
                // on évite « Film · Films · Drame ».
                bool groupCarriesKind = channel.Group.StartsWith(channel.Kind, StringComparison.OrdinalIgnoreCase)
                    || channel.Group.StartsWith(kindLabel, StringComparison.OrdinalIgnoreCase);
                if (!groupCarriesKind) eyebrowParts.Add(kindLabel);
                eyebrowParts.Add(channel.Group);
            }
            else eyebrowParts.Add(kindLabel);
            var eyebrow = Text(String.Join("  ·  ", eyebrowParts.ToArray()), 12.5, Brush(Color.FromArgb(220, 255, 255, 255)), true);
            copy.Children.Add(eyebrow);

            var title = Text(TextUtil.DisplayTitle(channel.Name), modal ? 27 : 34, Brushes.White, true);
            title.Margin = new Thickness(0, 4, 0, 0);
            title.TextWrapping = TextWrapping.Wrap;
            title.MaxHeight = 88;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            copy.Children.Add(title);

            var metaParts = new List<string>();
            if (!String.IsNullOrWhiteSpace(channel.Year)) metaParts.Add(channel.Year);
            if (!String.IsNullOrWhiteSpace(channel.Runtime)) metaParts.Add(channel.Runtime);
            if (!String.IsNullOrWhiteSpace(channel.Certification)) metaParts.Add(channel.Certification);
            if (channel.TmdbRating > 0) metaParts.Add("★ " + channel.TmdbRating.ToString("0.0", CultureInfo.InvariantCulture) + "/10");
            if (!String.IsNullOrWhiteSpace(channel.Genres)) metaParts.Add(TextUtil.Ellipsis(channel.Genres, 70));
            if (metaParts.Count > 0)
            {
                var meta = Text(String.Join("  ·  ", metaParts.ToArray()), 12.5, Brush(Color.FromArgb(232, 244, 244, 248)), false);
                meta.Margin = new Thickness(0, 6, 0, 0);
                meta.TextWrapping = TextWrapping.Wrap;
                copy.Children.Add(meta);
            }

            double saved = GetSavedPosition(channel);
            bool series = channel.IsSeries || (channel.Kind == "Séries" && String.IsNullOrWhiteSpace(channel.Url));
            if (saved > 1 && !series)
                copy.Children.Add(Text("Reprendre à " + TextUtil.FormatPosition(TimeSpan.FromSeconds(saved)), 12.5, Brush(Color.FromArgb(235, 255, 255, 255)), true));

            var actions = new WrapPanel { Margin = new Thickness(0, 15, 0, 0) };
            string playLabel = series ? "Voir les épisodes"
                : saved > 5 ? "Reprendre"
                : channel.Kind == "Chaînes" ? "Regarder le direct" : "Lecture";
            var play = Button(Glyphs.Play, playLabel, delegate
            {
                if (series) LoadSeriesEpisodes(channel);
                else Play(channel, null, saved, false);
            }, true);
            play.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(play);

            Button favorite = null;
            favorite = Button(favorites.Contains(channel.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite,
                favorites.Contains(channel.Id) ? "Favori" : "Ajouter aux favoris", delegate
                {
                    ToggleFavorite(channel);
                    favorite.Content = IconText(favorites.Contains(channel.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite,
                        favorites.Contains(channel.Id) ? "Favori" : "Ajouter aux favoris", Brush(TextPrimary), 12.5);
                }, false);
            favorite.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(favorite);

            if (channel.Kind == "Films" || channel.Kind == "Séries")
            {
                var match = Button(Glyphs.Search, "Corriger TMDb", delegate { ShowTmdbMatchDialog(channel); }, false);
                match.Margin = new Thickness(0, 0, 8, 8);
                actions.Children.Add(match);
                if (!String.IsNullOrWhiteSpace(channel.TmdbId))
                {
                    var reset = Button(Glyphs.Refresh, "Réinitialiser TMDb", delegate { ResetChannelTmdb(channel); }, false);
                    reset.Margin = new Thickness(0, 0, 8, 8);
                    actions.Children.Add(reset);
                }
            }

            if (!String.IsNullOrWhiteSpace(channel.Url))
            {
                var external = IconButton(Glyphs.External, "Ouvrir dans le lecteur externe", delegate { OpenInExternalPlayer(channel.Url); }, false);
                external.Margin = new Thickness(0, 0, 8, 8);
                actions.Children.Add(external);
                var copyUrl = IconButton(Glyphs.Copy, "Copier l’adresse", delegate { CopyText(channel.Url); }, false);
                copyUrl.Margin = new Thickness(0, 0, 8, 8);
                actions.Children.Add(copyUrl);
            }
            copy.Children.Add(actions);

            Grid.SetColumn(copy, 1);
            content.Children.Add(copy);
            hero.Children.Add(content);
            return hero;
        }

        void AddDetailFact(System.Windows.Controls.Panel panel, string label, string value)
        {
            if (panel == null || String.IsNullOrWhiteSpace(value)) return;
            var stack = new StackPanel
            {
                Margin = new Thickness(0, 0, 38, 12), MinWidth = 120, MaxWidth = 280,
                VerticalAlignment = VerticalAlignment.Top
            };
            stack.Children.Add(Text(label, 11, Brush(Muted), true));
            var text = Text(value, 13, Brush(TextPrimary), false);
            text.Margin = new Thickness(0, 2, 0, 0);
            text.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(text);
            panel.Children.Add(stack);
        }

        void ResetChannelTmdb(Channel channel)
        {
            if (channel == null) return;
            TmdbMatchStore.Remove(channel);
            TmdbCacheStore.Remove(TmdbCacheStore.CacheKey(channel));
            channel.TmdbId = channel.PosterUrl = channel.BackdropUrl = channel.Overview = channel.Genres = "";
            channel.OriginalTitle = channel.Runtime = channel.Certification = channel.TrailerUrl = channel.Cast = channel.Director = channel.Countries = "";
            channel.TmdbRating = 0; channel.VoteCount = 0; channel.TmdbChecked = false; channel.TmdbDetailsLoaded = false;
            StartTmdbEnrichment();
            ShowToast("Correspondance réinitialisée. Nouvelle recherche lancée en arrière-plan.", false, true);
            ShowChannelInfo(channel);
        }
    }
}
