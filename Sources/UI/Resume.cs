using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LyaranTV
{
    /// <summary>
    /// « Reprendre la lecture » : séries, films, épisodes et fichiers locaux en
    /// cours, avec progression et temps restant, présentés comme une rangée Netflix.
    /// </summary>
    public sealed partial class MainWindow
    {
        const double ResumeCardWidth = 280;
        const double ResumeArtHeight = 158;

        // ------------------------------------------------------- résolution

        /// <summary>Retrouve un contenu par identifiant, y compris un épisode non présent dans la bibliothèque.</summary>
        Channel ResolveContent(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            Channel channel = FindChannel(id);
            if (channel != null) return channel;
            foreach (var list in seriesEpisodes.Values)
            {
                if (list == null) continue;
                var episode = list.FirstOrDefault(e => e != null && String.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
                if (episode != null) return episode;
            }
            // Épisode connu d'une session précédente : la fiche de la série n'est pas
            // forcément chargée, mais la trace suffit pour reprendre la lecture.
            EpisodeRecord record = episodeHistory.FirstOrDefault(r => r != null && String.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
            if (record != null && !String.IsNullOrWhiteSpace(record.Url))
            {
                return new Channel
                {
                    Id = record.Id, Name = record.Name, Url = record.Url, Group = record.Group,
                    Kind = "Épisodes", Season = record.Season, Episode = record.Episode,
                    SeriesId = record.SeriesId, StreamId = record.SeriesId,
                    Logo = record.PosterUrl, PosterUrl = record.PosterUrl, Order = 0
                };
            }
            if (id.StartsWith("cli:", StringComparison.OrdinalIgnoreCase))
            {
                string path = id.Substring(4);
                if (!String.IsNullOrWhiteSpace(path))
                {
                    return new Channel
                    {
                        Name = System.IO.Path.GetFileName(path), Group = "Médias locaux", Kind = "Local",
                        Url = path, Id = id, Description = path, Order = 0
                    };
                }
            }
            return null;
        }

        Channel SeriesForEpisode(Channel episode)
        {
            if (episode == null) return null;
            if (String.IsNullOrWhiteSpace(episode.SeriesId) && !String.Equals(episode.Kind, "Épisodes", StringComparison.OrdinalIgnoreCase)) return null;
            foreach (Channel candidate in channels)
            {
                if (candidate == null) continue;
                if (!String.IsNullOrWhiteSpace(episode.SeriesId)
                    && String.Equals(candidate.StreamId, episode.SeriesId, StringComparison.OrdinalIgnoreCase)) return candidate;
            }
            return null;
        }

        /// <summary>Un épisode est présenté avec le nom et l'affiche de sa série, comme sur Netflix.</summary>
        Channel ResumeDisplay(Channel content)
        {
            if (content == null) return null;
            Channel series = SeriesForEpisode(content);
            if (series == null)
            {
                EpisodeRecord trace = episodeHistory.FirstOrDefault(r => r != null && String.Equals(r.Id, content.Id, StringComparison.OrdinalIgnoreCase));
                if (trace == null || String.IsNullOrWhiteSpace(trace.SeriesName)) return content;
                var traceClone = new Channel
                {
                    Id = content.Id, Name = trace.SeriesName, Url = content.Url, Group = content.Group,
                    Kind = content.Kind, Season = content.Season, Episode = content.Episode,
                    SeriesId = content.SeriesId, StreamId = content.SeriesId,
                    PosterUrl = String.IsNullOrWhiteSpace(content.PosterUrl) ? trace.PosterUrl : content.PosterUrl,
                    Logo = content.Logo, Description = content.Description, Order = content.Order,
                    HttpUserAgent = content.HttpUserAgent, HttpReferer = content.HttpReferer,
                    HttpOrigin = content.HttpOrigin, HttpCookie = content.HttpCookie
                };
                return traceClone;
            }
            var clone = new Channel
            {
                Name = series.Name, Group = content.Group, Url = content.Url, AlternateUrl = content.AlternateUrl,
                Logo = !String.IsNullOrWhiteSpace(series.Logo) ? series.Logo : content.Logo,
                Id = content.Id, TvgId = content.TvgId, Kind = content.Kind, StreamId = content.StreamId,
                Number = content.Number, Description = !String.IsNullOrWhiteSpace(series.Description) ? series.Description : content.Description,
                Rating = series.Rating, Year = series.Year, SeriesId = content.SeriesId,
                Season = content.Season, Episode = content.Episode,
                TmdbId = series.TmdbId,
                PosterUrl = !String.IsNullOrWhiteSpace(series.PosterUrl) ? series.PosterUrl : content.PosterUrl,
                BackdropUrl = !String.IsNullOrWhiteSpace(series.BackdropUrl) ? series.BackdropUrl : content.BackdropUrl,
                Overview = series.Overview, Genres = series.Genres, Runtime = content.Runtime,
                Certification = series.Certification, Cast = series.Cast,
                TmdbRating = series.TmdbRating, VoteCount = series.VoteCount,
                IsSeries = false, Order = content.Order,
                HttpUserAgent = content.HttpUserAgent, HttpReferer = content.HttpReferer,
                HttpOrigin = content.HttpOrigin, HttpCookie = content.HttpCookie
            };
            return clone;
        }

        Channel NextEpisodeFor(Channel series)
        {
            if (series == null) return null;
            List<Channel> episodes;
            if (!seriesEpisodes.TryGetValue(SeriesKey(series), out episodes) || episodes == null || episodes.Count == 0) return null;
            foreach (Channel episode in episodes)
            {
                if (GetSavedPosition(episode) > 1) return episode;
            }
            return episodes[0];
        }

        /// <summary>Contenus commencés, du plus récent au plus ancien, séries comprises.</summary>
        internal List<Channel> BuildResumeItems(int max)
        {
            var items = new List<Channel>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<Channel> add = delegate(Channel content)
            {
                if (content == null || items.Count >= max) return;
                if (hiddenChannels.Contains(content.Id)) return;
                if (!IsChannelAllowed(content)) return;
                if (!seen.Add(content.Id)) return;
                items.Add(ResumeDisplay(content));
            };

            // 1. Historique récent, dans l'ordre : c'est l'ordre « continuer à regarder ».
            foreach (string id in recentIds)
            {
                if (!positions.ContainsKey(id)) continue;
                add(ResolveContent(id));
            }
            // 2. Positions enregistrées hors historique (séries lues depuis une fiche).
            foreach (var pair in positions.OrderByDescending(p => p.Value))
                add(ResolveContent(pair.Key));
            // 3. Séries ouvertes récemment sans progression : on propose le prochain épisode.
            foreach (string id in recentIds)
            {
                Channel series = FindChannel(id);
                if (series == null || !(series.IsSeries || String.Equals(series.Kind, "Séries", StringComparison.OrdinalIgnoreCase))) continue;
                if (seen.Contains(series.Id)) continue;
                Channel next = NextEpisodeFor(series);
                if (next != null) add(next);
                else add(series);
            }
            return items;
        }

        double ResumeProgress(Channel channel)
        {
            if (channel == null) return 0;
            double saved = GetSavedPosition(channel);
            if (saved <= 0) return 0;
            double total;
            if (!durations.TryGetValue(channel.Id, out total) || total <= 1) return 0;
            double fraction = saved / total;
            if (fraction < 0.01) fraction = 0.01;
            if (fraction > 0.995) fraction = 0.995;
            return fraction;
        }

        string ResumeStatusLine(Channel channel)
        {
            if (channel == null) return "";
            var parts = new List<string>();
            if (!String.IsNullOrWhiteSpace(channel.Season)) parts.Add("Saison " + channel.Season);
            if (!String.IsNullOrWhiteSpace(channel.Episode)) parts.Add("Épisode " + channel.Episode);
            double saved = GetSavedPosition(channel);
            if (parts.Count == 0 && !String.IsNullOrWhiteSpace(channel.Group)) parts.Add(channel.Group);
            if (parts.Count == 0) parts.Add(KindLabel(channel.Kind));
            string line = String.Join("  ·  ", parts.ToArray());
            if (saved <= 0) return line;
            double total;
            if (durations.TryGetValue(channel.Id, out total) && total > saved + 30)
            {
                int remaining = (int)Math.Round((total - saved) / 60.0);
                if (remaining < 1) remaining = 1;
                line += "  ·  " + remaining.ToString() + " min restantes";
            }
            else line += "  ·  à " + TextUtil.FormatPosition(TimeSpan.FromSeconds(saved));
            return line;
        }

        // ------------------------------------------------------------ cartes

        /// <summary>Léger soulèvement d'une carte au survol, comme les vignettes Netflix.</summary>
        void LiftCard(TranslateTransform transform, double offset)
        {
            if (transform == null) return;
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            if (UiMotion.Reduced) { transform.Y = offset; return; }
            transform.BeginAnimation(TranslateTransform.YProperty,
                new System.Windows.Media.Animation.DoubleAnimation(offset, TimeSpan.FromMilliseconds(130))
                {
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                });
        }

        Border ResumeCard(Channel item)
        {
            if (item == null) return null;
            double fraction = ResumeProgress(item);
            var stack = new StackPanel();
            var art = new Grid { Height = ResumeArtHeight, ClipToBounds = true };
            var artBorder = new Border { CornerRadius = new CornerRadius(12), Background = Brush(Raised), ClipToBounds = true };
            RoundClip(artBorder, 12);
            art.Children.Add(artBorder);
            artBorder.Child = PosterVisual(item, ResumeCardWidth - 14, ResumeArtHeight);
            art.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.35),
                    new GradientStop(Color.FromArgb(150, 0, 0, 0), 1)
                }, new Point(0.5, 0), new Point(0.5, 1))
            });

            // Un épisode est présenté comme sa série, avec saison et épisode dans la ligne d'état.
            string kind = item.Kind == "Films" ? "FILM" : item.Kind == "Séries" ? "SÉRIE"
                : item.Kind == "Épisodes" ? (String.IsNullOrWhiteSpace(item.Season) && String.IsNullOrWhiteSpace(item.SeriesId) ? "ÉPISODE" : "SÉRIE")
                : item.Kind == "Chaînes" ? "DIRECT" : "MÉDIA";
            var badge = Badge(kind, Brush(Color.FromArgb(190, 8, 8, 16)), item.Kind == "Chaînes" ? Brush(Danger) : Brushes.White);
            badge.HorizontalAlignment = HorizontalAlignment.Left;
            badge.VerticalAlignment = VerticalAlignment.Top;
            badge.Margin = new Thickness(8, 8, 0, 0);
            art.Children.Add(badge);

            var play = new Border
            {
                Width = 44, Height = 44, CornerRadius = new CornerRadius(22), Background = Brush(Accent),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0, IsHitTestVisible = false,
                Child = Icon(Glyphs.Play, 16, Brush(TextOnAccentColor())),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.4, Color = Colors.Black }
            };
            art.Children.Add(play);

            if (fraction > 0)
            {
                var bar = new Grid
                {
                    Height = 14, VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(10, 0, 10, 2)
                };
                bar.Children.Add(new Border
                {
                    Height = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center,
                    Background = Brush(Color.FromArgb(110, 255, 255, 255))
                });
                bar.Children.Add(new Border
                {
                    Height = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left, Background = Brush(Accent),
                    Width = (ResumeCardWidth - 34) * fraction
                });
                art.Children.Add(bar);
            }
            stack.Children.Add(art);

            var title = Text(item.Name, 12.5, Brush(TextPrimary), true);
            title.TextWrapping = TextWrapping.NoWrap;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.Margin = new Thickness(2, 9, 2, 1);
            stack.Children.Add(title);

            var status = Text(ResumeStatusLine(item), 10, Brush(Muted), false);
            status.TextWrapping = TextWrapping.NoWrap;
            status.TextTrimming = TextTrimming.CharacterEllipsis;
            status.Margin = new Thickness(2, 0, 2, 0);
            stack.Children.Add(status);

            var card = new Border
            {
                Child = stack, Width = ResumeCardWidth, Padding = new Thickness(7), CornerRadius = new CornerRadius(16),
                Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 12, 6), Cursor = Cursors.Hand, Focusable = true,
                ToolTip = ResumeStatusLine(item)
            };
            var lift = new TranslateTransform();
            card.RenderTransform = lift;
            card.MouseEnter += delegate
            {
                card.BorderBrush = Brush(Accent);
                card.Background = Brush(Raised);
                play.Opacity = 1;
                LiftCard(lift, -3);
            };
            card.MouseLeave += delegate
            {
                card.BorderBrush = Brush(BorderColor);
                card.Background = Brush(Panel);
                play.Opacity = 0;
                LiftCard(lift, 0);
            };
            card.MouseLeftButtonUp += delegate { ResumeItem(item); };

            var menu = CreateContextMenu();
            menu.Items.Add(MenuItem("Reprendre", delegate { ResumeItem(item); }, Glyphs.Play));
            menu.Items.Add(MenuItem("Recommencer depuis le début", delegate
            {
                positions.Remove(item.Id);
                StateStore.SavePositions(positions);
                Play(item, null, 0, false);
            }, Glyphs.Refresh));
            if (item.Kind == "Films" || item.Kind == "Séries" || item.Kind == "Épisodes")
                menu.Items.Add(MenuItem("Voir la fiche", delegate { ShowChannelInfo(item); }, Glyphs.Info));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Retirer de la liste", delegate
            {
                positions.Remove(item.Id);
                StateStore.SavePositions(positions);
                if (page == "Accueil") ShowHome();
                else Nav(page);
                ShowToast("Retiré de « Reprendre la lecture ».", false, true);
            }, Glyphs.Close));
            card.ContextMenu = menu;
            return card;
        }

        void ResumeItem(Channel item)
        {
            if (item == null) return;
            if (item.IsSeries || (String.Equals(item.Kind, "Séries", StringComparison.OrdinalIgnoreCase) && String.IsNullOrWhiteSpace(item.Url)))
            {
                LoadSeriesEpisodes(item);
                return;
            }
            double saved = GetSavedPosition(item);
            Play(item, null, saved, false);
        }

        FrameworkElement BuildResumeShelf(int max)
        {
            List<Channel> items = BuildResumeItems(max);
            if (items.Count == 0) return null;
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
            var seeAll = Chip("Tout voir", false, delegate { Nav("Bibliothèque"); });
            seeAll.Margin = new Thickness(0);
            DockPanel.SetDock(seeAll, Dock.Right);
            header.Children.Add(seeAll);
            var labels = new StackPanel();
            var kicker = Text("CONTINUER", 9, Brush(Accent), true);
            kicker.Margin = new Thickness(0, 0, 0, 3);
            labels.Children.Add(kicker);
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(Text("Reprendre la lecture", 19, Brush(TextPrimary), true));
            var count = Badge(TextUtil.HumanCount(items.Count), Brush(WithAlpha(TextPrimary, 10)), Brush(Muted));
            count.Margin = new Thickness(10, 0, 0, 0);
            titleRow.Children.Add(count);
            labels.Children.Add(titleRow);
            labels.Children.Add(Text("Vos séries, films et épisodes en cours, à reprendre en un clic.", 11, Brush(Muted), false));
            header.Children.Add(labels);
            section.Children.Add(header);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            foreach (Channel item in items)
            {
                var card = ResumeCard(item);
                if (card != null) row.Children.Add(card);
            }
            section.Children.Add(WrapLibraryScroller(row));
            return section;
        }

        /// <summary>Repli quand rien n'a encore été commencé : le dernier contenu regardé.</summary>
        Border LastPlayedCard()
        {
            var last = LastPlayedChannel();
            if (last == null) return null;
            double saved = GetSavedPosition(last);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var logo = BuildLogo(last, 46); Grid.SetColumn(logo, 0); grid.Children.Add(logo);
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 10, 0) };
            labels.Children.Add(Text(last.Name, 15, Brush(TextPrimary), true));
            string sub = last.Group + "  ·  " + KindLabel(last.Kind);
            var current = CurrentProgram(last);
            if (current != null) sub = "À l'antenne  ·  " + current.Title;
            labels.Children.Add(Text(sub, 10.5, Brush(Muted), false));
            if (saved > 0) labels.Children.Add(Text("Reprendre à " + TextUtil.FormatPosition(TimeSpan.FromSeconds(saved)), 10, Brush(Accent), true));
            Grid.SetColumn(labels, 1); grid.Children.Add(labels);
            var play = Button(Glyphs.Play, saved > 0 ? "Reprendre" : "Regarder", delegate { Play(last, null, saved, false); }, true);
            play.Margin = new Thickness(0); Grid.SetColumn(play, 2); grid.Children.Add(play);
            return Card(grid, 14);
        }
    }
}
