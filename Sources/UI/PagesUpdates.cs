using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LyaranTV
{
    /// <summary>
    /// Mises à jour et informations légales. Le contrôle se fait sur GitHub
    /// Releases ; le fichier téléchargé n'est exécuté qu'après vérification de
    /// son empreinte SHA-256.
    /// </summary>
    public sealed partial class MainWindow
    {
        // ------------------------------------------------------------ vérification

        internal void CheckForUpdates(bool silent)
        {
            if (!silent) SetStatus(I18n.T("Recherche en cours…"));
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = Updates.Check(settings); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    string message = e.Error.GetBaseException().Message;
                    SetStatus(I18n.T("Impossible de vérifier les mises à jour."));
                    if (!silent) ShowError(I18n.T("Impossible de vérifier les mises à jour.") + "\n\n" + message);
                    return;
                }

                var info = e.Result as UpdateInfo;
                settings.LastUpdateCheck = DateTime.Now.ToString("s");
                SaveSettings();

                if (info == null || !info.Ok)
                {
                    if (!silent) SetStatus(I18n.T("Impossible de vérifier les mises à jour."));
                    if (!silent) ShowError(info == null ? I18n.T("Une erreur est survenue.") : I18n.T(info.Error));
                    return;
                }

                if (!info.HasUpdate)
                {
                    if (!silent) SetStatus(I18n.T("Vous utilisez la dernière version."));
                    if (!silent) ShowToast(I18n.T("Vous utilisez la dernière version.") + " · " + AppInfo.Version, false, true);
                    return;
                }

                SetStatus(I18n.T("Mise à jour disponible") + " · " + info.VersionText);
                ShowUpdateDialog(info);
            };
            worker.RunWorkerAsync();
        }

        // ------------------------------------------------------------ dialogue

        void ShowUpdateDialog(UpdateInfo info)
        {
            var window = new Window
            {
                Title = I18n.T("Mises à jour"),
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 680,
                Height = 620,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                FontFamily = new FontFamily("Inter, Segoe UI")
            };
            var frame = new Border
            {
                Margin = new Thickness(2),
                Background = Brush(Panel),
                BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(26, 22, 26, 22),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 34, ShadowDepth = 10, Opacity = 0.38, Color = Colors.Black
                }
            };
            window.MouseLeftButtonDown += delegate { try { window.DragMove(); } catch { } };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // En-tête : version, taille et fermeture discrète.
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 16) };
            var close = IconButton(Glyphs.Close, I18n.T("Fermer"), delegate { window.Close(); }, false);
            close.Width = 34; close.Height = 34; close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);

            var head = new StackPanel();
            var eyebrow = Text(I18n.T("MISE À JOUR DISPONIBLE"), 12.5, Brush(Accent), true);
            eyebrow.Margin = new Thickness(0, 0, 0, 5);
            head.Children.Add(eyebrow);
            var title = Text(AppInfo.Name + " " + info.VersionText, 27, Brush(TextPrimary), true);
            head.Children.Add(title);
            string details = I18n.T("Version installée") + " " + AppInfo.Version
                + "  ·  " + I18n.T("Nouvelle version") + " " + info.VersionText;
            if (info.Size > 0) details += "  ·  " + FormatSize(info.Size);
            var subtitle = Text(details, 12.5, Brush(Muted), false);
            subtitle.Margin = new Thickness(0, 4, 0, 0);
            head.Children.Add(subtitle);
            header.Children.Add(head);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // Notes de la seule version publiée, mises en forme plutôt qu'un
            // pavé de texte brut : titres, puces et gras ressortent.
            var notesHost = new Border
            {
                Background = Brush(WithAlpha(TextPrimary, 7)),
                BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 15, 18, 15)
            };
            var notesScroll = new ScrollViewer
            {
                Content = BuildReleaseNotesPanel(info.Notes, info.VersionText),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            notesHost.Child = notesScroll;
            Grid.SetRow(notesHost, 1);
            root.Children.Add(notesHost);

            var footer = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
            var progress = StyledProgressBar();
            progress.Width = 210;
            progress.Margin = new Thickness(0, 0, 12, 0);
            progress.Visibility = Visibility.Collapsed;
            progress.VerticalAlignment = VerticalAlignment.Center;

            var actionRow = new DockPanel { LastChildFill = false };
            var hint = Text(I18n.T("L’installation vérifie l’empreinte SHA-256 et conserve vos données."), 12.5, Brush(Muted), false);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.MaxWidth = 330;
            hint.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(hint, Dock.Left);
            actionRow.Children.Add(hint);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(progress);
            Button install = null;
            var later = Button(I18n.T("Plus tard"), delegate { window.Close(); }, false);
            later.Margin = new Thickness(0, 0, 8, 0);
            install = Button(I18n.T("Télécharger et installer"), delegate
            {
                install.IsEnabled = false;
                later.IsEnabled = false;
                progress.Visibility = Visibility.Visible;
                progress.IsIndeterminate = true;
                SetStatus(I18n.T("Téléchargement…"));

                var worker = new BackgroundWorker();
                worker.DoWork += delegate(object sender, DoWorkEventArgs e)
                {
                    e.Result = Updates.Download(info, settings, delegate(long received, long total)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            progress.IsIndeterminate = false;
                            progress.Maximum = 100;
                            progress.Value = total > 0 ? (received * 100.0 / total) : 0;
                        }));
                    });
                };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    if (e.Error != null)
                    {
                        install.IsEnabled = true;
                        later.IsEnabled = true;
                        progress.Visibility = Visibility.Collapsed;
                        SetStatus(I18n.T("Impossible de vérifier les mises à jour."));
                        ShowError(e.Error.GetBaseException().Message);
                        return;
                    }
                    string path = Convert.ToString(e.Result);
                    SetStatus(I18n.T("Redémarrer pour terminer"));
                    try
                    {
                        window.Close();
                        // Le setup prend le relais : il attend la fermeture du
                        // programme, remplace les fichiers et relance.
                        Updates.Install(path, true);
                        appClosing = true;
                        Application.Current.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        install.IsEnabled = true;
                        later.IsEnabled = true;
                        ShowError(ex.GetBaseException().Message);
                    }
                };
                worker.RunWorkerAsync();
            }, true);
            actions.Children.Add(later);
            actions.Children.Add(install);
            DockPanel.SetDock(actions, Dock.Right);
            actionRow.Children.Add(actions);
            footer.Children.Add(actionRow);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            window.Content = frame;
            frame.Child = root;
            window.ShowDialog();
        }

        /// <summary>
        /// Rend les notes de version en Markdown léger : titres, puces et gras.
        /// Le corps complet de la Release est réduit à la section publiée.
        /// </summary>
        FrameworkElement BuildReleaseNotesPanel(string notes, string version)
        {
            var panel = new StackPanel();
            string body = UpdateNotes.Latest(notes, version);
            if (String.IsNullOrWhiteSpace(body))
            {
                panel.Children.Add(Text(I18n.T("Aucune note de version n’est fournie pour cette Release."), 13, Brush(Muted), false));
                return panel;
            }

            bool skippedTitle = false;
            foreach (string raw in body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    panel.Children.Add(new Border { Height = 5 });
                    continue;
                }
                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    var sub = MarkdownLine(line.Substring(4), 13.5, Brush(TextPrimary), true);
                    sub.Margin = new Thickness(0, 10, 0, 4);
                    panel.Children.Add(sub);
                    continue;
                }
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    var section = Text(line.Substring(3), 14.5, Brush(Accent), true);
                    section.Margin = new Thickness(0, 14, 0, 5);
                    panel.Children.Add(section);
                    continue;
                }
                if (line.StartsWith("# ", StringComparison.Ordinal))
                {
                    // Le titre de la version est déjà dans l'en-tête de la fenêtre.
                    if (!skippedTitle) { skippedTitle = true; continue; }
                    var heading = Text(line.Substring(2), 17, Brush(TextPrimary), true);
                    heading.Margin = new Thickness(0, 8, 0, 6);
                    panel.Children.Add(heading);
                    continue;
                }
                if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
                {
                    panel.Children.Add(BuildReleaseBullet(line.Substring(2)));
                    continue;
                }
                var paragraph = MarkdownLine(line, 13, Brush(TextPrimary), false);
                paragraph.Margin = new Thickness(0, 2, 0, 3);
                panel.Children.Add(paragraph);
            }
            return panel;
        }

        FrameworkElement BuildReleaseBullet(string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var dot = new Border
            {
                Width = 5, Height = 5, CornerRadius = new CornerRadius(3), Background = Brush(Accent),
                VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 7, 0, 0)
            };
            Grid.SetColumn(dot, 0);
            grid.Children.Add(dot);
            var text = MarkdownLine(value, 13, Brush(TextPrimary), false);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            return grid;
        }

        TextBlock MarkdownLine(string value, double size, Brush color, bool bold)
        {
            var block = Text("", size, color, bold);
            block.Inlines.Clear();
            string text = value ?? "";
            // Les liens Markdown gardent leur libellé, sans URL brute.
            text = Regex.Replace(text, @"\[([^\]]+)\]\([^\)]+\)", "$1");
            text = text.Replace("`", "");
            string[] parts = text.Split(new[] { "**" }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                var run = new System.Windows.Documents.Run(parts[i]);
                if (bold || i % 2 == 1) run.FontWeight = FontWeights.SemiBold;
                block.Inlines.Add(run);
            }
            return block;
        }


        static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L) return (bytes / (1024.0 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " Mo";
            if (bytes >= 1024L) return (bytes / 1024.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " Ko";
            return bytes + " o";
        }

        // ------------------------------------------------------------ jeton

        void OpenUpdateTokenDialog()
        {
            var window = new Window
            {
                Title = I18n.T("Jeton GitHub"),
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 520,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                Background = Brush(Panel),
                ShowInTaskbar = false
            };

            var stack = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
            stack.Children.Add(Text(I18n.T("Jeton GitHub"), 17, Brush(TextPrimary), true));
            var hint = Text("Pour un dépôt public, aucun jeton n'est nécessaire. Si le dépôt reste privé, utilisez un jeton à granularité fine (github_pat_) autorisé pour " + AppInfo.Repository
                + " et accordez Contents : Read. Pour un jeton classique (ghp_), utilisez la portée repo. "
                + I18n.T("Le jeton reste chiffré sur cet ordinateur."), 11, Brush(Muted), false);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 6, 0, 12);
            stack.Children.Add(hint);

            var box = new PasswordBox
            {
                Height = 34, Padding = new Thickness(10, 0, 10, 0),
                Background = Brush(WithAlpha(TextPrimary, 8)), Foreground = Brush(TextPrimary),
                BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                PasswordChar = '•'
            };
            stack.Children.Add(box);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var cancel = Button(I18n.T("Annuler"), delegate { window.Close(); }, false);
            cancel.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(cancel);
            actions.Children.Add(Button(I18n.T("Enregistrer"), delegate
            {
                Updates.SetToken(settings, box.Password);
                SaveSettings();
                window.Close();
                ShowToast(I18n.T("Enregistrer") + " · " + I18n.T("Jeton GitHub"), false, true);
            }, true));
            stack.Children.Add(actions);

            window.Content = stack;
            window.ShowDialog();
        }

        // ------------------------------------------------------------ à propos

        void OpenAboutDialog()
        {
            var window = new Window
            {
                Title = I18n.T("À propos"),
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 620,
                SizeToContent = SizeToContent.Height,
                MaxHeight = 720,
                ResizeMode = ResizeMode.NoResize,
                Background = Brush(Panel),
                ShowInTaskbar = false
            };

            var stack = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            var mark = new Image { Source = CreateLogoMark(), Width = 64, Height = 58, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 16, 0) };
            head.Children.Add(mark);
            var titles = new StackPanel();
            titles.Children.Add(Text(AppInfo.Name + " " + AppInfo.Version, 22, Brush(TextPrimary), true));
            titles.Children.Add(Text(AppInfo.Description, 11, Brush(Muted), false));
            head.Children.Add(titles);
            stack.Children.Add(head);

            stack.Children.Add(SectionLine(I18n.T("Version"), AppInfo.Version));
            stack.Children.Add(SectionLine(I18n.T("Éditeur"), AppInfo.Vendor));
            stack.Children.Add(SectionLine(I18n.T("Site web"), AppInfo.Website));

            var thanks = Text("Ce lecteur ne fournit aucun contenu : il se contente de lire les playlists et les fichiers que vous lui indiquez. "
                + "Les métadonnées de films et de séries proviennent de TMDb ; ce produit utilise l'API TMDb mais n'est ni approuvé ni certifié par TMDb. "
                + "La lecture repose sur libVLC 3.0 (VideoLAN), distribué sous licence LGPL 2.1 ; son code source est disponible sur videolan.org. "
                + "Les sous-titres peuvent être cherchés sur OpenSubtitles.", 10.5, Brush(Muted), false);
            thanks.TextWrapping = TextWrapping.Wrap;
            thanks.Margin = new Thickness(0, 14, 0, 0);
            stack.Children.Add(thanks);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            var log = Button(I18n.T("Exporter le journal"), ExportLog, false);
            log.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(log);
            actions.Children.Add(Button(I18n.T("Fermer"), delegate { window.Close(); }, true));
            stack.Children.Add(actions);

            window.Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            window.ShowDialog();
        }

        UIElement SectionLine(string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var key = Text(label, 11, Brush(Muted), false);
            var val = Text(value, 11, Brush(TextPrimary), false);
            val.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(key, 0); Grid.SetColumn(val, 1);
            grid.Children.Add(key); grid.Children.Add(val);
            return grid;
        }

        /// <summary>
        /// Copie le journal en masquant identifiants, mots de passe et jetons :
        /// un rapport de bogue ne doit jamais divulguer un accès au fournisseur.
        /// </summary>
        internal void ExportLog()
        {
            try
            {
                string source = AppPaths.File("error.log");
                string text = File.Exists(source) ? File.ReadAllText(source) : "";
                text = MaskSecrets(text);

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = I18n.T("Exporter le journal"),
                    FileName = "lyarantv-journal-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt",
                    Filter = "Texte (*.txt)|*.txt"
                };
                if (dialog.ShowDialog() != true) return;
                File.WriteAllText(dialog.FileName, text, Encoding.UTF8);
                ShowToast(I18n.T("Exporter le journal") + " · " + Path.GetFileName(dialog.FileName), false, true);
            }
            catch (Exception ex)
            {
                ShowError(ex.GetBaseException().Message);
            }
        }

        internal static string MaskSecrets(string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            text = Regex.Replace(text, @"(://[^:/\s]+:)[^@\s]+@", "$1•••@");
            text = Regex.Replace(text, @"(?i)(password|passwd|pwd|token|secret|api_key|apikey|username)=([^&\s""]+)", "$1=•••");
            text = Regex.Replace(text, @"(?i)bearer\s+[A-Za-z0-9._\-]+", "Bearer •••");
            text = Regex.Replace(text, @"\bgithub_pat_[A-Za-z0-9_]{20,}\b", "•••");
            text = Regex.Replace(text, @"\bgh[pousr]_[A-Za-z0-9]{20,}\b", "•••");
            return text;
        }
    }
}
