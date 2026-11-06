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
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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
            var root = PageRoot("Paramètres", "Apparence, lecture, profils et données.");

            root.Children.Add(SettingsCard("Apparence", "Thème, accent et animations.",
                BuildAppearanceSettings()));
            root.Children.Add(SettingsCard("Lecture", "Lecteur intégré et médias.",
                BuildPlaybackSettings()));
            root.Children.Add(SettingsCard("Profils et contrôle parental", "Un espace et une photo par personne.",
                BuildProfileSettings()));
            root.Children.Add(SettingsCard("Métadonnées et sous-titres", "Affiches et sous-titres, en option.",
                BuildTmdbSettings()));
            root.Children.Add(SettingsCard("Mode langue", "N’afficher que les catégories de la langue choisie.",
                BuildLanguageModeSettings()));
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
            var row = new Grid { Margin = new Thickness(0, 7, 0, 7), MinHeight = 34 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0) };
            labels.Children.Add(Text(label, 12.5, Brush(TextPrimary), false));
            if (!String.IsNullOrWhiteSpace(hint))
            {
                var note = Text(hint, 10.2, Brush(Muted), false);
                note.Margin = new Thickness(0, 3, 0, 0);
                note.LineHeight = 17;
                labels.Children.Add(note);
            }
            row.Children.Add(labels);
            if (control != null)
            {
                var element = control as FrameworkElement;
                if (element != null)
                {
                    element.VerticalAlignment = VerticalAlignment.Center;
                    if (element.Margin.Left < 24)
                        element.Margin = new Thickness(24, element.Margin.Top, element.Margin.Right, element.Margin.Bottom);
                }
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
            var accents = new WrapPanel { Margin = new Thickness(0, 8, 0, 2) };
            string[] accentColors = { "#8B5CF6", "#22D3EE", "#3B82F6", "#10B981", "#F59E0B", "#F43F5E", "#EC4899", "#94A3B8" };
            foreach (string hex in accentColors)
            {
                Color parsed;
                if (!TryParseHexColor(hex, out parsed)) continue;
                bool active = String.Equals(hex, accentHex, StringComparison.OrdinalIgnoreCase);
                accents.Children.Add(AppearanceSwatch(parsed, active, "Accent " + hex, delegate { ChangeAccent(parsed); }));
            }
            stack.Children.Add(accents);

            var custom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 14) };
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

            stack.Children.Add(Text("COULEUR DU DÉGRADÉ", 9.5, Brush(Muted), true));
            var secondaries = new WrapPanel { Margin = new Thickness(0, 8, 0, 2) };
            foreach (string hex in accentColors)
            {
                Color parsed;
                if (!TryParseHexColor(hex, out parsed)) continue;
                bool active = String.Equals(hex, accentSecondaryHex, StringComparison.OrdinalIgnoreCase);
                secondaries.Children.Add(AppearanceSwatch(parsed, active, "Dégradé " + hex, delegate { ChangeSecondaryAccent(parsed); }));
            }
            stack.Children.Add(secondaries);
            var gradientNote = Text("Deuxième teinte des boutons, des halos et des accents secondaires. Choisissez-la proche de l’accent pour un dégradé subtil, ou opposée pour un rendu plus vibrant.", 10.2, Brush(Muted), false);
            gradientNote.Margin = new Thickness(0, 0, 0, 16);
            gradientNote.MaxWidth = 720;
            gradientNote.HorizontalAlignment = HorizontalAlignment.Left;
            stack.Children.Add(gradientNote);

            stack.Children.Add(Text("AMBIANCE", 9.5, Brush(Muted), true));
            var ambienceRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 12) };
            foreach (string value in new[] { "Discrète", "Équilibrée", "Vive" })
            {
                string captured = value;
                var button = Button(ambience == captured ? Glyphs.Check : "", captured, delegate { ChangeAmbience(captured); }, ambience == captured);
                button.MinWidth = 132;
                button.Height = 38;
                ambienceRow.Children.Add(button);
            }
            stack.Children.Add(ambienceRow);

            stack.Children.Add(SettingToggle("Teinter les surfaces avec l’accent", tintedBackground, delegate(bool value)
            {
                tintedBackground = value;
                SaveSettings();
                ChangeTheme(themeName);
            }));
            stack.Children.Add(SettingToggle("Réduire les animations et les effets de mouvement", settings.ReduceMotion, delegate(bool value)
            {
                settings.ReduceMotion = value;
                UiMotion.Reduced = value;
                SaveSettings();
                Notice(value ? "Animations réduites." : "Animations complètes activées.");
            }));
            stack.Children.Add(SettingToggle("Sons d’interface discrets", settings.UiSounds, delegate(bool value)
            {
                settings.UiSounds = value;
                UiSound.Enabled = value;
                SaveSettings();
                if (value) UiSound.Confirm();
                Notice(value ? "Sons d’interface activés." : "Sons d’interface coupés.");
            }));
            stack.Children.Add(Text("LyaranTV respecte le thème clair/sombre de Windows lorsque « Automatique » est sélectionné.", 10.2, Brush(Muted), false));
            return stack;
        }

        /// <summary>
        /// Pastille d'apparence : anneau net autour de la couleur, coche sur la
        /// teinte active et halo discret au survol.
        /// </summary>
        Border AppearanceSwatch(Color color, bool active, string tooltip, Action click)
        {
            var check = Icon(Glyphs.Check, 13, Brush(TextOnColor(color)));
            check.HorizontalAlignment = HorizontalAlignment.Center;
            check.VerticalAlignment = VerticalAlignment.Center;
            check.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            var inner = new Border
            {
                CornerRadius = new CornerRadius(13), Background = Brush(color),
                Child = check, Margin = new Thickness(active ? 3 : 2)
            };
            var outer = new Border
            {
                Width = 34, Height = 34, CornerRadius = new CornerRadius(17),
                Background = active ? Brush(TextPrimary) : Brush(BorderColor),
                Padding = new Thickness(active ? 2 : 1),
                Margin = new Thickness(0, 0, 9, 8), Cursor = Cursors.Hand,
                ToolTip = tooltip, Child = inner, SnapsToDevicePixels = true
            };
            outer.MouseEnter += delegate
            {
                outer.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 14, ShadowDepth = 0, Opacity = 0.45, Color = color
                };
            };
            outer.MouseLeave += delegate { outer.Effect = null; };
            outer.MouseLeftButtonUp += delegate { click(); };
            return outer;
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
            foreach (var profile in profiles.ToList())
            {
                var captured = profile;
                var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Color avatar; if (!TryParseHexColor(profile.AvatarColor, out avatar)) avatar = Accent;
                var circle = ProfileAvatars.Build(profile, 32, 16, avatar, false);
                circle.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(circle, 0); row.Children.Add(circle);
                var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 0, 8, 0) };
                var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
                nameRow.Children.Add(Text(profile.Name, 12.5, Brush(profile == activeProfile ? Accent : TextPrimary), profile == activeProfile));
                if (profile == activeProfile)
                {
                    var activePill = new Border
                    {
                        Child = Text("ACTIF", 10, Brush(TextOnAccentColor()), true),
                        Background = Brush(WithAlpha(Accent, 34)), BorderBrush = Brush(WithAlpha(Accent, 90)),
                        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(6, 1, 6, 2), Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    nameRow.Children.Add(activePill);
                }
                labels.Children.Add(nameRow);
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

            // Sources d'affiches complémentaires : la liste explique clairement
            // lesquelles demandent une clé et lesquelles restent sans compte.
            stack.Children.Add(BuildArtworkSourcesCard());

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
            stack.Children.Add(SettingsRow("Taille du cache local", TextUtil.Count(TmdbCacheStore.Count, "fiche") + " en cache actuellement.", cacheLimit));

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

            var attribution = Text(TmdbClient.Attribution + " " + ArtworkResolver.Attribution + " (" + TextUtil.Count(ArtworkStore.Count, "fiche") + " en cache). Les métadonnées restent facultatives : sans réseau, chaque contenu garde une vignette dessinée à partir de son titre.", 9.8, Brush(Muted), false);
            attribution.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(attribution);
            return stack;
        }

        /// <summary>
        /// Carte « Sources d'affiches » : catalogue lisible, état de chaque
        /// source et champs de clés pour OMDb, Fanart.tv et TheTVDB.
        /// </summary>
        StackPanel BuildArtworkSourcesCard()
        {
            var stack = new StackPanel();
            stack.Children.Add(Text("Sources d'affiches", 14, Brush(TextPrimary), true));
            var intro = Text("LyaranTV combine plusieurs sources. TMDb, OMDb, Fanart.tv et TheTVDB demandent une clé personnelle ; TVmaze, IMDb, iTunes et Wikipédia fonctionnent sans compte. ThePosterDB, MoviePosterDB, IMP Awards et Cinematerial restent des sources de recherche manuelle.", 10.5, Brush(Muted), false);
            intro.TextWrapping = TextWrapping.Wrap;
            intro.Margin = new Thickness(0, 5, 0, 10);
            stack.Children.Add(intro);

            foreach (var source in ArtworkResolver.Catalog)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 7) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var left = new StackPanel();
                var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
                titleRow.Children.Add(Text(source.Name, 11.5, Brush(TextPrimary), true));
                var badge = new Border
                {
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(8, 0, 0, 0),
                    Background = source.RequiresKey ? Brush(WithAlpha(Accent, 36)) : Brush(WithAlpha(Success, 26)),
                    BorderBrush = source.RequiresKey ? Brush(WithAlpha(Accent, 90)) : Brush(WithAlpha(Success, 70)),
                    BorderThickness = new Thickness(1),
                    Child = Text(source.RequiresKey ? "clé requise" : "sans clé", 9.5,
                        source.RequiresKey ? Brush(Accent) : Brush(Success), true)
                };
                titleRow.Children.Add(badge);
                left.Children.Add(titleRow);
                var note = Text(source.Note, 10, Brush(Muted), false);
                note.TextWrapping = TextWrapping.Wrap;
                note.Margin = new Thickness(0, 2, 0, 0);
                left.Children.Add(note);
                Grid.SetColumn(left, 0);
                row.Children.Add(left);

                bool ready = !source.RequiresKey || ArtworkResolver.HasKey(source.Setting);
                var state = Text(ready ? (source.RequiresKey ? "Prête" : "Active") : "À configurer", 10,
                    ready ? Brush(Success) : Brush(WithAlpha(Muted, 220)), true);
                state.VerticalAlignment = VerticalAlignment.Center;
                state.Margin = new Thickness(12, 0, 0, 0);
                Grid.SetColumn(state, 1);
                row.Children.Add(state);
                stack.Children.Add(row);
            }

            stack.Children.Add(new Border { Height = 1, Background = Brush(WithAlpha(BorderColor, 150)), Margin = new Thickness(0, 6, 0, 12) });
            stack.Children.Add(ArtworkKeyEditor("omdb", "Clé API OMDb", "Gratuite sur omdbapi.com. Complète les affiches et les notes IMDb quand les sources publiques ne trouvent rien.", settings.OmdbApiKey, "LYARANTV_OMDB_KEY"));
            stack.Children.Add(ArtworkKeyEditor("fanart", "Clé API Fanart.tv", "Gratuite sur fanart.tv. Fournit des affiches et arrière-plans haute définition.", settings.FanartApiKey, "LYARANTV_FANART_KEY"));
            stack.Children.Add(ArtworkKeyEditor("tvdb", "Clé API TheTVDB", "Gratuite sur thetvdb.com. Utile pour les séries anciennes ou peu référencées.", settings.TvdbApiKey, "LYARANTV_TVDB_KEY"));
            return stack;
        }

        UIElement ArtworkKeyEditor(string setting, string label, string hint, string current, string environment)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            var box = StyledPasswordBox();
            box.Width = 300;
            box.Password = current ?? "";
            box.Margin = new Thickness(0, 0, 7, 0);
            box.ToolTip = "Stockée localement dans vos réglages, jamais incluse dans LyaranTV.";
            row.Children.Add(box);
            row.Children.Add(Button(Glyphs.Save, "Enregistrer", delegate
            {
                string value = (box.Password ?? "").Trim();
                if (setting == "omdb") settings.OmdbApiKey = value;
                else if (setting == "fanart") settings.FanartApiKey = value;
                else if (setting == "tvdb") settings.TvdbApiKey = value;
                SaveSettings();
                ArtworkResolver.Configure(settings);
                ShowSettings();
                Notice(value.Length == 0
                    ? label + " effacée. La source reste désactivée."
                    : label + " enregistrée. Les prochaines affiches pourront l'utiliser.");
            }, false));
            var editor = SettingsRow(label, hint + " Variable possible : " + environment + ".", row);
            if (String.IsNullOrWhiteSpace(current) && !String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environment)))
            {
                var note = Text("Une clé est fournie par la variable d’environnement " + environment + ".", 10, Brush(Success), false);
                note.Margin = new Thickness(0, 0, 0, 8);
                var wrapper = new StackPanel();
                wrapper.Children.Add(editor);
                wrapper.Children.Add(note);
                return wrapper;
            }
            return editor;
        }

        StackPanel BuildLanguageModeSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(SettingToggle("Activer le mode langue", settings.LanguageModeEnabled, delegate(bool value)
            {
                settings.LanguageModeEnabled = value;
                SaveSettings();
                RebuildSidebar();
                UpdateStatusBar();
                ShowToast(value
                    ? "Mode langue activé · seules les catégories contenant « " + settings.LanguageModeCode + " » sont affichées."
                    : "Mode langue désactivé · toutes les catégories sont affichées.", false, true);
            }));
            var code = StyledTextBox();
            code.Width = 120;
            code.CharacterCasing = CharacterCasing.Upper;
            code.Text = String.IsNullOrWhiteSpace(settings.LanguageModeCode) ? "FR" : settings.LanguageModeCode;
            code.ToolTip = "Code écrit dans le nom des catégories : FR, EN, AR, ES…";
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(code);
            var apply = Button(Glyphs.Check, "Appliquer", delegate
            {
                string value = (code.Text ?? "").Trim().ToUpperInvariant();
                if (value.Length == 0) { ShowToast("Saisissez un code de langue (FR, EN…).", true, false); return; }
                settings.LanguageModeCode = value;
                SaveSettings();
                RebuildSidebar();
                UpdateStatusBar();
                ShowSettings();
                ShowToast("Mode langue · catégories contenant « " + value + " ».", false, true);
            }, true);
            apply.Margin = new Thickness(7, 0, 0, 0);
            row.Children.Add(apply);
            var clear = Button(Glyphs.View, "Tout afficher", delegate
            {
                settings.LanguageModeEnabled = false;
                SaveSettings();
                RebuildSidebar();
                UpdateStatusBar();
                ShowSettings();
                ShowToast("Toutes les catégories sont de nouveau visibles.", false, true);
            }, false);
            clear.Margin = new Thickness(7, 0, 0, 0);
            row.Children.Add(clear);
            stack.Children.Add(SettingsRow("Code de langue", "Les catégories dont le nom contient ce code restent visibles ; le mode reste actif en changeant de page.", row));

            int total = 0, matching = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var channel in channels)
            {
                if (channel == null) continue;
                string group = String.IsNullOrWhiteSpace(channel.Group) ? "Non classé" : channel.Group;
                if (!seen.Add(group)) continue;
                total++;
                if (TextUtil.Contains(group, String.IsNullOrWhiteSpace(settings.LanguageModeCode) ? "FR" : settings.LanguageModeCode)) matching++;
            }
            var count = Text(channels.Count == 0
                ? "Chargez une playlist pour voir les catégories correspondantes."
                : TextUtil.Count(matching, "catégorie", "catégories") + " sur " + total + " contiennent « " + settings.LanguageModeCode + " »."
                    + (settings.LanguageModeEnabled ? "  ·  Mode actif." : "  ·  Mode désactivé."), 10, Brush(Muted), false);
            count.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(count);
            return stack;
        }

        StackPanel BuildStartupSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(SettingToggle("Afficher « Qui regarde ? » au démarrage", !settings.HideProfilePicker, delegate(bool value)
            {
                settings.HideProfilePicker = !value;
                SaveSettings();
                Notice(value ? "Le choix du profil s’affichera au prochain lancement." : "LyaranTV ouvrira directement le dernier profil utilisé.");
            }));
            stack.Children.Add(SettingToggle("Navigation manette / télécommande", settings.TvNavigation, delegate(bool value)
            {
                settings.TvNavigation = value;
                SaveSettings();
                Notice(value ? "Navigation salon activée : croix directionnelle, A/B et clavier virtuel." : "Navigation salon désactivée.");
            }));
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
            var imagesNote = Text("Les affiches de films et de séries, les vignettes et les logos de chaînes sont chargés à la demande depuis leurs sources en ligne (TMDb, IMDb, TVmaze, iTunes ou URL de la playlist). Ils sont conservés dans un cache disque borné sous %APPDATA%\\LyaranTV\\covers (purge après 45 jours, environ 1 200 images) et se vident avec le bouton ci-dessus.", 9.8, Brush(Muted), false);
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
                ? I18n.T("Aucun jeton requis pour un dépôt public.")
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
                + " sans jeton si elles sont publiques ; le fichier téléchargé est comparé à son empreinte SHA-256 avant exécution. Si le dépôt reste privé, un jeton GitHub autorisé est nécessaire.", 9.8, Brush(Muted), false);
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
            // Étape obligatoire : chaque nouveau profil possède son propre compte.
            var account = ShowAccountOnboarding(created);
            if (account == null)
            {
                Notice("Création annulée : un compte IPTV est requis pour un nouveau profil.");
                return;
            }
            allSources.Add(account);
            SaveSources();
            profiles.Add(created);
            ProfileStore.Save(profiles);
            ShowSettings();
            Notice("Profil « " + created.Name + " » ajouté avec son compte IPTV.");
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
            profile.AvatarImage = updated.AvatarImage;
            profile.AvatarPreset = updated.AvatarPreset;
            profile.IsChild = updated.IsChild;
            profile.PinProtected = updated.PinProtected;
            if (!String.Equals(oldName, profile.Name, StringComparison.Ordinal))
            {
                StateStore.RenameProfile(oldName, profile.Name);
                // Les comptes suivent le profil renommé, sans changer de propriétaire.
                foreach (var source in allSources)
                    if (BelongsToProfile(source, oldName)) source.Profile = profile.Name;
                SaveSources();
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
            // Les comptes IPTV du profil supprimé ne doivent pas rester
            // accessibles aux autres profils.
            allSources.RemoveAll(s => BelongsToProfile(s, profile.Name));
            SaveSources();
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
                Width = 640, SizeToContent = SizeToContent.Height, MaxHeight = 780,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None, Background = Brushes.Transparent, AllowsTransparency = true,
                FontFamily = new FontFamily("Inter, Segoe UI"), ShowInTaskbar = false
            };
            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20), Padding = new Thickness(26), Opacity = 0
            };
            frame.RenderTransform = new ScaleTransform(0.985, 0.985);
            frame.RenderTransformOrigin = new Point(0.5, 0.5);
            dialog.Content = frame;

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 716, Focusable = false
            };
            var root = new StackPanel();
            scroll.Content = root;
            frame.Child = scroll;

            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 18) };
            var close = IconButton(Glyphs.Close, "Fermer", delegate { dialog.DialogResult = false; }, false);
            close.Width = 32; close.Height = 30; close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var heading = new StackPanel();
            heading.Children.Add(Text(existing == null ? "Nouveau profil" : "Modifier le profil", 21, Brush(TextPrimary), true));
            heading.Children.Add(Text("Une identité, une photo, un espace rien qu'à vous.", 12.5, Brush(Muted), false));
            header.Children.Add(heading);
            root.Children.Add(header);

            string selectedColor = existing == null ? "#8B5CF6" : (String.IsNullOrWhiteSpace(existing.AvatarColor) ? "#8B5CF6" : existing.AvatarColor);
            string selectedImage = existing == null ? "" : (existing.AvatarImage ?? "");
            string selectedPreset = existing == null ? "" : (existing.AvatarPreset ?? "");
            Color accentColor;
            if (!TryParseHexColor(selectedColor, out accentColor)) accentColor = Accent;

            var hero = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var preview = new Border
            {
                Width = 104, Height = 104, Padding = new Thickness(5), CornerRadius = new CornerRadius(26),
                Background = Brush(WithAlpha(accentColor, 18)), BorderBrush = Brush(WithAlpha(accentColor, 110)),
                BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0)
            };
            hero.Children.Add(preview);
            var fields = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
            fields.Children.Add(FieldLabel("NOM DU PROFIL"));
            var nameBox = StyledTextBox();
            nameBox.Text = existing == null ? "" : existing.Name;
            nameBox.Margin = new Thickness(0, 5, 0, 12);
            fields.Children.Add(nameBox);
            fields.Children.Add(FieldLabel("COULEUR D'ACCENT"));
            var colors = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
            var swatches = new List<Border>();
            Action refresh = null;
            foreach (string hex in new[] { "#8B5CF6", "#22D3EE", "#3B82F6", "#10B981", "#F59E0B", "#F43F5E" })
            {
                Color color; if (!TryParseHexColor(hex, out color)) continue;
                string captured = hex;
                var swatch = new Border
                {
                    Width = 28, Height = 28, CornerRadius = new CornerRadius(9), Background = Brush(color),
                    BorderBrush = String.Equals(hex, selectedColor, StringComparison.OrdinalIgnoreCase) ? Brush(TextPrimary) : Brush(BorderColor),
                    BorderThickness = String.Equals(hex, selectedColor, StringComparison.OrdinalIgnoreCase) ? new Thickness(3) : new Thickness(1),
                    Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand
                };
                swatch.MouseLeftButtonUp += delegate
                {
                    selectedColor = captured;
                    if (!TryParseHexColor(selectedColor, out accentColor)) accentColor = Accent;
                    foreach (var item in swatches)
                    {
                        item.BorderBrush = Brush(BorderColor); item.BorderThickness = new Thickness(1);
                    }
                    swatch.BorderBrush = Brush(TextPrimary); swatch.BorderThickness = new Thickness(3);
                    if (refresh != null) refresh();
                };
                swatches.Add(swatch);
                colors.Children.Add(swatch);
            }
            fields.Children.Add(colors);
            Grid.SetColumn(fields, 1); hero.Children.Add(fields);
            root.Children.Add(hero);

            root.Children.Add(FieldLabel("PHOTO DE PROFIL"));
            var gallery = new WrapPanel { Margin = new Thickness(0, 8, 0, 2) };
            var tiles = new List<Action>();
            var error = Text("", 12.5, Brush(Danger), false);
            error.Visibility = Visibility.Collapsed;
            error.Margin = new Thickness(0, 0, 0, 8);

            Action<string, string> addTile = delegate(string preset, string image)
            {
                string capturedPreset = preset;
                bool isInitial = String.IsNullOrEmpty(preset) && String.IsNullOrEmpty(image);
                var ring = new Border
                {
                    Width = 50, Height = 50, Padding = new Thickness(3), CornerRadius = new CornerRadius(13),
                    BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent, Background = Brushes.Transparent
                };
                var tileButton = new Button
                {
                    Content = ring, Width = 54, Height = 54, Padding = new Thickness(0), Margin = new Thickness(0, 0, 2, 2),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                    FocusVisualStyle = null, Template = RoundedButtonTemplate(13, false, false),
                    ToolTip = isInitial ? "Initiale du profil" : ProfileAvatars.Label(capturedPreset)
                };
                tileButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,
                    isInitial ? "Avatar initiale" : "Avatar " + ProfileAvatars.Label(capturedPreset));
                Action update = delegate
                {
                    bool selected = String.IsNullOrEmpty(selectedImage) && String.Equals(selectedPreset, capturedPreset, StringComparison.Ordinal);
                    ring.BorderBrush = selected ? Brush(accentColor) : Brushes.Transparent;
                    ring.Effect = selected ? new DropShadowEffect { BlurRadius = 20, ShadowDepth = 0, Opacity = 0.42, Color = accentColor } : null;
                    ring.Child = ProfileAvatars.Build(
                        new UserProfile { Name = nameBox.Text, AvatarColor = selectedColor, AvatarImage = "", AvatarPreset = capturedPreset },
                        44, 12, Accent, false);
                };
                tileButton.Click += delegate
                {
                    selectedPreset = capturedPreset;
                    selectedImage = "";
                    if (refresh != null) refresh();
                };
                tiles.Add(update);
                update();
                gallery.Children.Add(tileButton);
            };

            addTile("", "");
            foreach (string preset in ProfileAvatars.Presets)
                addTile(ProfileAvatars.PresetValue(preset), "");

            var importRing = new Border
            {
                Width = 50, Height = 50, Padding = new Thickness(3), CornerRadius = new CornerRadius(13),
                BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent, Background = Brushes.Transparent
            };
            var importButton = new Button
            {
                Content = importRing, Width = 54, Height = 54, Padding = new Thickness(0), Margin = new Thickness(0, 0, 2, 2),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                FocusVisualStyle = null, Template = RoundedButtonTemplate(13, false, false),
                ToolTip = "Importer une photo (PNG, JPG ou BMP)"
            };
            importButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Importer une photo");
            var importFrame = new System.Windows.Shapes.Rectangle
            {
                RadiusX = 12, RadiusY = 12, StrokeThickness = 1.3, Stroke = Brush(WithAlpha(TextPrimary, 55)),
                Fill = Brushes.Transparent, StrokeDashArray = new DoubleCollection { 3, 3 }
            };
            var importGlyph = Icon(Glyphs.Camera, 18, Brush(Muted));
            importGlyph.HorizontalAlignment = HorizontalAlignment.Center;
            importGlyph.VerticalAlignment = VerticalAlignment.Center;
            var importHost = new Grid();
            importHost.Children.Add(importFrame);
            importHost.Children.Add(importGlyph);
            importRing.Child = importHost;
            Action updateImport = delegate
            {
                bool selected = !String.IsNullOrEmpty(selectedImage);
                importRing.BorderBrush = selected ? Brush(accentColor) : Brushes.Transparent;
                importRing.Effect = selected ? new DropShadowEffect { BlurRadius = 20, ShadowDepth = 0, Opacity = 0.42, Color = accentColor } : null;
                if (selected)
                {
                    importRing.Child = ProfileAvatars.Build(
                        new UserProfile { Name = nameBox.Text, AvatarColor = selectedColor, AvatarImage = selectedImage, AvatarPreset = "" },
                        44, 12, Accent, false);
                }
                else
                {
                    importRing.Child = importHost;
                }
            };
            importButton.Click += delegate
            {
                var dialogFile = new OpenFileDialog
                {
                    Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Tous les fichiers (*.*)|*.*",
                    Title = "Choisir une photo de profil"
                };
                if (dialogFile.ShowDialog() != true) return;
                string importError;
                string encoded = ProfileAvatars.Import(dialogFile.FileName, out importError);
                if (encoded == null)
                {
                    error.Text = importError;
                    error.Visibility = Visibility.Visible;
                    return;
                }
                selectedImage = encoded;
                selectedPreset = "";
                error.Visibility = Visibility.Collapsed;
                if (refresh != null) refresh();
            };
            tiles.Add(updateImport);
            updateImport();
            gallery.Children.Add(importButton);
            root.Children.Add(gallery);

            var removePhoto = TextAction("Retirer la photo", delegate
            {
                selectedImage = "";
                selectedPreset = "";
                if (refresh != null) refresh();
            }, "Revenir à l'initiale ou à un avatar prédéfini");
            removePhoto.Margin = new Thickness(0, 2, 0, 8);
            removePhoto.HorizontalAlignment = HorizontalAlignment.Left;
            root.Children.Add(removePhoto);

            var child = new CheckBox
            {
                Content = Text("Profil enfant", 12.5, Brush(TextPrimary), false),
                IsChecked = existing != null && existing.IsChild,
                Foreground = Brush(TextPrimary), Template = ModernCheckBoxTemplate(), Margin = new Thickness(0, 2, 0, 4),
                HorizontalContentAlignment = HorizontalAlignment.Left, ToolTip = "Les catégories verrouillées sont masquées."
            };
            root.Children.Add(child);
            var childHint = Text("Les catégories verrouillées sont masquées.", 12.5, Brush(Muted), false);
            childHint.Margin = new Thickness(27, 0, 0, 12);
            root.Children.Add(childHint);

            root.Children.Add(FieldLabel("CODE PIN (FACULTATIF)"));
            var pinBox = StyledPasswordBox();
            pinBox.Margin = new Thickness(0, 5, 0, 5);
            pinBox.ToolTip = "4 caractères ou plus. Laissez vide pour ne pas changer.";
            root.Children.Add(pinBox);
            var removePin = new CheckBox
            {
                Content = Text("Supprimer le code PIN existant", 12.5, Brush(Muted), false),
                Foreground = Brush(TextPrimary), Template = ModernCheckBoxTemplate(), Margin = new Thickness(0, 0, 0, 10),
                Visibility = existing != null && !String.IsNullOrWhiteSpace(existing.PinProtected) ? Visibility.Visible : Visibility.Collapsed,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            root.Children.Add(removePin);

            root.Children.Add(error);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
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
                    Name = name, AvatarColor = selectedColor, AvatarImage = selectedImage, AvatarPreset = selectedPreset,
                    IsChild = child.IsChecked == true, PinProtected = protectedPin,
                    Created = existing == null ? DateTime.Now.ToString("s") : existing.Created
                };
                dialog.DialogResult = true;
            }, true);
            actions.Children.Add(save);
            root.Children.Add(actions);

            refresh = delegate
            {
                UserProfile draft = new UserProfile
                {
                    Name = nameBox.Text, AvatarColor = selectedColor, AvatarImage = selectedImage, AvatarPreset = selectedPreset
                };
                preview.Child = ProfileAvatars.Build(draft, 92, 23, Accent, true);
                preview.Background = Brush(WithAlpha(accentColor, 18));
                preview.BorderBrush = Brush(WithAlpha(accentColor, 110));
                preview.Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Opacity = 0.30, Color = accentColor };
                foreach (var update in tiles) update();
                removePhoto.Visibility = (String.IsNullOrEmpty(selectedImage) && String.IsNullOrEmpty(selectedPreset))
                    ? Visibility.Collapsed : Visibility.Visible;
            };
            refresh();

            nameBox.TextChanged += delegate { if (refresh != null) refresh(); };
            nameBox.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); e.Handled = true; } };
            dialog.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) dialog.DialogResult = false; };
            dialog.Loaded += delegate
            {
                nameBox.Focus(); nameBox.SelectAll();
                if (!UiMotion.Reduced)
                {
                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                    frame.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
                    var scale = frame.RenderTransform as ScaleTransform;
                    if (scale != null)
                    {
                        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
                        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
                    }
                }
                else frame.Opacity = 1;
            };
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
                var data = StateStore.BuildPortable(settings, allSources, favorites, recentIds, positions, profiles, hiddenChannels, hiddenGroups);
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
                    if (profile.AvatarImage == null || profile.AvatarImage.Length > 800000) profile.AvatarImage = "";
                    if (profile.AvatarPreset == null || ProfileAvatars.PresetId(profile.AvatarPreset).Length == 0) profile.AvatarPreset = "";
                    if (String.IsNullOrWhiteSpace(profile.PinProtected))
                    {
                        var old = oldProfiles.FirstOrDefault(p => String.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
                        if (old != null) profile.PinProtected = old.PinProtected;
                    }
                }
                activeProfile = profiles.FirstOrDefault(p => String.Equals(p.Name, settings.ActiveProfile, StringComparison.OrdinalIgnoreCase)) ?? profiles[0];
                settings.ActiveProfile = activeProfile.Name;
                ProfileScope.Current = activeProfile.Name;

                allSources.Clear();
                foreach (var item in data.Sources)
                {
                    var source = new PlaylistSource
                    {
                        Name = item.Name, Type = item.Type, Url = item.Url, Username = item.Username,
                        FilePath = item.FilePath, EpgUrl = item.EpgUrl, DeviceType = item.DeviceType, IsDemo = item.IsDemo,
                        Profile = String.IsNullOrWhiteSpace(item.Profile) ? activeProfile.Name : item.Profile
                    };
                    var old = oldSources.FirstOrDefault(s => String.Equals(s.Name, source.Name, StringComparison.OrdinalIgnoreCase));
                    if (old != null) { source.Password = old.Password; source.MacAddress = old.MacAddress; }
                    allSources.Add(source);
                }
                sources.Clear();
                sources.AddRange(allSources.Where(s => BelongsToProfile(s, activeProfile.Name)));
                favorites.Clear(); foreach (string value in data.Favorites) favorites.Add(value);
                recentIds.Clear(); recentIds.AddRange(data.Recent);
                positions.Clear(); foreach (var pair in data.Positions) positions[pair.Key] = pair.Value;
                hiddenChannels.Clear(); foreach (string value in data.HiddenChannels) hiddenChannels.Add(value);
                hiddenGroups.Clear(); foreach (string value in data.HiddenGroups) hiddenGroups.Add(value);

                ProfileStore.Save(profiles);
                SourceStore.Save(allSources);
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
