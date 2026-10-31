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
    public sealed partial class MainWindow
    {
        /// <summary>
        /// Recherche de sous-titres sur OpenSubtitles : le titre du film ou de la
        /// série est prérempli (saison et épisode compris), il ne reste qu'à
        /// choisir un fichier dans la liste.
        /// </summary>
        void ShowSubtitleSearch()
        {
            if (currentPlaying == null)
            {
                ShowError("Lancez une lecture avant de chercher des sous-titres.");
                return;
            }
            bool episode = IsEpisodeSearch();
            string title = SubtitleSearchTitle();

            var dialog = new Window
            {
                Title = "Sous-titres OpenSubtitles",
                Width = 780, Height = 660, MinWidth = 640, MinHeight = 500,
                ResizeMode = ResizeMode.CanResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, WindowStyle = WindowStyle.None, Background = Brushes.Transparent,
                AllowsTransparency = true, FontFamily = new FontFamily("Segoe UI"), ShowInTaskbar = false
            };
            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel), BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(24)
            };
            dialog.Content = frame;
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            frame.Child = layout;

            // ------------------------------------------------------------- en-tête
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 16) };
            var close = IconButton(Glyphs.Close, "Fermer", delegate { dialog.Close(); }, false);
            close.Width = 32; close.Height = 30; close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            var heading = new StackPanel();
            heading.Children.Add(Text("Sous-titres en ligne", 21, Brush(TextPrimary), true));
            heading.Children.Add(Text(episode
                ? "Le nom de la série, la saison et l'épisode sont déjà remplis."
                : "Le titre du film est déjà rempli : choisissez simplement le fichier qui vous convient.", 11.5, Brush(Muted), false));
            header.Children.Add(heading);
            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            // ---------------------------------------------------------- recherche
            var searchRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var query = StyledTextBox();
            query.Height = 36;
            query.Text = title;
            query.ToolTip = "Titre recherché sur OpenSubtitles";
            Grid.SetColumn(query, 0);
            searchRow.Children.Add(query);

            var language = new ComboBox { Width = 150, Height = 36, Style = ModernComboStyle(), Margin = new Thickness(8, 0, 8, 0) };
            AddSubtitleLanguage(language, "Français", "fr");
            AddSubtitleLanguage(language, "Anglais", "en");
            AddSubtitleLanguage(language, "Espagnol", "es");
            AddSubtitleLanguage(language, "Allemand", "de");
            AddSubtitleLanguage(language, "Italien", "it");
            AddSubtitleLanguage(language, "Portugais", "pt");
            AddSubtitleLanguage(language, "Arabe", "ar");
            AddSubtitleLanguage(language, "Toutes", "all");
            language.SelectedIndex = 0;
            string preferredLanguage = (settings.SubtitleLanguage ?? "fr").Trim().ToLowerInvariant();
            if (preferredLanguage.Length > 2) preferredLanguage = preferredLanguage.Substring(0, 2);
            for (int i = 0; i < language.Items.Count; i++)
            {
                var entry = language.Items[i] as ComboBoxItem;
                if (entry != null && String.Equals(Convert.ToString(entry.Tag), preferredLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    language.SelectedIndex = i;
                    break;
                }
            }
            Grid.SetColumn(language, 1);
            searchRow.Children.Add(language);

            var status = Text("", 11.5, Brush(Muted), false);
            status.TextWrapping = TextWrapping.Wrap;
            status.Margin = new Thickness(0, 0, 0, 10);

            // ------------------------------------------------------------ résultats
            var results = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 0, 8, 0), Content = results
            };

            Button search = null;
            search = Button(Glyphs.Search, "Rechercher", delegate
            {
                RunSubtitleSearch(dialog, query, language, episode, results, status, search);
            }, true);
            search.Height = 36;
            search.Margin = new Thickness(0);
            Grid.SetColumn(search, 2);
            searchRow.Children.Add(search);
            Grid.SetRow(searchRow, 1);
            layout.Children.Add(searchRow);
            Grid.SetRow(status, 2);
            layout.Children.Add(status);
            Grid.SetRow(scroll, 3);
            layout.Children.Add(scroll);

            query.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key != Key.Enter) return;
                e.Handled = true;
                RunSubtitleSearch(dialog, query, language, episode, results, status, search);
            };

            // -------------------------------------------------------------- pied
            var footer = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 12, 0, 0) };
            var local = Button(Glyphs.OpenFile, "Choisir un fichier local", delegate
            {
                dialog.Close();
                ChooseSubtitle();
            }, false);
            local.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(local, Dock.Right);
            footer.Children.Add(local);
            var credit = Text(OpenSubtitlesClient.Attribution, 10, Brush(Muted), false);
            credit.VerticalAlignment = VerticalAlignment.Center;
            footer.Children.Add(credit);
            Grid.SetRow(footer, 4);
            layout.Children.Add(footer);

            bool configured = OpenSubtitlesClient.IsConfigured(settings);
            if (!configured)
            {
                status.Foreground = Brush(Warning);
                status.Text = "Aucune clé API OpenSubtitles. Ajoutez-la dans Paramètres → Métadonnées et sous-titres pour lancer la recherche automatique.";
                AddSubtitleHint(results, Glyphs.Globe,
                    "La clé est gratuite sur opensubtitles.com. En attendant, vous pouvez ouvrir un fichier de sous-titres depuis votre disque.");
            }
            else
            {
                status.Text = episode
                    ? "Recherche de « " + query.Text + " » · saison " + (currentPlaying.Season ?? "") + " · épisode " + (currentPlaying.Episode ?? "") + "…"
                    : "Recherche de « " + query.Text + " »…";
            }

            dialog.Loaded += delegate
            {
                if (configured) RunSubtitleSearch(dialog, query, language, episode, results, status, search);
            };
            dialog.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { e.Handled = true; dialog.Close(); }
            };
            dialog.ShowDialog();
        }

        static void AddSubtitleLanguage(ComboBox box, string label, string code)
        {
            box.Items.Add(new ComboBoxItem { Content = label, Tag = code });
        }

        bool IsEpisodeSearch()
        {
            return currentPlaying != null
                && (String.Equals(currentPlaying.Kind, "Épisodes", StringComparison.OrdinalIgnoreCase)
                    || !String.IsNullOrWhiteSpace(currentPlaying.SeriesId)
                    || !String.IsNullOrWhiteSpace(currentPlaying.Season));
        }

        /// <summary>
        /// Titre envoyé à OpenSubtitles : celui de la série pour un épisode, sinon
        /// celui du film, débarrassé des préfixes et suffixes de playlist.
        /// </summary>
        string SubtitleSearchTitle()
        {
            Channel series = null;
            try { series = SeriesForEpisode(currentPlaying); } catch { series = null; }
            string raw = series != null && !String.IsNullOrWhiteSpace(series.Name)
                ? series.Name
                : (currentPlaying == null ? "" : currentPlaying.Name);
            string cleaned = OpenSubtitlesClient.CleanTitle(raw);
            return String.IsNullOrWhiteSpace(cleaned) ? (raw ?? "").Trim() : cleaned;
        }

        void RunSubtitleSearch(Window dialog, TextBox query, ComboBox language, bool episode,
            StackPanel results, TextBlock status, Button trigger)
        {
            if (results == null || status == null) return;
            string title = (query == null ? "" : query.Text ?? "").Trim();
            if (title.Length == 0)
            {
                status.Foreground = Brush(Danger);
                status.Text = "Indiquez le titre du film ou de la série.";
                return;
            }
            if (trigger != null) trigger.IsEnabled = false;
            results.Children.Clear();
            status.Foreground = Brush(Muted);
            status.Text = "Recherche sur OpenSubtitles…";
            var selected = language == null ? null : language.SelectedItem as ComboBoxItem;
            string code = selected == null ? "fr" : Convert.ToString(selected.Tag);
            string season = currentPlaying == null ? "" : currentPlaying.Season;
            string episodeNumber = currentPlaying == null ? "" : currentPlaying.Episode;

            var worker = new BackgroundWorker();
            bool finished = false;
            // Garde-fou : si l'API ne répond pas, l'écran ne reste pas bloqué
            // sur « Recherche… » indéfiniment. Le bouton redevient actif et un
            // message invite à réessayer.
            var watchdog = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            watchdog.Tick += delegate
            {
                if (finished) { watchdog.Stop(); return; }
                watchdog.Stop();
                if (trigger != null) trigger.IsEnabled = true;
                if (!dialog.IsVisible) return;
                status.Foreground = Brush(Warning);
                status.Text = "OpenSubtitles met trop de temps à répondre. Vérifiez la connexion puis relancez la recherche.";
            };
            watchdog.Start();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = OpenSubtitlesClient.Search(settings, title, code, episode, season, episodeNumber);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                finished = true;
                watchdog.Stop();
                if (trigger != null) trigger.IsEnabled = true;
                if (appClosing) return;
                if (e.Error != null)
                {
                    status.Foreground = Brush(Danger);
                    string reason = e.Error.GetBaseException().Message;
                    if (reason.IndexOf("expir", StringComparison.OrdinalIgnoreCase) >= 0
                        || reason.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                        || reason.IndexOf("connexion", StringComparison.OrdinalIgnoreCase) >= 0)
                        reason = "Connexion à OpenSubtitles impossible. Vérifiez votre réseau puis réessayez.";
                    status.Text = reason;
                    return;
                }
                var list = e.Result as List<OnlineSubtitle>;
                if (list == null || list.Count == 0)
                {
                    status.Foreground = Brush(Muted);
                    status.Text = "Aucun sous-titre trouvé pour « " + title + " » dans cette langue. Essayez « Toutes » ou un autre titre.";
                    AddSubtitleHint(results, Glyphs.Search,
                        "Aucun fichier pour cette recherche. Vous pouvez corriger le titre, essayer « Toutes » ou charger un fichier local.");
                    return;
                }
                status.Foreground = Brush(Success);
                status.Text = list.Count.ToString(CultureInfo.InvariantCulture) + (list.Count > 1 ? " fichiers trouvés" : " fichier trouvé") + " · choisissez celui qui correspond à votre version.";
                foreach (var item in list) AddSubtitleResult(results, item, dialog, status);
            };
            worker.RunWorkerAsync();
        }

        void AddSubtitleHint(StackPanel results, string glyph, string message)
        {
            if (results == null) return;
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 46, 0, 0) };
            var icon = Icon(glyph, 30, Brush(Muted));
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(icon);
            var text = Text(message, 11.5, Brush(Muted), false);
            text.TextAlignment = TextAlignment.Center;
            text.TextWrapping = TextWrapping.Wrap;
            text.MaxWidth = 520;
            stack.Children.Add(text);
            results.Children.Add(stack);
        }

        void AddSubtitleResult(StackPanel results, OnlineSubtitle item, Window dialog, TextBlock status)
        {
            if (results == null || item == null) return;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var label = Text(item.Label, 12.5, Brush(TextPrimary), true);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.ToolTip = item.Label;
            left.Children.Add(label);
            var detail = Text(item.Detail, 10.5, Brush(Muted), false);
            detail.Margin = new Thickness(0, 3, 0, 0);
            left.Children.Add(detail);
            grid.Children.Add(left);

            var captured = item;
            Button use = null;
            use = Button(Glyphs.Download, "Utiliser", delegate
            {
                use.IsEnabled = false;
                DownloadOnlineSubtitle(captured, dialog, status, use);
            }, false);
            use.Margin = new Thickness(12, 0, 0, 0);
            use.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(use, 1);
            grid.Children.Add(use);

            var card = Card(grid, 12);
            card.Margin = new Thickness(0, 0, 0, 8);
            results.Children.Add(card);
        }

        void DownloadOnlineSubtitle(OnlineSubtitle subtitle, Window dialog, TextBlock status, Button trigger)
        {
            if (subtitle == null) return;
            if (status != null)
            {
                status.Foreground = Brush(Accent);
                status.Text = "Téléchargement de « " + TextUtil.Ellipsis(subtitle.Label, 70) + " »…";
            }
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = OpenSubtitlesClient.Download(settings, subtitle);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (trigger != null) trigger.IsEnabled = true;
                if (appClosing) return;
                if (e.Error != null)
                {
                    if (status != null)
                    {
                        status.Foreground = Brush(Danger);
                        status.Text = e.Error.GetBaseException().Message;
                    }
                    return;
                }
                string path = e.Result as string;
                if (String.IsNullOrWhiteSpace(path)) return;
                LoadSubtitle(path, true);
                if (dialog != null) dialog.Close();
            };
            worker.RunWorkerAsync();
        }
    }
}
