using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LyaranTV
{
    /// <summary>
    /// Fonctions de lecteur vidéo classique : vitesse, minuterie de veille,
    /// boucle, avance image par image et aide des raccourcis. Elles s'ajoutent
    /// aux commandes principales sans alourdir la barre de lecture.
    /// </summary>
    public sealed partial class MainWindow
    {
        double playbackSpeed = 1.0;
        bool loopPlayback;
        bool sleepAtEpisodeEnd;
        DateTime sleepDeadline = DateTime.MinValue;
        DispatcherTimer sleepTimer;
        Button playerSpeedButton, playerSleepButton, playerLoopButton;
        double abStart = -1, abEnd = -1;
        DateTime abLastJump = DateTime.MinValue;
        Border abRange;
        // Réglages d'image et de synchronisation, proches d'un lecteur de bureau.
        double subtitleDelay;
        double playerZoom = 1.0;
        int playerRotation;
        bool playerFlip;
        Border statsHud, nextEpisodeArt;
        TextBlock statsText;
        bool statsVisible;
        Button playerOptionsButton;
        bool playerChromeShown;

        Button PlayerLabelButton(string label, string tooltip, Action click)
        {
            var button = new Button
            {
                Content = Text(label, 11.5, Brushes.White, true),
                Height = 34, MinWidth = 48, Padding = new Thickness(9, 0, 9, 0),
                Background = Brush(Color.FromArgb(120, 12, 12, 18)),
                BorderBrush = Brush(Color.FromArgb(70, 255, 255, 255)), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, FocusVisualStyle = null, ToolTip = tooltip, IsTabStop = false,
                Template = RoundedButtonTemplate(9), HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0)
            };
            if (click != null) button.Click += delegate { click(); };
            return button;
        }

        string SpeedLabel(double speed)
        {
            if (Math.Abs(speed - 1.0) < 0.01) return "Normal (1×)";
            return speed.ToString("0.##", CultureInfo.CurrentCulture) + "×";
        }

        void ApplyPlaybackSpeed(double speed, bool announce)
        {
            if (speed < 0.5) speed = 0.5;
            if (speed > 2.0) speed = 2.0;
            playbackSpeed = speed;
            settings.PlaybackSpeed = speed;
            SaveSettings();
            if (player != null) { try { player.SpeedRatio = speed; } catch { } }
            UpdateSpeedButton();
            if (announce) ShowToast("Vitesse de lecture · " + SpeedLabel(speed), false, true);
            ShowFullscreenControls();
        }

        void UpdateSpeedButton()
        {
            if (playerSpeedButton == null) return;
            playerSpeedButton.Content = Text(
                Math.Abs(playbackSpeed - 1.0) < 0.01 ? "1×" : playbackSpeed.ToString("0.##", CultureInfo.CurrentCulture) + "×",
                11.5, Brushes.White, true);
            playerSpeedButton.ToolTip = "Vitesse de lecture · " + SpeedLabel(playbackSpeed) + "  ·  [ et ]";
        }

        void OpenSpeedMenu()
        {
            if (player == null || playerSpeedButton == null) return;
            ShowFullscreenControls();
            var menu = CreateContextMenu();
            menu.Items.Add(SectionItem("VITESSE DE LECTURE"));
            foreach (double value in new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 })
            {
                double captured = value;
                bool active = Math.Abs(playbackSpeed - captured) < 0.01;
                menu.Items.Add(MenuItem(SpeedLabel(captured) + (active ? "  ✓" : ""),
                    delegate { ApplyPlaybackSpeed(captured, true); }, active ? Glyphs.Check : Glyphs.Clock));
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(InfoItem("La vitesse modifie aussi le son. Les sous-titres texte suivent la position réelle."));
            menu.PlacementTarget = playerSpeedButton;
            menu.Placement = PlacementMode.Top;
            menu.IsOpen = true;
        }

        // --------------------------------------------------------------- veille

        void OpenSleepMenu()
        {
            if (playerSleepButton == null) return;
            ShowFullscreenControls();
            var menu = CreateContextMenu();
            menu.Items.Add(SectionItem("MINUTERIE DE VEILLE"));
            foreach (int minutes in new[] { 15, 30, 45, 60, 90 })
            {
                int captured = minutes;
                menu.Items.Add(MenuItem(minutes.ToString(CultureInfo.InvariantCulture) + " minutes",
                    delegate { StartSleepTimer(captured); }, Glyphs.Clock));
            }
            menu.Items.Add(MenuItem("Fin de l'épisode en cours", StartSleepAtEpisodeEnd, Glyphs.Stop));
            if (sleepDeadline != DateTime.MinValue || sleepAtEpisodeEnd)
            {
                menu.Items.Add(new Separator());
                menu.Items.Add(MenuItem("Désactiver la minuterie", CancelSleepTimer, Glyphs.Close));
            }
            menu.PlacementTarget = playerSleepButton;
            menu.Placement = PlacementMode.Top;
            menu.IsOpen = true;
        }

        void StartSleepTimer(int minutes)
        {
            CancelSleepTimer();
            sleepDeadline = DateTime.UtcNow.AddMinutes(minutes);
            sleepTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            sleepTimer.Tick += delegate { UpdateSleepTimer(); };
            sleepTimer.Start();
            UpdateSleepButton();
            ShowToast("Mise en veille dans " + minutes.ToString(CultureInfo.InvariantCulture) + " minutes.", false, true);
        }

        void StartSleepAtEpisodeEnd()
        {
            CancelSleepTimer();
            sleepAtEpisodeEnd = true;
            UpdateSleepButton();
            ShowToast("Le lecteur se mettra en pause à la fin de l'épisode.", false, true);
        }

        void CancelSleepTimer()
        {
            sleepDeadline = DateTime.MinValue;
            sleepAtEpisodeEnd = false;
            if (sleepTimer != null) { sleepTimer.Stop(); sleepTimer = null; }
            UpdateSleepButton();
        }

        void UpdateSleepTimer()
        {
            if (sleepDeadline == DateTime.MinValue)
            {
                if (sleepTimer != null) { sleepTimer.Stop(); sleepTimer = null; }
                return;
            }
            if (DateTime.UtcNow < sleepDeadline) { UpdateSleepButton(); return; }
            CancelSleepTimer();
            if (player != null)
            {
                try { player.Pause(); } catch { }
                playbackPaused = true;
                UpdatePlayPauseButton();
                ShowFullscreenControls();
            }
            ShowToast("Mise en veille · lecture en pause.", false, true);
        }

        void UpdateSleepButton()
        {
            if (playerSleepButton == null) return;
            bool active = sleepAtEpisodeEnd || sleepDeadline != DateTime.MinValue;
            playerSleepButton.Background = Brush(active ? Accent : Color.FromArgb(132, 12, 12, 18));
            playerSleepButton.BorderBrush = Brush(active ? Accent : Color.FromArgb(74, 255, 255, 255));
            if (sleepAtEpisodeEnd) playerSleepButton.ToolTip = "Veille · fin de l'épisode (cliquer pour modifier)";
            else if (sleepDeadline != DateTime.MinValue)
            {
                TimeSpan left = sleepDeadline - DateTime.UtcNow;
                int minutes = Math.Max(0, (int)Math.Ceiling(left.TotalMinutes));
                playerSleepButton.ToolTip = "Veille dans " + minutes + " min (cliquer pour modifier)";
            }
            else playerSleepButton.ToolTip = "Minuterie de veille";
        }

        // --------------------------------------------------------------- boucle

        void ToggleLoopPlayback()
        {
            loopPlayback = !loopPlayback;
            if (playerLoopButton != null)
            {
                playerLoopButton.Background = Brush(loopPlayback ? Accent : Color.FromArgb(132, 12, 12, 18));
                playerLoopButton.BorderBrush = Brush(loopPlayback ? Accent : Color.FromArgb(74, 255, 255, 255));
            }
            ShowToast(loopPlayback ? "Lecture en boucle activée." : "Lecture en boucle désactivée.", false, true);
            ShowFullscreenControls();
        }

        // ------------------------------------------------------------ boucle A-B

        void SetAbPoint(bool start)
        {
            if (player == null || playerLive)
            {
                ShowToast("La boucle A-B est réservée aux vidéos à la demande.", false, false);
                return;
            }
            double position = 0;
            try { position = player.Position.TotalSeconds; } catch { }
            if (start)
            {
                abStart = position;
                abEnd = -1;
                ShowToast("Point A placé à " + TextUtil.FormatPosition(TimeSpan.FromSeconds(position)) + ".", false, true);
            }
            else
            {
                if (abStart < 0)
                {
                    ShowToast("Placez d'abord un point A avec Maj + A.", false, false);
                    return;
                }
                if (position <= abStart + 1)
                {
                    ShowToast("Le point B doit être au moins une seconde après le point A.", false, false);
                    return;
                }
                abEnd = position;
                ShowToast("Séquence A-B · " + TextUtil.FormatPosition(TimeSpan.FromSeconds(abEnd - abStart)) + " en boucle.", false, true);
            }
            UpdatePlaybackProgress();
            ShowFullscreenControls();
        }

        void ClearAbLoop()
        {
            if (abStart < 0 && abEnd < 0) return;
            abStart = -1; abEnd = -1;
            if (abRange != null) abRange.Visibility = Visibility.Collapsed;
            ShowToast("Boucle A-B désactivée.", false, false);
            ShowFullscreenControls();
        }

        /// <summary>Ramène au point A quand la lecture dépasse le point B.</summary>
        void EnforceAbLoop(double position)
        {
            if (player == null || abStart < 0 || abEnd <= abStart) return;
            if (position < abEnd - 0.18) return;
            if ((DateTime.UtcNow - abLastJump).TotalMilliseconds < 900) return;
            abLastJump = DateTime.UtcNow;
            try { player.Position = TimeSpan.FromSeconds(abStart); } catch { }
        }

        // ------------------------------------------------------ image par image

        void StepFrame(int direction)
        {
            if (player == null || playerLive) return;
            // Un saut image par image sort volontairement de la boucle A-B.
            ClearAbLoop();
            if (!playbackPaused)
            {
                playbackPaused = true;
                try { player.Pause(); } catch { }
                UpdatePlayPauseButton();
            }
            double frame = 1.0 / (25.0 * Math.Max(0.25, playbackSpeed));
            double target = 0;
            try { target = Math.Max(0, player.Position.TotalSeconds + direction * frame); player.Position = TimeSpan.FromSeconds(target); }
            catch { }
            UpdatePlaybackProgress();
            ShowSeekHint(direction > 0 ? "+ 1 image" : "− 1 image", 0);
            ShowFullscreenControls();
        }

        // ----------------------------------------------------------- raccourcis

        internal void ShowShortcutsDialog()
        {
            var dialog = new Window
            {
                Title = "Raccourcis · LyaranTV", Width = 600, Height = 660, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                WindowStyle = WindowStyle.None, Background = Brushes.Transparent, AllowsTransparency = true,
                FontFamily = new FontFamily("Inter, Segoe UI"), ShowInTaskbar = false
            };
            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel), BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20), Padding = new Thickness(26)
            };
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 14) };
            var close = IconButton(Glyphs.Close, "Fermer", delegate { dialog.Close(); }, false);
            close.Margin = new Thickness(0); close.Width = 32; close.Height = 30;
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var heading = new StackPanel();
            heading.Children.Add(Text("Raccourcis du lecteur", 21, Brush(TextPrimary), true));
            heading.Children.Add(Text("Tout se pilote au clavier, sans quitter l'image.", 11.5, Brush(Muted), false));
            header.Children.Add(heading);
            layout.Children.Add(header);

            var rows = new StackPanel();
            AddShortcut(rows, "Espace", "Lecture / pause");
            AddShortcut(rows, "←  /  →", "Reculer ou avancer de 5 secondes");
            AddShortcut(rows, "J  /  L", "Reculer ou avancer de 10 secondes");
            AddShortcut(rows, "↑  /  ↓", "Volume");
            AddShortcut(rows, "M", "Couper le son");
            AddShortcut(rows, "F11", "Plein écran");
            AddShortcut(rows, "Échap", "Quitter le plein écran ou le lecteur");
            AddShortcut(rows, "A", "Pistes audio");
            AddShortcut(rows, "S", "Sous-titres");
            AddShortcut(rows, "E", "Autres épisodes");
            AddShortcut(rows, "[  /  ]", "Ralentir ou accélérer la lecture");
            AddShortcut(rows, "Ctrl + L", "Lecture en boucle");
            AddShortcut(rows, "Maj + A / B", "Définir une séquence A-B en boucle");
            AddShortcut(rows, "Maj + X", "Effacer la séquence A-B");
            AddShortcut(rows, ",  /  .", "Image précédente ou suivante (en pause)");
            AddShortcut(rows, "Page ↑ / ↓", "Chaîne ou épisode précédent / suivant");
            AddShortcut(rows, "Ctrl + D", "Ajouter aux favoris");
            var scroll = new ScrollViewer
            {
                Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0)
            };
            Grid.SetRow(scroll, 1);
            layout.Children.Add(scroll);

            var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            var tip = Text("Ces raccourcis sont aussi disponibles depuis le bouton ? du lecteur.", 10, Brush(Muted), false);
            tip.VerticalAlignment = VerticalAlignment.Center;
            footer.Children.Add(tip);
            var ok = Button(Glyphs.Check, "Compris", delegate { dialog.Close(); }, true);
            ok.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(ok, Dock.Right);
            footer.Children.Add(ok);
            Grid.SetRow(footer, 2);
            layout.Children.Add(footer);

            frame.Child = layout;
            dialog.Content = frame;
            dialog.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) dialog.Close(); };
            dialog.ShowDialog();
        }

        void AddShortcut(StackPanel rows, string keys, string action)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var badge = new Border
            {
                Background = Brush(Raised), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 4, 9, 5), HorizontalAlignment = HorizontalAlignment.Left,
                Child = Text(keys, 11, Brush(TextPrimary), true)
            };
            Grid.SetColumn(badge, 0); row.Children.Add(badge);
            var label = Text(action, 12, Brush(Muted), false);
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 1); row.Children.Add(label);
            rows.Children.Add(row);
        }

        // ----------------------------------------------- options avancées

        void OpenPlayerOptions()
        {
            if (playerOptionsButton == null) return;
            var menu = BuildPlayerContextMenu();
            ShowTrackMenu(menu, playerOptionsButton);
        }

        void ShiftSubtitleDelay(double delta)
        {
            subtitleDelay += delta;
            if (subtitleDelay > 30) subtitleDelay = 30;
            if (subtitleDelay < -30) subtitleDelay = -30;
            if (Math.Abs(subtitleDelay) < 0.01) subtitleDelay = 0;
            UpdateSubtitle();
            string value = (subtitleDelay >= 0 ? "+ " : "− ") + Math.Abs(subtitleDelay).ToString("0.0", CultureInfo.InvariantCulture) + " s";
            ShowToast("Synchronisation des sous-titres · " + value, false, subtitleDelay == 0);
            ShowFullscreenControls();
        }

        string SubtitleDelayLabel()
        {
            if (Math.Abs(subtitleDelay) < 0.01) return "Sous-titres synchronisés";
            return "Décalage · " + (subtitleDelay >= 0 ? "+ " : "− ")
                + Math.Abs(subtitleDelay).ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        void ApplyPlayerImageTransform()
        {
            if (player == null) return;
            var group = new TransformGroup();
            group.Children.Add(new ScaleTransform(playerFlip ? -playerZoom : playerZoom, playerZoom));
            if (playerRotation != 0) group.Children.Add(new RotateTransform(playerRotation));
            player.RenderTransformOrigin = new Point(0.5, 0.5);
            player.RenderTransform = group;
        }

        void ChangeZoom(double delta)
        {
            playerZoom += delta;
            if (playerZoom < 1) playerZoom = 1;
            if (playerZoom > 2) playerZoom = 2;
            ApplyPlayerImageTransform();
            ShowToast("Zoom · " + Math.Round(playerZoom * 100).ToString(CultureInfo.InvariantCulture) + " %", false, false);
            ShowFullscreenControls();
        }

        void RotatePlayerImage()
        {
            playerRotation = (playerRotation + 90) % 360;
            ApplyPlayerImageTransform();
            ShowToast("Rotation · " + playerRotation.ToString(CultureInfo.InvariantCulture) + "°", false, false);
            ShowFullscreenControls();
        }

        void FlipPlayerImage()
        {
            playerFlip = !playerFlip;
            ApplyPlayerImageTransform();
            ShowToast(playerFlip ? "Image en miroir" : "Image normale", false, false);
            ShowFullscreenControls();
        }

        void ResetPlayerImage()
        {
            playerZoom = 1.0;
            playerRotation = 0;
            playerFlip = false;
            subtitleDelay = 0;
            ApplyPlayerImageTransform();
            ShowToast("Image et sous-titres réinitialisés.", false, true);
            ShowFullscreenControls();
        }

        void ToggleStatsHud()
        {
            statsVisible = !statsVisible;
            if (statsHud != null) statsHud.Visibility = statsVisible ? Visibility.Visible : Visibility.Collapsed;
            if (statsVisible) UpdateStatsHud();
            ShowFullscreenControls();
        }

        void UpdateStatsHud()
        {
            if (statsHud == null || statsText == null || !statsVisible || player == null) return;
            string resolution = "—";
            try
            {
                if (player.NaturalVideoWidth > 0 && player.NaturalVideoHeight > 0)
                    resolution = player.NaturalVideoWidth.ToString(CultureInfo.InvariantCulture) + " × " + player.NaturalVideoHeight.ToString(CultureInfo.InvariantCulture);
            }
            catch { }
            double position = 0;
            try { position = player.Position.TotalSeconds; } catch { }
            double buffer = 0;
            try { buffer = Math.Max(player.DownloadProgress, player.BufferingProgress) * 100; } catch { }
            string lines = "LECTURE · " + (playerLive ? "DIRECT" : "VOD") + "\n";
            string engine = player == null ? "—" : (player.UsingVlc ? "VLC intégré" : "Media Foundation");
            lines += "Moteur     " + engine + "\n";
            lines += "Image      " + resolution + "\n";
            lines += "Position   " + TextUtil.FormatPosition(TimeSpan.FromSeconds(position)) + "\n";
            lines += "Tampon     " + Math.Round(buffer).ToString(CultureInfo.InvariantCulture) + " %\n";
            lines += "Vitesse    " + SpeedLabel(playbackSpeed) + "\n";
            lines += "Volume     " + Math.Round((wasMuted ? 0 : savedVolume) * 100).ToString(CultureInfo.InvariantCulture) + " %\n";
            lines += "Flux       " + (String.IsNullOrWhiteSpace(playbackContentType) ? "inconnu" : ShortContentType(playbackContentType));
            if (playbackConnectMs > 0) lines += "  ·  " + playbackConnectMs.ToString(CultureInfo.InvariantCulture) + " ms";
            if (player.UsingVlc)
            {
                List<AudioStream> audio = player.AudioStreams();
                if (audio.Count > 0)
                {
                    AudioStream active = null;
                    int current = player.CurrentAudioTrack;
                    foreach (AudioStream stream in audio)
                        if (stream.Id == current) { active = stream; break; }
                    if (active == null) active = audio[0];
                    lines += "\nAudio      " + active.Label;
                    lines += "\nPistes     " + audio.Count.ToString(CultureInfo.InvariantCulture);
                }
            }
            if (playbackCandidates != null && playbackCandidates.Count > 1)
                lines += "\nSource     " + (playbackCandidateIndex + 1).ToString(CultureInfo.InvariantCulture) + "/" + playbackCandidates.Count.ToString(CultureInfo.InvariantCulture);
            statsText.Text = lines;
        }
    }
}
