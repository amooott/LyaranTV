using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LyaranTV
{
    public sealed partial class MainWindow
    {
        readonly HashSet<string> episodeArtworkRequests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Complète l'affiche d'un film ou d'une série qui n'en a pas : TMDb
        /// d'abord (déjà fait au chargement), puis TVmaze ou iTunes. Le rappel
        /// n'est appelé qu'une fois, avec null si rien n'a été trouvé.
        /// </summary>
        void RequestChannelArtwork(Channel channel, Action<ArtworkRecord> done, bool force = false)
        {
            if (channel == null) { if (done != null) done(null); return; }
            if (channel.ArtworkRequested)
            {
                if (done != null) done(null);
                return;
            }
            if (!force && !String.IsNullOrWhiteSpace(channel.PosterUrl))
            {
                if (done != null) done(null);
                return;
            }
            if (channel.Kind != "Films" && channel.Kind != "Séries") { if (done != null) done(null); return; }
            if (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode)) { if (done != null) done(null); return; }
            if (String.IsNullOrWhiteSpace(channel.Name)) { if (done != null) done(null); return; }
            channel.ArtworkRequested = true;
            bool series = channel.Kind == "Séries" || channel.IsSeries;
            ArtworkResolver.Request(channel.Name, series, channel.Year, delegate(ArtworkRecord record)
            {
                if (appClosing) return;
                if (record != null) ArtworkResolver.Apply(channel, record);
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
        /// Préchauffe les covers des premiers contenus d'une playlist : elles sont
        /// prêtes avant que l'accueil et les étagères ne les demandent.
        /// </summary>
        void PrefetchArtwork(IEnumerable<Channel> items, int maximum)
        {
            if (items == null || maximum <= 0) return;
            int count = 0;
            foreach (Channel channel in items)
            {
                if (channel == null) continue;
                if (channel.Kind != "Films" && channel.Kind != "Séries") continue;
                if (!String.IsNullOrWhiteSpace(channel.PosterUrl) || !String.IsNullOrWhiteSpace(channel.Season)) continue;
                if (String.IsNullOrWhiteSpace(channel.Name)) continue;
                var target = channel;
                ArtworkResolver.Request(channel.Name, channel.Kind == "Séries", channel.Year, delegate(ArtworkRecord record)
                {
                    if (record != null) ArtworkResolver.Apply(target, record);
                });
                count++;
                if (count >= maximum) break;
            }
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
                    // Dernier recours : demander une affiche en ligne pour un film
                    // ou une série qui n'en a pas encore.
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
