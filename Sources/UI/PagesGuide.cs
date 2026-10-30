using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LyaranTV
{
    public sealed partial class MainWindow
    {
        string epgFilter = "Tout";

        void ShowEpg()
        {
            page = "Guide EPG";
            RebuildSidebar();
            UpdateStatusBar();
            var root = PageRoot("Guide des programmes", loadedEpg.Count == 0
                ? "Importez un guide XMLTV pour voir ce qui passe maintenant et à venir."
                : TextUtil.HumanCount(loadedEpg.Count) + " programmes chargés · source XMLTV");

            var top = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dateBar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var previous = IconButton(Glyphs.ChevronLeft, "Jour précédent", delegate { epgDate = epgDate.AddDays(-1); ShowEpg(); }, false);
            previous.Margin = new Thickness(0, 0, 5, 0);
            dateBar.Children.Add(previous);
            var today = Button(Glyphs.Clock, "Aujourd'hui", delegate { epgDate = DateTime.Today; ShowEpg(); }, epgDate.Date == DateTime.Today);
            today.Margin = new Thickness(0, 0, 5, 0);
            dateBar.Children.Add(today);
            var next = IconButton(Glyphs.ChevronRight, "Jour suivant", delegate { epgDate = epgDate.AddDays(1); ShowEpg(); }, false);
            next.Margin = new Thickness(0, 0, 10, 0);
            dateBar.Children.Add(next);
            var dateLabel = Text(TextUtil.FormatDate(epgDate), 14, Brush(TextPrimary), true);
            dateLabel.VerticalAlignment = VerticalAlignment.Center;
            dateBar.Children.Add(dateLabel);
            Grid.SetColumn(dateBar, 0);
            top.Children.Add(dateBar);

            var tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var import = Button(Glyphs.Upload, "Importer XMLTV", ImportEpgFile, false);
            import.Margin = new Thickness(0, 0, 7, 0);
            tools.Children.Add(import);
            if (activeSource != null)
            {
                var reload = Button(Glyphs.Refresh, "Recharger la playlist", delegate { Activate(activeSource); }, false);
                reload.Margin = new Thickness(0, 0, 7, 0);
                tools.Children.Add(reload);
            }
            Grid.SetColumn(tools, 1);
            top.Children.Add(tools);
            root.Children.Add(top);

            var sourceCard = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            sourceCard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sourceCard.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var sourceStack = new StackPanel();
            sourceStack.Children.Add(Text("SOURCE DU GUIDE", 9.5, Brush(Muted), true));
            var sourceBox = StyledTextBox();
            sourceBox.Height = 36;
            sourceBox.Margin = new Thickness(0, 4, 8, 0);
            sourceBox.Text = activeSource == null ? "" : (activeSource.EpgUrl ?? "");
            sourceBox.ToolTip = "URL XMLTV, ou chemin d'un fichier .xml / .xml.gz";
            var sourceHint = Text("URL XMLTV ou fichier .xml / .xml.gz", 11.5, Brush(Muted), false);
            sourceHint.IsHitTestVisible = false;
            sourceHint.Margin = new Thickness(12, 0, 0, 0);
            var sourceHost = new Grid();
            sourceHost.Children.Add(sourceBox);
            sourceHost.Children.Add(sourceHint);
            SetPlaceholder(sourceBox, sourceHint);
            sourceStack.Children.Add(sourceHost);
            Grid.SetColumn(sourceStack, 0);
            sourceCard.Children.Add(sourceStack);
            var sourceActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1) };
            var save = Button(Glyphs.Save, "Enregistrer", delegate
            {
                if (activeSource == null) { ShowError("Ajoutez ou sélectionnez une playlist d'abord."); return; }
                string value = sourceBox.Text.Trim();
                if (value.Length > 0 && !TextUtil.IsHttp(value) && !File.Exists(value)) { ShowError("Indiquez une URL HTTP(S) ou un fichier XMLTV existant."); return; }
                activeSource.EpgUrl = value;
                SaveSources();
                if (value.Length > 0) LoadEpgLocation(value, true);
                else ShowToast("Source EPG effacée.", false, true);
            }, true);
            save.Margin = new Thickness(0, 0, 6, 0);
            sourceActions.Children.Add(save);
            var browse = Button(Glyphs.OpenFile, "Parcourir", ImportEpgFile, false);
            sourceActions.Children.Add(browse);
            Grid.SetColumn(sourceActions, 1);
            sourceCard.Children.Add(sourceActions);
            root.Children.Add(Card(sourceCard, 13));

            var filterBar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            filterBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var searchBox = StyledTextBox();
            searchBox.Width = 300;
            searchBox.HorizontalAlignment = HorizontalAlignment.Left;
            searchBox.Text = epgQuery;
            var searchHint = Text("Rechercher une chaîne ou un programme…", 12, Brush(Muted), false);
            searchHint.IsHitTestVisible = false;
            searchHint.Margin = new Thickness(12, 0, 0, 0);
            var searchHost = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Width = 300 };
            searchHost.Children.Add(searchBox);
            searchHost.Children.Add(searchHint);
            SetPlaceholder(searchBox, searchHint);
            DockPanel.SetDock(searchHost, Dock.Left);
            filterBar.Children.Add(searchHost);
            var filter = new ComboBox { Width = 150, Height = 34, Style = ModernComboStyle() };
            filter.Items.Add("Tout"); filter.Items.Add("En cours"); filter.Items.Add("À venir");
            filter.SelectedItem = epgFilter;
            Grid.SetColumn(filter, 1);
            filterBar.Children.Add(filter);
            root.Children.Add(filterBar);

            var listing = new StackPanel();
            root.Children.Add(listing);
            Action render = null;
            render = delegate
            {
                listing.Children.Clear();
                epgQuery = searchBox.Text.Trim();
                epgFilter = filter.SelectedItem == null ? "Tout" : (string)filter.SelectedItem;
                var now = DateTimeOffset.Now;
                DateTime dayStart = epgDate.Date;
                DateTime dayEnd = dayStart.AddDays(1);
                string query = epgQuery;
                var rows = new List<EpgChannelRow>();
                var visibleChannels = VisibleChannels.Where(c => !hiddenChannels.Contains(c.Id)).ToList();
                foreach (var channel in visibleChannels)
                {
                    var programs = ProgramsFor(channel).Where(p => p.Stop.ToLocalTime().DateTime > dayStart && p.Start.ToLocalTime().DateTime < dayEnd).OrderBy(p => p.Start).ToList();
                    if (programs.Count == 0) continue;
                    if (epgFilter == "En cours" && !programs.Any(p => p.IsLive(now))) continue;
                    if (epgFilter == "À venir" && !programs.Any(p => p.Start > now)) continue;
                    if (query.Length > 0 && !TextUtil.Contains(channel.Name, query) && !programs.Any(p => TextUtil.Contains(p.Title, query) || TextUtil.Contains(p.Description, query))) continue;
                    rows.Add(new EpgChannelRow { Channel = channel, Programs = programs });
                }
                if (visibleChannels.Count == 0 && (activeProfile == null || !activeProfile.IsChild || !settings.ParentalEnabled))
                {
                    foreach (var group in loadedEpg.Where(p => p.Stop.ToLocalTime().DateTime > dayStart && p.Start.ToLocalTime().DateTime < dayEnd).GroupBy(p => p.ChannelName ?? "").OrderBy(g => g.Key))
                    {
                        var programs = group.OrderBy(p => p.Start).ToList();
                        if (epgFilter == "En cours" && !programs.Any(p => p.IsLive(now))) continue;
                        if (epgFilter == "À venir" && !programs.Any(p => p.Start > now)) continue;
                        if (query.Length > 0 && !TextUtil.Contains(group.Key, query) && !programs.Any(p => TextUtil.Contains(p.Title, query) || TextUtil.Contains(p.Description, query))) continue;
                        rows.Add(new EpgChannelRow { ChannelName = group.Key, Programs = programs });
                    }
                }
                if (rows.Count == 0)
                {
                    var empty = EmptyState(Glyphs.Guide, "Aucun programme à afficher",
                        loadedEpg.Count == 0 ? "Importez un guide XMLTV ou renseignez l'URL EPG de votre playlist." : "Aucun programme ne correspond à ce jour ou à ce filtre.", null);
                    listing.Children.Add(empty);
                    return;
                }
                int shown = 0;
                foreach (var row in rows.OrderBy(r => r.ChannelName).Take(120))
                {
                    var cardStack = new StackPanel();
                    var head = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                    head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
                    head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    if (row.Channel != null)
                    {
                        var logo = BuildLogo(row.Channel, 38);
                        Grid.SetColumn(logo, 0);
                        head.Children.Add(logo);
                    }
                    var names = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    names.Children.Add(Text(row.Channel != null ? row.Channel.Name : (String.IsNullOrWhiteSpace(row.ChannelName) ? "Chaîne inconnue" : row.ChannelName), 13.5, Brush(TextPrimary), true));
                    names.Children.Add(Text(TextUtil.HumanCount(row.Programs.Count) + " programme(s) ce jour", 10, Brush(Muted), false));
                    Grid.SetColumn(names, 1);
                    head.Children.Add(names);
                    if (row.Channel != null)
                    {
                        var play = Button(Glyphs.Play, "Regarder", delegate { Play(row.Channel, null, 0, false); }, true);
                        play.Margin = new Thickness(0);
                        Grid.SetColumn(play, 2);
                        head.Children.Add(play);
                    }
                    cardStack.Children.Add(head);
                    foreach (var program in row.Programs.Take(10))
                    {
                        bool live = program.IsLive(now);
                        var programRow = new Grid { Margin = new Thickness(0, 0, 0, 4), MinHeight = 36 };
                        programRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
                        programRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        var time = Text(program.TimeRange, 10.5, live ? Brush(Success) : Brush(Accent), true);
                        time.VerticalAlignment = VerticalAlignment.Center;
                        Grid.SetColumn(time, 0);
                        programRow.Children.Add(time);
                        var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                        titlePanel.Children.Add(Text(program.Title, 12, live ? Brush(TextPrimary) : Brush(TextPrimary), live));
                        if (!String.IsNullOrWhiteSpace(program.Description))
                            titlePanel.Children.Add(Text(TextUtil.Ellipsis(program.Description, 160), 9.5, Brush(Muted), false));
                        Grid.SetColumn(titlePanel, 1);
                        programRow.Children.Add(titlePanel);
                        var programBorder = new Border
                        {
                            Child = programRow, Background = live ? Brush(WithAlpha(Success, 20)) : Brush(Raised),
                            BorderBrush = live ? Brush(WithAlpha(Success, 80)) : Brush(BorderColor), BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 5, 9, 5), Cursor = row.Channel == null ? Cursors.Arrow : Cursors.Hand,
                            ToolTip = String.IsNullOrWhiteSpace(program.Description) ? program.TimeRange : program.Description
                        };
                        if (row.Channel != null)
                        {
                            Channel captured = row.Channel;
                            programBorder.MouseLeftButtonUp += delegate { Play(captured, null, 0, false); };
                        }
                        cardStack.Children.Add(programBorder);
                    }
                    if (row.Programs.Count > 10)
                    {
                        var more = Text("+ " + TextUtil.HumanCount(row.Programs.Count - 10) + " autres programmes…", 10, Brush(Muted), false);
                        more.Margin = new Thickness(62, 2, 0, 0);
                        cardStack.Children.Add(more);
                    }
                    var card = Card(cardStack, 13);
                    listing.Children.Add(card);
                    shown++;
                }
                if (rows.Count > shown)
                {
                    var note = Text("Affichage limité à " + shown + " chaînes. Utilisez la recherche pour affiner.", 10.5, Brush(Muted), false);
                    note.HorizontalAlignment = HorizontalAlignment.Center;
                    note.Margin = new Thickness(0, 6, 0, 0);
                    listing.Children.Add(note);
                }
            };
            searchBox.TextChanged += delegate { if (render != null) render(); };
            filter.SelectionChanged += delegate { if (render != null) render(); };
            render();
            SetContent(root);
            SetStatus(loadedEpg.Count == 0 ? "Aucun guide chargé." : "Guide des programmes prêt.");
        }

        sealed class EpgChannelRow
        {
            public Channel Channel;
            public string ChannelName;
            public List<EpgProgram> Programs = new List<EpgProgram>();
        }

        void ImportEpgFile()
        {
            var dialog = new OpenFileDialog { Filter = "Guide XMLTV (*.xml;*.xml.gz;*.gz)|*.xml;*.xml.gz;*.gz|Tous les fichiers (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            LoadEpgLocation(dialog.FileName, true);
        }

        void LoadEpgLocation(string location, bool saveToSource)
        {
            if (String.IsNullOrWhiteSpace(location)) return;
            SetStatus("Chargement du guide des programmes…");
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = PlaylistLoader.DownloadEpg(location); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    SetStatus("Guide EPG indisponible.");
                    ShowError("Impossible de charger le guide.\n\n" + e.Error.GetBaseException().Message);
                    return;
                }
                var guide = e.Result as List<EpgProgram>;
                if (guide == null) return;
                loadedEpg.Clear();
                loadedEpg.AddRange(guide);
                RebuildEpgIndex();
                if (saveToSource && activeSource != null)
                {
                    activeSource.EpgUrl = location;
                    SaveSources();
                }
                ShowEpg();
                ShowToast(TextUtil.HumanCount(guide.Count) + " programmes EPG importés.", false, true);
            };
            worker.RunWorkerAsync();
        }

        CheckBox SettingToggle(string label, bool value, Action<bool> changed)
        {
            var check = new CheckBox
            {
                Content = Text(label, 12, Brush(TextPrimary), false), IsChecked = value,
                Foreground = Brush(TextPrimary), Template = ModernCheckBoxTemplate(), Margin = new Thickness(0, 6, 0, 6),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            check.Checked += delegate { changed(true); };
            check.Unchecked += delegate { changed(false); };
            return check;
        }

        void ShowGuide()
        {
            page = "Guide rapide";
            RebuildSidebar();
            var root = PageRoot("Guide rapide", "Tout ce qu'il faut savoir pour profiter de LyaranTV.");
            var start = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            start.Children.Add(Button(Glyphs.Add, "Ajouter une playlist", AddPlaylist, true));
            start.Children.Add(Button(Glyphs.Play, "Essayer la démo", LoadDemo, false));
            start.Children.Add(Button(Glyphs.Guide, "Ouvrir le guide EPG", delegate { Nav("Guide EPG"); }, false));
            root.Children.Add(start);

            string[][] sections =
            {
                new[] { "1 · Ajoutez votre source", "M3U/M3U8 (fichier ou URL), Xtream Codes (serveur + identifiants) ou portail MAC/Stalker. Le bouton « Tester » vérifie la connexion avant l'enregistrement." },
                new[] { "2 · Organisez votre bibliothèque", "Utilisez les catégories, la recherche et les filtres. L'étoile ajoute un favori, le menu contextuel permet de masquer une chaîne ou une catégorie." },
                new[] { "3 · Regardez confortablement", "Le lecteur intégré gère la pause, le volume, la reprise, les pistes audio et les sous-titres, en fenêtre comme en plein écran. Il utilise VLC lorsqu'il est installé (son AC-3, DTS et TrueHD garanti), sinon le moteur Windows." },
                new[] { "4 · Activez le guide", "Renseignez une URL XMLTV ou importez un fichier .xml/.xml.gz dans le Guide EPG. Les programmes apparaissent ensuite dans la bibliothèque et le lecteur." },
                new[] { "5 · Sauvegardez", "Paramètres → Données et sauvegarde. La sauvegarde contient les playlists, favoris, masquages, positions et réglages." }
            };
            foreach (string[] section in sections)
            {
                var stack = new StackPanel();
                stack.Children.Add(Text(section[0], 14, Brush(TextPrimary), true));
                var body = Text(section[1], 11.5, Brush(Muted), false);
                body.Margin = new Thickness(0, 5, 0, 0);
                stack.Children.Add(body);
                root.Children.Add(Card(stack, 15));
            }

            root.Children.Add(Text("RACCOURCIS CLAVIER", 10, Brush(Muted), true));
            var shortcuts = new Grid { Margin = new Thickness(0, 8, 0, 10) };
            shortcuts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            shortcuts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            string[][] keys =
            {
                new[] { "Espace", "Lire / mettre en pause" }, new[] { "← / →", "Reculer / avancer de 5 s" },
                new[] { "↑ / ↓", "Volume + / −" }, new[] { "M", "Couper le son" },
                new[] { "S", "Menu des sous-titres" }, new[] { "A", "Menu des pistes audio" },
                new[] { "Page préc. / suiv.", "Contenu précédent / suivant" }, new[] { "F11", "Plein écran" },
                new[] { "Ctrl+F", "Rechercher" }, new[] { "Ctrl+R", "Actualiser la playlist" },
                new[] { "Ctrl+N", "Ajouter une playlist" }, new[] { "Ctrl+D", "Favori" },
                new[] { "Échap", "Quitter le lecteur" }, new[] { "Entrée", "Lancer la recherche" }
            };
            for (int i = 0; i < keys.Length; i++)
            {
                var item = new Grid { Margin = new Thickness(0, 3, 12, 3) };
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var key = Badge(keys[i][0], Brush(Raised), Brush(Accent));
                key.HorizontalAlignment = HorizontalAlignment.Left;
                item.Children.Add(key);
                var label = Text(keys[i][1], 11, Brush(Muted), false);
                Grid.SetColumn(label, 1);
                item.Children.Add(label);
                Grid.SetColumn(item, i % 2);
                Grid.SetRow(item, i / 2);
                shortcuts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                shortcuts.Children.Add(item);
            }
            root.Children.Add(Card(shortcuts, 14));

            var help = new StackPanel();
            help.Children.Add(Text("Un problème de lecture ?", 14, Brush(TextPrimary), true));
            help.Children.Add(Text("Deux moteurs sont disponibles : libVLC (recommandé, installé avec VLC) décode l'AC-3, l'E-AC-3, le DTS et le TrueHD, que Windows laisse muets, et permet de changer de piste audio à la volée. Sans VLC, l'application utilise Windows Media Foundation, plus limité sur les MKV et les codecs audio ci-dessus. Le choix se fait dans Paramètres → Lecture → Moteur de lecture ; le lecteur bascule seul sur l'autre moteur si le premier refuse un flux. Le journal du dossier de données reste le meilleur point de départ en cas de problème.", 11.5, Brush(Muted), false));
            var logButton = Button(Glyphs.Info, "Ouvrir le journal", OpenLogFile, false);
            logButton.Margin = new Thickness(0, 10, 0, 0);
            help.Children.Add(logButton);
            root.Children.Add(Card(help, 15));
            SetContent(root);
        }

        void OpenLogFile()
        {
            try
            {
                if (!File.Exists(AppPaths.LogFile)) { ShowToast("Aucun journal d'erreur pour le moment.", false, false); return; }
                Process.Start(new ProcessStartInfo("notepad.exe", "\"" + AppPaths.LogFile + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { ShowError("Impossible d'ouvrir le journal : " + ex.Message); }
        }

        void ShowLocalMedia()
        {
            page = "Médias locaux";
            RebuildSidebar();
            var root = PageRoot("Médias locaux", "Lisez une vidéo ou un fichier audio depuis votre ordinateur, sans playlist.");
            var actions = new WrapPanel();
            actions.Children.Add(Button(Glyphs.OpenFile, "Ouvrir un média", ChooseLocalMedia, true));
            actions.Children.Add(Button(Glyphs.Folder, "Ouvrir le dossier", delegate { OpenDataFolder(); }, false));
            root.Children.Add(actions);

            var drop = new Border
            {
                Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14),
                Padding = new Thickness(24), Margin = new Thickness(0, 8, 0, 14), AllowDrop = true, MinHeight = 100
            };
            var dropStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var dropIcon = Icon(Glyphs.Upload, 28, Brush(Accent));
            dropIcon.HorizontalAlignment = HorizontalAlignment.Center;
            dropStack.Children.Add(dropIcon);
            var dropText = Text("Glissez-déposez une vidéo ou un fichier audio ici", 13, Brush(TextPrimary), true);
            dropText.HorizontalAlignment = HorizontalAlignment.Center;
            dropStack.Children.Add(dropText);
            var dropHint = Text("Vidéo : MP4, MKV, AVI, WMV, MOV, MPEG, TS, WEBM…  ·  Audio : MP3, FLAC, WAV, M4A, AAC, OGG, OPUS, WMA, MKA, AC-3, DTS…", 10.5, Brush(Muted), false);
            dropHint.HorizontalAlignment = HorizontalAlignment.Center;
            dropStack.Children.Add(dropHint);
            drop.Child = dropStack;
            drop.DragOver += delegate(object sender, DragEventArgs e)
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };
            drop.Drop += delegate(object sender, DragEventArgs e)
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null) return;
                var playable = files.Where(f => File.Exists(f) && TextUtil.IsMediaFile(f)).ToList();
                if (playable.Count == 0) { ShowToast("Aucun fichier vidéo ou audio reconnu.", true, false); return; }
                PlayLocalFile(playable[0]);
            };
            root.Children.Add(drop);

            if (recentLocal.Count == 0)
            {
                root.Children.Add(EmptyState(Glyphs.Local, "Aucun média récent", "Les vidéos et fichiers audio ouverts apparaîtront ici pour être relancés en un clic.", null));
            }
            else
            {
                root.Children.Add(SectionHeader("Médias récents", "Relancez une vidéo ou un fichier audio local.", Button(Glyphs.Delete, "Tout effacer", delegate
                {
                    recentLocal.Clear();
                    StateStore.WriteList("local-files.txt", recentLocal);
                    ShowLocalMedia();
                    ShowToast("Liste des médias locaux effacée.", false, true);
                }, false)));
                foreach (string path in recentLocal.Take(40))
                {
                    bool exists = File.Exists(path);
                    var row = new Grid { Height = 56 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var icon = new Border
                    {
                        Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Background = Brush(Raised),
                        Child = Icon(exists ? Glyphs.Play : Glyphs.Warning, 15, Brush(exists ? Accent : Danger)), VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(icon, 0);
                    row.Children.Add(icon);
                    var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
                    labels.Children.Add(Text(Path.GetFileName(path), 12.5, Brush(TextPrimary), true));
                    labels.Children.Add(Text(exists ? path : "Fichier introuvable · " + path, 9.5, Brush(Muted), false));
                    Grid.SetColumn(labels, 1);
                    row.Children.Add(labels);
                    var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    var play = Button(Glyphs.Play, "Lire", delegate { PlayLocalFile(path); }, true);
                    play.IsEnabled = exists;
                    play.Margin = new Thickness(0, 0, 5, 0);
                    buttons.Children.Add(play);
                    var remove = IconButton(Glyphs.Close, "Retirer de la liste", delegate
                    {
                        recentLocal.RemoveAll(p => String.Equals(p, path, StringComparison.OrdinalIgnoreCase));
                        StateStore.WriteList("local-files.txt", recentLocal);
                        ShowLocalMedia();
                    }, false);
                    buttons.Children.Add(remove);
                    Grid.SetColumn(buttons, 2);
                    row.Children.Add(buttons);
                    root.Children.Add(Card(row, 10));
                }
            }
            SetContent(root);
        }

        void ChooseLocalMedia()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Médias (vidéo et audio)|*.mp4;*.mkv;*.avi;*.wmv;*.mov;*.m4v;*.mpg;*.mpeg;*.ts;*.m2ts;*.webm;*.flv;*.ogv;*.3gp;*.mp3;*.flac;*.wav;*.m4a;*.m4b;*.aac;*.ogg;*.oga;*.opus;*.wma;*.mka;*.ape;*.aiff;*.aif;*.alac;*.ac3;*.eac3;*.ec3;*.dts;*.dtshd;*.truehd;*.mlp;*.wv;*.tta;*.mpc;*.dsf;*.dff;*.caf;*.amr|Vidéos|*.mp4;*.mkv;*.avi;*.wmv;*.mov;*.m4v;*.mpg;*.mpeg;*.ts;*.m2ts;*.webm;*.flv;*.ogv;*.3gp|Fichiers audio|*.mp3;*.flac;*.wav;*.m4a;*.m4b;*.aac;*.ogg;*.oga;*.opus;*.wma;*.mka;*.ape;*.aiff;*.aif;*.alac;*.ac3;*.eac3;*.ec3;*.dts;*.dtshd;*.truehd;*.mlp;*.wv;*.tta;*.mpc;*.dsf;*.dff;*.caf;*.amr|Tous les fichiers (*.*)|*.*",
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;
            PlayLocalFile(dialog.FileName);
        }

        void PlayLocalFile(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) { ShowError("Fichier introuvable : " + path); return; }
            var channel = new Channel
            {
                Name = Path.GetFileName(path), Group = "Médias locaux", Kind = "Local", Url = path,
                Id = "local:" + path, Description = path, Order = 0
            };
            AddLocalRecent(path);
            Play(channel, null, 0, false);
        }
    }
}
