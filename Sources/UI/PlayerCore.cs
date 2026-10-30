using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LyaranTV
{
    /// <summary>
    /// Moteur de lecture du lecteur intégré : événements du MediaElement, lecture
    /// et pause, déplacement clavier, volume, barre de progression, reprise des
    /// erreurs et points de retour.
    /// </summary>
    public sealed partial class MainWindow
    {
        // Points de retour : position du lecteur juste avant chaque saut. Le bouton
        // Retour du lecteur ramène à l'endroit que l'on vient de quitter.
        readonly List<double> returnPoints = new List<double>();
        Border seekHint;
        TextBlock seekHintText;
        DispatcherTimer seekHintTimer;
        DispatcherTimer nextEpisodeTimer;
        Channel nextEpisodeTarget;
        int nextEpisodeCountdown;
        bool playbackFailed;
        bool volumeChanging;
        Button playerHeaderBackButton, playerTopBackButton;

        // ---------------------------------------------------------------- lecture

        internal string CurrentPlaybackUrl()
        {
            if (playbackCandidates != null && playbackCandidateIndex >= 0 && playbackCandidateIndex < playbackCandidates.Count)
                return playbackCandidates[playbackCandidateIndex];
            return currentPlaying == null ? "" : currentPlaying.Url;
        }

        void PlayerOpened()
        {
            StopPlaybackOpenTimer();
            playbackOpenedAt = DateTime.Now;
            if (playerBufferBar != null) playerBufferBar.Visibility = Visibility.Collapsed;
            HidePlayerOverlay();
            if (playerStatusText != null) playerStatusText.Text = playerLive ? "En direct" : "Lecture en cours";

            try
            {
                if (player != null)
                {
                    player.Volume = wasMuted ? 0 : savedVolume;
                    if (!resumeApplied)
                    {
                        resumeApplied = true;
                        if (resumePosition > 1 && player.NaturalDuration.HasTimeSpan)
                        {
                            double duration = player.NaturalDuration.TimeSpan.TotalSeconds;
                            if (duration > 0 && resumePosition < duration - 10)
                                player.Position = TimeSpan.FromSeconds(resumePosition);
                        }
                        if (resumePaused || playbackPaused)
                        {
                            playbackPaused = true;
                            player.Pause();
                        }
                    }
                }
            }
            catch { }

            returnPoints.Clear();
            if (playerLive && resumePosition <= 1)
                ShowToast("En direct sur « " + (currentPlaying == null ? "" : currentPlaying.Name) + " ».", false, true);
            else if (resumePosition > 1 && currentPlaying != null)
                ShowToast("Reprise à " + TextUtil.FormatPosition(TimeSpan.FromSeconds(resumePosition)) + ".", false, true);

            UpdatePlayPauseButton();
            UpdatePlaybackProgress();
            ScheduleAudioOnlyCheck();
            if (currentPlaying != null && (currentPlaying.IsSeries || currentPlaying.Kind == "Épisodes"))
                EnsureSeriesEpisodes(currentPlaying);
        }

        void PlayerFailed(Exception error)
        {
            StopPlaybackOpenTimer();
            string message = error == null ? "" : error.Message;
            HandleCandidateFailure(String.IsNullOrWhiteSpace(message) ? "Le flux ne peut pas être lu." : message);
        }

        void PlayerEnded()
        {
            StopPlaybackOpenTimer();
            if (playerBufferBar != null) playerBufferBar.Visibility = Visibility.Collapsed;
            if (currentPlaying != null) positions.Remove(currentPlaying.Id);

            if (playerLive)
            {
                // Un direct qui s'arrête est presque toujours une coupure réseau :
                // on retente le flux avant d'afficher une erreur.
                HandleCandidateFailure("Le direct s'est interrompu.");
                return;
            }

            if (loopPlayback && player != null)
            {
                try
                {
                    player.Position = TimeSpan.Zero;
                    player.Play();
                    playbackPaused = false;
                    UpdatePlayPauseButton();
                    ShowToast("Boucle · reprise du même contenu.", false, false);
                    return;
                }
                catch { }
            }

            if (sleepAtEpisodeEnd)
            {
                CancelSleepTimer();
                playbackPaused = true;
                try { player.Pause(); } catch { }
                UpdatePlayPauseButton();
                ShowFullscreenControls();
                ShowToast("Veille · lecture mise en pause à la fin de l'épisode.", false, true);
                return;
            }

            Channel next = NextEpisodeInQueue();
            if (next != null)
            {
                if (settings.AutoPlayNext) StartNextEpisodeCountdown(next);
                else
                {
                    nextEpisodeTarget = next;
                    nextEpisodeCountdown = 0;
                    ShowNextEpisodeOverlay();
                }
                return;
            }
            ShowPlaybackError("La lecture est terminée.");
        }

        void ShowBuffering()
        {
            if (playerStatusText != null) playerStatusText.Text = "Mise en mémoire tampon";
            if (playbackOpenedAt == DateTime.MinValue)
            {
                if (playerOverlay != null)
                {
                    playerOverlay.Visibility = Visibility.Visible;
                    playerOverlay.IsHitTestVisible = false;
                }
                if (playerOverlayText != null) playerOverlayText.Text = "Mise en mémoire tampon";
                if (playerBufferBar != null) playerBufferBar.Visibility = Visibility.Visible;
                if (playerOverlayActions != null) playerOverlayActions.Visibility = Visibility.Collapsed;
            }
        }

        void HideBuffering()
        {
            if (playerStatusText != null) playerStatusText.Text = playerLive ? "En direct" : "Lecture en cours";
            if (playbackOpenedAt != DateTime.MinValue && playerBufferBar != null)
                playerBufferBar.Visibility = Visibility.Collapsed;
            if (playbackOpenedAt != DateTime.MinValue) HidePlayerOverlay();
        }

        void HandleCandidateFailure(string reason)
        {
            if (appClosing || page != "Lecture" || currentPlaying == null) return;

            if (playbackCandidates != null && playbackCandidateIndex + 1 < playbackCandidates.Count)
            {
                playbackCandidateIndex++;
                if (playerStatusText != null) playerStatusText.Text = "Nouvel essai · lien de secours";
                StartCurrentCandidate();
                return;
            }
            if (playerLive && liveReconnectCount < 3)
            {
                liveReconnectCount++;
                if (playerStatusText != null)
                    playerStatusText.Text = "Reconnexion au direct (" + liveReconnectCount.ToString() + "/3)";
                StartCurrentCandidate();
                return;
            }
            ShowPlaybackError(String.IsNullOrWhiteSpace(reason) ? "Le flux ne peut pas être lu." : reason);
        }

        void ShowPlaybackError(string message)
        {
            if (playerOverlay == null) return;
            playbackFailed = true;
            playerOverlay.Visibility = Visibility.Visible;
            playerOverlay.IsHitTestVisible = true;
            if (playerOverlayText != null) playerOverlayText.Text = message;
            if (playerBufferBar != null) playerBufferBar.Visibility = Visibility.Collapsed;
            if (playerOverlayActions != null)
            {
                playerOverlayActions.Children.Clear();
                playerOverlayActions.Visibility = Visibility.Visible;
                var retry = Button(Glyphs.Refresh, "Réessayer", delegate
                {
                    playbackCandidateIndex = 0;
                    liveReconnectCount = 0;
                    StartCurrentCandidate();
                }, true);
                retry.Margin = new Thickness(0, 0, 8, 0);
                playerOverlayActions.Children.Add(retry);
                var external = Button(Glyphs.External, "Lecteur externe", delegate { OpenInExternalPlayer(CurrentPlaybackUrl()); }, false);
                external.Margin = new Thickness(0, 0, 8, 0);
                playerOverlayActions.Children.Add(external);
                playerOverlayActions.Children.Add(Button(Glyphs.Back, "Retour", ExitPlayerPage, false));
            }
            if (playerStatusText != null) playerStatusText.Text = "Lecture impossible";
            SetStatus(message);
        }

        void HidePlayerOverlay()
        {
            if (playerOverlay != null)
            {
                playerOverlay.Visibility = Visibility.Collapsed;
                playerOverlay.IsHitTestVisible = false;
            }
            if (playerBufferBar != null) playerBufferBar.Visibility = Visibility.Collapsed;
            if (playerOverlayActions != null) playerOverlayActions.Visibility = Visibility.Collapsed;
            if (nextEpisodeArt != null) nextEpisodeArt.Visibility = Visibility.Collapsed;
        }

        // Tant que le lecteur avance, aucun voile de chargement ne doit rester
        // affiché par-dessus l'image.
        void ClearLoadingVeil()
        {
            if (playbackFailed || nextEpisodeTimer != null || playerOverlay == null) return;
            if (playerOverlay.Visibility != Visibility.Visible) return;
            if (playerOverlayActions != null && playerOverlayActions.Visibility == Visibility.Visible) return;
            HidePlayerOverlay();
        }

        // ------------------------------------------------------------ commandes

        void TogglePlayback()
        {
            if (player == null) return;
            if (playbackPaused)
            {
                try { player.Play(); } catch { return; }
                playbackPaused = false;
            }
            else
            {
                try { player.Pause(); } catch { return; }
                playbackPaused = true;
                SaveCurrentPosition();
            }
            UpdatePlayPauseButton();
            if (playerControlWrap != null && playbackPaused) playerControlWrap.Visibility = Visibility.Visible;
            ShowFullscreenControls();
        }

        void PushReturnPoint(double seconds)
        {
            if (seconds <= 1) return;
            if (returnPoints.Count > 0 && Math.Abs(returnPoints[returnPoints.Count - 1] - seconds) < 0.5) return;
            returnPoints.Add(seconds);
            while (returnPoints.Count > 12) returnPoints.RemoveAt(0);
            UpdatePlayerBackButton();
        }

        // Ramène à la position quittée lors du dernier saut : c'est le « dernier
        // point de retour » avant celui où l'on se trouve. Ce n'est plus le rôle
        // du bouton Retour (qui quitte le lecteur) mais un raccourci séparé,
        // disponible avec Retour arrière et dans le menu contextuel.
        void ReturnToPreviousPoint()
        {
            if (player != null && !playerLive && returnPoints.Count > 0)
            {
                double point = returnPoints[returnPoints.Count - 1];
                bool restored = false;
                try
                {
                    if (player.NaturalDuration.HasTimeSpan)
                    {
                        player.Position = TimeSpan.FromSeconds(point);
                        restored = true;
                    }
                }
                catch { }
                if (restored)
                {
                    returnPoints.RemoveAt(returnPoints.Count - 1);
                    UpdatePlayerBackButton();
                    ShowSeekHint("Point de retour", point);
                    ShowToast("Retour au point précédent · " + TextUtil.FormatPosition(TimeSpan.FromSeconds(point)), false, true);
                    playbackPaused = false;
                    try { player.Play(); } catch { }
                    UpdatePlayPauseButton();
                    UpdatePlaybackProgress();
                    ShowFullscreenControls();
                    return;
                }
            }
            ShowToast("Aucun point de retour dans cette lecture.", false, false);
        }

        // Le bouton Retour du lecteur quitte la lecture et retrouve l'interface,
        // comme dans une application vidéo classique.
        void PlayerBack()
        {
            ExitPlayerPage();
        }

        void ExitPlayerPage()
        {
            if (fullScreen) ExitFullScreen();
            // Une série retrouve sa page d'épisodes ; un film, une chaîne ou un
            // fichier retrouve la catégorie exacte d'où la lecture a été lancée.
            Channel series = backSeries;
            List<Channel> episodes = backSeriesEpisodes;
            backSeries = null;
            backSeriesEpisodes = null;
            if (series != null && episodes != null && episodes.Count > 0)
            {
                ShowSeriesEpisodes(series, episodes);
                return;
            }
            RestorePlayerOrigin();
        }

        void UpdatePlayerBackButton()
        {
            string tip = "Quitter le lecteur et revenir à l'interface · Échap";
            if (returnPoints.Count > 0) tip += "\nRetour arrière : revenir au point précédent";
            if (playerHeaderBackButton != null) playerHeaderBackButton.ToolTip = tip;
            if (playerTopBackButton != null) playerTopBackButton.ToolTip = tip;
        }

        void SeekRelative(double delta)
        {
            if (player == null) return;
            if (playerLive && !player.NaturalDuration.HasTimeSpan)
            {
                ShowToast("Le direct ne peut pas être décalé.", false, false);
                return;
            }
            double duration = 0;
            double position = 0;
            try
            {
                if (player.NaturalDuration.HasTimeSpan) duration = player.NaturalDuration.TimeSpan.TotalSeconds;
                position = player.Position.TotalSeconds;
            }
            catch { }
            if (duration <= 0) { ShowToast("Ce flux ne permet pas le déplacement.", false, false); return; }

            double target = position + delta;
            if (target < 0) target = 0;
            if (target > duration - 1) target = Math.Max(0, duration - 1);
            PushReturnPoint(position);
            try { player.Position = TimeSpan.FromSeconds(target); } catch { }
            ShowSeekHint(delta > 0 ? "+ " + ((int)Math.Abs(delta)).ToString() + " s" : "− " + ((int)Math.Abs(delta)).ToString() + " s", 0);
            UpdatePlaybackProgress();
            ShowFullscreenControls();
        }

        void SeekToFraction(double fraction)
        {
            if (player == null || fraction < 0) return;
            if (fraction > 1) fraction = 1;
            double duration = 0;
            try { if (player.NaturalDuration.HasTimeSpan) duration = player.NaturalDuration.TimeSpan.TotalSeconds; }
            catch { }
            if (duration <= 0) return;
            PushReturnPoint(scrubValue);
            double target = duration * fraction;
            try { player.Position = TimeSpan.FromSeconds(target); } catch { }
            scrubValue = target;
            UpdatePlaybackProgress();
        }

        void ShowSeekHint(string text, double seconds)
        {
            if (seekHint == null) return;
            if (seekHintText != null)
                seekHintText.Text = seconds > 0 && text == "Point de retour"
                    ? text + "\n" + TextUtil.FormatPosition(TimeSpan.FromSeconds(seconds))
                    : text;
            seekHint.Visibility = Visibility.Visible;
            seekHint.Opacity = 1;
            if (seekHintTimer == null)
            {
                seekHintTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(760) };
                seekHintTimer.Tick += delegate
                {
                    seekHintTimer.Stop();
                    if (seekHint != null) seekHint.Visibility = Visibility.Collapsed;
                };
            }
            seekHintTimer.Stop();
            seekHintTimer.Start();
        }

        void SwitchChannel(int direction)
        {
            if (playQueue == null || playQueue.Count == 0 || currentPlaying == null) return;
            int index = playQueue.FindIndex(delegate(Channel item) { return item != null && String.Equals(item.Id, currentPlaying.Id, StringComparison.OrdinalIgnoreCase); });
            if (index < 0) index = 0;
            int next = index + direction;
            if (next < 0) next = playQueue.Count - 1;
            if (next >= playQueue.Count) next = 0;
            if (playQueue.Count == 1) { ShowToast("Aucun autre contenu dans la file.", false, false); return; }
            Play(playQueue[next], playQueue, 0, false);
        }

        // ------------------------------------------------------------- volume

        void ToggleMute()
        {
            wasMuted = !wasMuted;
            if (player != null)
            {
                try { player.Volume = wasMuted ? 0 : savedVolume; } catch { }
            }
            if (volumeSlider != null)
            {
                double value = wasMuted ? 0 : savedVolume * 100;
                if (Math.Abs(volumeSlider.Value - value) > 0.5)
                {
                    volumeChanging = true;
                    volumeSlider.Value = value;
                    volumeChanging = false;
                }
            }
            settings.Muted = wasMuted;
            SettingsStore.Save(settings);
            UpdateMuteIcon();
            ShowFullscreenControls();
        }

        void ChangeVolume(double delta)
        {
            double volume = (wasMuted ? 0 : savedVolume) + delta;
            if (volume < 0) volume = 0;
            if (volume > 1) volume = 1;
            savedVolume = volume <= 0 ? savedVolume : volume;
            wasMuted = volume <= 0;
            if (player != null) { try { player.Volume = volume; } catch { } }
            if (volumeSlider != null)
            {
                volumeChanging = true;
                volumeSlider.Value = volume * 100;
                volumeChanging = false;
            }
            settings.Volume = savedVolume;
            settings.Muted = wasMuted;
            SettingsStore.Save(settings);
            UpdateMuteIcon();
            ShowFullscreenControls();
        }

        void UpdateMuteIcon()
        {
            bool muted = wasMuted || (volumeSlider != null && volumeSlider.Value <= 0.5);
            if (muteButton != null)
            {
                muteButton.Content = Icon(muted ? Glyphs.Mute : Glyphs.Volume, 16, Brushes.White);
                muteButton.ToolTip = muted ? "Rétablir le son · M" : "Couper le son · M";
            }
            double level = wasMuted ? 0 : savedVolume;
            if (volumeSlider != null && Math.Abs(volumeSlider.Value - level * 100) > 2)
            {
                volumeChanging = true;
                volumeSlider.Value = level * 100;
                volumeChanging = false;
            }
            if (volumeSlider != null)
                volumeSlider.ToolTip = "Volume " + ((int)Math.Round((volumeSlider.Value / 100.0) * 100)).ToString() + " %";
            UpdateVolumeReadout();
        }

        void UpdatePlayPauseButton()
        {
            string glyph = playbackPaused ? Glyphs.Play : Glyphs.Pause;
            if (playPauseButton != null) playPauseButton.Content = Icon(glyph, 22, Brushes.White);
            if (playerCenterButton != null) playerCenterButton.Content = Icon(glyph, 30, Brushes.White);
        }

        // ---------------------------------------------------------- progression

        void UpdatePlaybackProgress()
        {
            if (player == null || page != "Lecture") return;
            double duration = 0;
            double position = 0;
            try
            {
                if (player.NaturalDuration.HasTimeSpan) duration = player.NaturalDuration.TimeSpan.TotalSeconds;
                position = player.Position.TotalSeconds;
            }
            catch { }
            if (position < 0) position = 0;
            if (duration <= 0 && playerLive) duration = 0;
            if (abStart >= 0 && abEnd > abStart && position >= abEnd - 0.18 && !scrubbing)
            {
                EnforceAbLoop(position);
                position = abStart;
            }

            // Certains conteneurs (WAV, TS en direct) ne déclenchent pas MediaOpened :
            // on applique la reprise dès que la durée est connue.
            if (!resumeApplied && duration > 0 && player != null)
            {
                resumeApplied = true;
                if (resumePosition > 1 && resumePosition < duration - 10)
                {
                    try { player.Position = TimeSpan.FromSeconds(resumePosition); } catch { }
                }
                if (resumePaused || playbackPaused)
                {
                    playbackPaused = true;
                    try { player.Pause(); } catch { }
                    UpdatePlayPauseButton();
                }
                if (playerStatusText != null) playerStatusText.Text = playerLive ? "En direct" : "Lecture en cours";
                ClearLoadingVeil();
            }
            scrubDuration = duration;
            if (!scrubbing) scrubValue = position;

            if (elapsedText != null)
                elapsedText.Text = playerLive && duration <= 0 ? "EN DIRECT" : TextUtil.FormatPosition(TimeSpan.FromSeconds(position));
            if (totalText != null)
                totalText.Text = duration <= 0 ? (playerLive ? "LIVE" : "—:—") : TextUtil.FormatPosition(TimeSpan.FromSeconds(duration));

            UpdateScrubberVisual();
            UpdateSubtitle();
            UpdatePlaybackInfo();
            if (statsVisible) UpdateStatsHud();
            if (position > 0.25 || duration > 0) ClearLoadingVeil();
        }

        void UpdateScrubberVisual()
        {
            if (scrubTrack == null) return;
            double width = scrubTrack.ActualWidth;
            if (width <= 1 && scrubFill != null) width = scrubFill.ActualWidth;
            if (width <= 1) width = 0;
            double fraction = scrubDuration > 0 ? scrubValue / scrubDuration : 0;
            if (fraction < 0) fraction = 0;
            if (fraction > 1) fraction = 1;
            if (scrubFill != null) scrubFill.Width = Math.Max(0, width * fraction);
            if (scrubBuffer != null)
            {
                double buffered = fraction;
                try
                {
                    double download = player == null ? 0 : player.DownloadProgress;
                    double buffer = player == null ? 0 : player.BufferingProgress;
                    if (download > buffered) buffered = download;
                    else if (download <= 0.01 && buffer > buffered) buffered = buffer;
                }
                catch { }
                if (buffered < 0) buffered = 0;
                if (buffered > 1) buffered = 1;
                scrubBuffer.Width = Math.Max(0, width * buffered);
                scrubBuffer.Visibility = scrubDuration > 0 && buffered > 0.01 ? Visibility.Visible : Visibility.Collapsed;
            }
            if (scrubThumb != null)
            {
                scrubThumb.Visibility = scrubDuration > 0 ? Visibility.Visible : Visibility.Collapsed;
                double offset = width * fraction - scrubThumb.Width / 2;
                scrubThumb.Margin = new Thickness(Math.Max(0, Math.Min(width - scrubThumb.Width, offset)), 0, 0, 0);
            }
            if (abRange != null)
            {
                if (scrubDuration > 0 && abStart >= 0 && abEnd > abStart && width > 1)
                {
                    double left = Math.Max(0, width * abStart / scrubDuration);
                    double segment = Math.Max(3, width * (abEnd - abStart) / scrubDuration);
                    if (left + segment > width) segment = Math.Max(0, width - left);
                    abRange.Margin = new Thickness(left, 0, 0, 0);
                    abRange.Width = segment;
                    abRange.Visibility = Visibility.Visible;
                }
                else abRange.Visibility = Visibility.Collapsed;
            }
            if (playerLive && scrubDuration <= 0 && scrubTrack != null)
                scrubTrack.Opacity = 0.35;
            else if (scrubTrack != null) scrubTrack.Opacity = 1;
        }

        void BeginScrub(double fraction)
        {
            if (player == null || scrubDuration <= 0) return;
            scrubbing = true;
            if (fraction < 0) fraction = 0;
            if (fraction > 1) fraction = 1;
            scrubValue = scrubDuration * fraction;
            if (elapsedText != null) elapsedText.Text = TextUtil.FormatPosition(TimeSpan.FromSeconds(scrubValue));
            if (scrubFill != null) scrubFill.Width = Math.Max(0, scrubTrack.ActualWidth * fraction);
            if (scrubThumb != null)
                scrubThumb.Margin = new Thickness(Math.Max(0, scrubTrack.ActualWidth * fraction - scrubThumb.Width / 2), 0, 0, 0);
        }

        void UpdateScrubTarget(double fraction)
        {
            if (scrubDuration <= 0) return;
            if (fraction < 0) fraction = 0;
            if (fraction > 1) fraction = 1;
            scrubValue = scrubDuration * fraction;
            if (elapsedText != null) elapsedText.Text = TextUtil.FormatPosition(TimeSpan.FromSeconds(scrubValue));
            if (scrubTip != null && scrubTipBox != null)
            {
                scrubTip.Text = TextUtil.FormatPosition(TimeSpan.FromSeconds(scrubValue));
                scrubTipBox.Visibility = Visibility.Visible;
                double width = scrubTrack.ActualWidth;
                double left = Math.Max(0, Math.Min(width - 52, width * fraction - 26));
                scrubTipBox.Margin = new Thickness(left, 0, 0, 0);
            }
            UpdateScrubberVisual();
        }

        void EndScrub(bool apply)
        {
            if (scrubTipBox != null) scrubTipBox.Visibility = Visibility.Collapsed;
            if (!scrubbing) return;
            scrubbing = false;
            if (!apply) { UpdatePlaybackProgress(); return; }
            if (scrubDuration <= 0) return;
            double duration = scrubDuration;
            double target = scrubValue;
            if (player != null)
            {
                double previous = 0;
                try { previous = player.Position.TotalSeconds; } catch { }
                PushReturnPoint(previous);
                try { player.Position = TimeSpan.FromSeconds(target); } catch { }
            }
            ShowSeekHint(TextUtil.FormatPosition(TimeSpan.FromSeconds(target)), 0);
            UpdatePlaybackProgress();
        }

        // ------------------------------------------------- épisode suivant

        Channel NextEpisodeInQueue()
        {
            if (playQueue == null || currentPlaying == null) return null;
            int index = playQueue.FindIndex(delegate(Channel item) { return item != null && String.Equals(item.Id, currentPlaying.Id, StringComparison.OrdinalIgnoreCase); });
            if (index < 0) return null;
            for (int i = index + 1; i < playQueue.Count; i++)
            {
                Channel candidate = playQueue[i];
                if (candidate == null) continue;
                if (candidate.Kind == "Épisodes" || candidate.IsSeries) return candidate;
                break;
            }
            return null;
        }

        void StartNextEpisodeCountdown(Channel next)
        {
            if (next == null) return;
            nextEpisodeTarget = next;
            nextEpisodeCountdown = 8;
            if (nextEpisodeTimer != null) nextEpisodeTimer.Stop();
            nextEpisodeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            nextEpisodeTimer.Tick += delegate
            {
                nextEpisodeCountdown--;
                if (nextEpisodeCountdown > 0)
                {
                    ShowNextEpisodeOverlay();
                    return;
                }
                nextEpisodeTimer.Stop();
                nextEpisodeTimer = null;
                Channel target = nextEpisodeTarget;
                nextEpisodeTarget = null;
                HidePlayerOverlay();
                if (target != null) Play(target, playQueue, 0, false);
            };
            nextEpisodeTimer.Start();
            ShowNextEpisodeOverlay();
        }

        void ShowNextEpisodeOverlay()
        {
            if (playerOverlay == null) return;
            playerOverlay.Visibility = Visibility.Visible;
            playerOverlay.IsHitTestVisible = true;
            if (nextEpisodeArt != null)
            {
                if (nextEpisodeTarget != null)
                {
                    nextEpisodeArt.Child = CoverArt(nextEpisodeTarget, 320, 180, true);
                    nextEpisodeArt.Visibility = Visibility.Visible;
                }
                else nextEpisodeArt.Visibility = Visibility.Collapsed;
            }
            if (playerOverlayText != null)
                playerOverlayText.Text = "Épisode suivant · " + (nextEpisodeTarget == null ? "" : nextEpisodeTarget.Name)
                    + (nextEpisodeCountdown > 0 ? "\nLecture dans " + nextEpisodeCountdown.ToString() + " s" : "\nPrêt à être lancé");
            if (playerBufferBar != null) playerBufferBar.Visibility = Visibility.Collapsed;
            if (playerOverlayActions != null)
            {
                playerOverlayActions.Children.Clear();
                playerOverlayActions.Visibility = Visibility.Visible;
                var now = Button(Glyphs.Play, "Lire maintenant", delegate
                {
                    if (nextEpisodeTimer != null) { nextEpisodeTimer.Stop(); nextEpisodeTimer = null; }
                    Channel target = nextEpisodeTarget;
                    nextEpisodeTarget = null;
                    HidePlayerOverlay();
                    if (target != null) Play(target, playQueue, 0, false);
                }, true);
                now.Margin = new Thickness(0, 0, 8, 0);
                playerOverlayActions.Children.Add(now);
                playerOverlayActions.Children.Add(Button(Glyphs.Close, "Annuler", delegate
                {
                    if (nextEpisodeTimer != null) { nextEpisodeTimer.Stop(); nextEpisodeTimer = null; }
                    nextEpisodeTarget = null;
                    HidePlayerOverlay();
                }, false));
            }
        }

        // --------------------------------------------------------- sous-titres

        void UpdateSubtitle()
        {
            if (subtitleBox == null || subtitleText == null) return;
            if (activeSubtitle == null || player == null)
            {
                subtitleBox.Visibility = Visibility.Collapsed;
                return;
            }
            double position = 0;
            try { position = player.Position.TotalSeconds; } catch { }
            // Décalage de synchronisation : une valeur positive retarde les sous-titres.
            double query = position - subtitleDelay;
            if (query < 0) query = 0;
            SubtitleCue cue = activeSubtitle.At(query);
            if (cue == null || String.IsNullOrWhiteSpace(cue.Text))
            {
                subtitleBox.Visibility = Visibility.Collapsed;
                return;
            }
            subtitleText.Text = cue.Text;
            subtitleBox.Visibility = Visibility.Visible;
            UpdateSubtitlePosition();
        }

        void UpdateSubtitlePosition()
        {
            if (subtitleBox == null || subtitleText == null) return;
            bool controlsVisible = playerControlWrap == null || playerControlWrap.Visibility == Visibility.Visible;
            double bottom = fullScreen ? (controlsVisible ? 132 : 46) : (controlsVisible ? 122 : 30);
            subtitleBox.Margin = new Thickness(24, 0, 24, bottom);
            ApplySubtitleStyle();
        }

        void UpdateSubtitleButton()
        {
            if (subtitleButton == null) return;
            bool vlcActive = player != null && player.UsingVlc && player.CurrentSubtitleTrack >= 0;
            bool active = activeSubtitle != null || vlcActive;
            subtitleButton.Content = Icon(Glyphs.Subtitles, 16, active ? Brush(Accent) : Brushes.White);
            subtitleButton.ToolTip = ActiveSubtitlesLabel() + "  ·  S";
        }

        string ActiveSubtitlesLabel()
        {
            if (activeSubtitle != null) return "Sous-titres · " + SubtitleLoader.DisplayName(activeSubtitle);
            if (player != null && player.UsingVlc)
            {
                int current = player.CurrentSubtitleTrack;
                if (current >= 0)
                {
                    foreach (SubtitleStream stream in player.SubtitleStreams())
                        if (stream.Id == current) return "Sous-titres · " + stream.Label;
                    return "Sous-titres intégrés";
                }
            }
            return "Sous-titres";
        }

        void UpdateAudioButton()
        {
            if (audioButton == null) return;
            MediaTrackScan scan = CurrentTrackScan();
            int audio = scan == null ? 0 : scan.Audio.Count;
            int subtitles = scan == null ? 0 : scan.Subtitles.Count;
            if (audio == 0 && player != null && player.UsingVlc)
            {
                try { audio = player.AudioStreams().Count; } catch { }
            }
            if (subtitles == 0 && player != null && player.UsingVlc)
            {
                try { subtitles = player.SubtitleStreams().Count; } catch { }
            }
            audioButton.Content = Icon(Glyphs.Music, 16, audio > 1 ? Brush(Accent) : Brushes.White);
            var parts = new List<string>();
            if (audio == 1) parts.Add("1 piste audio");
            else if (audio > 1) parts.Add(audio.ToString() + " pistes audio");
            if (subtitles == 1) parts.Add("1 sous-titre");
            else if (subtitles > 1) parts.Add(subtitles.ToString() + " sous-titres");
            audioButton.ToolTip = parts.Count == 0
                ? "Pistes audio et sous-titres · A"
                : String.Join("  ·  ", parts.ToArray()) + "  ·  A";
        }

        // ------------------------------------------------------- médias locaux

        internal void AddLocalRecent(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return;
            string full = TextUtil.LocalPathFromUrl(path);
            if (String.IsNullOrWhiteSpace(full) || !File.Exists(full)) return;
            full = Path.GetFullPath(full);
            recentLocal.RemoveAll(delegate(string item) { return String.Equals(item, full, StringComparison.OrdinalIgnoreCase); });
            recentLocal.Insert(0, full);
            while (recentLocal.Count > 40) recentLocal.RemoveAt(recentLocal.Count - 1);
            StateStore.WriteList("local-files.txt", recentLocal);
        }
    }
}
