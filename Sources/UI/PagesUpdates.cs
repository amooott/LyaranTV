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
                    SetStatus(I18n.T("Impossible de vérifier les mises à jour."));
                    if (!silent) ShowError(info == null ? I18n.T("Une erreur est survenue.") : I18n.T(info.Error));
                    return;
                }

                if (!info.HasUpdate)
                {
                    SetStatus(I18n.T("Vous utilisez la dernière version."));
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
                Width = 620,
                Height = 520,
                Background = Brush(Panel),
                ShowInTaskbar = false
            };

            var root = new Grid { Margin = new Thickness(22, 18, 22, 18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel();
            header.Children.Add(Text(I18n.T("Mise à jour disponible"), 20, Brush(TextPrimary), true));
            header.Children.Add(Text(AppInfo.Name + " · " + I18n.T("Version installée") + " " + AppInfo.Version
                + " → " + I18n.T("Nouvelle version") + " " + info.VersionText, 12, Brush(Muted), false));
            if (info.Size > 0) header.Children.Add(Text(I18n.T("Télécharger") + " · " + FormatSize(info.Size), 10, Brush(Muted), false));
            Grid.SetRow(header, 0); root.Children.Add(header);

            var notes = new TextBox
            {
                Text = String.IsNullOrWhiteSpace(info.Notes) ? I18n.T("Notes de version") + "…" : info.Notes,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Background = Brush(WithAlpha(TextPrimary, 8)),
                Foreground = Brush(TextPrimary),
                BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 14, 0, 14),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontSize = 11.5
            };
            Grid.SetRow(notes, 1); root.Children.Add(notes);

            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var progress = new ProgressBar
            {
                Width = 200, Height = 6, Margin = new Thickness(0, 0, 12, 0),
                Foreground = Brush(Accent), Background = Brush(WithAlpha(TextPrimary, 12)),
                Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center
            };
            footer.Children.Add(progress);

            Button install = null;
            var later = Button(I18n.T("Fermer"), delegate { window.Close(); }, false);
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
            footer.Children.Add(later);
            footer.Children.Add(install);
            Grid.SetRow(footer, 2); root.Children.Add(footer);

            window.Content = root;
            window.ShowDialog();
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
            var hint = Text("Le dépôt est privé : la vérification des mises à jour demande un jeton personnel GitHub (portée « repo »). "
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
            text = Regex.Replace(text, @"\bgh[pousr]_[A-Za-z0-9]{20,}\b", "•••");
            return text;
        }
    }
}
