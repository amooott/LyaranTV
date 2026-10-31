using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LyaranTV
{
    internal sealed class PlaylistLoadingWindow : Window
    {
        readonly string[] labels;
        readonly TextBlock[] rows = new TextBlock[5];
        readonly bool[] skippedSteps = new bool[5];
        readonly TextBlock detail;
        readonly TextBlock percentLabel;
        readonly TextBlock etaLabel;
        readonly TextBlock elapsedLabel;
        readonly Border fill;
        readonly Border track;
        readonly Button cancel;
        readonly DispatcherTimer ticker;
        readonly DateTime startedAt = DateTime.UtcNow;
        readonly Color accent;
        double targetProgress;
        double shownProgress;
        double smoothedEta;
        bool hasEta;
        int activeStep;
        bool finished;

        static SolidColorBrush B(Color color) { return new SolidColorBrush(color); }

        static ControlTemplate ButtonTemplate()
        {
            var grid = new FrameworkElementFactory(typeof(Grid));
            var chrome = new FrameworkElementFactory(typeof(Border));
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            chrome.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            chrome.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            chrome.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            grid.AppendChild(chrome);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            content.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            grid.AppendChild(content);
            return new ControlTemplate(typeof(Button)) { VisualTree = grid };
        }

        public event EventHandler CancelRequested;

        public PlaylistLoadingWindow(string playlistName, Color background, Color panel, Color accent, Color muted, Color textPrimary)
            : this(playlistName, background, panel, accent, muted, textPrimary, null)
        {
        }

        /// <summary>
        /// Les étapes sont personnalisables : la restauration d'une copie locale
        /// décrit son propre déroulé au lieu de réutiliser celui du réseau.
        /// </summary>
        public PlaylistLoadingWindow(string playlistName, Color background, Color panel, Color accent, Color muted, Color textPrimary, string[] steps)
        {
            labels = steps ?? new[]
            {
                "Connexion au compte", "Catégories et catalogues", "Guide des programmes · EPG",
                "Préparation de la bibliothèque", "Chargement des affiches de films et séries"
            };
            this.accent = accent;
            Title = "Chargement · LyaranTV";
            Width = 548;
            Height = 500;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            FontFamily = new FontFamily("Segoe UI");
            ShowInTaskbar = false;

            var outer = new Border
            {
                Margin = new Thickness(2), CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1),
                BorderBrush = B(Color.FromArgb(70, 255, 255, 255)), Background = B(panel), Padding = new Thickness(28)
            };
            Content = outer;
            var root = new StackPanel();
            outer.Child = root;

            root.Children.Add(new Border
            {
                Height = 3, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 20),
                Background = new LinearGradientBrush(accent, Color.FromArgb(40, accent.R, accent.G, accent.B), 0)
            });

            var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            var logo = new Image
            {
                Source = MainWindow.CreateLogoMark(), Width = 46, Height = 42, Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center
            };
            brand.Children.Add(logo);
            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = steps != null ? "Restauration de votre bibliothèque" : "Préparation de votre playlist",
                FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = B(textPrimary)
            });
            titleStack.Children.Add(new TextBlock { Text = playlistName ?? "", FontSize = 12, Foreground = B(muted), Margin = new Thickness(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            brand.Children.Add(titleStack);
            root.Children.Add(brand);

            detail = new TextBlock { Text = steps != null ? "Lecture de la copie locale…" : "Connexion à la source…", FontSize = 12, Foreground = B(muted), Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap };
            root.Children.Add(detail);

            var progressLine = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            progressLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            progressLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            track = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = B(Color.FromRgb(34, 37, 47)), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            fill = new Border { Width = 0, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(3), Background = new LinearGradientBrush(accent, Color.FromArgb(190, 255, 255, 255), 0) };
            track.Child = fill;
            progressLine.Children.Add(track);
            percentLabel = new TextBlock { Text = "0 %", MinWidth = 44, TextAlignment = TextAlignment.Right, FontSize = 11, Foreground = B(textPrimary), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(percentLabel, 1);
            progressLine.Children.Add(percentLabel);
            root.Children.Add(progressLine);
            track.SizeChanged += delegate { UpdateBar(); };

            var timing = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
            etaLabel = new TextBlock { Text = "Estimation du temps restant…", FontSize = 11, Foreground = B(accent) };
            elapsedLabel = new TextBlock { Text = "Temps écoulé : 0 s", FontSize = 10.5, Foreground = B(muted), TextAlignment = TextAlignment.Right };
            DockPanel.SetDock(elapsedLabel, Dock.Right);
            timing.Children.Add(elapsedLabel);
            timing.Children.Add(etaLabel);
            root.Children.Add(timing);

            for (int i = 0; i < labels.Length; i++)
            {
                var row = new TextBlock { Text = "○   " + labels[i], FontSize = 13, Foreground = B(muted), Margin = new Thickness(2, 0, 0, 11), TextTrimming = TextTrimming.CharacterEllipsis };
                rows[i] = row;
                root.Children.Add(row);
            }

            var footer = new Border
            {
                Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(9),
                Background = B(Color.FromArgb(26, accent.R, accent.G, accent.B)),
                BorderBrush = B(Color.FromArgb(70, accent.R, accent.G, accent.B)), BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = steps != null
                        ? "La bibliothèque s'ouvre depuis la copie locale enregistrée à la dernière session ; seul le guide peut être retéléchargé ensuite."
                        : "Le guide EPG continue en arrière-plan : la bibliothèque s’ouvre dès que les catalogues sont prêts.",
                    FontSize = 10, Foreground = B(muted), TextWrapping = TextWrapping.Wrap
                }
            };
            root.Children.Add(footer);

            cancel = new Button
            {
                Content = "Annuler", Height = 34, Width = 96, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 13, 0, 0), Background = Brushes.Transparent, Foreground = B(muted),
                BorderBrush = B(Color.FromArgb(70, 255, 255, 255)), BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                FocusVisualStyle = null, Template = ButtonTemplate()
            };
            cancel.Click += delegate
            {
                if (finished) return;
                cancel.IsEnabled = false;
                cancel.Content = "Annulation…";
                if (CancelRequested != null) CancelRequested(this, EventArgs.Empty);
            };
            root.Children.Add(cancel);

            ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            ticker.Tick += delegate { Animate(); };
            ticker.Start();
            Closed += delegate { if (ticker != null) ticker.Stop(); };
            Loaded += delegate { UpdateBar(); UpdateEta(); };
        }

        public void Report(int stage, int value, string message, bool skipped)
        {
            if (finished) return;
            activeStep = Math.Max(0, Math.Min(labels.Length - 1, stage));
            targetProgress = Math.Max(0, Math.Min(100, value));
            skippedSteps[activeStep] = skipped;
            if (detail != null) detail.Text = message ?? "";
            if (percentLabel != null) percentLabel.Text = ((int)targetProgress).ToString() + " %";
            for (int i = 0; i < rows.Length; i++)
            {
                if (i < activeStep)
                {
                    rows[i].Text = (skippedSteps[i] ? "↻   " : "✓   ") + labels[i] + (skippedSteps[i] ? "  ·  en arrière-plan" : "  ·  terminé");
                    rows[i].Foreground = B(skippedSteps[i] ? Color.FromRgb(145, 152, 167) : Color.FromRgb(74, 222, 128));
                }
                else if (i == activeStep)
                {
                    rows[i].Text = (skipped ? "–   " : "●   ") + labels[i] + (skipped ? "  ·  ignoré" : "  ·  en cours");
                    rows[i].Foreground = B(skipped ? Color.FromRgb(145, 152, 167) : accent);
                }
                else
                {
                    rows[i].Text = "○   " + labels[i];
                    rows[i].Foreground = B(Color.FromRgb(123, 130, 145));
                }
            }
            if (ticker != null && !ticker.IsEnabled) ticker.Start();
            UpdateEta();
        }

        /// <summary>
        /// État de l'étape « Chargement des affiches » : en cours, puis terminé.
        /// Elle se met à jour après la fin du chargement principal, d'où ce
        /// point d'entrée distinct de <see cref="Report"/>.
        /// </summary>
        public void CoversReport(string message, bool done)
        {
            int index = labels.Length - 1;
            if (rows == null || index < 0 || index >= rows.Length || rows[index] == null) return;
            rows[index].Text = (done ? "✓   " : "●   ") + labels[index] + "  ·  "
                + (String.IsNullOrWhiteSpace(message) ? (done ? "terminé" : "en cours") : message);
            rows[index].Foreground = B(done ? Color.FromRgb(74, 222, 128) : accent);
        }

        void Animate()
        {
            double delta = targetProgress - shownProgress;
            if (Math.Abs(delta) < 0.08) shownProgress = targetProgress;
            else shownProgress += delta * 0.28;
            UpdateBar();
            UpdateEta();
            if (finished && Math.Abs(targetProgress - shownProgress) < 0.1 && ticker != null) ticker.Stop();
        }

        void UpdateEta()
        {
            double elapsed = Math.Max(0, (DateTime.UtcNow - startedAt).TotalSeconds);
            if (elapsedLabel != null) elapsedLabel.Text = "Temps écoulé : " + FormatElapsed(elapsed);
            if (etaLabel == null) return;
            if (finished) return;
            if (targetProgress < 4 || elapsed < 1.2)
            {
                etaLabel.Text = "Estimation du temps restant…";
                return;
            }
            // La progression est pondérée par étapes : au tout début, le pourcentage
            // est faible alors que la connexion est déjà bien avancée. Plancher à 30 %
            // pour éviter une estimation démesurée pendant les premières secondes.
            double raw = elapsed * (100 - targetProgress) / Math.Max(30, targetProgress);
            if (raw < 0) raw = 0;
            if (!hasEta) { smoothedEta = raw; hasEta = true; }
            else smoothedEta = smoothedEta * 0.72 + raw * 0.28;
            if (smoothedEta > 900) smoothedEta = 900;
            string eta = TextUtil.FormatEta(smoothedEta);
            etaLabel.Text = eta.Length == 0 ? "Bientôt prêt…" : eta + " restantes";
        }

        static string FormatElapsed(double seconds)
        {
            int value = (int)Math.Max(0, Math.Round(seconds));
            if (value < 60) return value + " s";
            return (value / 60) + " min " + (value % 60).ToString("00") + " s";
        }

        public void Complete(string message)
        {
            finished = true;
            targetProgress = 100;
            shownProgress = 100;
            if (detail != null) { detail.Text = message ?? "Bibliothèque prête."; detail.Foreground = B(Color.FromRgb(98, 199, 167)); }
            if (percentLabel != null) percentLabel.Text = "100 %";
            if (etaLabel != null) etaLabel.Text = "Prêt en " + FormatElapsed((DateTime.UtcNow - startedAt).TotalSeconds);
            for (int i = 0; i < rows.Length; i++)
            {
                if (skippedSteps[i])
                {
                    rows[i].Text = "↻   " + labels[i] + "  ·  en arrière-plan";
                    rows[i].Foreground = B(Color.FromRgb(145, 158, 177));
                }
                else
                {
                    rows[i].Text = "✓   " + labels[i] + "  ·  terminé";
                    rows[i].Foreground = B(Color.FromRgb(98, 199, 167));
                }
            }
            if (cancel != null) cancel.Visibility = Visibility.Collapsed;
            UpdateBar();
        }

        public void Fail(string message)
        {
            finished = true;
            if (detail != null) { detail.Text = message ?? "Le chargement a échoué."; detail.Foreground = B(Color.FromRgb(255, 145, 145)); }
            if (percentLabel != null) percentLabel.Text = "Échec";
            if (etaLabel != null) etaLabel.Text = "Échec après " + FormatElapsed((DateTime.UtcNow - startedAt).TotalSeconds);
            if (activeStep >= 0 && activeStep < rows.Length)
            {
                rows[activeStep].Text = "!   " + labels[activeStep] + "  ·  échec";
                rows[activeStep].Foreground = B(Color.FromRgb(255, 145, 145));
            }
            if (cancel != null) cancel.Visibility = Visibility.Collapsed;
            if (ticker != null) ticker.Stop();
        }

        void UpdateBar()
        {
            if (fill != null && track != null) fill.Width = Math.Max(0, track.ActualWidth * shownProgress / 100.0);
        }
    }

    public sealed partial class MainWindow
    {
        PlaylistSource ShowSourceDialog(PlaylistSource existing)
        {
            var dialog = new Window
            {
                Title = existing == null ? "Ajouter une playlist" : "Modifier la playlist",
                Width = 640, Height = 690, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                WindowStyle = WindowStyle.None, Background = Brushes.Transparent, AllowsTransparency = true,
                FontFamily = new FontFamily("Segoe UI"), ShowInTaskbar = false
            };
            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18), Padding = new Thickness(24)
            };
            dialog.Content = frame;
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            frame.Child = layout;

            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 14) };
            var close = IconButton(Glyphs.Close, "Fermer", delegate { dialog.DialogResult = false; }, false);
            close.Width = 32; close.Height = 30; close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            var heading = new StackPanel();
            heading.Children.Add(Text(existing == null ? "Ajouter une playlist" : "Modifier la playlist", 21, Brush(TextPrimary), true));
            heading.Children.Add(Text("M3U/M3U8, Xtream Codes, portail MAC ou démonstration.", 11.5, Brush(Muted), false));
            header.Children.Add(heading);
            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) };
            var form = new StackPanel();
            scroll.Content = form;
            Grid.SetRow(scroll, 1);
            layout.Children.Add(scroll);

            var type = new ComboBox { Height = 36, Style = ModernComboStyle(), Margin = new Thickness(0, 4, 0, 12) };
            type.Items.Add("M3U / M3U8 - fichier ou URL");
            type.Items.Add("Xtream Codes - serveur et identifiants");
            type.Items.Add("Portail MAC / Stalker");
            type.Items.Add("Démonstration LyaranTV");
            form.Children.Add(FieldLabel("TYPE DE SOURCE"));
            form.Children.Add(type);

            var name = StyledTextBox(); name.Height = 36; name.Margin = new Thickness(0, 4, 0, 12);
            form.Children.Add(FieldLabel("NOM DE LA PLAYLIST"));
            form.Children.Add(name);

            var m3uPanel = new StackPanel();
            form.Children.Add(m3uPanel);
            var source = StyledTextBox(); source.Height = 36; source.Margin = new Thickness(0, 4, 0, 4);
            source.ToolTip = "URL M3U/M3U8 ou chemin d'un fichier local";
            m3uPanel.Children.Add(FieldLabel("FICHIER OU URL M3U"));
            m3uPanel.Children.Add(source);
            var fileRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 12) };
            var pick = Button(Glyphs.OpenFile, "Choisir un fichier M3U", delegate
            {
                var ofd = new OpenFileDialog { Filter = "Playlists (*.m3u;*.m3u8)|*.m3u;*.m3u8|Tous les fichiers (*.*)|*.*" };
                if (ofd.ShowDialog(dialog) == true)
                {
                    source.Text = ofd.FileName;
                    if (String.IsNullOrWhiteSpace(name.Text)) name.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                }
            }, false);
            pick.Margin = new Thickness(0);
            fileRow.Children.Add(pick);
            m3uPanel.Children.Add(fileRow);
            var conversionHint = Text("", 10.5, Brush(Accent), false);
            conversionHint.Margin = new Thickness(0, 0, 0, 12);
            conversionHint.TextWrapping = TextWrapping.Wrap;
            conversionHint.Visibility = Visibility.Collapsed;
            m3uPanel.Children.Add(conversionHint);

            var xtreamPanel = new StackPanel { Visibility = Visibility.Collapsed };
            form.Children.Add(xtreamPanel);
            var server = StyledTextBox(); server.Height = 36; server.Margin = new Thickness(0, 4, 0, 12);
            xtreamPanel.Children.Add(FieldLabel("ADRESSE DU SERVEUR XTREAM"));
            xtreamPanel.Children.Add(server);
            var user = StyledTextBox(); user.Height = 36; user.Margin = new Thickness(0, 4, 0, 12);
            xtreamPanel.Children.Add(FieldLabel("NOM D'UTILISATEUR"));
            xtreamPanel.Children.Add(user);
            var pass = StyledPasswordBox(); pass.Height = 36; pass.Margin = new Thickness(0, 4, 0, 12);
            xtreamPanel.Children.Add(FieldLabel("MOT DE PASSE"));
            xtreamPanel.Children.Add(pass);

            var stalkerPanel = new StackPanel { Visibility = Visibility.Collapsed };
            form.Children.Add(stalkerPanel);
            var portal = StyledTextBox(); portal.Height = 36; portal.Margin = new Thickness(0, 4, 0, 12);
            stalkerPanel.Children.Add(FieldLabel("URL DU PORTAIL STALKER"));
            stalkerPanel.Children.Add(portal);
            var mac = StyledTextBox(); mac.Height = 36; mac.Margin = new Thickness(0, 4, 0, 12);
            stalkerPanel.Children.Add(FieldLabel("ADRESSE MAC"));
            stalkerPanel.Children.Add(mac);
            var device = StyledTextBox(); device.Height = 36; device.Margin = new Thickness(0, 4, 0, 12);
            stalkerPanel.Children.Add(FieldLabel("TYPE D'APPAREIL"));
            stalkerPanel.Children.Add(device);

            var demoPanel = new StackPanel { Visibility = Visibility.Collapsed };
            demoPanel.Children.Add(Card(Text("La démonstration inclut des chaînes, des films, des séries et un guide EPG générés localement. Elle est idéale pour découvrir LyaranTV sans compte IPTV.", 11.5, Brush(Muted), false), 12));
            form.Children.Add(demoPanel);

            var epg = StyledTextBox(); epg.Height = 36; epg.Margin = new Thickness(0, 4, 0, 12);
            form.Children.Add(FieldLabel("URL OU FICHIER EPG XMLTV (FACULTATIF)"));
            form.Children.Add(epg);

            var testRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 4) };
            var test = Button(Glyphs.Globe, "Tester la connexion", null, false);
            test.Margin = new Thickness(0);
            testRow.Children.Add(test);
            var status = Text("", 11, Brush(Muted), false);
            status.Margin = new Thickness(10, 0, 0, 0);
            status.TextTrimming = TextTrimming.CharacterEllipsis;
            testRow.Children.Add(status);
            form.Children.Add(testRow);

            var footer = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 14, 0, 0) };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = Button("Annuler", delegate { dialog.DialogResult = false; }, false);
            cancel.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(cancel);
            var save = Button(Glyphs.Save, existing == null ? "Ajouter et charger" : "Enregistrer", null, true);
            save.Margin = new Thickness(0);
            actions.Children.Add(save);
            footer.Children.Add(actions);
            Grid.SetRow(footer, 2);
            layout.Children.Add(footer);

            PlaylistSource result = null;

            Action updateVisibility = delegate
            {
                int index = type.SelectedIndex;
                m3uPanel.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
                xtreamPanel.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
                stalkerPanel.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
                demoPanel.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
                epg.Visibility = index == 3 ? Visibility.Collapsed : Visibility.Visible;
            };
            Action refreshConversionHint = delegate
            {
                if (type.SelectedIndex != 0)
                {
                    conversionHint.Visibility = Visibility.Collapsed;
                    return;
                }
                XtreamConversion conversion = PlaylistConverter.FromUrl(source.Text.Trim());
                if (conversion == null)
                {
                    conversionHint.Visibility = Visibility.Collapsed;
                    return;
                }
                conversionHint.Text = "Adresse Xtream détectée (" + conversion.Reason + ") · conversion automatique au chargement.";
                conversionHint.Visibility = Visibility.Visible;
            };
            source.TextChanged += delegate { refreshConversionHint(); };
            type.SelectionChanged += delegate { updateVisibility(); refreshConversionHint(); };

            if (existing != null)
            {
                name.Text = existing.Name ?? "";
                type.SelectedIndex = existing.IsDemo || existing.Type == "Démo" ? 3 : existing.Type == "Xtream" ? 1 : existing.Type == "Portail MAC" ? 2 : 0;
                source.Text = existing.FilePath ?? existing.Url ?? "";
                server.Text = existing.Url ?? "";
                user.Text = existing.Username ?? "";
                pass.Password = existing.Password ?? "";
                portal.Text = existing.Url ?? "";
                mac.Text = existing.MacAddress ?? "";
                device.Text = String.IsNullOrWhiteSpace(existing.DeviceType) ? "MAG250" : existing.DeviceType;
                epg.Text = existing.EpgUrl ?? "";
            }
            else
            {
                type.SelectedIndex = 0;
                device.Text = "MAG250";
            }
            updateVisibility();
            refreshConversionHint();

            Action<string, bool> setStatus = delegate(string message, bool error)
            {
                status.Text = message;
                status.Foreground = Brush(error ? Danger : Success);
            };

            Func<PlaylistSource> readModel = delegate
            {
                int index = type.SelectedIndex;
                string title = name.Text.Trim();
                if (title.Length == 0) title = index == 3 ? "Démo LyaranTV" : index == 1 ? "Xtream" : index == 2 ? "Portail MAC" : "Ma playlist";
                var model = new PlaylistSource { Name = title, EpgUrl = epg.Text.Trim() };
                if (index == 0)
                {
                    string location = source.Text.Trim();
                    if (location.Length == 0) { setStatus("Indiquez un fichier ou une URL M3U.", true); return null; }
                    if (File.Exists(location)) model.FilePath = location;
                    else if (TextUtil.IsHttp(location)) model.Url = location;
                    else { setStatus("Le fichier n'existe pas et l'URL n'est pas HTTP(S).", true); return null; }
                    model.Type = "M3U/M3U8";
                }
                else if (index == 1)
                {
                    string address = server.Text.Trim();
                    if (address.Length == 0) { setStatus("Indiquez l'adresse du serveur Xtream.", true); return null; }
                    if (!TextUtil.IsHttp(address)) { setStatus("L'adresse Xtream doit commencer par http:// ou https://.", true); return null; }
                    if (String.IsNullOrWhiteSpace(user.Text)) { setStatus("Indiquez le nom d'utilisateur Xtream.", true); return null; }
                    if (String.IsNullOrWhiteSpace(pass.Password)) { setStatus("Indiquez le mot de passe Xtream.", true); return null; }
                    model.Type = "Xtream"; model.Url = address; model.Username = user.Text.Trim(); model.Password = pass.Password;
                }
                else if (index == 2)
                {
                    string address = portal.Text.Trim();
                    if (address.Length == 0 || !TextUtil.IsHttp(address)) { setStatus("Indiquez une URL de portail HTTP(S).", true); return null; }
                    try { model.MacAddress = StalkerClient.NormalizeMac(mac.Text); }
                    catch (Exception ex) { setStatus(ex.Message, true); return null; }
                    model.Type = "Portail MAC"; model.Url = address; model.DeviceType = String.IsNullOrWhiteSpace(device.Text) ? "MAG250" : device.Text.Trim();
                }
                else
                {
                    model.Type = "Démo"; model.IsDemo = true; model.Name = title;
                }
                if (model.EpgUrl.Length > 0 && !TextUtil.IsHttp(model.EpgUrl) && !File.Exists(model.EpgUrl))
                {
                    setStatus("L'URL EPG doit être HTTP(S) ou un fichier XMLTV existant.", true);
                    return null;
                }
                return model;
            };

            test.Click += delegate
            {
                var model = readModel();
                if (model == null) return;
                test.IsEnabled = false;
                save.IsEnabled = false;
                setStatus("Test de la connexion…", false);
                status.Foreground = Brush(Muted);
                var worker = new BackgroundWorker();
                worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = PlaylistLoader.TestSource(model); };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    test.IsEnabled = true;
                    save.IsEnabled = true;
                    if (e.Error != null) setStatus(e.Error.GetBaseException().Message, true);
                    else setStatus((string)e.Result, false);
                };
                worker.RunWorkerAsync();
            };

            save.Click += delegate
            {
                var model = readModel();
                if (model == null) return;
                result = model;
                dialog.DialogResult = true;
            };

            bool? answer = dialog.ShowDialog();
            return answer == true ? result : null;
        }

        TextBlock FieldLabel(string text)
        {
            var label = Text(text, 9.5, Brush(Muted), true);
            label.Margin = new Thickness(0, 0, 0, 0);
            return label;
        }

        void ShowChannelInfo(Channel channel)
        {
            if (channel == null) return;
            var dialog = new Window
            {
                Title = "Fiche · " + channel.Name, Width = 700, SizeToContent = SizeToContent.Height, MaxHeight = 840,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None, Background = Brushes.Transparent, AllowsTransparency = true,
                FontFamily = new FontFamily("Inter, Segoe UI"), ShowInTaskbar = false
            };
            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20), Padding = new Thickness(22)
            };
            dialog.Content = frame;
            var root = new StackPanel();
            frame.Child = root;
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 14) };
            var close = IconButton(Glyphs.Close, "Fermer", delegate { dialog.DialogResult = false; }, false);
            close.Width = 32; close.Height = 30; close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            header.Children.Add(Text("Fiche du contenu", 20, Brush(TextPrimary), true));
            root.Children.Add(header);

            var contentBody = new StackPanel();
            var contentScroll = new ScrollViewer
            {
                Content = contentBody, MaxHeight = 610,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            root.Children.Add(contentScroll);

            // Un épisode n'a souvent qu'un aperçu 16:9 : il tient le rôle du fond.
            string preferredBackdrop = !String.IsNullOrWhiteSpace(channel.BackdropUrl) ? channel.BackdropUrl : channel.StillUrl;
            bool hasBackdrop = !String.IsNullOrWhiteSpace(preferredBackdrop);
            bool hasPoster = !String.IsNullOrWhiteSpace(channel.PosterUrl);
            var hero = new Grid { Height = 236, ClipToBounds = true };
            var heroBorder = new Border
            {
                CornerRadius = new CornerRadius(15), Background = Brush(Raised), ClipToBounds = true,
                BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1)
            };
            hero.Children.Add(heroBorder);
            if (hasBackdrop)
            {
                var backdrop = new Grid();
                backdrop.Children.Add(new Border { Background = new LinearGradientBrush(WithAlpha(Accent, 150), Color.FromArgb(255, 10, 10, 18), 0) });
                var fallbackIcon = Icon(channel.Kind == "Séries" ? Glyphs.Series : Glyphs.Movies, 54, Brush(WithAlpha(Accent, 120)));
                fallbackIcon.HorizontalAlignment = HorizontalAlignment.Center;
                fallbackIcon.VerticalAlignment = VerticalAlignment.Center;
                backdrop.Children.Add(fallbackIcon);
                backdrop.Children.Add(new AsyncImage { Url = preferredBackdrop, Stretch = Stretch.UniformToFill });
                heroBorder.Child = backdrop;
            }
            else if (hasPoster)
            {
                var backdrop = new Grid();
                backdrop.Children.Add(new Border { Background = new LinearGradientBrush(WithAlpha(Accent, 150), Color.FromArgb(255, 10, 10, 18), 0) });
                backdrop.Children.Add(new AsyncImage { Url = channel.PosterUrl, Stretch = Stretch.UniformToFill });
                heroBorder.Child = backdrop;
            }
            else heroBorder.Background = new LinearGradientBrush(WithAlpha(Accent, 170), Color.FromArgb(255, 10, 10, 18), 0);
            hero.Children.Add(new Border
            {
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(30, 0, 0, 0), 0),
                        new GradientStop(Color.FromArgb(235, 5, 5, 12), 1)
                    }, new Point(0.5, 0), new Point(0.5, 1))
            });

            var heroContent = new Grid { Margin = new Thickness(16), VerticalAlignment = VerticalAlignment.Bottom };
            heroContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            heroContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var poster = new Border
            {
                Width = 92, Height = 138, CornerRadius = new CornerRadius(10), Background = Brush(Raised), ClipToBounds = true,
                BorderBrush = Brush(Color.FromArgb(90, 255, 255, 255)), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Bottom,
                Child = BuildLogoContent(channel, 92)
            };
            Grid.SetColumn(poster, 0); heroContent.Children.Add(poster);
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(16, 0, 0, 2) };
            var title = Text(TextUtil.DisplayTitle(channel.Name), 22, Brushes.White, true);
            title.TextWrapping = TextWrapping.Wrap; title.MaxHeight = 64; title.TextTrimming = TextTrimming.CharacterEllipsis;
            labels.Children.Add(title);
            var metaParts = new List<string>();
            if (!String.IsNullOrWhiteSpace(channel.Year)) metaParts.Add(channel.Year);
            if (!String.IsNullOrWhiteSpace(channel.Group)) metaParts.Add(channel.Group);
            metaParts.Add(KindLabel(channel.Kind));
            if (channel.TmdbRating > 0) metaParts.Add("★ " + channel.TmdbRating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "/10");
            var meta = Text(String.Join("  ·  ", metaParts.ToArray()), 11, Brush(Color.FromArgb(230, 244, 244, 248)), false);
            meta.Margin = new Thickness(0, 5, 0, 0); meta.TextWrapping = TextWrapping.Wrap;
            labels.Children.Add(meta);
            if (!String.IsNullOrWhiteSpace(channel.Genres))
            {
                var genres = Text(channel.Genres, 10.5, Brush(Color.FromArgb(210, 156, 156, 176)), false);
                genres.Margin = new Thickness(0, 4, 0, 0); genres.TextWrapping = TextWrapping.Wrap;
                labels.Children.Add(genres);
            }
            Grid.SetColumn(labels, 1); heroContent.Children.Add(labels);
            hero.Children.Add(heroContent);
            contentBody.Children.Add(hero);

            string overview = String.IsNullOrWhiteSpace(channel.Overview) ? channel.Description : channel.Overview;
            if (!String.IsNullOrWhiteSpace(overview))
            {
                var description = Text(overview, 12, Brush(Muted), false);
                var scroll = new ScrollViewer { Content = description, MaxHeight = 156, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 12, 0, 0) };
                contentBody.Children.Add(Card(scroll, 14));
            }

            bool canLoadTmdbDetails = !String.IsNullOrWhiteSpace(channel.TmdbId) && TmdbClient.IsConfigured(settings);
            var detailBody = new StackPanel();
            var detailStatus = Text("", 10, Brush(Muted), false);
            var detailStack = new StackPanel();
            detailStack.Children.Add(Text("DÉTAILS", 9.5, Brush(Accent), true));
            detailStack.Children.Add(detailStatus);
            detailStack.Children.Add(detailBody);
            if (canLoadTmdbDetails || !String.IsNullOrWhiteSpace(channel.Cast) || !String.IsNullOrWhiteSpace(channel.Director))
                contentBody.Children.Add(Card(detailStack, 13));
            Action<TmdbResult> renderDetails = delegate(TmdbResult data)
            {
                detailBody.Children.Clear();
                if (data == null) return;
                if (!String.IsNullOrWhiteSpace(data.OriginalTitle)
                    && !String.Equals(TmdbTitleCleaner.CleanTitle(data.OriginalTitle), TmdbTitleCleaner.CleanTitle(channel.Name), StringComparison.OrdinalIgnoreCase))
                    AddMetadataLine(detailBody, "Titre original", data.OriginalTitle);
                AddMetadataLine(detailBody, "Durée", data.Runtime);
                AddMetadataLine(detailBody, "Réalisation / création", data.Director);
                AddMetadataLine(detailBody, "Pays", data.Countries);
                AddMetadataLine(detailBody, "Classification", data.Certification);
                AddMetadataLine(detailBody, "Distribution", data.Cast);
                if (!String.IsNullOrWhiteSpace(data.TrailerUrl))
                {
                    var trailer = Button(Glyphs.Play, "Voir la bande-annonce", delegate
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(data.TrailerUrl) { UseShellExecute = true }); }
                        catch { ShowError("Impossible d’ouvrir la bande-annonce."); }
                    }, false);
                    trailer.Margin = new Thickness(0, 6, 0, 0);
                    detailBody.Children.Add(trailer);
                }
                detailStatus.Text = String.IsNullOrWhiteSpace(data.TmdbId) ? "" : "Métadonnées fournies par TMDb · " + data.VoteCount.ToString("N0", CultureInfo.GetCultureInfo("fr-FR")) + " votes";
            };
            renderDetails(new TmdbResult
            {
                TmdbId = channel.TmdbId ?? "", MediaType = channel.Kind == "Séries" ? "tv" : "movie",
                Title = channel.Name ?? "", OriginalTitle = channel.OriginalTitle ?? "", Runtime = channel.Runtime ?? "",
                Certification = channel.Certification ?? "", TrailerUrl = channel.TrailerUrl ?? "", Cast = channel.Cast ?? "",
                Director = channel.Director ?? "", Countries = channel.Countries ?? "", VoteCount = channel.VoteCount
            });

            var now = CurrentProgram(channel);
            var next = NextProgram(channel);
            if (now != null || next != null)
            {
                var epgStack = new StackPanel();
                epgStack.Children.Add(Text("PROGRAMME", 9.5, Brush(Muted), true));
                if (now != null)
                {
                    epgStack.Children.Add(Text("À l'antenne · " + now.Title, 13, Brush(Accent), true));
                    epgStack.Children.Add(Text(now.TimeRange + "  ·  " + TextUtil.FormatDuration(now.Duration), 10.5, Brush(Muted), false));
                    if (!String.IsNullOrWhiteSpace(now.Description)) epgStack.Children.Add(Text(TextUtil.Ellipsis(now.Description, 300), 10.5, Brush(Muted), false));
                }
                if (next != null)
                {
                    var nextText = Text("À suivre · " + next.Title + "  ·  " + next.TimeRange, 11, Brush(TextPrimary), false);
                    nextText.Margin = new Thickness(0, 6, 0, 0);
                    epgStack.Children.Add(nextText);
                }
                contentBody.Children.Add(Card(epgStack, 13));
            }

            if (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode))
                contentBody.Children.Add(Card(Text("Saison " + channel.Season + "  ·  Épisode " + channel.Episode, 11, Brush(Muted), false), 12));

            if (!String.IsNullOrWhiteSpace(channel.TmdbId))
            {
                var credit = Text(TmdbClient.Attribution, 9.5, Brush(Muted), false);
                credit.Margin = new Thickness(0, 0, 0, 8); credit.TextWrapping = TextWrapping.Wrap;
                contentBody.Children.Add(credit);
            }

            var actions = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
            string label = channel.IsSeries || (channel.Kind == "Séries" && String.IsNullOrWhiteSpace(channel.Url)) ? "Voir les épisodes" : "Lire";
            var play = Button(Glyphs.Play, label, delegate { dialog.DialogResult = true; }, true);
            play.Margin = new Thickness(0, 0, 7, 7);
            actions.Children.Add(play);
            Button favorite = null;
            favorite = Button(favorites.Contains(channel.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite, favorites.Contains(channel.Id) ? "Favori" : "Ajouter aux favoris", delegate
            {
                ToggleFavorite(channel);
                favorite.Content = IconText(favorites.Contains(channel.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite, favorites.Contains(channel.Id) ? "Favori" : "Ajouter aux favoris", Brush(TextPrimary), 12.5);
            }, false);
            favorite.Margin = new Thickness(0, 0, 7, 7);
            actions.Children.Add(favorite);
            bool editTmdb = false;
            bool resetTmdb = false;
            if (channel.Kind == "Films" || channel.Kind == "Séries")
            {
                var match = Button(Glyphs.Search, "Corriger TMDb", delegate { editTmdb = true; dialog.DialogResult = false; }, false);
                match.Margin = new Thickness(0, 0, 7, 7);
                actions.Children.Add(match);
                if (!String.IsNullOrWhiteSpace(channel.TmdbId))
                {
                    var resetMatch = Button(Glyphs.Refresh, "Réinitialiser la correspondance", delegate { resetTmdb = true; dialog.DialogResult = false; }, false);
                    resetMatch.Margin = new Thickness(0, 0, 7, 7);
                    actions.Children.Add(resetMatch);
                }
            }
            if (!String.IsNullOrWhiteSpace(channel.Url))
            {
                var copy = Button(Glyphs.Copy, "Copier l'URL", delegate { CopyText(channel.Url); }, false);
                copy.Margin = new Thickness(0, 0, 7, 7);
                actions.Children.Add(copy);
                var external = Button(Glyphs.External, "Lecteur externe", delegate { OpenInExternalPlayer(channel.Url); }, false);
                external.Margin = new Thickness(0, 0, 7, 7);
                actions.Children.Add(external);
            }
            var closeButton = Button("Fermer", delegate { dialog.DialogResult = false; }, false);
            closeButton.Margin = new Thickness(0, 0, 0, 7);
            actions.Children.Add(closeButton);
            root.Children.Add(actions);

            BackgroundWorker detailWorker = null;
            if (canLoadTmdbDetails && !channel.TmdbDetailsLoaded)
            {
                detailStatus.Text = "Chargement des crédits et informations complémentaires…";
                var basis = new TmdbResult
                {
                    TmdbId = channel.TmdbId, MediaType = channel.Kind == "Séries" ? "tv" : "movie", Title = channel.Name,
                    OriginalTitle = channel.OriginalTitle, PosterUrl = channel.PosterUrl, BackdropUrl = channel.BackdropUrl,
                    Overview = channel.Overview, Genres = channel.Genres, Year = channel.Year, Rating = channel.TmdbRating, VoteCount = channel.VoteCount,
                    DetailsLoaded = channel.TmdbDetailsLoaded
                };
                detailWorker = new BackgroundWorker();
                detailWorker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = TmdbClient.LoadDetails(settings, basis); };
                detailWorker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    if (resetTmdb) return;
                    if (e.Error != null)
                    {
                        detailStatus.Text = "Les informations complémentaires ne sont pas disponibles pour le moment.";
                        return;
                    }
                    var detailed = e.Result as TmdbResult;
                    if (detailed == null) return;
                    TmdbClient.Apply(channel, detailed);
                    TmdbCacheStore.Put(TmdbCacheStore.CacheKey(channel), detailed);
                    TmdbCacheStore.Save();
                    renderDetails(detailed);
                };
                detailWorker.RunWorkerAsync();
            }
            bool? answer = dialog.ShowDialog();
            if (editTmdb) { ShowTmdbMatchDialog(channel); return; }
            if (resetTmdb)
            {
                TmdbMatchStore.Remove(channel);
                TmdbCacheStore.Remove(TmdbCacheStore.CacheKey(channel));
                channel.TmdbId = channel.PosterUrl = channel.BackdropUrl = channel.Overview = channel.Genres = "";
                channel.OriginalTitle = channel.Runtime = channel.Certification = channel.TrailerUrl = channel.Cast = channel.Director = channel.Countries = "";
                channel.TmdbRating = 0; channel.VoteCount = 0; channel.TmdbChecked = false; channel.TmdbDetailsLoaded = false;
                StartTmdbEnrichment();
                ShowToast("Correspondance réinitialisée. Nouvelle recherche lancée en arrière-plan.", false, true);
                return;
            }
            if (answer == true)
            {
                if (channel.IsSeries || (channel.Kind == "Séries" && String.IsNullOrWhiteSpace(channel.Url))) LoadSeriesEpisodes(channel);
                else Play(channel, null, GetSavedPosition(channel), false);
            }
        }

        void AddMetadataLine(StackPanel panel, string label, string value)
        {
            if (panel == null || String.IsNullOrWhiteSpace(value)) return;
            var line = Text(label + "  ·  " + value, 10.5, Brush(Muted), false);
            line.TextWrapping = TextWrapping.Wrap;
            line.Margin = new Thickness(0, 3, 0, 0);
            panel.Children.Add(line);
        }

        void ShowTmdbMatchDialog(Channel channel)
        {
            if (channel == null) return;
            if (!TmdbClient.IsConfigured(settings))
            {
                ShowError("Ajoutez une clé API TMDb (v3) dans Paramètres → Métadonnées et sous-titres pour corriger cette fiche.");
                return;
            }
            var dialog = new Window
            {
                Title = "Correspondance TMDb · " + channel.Name, Width = 780, Height = 720,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None, Background = Brushes.Transparent, AllowsTransparency = true,
                FontFamily = new FontFamily("Inter, Segoe UI"), ShowInTaskbar = false
            };
            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20), Padding = new Thickness(22)
            };
            dialog.Content = frame;
            var root = new Grid();
            frame.Child = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 12) };
            var close = IconButton(Glyphs.Close, "Fermer", delegate { dialog.DialogResult = false; }, false);
            close.Width = 32; close.Height = 30; close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var headerStack = new StackPanel();
            headerStack.Children.Add(Text("Corriger la correspondance TMDb", 20, Brush(TextPrimary), true));
            headerStack.Children.Add(Text("Choisissez la bonne fiche pour « " + channel.Name + " ». Votre choix est mémorisé dans le cache local.", 10.5, Brush(Muted), false));
            header.Children.Add(headerStack);
            Grid.SetRow(header, 0); root.Children.Add(header);

            var searchRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var searchBox = StyledTextBox(); searchBox.Text = TmdbTitleCleaner.CleanTitle(channel.Name);
            searchBox.ToolTip = "Titre à rechercher sur TMDb";
            searchRow.Children.Add(searchBox);
            var typeBox = new ComboBox { Width = 108, Height = 36, Margin = new Thickness(7, 0, 7, 0), Style = ModernComboStyle() };
            typeBox.Items.Add("Film"); typeBox.Items.Add("Série");
            typeBox.SelectedItem = channel.Kind == "Séries" || channel.IsSeries ? "Série" : "Film";
            Grid.SetColumn(typeBox, 1); searchRow.Children.Add(typeBox);
            var search = Button(Glyphs.Search, "Rechercher", null, true);
            search.Width = 128; Grid.SetColumn(search, 2); searchRow.Children.Add(search);
            Grid.SetRow(searchRow, 1); root.Children.Add(searchRow);

            var status = Text("", 11, Brush(Muted), false);
            status.Margin = new Thickness(0, 0, 0, 8);
            var results = new StackPanel();
            var scroll = new ScrollViewer { Content = results, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var body = new StackPanel();
            body.Children.Add(status); body.Children.Add(scroll);
            Grid.SetRow(body, 2); root.Children.Add(body);

            Action runSearch = null;
            runSearch = delegate
            {
                string query = searchBox.Text.Trim();
                if (query.Length == 0)
                {
                    status.Foreground = Brush(Danger);
                    status.Text = "Saisissez un titre à rechercher.";
                    return;
                }
                bool series = String.Equals(Convert.ToString(typeBox.SelectedItem), "Série", StringComparison.Ordinal);
                int searchYear = TmdbTitleCleaner.ExtractYear(channel.Name);
                if (searchYear == 0) Int32.TryParse(channel.Year, out searchYear);
                results.Children.Clear();
                status.Foreground = Brush(Accent);
                status.Text = "Recherche sur TMDb…";
                search.IsEnabled = false;
                var worker = new BackgroundWorker();
                worker.DoWork += delegate(object sender, DoWorkEventArgs e)
                {
                    // Une année erronée dans la playlist ne doit pas vider la
                    // liste : on élargit la recherche si la première passe ne
                    // renvoie rien.
                    var found = TmdbClient.Search(settings, query, searchYear, series, 12);
                    bool widened = false;
                    if (found.Count == 0 && searchYear > 0)
                    {
                        found = TmdbClient.Search(settings, query, 0, series, 12);
                        widened = true;
                    }
                    e.Result = new object[] { found, widened };
                };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    search.IsEnabled = true;
                    if (e.Error != null)
                    {
                        status.Foreground = Brush(Danger);
                        status.Text = "Recherche impossible.";
                        results.Children.Add(Card(Text("TMDb ne répond pas : " + e.Error.GetBaseException().Message, 11.5, Brush(Danger), false), 14));
                        return;
                    }
                    var payload = e.Result as object[];
                    var found = payload == null ? null : payload[0] as List<TmdbResult>;
                    bool widened = payload != null && payload.Length > 1 && payload[1] is bool && (bool)payload[1];
                    if (found == null || found.Count == 0)
                    {
                        status.Foreground = Brush(Muted);
                        status.Text = "Aucun résultat. Essayez un titre plus court, sans l’année, ou l’autre type de contenu.";
                        return;
                    }
                    status.Foreground = Brush(Success);
                    status.Text = found.Count + " résultat(s). Choisissez la fiche qui correspond."
                        + (widened ? " Recherche élargie : aucune fiche pour l’année indiquée." : "");
                    foreach (var result in found)
                    {
                        var captured = result;
                        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        var poster = new Border
                        {
                            Width = 56, Height = 84, CornerRadius = new CornerRadius(8), Background = Brush(Raised), ClipToBounds = true,
                            Child = String.IsNullOrWhiteSpace(captured.PosterUrl)
                                ? (UIElement)Icon(Glyphs.Movies, 22, Brush(Accent))
                                : new AsyncImage { Url = captured.PosterUrl, Stretch = Stretch.UniformToFill }
                        };
                        Grid.SetColumn(poster, 0); row.Children.Add(poster);
                        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(11, 0, 10, 0) };
                        labels.Children.Add(Text(captured.Title, 13, Brush(TextPrimary), true));
                        string meta = captured.MediaType == "tv" ? "Série" : "Film";
                        if (!String.IsNullOrWhiteSpace(captured.Year)) meta += "  ·  " + captured.Year;
                        if (captured.Rating > 0) meta += "  ·  ★ " + captured.Rating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (!String.IsNullOrWhiteSpace(captured.Genres)) meta += "  ·  " + captured.Genres;
                        int confidenceScore = TmdbClient.MatchScore(query, searchYear, captured);
                        meta += "  ·  Correspondance " + TmdbClient.MatchConfidence(confidenceScore) + " (" + confidenceScore + " %)";
                        if (searchYear > 0 && !String.IsNullOrWhiteSpace(captured.Year) && captured.Year != searchYear.ToString(System.Globalization.CultureInfo.InvariantCulture))
                            meta += "  ·  année différente";
                        var matchMeta = Text(meta, 10, confidenceScore < 76 ? Brush(Danger) : Brush(Muted), false);
                        matchMeta.TextWrapping = TextWrapping.Wrap;
                        labels.Children.Add(matchMeta);
                        if (!String.IsNullOrWhiteSpace(captured.Overview))
                        {
                            var overview = Text(TextUtil.Ellipsis(captured.Overview, 170), 10, Brush(Muted), false);
                            overview.Margin = new Thickness(0, 3, 0, 0); labels.Children.Add(overview);
                        }
                        Grid.SetColumn(labels, 1); row.Children.Add(labels);
                        var choose = Button(Glyphs.Check, "Choisir", delegate
                        {
                            TmdbClient.ApplyOverride(channel, captured);
                            dialog.DialogResult = true;
                        }, false);
                        choose.VerticalAlignment = VerticalAlignment.Center;
                        Grid.SetColumn(choose, 2); row.Children.Add(choose);
                        results.Children.Add(Card(row, 9));
                    }
                };
                worker.RunWorkerAsync();
            };
            search.Click += delegate { runSearch(); };
            searchBox.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { runSearch(); e.Handled = true; } };
            // La recherche démarre seule : le titre est déjà rempli, l'utilisateur
            // n'a qu'à choisir la bonne fiche au lieu de deviner le bouton.
            dialog.Loaded += delegate { searchBox.Focus(); searchBox.SelectAll(); runSearch(); };
            bool? answer = dialog.ShowDialog();
            if (answer == true)
            {
                ShowToast("Correspondance TMDb enregistrée pour « " + channel.Name + " ».", false, true);
                ScheduleTmdbRefresh();
                ShowChannelInfo(channel);
            }
        }
    }
}
