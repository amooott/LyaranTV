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
    /// Tiroir des chaînes du lecteur : colonne de gauche façon IPTV / box TV.
    /// La vidéo reste en place, on zappe sans quitter la chaîne en cours.
    /// </summary>
    public sealed partial class MainWindow
    {
        Border channelDrawer;
        StackPanel channelDrawerList;
        TextBox channelDrawerFilter;
        TextBlock channelDrawerCount;
        string channelDrawerQuery = "";
        ScrollViewer channelDrawerScroll;

        void ToggleChannelDrawer()
        {
            if (page != "Lecture") return;
            if (channelDrawer == null || channelDrawer.Visibility != Visibility.Visible) OpenChannelDrawer();
            else CloseChannelDrawer();
        }

        void OpenChannelDrawer()
        {
            if (page != "Lecture" || playerVideoGrid == null) return;
            if (channelDrawer == null) BuildChannelDrawer();
            RefreshChannelDrawer();
            channelDrawer.Visibility = Visibility.Visible;
            ShowFullscreenControls();
            if (channelDrawerFilter != null && pointerInput)
            {
                // Clavier/souris : focus direct sur le filtre. Manette : on
                // laisse le focus spatial parcourir la liste.
                channelDrawerFilter.Focus();
                channelDrawerFilter.SelectAll();
            }
        }

        void CloseChannelDrawer()
        {
            if (channelDrawer != null) channelDrawer.Visibility = Visibility.Collapsed;
        }

        bool IsChannelDrawerOpen()
        {
            return channelDrawer != null && channelDrawer.Visibility == Visibility.Visible;
        }

        void BuildChannelDrawer()
        {
            var drawer = new Border
            {
                Width = 348,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0),
                Background = Brush(Color.FromArgb(242, 8, 9, 15)),
                BorderBrush = Brush(WithAlpha(BorderColor, 160)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Visibility = Visibility.Collapsed
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            drawer.Child = grid;

            // En-tête : titre + compteur + fermer.
            var header = new Grid { Margin = new Thickness(14, 12, 10, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titles = new StackPanel();
            titles.Children.Add(Text("CHAÎNES EN DIRECT", 12, Brush(Accent), true));
            channelDrawerCount = Text("", 11.5, Brush(Muted), false);
            channelDrawerCount.Margin = new Thickness(0, 2, 0, 0);
            titles.Children.Add(channelDrawerCount);
            header.Children.Add(titles);
            var close = PlayerButton(Glyphs.Close, "Fermer la liste des chaînes · C / X", CloseChannelDrawer, false, 34);
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Filtre rapide, utilisable au clavier comme à la manette (via clavier virtuel).
            var filterWrap = new Border
            {
                Background = Brush(Raised), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9), Margin = new Thickness(14, 0, 14, 8), Padding = new Thickness(10, 0, 6, 0)
            };
            var filterGrid = new Grid();
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = Icon(Glyphs.Search, 14, Brush(Muted));
            icon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(icon, 0);
            filterGrid.Children.Add(icon);
            channelDrawerFilter = new TextBox
            {
                Height = 34, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = Brush(TextPrimary), CaretBrush = Brush(Accent),
                VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0)
            };
            channelDrawerFilter.ToolTip = "Filtrer les chaînes";
            channelDrawerFilter.TextChanged += delegate
            {
                channelDrawerQuery = channelDrawerFilter.Text == null ? "" : channelDrawerFilter.Text.Trim();
                RefreshChannelDrawer();
            };
            channelDrawerFilter.PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { CloseChannelDrawer(); e.Handled = true; }
                else if (e.Key == Key.Down && channelDrawerList != null && channelDrawerList.Children.Count > 0)
                {
                    var first = channelDrawerList.Children[0] as Button;
                    if (first != null) first.Focus();
                    e.Handled = true;
                }
            };
            Grid.SetColumn(channelDrawerFilter, 1);
            filterGrid.Children.Add(channelDrawerFilter);
            var kb = IconButton(Glyphs.Keyboard, "Clavier à l'écran", delegate
            {
                ShowVirtualKeyboard(channelDrawerFilter);
            }, false);
            kb.Width = 30; kb.Height = 28;
            Grid.SetColumn(kb, 2);
            filterGrid.Children.Add(kb);
            filterWrap.Child = filterGrid;
            Grid.SetRow(filterWrap, 1);
            grid.Children.Add(filterWrap);

            channelDrawerScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(8, 0, 8, 4)
            };
            channelDrawerList = new StackPanel();
            channelDrawerScroll.Content = channelDrawerList;
            Grid.SetRow(channelDrawerScroll, 2);
            grid.Children.Add(channelDrawerScroll);

            var footer = new Border
            {
                BorderBrush = Brush(WithAlpha(BorderColor, 150)), BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 9, 14, 11)
            };
            footer.Child = Text("Page haut / bas · zapper  ·  Entrée · regarder  ·  C · fermer", 10.5, Brush(Muted), false);
            Grid.SetRow(footer, 3);
            grid.Children.Add(footer);

            channelDrawer = drawer;
            System.Windows.Controls.Panel.SetZIndex(drawer, 32);
            playerVideoGrid.Children.Add(drawer);
        }

        List<Channel> ChannelDrawerSource()
        {
            var list = new List<Channel>();
            // On reste sur la chaîne de départ : la file du lecteur d'abord,
            // sinon toutes les chaînes visibles de la source.
            if (playQueue != null)
            {
                foreach (var c in playQueue)
                {
                    if (c != null && c.Kind == "Chaînes") list.Add(c);
                }
            }
            if (list.Count == 0)
            {
                foreach (var c in VisibleChannels)
                {
                    if (c != null && c.Kind == "Chaînes" && !hiddenChannels.Contains(c.Id)) list.Add(c);
                }
            }
            if (!String.IsNullOrWhiteSpace(channelDrawerQuery))
            {
                string q = channelDrawerQuery.Trim();
                list = list.Where(delegate(Channel c) { return c != null && c.MatchesSearch(q); }).ToList();
            }
            return list.OrderBy(delegate(Channel c) { return c == null ? "" : c.Name; }, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        void RefreshChannelDrawer()
        {
            if (channelDrawerList == null) return;
            channelDrawerList.Children.Clear();
            var items = ChannelDrawerSource();
            if (channelDrawerCount != null)
                channelDrawerCount.Text = TextUtil.HumanCount(items.Count) + (items.Count == 1 ? " chaîne" : " chaînes");
            string currentId = currentPlaying == null ? "" : currentPlaying.Id;
            int shown = 0;
            foreach (var channel in items)
            {
                if (shown >= 220) break;
                shown++;
                channelDrawerList.Children.Add(BuildChannelDrawerRow(channel, String.Equals(channel.Id, currentId, StringComparison.OrdinalIgnoreCase)));
            }
            if (items.Count == 0)
            {
                var empty = Text("Aucune chaîne ne correspond à ce filtre.", 12, Brush(Muted), false);
                empty.Margin = new Thickness(10, 14, 10, 10);
                empty.TextWrapping = TextWrapping.Wrap;
                channelDrawerList.Children.Add(empty);
            }
            if (channelDrawerScroll != null) channelDrawerScroll.ScrollToTop();
        }

        Button BuildChannelDrawerRow(Channel channel, bool current)
        {
            var row = new Grid { MinHeight = 56 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Logo : vrai logo de la chaîne (tvg-logo) d'abord, affiche TMDb ensuite, initiale en dernier recours.
            Border logo;
            string logoUrl = !String.IsNullOrWhiteSpace(channel.Logo) ? channel.Logo.Trim() : "";
            if (String.IsNullOrWhiteSpace(logoUrl) && !String.IsNullOrWhiteSpace(channel.PosterUrl))
                logoUrl = channel.PosterUrl.Trim();
            if (!String.IsNullOrWhiteSpace(logoUrl))
            {
                logo = new Border
                {
                    Width = 44, Height = 44, CornerRadius = new CornerRadius(9),
                    Background = Brush(Color.FromRgb(22, 22, 32)),
                    BorderBrush = Brush(current ? WithAlpha(Accent, 220) : WithAlpha(BorderColor, 170)),
                    BorderThickness = new Thickness(1), ClipToBounds = true, Padding = new Thickness(3),
                    Child = new AsyncImage { Url = logoUrl, Stretch = Stretch.Uniform }
                };
            }
            else
            {
                string initial = String.IsNullOrWhiteSpace(channel.Name) ? "?" : channel.Name.Trim().Substring(0, 1).ToUpperInvariant();
                var label = Text(initial, 17, Brush(Color.FromRgb(255, 255, 255)), true);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.TextAlignment = TextAlignment.Center;
                logo = new Border
                {
                    Width = 44, Height = 44, CornerRadius = new CornerRadius(9),
                    Background = current ? AccentGradientBrush() : Brush(Raised),
                    BorderBrush = Brush(current ? WithAlpha(Accent, 220) : WithAlpha(BorderColor, 170)),
                    BorderThickness = new Thickness(1), Child = label
                };
            }
            logo.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(logo, 0);
            row.Children.Add(logo);

            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 6, 0) };
            var name = Text(TextUtil.DisplayTitle(channel.Name), 13, Brush(current ? Accent : TextPrimary), current);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            texts.Children.Add(name);
            var program = CurrentProgram(channel);
            if (program != null)
            {
                var prog = Text(TextUtil.Ellipsis(program.Title, 52), 11, Brush(Muted), false);
                prog.TextTrimming = TextTrimming.CharacterEllipsis;
                texts.Children.Add(prog);
                var bar = new Border { Height = 3, CornerRadius = new CornerRadius(2), Background = Brush(WithAlpha(TextPrimary, 26)), Margin = new Thickness(0, 4, 0, 0) };
                double total = Math.Max(1, program.Duration.TotalSeconds);
                double done = Math.Max(0, Math.Min(total, (DateTimeOffset.Now - program.Start).TotalSeconds));
                bar.Child = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Left, Width = Math.Max(2, 200 * done / total), Height = 3,
                    CornerRadius = new CornerRadius(2), Background = Brush(current ? Accent : Success)
                };
                texts.Children.Add(bar);
            }
            else
            {
                var sub = Text(String.IsNullOrWhiteSpace(channel.Group) ? "Direct" : channel.Group, 11, Brush(Muted), false);
                sub.TextTrimming = TextTrimming.CharacterEllipsis;
                texts.Children.Add(sub);
            }
            Grid.SetColumn(texts, 1);
            row.Children.Add(texts);

            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (current)
            {
                var badge = new Border
                {
                    Child = Text("EN COURS", 9, Brush(Color.FromRgb(255, 255, 255)), true),
                    Background = AccentGradientBrush(), CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(7, 3, 7, 4)
                };
                right.Children.Add(badge);
            }
            else if (favorites.Contains(channel.Id))
            {
                right.Children.Add(Icon(Glyphs.FavoriteFill, 14, Brush(Accent)));
            }
            Grid.SetColumn(right, 2);
            row.Children.Add(right);

            var button = new Button
            {
                Content = row, Padding = new Thickness(8, 6, 10, 6), Margin = new Thickness(0, 1, 0, 1),
                Background = current ? Brush(WithAlpha(Accent, 30)) : Brushes.Transparent,
                BorderBrush = Brush(current ? WithAlpha(Accent, 140) : Colors.Transparent),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, FocusVisualStyle = null,
                Template = RoundedButtonTemplate(10, false),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                ToolTip = channel.Name + " · Entrée pour regarder"
            };
            if (!current)
            {
                button.MouseEnter += delegate { button.Background = HoverBrush; };
                button.MouseLeave += delegate { button.Background = Brushes.Transparent; };
            }
            Channel captured = channel;
            button.Click += delegate
            {
                if (currentPlaying != null && String.Equals(currentPlaying.Id, captured.Id, StringComparison.OrdinalIgnoreCase))
                {
                    CloseChannelDrawer();
                    return;
                }
                Play(captured, playQueue, 0, false);
                Dispatcher.BeginInvoke(new Action(delegate { RefreshChannelDrawer(); }), System.Windows.Threading.DispatcherPriority.Background);
            };
            return button;
        }

        void MoveChannelDrawerSelection(int direction)
        {
            try
            {
                if (channelDrawerList == null) return;
                var items = ChannelDrawerSource();
                if (items.Count == 0 || currentPlaying == null) return;
                int index = -1;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null && String.Equals(items[i].Id, currentPlaying.Id, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
                }
                int next = index < 0 ? 0 : (index + direction + items.Count) % items.Count;
                Channel target = items[next];
                if (target == null) return;
                Play(target, playQueue, 0, false);
                RefreshChannelDrawer();
            }
            catch { }
        }
    }
}
