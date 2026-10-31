using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LyaranTV
{
    public sealed partial class MainWindow
    {
        Channel LastPlayedChannel()
        {
            var channel = FindChannel(settings.LastChannelId);
            if (channel != null) return channel;
            foreach (string id in recentIds) { channel = FindChannel(id); if (channel != null) return channel; }
            foreach (string id in favorites) { channel = FindChannel(id); if (channel != null) return channel; }
            return channels.FirstOrDefault(c => c.Kind == "Chaînes") ?? channels.FirstOrDefault();
        }

        void LoadDemo()
        {
            var source = sources.FirstOrDefault(s => s.IsDemo || s.Type == "Démo");
            if (source == null)
            {
                source = new PlaylistSource { Name = "Démo LyaranTV", Type = "Démo", IsDemo = true };
                sources.Add(source);
            }
            activeSource = source;
            ApplyLoadResult(source, PlaylistLoader.DemoLibrary());
        }

        // Catalogue d'accueil : une seule passe sur les 240 000 éléments d'un
        // gros compte, puis chaque étagère travaille sur les films et séries
        // uniques. Sans cela, six étagères refaisaient chacune un regroupement
        // et un tri complet du catalogue, d'où plusieurs secondes d'attente.
        readonly List<Channel> homeUniqueMovies = new List<Channel>();
        readonly List<Channel> homeUniqueSeries = new List<Channel>();
        readonly List<Channel> homeUniqueTitles = new List<Channel>();
        readonly List<Channel> homeTopRated = new List<Channel>();
        readonly List<Channel> homeNovelties = new List<Channel>();
        string homeCatalogStamp = "";

        string CatalogStamp(IList<Channel> list)
        {
            return list.Count + "|" + hiddenChannels.Count + "|" + hiddenGroups.Count + "|"
                + (settings.ParentalEnabled ? "1" : "0") + "|" + (settings.LockedGroups ?? "") + "|" + ProfileScope.Current
                + "|" + (list.Count > 0 ? list[0].Id : "") + "|" + (list.Count > 0 ? list[list.Count - 1].Id : "");
        }

        /// <summary>
        /// Prépare le catalogue de l'accueil. Peut être appelé depuis le fil de
        /// restauration pour que l'ouverture de la page n'attende pas : l'empreinte
        /// évite alors tout second parcours.
        /// </summary>
        void BuildHomeCatalog(IList<Channel> source = null)
        {
            // Le catalogue ne change qu'au chargement d'une playlist ou au
            // masquage d'une catégorie : on le conserve entre deux visites de
            // l'accueil au lieu de reparcourir 240 000 éléments à chaque fois.
            IList<Channel> list = source ?? channels;
            string stamp = CatalogStamp(list);
            if (stamp == homeCatalogStamp && homeUniqueTitles.Count > 0) return;
            homeCatalogStamp = stamp;
            homeUniqueMovies.Clear(); homeUniqueSeries.Clear(); homeUniqueTitles.Clear();
            homeTopRated.Clear(); homeNovelties.Clear();
            if (list.Count == 0) return;
            var movies = new Dictionary<string, Channel>(StringComparer.OrdinalIgnoreCase);
            var series = new Dictionary<string, Channel>(StringComparer.OrdinalIgnoreCase);
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var channel in list)
            {
                if (channel == null || hiddenChannels.Contains(channel.Id) || !IsChannelAllowed(channel)) continue;
                bool movie = channel.Kind == "Films";
                bool show = channel.Kind == "Séries";
                if (!movie && !show) continue;
                if (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode)) continue;
                string rawName = channel.Name ?? "";
                string key;
                if (!keys.TryGetValue(rawName, out key)) { key = TextUtil.NormalizeKey(rawName); keys[rawName] = key; }
                if (String.IsNullOrWhiteSpace(key)) continue;
                var target = movie ? movies : series;
                Channel current;
                if (!target.TryGetValue(key, out current) || BetterHomeEntry(channel, current)) target[key] = channel;
            }
            homeUniqueMovies.AddRange(movies.Values);
            homeUniqueSeries.AddRange(series.Values);
            homeUniqueMovies.Sort(CompareHomeRating);
            homeUniqueSeries.Sort(CompareHomeRating);
            homeUniqueTitles.AddRange(homeUniqueMovies);
            homeUniqueTitles.AddRange(homeUniqueSeries);
            homeTopRated.AddRange(homeUniqueTitles);
            homeTopRated.Sort(CompareHomeRating);
            homeNovelties.AddRange(homeUniqueTitles);
            homeNovelties.Sort(delegate(Channel first, Channel second) { return second.Order.CompareTo(first.Order); });
        }

        static int CompareHomeRating(Channel first, Channel second)
        {
            int score = second.TmdbRating.CompareTo(first.TmdbRating);
            if (score != 0) return score;
            score = (second.BackdropUrl ?? "").Length.CompareTo((first.BackdropUrl ?? "").Length);
            if (score != 0) return score;
            return StringComparer.CurrentCultureIgnoreCase.Compare(first.Name, second.Name);
        }

        static bool BetterHomeEntry(Channel candidate, Channel current)
        {
            if (current == null) return true;
            if (candidate.TmdbRating != current.TmdbRating) return candidate.TmdbRating > current.TmdbRating;
            return (candidate.BackdropUrl ?? "").Length > (current.BackdropUrl ?? "").Length;
        }

        IEnumerable<Channel> HomeHeroCandidates()
        {
            return homeTopRated;
        }

        void ShowHome()
        {
            var homePerf = System.Diagnostics.Stopwatch.StartNew();
            BuildHomeCatalog();
            try { HttpUtil.AppendLog("Perf accueil · catalogue " + homePerf.ElapsedMilliseconds + " ms"); } catch { }
            page = "Accueil"; RebuildSidebar(); UpdateStatusBar();
            SetPageStatus();
            string greeting = GreetingForHour();
            if (activeProfile != null && !String.IsNullOrWhiteSpace(activeProfile.Name)) greeting += ", " + activeProfile.Name;
            var shellRoot = new StackPanel { Margin = new Thickness(0, 0, 0, 36) };
            shellRoot.Children.Add(BuildHomeBillboard());
            try { HttpUtil.AppendLog("Perf accueil · bannière " + homePerf.ElapsedMilliseconds + " ms"); } catch { }
            shellRoot.Children.Add(BuildHomeIntroRow(greeting));
            var root = new StackPanel { Margin = new Thickness(30, 2, 30, 0) };
            shellRoot.Children.Add(root);

            // Rangée « Reprendre la lecture » : séries (via leur épisode en cours),
            // films, épisodes et fichiers locaux, avec progression et temps restant.
            var resumeShelf = BuildResumeShelf(14);
            if (resumeShelf != null) root.Children.Add(resumeShelf);

            // « Ma liste » d'abord : les contenus enregistrés par le profil.
            var watchlist = BuildHomeCardShelf("Ma liste", "Vos films, séries et chaînes enregistrés, prêts à être lancés.",
                VisibleChannels.Where(c => favorites.Contains(c.Id)), "Favoris");
            if (watchlist != null) root.Children.Add(watchlist);

            var topTen = BuildTopTenShelf();
            if (topTen != null) root.Children.Add(topTen);

            // Nouveautés : les derniers contenus ajoutés à la playlist.
            var novelties = BuildHomeCardShelf("Nouveautés", "Les derniers contenus ajoutés à votre playlist.",
                homeNovelties, "Bibliothèque", true);
            if (novelties != null) root.Children.Add(novelties);

            var filmShelf = BuildPosterShelf("Films à voir", "Les films les mieux notés de votre playlist.", homeUniqueMovies);
            if (filmShelf != null) root.Children.Add(filmShelf);

            var seriesShelf = BuildPosterShelf("Séries à suivre", "Vos séries, prêtes à être enchaînées.", homeUniqueSeries);
            if (seriesShelf != null) root.Children.Add(seriesShelf);

            var liveShelf = BuildLiveShelf();
            if (liveShelf != null) root.Children.Add(liveShelf);
            try { HttpUtil.AppendLog("Perf accueil · étagères " + homePerf.ElapsedMilliseconds + " ms"); } catch { }

            var playlistHead = SectionHeader("Vos playlists", "Ajoutez, actualisez et organisez vos sources.", Button(Glyphs.Add, "Ajouter", AddPlaylist, false));
            root.Children.Add(playlistHead);
            if (sources.Count == 0)
            {
                var emptyActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                emptyActions.Children.Add(Button(Glyphs.Add, "Ajouter une playlist", AddPlaylist, true));
                emptyActions.Children.Add(Button(Glyphs.Play, "Essayer la démo", LoadDemo, false));
                root.Children.Add(EmptyState(Glyphs.Library, "Aucune playlist pour le moment", "Importez un fichier M3U/M3U8, connectez un compte Xtream ou ajoutez un portail MAC. Vous pouvez aussi essayer la démonstration intégrée.", null));
                root.Children.Add(emptyActions);
                var steps = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
                string[] headings = { "1 · Ajoutez votre source", "2 · Parcourez la bibliothèque", "3 · Regardez et gardez vos favoris" };
                string[] descriptions = { "Fichier M3U, URL, Xtream Codes ou portail MAC.", "Catégories, recherche, films, séries et guide EPG.", "Lecture intégrée, reprise, favoris et réglages." };
                string[] icons = { Glyphs.Add, Glyphs.Library, Glyphs.Play };
                for (int i = 0; i < 3; i++)
                {
                    var stack = new StackPanel();
                    var icon = Icon(icons[i], 22, Brush(Accent)); icon.Margin = new Thickness(0, 0, 0, 8); stack.Children.Add(icon);
                    stack.Children.Add(Text(headings[i], 14, Brush(TextPrimary), true));
                    stack.Children.Add(Text(descriptions[i], 11, Brush(Muted), false));
                    var card = Card(stack, 16); card.Width = 250; card.Margin = new Thickness(0, 0, 12, 10); steps.Children.Add(card);
                }
                root.Children.Add(steps);
            }
            else
            {
                var sourceCards = new WrapPanel();
                foreach (var source in sources) sourceCards.Children.Add(BuildSourceCard(source));
                root.Children.Add(sourceCards);
            }

            var recentChannels = recentIds.Select(FindChannel).Where(c => c != null && IsChannelAllowed(c)).Take(6).ToList();
            if (recentChannels.Count > 0)
            {
                root.Children.Add(SectionHeader("Récemment regardés", "Reprenez où vous en étiez.", Button(Glyphs.Recent, "Historique", delegate { Nav("Récents"); }, false)));
                var wrap = new WrapPanel();
                foreach (var channel in recentChannels) wrap.Children.Add(HomeChannelTile(channel));
                root.Children.Add(wrap);
            }

            var nowPrograms = VisibleChannels.Where(c => c.Kind == "Chaînes").Select(c => new { Channel = c, Program = CurrentProgram(c) }).Where(x => x.Program != null).Take(6).ToList();
            if (nowPrograms.Count > 0)
            {
                root.Children.Add(SectionHeader("À l’antenne", "Ce qui passe en ce moment.", Button(Glyphs.Guide, "Ouvrir le guide", delegate { Nav("Guide EPG"); }, false)));
                var wrap = new WrapPanel();
                foreach (var item in nowPrograms) wrap.Children.Add(NowProgramCard(item.Channel, item.Program));
                root.Children.Add(wrap);
            }
            else if (loadedEpg.Count == 0 && channels.Count > 0)
            {
                root.Children.Add(Card(Text("Conseil : ajoutez une URL XMLTV ou importez un guide dans « Guide EPG » pour afficher les programmes en cours.", 12, Brush(Muted), false), 16));
            }
            SetContent(shellRoot);
            try { HttpUtil.AppendLog("Perf accueil · " + homePerf.ElapsedMilliseconds + " ms · " + channels.Count + " éléments"); } catch { }
            StartHomeHeroTimer();
        }

        Border BuildSourceCard(PlaylistSource source)
        {
            var stack = new StackPanel();
            var head = new DockPanel { LastChildFill = true };
            var icon = Icon(source.IsDemo || source.Type == "Démo" ? Glyphs.Play : source.Type == "Xtream" ? Glyphs.Globe : source.Type == "Portail MAC" ? Glyphs.TV : Glyphs.List, 20, Brush(Accent));
            icon.Margin = new Thickness(0, 0, 10, 0); DockPanel.SetDock(icon, Dock.Left); head.Children.Add(icon);
            var titleStack = new StackPanel(); titleStack.Children.Add(Text(source.Name, 15, Brush(TextPrimary), true)); titleStack.Children.Add(Text(source.DisplayType, 10.5, Brush(Muted), false)); head.Children.Add(titleStack);
            if (source == activeSource) { var badge = Badge("ACTIVE", AccentSoftBrush, Brush(Accent)); DockPanel.SetDock(badge, Dock.Right); head.Children.Add(badge); }
            stack.Children.Add(head);
            string detail = source.LastCount > 0 ? TextUtil.HumanCount(source.LastCount) + " éléments" : "Pas encore chargée";
            if (!String.IsNullOrWhiteSpace(source.LastLoaded)) detail += " · " + source.LastLoaded;
            var detailText = Text(detail, 10.5, Brush(Muted), false); detailText.Margin = new Thickness(0, 7, 0, 0); stack.Children.Add(detailText);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 13, 0, 0) };
            var open = Button(Glyphs.Play, source == activeSource && channels.Count > 0 ? "Ouvrir" : "Charger", delegate { Activate(source); }, source == activeSource && channels.Count > 0);
            open.Margin = new Thickness(0, 0, 7, 0); buttons.Children.Add(open);
            buttons.Children.Add(IconButton(Glyphs.Refresh, "Actualiser", delegate { Activate(source); }, false));
            buttons.Children.Add(IconButton(Glyphs.Edit, "Modifier", delegate { EditSource(source); }, false));
            buttons.Children.Add(IconButton(Glyphs.Delete, "Supprimer", delegate { RemoveSource(source); }, false));
            stack.Children.Add(buttons);
            var card = Card(stack, 15);
            card.Width = 292; card.Margin = new Thickness(0, 0, 12, 12);
            AddHover(card, Brush(Panel), Brush(Raised), Brush(BorderColor), Brush(Accent));
            var menu = CreateContextMenu();
            menu.Items.Add(MenuItem("Charger", delegate { Activate(source); }, Glyphs.Play));
            menu.Items.Add(MenuItem("Modifier", delegate { EditSource(source); }, Glyphs.Edit));
            menu.Items.Add(MenuItem("Tester la connexion", delegate { TestSourceAsync(source); }, Glyphs.Globe));
            menu.Items.Add(MenuItem("Supprimer", delegate { RemoveSource(source); }, Glyphs.Delete));
            card.ContextMenu = menu;
            return card;
        }

        Border HomeChannelTile(Channel channel)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var logo = BuildThumb(channel, 34, 51); Grid.SetColumn(logo, 0); grid.Children.Add(logo);
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 4, 0) };
            labels.Children.Add(Text(channel.Name, 12.5, Brush(TextPrimary), true));
            string meta = channel.Group;
            if (channel.TmdbRating > 0) meta += "  ·  ★ " + channel.TmdbRating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            labels.Children.Add(Text(meta, 9.5, Brush(Muted), false));
            Grid.SetColumn(labels, 1); grid.Children.Add(labels);
            var card = new Border
            {
                Child = grid, Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11), Padding = new Thickness(8), Width = 218, Height = 68, Margin = new Thickness(0, 0, 10, 10),
                Cursor = Cursors.Hand, ToolTip = channel.Group + " · " + channel.Name
            };
            AddHover(card, Brush(Panel), Brush(Raised), Brush(BorderColor), Brush(Accent));
            card.MouseLeftButtonUp += delegate
            {
                if (channel.Kind == "Films" || channel.Kind == "Séries") ShowChannelInfo(channel);
                else Play(channel, null, GetSavedPosition(channel), false);
            };
            var menu = CreateContextMenu();
            menu.Items.Add(MenuItem("Lire", delegate { Play(channel, null, GetSavedPosition(channel), false); }, Glyphs.Play));
            menu.Items.Add(MenuItem(favorites.Contains(channel.Id) ? "Retirer des favoris" : "Ajouter aux favoris", delegate { ToggleFavorite(channel); }, Glyphs.Favorite));
            if (channel.Kind == "Films" || channel.Kind == "Séries")
                menu.Items.Add(MenuItem("Voir la fiche", delegate { ShowChannelInfo(channel); }, Glyphs.Info));
            menu.Items.Add(MenuItem("Masquer", delegate { hiddenChannels.Add(channel.Id); SaveState(); ShowHome(); }, Glyphs.View));
            card.ContextMenu = menu;
            return card;
        }

        Border NowProgramCard(Channel channel, EpgProgram program)
        {
            var stack = new StackPanel();
            var top = new DockPanel { LastChildFill = true };
            var channelText = Text(channel.Name, 11, Brush(Accent), true); DockPanel.SetDock(channelText, Dock.Left); top.Children.Add(channelText);
            var time = Text(program.TimeRange, 10, Brush(Muted), false); time.HorizontalAlignment = HorizontalAlignment.Right; top.Children.Add(time);
            stack.Children.Add(top);
            var title = Text(program.Title, 13, Brush(TextPrimary), true); title.Margin = new Thickness(0, 4, 0, 5); stack.Children.Add(title);
            var progress = StyledProgressBar(); progress.Maximum = Math.Max(1, program.Duration.TotalSeconds);
            progress.Value = Math.Max(0, Math.Min(progress.Maximum, (DateTimeOffset.Now - program.Start).TotalSeconds)); stack.Children.Add(progress);
            var card = new Border
            {
                Child = stack, Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11), Padding = new Thickness(12), Width = 250, Height = 102, Margin = new Thickness(0, 0, 10, 10), Cursor = Cursors.Hand
            };
            AddHover(card, Brush(Panel), Brush(Raised), Brush(BorderColor), Brush(Accent));
            card.MouseLeftButtonUp += delegate { Play(channel, null, 0, false); };
            card.ToolTip = program.Description;
            return card;
        }

        void ShowSearch()
        {
            page = "Recherche"; RebuildSidebar(); UpdateStatusBar();
            SetPageStatus();
            string query = searchQuery;
            var root = PageRoot(query.Length == 0 ? "Recherche" : "Résultats pour « " + query + " »", "Titres, synopsis, personnes et crédits de la playlist active.");
            if (activeSource == null || channels.Count == 0)
            {
                root.Children.Add(EmptyState(Glyphs.Search, "Aucune bibliothèque chargée", "Ajoutez ou chargez une playlist pour lancer une recherche.", Button(Glyphs.Add, "Ajouter une playlist", AddPlaylist, true)));
                SetContent(root); return;
            }
            var modeBase = BaseViewForMode("Recherche").ToList();
            var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            AddKindChip(chips, "Tous", "Tous", modeBase.Count);
            AddKindChip(chips, "Chaînes", "Chaînes", modeBase.Count(c => c.Kind == "Chaînes"));
            AddKindChip(chips, "Films", "Films", modeBase.Count(c => c.Kind == "Films"));
            AddKindChip(chips, "Séries", "Séries", modeBase.Count(c => c.Kind == "Séries"));
            root.Children.Add(chips);
            resultCountText = Text("", 11, Brush(Muted), false); resultCountText.Margin = new Thickness(0, 0, 0, 8); root.Children.Add(resultCountText);
            channelList = new StackPanel();
            resultsScroll = null;
            root.Children.Add(channelList);
            RenderChannels();
            SetContent(root);
        }

        void AddKindChip(Panel panel, string label, string kind, int count)
        {
            string captured = kind;
            var chip = Chip(label + "  " + TextUtil.HumanCount(count), activeKind == captured, delegate
            {
                activeKind = captured; activeGroup = AllGroups;
                RenderChannels();
            });
            panel.Children.Add(chip);
        }

        IEnumerable<Channel> BaseViewForMode(string mode)
        {
            return BaseViewForMode(mode, channels);
        }

        /// <summary>
        /// Même vue que <see cref="VisibleChannels"/> mais calculée sur un instantané,
        /// pour pouvoir préparer la bibliothèque hors du fil d'interface.
        /// </summary>
        IEnumerable<Channel> BaseViewForMode(string mode, IList<Channel> source)
        {
            IEnumerable<Channel> view = (source ?? new List<Channel>()).Where(c => c != null && IsChannelAllowed(c));
            if (settings.LanguageModeEnabled && !String.IsNullOrWhiteSpace(settings.LanguageModeCode))
                view = view.Where(c => IsLanguageCategory(c.Group));
            view = view.Where(c => !hiddenChannels.Contains(c.Id));
            if (mode == "Favoris") view = view.Where(c => favorites.Contains(c.Id));
            else if (mode == "Récents")
            {
                var order = recentIds.Select((id, index) => new { id, index }).ToDictionary(x => x.id, x => x.index, StringComparer.OrdinalIgnoreCase);
                view = view.Where(c => order.ContainsKey(c.Id)).OrderBy(c => order[c.Id]);
            }
            return view;
        }

        void RenderChannels() { RenderChannels(true); }

        void RenderChannels(bool resetLimit)
        {
            if (channelList == null) return;
            if (resetLimit) renderLimit = 150;
            double offset = resultsScroll == null ? 0 : resultsScroll.VerticalOffset;
            var baseView = BaseViewForMode(page).ToList();
            IEnumerable<Channel> view = baseView;
            if (activeKind != "Tous") view = view.Where(c => c.Kind == activeKind);
            if (activeGroup != AllGroups) view = view.Where(c => String.Equals(c.Group, activeGroup, StringComparison.OrdinalIgnoreCase));
            string query = String.IsNullOrWhiteSpace(searchQuery) ? "" : searchQuery.Trim();
            if (query.Length > 0) view = view.Where(c => c.MatchesSearch(query));
            currentView = SortChannels(view).ToList();
            bool groupedSearch = page == "Recherche" && query.Length > 0 && activeKind == "Tous" && activeGroup == AllGroups;
            List<Tuple<string, string, Channel>> people = groupedSearch ? SearchPeople(query, currentView) : new List<Tuple<string, string, Channel>>();
            if (resultCountText != null)
            {
                string suffix = activeGroup != AllGroups ? " dans « " + activeGroup + " »" : "";
                if (query.Length > 0) suffix += " pour « " + query + " »";
                if (groupedSearch)
                {
                    var counts = currentView
                        .GroupBy(SearchSectionKind).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
                    var summary = new List<string>();
                    foreach (string kind in new[] { "Films", "Séries", "Épisodes", "Chaînes", "Autres" })
                    {
                        int count;
                        if (counts.TryGetValue(kind, out count) && count > 0) summary.Add(kind + " " + count.ToString(CultureInfo.InvariantCulture));
                    }
                    if (people.Count > 0) summary.Add("Personnes " + people.Count.ToString(CultureInfo.InvariantCulture));
                    resultCountText.Text = TextUtil.HumanCount(currentView.Count) + " contenus" + (summary.Count == 0 ? "" : " · " + String.Join(" · ", summary.ToArray())) + suffix;
                }
                else resultCountText.Text = TextUtil.HumanCount(currentView.Count) + (currentView.Count == 1 ? " résultat" : " résultats") + suffix;
            }
            channelList.Children.Clear();
            int limit = Math.Min(renderLimit, currentView.Count);
            if (groupedSearch)
                RenderGroupedSearchResults(query, currentView, people, limit);
            else if (gridView)
            {
                var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
                for (int i = 0; i < limit; i++) wrap.Children.Add(RenderChannelCard(currentView[i]));
                channelList.Children.Add(wrap);
            }
            else
            {
                for (int i = 0; i < limit; i++) channelList.Children.Add(RenderChannelRow(currentView[i]));
            }
            renderedCount = limit;
            if (currentView.Count > limit)
            {
                moreHost = new Border { Margin = new Thickness(0, 6, 0, 8) };
                var more = Button(Glyphs.ChevronDown, "Afficher plus  ·  " + TextUtil.HumanCount(currentView.Count - limit) + " restants", delegate
                {
                    renderLimit += 200; RenderChannels(false);
                }, false);
                more.HorizontalAlignment = HorizontalAlignment.Center; more.Margin = new Thickness(0);
                moreHost.Child = more; channelList.Children.Add(moreHost);
            }
            else if (currentView.Count > 0 && currentView.Count > 150)
            {
                var end = Text("Fin de la liste · " + TextUtil.HumanCount(currentView.Count) + " éléments", 10.5, Brush(Muted), false);
                end.HorizontalAlignment = HorizontalAlignment.Center; end.Margin = new Thickness(0, 12, 0, 4); channelList.Children.Add(end);
            }
            if (currentView.Count == 0)
            {
                string message = query.Length > 0 ? "Aucun élément ne correspond à votre recherche. Essayez un autre mot-clé ou changez de catégorie." : "Aucun élément à afficher avec ces filtres.";
                var empty = EmptyState(Glyphs.Search, "Rien à afficher", message, null);
                empty.Margin = new Thickness(0, 20, 0, 0); channelList.Children.Add(empty);
            }
            if (resultsScroll != null)
            {
                Dispatcher.BeginInvoke(new Action(delegate { try { resultsScroll.ScrollToVerticalOffset(offset); } catch { } }), DispatcherPriority.Background);
            }
        }

        List<Tuple<string, string, Channel>> SearchPeople(string query, IEnumerable<Channel> source)
        {
            var hits = new List<Tuple<string, string, Channel>>();
            foreach (var channel in source ?? Enumerable.Empty<Channel>())
            {
                if (channel == null || hiddenChannels.Contains(channel.Id) || !IsChannelAllowed(channel)) continue;
                foreach (string name in (channel.Cast ?? "").Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()).Where(value => value.Length > 1))
                    if (TextUtil.Contains(name, query)) hits.Add(Tuple.Create(name, "Acteur", channel));
                foreach (string name in (channel.Director ?? "").Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()).Where(value => value.Length > 1))
                    if (TextUtil.Contains(name, query)) hits.Add(Tuple.Create(name, "Réalisation", channel));
            }
            return hits.GroupBy(hit => TextUtil.NormalizeKey(hit.Item1), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(hit => hit.Item1, StringComparer.CurrentCultureIgnoreCase).First()).Take(12).ToList();
        }

        static string SearchSectionKind(Channel channel)
        {
            if (channel == null) return "Autres";
            if (channel.Kind == "Séries" && (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode))) return "Épisodes";
            if (channel.Kind == "Films" || channel.Kind == "Séries" || channel.Kind == "Chaînes") return channel.Kind;
            return "Autres";
        }

        void RenderGroupedSearchResults(string query, List<Channel> results,
            List<Tuple<string, string, Channel>> people, int limit)
        {
            int remaining = limit;
            if (people.Count > 0)
            {
                channelList.Children.Add(SectionHeader("Personnes", TextUtil.HumanCount(people.Count) + " personne(s) dans les crédits chargés.", null));
                var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var person in people)
                {
                    var content = new StackPanel();
                    content.Children.Add(Text(person.Item1, 14, Brush(TextPrimary), true));
                    content.Children.Add(Text(person.Item2 + " · " + person.Item3.Name, 10.5, Brush(Muted), false));
                    var card = Card(content, 13);
                    card.Width = 240;
                    card.Margin = new Thickness(0, 0, 10, 9);
                    card.Cursor = Cursors.Hand;
                    card.Focusable = true;
                    card.KeyDown += delegate(object sender, KeyEventArgs e)
                    {
                        if (e.Key == Key.Enter || e.Key == Key.Space) { ShowChannelInfo(person.Item3); e.Handled = true; }
                    };
                    card.MouseLeftButtonUp += delegate { ShowChannelInfo(person.Item3); };
                    wrap.Children.Add(card);
                }
                channelList.Children.Add(wrap);
            }
            var contentResults = results;
            var groups = contentResults.GroupBy(SearchSectionKind)
                .OrderBy(group => SearchSectionOrder(group.Key)).ToList();
            foreach (var group in groups)
            {
                if (remaining <= 0) break;
                channelList.Children.Add(SectionHeader(group.Key, TextUtil.HumanCount(group.Count()) + " résultat(s)", null));
                int take = Math.Min(remaining, group.Count());
                if (gridView)
                {
                    var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
                    foreach (var channel in group.Take(take)) wrap.Children.Add(RenderChannelCard(channel));
                    channelList.Children.Add(wrap);
                }
                else foreach (var channel in group.Take(take)) channelList.Children.Add(RenderChannelRow(channel));
                remaining -= take;
            }
            renderedCount = limit - remaining;
        }

        static int SearchSectionOrder(string kind)
        {
            if (kind == "Films") return 0;
            if (kind == "Séries") return 1;
            if (kind == "Épisodes") return 2;
            if (kind == "Chaînes") return 3;
            return 4;
        }

        string FirstVisibleLetter(ScrollViewer viewer, Panel list)
        {
            if (viewer == null || list == null) return "";
            try
            {
                foreach (UIElement child in list.Children)
                {
                    var wrap = child as WrapPanel;
                    if (wrap != null)
                    {
                        foreach (UIElement item in wrap.Children)
                        {
                            string letter = VisibleLetter(viewer, item);
                            if (letter.Length > 0) return letter;
                        }
                    }
                    else
                    {
                        string letter = VisibleLetter(viewer, child);
                        if (letter.Length > 0) return letter;
                    }
                }
            }
            catch { }
            return "";
        }

        string VisibleLetter(ScrollViewer viewer, UIElement element)
        {
            var framework = element as FrameworkElement;
            if (framework == null) return "";
            try
            {
                Point point = framework.TranslatePoint(new Point(0, 0), viewer);
                if (point.Y + framework.ActualHeight < 2) return "";
                if (point.Y > viewer.ViewportHeight - 2) return "";
                return FirstLetter(framework);
            }
            catch { return ""; }
        }

        string FirstLetter(DependencyObject element)
        {
            var text = element as TextBlock;
            if (text != null && !String.IsNullOrWhiteSpace(text.Text))
            {
                string value = text.Text.TrimStart();
                if (value.Length > 0)
                {
                    char first = Char.ToUpperInvariant(value[0]);
                    if (Char.IsLetter(first)) return first.ToString();
                    if (Char.IsDigit(first)) return "#";
                }
            }
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                string letter = FirstLetter(VisualTreeHelper.GetChild(element, i));
                if (letter.Length > 0) return letter;
            }
            return "";
        }

        List<Channel> SortChannels(IEnumerable<Channel> view)
        {
            return SortChannels(view, sortMode);
        }

        List<Channel> SortChannels(IEnumerable<Channel> view, string sort)
        {
            if (sort == "Nom (Z → A)") return view.OrderByDescending(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (sort == "Catégorie") return view.OrderBy(c => c.Group, StringComparer.CurrentCultureIgnoreCase).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (sort == "Numéro") return view.OrderBy(c => TextUtil.ParseInt(c.Number, Int32.MaxValue)).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (sort == "Ordre de la playlist") return view.OrderBy(c => c.Order).ToList();
            return view.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        static string[] SortOptions() { return new[] { "Nom (A → Z)", "Nom (Z → A)", "Catégorie", "Numéro", "Ordre de la playlist" }; }

        Border RenderChannelRow(Channel channel)
        {
            var grid = new Grid { Height = 68 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var logo = BuildThumb(channel, 40, 60); Grid.SetColumn(logo, 0); grid.Children.Add(logo);
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
            labels.Children.Add(Text(channel.Name, 13, Brush(TextPrimary), true));
            string second = channel.Group + "  ·  " + KindLabel(channel.Kind);
            if (channel.TmdbRating > 0)
                second += "  ·  ★ " + channel.TmdbRating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            if (settings.ShowEpgInList)
            {
                var current = CurrentProgram(channel);
                if (current != null) second = "À l’antenne · " + current.Title;
                else
                {
                    var next = NextProgram(channel);
                    if (next != null) second = "À suivre · " + next.Title;
                }
            }
            labels.Children.Add(Text(second, 10.5, Brush(Muted), false));
            Grid.SetColumn(labels, 1); grid.Children.Add(labels);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            var star = IconButton(favorites.Contains(channel.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite, favorites.Contains(channel.Id) ? "Retirer des favoris" : "Ajouter aux favoris", delegate { ToggleFavorite(channel); }, false);
            star.Width = 34; star.Height = 34; star.Margin = new Thickness(0, 0, 4, 0); actions.Children.Add(star);
            var play = IconButton(Glyphs.Play, channel.IsSeries ? "Voir les épisodes" : "Lire", delegate { Play(channel, currentView); }, false);
            play.Width = 34; play.Height = 34; play.Margin = new Thickness(0); actions.Children.Add(play);
            Grid.SetColumn(actions, 2); grid.Children.Add(actions);
            var card = new Border
            {
                Child = grid, Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(3, 2, 3, 2), Cursor = Cursors.Hand,
                ToolTip = channel.Group + " · " + channel.Name + (String.IsNullOrWhiteSpace(channel.Description) ? "" : "\n" + TextUtil.Ellipsis(channel.Description, 260))
            };
            AddHover(card, Brush(Panel), Brush(Raised), Brush(BorderColor), Brush(Accent));
            card.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
                if (channel.Kind == "Films" || channel.Kind == "Séries") ShowChannelInfo(channel);
                else Play(channel, currentView);
            };
            var menu = CreateContextMenu();
            menu.Items.Add(MenuItem(channel.IsSeries ? "Voir les épisodes" : "Lire", delegate { Play(channel, currentView); }, Glyphs.Play));
            menu.Items.Add(MenuItem(favorites.Contains(channel.Id) ? "Retirer des favoris" : "Ajouter aux favoris", delegate { ToggleFavorite(channel); }, Glyphs.Favorite));
            if (channel.Kind == "Films" || channel.Kind == "Séries")
                menu.Items.Add(MenuItem("Voir la fiche", delegate { ShowChannelInfo(channel); }, Glyphs.Info));
            menu.Items.Add(MenuItem("Masquer la chaîne", delegate { hiddenChannels.Add(channel.Id); SaveState(); RenderChannels(); RebuildSidebar(); }, Glyphs.View));
            menu.Items.Add(MenuItem("Copier l’URL du flux", delegate { CopyText(channel.Url); }, Glyphs.Copy));
            menu.Items.Add(MenuItem("Ouvrir dans le lecteur externe", delegate { OpenInExternalPlayer(channel.Url); }, Glyphs.External));
            card.ContextMenu = menu;
            return card;
        }

        Border RenderChannelCard(Channel channel)
        {
            return CreateLibraryCard(channel, false, false);
        }

        FrameworkElement BuildLogo(Channel channel, double size)
        {
            return new Border
            {
                Width = size, Height = size, CornerRadius = new CornerRadius(9), Background = Brush(Raised), ClipToBounds = true,
                Child = BuildLogoContent(channel, size)
            };
        }

        FrameworkElement BuildThumb(Channel channel, double width, double height)
        {
            return new Border
            {
                Width = width, Height = height, CornerRadius = new CornerRadius(8), Background = Brush(Raised), ClipToBounds = true,
                Child = BuildLogoContent(channel, Math.Min(width, height))
            };
        }

        FrameworkElement BuildLogoContent(Channel channel, double size)
        {
            var grid = new Grid();
            var fallback = Icon(channel.Kind == "Films" ? Glyphs.Movies : channel.Kind == "Séries" ? Glyphs.Series : Glyphs.TV, Math.Max(14, size * 0.32), Brush(Accent));
            fallback.HorizontalAlignment = HorizontalAlignment.Center; fallback.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(fallback);
            string imageUrl = !String.IsNullOrWhiteSpace(channel.PosterUrl) ? channel.PosterUrl
                : (!String.IsNullOrWhiteSpace(channel.StillUrl) ? channel.StillUrl : channel.Logo);
            if (!String.IsNullOrWhiteSpace(imageUrl))
            {
                var image = new AsyncImage { Url = imageUrl, Stretch = Stretch.UniformToFill };
                grid.Children.Add(image);
            }
            else if (channel.Kind != "Chaînes")
            {
                var host = grid;
                RequestChannelArtwork(channel, delegate(ArtworkRecord record)
                {
                    if (record == null || !record.HasPoster) return;
                    host.Children.Add(new AsyncImage { Url = record.Poster, Stretch = Stretch.UniformToFill });
                });
            }
            return grid;
        }

        static string GreetingForHour()
        {
            int hour = DateTime.Now.Hour;
            if (hour < 6) return "Bonne nuit";
            if (hour < 12) return "Bonjour";
            if (hour < 18) return "Bon après-midi";
            return "Bonsoir";
        }

        /// <summary>Étagère de cartes verticales réutilisable (Ma liste, nouveautés…).</summary>
        FrameworkElement BuildHomeCardShelf(string title, string subtitle, IEnumerable<Channel> items, string seeAllPage, bool unique = false)
        {
            if (items == null) return null;
            IEnumerable<Channel> source = items.Where(c => c != null && !hiddenChannels.Contains(c.Id) && IsChannelAllowed(c));
            // Les listes déjà dédoublonnées par le catalogue d'accueil évitent
            // un regroupement complet, coûteux sur des dizaines de milliers de titres.
            if (!unique)
                source = source.GroupBy(c => TextUtil.NormalizeKey(c.Name))
                    .Select(g => g.OrderByDescending(c => c.TmdbRating).ThenByDescending(c => (c.PosterUrl ?? "").Length).First());
            var list = source.Take(20).ToList();
            // Une seule carte n'a pas besoin d'une étagère entière ; Ma liste
            // reste utile dès le premier contenu enregistré.
            if (list.Count == 0) return null;
            var wrap = new StackPanel { Margin = new Thickness(0, 4, 0, 2) };
            var seeAll = Chip("Tout voir", false, delegate { Nav(seeAllPage); });
            seeAll.Margin = new Thickness(0);
            wrap.Children.Add(SectionHeader(title, subtitle, seeAll));
            var strip = new StackPanel { Orientation = Orientation.Horizontal };
            // Dès que les contenus possèdent leur image panoramique d'origine,
            // l'étagère devient une rangée de bannières : on voit la scène du
            // film ou de la série, pas seulement son affiche verticale.
            bool banners = ShouldUseBanners(list);
            foreach (var item in list) strip.Children.Add(CreateLibraryCard(item, banners, false));
            wrap.Children.Add(WrapLibraryScroller(strip));
            return wrap;
        }

        /// <summary>
        /// Vrai quand l'étagère peut s'afficher en bannières : il faut une vraie
        /// image panoramique pour la grande majorité des contenus, sinon la
        /// rangée mélangerait deux formats et paraîtrait bancale.
        /// </summary>
        bool ShouldUseBanners(List<Channel> items)
        {
            if (items == null || items.Count == 0) return false;
            // Il suffit d'un visuel réel : une image panoramique remplit la
            // bannière directement, une affiche verticale est posée sur sa
            // propre version floutée. Sans aucun visuel, la rangée reste en
            // affiches dessinées, plus lisibles à ce format.
            int withArt = items.Count(c => c != null && (!String.IsNullOrWhiteSpace(c.BackdropUrl)
                || !String.IsNullOrWhiteSpace(c.PosterUrl) || !String.IsNullOrWhiteSpace(c.Logo)));
            return withArt * 4 >= items.Count * 3;
        }

        /// <summary>Étagère « Top 10 » : classement des films et séries les mieux notés.</summary>
        FrameworkElement BuildTopTenShelf()
        {
            var pool = homeTopRated.Take(10).ToList();
            if (pool.Count < 4) return null;
            var wrap = new StackPanel { Margin = new Thickness(0, 4, 0, 2) };
            wrap.Children.Add(SectionHeader("Top 10 aujourd'hui", "Le classement des contenus les mieux notés de votre playlist.", null));
            var strip = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < pool.Count; i++) strip.Children.Add(TopTenCard(pool[i], i + 1));
            wrap.Children.Add(WrapLibraryScroller(strip));
            return wrap;
        }

        FrameworkElement TopTenCard(Channel channel, int rank)
        {
            var host = new Grid { Width = 266, Height = 294, Margin = new Thickness(0, 0, 10, 16), Cursor = Cursors.Hand };
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) });
            host.ToolTip = channel.Name + (channel.TmdbRating > 0 ? "  ·  ★ " + channel.TmdbRating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "");

            var number = Text(rank.ToString(), 108, Brush(WithAlpha(Accent, 132)), true);
            number.FontFamily = new FontFamily("Impact, Segoe UI Semibold");
            number.VerticalAlignment = VerticalAlignment.Bottom;
            number.HorizontalAlignment = HorizontalAlignment.Left;
            number.Margin = new Thickness(-5, 0, 0, 64);
            number.IsHitTestVisible = false;
            Grid.SetColumn(number, 0);
            host.Children.Add(number);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            var art = new Border
            {
                Width = 176, Height = 264, CornerRadius = new CornerRadius(13), ClipToBounds = true,
                Background = Brush(Raised), BorderBrush = Brush(WithAlpha(BorderColor, 220)), BorderThickness = new Thickness(1)
            };
            art.Child = PosterVisual(channel, 176, 264);
            RoundClip(art, 13);
            stack.Children.Add(art);
            var name = Text(channel.Name, 12.5, Brush(TextPrimary), true);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            name.MaxWidth = 176;
            name.Margin = new Thickness(2, 8, 0, 0);
            stack.Children.Add(name);
            Grid.SetColumn(stack, 1);
            host.Children.Add(stack);

            var lift = new TranslateTransform();
            host.RenderTransform = lift;
            host.MouseEnter += delegate
            {
                if (UiMotion.Reduced) { lift.Y = -4; return; }
                lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-4, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                art.BorderBrush = Brush(Accent);
            };
            host.MouseLeave += delegate
            {
                if (UiMotion.Reduced) { lift.Y = 0; return; }
                lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));
                art.BorderBrush = Brush(WithAlpha(BorderColor, 220));
            };
            host.MouseLeftButtonUp += delegate { ShowChannelInfo(channel); };
            return host;
        }

        /// <summary>Étagère d'affiches réutilisable, avec défilement horizontal et bords fondus.</summary>
        FrameworkElement BuildPosterShelf(string title, string subtitle, IEnumerable<Channel> items)
        {
            if (items == null) return null;
            // Les listes transmises sont déjà uniques et classées par note.
            var list = items.Take(20).ToList();
            if (list.Count < 3) return null;
            var wrap = new StackPanel { Margin = new Thickness(0, 4, 0, 2) };
            string kind = list[0].Kind;
            var seeAll = Chip("Tout voir", false, delegate
            {
                ShowLibraryCollection(CollectionTitle(kind, AllGroups), kind, AllGroups);
            });
            seeAll.Margin = new Thickness(0);
            wrap.Children.Add(SectionHeader(title, subtitle, seeAll));
            var strip = new StackPanel { Orientation = Orientation.Horizontal };
            bool banners = ShouldUseBanners(list);
            foreach (var item in list) strip.Children.Add(CreateLibraryCard(item, banners, false));
            wrap.Children.Add(WrapLibraryScroller(strip));
            return wrap;
        }

        static string KindLabel(string kind)
        {
            if (kind == "Films") return "Film";
            if (kind == "Séries") return "Série";
            if (kind == "Épisodes") return "Épisode";
            if (kind == "Local") return "Média local";
            return "Chaîne";
        }

        void ShowCategories()
        {
            page = "Catégories"; RebuildSidebar();
            var root = PageRoot("Gestion des catégories", "Masquez les groupes inutiles pour alléger votre bibliothèque.");
            if (channels.Count == 0) { root.Children.Add(EmptyState(Glyphs.Filter, "Aucune catégorie", "Chargez une playlist pour gérer ses catégories.", Button(Glyphs.Add, "Ajouter une playlist", AddPlaylist, true))); SetContent(root); return; }
            var head = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 12) };
            var search = StyledTextBox(); search.Width = 320; search.ToolTip = "Filtrer les catégories";
            var hint = Text("Filtrer les catégories…", 12, Brush(Muted), false); hint.IsHitTestVisible = false; hint.Margin = new Thickness(12, 0, 0, 0);
            var searchHost = new Grid(); searchHost.Children.Add(search); searchHost.Children.Add(hint); SetPlaceholder(search, hint);
            DockPanel.SetDock(searchHost, Dock.Left); head.Children.Add(searchHost);
            var restore = Button(Glyphs.View, "Tout afficher", delegate { hiddenGroups.Clear(); SaveState(); ShowCategories(); ShowToast("Toutes les catégories sont visibles.", false, true); }, false);
            DockPanel.SetDock(restore, Dock.Right); head.Children.Add(restore);
            root.Children.Add(head);
            var listing = new StackPanel(); root.Children.Add(listing);
            Action render = null;
            render = delegate
            {
                listing.Children.Clear();
                string query = search.Text.Trim();
                var groups = channels.GroupBy(c => String.IsNullOrWhiteSpace(c.Group) ? "Non classé" : c.Group, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new { Name = g.Key, Count = g.Count(), Hidden = hiddenGroups.Contains(g.Key) })
                    .Where(g => query.Length == 0 || TextUtil.Contains(g.Name, query))
                    .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Take(600).ToList();
                foreach (var group in groups)
                {
                    var row = new DockPanel { LastChildFill = false };
                    var labels = new StackPanel(); labels.Children.Add(Text(group.Name, 13.5, Brush(TextPrimary), true)); labels.Children.Add(Text(TextUtil.HumanCount(group.Count) + " élément(s)" + (group.Hidden ? " · masquée" : ""), 10.5, Brush(Muted), false)); DockPanel.SetDock(labels, Dock.Left); row.Children.Add(labels);
                    string captured = group.Name;
                    var toggle = Button(group.Hidden ? "Afficher" : "Masquer", delegate
                    {
                        if (hiddenGroups.Contains(captured)) hiddenGroups.Remove(captured); else hiddenGroups.Add(captured);
                        SaveState(); render();
                    }, group.Hidden);
                    toggle.Margin = new Thickness(0); DockPanel.SetDock(toggle, Dock.Right); row.Children.Add(toggle);
                    if (group.Hidden) { var badge = Badge("MASQUÉE", Brush(WithAlpha(Danger, 36)), Brush(Danger)); badge.Margin = new Thickness(0, 0, 10, 0); DockPanel.SetDock(badge, Dock.Right); row.Children.Add(badge); }
                    listing.Children.Add(Card(row, 12));
                }
                if (listing.Children.Count == 0) listing.Children.Add(Card(Text("Aucune catégorie ne correspond au filtre.", 12, Brush(Muted), false), 16));
            };
            search.TextChanged += delegate { render(); };
            render();
            SetContent(root);
        }

        void ShowHiddenChannels()
        {
            page = "Chaînes masquées"; RebuildSidebar();
            var root = PageRoot("Chaînes masquées", "Restaurez les chaînes que vous avez masquées.");
            var hidden = channels.Where(c => hiddenChannels.Contains(c.Id)).ToList();
            if (hidden.Count == 0)
            {
                root.Children.Add(EmptyState(Glyphs.View, "Aucune chaîne masquée", "Les chaînes que vous masquez depuis la bibliothèque apparaîtront ici.", null));
                SetContent(root); return;
            }
            var head = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 12) };
            var search = StyledTextBox(); search.Width = 320;
            var hint = Text("Rechercher une chaîne masquée…", 12, Brush(Muted), false); hint.IsHitTestVisible = false; hint.Margin = new Thickness(12, 0, 0, 0);
            var searchHost = new Grid(); searchHost.Children.Add(search); searchHost.Children.Add(hint); SetPlaceholder(search, hint);
            DockPanel.SetDock(searchHost, Dock.Left); head.Children.Add(searchHost);
            var restoreAll = Button(Glyphs.View, "Tout restaurer", delegate { hiddenChannels.Clear(); SaveState(); ShowHiddenChannels(); RebuildSidebar(); ShowToast("Toutes les chaînes sont visibles.", false, true); }, false);
            DockPanel.SetDock(restoreAll, Dock.Right); head.Children.Add(restoreAll);
            root.Children.Add(head);
            var listing = new StackPanel(); root.Children.Add(listing);
            Action render = delegate
            {
                listing.Children.Clear();
                string query = search.Text.Trim();
                var visible = hidden.Where(c => query.Length == 0 || TextUtil.Contains(c.Name, query) || TextUtil.Contains(c.Group, query)).Take(500).ToList();
                foreach (var channel in visible)
                {
                    var row = new DockPanel { LastChildFill = false };
                    var logo = BuildLogo(channel, 34); DockPanel.SetDock(logo, Dock.Left); row.Children.Add(logo);
                    var labels = new StackPanel { Margin = new Thickness(10, 0, 0, 0) }; labels.Children.Add(Text(channel.Name, 13, Brush(TextPrimary), true)); labels.Children.Add(Text(channel.Group, 10, Brush(Muted), false)); DockPanel.SetDock(labels, Dock.Left); row.Children.Add(labels);
                    var restore = Button("Afficher", delegate { hiddenChannels.Remove(channel.Id); SaveState(); ShowHiddenChannels(); RebuildSidebar(); }, true);
                    restore.Margin = new Thickness(0); DockPanel.SetDock(restore, Dock.Right); row.Children.Add(restore);
                    listing.Children.Add(Card(row, 11));
                }
                if (visible.Count == 0) listing.Children.Add(Card(Text("Aucune chaîne ne correspond au filtre.", 12, Brush(Muted), false), 16));
            };
            search.TextChanged += delegate { render(); };
            render();
            SetContent(root);
        }
    }
}
