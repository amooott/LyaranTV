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

        void ShowChannelInfo(Channel channel)
        {
            if (channel == null) return;
            if (fullScreen) ExitFullScreen();
            if (page != "Fiche") PushNavFrame();
            detailChannel = channel;
            page = "Fiche";
            RebuildSidebar();
            UpdateStatusBar();
            SetPageStatus();
            BuildChannelDetailPage(channel);
        }

        void BuildChannelDetailPage(Channel channel)
        {
            if (channel == null) return;
            var root = new StackPanel { Margin = new Thickness(0) };
            root.Children.Add(BuildChannelDetailHero(channel));

            var body = new StackPanel
            {
                Margin = new Thickness(34, 22, 34, 40),
                MaxWidth = 1120,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            string overview = String.IsNullOrWhiteSpace(channel.Overview) ? channel.Description : channel.Overview;
            if (!String.IsNullOrWhiteSpace(overview))
            {
                var synopsis = new StackPanel();
                synopsis.Children.Add(Text("SYNOPSIS", 12.5, Brush(Accent), true));
                var text = Text(overview, 13.5, Brush(TextPrimary), false);
                text.Margin = new Thickness(0, 6, 0, 0);
                text.LineHeight = 20;
                text.MaxWidth = 900;
                text.HorizontalAlignment = HorizontalAlignment.Left;
                synopsis.Children.Add(text);
                body.Children.Add(Card(synopsis, 18));
            }

            var facts = BuildChannelDetailFacts(channel);
            if (facts != null) body.Children.Add(facts);

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

            if (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode))
            {
                string line = "Saison " + (String.IsNullOrWhiteSpace(channel.Season) ? "1" : channel.Season);
                if (!String.IsNullOrWhiteSpace(channel.Episode)) line += "  ·  Épisode " + channel.Episode;
                body.Children.Add(Card(Text(line, 13, Brush(TextPrimary), true), 16));
            }

            if (!String.IsNullOrWhiteSpace(channel.TmdbId))
            {
                var credit = Text(TmdbClient.Attribution, 12.5, Brush(Muted), false);
                credit.Margin = new Thickness(0, 0, 0, 10);
                credit.TextWrapping = TextWrapping.Wrap;
                body.Children.Add(credit);
            }

            root.Children.Add(body);
            SetContent(root);
            SetStatus(channel.Name + " · " + KindLabel(channel.Kind) + ".");
        }

        FrameworkElement BuildChannelDetailHero(Channel channel)
        {
            // Un épisode n'a souvent qu'un aperçu 16:9 : il tient le rôle du fond.
            string preferredBackdrop = !String.IsNullOrWhiteSpace(channel.BackdropUrl) ? channel.BackdropUrl : channel.StillUrl;
            if (String.IsNullOrWhiteSpace(preferredBackdrop)) preferredBackdrop = channel.PosterUrl;
            bool hasBackdrop = !String.IsNullOrWhiteSpace(preferredBackdrop);

            var hero = new Grid
            {
                Height = 430, ClipToBounds = true,
                Background = Brush(Color.FromRgb(8, 8, 14))
            };
            if (hasBackdrop)
            {
                hero.Children.Add(new AsyncImage { Url = preferredBackdrop, Stretch = Stretch.UniformToFill });
            }
            else
            {
                hero.Children.Add(new Border
                {
                    Background = new LinearGradientBrush(new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(22, 16, 42), 0),
                        new GradientStop(Color.FromRgb(8, 8, 14), 1)
                    }, new Point(0, 0), new Point(1, 1))
                });
                var mark = Icon(channel.Kind == "Séries" ? Glyphs.Series : channel.Kind == "Chaînes" ? Glyphs.TV : Glyphs.Movies,
                    92, Brush(Color.FromArgb(105, 255, 255, 255)));
                mark.HorizontalAlignment = HorizontalAlignment.Center;
                mark.VerticalAlignment = VerticalAlignment.Center;
                hero.Children.Add(mark);
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

            var back = PlayerButton(Glyphs.Back, "Retour", delegate { NavBack(); }, false, 38);
            back.HorizontalAlignment = HorizontalAlignment.Left;
            back.VerticalAlignment = VerticalAlignment.Top;
            back.Margin = new Thickness(28, 22, 0, 0);
            hero.Children.Add(back);

            var content = new Grid { Margin = new Thickness(34, 0, 34, 26), VerticalAlignment = VerticalAlignment.Bottom };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var poster = new Border
            {
                Width = 178, Height = 264, CornerRadius = new CornerRadius(12),
                Background = Brush(Color.FromRgb(18, 18, 26)),
                BorderBrush = Brush(Color.FromArgb(95, 255, 255, 255)), BorderThickness = new Thickness(1),
                ClipToBounds = true, VerticalAlignment = VerticalAlignment.Bottom,
                Child = PosterVisual(channel, 178, 264)
            };
            RoundClip(poster, 12);
            Grid.SetColumn(poster, 0);
            content.Children.Add(poster);

            var copy = new StackPanel { Margin = new Thickness(22, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
            var eyebrowParts = new List<string>();
            eyebrowParts.Add(KindLabel(channel.Kind));
            if (!String.IsNullOrWhiteSpace(channel.Group)
                && !channel.Group.StartsWith("Saison", StringComparison.OrdinalIgnoreCase))
                eyebrowParts.Add(channel.Group);
            var eyebrow = Text(String.Join("  ·  ", eyebrowParts.ToArray()), 12.5, Brush(Color.FromArgb(220, 255, 255, 255)), true);
            copy.Children.Add(eyebrow);

            var title = Text(TextUtil.DisplayTitle(channel.Name), 34, Brushes.White, true);
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

        FrameworkElement BuildChannelDetailFacts(Channel channel)
        {
            var facts = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            AddDetailFact(facts, "ANNÉE", channel.Year);
            AddDetailFact(facts, "DURÉE", channel.Runtime);
            AddDetailFact(facts, "GENRES", channel.Genres);
            AddDetailFact(facts, "PAYS", channel.Countries);
            AddDetailFact(facts, "CLASSIFICATION", channel.Certification);
            if (channel.TmdbRating > 0)
                AddDetailFact(facts, "NOTE TMDb", channel.TmdbRating.ToString("0.0", CultureInfo.InvariantCulture) + " / 10");
            if (!String.IsNullOrWhiteSpace(channel.Group)) AddDetailFact(facts, "CATÉGORIE", channel.Group);
            if (!String.IsNullOrWhiteSpace(channel.OriginalTitle)) AddDetailFact(facts, "TITRE ORIGINAL", channel.OriginalTitle);
            if (facts.Children.Count == 0) return null;
            return facts;
        }

        void AddDetailFact(System.Windows.Controls.Panel panel, string label, string value)
        {
            if (panel == null || String.IsNullOrWhiteSpace(value)) return;
            var stack = new StackPanel();
            stack.Children.Add(Text(label, 12.5, Brush(Muted), true));
            var text = Text(value, 13, Brush(TextPrimary), false);
            text.Margin = new Thickness(0, 2, 0, 0);
            text.MaxWidth = 230;
            text.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(text);
            var card = new Border
            {
                Child = stack, Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 11, 14, 11),
                Margin = new Thickness(0, 0, 10, 10), MinWidth = 170, MaxWidth = 260
            };
            panel.Children.Add(card);
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
