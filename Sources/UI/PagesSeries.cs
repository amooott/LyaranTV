using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LyaranTV
{
    /// <summary>
    /// Liste des épisodes d'une série : page complète (saisons + épisodes) et
    /// rangée « Autres épisodes » affichée sous le lecteur, à la manière de Netflix.
    /// </summary>
    public sealed partial class MainWindow
    {
        readonly Dictionary<string, List<Channel>> seriesEpisodes = new Dictionary<string, List<Channel>>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> seriesLoading = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string episodesSeason = "";
        Channel episodesSeries;

        string SeriesKey(Channel series)
        {
            if (series == null) return "";
            if (!String.IsNullOrWhiteSpace(series.StreamId)) return "xtream:" + series.StreamId;
            return "id:" + series.Id;
        }

        // ------------------------------------------------------- chargement

        internal void LoadSeriesEpisodes(Channel series)
        {
            if (series == null) return;

            // Une série M3U peut porter directement le lien de son unique épisode.
            if (!String.IsNullOrWhiteSpace(series.Url) && (activeSource == null || activeSource.Type != "Xtream"))
            {
                Play(series, null, GetSavedPosition(series), false);
                return;
            }
            if (series.IsSeries && String.IsNullOrWhiteSpace(series.StreamId))
            {
                ShowError("Cette série ne contient aucun épisode lisible dans votre playlist.");
                return;
            }

            List<Channel> cached;
            if (seriesEpisodes.TryGetValue(SeriesKey(series), out cached) && cached.Count > 0)
            {
                ShowSeriesEpisodes(series, cached);
                return;
            }
            if (activeSource == null || activeSource.Type != "Xtream" || String.IsNullOrWhiteSpace(series.StreamId))
            {
                ShowError("La liste des épisodes n’est disponible que pour les séries Xtream.");
                return;
            }

            if (player != null) { SaveCurrentPosition(); try { player.Stop(); player.Close(); } catch { } player = null; }
            if (playbackTimer != null) { playbackTimer.Stop(); playbackTimer = null; }
            StreamProxy.Clear();
            if (page != "Épisodes") PushNavFrame();
            page = "Épisodes";
            episodesSeries = series;
            RebuildSidebar();
            UpdateStatusBar();
            SetPageStatus();

            var root = PageRoot(series.Name, "Chargement des épisodes…");
            var waiting = new StackPanel { Margin = new Thickness(0, 24, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            waiting.Children.Add(Text("Connexion au serveur : récupération de la liste des épisodes…", 12.5, Brush(Muted), false));
            var bar = CreateSpinner(34);
            bar.HorizontalAlignment = HorizontalAlignment.Left;
            bar.Margin = new Thickness(0, 14, 0, 0);
            waiting.Children.Add(bar);
            root.Children.Add(waiting);
            SetContent(root);
            SetStatus("Chargement des épisodes de « " + series.Name + " »…");

            PlaylistSource source = activeSource;
            string key = SeriesKey(series);
            seriesLoading.Add(key);
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                string url = XtreamSeriesUrl(series, source);
                string json = HttpUtil.GetString(url, 20000, "LyaranTV/2.0");
                e.Result = PlaylistLoader.ParseSeriesEpisodes(json, series, source);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                seriesLoading.Remove(key);
                if (appClosing || page != "Épisodes" || episodesSeries == null
                    || !String.Equals(SeriesKey(episodesSeries), key, StringComparison.OrdinalIgnoreCase)) return;
                if (e.Error != null)
                {
                    string message = e.Error.Message;
                    if (String.IsNullOrWhiteSpace(message)) message = e.Error.GetBaseException().Message;
                    ShowError("Impossible de récupérer les épisodes de « " + series.Name + " ».\n\n" + message);
                    var failed = PageRoot(series.Name, "Épisodes indisponibles");
                    failed.Children.Add(EmptyState(Glyphs.Series, "Épisodes indisponibles",
                        message, Button(Glyphs.Back, "Revenir à la bibliothèque", delegate { NavBack(); }, false)));
                    SetContent(failed);
                    return;
                }
                var items = e.Result as List<Channel>;
                if (items == null || items.Count == 0)
                {
                    ShowError("Cette série ne contient aucun épisode lisible.");
                    NavBack();
                    return;
                }
                seriesEpisodes[key] = items;
                ShowSeriesEpisodes(series, items);
            };
            worker.RunWorkerAsync();
        }

        static string XtreamSeriesUrl(Channel series, PlaylistSource source)
        {
            string baseUrl = PlaylistLoader.NormalizeXtreamBase(source.Url);
            return baseUrl.TrimEnd('/') + "/player_api.php?username=" + Uri.EscapeDataString(source.Username)
                + "&password=" + Uri.EscapeDataString(source.Password)
                + "&action=get_series_info&series_id=" + Uri.EscapeDataString(series.StreamId);
        }

        // Récupère les épisodes en arrière-plan pour la rangée du lecteur.
        void EnsureSeriesEpisodes(Channel series)
        {
            if (series == null) return;
            // On travaille sur la série parente : un épisode ne porte pas l'identifiant
            // de série attendu par l'API Xtream.
            Channel parent = SeriesForEpisode(series);
            if (parent != null) series = parent;
            string key = SeriesKey(series);
            List<Channel> cached;
            if (seriesEpisodes.TryGetValue(key, out cached) && cached.Count > 0)
            {
                RefreshPlayerEpisodesRow();
                return;
            }
            if (activeSource == null || activeSource.Type != "Xtream" || String.IsNullOrWhiteSpace(series.StreamId)) return;
            if (seriesLoading.Contains(key)) return;
            seriesLoading.Add(key);
            PlaylistSource source = activeSource;
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                string url = XtreamSeriesUrl(series, source);
                string json = HttpUtil.GetString(url, 20000, "LyaranTV/2.0");
                e.Result = PlaylistLoader.ParseSeriesEpisodes(json, series, source);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                seriesLoading.Remove(key);
                if (e.Error != null || appClosing) return;
                var items = e.Result as List<Channel>;
                if (items == null || items.Count == 0) return;
                seriesEpisodes[key] = items;
                if (page == "Lecture" && currentPlaying != null)
                {
                    Channel playingParent = SeriesForEpisode(currentPlaying) ?? currentPlaying;
                    if (String.Equals(SeriesKey(playingParent), key, StringComparison.OrdinalIgnoreCase))
                        RefreshPlayerEpisodesRow();
                }
            };
            worker.RunWorkerAsync();
        }

        // ---------------------------------------------------------- page épisodes

        void ShowSeriesEpisodes(Channel series, List<Channel> episodes)
        {
            if (series == null || episodes == null) return;
            episodesSeries = series;
            page = "Épisodes";
            RebuildSidebar();
            UpdateStatusBar();
            SetPageStatus();
            if (fullScreen) ExitFullScreen();

            // Chaque épisode garde un aperçu : celui du fournisseur, sinon
            // l'affiche de la série, puis TVmaze en arrière-plan.
            foreach (var episode in episodes)
            {
                if (episode == null) continue;
                if (String.IsNullOrWhiteSpace(episode.StillUrl) && String.IsNullOrWhiteSpace(episode.Logo)
                    && String.IsNullOrWhiteSpace(episode.PosterUrl) && !String.IsNullOrWhiteSpace(series.PosterUrl))
                    episode.PosterUrl = series.PosterUrl;
            }

            var root = new StackPanel { Margin = new Thickness(30, 22, 30, 32) };

            var back = Button(Glyphs.Back, "Retour", delegate { NavBack(); }, false);
            back.HorizontalAlignment = HorizontalAlignment.Left;
            root.Children.Add(back);

            var head = new Grid { Margin = new Thickness(0, 14, 0, 6) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(202) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var posterHost = new Border
            {
                Width = 188, Height = 282, CornerRadius = new CornerRadius(14), Background = Brush(Raised),
                BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1), ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
            };
            RoundClip(posterHost, 14);
            posterHost.Child = PosterVisual(series, 188, 282);
            Grid.SetColumn(posterHost, 0);
            head.Children.Add(posterHost);

            var copy = new StackPanel { Margin = new Thickness(22, 4, 0, 0) };
            var title = Text(series.Name, 25, Brush(TextPrimary), true);
            title.TextWrapping = TextWrapping.Wrap;
            copy.Children.Add(title);
            var seasons = episodes.Select(ep => String.IsNullOrWhiteSpace(ep.Season) ? "1" : ep.Season).Distinct().ToList();
            string meta = KindLabel(series.Kind) + "  ·  " + TextUtil.HumanCount(episodes.Count) + " épisode(s)";
            if (seasons.Count > 1) meta += "  ·  " + TextUtil.HumanCount(seasons.Count) + " saisons";
            else meta += "  ·  Saison " + seasons[0];
            if (!String.IsNullOrWhiteSpace(series.Year)) meta = series.Year + "  ·  " + meta;
            if (!String.IsNullOrWhiteSpace(series.Rating)) meta += "  ·  " + series.Rating + "/10";
            copy.Children.Add(Text(meta, 11.5, Brush(Muted), false));
            string description = !String.IsNullOrWhiteSpace(series.Description) ? series.Description : series.Overview;
            if (!String.IsNullOrWhiteSpace(description))
            {
                var body = Text(description, 12, Brush(TextPrimary), false);
                body.Margin = new Thickness(0, 10, 0, 0);
                body.MaxWidth = 760;
                body.TextWrapping = TextWrapping.Wrap;
                copy.Children.Add(body);
            }
            var resume = Text("", 12, Brush(TextPrimary), false);
            var next = NextEpisodeToWatch(series, episodes);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            if (next != null)
            {
                double saved = GetSavedPosition(next);
                string label = saved > 1 ? "Reprendre l’épisode " + (String.IsNullOrWhiteSpace(next.Episode) ? "" : next.Episode) : "Lire le premier épisode";
                actions.Children.Add(Button(Glyphs.Play, label.Trim(), delegate { Play(next, episodes, saved, false); }, true));
            }
            actions.Children.Add(Button(Glyphs.Favorite, favorites.Contains(series.Id) ? "Retirer des favoris" : "Ajouter aux favoris",
                delegate { ToggleFavorite(series); }, false));
            if (!String.IsNullOrWhiteSpace(series.Url)) actions.Children.Add(Button(Glyphs.External, "Lecteur externe", delegate { OpenInExternalPlayer(series.Url); }, false));
            copy.Children.Add(actions);
            Grid.SetColumn(copy, 1);
            head.Children.Add(copy);
            root.Children.Add(head);

            if (seasons.Count > 1)
            {
                var seasonBar = new WrapPanel { Margin = new Thickness(0, 18, 0, 2) };
                string active = String.IsNullOrWhiteSpace(episodesSeason) || !seasons.Contains(episodesSeason) ? seasons[0] : episodesSeason;
                foreach (string season in seasons)
                {
                    string captured = season;
                    seasonBar.Children.Add(Chip("Saison " + season, String.Equals(season, active), delegate
                    {
                        episodesSeason = captured;
                        ShowSeriesEpisodes(series, episodes);
                    }));
                }
                root.Children.Add(seasonBar);
            }
            else episodesSeason = seasons[0];

            string selected = String.IsNullOrWhiteSpace(episodesSeason) ? seasons[0] : episodesSeason;
            var list = episodes.Where(ep => String.Equals(String.IsNullOrWhiteSpace(ep.Season) ? "1" : ep.Season, selected))
                .OrderBy(ep => TextUtil.ParseInt(ep.Episode, 0)).ToList();
            if (list.Count == 0) list = episodes;

            var stack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            foreach (Channel episode in list) stack.Children.Add(EpisodeRow(episode, list, currentPlaying));
            root.Children.Add(stack);

            SetContent(root);
            SetStatus(series.Name + " · " + TextUtil.HumanCount(episodes.Count) + " épisode(s).");
            EnsureSeriesEpisodeArtwork(series, episodes);
        }

        Channel NextEpisodeToWatch(Channel series, List<Channel> episodes)
        {
            if (episodes == null || episodes.Count == 0) return null;
            foreach (Channel episode in episodes)
            {
                double saved = GetSavedPosition(episode);
                if (saved > 1 && !positions.ContainsKey(episode.Id + "|done")) return episode;
            }
            return episodes[0];
        }

        Border EpisodeRow(Channel episode, List<Channel> siblings, Channel playing)
        {
            bool current = playing != null && String.Equals(playing.Id, episode.Id, StringComparison.OrdinalIgnoreCase);
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(178) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var thumb = new Border
            {
                Width = 168, Height = 95, CornerRadius = new CornerRadius(10), Background = Brush(Raised),
                ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center
            };
            RoundClip(thumb, 10);
            thumb.Child = PosterVisual(episode, 168, 95);
            Grid.SetColumn(thumb, 0);
            row.Children.Add(thumb);

            var copy = new StackPanel { Margin = new Thickness(14, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            string number = String.IsNullOrWhiteSpace(episode.Episode) ? "" : "É" + episode.Episode + "  ·  ";
            var name = Text(number + episode.Name, 13.5, Brush(current ? Accent : TextPrimary), true);
            name.TextWrapping = TextWrapping.Wrap;
            copy.Children.Add(name);
            string detail = "";
            if (!String.IsNullOrWhiteSpace(episode.Runtime)) detail = episode.Runtime;
            else if (!String.IsNullOrWhiteSpace(episode.Description)) detail = TextUtil.Ellipsis(episode.Description, 170);
            if (detail.Length > 0) copy.Children.Add(Text(detail, 11, Brush(Muted), false));
            double saved = GetSavedPosition(episode);
            if (saved > 1) copy.Children.Add(Text("Reprendre à " + TextUtil.FormatPosition(TimeSpan.FromSeconds(saved)), 10.5, Brush(Accent), true));
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);

            var play = current
                ? Button(Glyphs.Stop, "En cours", delegate { Play(episode, siblings, GetSavedPosition(episode), false); }, true)
                : Button(Glyphs.Play, "Lire", delegate { Play(episode, siblings, GetSavedPosition(episode), false); }, false);
            play.Margin = new Thickness(0);
            play.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(play, 2);
            row.Children.Add(play);

            var card = new Border
            {
                Child = row, Padding = new Thickness(10), CornerRadius = new CornerRadius(12),
                Background = Brush(current ? WithAlpha(Accent, 26) : Panel),
                BorderBrush = Brush(current ? WithAlpha(Accent, 140) : BorderColor), BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand
            };
            card.MouseLeftButtonUp += delegate { Play(episode, siblings, GetSavedPosition(episode), false); };
            return card;
        }

        // ------------------------------------------------ rangée sous le lecteur

        void RefreshPlayerEpisodesRow()
        {
            if (episodesSection == null) return;
            var content = BuildEpisodesRow();
            episodesSection.Child = content;
            episodesSection.Visibility = showEpisodesBelowPlayer ? Visibility.Visible : Visibility.Collapsed;
            UpdateEpisodesToggle();
        }

        /// <summary>Épisodes disponibles pour la rangée : cache Xtream, sinon la file de lecture.</summary>
        List<Channel> CurrentEpisodesForRow()
        {
            if (currentPlaying == null) return new List<Channel>();
            List<Channel> cached;
            if (seriesEpisodes.TryGetValue(SeriesKey(currentPlaying), out cached) && cached != null && cached.Count > 0)
                return cached;
            if (playQueue == null) return new List<Channel>();
            return playQueue.Where(delegate(Channel ep)
            {
                // Les épisodes Xtream portent « Épisodes » ; la démonstration les
                // range parmi les séries, avec une saison renseignée.
                bool episode = ep != null && (ep.Kind == "Épisodes"
                    || (ep.Kind == "Séries" && !String.IsNullOrWhiteSpace(ep.Season)));
                if (!episode) return false;
                if (!String.IsNullOrWhiteSpace(currentPlaying.SeriesId))
                    return String.Equals(ep.SeriesId, currentPlaying.SeriesId, StringComparison.OrdinalIgnoreCase);
                return true;
            }).ToList();
        }

        void UpdateEpisodesToggle()
        {
            int count = CurrentEpisodesForRow().Count;
            if (episodesToggleButtonInControls != null)
                episodesToggleButtonInControls.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        FrameworkElement BuildEpisodesRow()
        {
            var root = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };
            if (currentPlaying == null) return root;
            List<Channel> all = CurrentEpisodesForRow();
            if (all.Count == 0) return root;

            var seasons = all.Select(ep => String.IsNullOrWhiteSpace(ep.Season) ? "1" : ep.Season).Distinct().ToList();
            string season = currentPlaying.Season;
            if (String.IsNullOrWhiteSpace(season) || !seasons.Contains(season)) season = seasons.Count > 0 ? seasons[0] : "1";
            var episodes = all.Where(ep => String.Equals(String.IsNullOrWhiteSpace(ep.Season) ? "1" : ep.Season, season))
                .OrderBy(ep => TextUtil.ParseInt(ep.Episode, 0)).ToList();
            if (episodes.Count == 0) episodes = all;

            int currentIndex = currentPlaying == null ? -1 : episodes.FindIndex(delegate(Channel ep)
            {
                return ep != null && String.Equals(ep.Id, currentPlaying.Id, StringComparison.OrdinalIgnoreCase);
            });
            Channel nextEpisode = currentIndex >= 0 && currentIndex + 1 < episodes.Count ? episodes[currentIndex + 1] : null;

            // En-tête en deux colonnes : le titre et le compteur ne peuvent plus
            // passer sous les actions, même avec « Épisode suivant » et la saison.
            var header = new Grid { Margin = new Thickness(2, 0, 0, 10) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(Text("Épisodes", 16, Brush(TextPrimary), true));
            string countLine = TextUtil.HumanCount(episodes.Count) + (episodes.Count > 1 ? " épisodes" : " épisode");
            countLine += "  ·  Saison " + season;
            if (currentIndex >= 0) countLine += "  ·  épisode " + (currentIndex + 1).ToString() + " en cours";
            var countText = Text(countLine, 11, Brush(Muted), false);
            countText.TextTrimming = TextTrimming.CharacterEllipsis;
            countText.Margin = new Thickness(0, 1, 0, 0);
            titles.Children.Add(countText);
            Grid.SetColumn(titles, 0);
            header.Children.Add(titles);

            var headerActions = new StackPanel
            {
                Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(14, 0, 0, 0)
            };
            if (nextEpisode != null)
            {
                var nextButton = Button(Glyphs.Next, "Épisode suivant", delegate { Play(nextEpisode, episodes, 0, false); }, true);
                nextButton.Margin = new Thickness(0, 0, 8, 0);
                nextButton.ToolTip = "Lire l'épisode " + (String.IsNullOrWhiteSpace(nextEpisode.Episode) ? "suivant" : nextEpisode.Episode);
                headerActions.Children.Add(nextButton);
            }
            if (seasons.Count > 1)
            {
                var picker = new ComboBox { Width = 132, Height = 32, Style = ModernComboStyle(), VerticalAlignment = VerticalAlignment.Center };
                foreach (string item in seasons) picker.Items.Add("Saison " + item);
                picker.SelectedItem = "Saison " + season;
                picker.SelectionChanged += delegate
                {
                    if (picker.SelectedItem == null) return;
                    string value = picker.SelectedItem.ToString().Replace("Saison ", "").Trim();
                    episodesSeason = value;
                    if (currentPlaying != null)
                    {
                        currentPlaying.Season = value;
                        RefreshPlayerEpisodesRow();
                    }
                };
                headerActions.Children.Add(picker);
            }
            Grid.SetColumn(headerActions, 1);
            header.Children.Add(headerActions);
            root.Children.Add(header);

            var strip = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < episodes.Count; i++)
            {
                Channel episode = episodes[i];
                bool isNext = nextEpisode != null && String.Equals(episode.Id, nextEpisode.Id, StringComparison.OrdinalIgnoreCase);
                strip.Children.Add(PlayerEpisodeCard(episode, episodes, isNext));
            }
            var scroll = new ScrollViewer
            {
                Content = strip, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 0, 6)
            };
            scroll.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e)
            {
                if (e.Delta == 0) return;
                scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta);
                e.Handled = true;
            };
            // Comme Netflix : la rangée s'ouvre sur l'épisode en cours, pas sur le premier.
            if (currentIndex > 0)
            {
                int focus = Math.Max(0, currentIndex - 1);
                scroll.Loaded += delegate
                {
                    try { scroll.ScrollToHorizontalOffset(focus * 278.0); } catch { }
                };
            }
            root.Children.Add(scroll);
            return root;
        }

        Border PlayerEpisodeCard(Channel episode, List<Channel> siblings, bool isNext)
        {
            bool current = currentPlaying != null && String.Equals(currentPlaying.Id, episode.Id, StringComparison.OrdinalIgnoreCase);
            var stack = new StackPanel();
            var thumbHost = new Border
            {
                Width = 252, Height = 142, CornerRadius = new CornerRadius(11), Background = Brush(Color.FromRgb(16, 16, 22)),
                ClipToBounds = true
            };
            RoundClip(thumbHost, 11);
            var thumbGrid = new Grid();
            thumbGrid.Children.Add(PosterVisual(episode, 252, 142));
            var shade = new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.35),
                    new GradientStop(Color.FromArgb(150, 0, 0, 0), 1)
                }, new Point(0.5, 0), new Point(0.5, 1))
            };
            thumbGrid.Children.Add(shade);
            var badge = new Border
            {
                Background = Brush(current ? Accent : Color.FromArgb(170, 10, 10, 16)),
                CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 2, 6, 3),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8)
            };
            badge.Child = Text(String.IsNullOrWhiteSpace(episode.Episode) ? "É" : "É" + episode.Episode, 10, current ? Brush(Color.FromRgb(12, 12, 18)) : Brushes.White, true);
            thumbGrid.Children.Add(badge);
            var playBadge = new Border
            {
                Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Brush(Color.FromArgb(190, 0, 0, 0)),
                BorderBrush = Brush(Color.FromArgb(120, 255, 255, 255)), BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Child = Icon(Glyphs.Play, 14, Brushes.White), IsHitTestVisible = false
            };
            thumbGrid.Children.Add(playBadge);
            if (current || isNext)
            {
                var now = new Border
                {
                    Background = Brush(current ? Accent : WithAlpha(Accent, 210)), CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 3), HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8)
                };
                now.Child = Text(current ? "EN COURS" : "À SUIVRE", 9, Brush(Color.FromRgb(12, 12, 18)), true);
                thumbGrid.Children.Add(now);
            }
            double progress = ResumeProgress(episode);
            if (progress > 0)
            {
                var bar = new Grid { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 10, 8) };
                bar.Children.Add(new Border
                {
                    Height = 4, CornerRadius = new CornerRadius(2), Background = Brush(Color.FromArgb(96, 255, 255, 255))
                });
                bar.Children.Add(new Border
                {
                    Height = 4, CornerRadius = new CornerRadius(2), Background = Brush(Accent),
                    HorizontalAlignment = HorizontalAlignment.Left, Width = 232 * progress
                });
                thumbGrid.Children.Add(bar);
            }
            thumbHost.Child = thumbGrid;
            stack.Children.Add(thumbHost);

            // Les noms Xtream finissent parfois par « - » quand le titre de
            // l'épisode est vide : on nettoie avant affichage.
            string displayName = (episode.Name ?? "").Trim();
            displayName = displayName.TrimEnd(' ', '-', '–', '—', '·', ':', '|');
            if (displayName.Length == 0)
                displayName = String.IsNullOrWhiteSpace(episode.Episode) ? "Épisode" : "Épisode " + episode.Episode;
            var name = Text(displayName, 13.5, Brush(current ? Accent : TextPrimary), true);
            name.Margin = new Thickness(2, 8, 2, 0);
            name.TextWrapping = TextWrapping.Wrap;
            name.MaxHeight = 38;
            stack.Children.Add(name);
            string detail = !String.IsNullOrWhiteSpace(episode.Runtime) ? episode.Runtime : TextUtil.Ellipsis(episode.Description, 90);
            if (!String.IsNullOrWhiteSpace(detail)) stack.Children.Add(Text(detail, 10.5, Brush(Muted), false));
            double saved = GetSavedPosition(episode);
            if (saved > 1) stack.Children.Add(Text("Reprendre · " + TextUtil.FormatPosition(TimeSpan.FromSeconds(saved)), 10, Brush(Accent), true));

            var card = new Border
            {
                Child = stack, Width = 268, Padding = new Thickness(8), CornerRadius = new CornerRadius(14),
                Background = Brush(current ? WithAlpha(Accent, 22) : Panel),
                BorderBrush = Brush(current ? WithAlpha(Accent, 130) : BorderColor), BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 4), Cursor = Cursors.Hand, ToolTip = displayName
            };
            var lift = new TranslateTransform();
            card.RenderTransform = lift;
            card.MouseEnter += delegate
            {
                card.BorderBrush = Brush(Accent);
                LiftCard(lift, -3);
            };
            card.MouseLeave += delegate
            {
                card.BorderBrush = Brush(current ? WithAlpha(Accent, 130) : BorderColor);
                LiftCard(lift, 0);
            };
            card.MouseLeftButtonUp += delegate { Play(episode, siblings, GetSavedPosition(episode), false); };
            return card;
        }

        FrameworkElement PosterVisual(Channel channel, double width, double height)
        {
            // Toujours une cover : repli dessiné immédiat + affiche distante qui
            // se pose dessus quand elle arrive. Toutes les vignettes du programme
            // (accueil, top 10, reprises, épisodes, fiches) passent par ici.
            return CoverArt(channel, width, height, width > height);
        }
    }
}
