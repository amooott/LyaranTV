using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
            if (page != "Épisodes") PushNavFrame();
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

            var seasons = episodes.Select(ep => String.IsNullOrWhiteSpace(ep.Season) ? "1" : ep.Season)
                .Distinct().ToList();
            if (seasons.Count == 0) seasons.Add("1");
            string active = String.IsNullOrWhiteSpace(episodesSeason) || !seasons.Contains(episodesSeason) ? seasons[0] : episodesSeason;
            episodesSeason = active;
            Channel next = NextEpisodeToWatch(series, episodes);

            var root = new StackPanel { Margin = new Thickness(0) };
            root.Children.Add(BuildSeriesHero(series, episodes, seasons, next));

            var content = new StackPanel { Margin = new Thickness(30, 18, 30, 34), MaxWidth = 1180, HorizontalAlignment = HorizontalAlignment.Left };

            // Sélecteur de saison : des onglets texte, sans pastille, comme la
            // navigation d'une plateforme de streaming.
            var seasonRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 10) };
            var seasonLabel = Text("SAISONS", 12.5, Brush(Muted), true);
            seasonLabel.VerticalAlignment = VerticalAlignment.Center;
            seasonLabel.Margin = new Thickness(0, 0, 14, 0);
            DockPanel.SetDock(seasonLabel, Dock.Left);
            seasonRow.Children.Add(seasonLabel);
            var seasonBar = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (string season in seasons)
            {
                string captured = season;
                seasonBar.Children.Add(FilterTab("Saison " + season, String.Equals(season, active), delegate
                {
                    episodesSeason = captured;
                    ShowSeriesEpisodes(series, episodes);
                }, "Afficher les épisodes de la saison " + season));
            }
            seasonRow.Children.Add(seasonBar);
            content.Children.Add(seasonRow);

            var list = episodes.Where(ep => String.Equals(String.IsNullOrWhiteSpace(ep.Season) ? "1" : ep.Season, active))
                .OrderBy(ep => TextUtil.ParseInt(ep.Episode, 0)).ToList();
            if (list.Count == 0) list = episodes;

            var countLine = Text(TextUtil.HumanCount(list.Count) + (list.Count > 1 ? " épisodes" : " épisode")
                + "  ·  Saison " + active, 12.5, Brush(Muted), false);
            countLine.Margin = new Thickness(2, 0, 0, 10);
            content.Children.Add(countLine);

            var stack = new StackPanel();
            foreach (Channel episode in list) stack.Children.Add(EpisodeRow(episode, list, currentPlaying));
            content.Children.Add(stack);

            root.Children.Add(content);
            SetContent(root);
            SetStatus(series.Name + " · " + TextUtil.HumanCount(episodes.Count) + " épisode(s).");
            EnsureSeriesEpisodeArtwork(series, episodes);
        }

        /// <summary>
        /// En-tête cinématographique de la série : image d'ambiance, affiche,
        /// titre, informations et actions principales, dans l'esprit d'une fiche
        /// Netflix.
        /// </summary>
        FrameworkElement BuildSeriesHero(Channel series, List<Channel> episodes, List<string> seasons, Channel next)
        {
            string backdrop = !String.IsNullOrWhiteSpace(series.BackdropUrl) ? series.BackdropUrl : series.PosterUrl;
            var hero = new Grid { Height = 356, ClipToBounds = true, Background = Brush(Color.FromRgb(8, 8, 14)) };
            if (!String.IsNullOrWhiteSpace(backdrop))
            {
                hero.Children.Add(new AsyncImage { Url = backdrop, Stretch = Stretch.UniformToFill });
            }
            else
            {
                hero.Children.Add(new Border
                {
                    Background = new LinearGradientBrush(new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(25, 18, 46), 0),
                        new GradientStop(Color.FromRgb(8, 8, 14), 1)
                    }, new Point(0, 0), new Point(1, 1))
                });
                var mark = Icon(Glyphs.Series, 86, Brush(Color.FromArgb(105, 255, 255, 255)));
                mark.HorizontalAlignment = HorizontalAlignment.Center;
                mark.VerticalAlignment = VerticalAlignment.Center;
                hero.Children.Add(mark);
            }
            hero.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(150, 3, 3, 8), 0),
                    new GradientStop(Color.FromArgb(24, 3, 3, 8), 0.35),
                    new GradientStop(Color.FromArgb(92, 3, 3, 8), 0.7),
                    new GradientStop(Color.FromArgb(248, Panel.R, Panel.G, Panel.B), 1)
                }, new Point(0.5, 0), new Point(0.5, 1))
            });
            hero.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(210, 3, 3, 8), 0),
                    new GradientStop(Color.FromArgb(40, 3, 3, 8), 0.6),
                    new GradientStop(Color.FromArgb(0, 3, 3, 8), 1)
                }, new Point(0, 0.5), new Point(1, 0.5))
            });

            var back = PlayerButton(Glyphs.Back, "Retour", delegate { NavBack(); }, false, 38);
            back.HorizontalAlignment = HorizontalAlignment.Left;
            back.VerticalAlignment = VerticalAlignment.Top;
            back.Margin = new Thickness(26, 20, 0, 0);
            hero.Children.Add(back);

            var content = new Grid { Margin = new Thickness(30, 0, 30, 24), VerticalAlignment = VerticalAlignment.Bottom };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var poster = new Border
            {
                Width = 158, Height = 236, CornerRadius = new CornerRadius(12), Background = Brush(Raised),
                BorderBrush = Brush(Color.FromArgb(95, 255, 255, 255)), BorderThickness = new Thickness(1),
                ClipToBounds = true, VerticalAlignment = VerticalAlignment.Bottom,
                Child = PosterVisual(series, 158, 236)
            };
            RoundClip(poster, 12);
            Grid.SetColumn(poster, 0);
            content.Children.Add(poster);

            var copy = new StackPanel { Margin = new Thickness(20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
            var eyebrow = Text("SÉRIE", 12.5, Brush(Color.FromArgb(225, 255, 255, 255)), true);
            copy.Children.Add(eyebrow);
            var title = Text(TextUtil.DisplayTitle(series.Name), 31, Brushes.White, true);
            title.Margin = new Thickness(0, 3, 0, 0);
            title.TextWrapping = TextWrapping.Wrap;
            title.MaxHeight = 82;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            copy.Children.Add(title);

            var metaParts = new List<string>();
            if (!String.IsNullOrWhiteSpace(series.Year)) metaParts.Add(series.Year);
            metaParts.Add(seasons.Count > 1 ? TextUtil.HumanCount(seasons.Count) + " saisons" : "Saison " + seasons[0]);
            metaParts.Add(TextUtil.HumanCount(episodes.Count) + " épisodes");
            if (!String.IsNullOrWhiteSpace(series.Rating)) metaParts.Add(series.Rating + "/10");
            if (series.TmdbRating > 0) metaParts.Add("★ " + series.TmdbRating.ToString("0.0", CultureInfo.InvariantCulture));
            if (!String.IsNullOrWhiteSpace(series.Genres)) metaParts.Add(TextUtil.Ellipsis(series.Genres, 60));
            var meta = Text(String.Join("  ·  ", metaParts.ToArray()), 12.5, Brush(Color.FromArgb(232, 244, 244, 248)), false);
            meta.Margin = new Thickness(0, 6, 0, 0);
            copy.Children.Add(meta);

            string description = !String.IsNullOrWhiteSpace(series.Description) ? series.Description : series.Overview;
            if (!String.IsNullOrWhiteSpace(description))
            {
                var body = Text(description, 12.5, Brush(Color.FromArgb(225, 244, 244, 248)), false);
                body.Margin = new Thickness(0, 8, 0, 0);
                body.MaxWidth = 760;
                body.MaxHeight = 58;
                body.LineHeight = 18;
                body.TextWrapping = TextWrapping.Wrap;
                body.TextTrimming = TextTrimming.CharacterEllipsis;
                copy.Children.Add(body);
            }

            var actions = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            if (next != null)
            {
                double saved = GetSavedPosition(next);
                string label = saved > 1 ? "Reprendre l’épisode " + (String.IsNullOrWhiteSpace(next.Episode) ? "" : next.Episode) : "Lire le premier épisode";
                var play = Button(Glyphs.Play, label.Trim(), delegate { Play(next, episodes, saved, false); }, true);
                play.Margin = new Thickness(0, 0, 8, 8);
                actions.Children.Add(play);
            }
            Button favorite = null;
            favorite = Button(favorites.Contains(series.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite,
                favorites.Contains(series.Id) ? "Favori" : "Ajouter aux favoris", delegate
                {
                    ToggleFavorite(series);
                    favorite.Content = IconText(favorites.Contains(series.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite,
                        favorites.Contains(series.Id) ? "Favori" : "Ajouter aux favoris", Brush(TextPrimary), 12.5);
                }, false);
            favorite.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(favorite);
            if (TmdbClient.IsConfigured(settings))
            {
                var match = Button(Glyphs.Search, "Corriger TMDb", delegate { ShowTmdbMatchDialog(series); }, false);
                match.Margin = new Thickness(0, 0, 8, 8);
                actions.Children.Add(match);
            }
            if (!String.IsNullOrWhiteSpace(series.Url))
            {
                var external = IconButton(Glyphs.External, "Ouvrir dans le lecteur externe", delegate { OpenInExternalPlayer(series.Url); }, false);
                external.Margin = new Thickness(0, 0, 8, 8);
                actions.Children.Add(external);
            }
            copy.Children.Add(actions);

            Grid.SetColumn(copy, 1);
            content.Children.Add(copy);
            hero.Children.Add(content);
            return hero;
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
            int index = siblings == null ? -1 : siblings.FindIndex(delegate(Channel item)
            {
                return item != null && String.Equals(item.Id, episode.Id, StringComparison.OrdinalIgnoreCase);
            });
            Channel next = index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null;
            bool upcoming = !current && next != null
                && String.Equals(next.Id, episode.Id, StringComparison.OrdinalIgnoreCase);
            double saved = GetSavedPosition(episode);
            double progress = ResumeProgress(episode);

            var row = new Grid { Margin = new Thickness(0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Vignette 16:9 avec numéro, état et progression de reprise.
            var thumb = new Border
            {
                Width = 176, Height = 99, CornerRadius = new CornerRadius(10), Background = Brush(Raised),
                ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center
            };
            RoundClip(thumb, 10);
            var thumbGrid = new Grid();
            thumbGrid.Children.Add(PosterVisual(episode, 176, 99));
            thumbGrid.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.35),
                    new GradientStop(Color.FromArgb(150, 0, 0, 0), 1)
                }, new Point(0.5, 0), new Point(0.5, 1))
            });
            var numberBadge = new Border
            {
                Background = Brush(current ? Accent : Color.FromArgb(178, 8, 8, 14)),
                CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 2, 6, 3),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(7)
            };
            numberBadge.Child = Text(String.IsNullOrWhiteSpace(episode.Episode) ? "É" : "É" + episode.Episode,
                10, current ? Brush(Color.FromRgb(12, 12, 18)) : Brushes.White, true);
            thumbGrid.Children.Add(numberBadge);

            var playBadge = new Border
            {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(16),
                Background = Brush(Color.FromArgb(196, 0, 0, 0)),
                BorderBrush = Brush(Color.FromArgb(120, 255, 255, 255)), BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Child = Icon(Glyphs.Play, 13, Brushes.White), IsHitTestVisible = false,
                Opacity = current ? 1 : 0.82
            };
            thumbGrid.Children.Add(playBadge);

            if (current || upcoming)
            {
                var stateBadge = new Border
                {
                    Background = Brush(current ? Accent : WithAlpha(Accent, 225)),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 3),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(7)
                };
                stateBadge.Child = Text(current ? "EN COURS" : "À SUIVRE", 9, Brush(Color.FromRgb(12, 12, 18)), true);
                thumbGrid.Children.Add(stateBadge);
            }

            if (progress > 0)
            {
                var bar = new Grid { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(9, 0, 9, 7) };
                bar.Children.Add(new Border
                {
                    Height = 3, CornerRadius = new CornerRadius(2), Background = Brush(Color.FromArgb(110, 255, 255, 255))
                });
                bar.Children.Add(new Border
                {
                    Height = 3, CornerRadius = new CornerRadius(2), Background = Brush(Accent),
                    HorizontalAlignment = HorizontalAlignment.Left, Width = 158 * progress
                });
                thumbGrid.Children.Add(bar);
            }
            thumb.Child = thumbGrid;
            Grid.SetColumn(thumb, 0);
            row.Children.Add(thumb);

            var copy = new StackPanel { Margin = new Thickness(16, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
            string eyebrow = String.IsNullOrWhiteSpace(episode.Episode) ? "ÉPISODE" : "ÉPISODE " + episode.Episode;
            if (!String.IsNullOrWhiteSpace(episode.Season)) eyebrow += "  ·  SAISON " + episode.Season;
            copy.Children.Add(Text(eyebrow, 11, Brush(current ? Accent : Muted), true));

            string displayName = (episode.Name ?? "").Trim().TrimEnd(' ', '-', '–', '—', '·', ':', '|');
            if (displayName.Length == 0)
                displayName = String.IsNullOrWhiteSpace(episode.Episode) ? "Épisode" : "Épisode " + episode.Episode;
            var name = Text(displayName, 14, Brush(current ? Accent : TextPrimary), true);
            name.Margin = new Thickness(0, 2, 0, 0);
            name.TextWrapping = TextWrapping.Wrap;
            name.MaxHeight = 40;
            copy.Children.Add(name);

            string detail = "";
            if (!String.IsNullOrWhiteSpace(episode.Runtime)) detail = episode.Runtime;
            if (!String.IsNullOrWhiteSpace(episode.Description))
            {
                string synopsis = TextUtil.Ellipsis(episode.Description, 190);
                detail = detail.Length > 0 ? detail + "  ·  " + synopsis : synopsis;
            }
            if (detail.Length > 0)
            {
                var line = Text(detail, 12, Brush(Muted), false);
                line.Margin = new Thickness(0, 4, 0, 0);
                line.TextWrapping = TextWrapping.Wrap;
                line.MaxHeight = 34;
                copy.Children.Add(line);
            }
            if (saved > 1)
            {
                var resume = Text((current ? "Lecture en cours" : "Reprendre") + " à "
                    + TextUtil.FormatPosition(TimeSpan.FromSeconds(saved)), 12, Brush(Accent), true);
                resume.Margin = new Thickness(0, 5, 0, 0);
                copy.Children.Add(resume);
            }
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);

            Action launch = delegate { Play(episode, siblings, GetSavedPosition(episode), false); };
            var play = current
                ? Button(Glyphs.Play, "En cours", launch, true)
                : upcoming ? Button(Glyphs.Play, "Lire", launch, true)
                : Button(Glyphs.Play, "Lire", launch, false);
            play.Margin = new Thickness(0);
            play.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(play, 2);
            row.Children.Add(play);

            Brush restingBorder = Brush(current ? WithAlpha(Accent, 150)
                : upcoming ? WithAlpha(Accent, 85) : BorderColor);
            var card = new Border
            {
                Child = row, Padding = new Thickness(10), CornerRadius = new CornerRadius(14),
                Background = Brush(current ? WithAlpha(Accent, 24) : upcoming ? WithAlpha(Accent, 10) : Panel),
                BorderBrush = restingBorder, BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand,
                Focusable = true, FocusVisualStyle = null, SnapsToDevicePixels = true
            };
            var lift = new TranslateTransform();
            card.RenderTransform = lift;
            card.MouseEnter += delegate
            {
                card.BorderBrush = Brush(Accent);
                card.Background = Brush(current ? WithAlpha(Accent, 34) : WithAlpha(TextPrimary, 9));
                playBadge.Opacity = 1;
                LiftCard(lift, -2);
            };
            card.MouseLeave += delegate
            {
                if (!card.IsKeyboardFocusWithin)
                {
                    card.BorderBrush = restingBorder;
                    card.Background = Brush(current ? WithAlpha(Accent, 24) : upcoming ? WithAlpha(Accent, 10) : Panel);
                    playBadge.Opacity = current ? 1 : 0.82;
                }
                LiftCard(lift, 0);
            };
            card.GotKeyboardFocus += delegate { card.BorderBrush = Brush(Accent); };
            card.LostKeyboardFocus += delegate
            {
                card.BorderBrush = restingBorder;
                card.Background = Brush(current ? WithAlpha(Accent, 24) : upcoming ? WithAlpha(Accent, 10) : Panel);
            };
            card.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key != Key.Enter && e.Key != Key.Space) return;
                launch();
                e.Handled = true;
            };
            card.MouseLeftButtonUp += delegate { launch(); };
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
