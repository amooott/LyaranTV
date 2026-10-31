using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
        void ShowSettings()
        {
            page = "Paramètres";
            RebuildSidebar();
            UpdateStatusBar();
            var root = PageRoot("Paramètres", "Apparence, lecture, profils, services connectés et données LyaranTV.");

            root.Children.Add(SettingsCard("Apparence", "Un thème, une couleur d’accent et des animations discrètes.",
                BuildAppearanceSettings()));
            root.Children.Add(SettingsCard("Lecture", "Comportement du lecteur intégré et des médias.",
                BuildPlaybackSettings()));
            root.Children.Add(SettingsCard("Profils et contrôle parental", "Des espaces personnels et des catégories protégées pour les enfants.",
                BuildProfileSettings()));
            root.Children.Add(SettingsCard("Métadonnées et sous-titres", "Affiches, notes et synopsis (TMDb), recherche de sous-titres (OpenSubtitles). Facultatif.",
                BuildTmdbSettings()));
            root.Children.Add(SettingsCard("API Letterboxd", "Un nom d’utilisateur suffit pour vos avis publics récents; vous pouvez importer l’historique.",
                BuildLetterboxdApiSettings()));
            root.Children.Add(SettingsCard("Démarrage", "Ce que LyaranTV ouvre au lancement.",
                BuildStartupSettings()));
            root.Children.Add(SettingsCard("Lecteur externe", "Une solution de repli pour les flux et codecs non pris en charge par Windows.",
                BuildExternalPlayerSettings()));
            root.Children.Add(SettingsCard("Données et sauvegarde", "Exportez votre configuration ou créez une sauvegarde complète.",
                BuildDataSettings()));
            root.Children.Add(SettingsCard(I18n.T("Mises à jour"), "Une version récente, vérifiée par empreinte SHA-256 avant installation.",
                BuildUpdateSettings()));
            root.Children.Add(BuildAboutSettings());

            SetContent(root);
            SetStatus("Paramètres de LyaranTV · profil « " + (activeProfile == null ? "Principal" : activeProfile.Name) + " ».");
        }

        Border SettingsCard(string title, string description, UIElement body)
        {
            var stack = new StackPanel();
            stack.Children.Add(Text(title, 16, Brush(TextPrimary), true));
            if (!String.IsNullOrWhiteSpace(description))
            {
                var subtitle = Text(description, 11, Brush(Muted), false);
                subtitle.Margin = new Thickness(0, 3, 0, 0);
                stack.Children.Add(subtitle);
            }
            if (body != null)
            {
                body.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 12, 0, 0));
                stack.Children.Add(body);
            }
            return Card(stack, 17);
        }

        Grid SettingsRow(string label, string hint, UIElement control)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(Text(label, 12.5, Brush(TextPrimary), false));
            if (!String.IsNullOrWhiteSpace(hint)) labels.Children.Add(Text(hint, 10.2, Brush(Muted), false));
            row.Children.Add(labels);
            if (control != null)
            {
                var element = control as FrameworkElement;
                if (element != null) element.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(control, 1);
                row.Children.Add(control);
            }
            return row;
        }

        StackPanel BuildAppearanceSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(Text("THÈME", 9.5, Brush(Muted), true));
            var themes = new WrapPanel { Margin = new Thickness(0, 8, 0, 10) };
            string[] themeNames = { "Sombre", "Clair", "Noir AMOLED", "Automatique" };
            foreach (string name in themeNames)
            {
                string captured = name;
                var button = Button(themeName == captured ? Glyphs.Check : "", captured, delegate { ChangeTheme(captured); }, themeName == captured);
                button.MinWidth = 132;
                button.Height = 38;
                themes.Children.Add(button);
            }
            stack.Children.Add(themes);

            stack.Children.Add(Text("COULEUR D’ACCENT", 9.5, Brush(Muted), true));
            var accents = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
            string[] accentColors = { "#8B5CF6", "#22D3EE", "#3B82F6", "#10B981", "#F59E0B", "#F43F5E", "#EC4899", "#94A3B8" };
            foreach (string hex in accentColors)
            {
                Color parsed;
                if (!TryParseHexColor(hex, out parsed)) continue;
                bool active = String.Equals(hex, accentHex, StringComparison.OrdinalIgnoreCase);
                var swatch = new Border
                {
                    Width = 34, Height = 34, CornerRadius = new CornerRadius(10), Background = Brush(parsed),
                    BorderBrush = active ? Brush(TextPrimary) : Brush(BorderColor),
                    BorderThickness = active ? new Thickness(3) : new Thickness(1),
                    Margin = new Thickness(0, 0, 9, 8), Cursor = Cursors.Hand,
                    ToolTip = "Accent " + hex, Child = active ? Icon(Glyphs.Check, 14, Brush(TextOnColor(parsed))) : null
                };
                swatch.MouseLeftButtonUp += delegate { ChangeAccent(parsed); };
                accents.Children.Add(swatch);
            }
            stack.Children.Add(accents);

            var custom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };
            var customBox = StyledTextBox();
            customBox.Width = 118; customBox.Text = accentHex; customBox.ToolTip = "Code hexadécimal, par exemple #8B5CF6";
            customBox.Margin = new Thickness(0, 0, 7, 0);
            custom.Children.Add(customBox);
            var apply = Button(Glyphs.Check, "Appliquer", delegate
            {
                Color parsed;
                if (!TryParseHexColor(customBox.Text, out parsed)) { ShowError("Couleur invalide. Utilisez un code hexadécimal à 6 chiffres, par exemple #8B5CF6."); return; }
                ChangeAccent(parsed);
            }, false);
            custom.Children.Add(apply);
            stack.Children.Add(custom);

            stack.Children.Add(SettingToggle("Réduire les animations et les effets de mouvement", settings.ReduceMotion, delegate(bool value)
            {
                settings.ReduceMotion = value;
                UiMotion.Reduced = value;
                SaveSettings();
                Notice(value ? "Animations réduites." : "Animations complètes activées.");
            }));
            stack.Children.Add(Text("LyaranTV respecte le thème clair/sombre de Windows lorsque « Automatique » est sélectionné.", 10.2, Brush(Muted), false));
            return stack;
        }

        static string LanguageOptionLabel(string code)
        {
            if (String.IsNullOrWhiteSpace(code) || code == "auto") return "Automatique (piste par défaut)";
            string name = MediaTrackScanner.LanguageName(code, code.ToUpperInvariant());
            return name + " (" + code.ToLowerInvariant() + ")";
        }

        static string LanguageCodeFromOption(string label)
        {
            if (String.IsNullOrWhiteSpace(label) || label.StartsWith("Automatique", StringComparison.OrdinalIgnoreCase)) return "auto";
            int open = label.LastIndexOf('(');
            int close = label.LastIndexOf(')');
            if (open >= 0 && close > open) return label.Substring(open + 1, close - open - 1).Trim().ToLowerInvariant();
            return "fr";
        }

        static string SpeedSettingLabel(double speed)
        {
            if (Math.Abs(speed - 1.0) < 0.01) return "1× (normale)";
            return speed.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',') + "×";
        }

        static double ParseSpeedSetting(string label)
        {
            if (String.IsNullOrWhiteSpace(label)) return 1.0;
            string value = label.Replace(" (normale)", "").Replace("×", "").Replace(',', '.').Trim();
            double parsed;
            return Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 1.0;
        }

        StackPanel BuildPlaybackSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(SettingToggle("Lire automatiquement le dernier contenu après le chargement d’une playlist", settings.AutoPlayLast, delegate(bool value) { settings.AutoPlayLast = value; SaveSettings(); }));
            stack.Children.Add(SettingToggle("Reprendre les vidéos là où vous les avez quittées", settings.ResumePlayback, delegate(bool value) { settings.ResumePlayback = value; SaveSettings(); }));
            stack.Children.Add(SettingToggle("Enchaîner automatiquement l’épisode suivant", settings.AutoPlayNext, delegate(bool value) { settings.AutoPlayNext = value; SaveSettings(); }));
            stack.Children.Add(SettingToggle("Afficher le programme en cours dans la bibliothèque", settings.ShowEpgInList, delegate(bool value) { settings.ShowEpgInList = value; SaveSettings(); ShowSettings(); }));

            var engine = new ComboBox { Width = 210, Height = 34, Style = ModernComboStyle() };
            engine.Items.Add("Automatique (VLC si installé)");
            engine.Items.Add("Lecteur Windows (Media Foundation)");
            engine.Items.Add("VLC intégré (tous les codecs)");
            engine.SelectedIndex = settings.PlaybackEngine == "windows" ? 1 : (settings.PlaybackEngine == "vlc" ? 2 : 0);
            engine.SelectionChanged += delegate
            {
                if (engine.SelectedIndex < 0) return;
                settings.PlaybackEngine = engine.SelectedIndex == 1 ? "windows" : (engine.SelectedIndex == 2 ? "vlc" : "auto");
                SaveSettings();
                Notice("Moteur de lecture enregistré. Il s’applique à la prochaine lecture.");
            };
            string engineNote = VlcRuntime.Available
                ? "VLC décode l’AC-3, l’E-AC-3, le DTS et le TrueHD, que Windows laisse muets : c’est lui qui rétablit le son des films et épisodes concernés. Détecté : " + Path.GetFileName(VlcRuntime.Directory.TrimEnd('\\')) + "."
                : "Windows Media Foundation lit la plupart des flux, mais pas l’AC-3, l’E-AC-3 ni le DTS. Installez VLC pour activer le moteur complet.";
            stack.Children.Add(SettingsRow("Moteur de lecture", engineNote, engine));

            if (!VlcRuntime.Available)
            {
                var installVlc = Button(Glyphs.Download, "Télécharger VLC", delegate
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.videolan.org/vlc/") { UseShellExecute = true }); }
                    catch { ShowError("Impossible d’ouvrir la page de téléchargement."); }
                }, false);
                installVlc.Height = 34;
                stack.Children.Add(SettingsRow("Son des films et des séries",
                    "Windows ne décode ni l’AC-3, ni l’E-AC-3, ni le DTS, ni le TrueHD : ces pistes sortent muettes. VLC les décode toutes. Installez-le, puis relancez LyaranTV.", installVlc));
            }

            // Une carte qui refuse le format d’une piste (7.1, 24 bits, TrueHD) ou
            // qui reste occupée en exclusif laisse le film muet : ce réglage permet
            // de changer de sortie sans quitter le lecteur.
            var audioOutput = new ComboBox { Width = 220, Height = 34, Style = ModernComboStyle() };
            audioOutput.Items.Add("Automatique (recommandé)");
            audioOutput.Items.Add("Périphérique Windows (WASAPI)");
            audioOutput.Items.Add("DirectSound");
            audioOutput.Items.Add("WaveOut (compatibilité)");
            string[] audioOutputValues = { "auto", "mmdevice", "directsound", "waveout" };
            int audioOutputIndex = Array.IndexOf(audioOutputValues, settings.AudioOutput ?? "auto");
            audioOutput.SelectedIndex = audioOutputIndex < 0 ? 0 : audioOutputIndex;
            audioOutput.SelectionChanged += delegate
            {
                if (audioOutput.SelectedIndex < 0) return;
                settings.AudioOutput = audioOutputValues[audioOutput.SelectedIndex];
                SaveSettings();
                Notice("Sortie audio enregistrée. Elle s’applique à la prochaine lecture.");
            };
            stack.Children.Add(SettingsRow("Sortie audio (VLC)",
                "Un film muet alors que la piste existe ? Changez de sortie : WASAPI gère le multicanal, DirectSound et WaveOut servent de repli.", audioOutput));

            var speed = new ComboBox { Width = 130, Height = 34, Style = ModernComboStyle() };
            foreach (string value in new[] { "0,5×", "0,75×", "1× (normale)", "1,25×", "1,5×", "2×" }) speed.Items.Add(value);
            speed.SelectedItem = SpeedSettingLabel(settings.PlaybackSpeed);
            speed.SelectionChanged += delegate
            {
                if (speed.SelectedItem == null) return;
                double chosen = ParseSpeedSetting((string)speed.SelectedItem);
                settings.PlaybackSpeed = chosen;
                SaveSettings();
                if (player != null) { try { player.SpeedRatio = chosen; } catch { } }
                playbackSpeed = chosen;
                UpdateSpeedButton();
            };
            stack.Children.Add(SettingsRow("Vitesse de lecture par défaut", "Ajustable à tout moment dans le lecteur avec [ et ].", speed));

            var subtitleSize = new ComboBox { Width = 110, Height = 34, Style = ModernComboStyle() };
            foreach (string value in new[] { "14", "16", "18", "20", "24", "28" }) subtitleSize.Items.Add(value);
            subtitleSize.SelectedItem = ((int)Math.Round(settings.SubtitleSize)).ToString();
            if (subtitleSize.SelectedItem == null) subtitleSize.SelectedItem = "16";
            subtitleSize.SelectionChanged += delegate
            {
                if (subtitleSize.SelectedItem == null) return;
                double parsed;
                if (Double.TryParse((string)subtitleSize.SelectedItem, out parsed))
                {
                    settings.SubtitleSize = parsed;
                    SaveSettings();
                    ApplySubtitleStyle();
                }
            };
            stack.Children.Add(SettingsRow("Taille des sous-titres", "S’applique aussi en plein écran, avec une taille légèrement supérieure.", subtitleSize));

            stack.Children.Add(SettingToggle("Fond sombre derrière les sous-titres", settings.SubtitleBackground, delegate(bool value)
            {
                settings.SubtitleBackground = value;
                SaveSettings();
                ApplySubtitleStyle();
            }));

            stack.Children.Add(SettingToggle("Choisir automatiquement un sous-titre disponible", settings.AutoSubtitles, delegate(bool value)
            {
                settings.AutoSubtitles = value;
                SaveSettings();
                Notice(value ? "Sous-titres automatiques activés." : "Sous-titres automatiques désactivés.");
            }));

            var audioLanguage = new ComboBox { Width = 180, Height = 34, Style = ModernComboStyle() };
            string[] audioCodes = { "auto", "fr", "en", "es", "de", "it", "pt", "ar", "nl", "ru", "ja" };
            foreach (string code in audioCodes) audioLanguage.Items.Add(LanguageOptionLabel(code));
            audioLanguage.SelectedItem = LanguageOptionLabel(settings.PreferredAudioLanguage);
            if (audioLanguage.SelectedItem == null) audioLanguage.SelectedItem = LanguageOptionLabel("auto");
            audioLanguage.SelectionChanged += delegate
            {
                if (audioLanguage.SelectedItem == null) return;
                settings.PreferredAudioLanguage = LanguageCodeFromOption((string)audioLanguage.SelectedItem);
                SaveSettings();
            };
            stack.Children.Add(SettingsRow("Langue audio préférée",
                "Priorité aux fichiers avec plusieurs pistes. Avec le moteur VLC, la piste se change directement dans le lecteur ; Windows lit la piste par défaut.", audioLanguage));

            var subtitleLanguage = new ComboBox { Width = 180, Height = 34, Style = ModernComboStyle() };
            string[] subtitleCodes = { "fr", "en", "es", "de", "it", "pt", "ar", "nl", "ru", "ja" };
            foreach (string code in subtitleCodes) subtitleLanguage.Items.Add(LanguageOptionLabel(code));
            subtitleLanguage.SelectedItem = LanguageOptionLabel(settings.SubtitleLanguage);
            if (subtitleLanguage.SelectedItem == null) subtitleLanguage.SelectedItem = LanguageOptionLabel("fr");
            subtitleLanguage.SelectionChanged += delegate
            {
                if (subtitleLanguage.SelectedItem == null) return;
                settings.SubtitleLanguage = LanguageCodeFromOption((string)subtitleLanguage.SelectedItem);
                SaveSettings();
            };
            stack.Children.Add(SettingsRow("Langue des sous-titres",
                "Priorité pour les sous-titres intégrés, les fichiers voisins et OpenSubtitles.", subtitleLanguage));

            var aspect = new ComboBox { Width = 150, Height = 34, Style = ModernComboStyle() };
            aspect.Items.Add("Adapter"); aspect.Items.Add("Remplir"); aspect.Items.Add("Étirer");
            aspect.SelectedItem = playerAspect;
            aspect.SelectionChanged += delegate
            {
                if (aspect.SelectedItem == null) return;
                playerAspect = (string)aspect.SelectedItem;
                settings.AspectMode = playerAspect;
                SaveSettings();
                ApplyPlayerStretch();
            };
            stack.Children.Add(SettingsRow("Format d’image par défaut", "Adapter conserve les proportions ; Remplir recadre l’image.", aspect));

            var volume = new Slider
            {
                Minimum = 0, Maximum = 100, Value = savedVolume * 100, Width = 190, IsMoveToPointEnabled = true,
                VerticalAlignment = VerticalAlignment.Center, Style = ModernSliderStyle()
            };
            volume.ValueChanged += delegate
            {
                savedVolume = volume.Value / 100.0;
                settings.Volume = savedVolume;
                if (player != null && !wasMuted) player.Volume = savedVolume;
                SaveSettings();
            };
            stack.Children.Add(SettingsRow("Volume par défaut", "Le volume est mémorisé entre deux sessions.", volume));
            return stack;
        }

        StackPanel BuildProfileSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(Text("PROFILS", 9.5, Brush(Muted), true));
            stack.Children.Add(Text("Chaque profil conserve ses propres favoris, son historique et ses positions de reprise.", 10.5, Brush(Muted), false));

            foreach (var profile in profiles.ToList())
            {
                var captured = profile;
                var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Color avatar; if (!TryParseHexColor(profile.AvatarColor, out avatar)) avatar = Accent;
                var circle = new Border
                {
                    Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = Brush(avatar),
                    VerticalAlignment = VerticalAlignment.Center, Child = Text(profile.Name.Substring(0, 1).ToUpperInvariant(), 13, Brush(Colors.White), true)
                };
                ((TextBlock)circle.Child).HorizontalAlignment = HorizontalAlignment.Center;
                Grid.SetColumn(circle, 0); row.Children.Add(circle);
                var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 0, 8, 0) };
                labels.Children.Add(Text(profile.Name + (profile == activeProfile ? "  ·  actif" : ""), 12.5, Brush(profile == activeProfile ? Accent : TextPrimary), profile == activeProfile));
                string meta = profile.IsChild ? "Profil enfant" : "Profil standard";
                if (!String.IsNullOrWhiteSpace(profile.PinProtected)) meta += "  ·  code PIN";
                labels.Children.Add(Text(meta, 10, Brush(Muted), false));
                Grid.SetColumn(labels, 1); row.Children.Add(labels);
                var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (profile != activeProfile)
                {
                    var activate = Button(Glyphs.Check, "Activer", delegate { SwitchProfile(captured); }, false);
                    activate.Margin = new Thickness(0, 0, 5, 0);
                    actions.Children.Add(activate);
                }
                var edit = IconButton(Glyphs.Edit, "Modifier le profil", delegate { EditProfile(captured); }, false);
                edit.Margin = new Thickness(0, 0, 5, 0);
                actions.Children.Add(edit);
                var remove = IconButton(Glyphs.Delete, "Supprimer le profil", delegate { DeleteProfile(captured); }, false);
                remove.Margin = new Thickness(0);
                actions.Children.Add(remove);
                Grid.SetColumn(actions, 2); row.Children.Add(actions);
                stack.Children.Add(Card(row, 10));
            }

            var add = Button(Glyphs.Add, "Ajouter un profil", delegate { AddProfile(); }, false);
            add.Margin = new Thickness(0, 10, 0, 12);
            stack.Children.Add(add);

            stack.Children.Add(Text("CONTRÔLE PARENTAL", 9.5, Brush(Muted), true));
            stack.Children.Add(SettingToggle("Activer le filtrage des catégories pour les profils enfant", settings.ParentalEnabled, delegate(bool value)
            {
                if (!RequireParentalUnlock()) { ShowSettings(); return; }
                settings.ParentalEnabled = value;
                SaveSettings();
                ShowSettings();
                if (value && String.IsNullOrWhiteSpace(settings.ParentalPinProtected))
                    Notice("Définissez un code PIN parental pour protéger ce réglage.");
                else Notice(value ? "Contrôle parental activé." : "Contrôle parental désactivé.");
            }));

            var pinRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            var pinBox = StyledPasswordBox();
            pinBox.Width = 150; pinBox.Margin = new Thickness(0, 0, 7, 0);
            pinBox.ToolTip = "Nouveau code PIN (4 chiffres ou plus)";
            pinRow.Children.Add(pinBox);
            var setPin = Button(Glyphs.Lock, "Définir le code", delegate { SetParentalPin(pinBox.Password); }, false);
            setPin.Margin = new Thickness(0, 0, 5, 0);
            pinRow.Children.Add(setPin);
            if (!String.IsNullOrWhiteSpace(settings.ParentalPinProtected))
            {
                var clearPin = Button(Glyphs.Unlock, "Supprimer", delegate { RemoveParentalPin(); }, false);
                pinRow.Children.Add(clearPin);
            }
            stack.Children.Add(SettingsRow("Code PIN parental", String.IsNullOrWhiteSpace(settings.ParentalPinProtected) ? "Aucun code défini." : "Un code PIN protège les réglages et le changement de profil.", pinRow));

            var groupsBox = StyledTextBox();
            groupsBox.AcceptsReturn = true; groupsBox.TextWrapping = TextWrapping.Wrap; groupsBox.Height = 78;
            groupsBox.VerticalContentAlignment = VerticalAlignment.Top;
            groupsBox.Padding = new Thickness(11, 8, 11, 8);
            groupsBox.Text = (settings.LockedGroups ?? "").Replace(";", Environment.NewLine);
            groupsBox.ToolTip = "Un groupe par ligne. Exemple : Adulte, Cinéma interdit…";
            stack.Children.Add(SettingsRow("Catégories verrouillées pour les profils enfant", "Un nom de catégorie par ligne. Les profils enfant ne verront pas ces contenus.", null));
            stack.Children.Add(groupsBox);
            var saveGroups = Button(Glyphs.Save, "Enregistrer les catégories verrouillées", delegate
            {
                if (!RequireParentalUnlock()) return;
                settings.LockedGroups = String.Join(";", groupsBox.Text.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray());
                SaveSettings();
                RebuildSidebar(); UpdateStatusBar();
                Notice("Catégories verrouillées enregistrées.");
            }, false);
            saveGroups.Margin = new Thickness(0, 7, 0, 0);
            stack.Children.Add(saveGroups);
            return stack;
        }

        StackPanel BuildTmdbSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(SettingToggle("Récupérer les affiches, notes et synopsis depuis TMDb", settings.TmdbEnabled, delegate(bool value)
            {
                settings.TmdbEnabled = value;
                SaveSettings();
                if (value && !TmdbClient.IsConfigured(settings))
                    Notice("Ajoutez une clé API TMDb (v3) pour activer les métadonnées.");
                else if (value && channels.Count > 0) StartTmdbEnrichment();
            }));

            var keyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            var keyBox = StyledPasswordBox();
            keyBox.Width = 300; keyBox.Password = settings.TmdbApiKey ?? "";
            keyBox.ToolTip = "Clé API TMDb v3. Stockée localement dans vos réglages.";
            keyBox.Margin = new Thickness(0, 0, 7, 0);
            keyRow.Children.Add(keyBox);
            var saveKey = Button(Glyphs.Save, "Enregistrer", delegate
            {
                settings.TmdbApiKey = (keyBox.Password ?? "").Trim();
                settings.TmdbEnabled = true;
                SaveSettings();
                if (settings.TmdbApiKey.Length == 0 && String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LYARANTV_TMDB_KEY")))
                    Notice("Clé effacée. TMDb reste désactivé tant qu’aucune clé n’est fournie.");
                else Notice("Clé TMDb enregistrée.");
            }, false);
            keyRow.Children.Add(saveKey);
            var test = Button(Glyphs.Globe, "Tester", delegate { TestTmdbKey(); }, false);
            keyRow.Children.Add(test);
            stack.Children.Add(SettingsRow("Clé API TMDb (v3)", "Créez une clé gratuite sur themoviedb.org. Elle n’est jamais incluse dans LyaranTV.", keyRow));
            if (String.IsNullOrWhiteSpace(settings.TmdbApiKey) && !String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LYARANTV_TMDB_KEY")))
                stack.Children.Add(Text("Une clé est fournie par la variable d’environnement LYARANTV_TMDB_KEY.", 10, Brush(Success), false));

            // Sous-titres en ligne : la clé n'est utilisée que sur demande explicite
            // depuis le lecteur (bouton « Chercher sur OpenSubtitles »).
            var subtitleKeyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            var subtitleKeyBox = StyledPasswordBox();
            subtitleKeyBox.Width = 300; subtitleKeyBox.Password = settings.OpenSubtitlesKey ?? "";
            subtitleKeyBox.ToolTip = "Clé API OpenSubtitles (compte gratuit). Stockée localement dans vos réglages.";
            subtitleKeyBox.Margin = new Thickness(0, 0, 7, 0);
            subtitleKeyRow.Children.Add(subtitleKeyBox);
            subtitleKeyRow.Children.Add(Button(Glyphs.Save, "Enregistrer", delegate
            {
                settings.OpenSubtitlesKey = (subtitleKeyBox.Password ?? "").Trim();
                SaveSettings();
                Notice(settings.OpenSubtitlesKey.Length == 0
                    ? "Clé OpenSubtitles effacée. La recherche en ligne est désactivée."
                    : "Clé OpenSubtitles enregistrée. La recherche s'ouvre depuis le lecteur.");
            }, false));
            stack.Children.Add(SettingsRow("Clé API OpenSubtitles",
                "Gratuite sur opensubtitles.com. Utilisée uniquement quand vous cherchez un sous-titre depuis le lecteur ; elle n'est jamais incluse dans LyaranTV.",
                subtitleKeyRow));
            if (String.IsNullOrWhiteSpace(settings.OpenSubtitlesKey) && !String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LYARANTV_OPENSUBTITLES_KEY")))
                stack.Children.Add(Text("Une clé est fournie par la variable d’environnement LYARANTV_OPENSUBTITLES_KEY.", 10, Brush(Success), false));

            var language = new ComboBox { Width = 150, Height = 34, Style = ModernComboStyle() };
            foreach (string value in new[] { "fr-FR", "en-US", "es-ES", "de-DE", "it-IT", "pt-BR" }) language.Items.Add(value);
            language.SelectedItem = String.IsNullOrWhiteSpace(settings.TmdbLanguage) ? "fr-FR" : settings.TmdbLanguage;
            language.SelectionChanged += delegate
            {
                if (language.SelectedItem == null) return;
                settings.TmdbLanguage = (string)language.SelectedItem;
                SaveSettings();
            };
            stack.Children.Add(SettingsRow("Langue des métadonnées", "Les synopsis et titres sont demandés dans cette langue.", language));

            var cacheLimit = new ComboBox { Width = 120, Height = 34, Style = ModernComboStyle() };
            foreach (string value in new[] { "100", "300", "500", "1000" }) cacheLimit.Items.Add(value);
            cacheLimit.SelectedItem = settings.TmdbCacheLimit.ToString();
            if (cacheLimit.SelectedItem == null) cacheLimit.SelectedItem = "300";
            cacheLimit.SelectionChanged += delegate
            {
                if (cacheLimit.SelectedItem == null) return;
                int parsed;
                if (Int32.TryParse((string)cacheLimit.SelectedItem, out parsed))
                {
                    settings.TmdbCacheLimit = parsed;
                    TmdbCacheStore.Trim(parsed);
                    TmdbCacheStore.Save();
                    SaveSettings();
                }
            };
            stack.Children.Add(SettingsRow("Taille du cache local", TmdbCacheStore.Count + " fiche(s) en cache actuellement.", cacheLimit));

            var cacheActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 0) };
            cacheActions.Children.Add(Button(Glyphs.Delete, "Vider le cache TMDb", delegate
            {
                TmdbCacheStore.Clear();
                ShowSettings();
                Notice("Cache TMDb vidé.");
            }, false));
            cacheActions.Children.Add(Button(Glyphs.Delete, "Vider le cache des affiches", delegate
            {
                ArtworkStore.Clear();
                ImageCache.ClearAll();
                ImageCache.ClearDisk();
                ShowSettings();
                Notice("Cache des affiches et des vignettes vidé.");
            }, false));
            cacheActions.Children.Add(Button(Glyphs.Refresh, "Actualiser les métadonnées", delegate { StartTmdbEnrichment(); ShowToast("Recherche TMDb lancée en arrière-plan.", false, false); }, false));
            stack.Children.Add(cacheActions);

            var attribution = Text(TmdbClient.Attribution + " Affiches et synopsis de secours : TVmaze, IMDb et Wikipédia (" + ArtworkStore.Count + " fiche(s) en cache). Les métadonnées restent facultatives : sans réseau, chaque contenu garde une vignette dessinée à partir de son titre.", 9.8, Brush(Muted), false);
            attribution.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(attribution);
            return stack;
        }

        StackPanel BuildLetterboxdApiSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(Text("ACTIVITÉ PUBLIQUE · NOM D’UTILISATEUR SUFFIT", 10, Brush(Accent), true));
            var apiNotice = Text("Saisissez le nom public de votre compte : LyaranTV chargera automatiquement son flux RSS public, sans authentification ni mot de passe. Le flux contient l’activité publique récente; importez votre export CSV pour récupérer l’historique complet, vos notes et votre liste à voir.", 11, Brush(Muted), false);
            apiNotice.TextWrapping = TextWrapping.Wrap;
            apiNotice.Margin = new Thickness(0, 4, 0, 8);
            stack.Children.Add(apiNotice);
            var apiLinks = new StackPanel { Orientation = Orientation.Horizontal };
            apiLinks.Children.Add(Button(Glyphs.External, "Documentation de l’API", delegate
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://api-docs.letterboxd.com/") { UseShellExecute = true }); }
                catch { ShowError("Impossible d’ouvrir la documentation Letterboxd."); }
            }, false));
            var access = Button(Glyphs.External, "Accès développeur Letterboxd", delegate
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://letterboxd.com/api-beta/access/") { UseShellExecute = true }); }
                catch { ShowError("Impossible d’ouvrir la page d’accès développeur Letterboxd."); }
            }, false);
            access.Margin = new Thickness(7, 0, 0, 0);
            apiLinks.Children.Add(access);
            stack.Children.Add(apiLinks);

            var accountHeader = Text("PROFIL ET ACTIVITÉ PUBLIQUE", 10, Brush(Accent), true);
            accountHeader.Margin = new Thickness(0, 17, 0, 4);
            stack.Children.Add(accountHeader);
            var letterboxdUser = StyledTextBox();
            letterboxdUser.Width = 220;
            letterboxdUser.Text = LetterboxdActivityStore.Username;
            letterboxdUser.ToolTip = "Votre nom public suffit pour charger l’activité récente; aucun mot de passe n’est demandé.";
            var letterboxdRow = new StackPanel { Orientation = Orientation.Horizontal };
            letterboxdRow.Children.Add(letterboxdUser);
            var saveLetterboxd = Button(Glyphs.Save, "Enregistrer", delegate
            {
                LetterboxdActivityStore.SetUsername(letterboxdUser.Text);
                Notice("Nom Letterboxd enregistré. Son flux public sera actualisé automatiquement à l’ouverture de l’Accueil.");
            }, false);
            saveLetterboxd.Margin = new Thickness(7, 0, 0, 0);
            letterboxdRow.Children.Add(saveLetterboxd);
            var importLetterboxd = Button(Glyphs.OpenFile, "Importer mes CSV", delegate
            {
                var picker = new OpenFileDialog
                {
                    Title = "Importer un export personnel Letterboxd",
                    Filter = "Fichiers CSV Letterboxd (*.csv)|*.csv|Fichiers CSV (*.csv)|*.csv",
                    Multiselect = true,
                    CheckFileExists = true
                };
                if (picker.ShowDialog(this) != true) return;
                int count = LetterboxdActivityStore.ImportFiles(picker.FileNames);
                ShowToast(count + " ligne(s) importée(s) dans le profil « " + ProfileScope.Current + " ».", false, true);
            }, false);
            importLetterboxd.Margin = new Thickness(7, 0, 0, 0);
            letterboxdRow.Children.Add(importLetterboxd);
            stack.Children.Add(SettingsRow("Nom d’utilisateur Letterboxd", "Suffit pour charger automatiquement vos avis publics récents. Enregistrez-le, puis ouvrez l’Accueil.", letterboxdRow));

            var letterboxdProfiles = StyledTextBox();
            letterboxdProfiles.Width = 350;
            letterboxdProfiles.Text = LetterboxdActivityStore.PublicUsernames;
            letterboxdProfiles.ToolTip = "Facultatif : noms publics d’amis, séparés par des virgules. Maximum : 7 amis.";
            var letterboxdProfilesRow = new StackPanel { Orientation = Orientation.Horizontal };
            letterboxdProfilesRow.Children.Add(letterboxdProfiles);
            var saveLetterboxdProfiles = Button(Glyphs.Save, "Enregistrer", delegate
            {
                LetterboxdActivityStore.SetPublicUsernames(letterboxdProfiles.Text);
                letterboxdProfiles.Text = LetterboxdActivityStore.PublicUsernames;
                Notice("Profils Letterboxd publics enregistrés dans le profil « " + ProfileScope.Current + " ».");
            }, false);
            saveLetterboxdProfiles.Margin = new Thickness(7, 0, 0, 0);
            letterboxdProfilesRow.Children.Add(saveLetterboxdProfiles);
            stack.Children.Add(SettingsRow("Autres profils publics (facultatif)", "Laissez ce champ vide pour ne suivre que votre compte. Vous pouvez ajouter jusqu’à 7 noms d’amis.", letterboxdProfilesRow));

            var letterboxdActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var clearLetterboxd = Button(Glyphs.Delete, "Supprimer mes données Letterboxd", delegate
            {
                if (!AskConfirmation("Supprimer les données Letterboxd", "L’activité importée, les noms de profils et le cache RSS du profil « " + ProfileScope.Current + " » seront supprimés.", "Supprimer")) return;
                LetterboxdActivityStore.Clear();
                ShowSettings();
                Notice("Données Letterboxd locales supprimées pour ce profil.");
            }, false);
            letterboxdActions.Children.Add(clearLetterboxd);
            stack.Children.Add(letterboxdActions);

            var privacy = Text("Les noms et les données importées restent locaux et séparés par profil. Aucun mot de passe Letterboxd n’est demandé. Les flux publics sont mis en cache six heures; les données privées et l’écriture sur Letterboxd nécessitent un accès API approuvé.", 9.8, Brush(Muted), false);
            privacy.TextWrapping = TextWrapping.Wrap;
            privacy.Margin = new Thickness(0, 9, 0, 0);
            stack.Children.Add(privacy);
            return stack;
        }

        StackPanel BuildStartupSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(SettingToggle("Charger la dernière playlist au démarrage", settings.LoadLastSource, delegate(bool value) { settings.LoadLastSource = value; SaveSettings(); }));
            var startPage = new ComboBox { Width = 190, Height = 34, Style = ModernComboStyle() };
            startPage.Items.Add("Accueil"); startPage.Items.Add("Bibliothèque"); startPage.Items.Add("Favoris"); startPage.Items.Add("Guide EPG");
            startPage.SelectedItem = String.IsNullOrWhiteSpace(settings.StartPage) ? "Accueil" : settings.StartPage;
            startPage.SelectionChanged += delegate
            {
                if (startPage.SelectedItem == null) return;
                settings.StartPage = (string)startPage.SelectedItem;
                SaveSettings();
            };
            stack.Children.Add(SettingsRow("Page d’accueil au lancement", "Choisissez la première vue après le démarrage.", startPage));
            return stack;
        }

        StackPanel BuildExternalPlayerSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(Text("Utilisez VLC ou PotPlayer lorsque Windows Media Foundation ne prend pas en charge un flux ou un codec.", 11.5, Brush(Muted), false));
            var row = new Grid { Margin = new Thickness(0, 9, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var externalBox = StyledTextBox();
            externalBox.Height = 36;
            externalBox.Text = settings.ExternalPlayer ?? "";
            externalBox.ToolTip = "Chemin vers vlc.exe ou PotPlayerMini64.exe";
            row.Children.Add(externalBox);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(7, 0, 0, 0) };
            var browse = Button(Glyphs.OpenFile, "Parcourir", delegate
            {
                var dialog = new OpenFileDialog { Filter = "Lecteur vidéo (*.exe)|*.exe|Tous les fichiers (*.*)|*.*" };
                if (dialog.ShowDialog(this) == true) externalBox.Text = dialog.FileName;
            }, false);
            browse.Margin = new Thickness(0, 0, 5, 0);
            buttons.Children.Add(browse);
            buttons.Children.Add(Button(Glyphs.Search, "Détecter", delegate
            {
                string found = FindExternalPlayer();
                if (String.IsNullOrWhiteSpace(found)) { ShowToast("Aucun lecteur externe détecté. Installez VLC ou indiquez son chemin.", true, false); return; }
                externalBox.Text = found;
                ShowToast("Lecteur détecté : " + Path.GetFileName(found), false, true);
            }, false));
            Grid.SetColumn(buttons, 1);
            row.Children.Add(buttons);
            stack.Children.Add(row);
            var saveExternal = Button(Glyphs.Save, "Enregistrer le lecteur", delegate
            {
                string value = externalBox.Text.Trim();
                if (value.Length > 0 && !File.Exists(value)) { ShowError("Le chemin indiqué n’existe pas."); return; }
                settings.ExternalPlayer = value;
                SaveSettings();
                ShowToast(value.Length == 0 ? "Lecteur externe réinitialisé." : "Lecteur externe enregistré.", false, true);
            }, false);
            saveExternal.Margin = new Thickness(0, 9, 0, 0);
            stack.Children.Add(saveExternal);
            return stack;
        }

        StackPanel BuildDataSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(Text("Vos playlists et préférences sont stockées localement : " + AppPaths.DataDir, 10.5, Brush(Muted), false));
            var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            actions.Children.Add(Button(Glyphs.Download, "Exporter la configuration JSON", ExportPortableJson, false));
            actions.Children.Add(Button(Glyphs.Upload, "Importer une configuration JSON", ImportPortableJson, false));
            actions.Children.Add(Button(Glyphs.Zip, "Sauvegarde complète", Backup, false));
            actions.Children.Add(Button(Glyphs.Repair, "Restaurer une sauvegarde", Restore, false));
            actions.Children.Add(Button(Glyphs.List, "Exporter la playlist M3U", ExportM3u, false));
            actions.Children.Add(Button(Glyphs.Folder, "Ouvrir le dossier", OpenDataFolder, false));
            actions.Children.Add(Button(Glyphs.Delete, "Vider le cache mémoire des images", delegate { ImageCache.ClearAll(); ShowToast("Cache mémoire des images vidé.", false, true); }, false));
            actions.Children.Add(Button(Glyphs.Power, "Réinitialiser LyaranTV", ResetData, false));
            stack.Children.Add(actions);
            var imagesNote = Text("Les affiches de films et de séries, les vignettes et les logos de chaînes sont chargés à la demande depuis leurs sources en ligne (TMDb ou URL de la playlist). Ils restent uniquement en mémoire : aucun fichier d'image n'est téléchargé sur le disque.", 9.8, Brush(Muted), false);
            imagesNote.Margin = new Thickness(0, 9, 0, 0);
            stack.Children.Add(imagesNote);
            var note = Text("L'export JSON est portable : il contient les playlists, les favoris, les profils et les réglages, mais jamais les mots de passe, les adresses MAC ni les codes PIN. La sauvegarde complète (.lyaran) conserve les identifiants chiffrés pour cet utilisateur Windows.", 9.8, Brush(Muted), false);
            note.Margin = new Thickness(0, 9, 0, 0);
            stack.Children.Add(note);
            return stack;
        }

        Border BuildAboutSettings()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var mark = new Image { Source = CreateLogoMark(), Width = 72, Height = 66, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(mark, 0); grid.Children.Add(mark);
            var stack = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            stack.Children.Add(Text(AppInfo.Name + " " + AppInfo.Version, 18, Brush(TextPrimary), true));
            stack.Children.Add(Text(AppInfo.Description, 11, Brush(Muted), false));
            stack.Children.Add(Text("Profil actif : " + (activeProfile == null ? "Principal" : activeProfile.Name) + " · " + TextUtil.HumanCount(channels.Count) + " éléments · " + TextUtil.HumanCount(loadedEpg.Count) + " programmes EPG", 10, Brush(Muted), false));
            stack.Children.Add(Text("Thème : " + themeName + " · Accent : " + accentHex + " · Données : " + AppPaths.DataDir, 9.8, Brush(Muted), false));
            var legal = Text(TmdbClient.Attribution + " Lecture assurée par libVLC 3.0 (VideoLAN, LGPL 2.1) ; repli sur Windows Media Foundation.", 9.8, Brush(Muted), false);
            legal.Margin = new Thickness(0, 7, 0, 0);
            stack.Children.Add(legal);

            var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            actions.Children.Add(Button(Glyphs.Refresh, I18n.T("Vérifier les mises à jour"), delegate { CheckForUpdates(false); }, false));
            actions.Children.Add(Button(Glyphs.Help, I18n.T("À propos"), OpenAboutDialog, false));
            actions.Children.Add(Button(Glyphs.List, I18n.T("Exporter le journal"), ExportLog, false));
            stack.Children.Add(actions);
            Grid.SetColumn(stack, 1); grid.Children.Add(stack);
            return SettingsCard(I18n.T("À propos"), "", grid);
        }

        StackPanel BuildUpdateSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(Text(I18n.T("Version installée") + " · " + AppInfo.Version, 11, Brush(Muted), false));
            string lastCheck = "";
            if (!String.IsNullOrWhiteSpace(settings.LastUpdateCheck))
            {
                DateTime stamp;
                if (DateTime.TryParse(settings.LastUpdateCheck, out stamp)) lastCheck = stamp.ToString("g");
            }
            if (lastCheck.Length > 0)
                stack.Children.Add(Text(I18n.T("Dernière vérification") + " · " + lastCheck, 10, Brush(Muted), false));

            var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            actions.Children.Add(Button(Glyphs.Refresh, I18n.T("Rechercher une mise à jour"), delegate { CheckForUpdates(false); }, true));
            actions.Children.Add(Button(Glyphs.Lock, I18n.T("Jeton GitHub"), OpenUpdateTokenDialog, false));
            stack.Children.Add(actions);

            var tokenState = Text(String.IsNullOrWhiteSpace(settings.UpdateTokenProtected)
                ? I18n.T("Dépôt privé : jeton GitHub requis.")
                : "Jeton enregistré · " + I18n.T("Le jeton reste chiffré sur cet ordinateur."), 10, Brush(Muted), false);
            tokenState.Margin = new Thickness(0, 8, 0, 0);
            tokenState.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(tokenState);

            stack.Children.Add(SettingToggle(I18n.T("Vérifier au démarrage"), settings.CheckUpdatesOnStart, delegate(bool value)
            {
                settings.CheckUpdatesOnStart = value;
                SaveSettings();
                ShowSettings();
            }));

            var note = Text("Le programme interroge les Releases du dépôt " + AppInfo.Repository
                + " ; le fichier téléchargé est comparé à l'empreinte SHA-256 publiée avant d'être exécuté. Pour un jeton à granularité fine, autorisez ce dépôt et Contents : Read; pour un jeton classique, utilisez repo.", 9.8, Brush(Muted), false);
            note.Margin = new Thickness(0, 9, 0, 0);
            note.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(note);
            return stack;
        }

        void TestTmdbKey()
        {
            SetStatus("Test de la clé TMDb…");
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = TmdbClient.Test(settings); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    SetStatus("Clé TMDb refusée.");
                    ShowError("TMDb ne répond pas correctement.\n\n" + e.Error.GetBaseException().Message);
                    return;
                }
                SetStatus("Clé TMDb valide.");
                ShowToast((string)e.Result, false, true);
            };
            worker.RunWorkerAsync();
        }

        bool RequireParentalUnlock()
        {
            if (!settings.ParentalEnabled || activeProfile == null || !activeProfile.IsChild || parentalUnlocked) return true;
            if (String.IsNullOrWhiteSpace(settings.ParentalPinProtected))
            {
                ShowError("Aucun code PIN parental n’est défini. Définissez-le depuis un profil standard.");
                return false;
            }
            string pin = PromptPin("Contrôle parental", "Saisissez le code PIN parental pour modifier ces réglages.");
            if (pin == null) return false;
            if (!String.Equals(CredentialVault.Unprotect(settings.ParentalPinProtected), pin, StringComparison.Ordinal))
            {
                ShowError("Code PIN parental incorrect.");
                return false;
            }
            parentalUnlocked = true;
            return true;
        }

        void SetParentalPin(string requested)
        {
            if (!RequireParentalUnlock()) return;
            if (!String.IsNullOrWhiteSpace(settings.ParentalPinProtected))
            {
                string current = PromptPin("Code parental actuel", "Saisissez le code PIN parental actuel.");
                if (current == null) return;
                if (!String.Equals(CredentialVault.Unprotect(settings.ParentalPinProtected), current, StringComparison.Ordinal))
                {
                    ShowError("Code PIN actuel incorrect.");
                    return;
                }
            }
            string pin = String.IsNullOrWhiteSpace(requested) ? PromptPin("Nouveau code parental", "Choisissez un code PIN de 4 chiffres ou plus.") : requested.Trim();
            if (pin == null) return;
            if (pin.Length < 4) { ShowError("Le code PIN doit contenir au moins 4 caractères."); return; }
            string confirmation = PromptPin("Confirmer le code parental", "Saisissez à nouveau le code PIN.");
            if (confirmation == null) return;
            if (!String.Equals(pin, confirmation, StringComparison.Ordinal)) { ShowError("Les deux codes PIN ne correspondent pas."); return; }
            settings.ParentalPinProtected = CredentialVault.Protect(pin);
            settings.ParentalEnabled = true;
            SaveSettings();
            ShowSettings();
            Notice("Code PIN parental enregistré.");
        }

        void RemoveParentalPin()
        {
            if (!RequireParentalUnlock()) return;
            string current = PromptPin("Supprimer le code parental", "Saisissez le code PIN parental actuel.");
            if (current == null) return;
            if (!String.Equals(CredentialVault.Unprotect(settings.ParentalPinProtected), current, StringComparison.Ordinal))
            {
                ShowError("Code PIN incorrect.");
                return;
            }
            settings.ParentalPinProtected = "";
            settings.ParentalEnabled = false;
            SaveSettings();
            ShowSettings();
            Notice("Code PIN parental supprimé. Le contrôle parental est désactivé.");
        }

        void AddProfile()
        {
            if (!RequireParentalUnlock()) return;
            var created = ShowProfileDialog(null);
            if (created == null) return;
            if (profiles.Any(p => String.Equals(p.Name, created.Name, StringComparison.OrdinalIgnoreCase)))
            {
                ShowError("Un profil porte déjà ce nom.");
                return;
            }
            profiles.Add(created);
            ProfileStore.Save(profiles);
            ShowSettings();
            Notice("Profil « " + created.Name + " » ajouté.");
        }

        void EditProfile(UserProfile profile)
        {
            if (profile == null) return;
            if (!RequireParentalUnlock()) return;
            var updated = ShowProfileDialog(profile);
            if (updated == null) return;
            if (!String.Equals(profile.Name, updated.Name, StringComparison.OrdinalIgnoreCase) &&
                profiles.Any(p => p != profile && String.Equals(p.Name, updated.Name, StringComparison.OrdinalIgnoreCase)))
            {
                ShowError("Un profil porte déjà ce nom.");
                return;
            }
            string oldName = profile.Name;
            profile.Name = updated.Name;
            profile.AvatarColor = updated.AvatarColor;
            profile.IsChild = updated.IsChild;
            profile.PinProtected = updated.PinProtected;
            if (!String.Equals(oldName, profile.Name, StringComparison.Ordinal))
            {
                StateStore.RenameProfile(oldName, profile.Name);
                if (profile == activeProfile) { ProfileScope.Current = profile.Name; settings.ActiveProfile = profile.Name; }
            }
            ProfileStore.Save(profiles);
            SaveSettings();
            ShowSettings();
            Notice("Profil modifié.");
        }

        void DeleteProfile(UserProfile profile)
        {
            if (profile == null) return;
            if (profiles.Count <= 1) { ShowError("LyaranTV doit conserver au moins un profil."); return; }
            if (!RequireParentalUnlock()) return;
            if (!AskConfirmation("Supprimer le profil", "Supprimer le profil « " + profile.Name + " » ? Ses favoris, son historique et ses positions de reprise seront supprimés.", "Supprimer")) return;
            bool wasActive = profile == activeProfile;
            profiles.Remove(profile);
            StateStore.DeleteProfile(profile.Name);
            if (wasActive)
            {
                activeProfile = profiles[0];
                ProfileScope.Current = activeProfile.Name;
                settings.ActiveProfile = activeProfile.Name;
                LoadProfileState();
            }
            ProfileStore.Save(profiles);
            SaveSettings();
            RebuildSidebar(); UpdateStatusBar();
            ShowSettings();
            Notice("Profil supprimé.");
        }

        UserProfile ShowProfileDialog(UserProfile existing)
        {
            var dialog = new Window
            {
                Title = existing == null ? "Ajouter un profil" : "Modifier le profil",
                Width = 560, SizeToContent = SizeToContent.Height, MaxHeight = 720,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None, Background = Brushes.Transparent, AllowsTransparency = true,
                FontFamily = new FontFamily("Inter, Segoe UI"), ShowInTaskbar = false
            };
            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18), Padding = new Thickness(24)
            };
            dialog.Content = frame;
            var root = new StackPanel();
            frame.Child = root;
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 16) };
            var close = IconButton(Glyphs.Close, "Fermer", delegate { dialog.DialogResult = false; }, false);
            close.Width = 32; close.Height = 30; close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(Text(existing == null ? "Nouveau profil" : "Modifier le profil", 20, Brush(TextPrimary), true));
            root.Children.Add(header);

            root.Children.Add(FieldLabel("NOM DU PROFIL"));
            var nameBox = StyledTextBox();
            nameBox.Text = existing == null ? "" : existing.Name;
            nameBox.Margin = new Thickness(0, 4, 0, 12);
            root.Children.Add(nameBox);

            root.Children.Add(FieldLabel("COULEUR"));
            var colors = new WrapPanel { Margin = new Thickness(0, 6, 0, 12) };
            string selectedColor = existing == null ? "#8B5CF6" : (String.IsNullOrWhiteSpace(existing.AvatarColor) ? "#8B5CF6" : existing.AvatarColor);
            var swatches = new List<Border>();
            foreach (string hex in new[] { "#8B5CF6", "#22D3EE", "#3B82F6", "#10B981", "#F59E0B", "#F43F5E" })
            {
                Color color; if (!TryParseHexColor(hex, out color)) continue;
                string captured = hex;
                var swatch = new Border
                {
                    Width = 30, Height = 30, CornerRadius = new CornerRadius(9), Background = Brush(color),
                    BorderBrush = String.Equals(hex, selectedColor, StringComparison.OrdinalIgnoreCase) ? Brush(TextPrimary) : Brush(BorderColor),
                    BorderThickness = String.Equals(hex, selectedColor, StringComparison.OrdinalIgnoreCase) ? new Thickness(3) : new Thickness(1),
                    Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand
                };
                swatch.MouseLeftButtonUp += delegate
                {
                    selectedColor = captured;
                    foreach (var item in swatches)
                    {
                        item.BorderBrush = Brush(BorderColor); item.BorderThickness = new Thickness(1);
                    }
                    swatch.BorderBrush = Brush(TextPrimary); swatch.BorderThickness = new Thickness(3);
                };
                swatches.Add(swatch);
                colors.Children.Add(swatch);
            }
            root.Children.Add(colors);

            var child = new CheckBox
            {
                Content = Text("Profil enfant · les catégories verrouillées sont masquées", 12, Brush(TextPrimary), false),
                IsChecked = existing != null && existing.IsChild,
                Foreground = Brush(TextPrimary), Template = ModernCheckBoxTemplate(), Margin = new Thickness(0, 0, 0, 12),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            root.Children.Add(child);

            root.Children.Add(FieldLabel("CODE PIN (FACULTATIF)"));
            var pinBox = StyledPasswordBox();
            pinBox.Margin = new Thickness(0, 4, 0, 6);
            pinBox.ToolTip = "4 caractères ou plus. Laissez vide pour ne pas changer.";
            root.Children.Add(pinBox);
            var removePin = new CheckBox
            {
                Content = Text("Supprimer le code PIN existant", 11, Brush(Muted), false),
                Foreground = Brush(TextPrimary), Template = ModernCheckBoxTemplate(), Margin = new Thickness(0, 0, 0, 10),
                Visibility = existing != null && !String.IsNullOrWhiteSpace(existing.PinProtected) ? Visibility.Visible : Visibility.Collapsed,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            root.Children.Add(removePin);

            var error = Text("", 10.5, Brush(Danger), false);
            error.Visibility = Visibility.Collapsed;
            error.Margin = new Thickness(0, 0, 0, 8);
            root.Children.Add(error);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = Button("Annuler", delegate { dialog.DialogResult = false; }, false);
            cancel.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(cancel);
            UserProfile result = null;
            var save = Button(Glyphs.Save, "Enregistrer", delegate
            {
                string name = nameBox.Text.Trim();
                if (name.Length == 0) { error.Text = "Donnez un nom au profil."; error.Visibility = Visibility.Visible; return; }
                if (name.Length > 32) { error.Text = "Le nom ne doit pas dépasser 32 caractères."; error.Visibility = Visibility.Visible; return; }
                string pin = (pinBox.Password ?? "").Trim();
                if (pin.Length > 0 && pin.Length < 4) { error.Text = "Le code PIN doit contenir au moins 4 caractères."; error.Visibility = Visibility.Visible; return; }
                string protectedPin = existing == null ? "" : (existing.PinProtected ?? "");
                if (removePin.IsChecked == true) protectedPin = "";
                else if (pin.Length > 0) protectedPin = CredentialVault.Protect(pin);
                result = new UserProfile
                {
                    Name = name, AvatarColor = selectedColor, IsChild = child.IsChecked == true,
                    PinProtected = protectedPin, Created = existing == null ? DateTime.Now.ToString("s") : existing.Created
                };
                dialog.DialogResult = true;
            }, true);
            actions.Children.Add(save);
            root.Children.Add(actions);

            nameBox.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); e.Handled = true; } };
            dialog.Loaded += delegate { nameBox.Focus(); nameBox.SelectAll(); };
            return dialog.ShowDialog() == true ? result : null;
        }

        string PromptPin(string title, string message)
        {
            var dialog = new Window
            {
                Title = title, Width = 440, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None, Background = Brushes.Transparent, AllowsTransparency = true,
                FontFamily = new FontFamily("Inter, Segoe UI"), ShowInTaskbar = false
            };
            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16), Padding = new Thickness(22)
            };
            dialog.Content = frame;
            var root = new StackPanel();
            frame.Child = root;
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
            var close = IconButton(Glyphs.Close, "Annuler", delegate { dialog.DialogResult = false; }, false);
            close.Width = 30; close.Height = 28; close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(Text(title, 18, Brush(TextPrimary), true));
            root.Children.Add(header);
            var body = Text(message, 11.5, Brush(Muted), false);
            body.Margin = new Thickness(0, 0, 0, 10);
            root.Children.Add(body);
            var pinBox = StyledPasswordBox();
            pinBox.Margin = new Thickness(0, 0, 0, 8);
            root.Children.Add(pinBox);
            var error = Text("", 10.5, Brush(Danger), false);
            error.Visibility = Visibility.Collapsed;
            root.Children.Add(error);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var cancel = Button("Annuler", delegate { dialog.DialogResult = false; }, false);
            cancel.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(cancel);
            var ok = Button(Glyphs.Lock, "Déverrouiller", delegate
            {
                if (String.IsNullOrWhiteSpace(pinBox.Password)) { error.Text = "Saisissez le code PIN."; error.Visibility = Visibility.Visible; return; }
                dialog.DialogResult = true;
            }, true);
            actions.Children.Add(ok);
            root.Children.Add(actions);
            pinBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { ok.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); e.Handled = true; }
                else if (e.Key == Key.Escape) { dialog.DialogResult = false; e.Handled = true; }
            };
            dialog.Loaded += delegate { pinBox.Focus(); };
            return dialog.ShowDialog() == true ? pinBox.Password : null;
        }

        void ExportPortableJson()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Configuration LyaranTV (*.json)|*.json",
                FileName = "lyarantv-configuration-" + DateTime.Now.ToString("yyyy-MM-dd") + ".json"
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var data = StateStore.BuildPortable(settings, sources, favorites, recentIds, positions, profiles, hiddenChannels, hiddenGroups);
                StateStore.WritePortable(dialog.FileName, data);
                ShowToast("Configuration exportée. Les mots de passe et codes PIN ne sont pas inclus.", false, true);
            }
            catch (Exception ex) { ShowError("Export impossible : " + ex.Message); }
        }

        void ImportPortableJson()
        {
            var dialog = new OpenFileDialog { Filter = "Configuration LyaranTV (*.json)|*.json|Tous les fichiers (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            if (!AskConfirmation("Importer une configuration", "Les playlists, favoris, profils et réglages actuels seront remplacés. Les mots de passe devront être saisis à nouveau. Continuer ?", "Importer")) return;
            try
            {
                var data = StateStore.ReadPortable(dialog.FileName);
                var oldSettings = settings;
                var oldSources = sources.ToList();
                var oldProfiles = profiles.ToList();
                settings = data.Settings;
                if (String.IsNullOrWhiteSpace(settings.TmdbApiKey)) settings.TmdbApiKey = oldSettings.TmdbApiKey;
                if (String.IsNullOrWhiteSpace(settings.OpenSubtitlesKey)) settings.OpenSubtitlesKey = oldSettings.OpenSubtitlesKey;
                if (String.IsNullOrWhiteSpace(settings.ParentalPinProtected)) settings.ParentalPinProtected = oldSettings.ParentalPinProtected;
                settings.Normalize();

                profiles = data.Profiles.Count > 0 ? data.Profiles : new List<UserProfile>();
                if (profiles.Count == 0) profiles.Add(new UserProfile { Name = "Principal", AvatarColor = "#8B5CF6", Created = DateTime.Now.ToString("s") });
                foreach (var profile in profiles)
                {
                    if (String.IsNullOrWhiteSpace(profile.Name)) profile.Name = "Principal";
                    if (String.IsNullOrWhiteSpace(profile.AvatarColor)) profile.AvatarColor = "#8B5CF6";
                    if (String.IsNullOrWhiteSpace(profile.PinProtected))
                    {
                        var old = oldProfiles.FirstOrDefault(p => String.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
                        if (old != null) profile.PinProtected = old.PinProtected;
                    }
                }
                activeProfile = profiles.FirstOrDefault(p => String.Equals(p.Name, settings.ActiveProfile, StringComparison.OrdinalIgnoreCase)) ?? profiles[0];
                settings.ActiveProfile = activeProfile.Name;
                ProfileScope.Current = activeProfile.Name;

                sources.Clear();
                foreach (var item in data.Sources)
                {
                    var source = new PlaylistSource
                    {
                        Name = item.Name, Type = item.Type, Url = item.Url, Username = item.Username,
                        FilePath = item.FilePath, EpgUrl = item.EpgUrl, DeviceType = item.DeviceType, IsDemo = item.IsDemo
                    };
                    var old = oldSources.FirstOrDefault(s => String.Equals(s.Name, source.Name, StringComparison.OrdinalIgnoreCase));
                    if (old != null) { source.Password = old.Password; source.MacAddress = old.MacAddress; }
                    sources.Add(source);
                }
                favorites.Clear(); foreach (string value in data.Favorites) favorites.Add(value);
                recentIds.Clear(); recentIds.AddRange(data.Recent);
                positions.Clear(); foreach (var pair in data.Positions) positions[pair.Key] = pair.Value;
                hiddenChannels.Clear(); foreach (string value in data.HiddenChannels) hiddenChannels.Add(value);
                hiddenGroups.Clear(); foreach (string value in data.HiddenGroups) hiddenGroups.Add(value);

                ProfileStore.Save(profiles);
                SourceStore.Save(sources);
                SaveState();
                SaveSettings();
                ReloadEverything();
                ShowToast("Configuration importée. Vérifiez les mots de passe des playlists.", false, true);
            }
            catch (Exception ex) { ShowError("Import impossible : " + ex.Message); }
        }

        static Color TextOnColor(Color color)
        {
            double brightness = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
            return brightness > 165 ? Color.FromRgb(20, 24, 31) : Colors.White;
        }
    }
}
