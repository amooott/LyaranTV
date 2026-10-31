using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LyaranTV
{
    public sealed partial class MainWindow
    {
        readonly HashSet<string> episodeArtworkRequests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Demandes d'affiche en cours, partagées par tous les contenus qui portent
        // le même titre : une carte construite pendant qu'une autre demande la
        // fiche reçoit le résultat au lieu de rester sur son visuel de repli.
        readonly Dictionary<string, List<Action<ArtworkRecord>>> artworkWaiters =
            new Dictionary<string, List<Action<ArtworkRecord>>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, ArtworkRecord> artworkFinished =
            new Dictionary<string, ArtworkRecord>(StringComparer.OrdinalIgnoreCase);
        DispatcherTimer artworkSaveTimer;

        // File de fond des jaquettes : elle parcourt la bibliothèque du haut de
        // l'accueil vers le reste du catalogue, quelques images à la fois. Les
        // images téléchargées restent dans le cache disque, si bien que les
        // lancements suivants ouvrent la bibliothèque déjà remplie.
        readonly Queue<Channel> coverWarmQueue = new Queue<Channel>();
        readonly HashSet<string> coverWarmSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DispatcherTimer coverWarmTimer;
        // L'ouverture d'un compte est prioritaire : la file de fond s'arrête
        // quelques secondes pour laisser tous les téléchargements disponibles
        // aux jaquettes du premier écran.
        bool coverWarmPaused;

        static bool NeedsCover(Channel channel)
        {
            return channel != null && (channel.Kind == "Films" || channel.Kind == "Séries" || channel.Kind == "Épisodes");
        }

        static string CoverWarmKey(Channel channel)
        {
            if (!String.IsNullOrWhiteSpace(channel.Id)) return channel.Id;
            return (channel.Name ?? "") + "|" + (channel.Kind ?? "") + "|" + (channel.Season ?? "") + "|" + (channel.Episode ?? "");
        }

        /// <summary>
        /// Met en file le préchargement des covers d'une liste de contenus. Le
        /// travail est découpé en petits lots minutés : l'interface reste fluide
        /// et les images des cartes visibles gardent la priorité. Une cover qui
        /// n'a pas encore d'adresse est résolue en arrière-plan (TVmaze, IMDb,
        /// iTunes) puis mise en cache, sans attendre que la carte soit affichée.
        /// </summary>
        void StartCoverPrewarm(IEnumerable<Channel> items, int maximum)
        {
            if (items == null || maximum <= 0) return;
            int added = 0;
            foreach (Channel channel in items)
            {
                if (added >= maximum) break;
                if (!NeedsCover(channel)) continue;
                if (!coverWarmSeen.Add(CoverWarmKey(channel))) continue;
                coverWarmQueue.Enqueue(channel);
                added++;
            }
            if (added == 0) return;
            if (coverWarmTimer == null)
            {
                coverWarmTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
                coverWarmTimer.Tick += delegate { CoverPrewarmTick(); };
            }
            if (!coverWarmTimer.IsEnabled) coverWarmTimer.Start();
        }

        void CoverPrewarmTick()
        {
            if (coverWarmPaused) return;
            int budget = 6;
            while (budget-- > 0 && coverWarmQueue.Count > 0)
            {
                Channel channel = coverWarmQueue.Dequeue();
                if (channel == null) continue;
                if (String.Equals(channel.Kind, "Épisodes", StringComparison.OrdinalIgnoreCase))
                {
                    string still = CoverPrefetchUrl(channel);
                    if (!String.IsNullOrWhiteSpace(still)) { ImageCache.Prefetch(still); continue; }
                    // Un épisode sans visuel reprend l'affiche de sa série ; la
                    // fiche de la série est demandée par sa propre entrée.
                    Channel series = SeriesForEpisode(channel);
                    if (series == null || String.IsNullOrWhiteSpace(series.PosterUrl)) continue;
                    ImageCache.Prefetch(series.PosterUrl);
                    continue;
                }
                string url = CoverPrefetchUrl(channel);
                if (!String.IsNullOrWhiteSpace(url)) { ImageCache.Prefetch(url); continue; }
                if (String.IsNullOrWhiteSpace(channel.Name)) continue;
                RequestChannelArtwork(channel, delegate(ArtworkRecord record)
                {
                    if (record != null && record.HasPoster) ImageCache.Prefetch(record.Poster);
                }, true);
            }
            if (coverWarmQueue.Count > 0) return;
            if (coverWarmTimer != null) coverWarmTimer.Stop();
            try { HttpUtil.AppendLog("Covers préchargées · " + coverWarmSeen.Count + " contenus demandés"); } catch { }
            if (coverWarmSeen.Count > 0) ScheduleArtworkSave();
        }

        /// <summary>
        /// Ordre de préchargement de l'accueil : reprises, défilé, nouveautés,
        /// puis les étagères de films et de séries. Ce sont les cartes que
        /// l'utilisateur voit en premier, elles doivent être prêtes avant le reste.
        /// </summary>
        List<Channel> HomeCoverOrder(int maximum)
        {
            var items = new List<Channel>();
            items.AddRange(BuildResumeItems(20));
            items.AddRange(homeTopRated.Take(40));
            items.AddRange(homeNovelties.Take(24));
            items.AddRange(homeUniqueMovies.Take(30));
            items.AddRange(homeUniqueSeries.Take(30));
            if (maximum > 0 && items.Count > maximum) items.RemoveRange(maximum, items.Count - maximum);
            return items;
        }

        /// <summary>
        /// Enregistre la bibliothèque peu après une nouvelle affiche : la cover
        /// est ainsi déjà là au prochain lancement, sans nouvelle requête.
        /// </summary>
        void ScheduleArtworkSave()
        {
            if (activeSource == null || activeSource.IsDemo || channels.Count == 0) return;
            if (artworkSaveTimer == null)
            {
                artworkSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
                artworkSaveTimer.Tick += delegate
                {
                    artworkSaveTimer.Stop();
                    SaveLibraryCache(activeSource);
                };
            }
            artworkSaveTimer.Stop();
            artworkSaveTimer.Start();
        }

        /// <summary>
        /// Complète l'affiche d'un film ou d'une série qui n'en a pas : TMDb
        /// d'abord (déjà fait au chargement), puis TVmaze ou iTunes. Le rappel
        /// n'est appelé qu'une fois, avec null si rien n'a été trouvé.
        /// </summary>
        void RequestChannelArtwork(Channel channel, Action<ArtworkRecord> done, bool force = false)
        {
            if (channel == null) { if (done != null) done(null); return; }
            if (!force && !String.IsNullOrWhiteSpace(channel.PosterUrl))
            {
                if (done != null) done(null);
                return;
            }
            if (channel.Kind != "Films" && channel.Kind != "Séries") { if (done != null) done(null); return; }
            if (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode)) { if (done != null) done(null); return; }
            if (String.IsNullOrWhiteSpace(channel.Name)) { if (done != null) done(null); return; }

            bool series = channel.Kind == "Séries" || channel.IsSeries;
            string clean = TmdbTitleCleaner.CleanTitle(channel.Name);
            string key = ArtworkStore.Key(clean, series);
            ArtworkRecord finished;
            if (artworkFinished.TryGetValue(key, out finished))
            {
                if (finished != null) ArtworkResolver.Apply(channel, finished);
                if (done != null) done(finished);
                return;
            }
            List<Action<ArtworkRecord>> waiters;
            if (artworkWaiters.TryGetValue(key, out waiters))
            {
                // Une demande est déjà en cours : on s'y rattache pour recevoir
                // l'affiche dès qu'elle arrive.
                if (done != null) waiters.Add(done);
                channel.ArtworkRequested = true;
                return;
            }
            waiters = new List<Action<ArtworkRecord>>();
            if (done != null) waiters.Add(done);
            artworkWaiters[key] = waiters;
            channel.ArtworkRequested = true;
            ArtworkResolver.Request(channel.Name, series, channel.Year, delegate(ArtworkRecord record)
            {
                List<Action<ArtworkRecord>> callbacks;
                artworkWaiters.TryGetValue(key, out callbacks);
                artworkWaiters.Remove(key);
                if (artworkFinished.Count > 4000) artworkFinished.Clear();
                artworkFinished[key] = record;
                if (record != null) ArtworkResolver.Apply(channel, record);
                if (record != null) ScheduleArtworkSave();
                if (appClosing) return;
                if (callbacks != null)
                {
                    foreach (var callback in callbacks)
                    {
                        try { callback(record); } catch { }
                    }
                }
            });
        }

        /// <summary>
        /// Résout l'affiche d'un épisode à partir de sa série : les cartes de
        /// reprise et les rangées d'épisodes n'ont souvent que l'identifiant de
        /// la série, pas encore sa cover.
        /// </summary>
        void RequestEpisodeArtwork(Channel episode, Action<ArtworkRecord> done)
        {
            if (episode == null) { if (done != null) done(null); return; }
            Channel series = SeriesForEpisode(episode);
            if (series == null) { if (done != null) done(null); return; }
            RequestChannelArtwork(series, delegate(ArtworkRecord record)
            {
                if (String.IsNullOrWhiteSpace(episode.PosterUrl) && !String.IsNullOrWhiteSpace(series.PosterUrl)) episode.PosterUrl = series.PosterUrl;
                if (String.IsNullOrWhiteSpace(episode.BackdropUrl) && !String.IsNullOrWhiteSpace(series.BackdropUrl)) episode.BackdropUrl = series.BackdropUrl;
                if (String.IsNullOrWhiteSpace(episode.Logo) && !String.IsNullOrWhiteSpace(series.Logo)) episode.Logo = series.Logo;
                if (done != null) done(record);
            });
        }

        /// <summary>
        /// Charge les aperçus d'épisodes d'une série : ceux du fournisseur Xtream
        /// sont déjà là, les autres viennent de TVmaze (image, synopsis, durée).
        /// </summary>
        void EnsureSeriesEpisodeArtwork(Channel series, List<Channel> episodes)
        {
            if (series == null || episodes == null || episodes.Count == 0) return;
            bool needsStill = episodes.Any(delegate(Channel ep)
            {
                return ep != null && String.IsNullOrWhiteSpace(ep.StillUrl) && String.IsNullOrWhiteSpace(ep.Logo)
                    && String.IsNullOrWhiteSpace(ep.PosterUrl);
            });
            bool needsInfo = episodes.Any(delegate(Channel ep) { return ep != null && String.IsNullOrWhiteSpace(ep.Description); });
            if (!needsStill && !needsInfo) return;
            string key = SeriesKey(series);
            if (String.IsNullOrWhiteSpace(key) || !episodeArtworkRequests.Add(key)) return;
            string title = series.Name;
            ArtworkResolver.Request(title, true, series.Year, delegate(ArtworkRecord record)
            {
                if (appClosing || record == null) return;
                ArtworkResolver.Apply(series, record);
                foreach (var episode in episodes)
                {
                    if (episode == null) continue;
                    ArtworkResolver.ApplyEpisode(episode, record);
                    if (String.IsNullOrWhiteSpace(episode.StillUrl) && String.IsNullOrWhiteSpace(episode.Logo)
                        && String.IsNullOrWhiteSpace(episode.PosterUrl) && !String.IsNullOrWhiteSpace(series.PosterUrl))
                        episode.PosterUrl = series.PosterUrl;
                }
                ScheduleArtworkSave();
                if (page == "Épisodes" && episodesSeries != null
                    && String.Equals(SeriesKey(episodesSeries), key, StringComparison.OrdinalIgnoreCase))
                    ShowSeriesEpisodes(episodesSeries, episodes);
                else if (page == "Lecture") RefreshPlayerEpisodesRow();
            });
        }

        /// <summary>
        /// Palette déterministe des vignettes de repli : deux contenus voisins
        /// n'ont pas la même teinte, comme des pochettes de DVD différentes.
        /// </summary>
        Color CoverTint(Channel channel)
        {
            if (channel == null || String.IsNullOrWhiteSpace(channel.Name)) return Accent;
            int hash = 0;
            string key = channel.Name.ToLowerInvariant();
            for (int i = 0; i < key.Length; i++) hash = (hash * 31 + key[i]) & 0x7fffffff;
            Color[] palette =
            {
                Accent,
                Secondary,
                Color.FromRgb(244, 114, 182),
                Color.FromRgb(251, 191, 36),
                Color.FromRgb(52, 211, 153),
                Color.FromRgb(129, 140, 248)
            };
            return palette[hash % palette.Length];
        }

        /// <summary>
        /// Préchauffe les covers des premiers contenus d'une playlist : les
        /// images existantes sont téléchargées tout de suite, et les fiches
        /// manquantes sont demandées en arrière-plan.
        /// </summary>
        void PrefetchArtwork(IEnumerable<Channel> items, int maximum)
        {
            if (items == null || maximum <= 0) return;
            int count = 0;
            foreach (Channel channel in items)
            {
                if (channel == null) continue;
                if (channel.Kind != "Films" && channel.Kind != "Séries") continue;
                if (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode)) continue;
                string known = CoverPrefetchUrl(channel);
                if (!String.IsNullOrWhiteSpace(known))
                {
                    ImageCache.Prefetch(known);
                    count++;
                    if (count >= maximum) break;
                    continue;
                }
                if (String.IsNullOrWhiteSpace(channel.Name)) continue;
                RequestChannelArtwork(channel, delegate(ArtworkRecord record)
                {
                    if (record != null && record.HasPoster) ImageCache.Prefetch(record.Poster);
                });
                count++;
                if (count >= maximum) break;
            }
        }

        /// <summary>
        /// Attend qu'un petit lot de jaquettes soit décodé, avec une limite de
        /// temps : l'écran de restauration reste affiché jusqu'à ce que les
        /// premières cartes aient leur cover, sans bloquer plus longtemps.
        /// </summary>
        void WaitForCoverImages(IEnumerable<Channel> items, int maximum, int timeoutMs, Action done, Action<int, int> progress = null)
        {
            if (done == null) return;
            var urls = new List<string>();
            if (items != null && maximum > 0)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Channel channel in items)
                {
                    string url = CoverPrefetchUrl(channel);
                    if (String.IsNullOrWhiteSpace(url) || !seen.Add(url)) continue;
                    urls.Add(url);
                    if (urls.Count >= maximum) break;
                }
            }
            if (urls.Count == 0) { done(); return; }
            try { HttpUtil.AppendLog("Covers test · " + String.Join(" ; ", urls.Take(4).ToArray())); } catch { }
            if (progress != null) { try { progress(0, urls.Count); } catch { } }
            coverWarmPaused = true;

            int remaining = urls.Count;
            int loaded = 0;
            bool finishing = false;
            // Une couverture morte (ancienne URL TMDb, hôte injoignable) ne doit
            // pas retenir tout le premier écran : dès que l'essentiel est prêt,
            // on ouvre l'interface et les dernières images arrivent après.
            int needed = Math.Max(1, (int)Math.Ceiling(urls.Count * 0.88));
            Action report = delegate
            {
                if (progress == null) return;
                try { progress(loaded, urls.Count); } catch { }
            };
            bool finished = false;
            DispatcherTimer timeout = null;
            Action complete = delegate
            {
                if (finished) return;
                finished = true;
                coverWarmPaused = false;
                if (timeout != null) timeout.Stop();
                done();
            };
            timeout = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(300, timeoutMs)) };
            timeout.Tick += delegate
            {
                timeout.Stop();
                try { HttpUtil.AppendLog("Covers d'ouverture · " + loaded + "/" + urls.Count + " prêtes (délai atteint)"); } catch { }
                complete();
            };
            timeout.Start();
            foreach (string url in urls)
            {
                ImageCache.GetAsync(url, delegate(ImageSource source)
                {
                    if (source != null) loaded++;
                    remaining--;
                    report();
                    if (!finishing && (loaded >= needed || remaining <= 0))
                    {
                        finishing = true;
                        try { HttpUtil.AppendLog("Covers d'ouverture · " + loaded + "/" + urls.Count + " prêtes"); } catch { }
                        // Terminer sur le tour suivant du fil d'interface : la
                        // boucle de demandes se finit avant l'ouverture de la page.
                        Dispatcher.BeginInvoke(new Action(complete));
                    }
                });
            }
            // Toutes les adresses étaient déjà en cache : aucune attente.
            if (!finishing && loaded >= needed)
            {
                finishing = true;
                Dispatcher.BeginInvoke(new Action(complete));
            }
        }

        /// <summary>
        /// Demande les affiches de secours des premières cartes sans attendre
        /// qu'une adresse du fournisseur échoue : la cover est déjà en cache
        /// quand la carte en a besoin.
        /// </summary>
        void PrefetchFallbackArtwork(IEnumerable<Channel> items, int maximum)
        {
            if (items == null || maximum <= 0) return;
            int count = 0;
            foreach (Channel channel in items)
            {
                if (channel == null) continue;
                if (channel.Kind != "Films" && channel.Kind != "Séries") continue;
                if (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode)) continue;
                if (String.IsNullOrWhiteSpace(channel.Name)) continue;
                bool series = channel.Kind == "Séries" || channel.IsSeries;
                ArtworkRecord known = ArtworkStore.Get(ArtworkStore.Key(TmdbTitleCleaner.CleanTitle(channel.Name), series));
                if (known != null)
                {
                    if (known.HasPoster) ImageCache.Prefetch(known.Poster);
                    continue;
                }
                RequestChannelArtwork(channel, delegate(ArtworkRecord record)
                {
                    if (record != null && record.HasPoster) ImageCache.Prefetch(record.Poster);
                }, true);
                count++;
                if (count >= maximum) break;
            }
        }

        /// <summary>
        /// Adresse la plus utile pour précharger la jaquette d'un contenu.
        /// </summary>
        static string CoverPrefetchUrl(Channel channel)
        {
            if (channel == null) return "";
            if (channel.Kind == "Chaînes") return channel.Logo;
            if (!String.IsNullOrWhiteSpace(channel.PosterUrl)) return channel.PosterUrl;
            if (!String.IsNullOrWhiteSpace(channel.StillUrl)) return channel.StillUrl;
            if (!String.IsNullOrWhiteSpace(channel.BackdropUrl)) return channel.BackdropUrl;
            return channel.Logo;
        }

        /// <summary>
        /// Précharge les jaquettes déjà connues d'une liste de cartes. Aucun
        /// appel réseau de métadonnées n'est déclenché ici : uniquement les
        /// images des contenus qui ont déjà une adresse.
        /// </summary>
        void PrefetchCovers(IEnumerable<Channel> items, int maximum)
        {
            if (items == null || maximum <= 0) return;
            int count = 0;
            foreach (Channel channel in items)
            {
                string url = CoverPrefetchUrl(channel);
                if (String.IsNullOrWhiteSpace(url)) continue;
                ImageCache.Prefetch(url);
                count++;
                if (count >= maximum) break;
            }
        }

        /// <summary>
        /// Prépare les jaquettes des étagères de la bibliothèque avant leur
        /// construction : la première image s'affiche depuis le cache au lieu
        /// de laisser un cadre vide le temps du téléchargement.
        /// </summary>
        void WarmLibraryCovers(List<Channel> view)
        {
            if (view == null || view.Count == 0) return;
            // Les adresses déjà connues partent tout de suite : les cartes de la
            // première rangée trouvent leur image en mémoire ou sur le disque.
            PrefetchCovers(view.Take(96), 96);
            // Puis la file de fond prend le relais pour tout ce qui se dévoile en
            // défilant, y compris les covers encore inconnues à résoudre.
            var warm = new List<Channel>();
            warm.AddRange(BuildResumeItems(18));
            warm.AddRange(view.Take(240));
            StartCoverPrewarm(warm, 260);
        }

        /// <summary>Aperçu de repli dessiné : dégradé, initiales et libellé, jamais un cadre vide.</summary>
        FrameworkElement DrawnArtwork(Channel channel, double width, double height, bool wide)
        {
            string name = channel == null ? "" : channel.Name;
            Color tint = CoverTint(channel);
            var host = new Grid { Width = width, Height = height, ClipToBounds = true };
            host.Children.Add(new Border
            {
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(WithAlpha(tint, 82), 0),
                        new GradientStop(Color.FromRgb(24, 25, 36), 0.58),
                        new GradientStop(Color.FromRgb(12, 13, 19), 1)
                    }, new Point(0, 0), new Point(1, 1))
            });
            var halo = new System.Windows.Shapes.Ellipse
            {
                Width = height * 0.86, Height = height * 0.86, Stroke = Brush(WithAlpha(tint, 74)), StrokeThickness = 1,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -height * 0.28, -width * 0.16, 0), IsHitTestVisible = false
            };
            host.Children.Add(halo);
            host.Children.Add(new Border
            {
                Background = new LinearGradientBrush(Color.FromArgb(0, 255, 255, 255), Color.FromArgb(14, 255, 255, 255), 90),
                IsHitTestVisible = false
            });
            // Deux fines bandes obliques rappellent une jaquette, sans surcharger.
            host.Children.Add(new Border
            {
                Width = width * 1.5, Height = 1, Background = Brush(WithAlpha(tint, 42)),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-26), IsHitTestVisible = false
            });
            host.Children.Add(new Border
            {
                Width = width * 1.2, Height = 1, Background = Brush(WithAlpha(tint, 26)),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 26, 0, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-26), IsHitTestVisible = false
            });
            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(wide ? 14 : 12, 0, wide ? 14 : 12, wide ? 10 : 12)
            };
            string glyph = channel == null ? Glyphs.Play
                : channel.Kind == "Films" ? Glyphs.Movies
                : channel.Kind == "Séries" || channel.Kind == "Épisodes" ? Glyphs.Series
                : channel.Kind == "Chaînes" ? Glyphs.TV : Glyphs.Play;
            var icon = Icon(glyph, wide ? 17 : 21, Brush(WithAlpha(tint, 235)));
            stack.Children.Add(icon);
            if (!String.IsNullOrWhiteSpace(name))
            {
                var mark = Text(LibraryInitials(name), wide ? 24 : 30, Brush(WithAlpha(TextPrimary, 108)), true);
                mark.HorizontalAlignment = HorizontalAlignment.Right;
                mark.VerticalAlignment = VerticalAlignment.Top;
                mark.Margin = new Thickness(0, wide ? 6 : 8, wide ? 12 : 10, 0);
                mark.IsHitTestVisible = false;
                host.Children.Add(mark);
            }
            host.Children.Add(stack);
            return host;
        }

        /// <summary>
        /// Affiche prête à l'emploi : un visuel de repli est toujours dessiné,
        /// puis la cover distante se pose par-dessus dès qu'elle est décodée. Si
        /// une URL échoue, on essaie les autres visuels du contenu avant de
        /// demander une affiche de secours (TVmaze, IMDb, iTunes…).
        /// </summary>
        FrameworkElement CoverArt(Channel channel, double width, double height, bool wide)
        {
            var host = new Grid { Width = width, Height = height, ClipToBounds = true, Background = Brush(Raised) };
            host.Children.Add(DrawnArtwork(channel, width, height, wide));
            var artHost = new Grid();
            host.Children.Add(artHost);

            // Une affiche déjà résolue lors d'une session précédente est
            // réappliquée ici, uniquement pour les cartes réellement affichées.
            // Une passe sur tout le catalogue coûterait plusieurs secondes.
            if (channel != null && String.IsNullOrWhiteSpace(channel.PosterUrl))
            {
                if (channel.Kind == "Épisodes" && !String.IsNullOrWhiteSpace(channel.SeriesId))
                {
                    Channel parent = SeriesForEpisode(channel);
                    if (parent != null)
                    {
                        if (String.IsNullOrWhiteSpace(parent.PosterUrl) && !String.IsNullOrWhiteSpace(parent.Name))
                        {
                            ArtworkRecord parentRecord = ArtworkResolver.Peek(parent.Name, true);
                            if (parentRecord != null && parentRecord.HasPoster) ArtworkResolver.Apply(parent, parentRecord);
                        }
                        if (!String.IsNullOrWhiteSpace(parent.PosterUrl)) channel.PosterUrl = parent.PosterUrl;
                    }
                }
                else if ((channel.Kind == "Films" || channel.Kind == "Séries")
                    && String.IsNullOrWhiteSpace(channel.Season) && String.IsNullOrWhiteSpace(channel.Episode)
                    && !String.IsNullOrWhiteSpace(channel.Name))
                {
                    bool serial = channel.Kind == "Séries" || channel.IsSeries;
                    ArtworkRecord known = ArtworkResolver.Peek(channel.Name, serial);
                    if (known != null && known.HasPoster) ArtworkResolver.Apply(channel, known);
                }
            }

            var candidates = new List<string>();
            if (channel != null)
            {
                bool live = channel.Kind == "Chaînes";
                // Une vignette large est une bannière : l'image panoramique du
                // contenu passe avant l'affiche verticale, sinon la bannière
                // serait un recadrage serré de l'affiche.
                if (wide && !String.IsNullOrWhiteSpace(channel.BackdropUrl)) candidates.Add(channel.BackdropUrl);
                if (live && !String.IsNullOrWhiteSpace(channel.Logo)) candidates.Add(channel.Logo);
                if (!live && !String.IsNullOrWhiteSpace(channel.StillUrl)) candidates.Add(channel.StillUrl);
                if (!String.IsNullOrWhiteSpace(channel.PosterUrl)) candidates.Add(channel.PosterUrl);
                if (!String.IsNullOrWhiteSpace(channel.BackdropUrl)) candidates.Add(channel.BackdropUrl);
                if (!String.IsNullOrWhiteSpace(channel.Logo)) candidates.Add(channel.Logo);
            }

            // Évite de charger deux fois la même adresse.
            var unique = new List<string>();
            foreach (string url in candidates)
            {
                if (String.IsNullOrWhiteSpace(url)) continue;
                bool seen = false;
                foreach (string known in unique) if (String.Equals(known, url, StringComparison.OrdinalIgnoreCase)) { seen = true; break; }
                if (!seen) unique.Add(url);
            }

            int index = 0;
            Action loadNext = null;
            loadNext = delegate
            {
                if (index >= unique.Count)
                {
                    try { HttpUtil.AppendLog("CoverArt demande · " + (channel == null ? "?" : channel.Name)); } catch { }
                    // Dernier recours : demander l'affiche de la série pour un
                    // épisode, ou une affiche en ligne pour un film / une série.
                    if (channel != null && String.Equals(channel.Kind, "Épisodes", StringComparison.OrdinalIgnoreCase)
                        && !String.IsNullOrWhiteSpace(channel.SeriesId))
                    {
                        var episode = channel;
                        RequestEpisodeArtwork(channel, delegate(ArtworkRecord record)
                        {
                            if (episode == null) return;
                            string poster = !String.IsNullOrWhiteSpace(episode.PosterUrl) ? episode.PosterUrl
                                : (record != null && record.HasPoster ? record.Poster : "");
                            if (String.IsNullOrWhiteSpace(poster)) return;
                            var found = CreateCoverImage(poster, false, episode, width, height);
                            if (found != null) artHost.Children.Add(found);
                        });
                        return;
                    }
                    if (channel != null && (channel.Kind == "Films" || channel.Kind == "Séries")
                        && String.IsNullOrWhiteSpace(channel.Season) && String.IsNullOrWhiteSpace(channel.Episode))
                    {
                        var target = channel;
                        RequestChannelArtwork(channel, delegate(ArtworkRecord record)
                        {
                            if (record == null || !record.HasPoster || target == null)
                            {
                                try { HttpUtil.AppendLog("CoverArt sans résultat · " + (target == null ? "?" : target.Name)); } catch { }
                                return;
                            }
                            if (String.IsNullOrWhiteSpace(target.PosterUrl)) target.PosterUrl = record.Poster;
                            try { HttpUtil.AppendLog("CoverArt reçue · " + target.Name + " · " + record.Poster); } catch { }
                            var found = CreateCoverImage(record.Poster, false, target, width, height);
                            if (found != null) artHost.Children.Add(found);
                        }, true);
                    }
                    return;
                }
                string url = unique[index++];
                // Une affiche de film ou de série peut arriver par le champ `logo`
                // du fournisseur : elle doit remplir le cadre comme une affiche,
                // pas être centrée avec des marges comme un logo de chaîne.
                bool logo = channel != null && channel.Kind == "Chaînes"
                    && String.Equals(url, channel.Logo, StringComparison.OrdinalIgnoreCase);
                var coverImage = CreateCoverImage(url, logo, channel, width, height);
                if (coverImage == null) { loadNext(); return; }
                // Bannière sans image panoramique : seul un visuel vertical est
                // disponible. On le présente au format du défilé en le posant
                // sur une version floutée de lui-même — la bannière reste donc
                // authentique (même œuvre) et le cadre est toujours rempli.
                bool posterBanner = wide && !logo && !IsWideArtwork(channel, url)
                    && !String.IsNullOrWhiteSpace(channel == null ? null : channel.PosterUrl);
                FrameworkElement presented = coverImage;
                if (posterBanner)
                {
                    var panel = new Grid { ClipToBounds = true };
                    var backdropFill = new AsyncImage
                    {
                        Url = url, Stretch = Stretch.UniformToFill, Opacity = 0.55, IsHitTestVisible = false,
                        Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 26 }
                    };
                    panel.Children.Add(backdropFill);
                    panel.Children.Add(new Border
                    {
                        Background = new LinearGradientBrush(new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(96, 8, 8, 14), 0),
                            new GradientStop(Color.FromArgb(196, 8, 8, 14), 1)
                        }, new Point(0, 0), new Point(0, 1)),
                        IsHitTestVisible = false
                    });
                    // L'affiche occupe le côté droit comme un panneau : le titre
                    // et les informations restent lisibles à gauche, sans se
                    // superposer au visuel.
                    coverImage.Stretch = Stretch.UniformToFill;
                    var posterHost = new Border
                    {
                        Width = Math.Round(Math.Max(96, width * 0.54)),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Background = Brush(Color.FromArgb(255, 8, 8, 14)),
                        ClipToBounds = true, Child = coverImage
                    };
                    panel.Children.Add(posterHost);
                    panel.Children.Add(new Border
                    {
                        Background = new LinearGradientBrush(new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(214, 8, 8, 14), 0),
                            new GradientStop(Color.FromArgb(96, 8, 8, 14), 0.62),
                            new GradientStop(Color.FromArgb(0, 8, 8, 14), 0.92)
                        }, new Point(0, 0), new Point(1, 0)),
                        IsHitTestVisible = false
                    });
                    presented = panel;
                }
                coverImage.Failed += delegate(string failedUrl)
                {
                    artHost.Children.Remove(presented);
                    // Une adresse morte (fournisseur hors ligne, ancienne
                    // affiche TMDb) ne doit pas être réessayée sur chaque carte.
                    if (channel != null)
                    {
                        if (String.Equals(channel.PosterUrl, failedUrl, StringComparison.OrdinalIgnoreCase)) channel.PosterUrl = "";
                        if (String.Equals(channel.BackdropUrl, failedUrl, StringComparison.OrdinalIgnoreCase)) channel.BackdropUrl = "";
                        if (String.Equals(channel.StillUrl, failedUrl, StringComparison.OrdinalIgnoreCase)) channel.StillUrl = "";
                        if (String.Equals(channel.Logo, failedUrl, StringComparison.OrdinalIgnoreCase)) channel.Logo = "";
                    }
                    try { HttpUtil.AppendLog("Visuel introuvable : " + failedUrl); } catch { }
                    loadNext();
                };
                artHost.Children.Add(presented);
            };
            loadNext();
            return host;
        }

        /// <summary>
        /// Vrai si l'adresse correspond à une image réellement panoramique : elle
        /// peut alors remplir une bannière telle quelle, sans composition.
        /// </summary>
        static bool IsWideArtwork(Channel channel, string url)
        {
            if (channel == null || String.IsNullOrWhiteSpace(url)) return false;
            if (!String.IsNullOrWhiteSpace(channel.BackdropUrl) && String.Equals(channel.BackdropUrl, url, StringComparison.OrdinalIgnoreCase)) return true;
            if (!String.IsNullOrWhiteSpace(channel.StillUrl) && String.Equals(channel.StillUrl, url, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        AsyncImage CreateCoverImage(string url, bool logo, Channel channel, double width, double height)
        {
            if (String.IsNullOrWhiteSpace(url)) return null;
            var image = new AsyncImage
            {
                Url = url,
                Stretch = logo ? Stretch.Uniform : Stretch.UniformToFill,
                FadeIn = true,
                IsHitTestVisible = false
            };
            if (logo)
            {
                image.Margin = new Thickness(Math.Max(10, width * 0.16), Math.Max(8, height * 0.14),
                    Math.Max(10, width * 0.16), Math.Max(8, height * 0.14));
            }
            return image;
        }
    }
}
