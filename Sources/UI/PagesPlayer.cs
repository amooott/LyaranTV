using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace LyaranTV
{
    public sealed partial class MainWindow
    {
        StackPanel playerOverlayActions;
        FrameworkElement playerBufferBar;
        bool pendingPlaybackStart;
        Border subtitleBox;
        TextBlock subtitleText;
        Button subtitleButton;
        Button audioButton;
        // Surface affichée à la place de la vidéo pour un fichier audio seul
        // (MP3, FLAC, WAV, M4A, OGG, Opus, MKA, AC-3/DTS brut…).
        Border audioOnlyPanel;
        TextBlock audioOnlyTitle;
        TextBlock audioOnlyDetail;
        DispatcherTimer audioOnlyCheckTimer;
        Button playerHeaderFullscreenButton;
        Button playerFullscreenButton;
        Button episodesToggleButtonInControls;
        SubtitleTrack activeSubtitle;
        MediaTrackScan playbackTracks;
        string playbackTracksPath = "";
        int playbackTracksGeneration;
        long activeEmbeddedSubtitleTrack;
        string activeSubtitlePath = "";
        int subtitleSelectionVersion;
        bool subtitleBusy;
        readonly Dictionary<string, SubtitleTrack> embeddedSubtitleCache = new Dictionary<string, SubtitleTrack>(StringComparer.OrdinalIgnoreCase);
        DispatcherTimer playbackOpenTimer;
        TextBlock playbackInfoText;
        string playbackContentType = "";
        long playbackConnectMs;
        int playbackProbeGeneration;
        int liveReconnectCount;
        DateTime playbackOpenedAt = DateTime.MinValue;
        bool showEpisodesBelowPlayer = false;
        Border episodesSection;

        // Destination du retour depuis le lecteur : la série en cours, sinon la
        // page et la catégorie exactes d'où la lecture a été lancée.
        Channel backSeries;
        List<Channel> backSeriesEpisodes;
        string playerOriginPage = "Bibliothèque";
        string playerOriginKind = "Tous";
        string playerOriginGroup = AllGroups;
        double playerOriginScroll;

        // ------------------------------------------------------------- lancement

        void Play(Channel channel)
        {
            Play(channel, null, 0, false);
        }

        void Play(Channel channel, List<Channel> queue)
        {
            Play(channel, queue, GetSavedPosition(channel), false);
        }

        void Play(Channel channel, List<Channel> queue, double startPosition, bool startPaused)
        {
            if (channel == null) return;
            if (!IsChannelAllowed(channel)) { ShowError("Ce contenu est verrouillé par le contrôle parental."); return; }
            if ((channel.IsSeries || channel.Kind == "Séries") && String.IsNullOrWhiteSpace(channel.Url))
            {
                LoadSeriesEpisodes(channel);
                return;
            }
            if (String.IsNullOrWhiteSpace(channel.Url))
            {
                ShowError("Ce contenu ne contient pas de lien de lecture.");
                return;
            }

            if (currentPlaying != null && player != null) SaveCurrentPosition();
            StopNextEpisodeCountdown();
            if (player != null)
            {
                try { player.Stop(); player.Close(); } catch { }
                player = null;
            }
            StopPlaybackOpenTimer();
            StreamProxy.Clear();
            if (playbackTimer != null) { playbackTimer.Stop(); playbackTimer = null; }

            bool enteringPlayer = page != "Lecture";
            if (enteringPlayer)
            {
                // On mémorise la page et ses filtres avant de basculer au lecteur :
                // c'est là que le bouton Retour doit ramener.
                playerOriginPage = page;
                playerOriginKind = activeKind;
                playerOriginGroup = activeGroup;
                playerOriginScroll = mainScroll == null ? 0 : mainScroll.VerticalOffset;
                backSeries = null;
                backSeriesEpisodes = null;
                PushNavFrame();
            }
            page = "Lecture";
            currentPlaying = channel;
            playQueue = BuildPlayQueue(channel, queue);
            playbackPaused = startPaused;
            resumePosition = startPosition > 0 ? startPosition : GetSavedPosition(channel);
            resumePaused = startPaused;
            resumeApplied = false;
            playerLive = channel.Kind == "Chaînes";
            playbackCandidates = BuildPlaybackCandidates(channel);
            playbackCandidateIndex = 0;
            playbackContentType = "";
            playbackConnectMs = 0;
            playbackProbeGeneration++;
            liveReconnectCount = 0;
            playbackOpenedAt = DateTime.MinValue;
            pendingPlaybackStart = false;
            activeSubtitle = null;
            activeSubtitlePath = "";
            activeEmbeddedSubtitleTrack = 0;
            subtitleSelectionVersion++;
            subtitleBusy = false;
            playbackTracks = null;
            playbackTracksPath = "";
            playbackTracksGeneration++;
            embeddedSubtitleCache.Clear();
            scrubbing = false;
            scrubValue = 0;
            scrubDuration = 0;
            subtitleDelay = 0;
            playerZoom = 1.0;
            playerRotation = 0;
            playerFlip = false;
            statsVisible = false;
            playerChromeShown = false;
            // Comme sur Netflix, les autres épisodes s'affichent d'ordinateur sous
            // le lecteur dès qu'on regarde un épisode.
            showEpisodesBelowPlayer = channel.Kind == "Épisodes" || channel.Kind == "Séries"
                || channel.IsSeries || !String.IsNullOrWhiteSpace(channel.SeriesId);
            if (enteringPlayer) CaptureSeriesReturn(channel);

            BuildPlayerPage();
            RebuildSidebar();
            UpdateStatusBar();
            SetContent(playerLayout);
            if (fullScreen) ApplyFullScreenLayout();
            SetStatus("Lecture de " + channel.Name + ".");
            if (channel.Kind == "Local") AddLocalRecent(channel.Url);
            else AddRecent(channel);
            RememberHistoryOwnership(new[] { channel });
            if (String.Equals(channel.Kind, "Épisodes", StringComparison.OrdinalIgnoreCase) || !String.IsNullOrWhiteSpace(channel.SeriesId))
                RememberEpisode(channel);
            settings.LastChannelId = channel.Id;
            SaveSettings();

            if (IsStalkerChannel(channel)) ResolveStalkerAndStart(channel);
            else StartCurrentCandidate();
            StartMediaTrackScan(channel);
        }

        List<Channel> BuildPlayQueue(Channel channel, List<Channel> requested)
        {
            IEnumerable<Channel> source = requested;
            if (source == null) source = currentView;
            if (source == null || !source.Any()) source = channels;
            var list = source.Where(c => c != null && (c.IsPlayable || c.IsSeries) && !hiddenChannels.Contains(c.Id)).ToList();
            if (!list.Any(c => String.Equals(c.Id, channel.Id, StringComparison.OrdinalIgnoreCase))) list.Insert(0, channel);
            return list;
        }

        bool IsStalkerChannel(Channel channel)
        {
            if (channel == null || activeSource == null || activeSource.Type != "Portail MAC") return false;
            if (!String.IsNullOrWhiteSpace(channel.Url) && channel.Url.StartsWith("stalker://", StringComparison.OrdinalIgnoreCase)) return true;
            return String.IsNullOrWhiteSpace(channel.Url) && !String.IsNullOrWhiteSpace(channel.StreamId);
        }

        List<string> BuildPlaybackCandidates(Channel channel)
        {
            return StreamPlanner.Candidates(channel);
        }

        void ResolveStalkerAndStart(Channel channel)
        {
            if (activeSource == null) { ShowPlaybackError("La source du portail n'est plus disponible."); return; }
            if (playerOverlay != null)
            {
                playerOverlay.Visibility = Visibility.Visible;
                if (playerOverlayText != null) playerOverlayText.Text = "Obtention du lien de lecture auprès du portail";
                if (playerBufferBar != null) playerBufferBar.Visibility = Visibility.Visible;
                if (playerOverlayActions != null) playerOverlayActions.Visibility = Visibility.Collapsed;
            }
            if (playerStatusText != null) playerStatusText.Text = "Connexion au portail";
            PlaylistSource source = activeSource;
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                var client = new StalkerClient(source.Url, source.MacAddress, source.DeviceType);
                e.Result = client.ResolveStream(channel);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    ShowPlaybackError("Le portail n'a pas fourni de flux : " + e.Error.GetBaseException().Message);
                    return;
                }
                string url = e.Result as string;
                if (String.IsNullOrWhiteSpace(url)) { ShowPlaybackError("Le portail n'a pas fourni de lien de lecture."); return; }
                if (page != "Lecture" || currentPlaying == null || !String.Equals(currentPlaying.Id, channel.Id, StringComparison.OrdinalIgnoreCase)) return;
                playbackCandidates.Clear();
                playbackCandidates.Add(url);
                if (!String.IsNullOrWhiteSpace(channel.AlternateUrl)) playbackCandidates.Add(channel.AlternateUrl);
                playbackCandidateIndex = 0;
                StartCurrentCandidate();
            };
            worker.RunWorkerAsync();
        }

        // --------------------------------------------------------------- page

        void BuildPlayerPage()
        {
            // Lecteur immersif : la page occupe toute la fenêtre, sans barre latérale
            // ni en-tête d'application, comme sur Netflix. Il ne reste que la vidéo,
            // une barre flottante discrète et, plus bas, la fiche du contenu.
            playerLayout = new Grid { Margin = new Thickness(0), Background = Brush(Back) };
            playerLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            playerLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            playerHeaderBackButton = null;
            playerHeaderFullscreenButton = null;

            var videoGrid = new Grid { Background = Brushes.Black, MinHeight = 250, ClipToBounds = true };
            playerVideoGrid = videoGrid;
            playbackSpeed = settings.PlaybackSpeed;
            // Deux moteurs : libVLC (audio AC-3, E-AC-3, DTS, TrueHD…) et Media
            // Foundation en repli. Le premier qui sait lire le fichier gagne.
            player = new PlaybackEngine
            {
                Stretch = AspectStretch(), Volume = wasMuted ? 0 : savedVolume, SpeedRatio = playbackSpeed
            };
            player.MediaOpened += delegate { PlayerOpened(); };
            player.MediaFailed += delegate(Exception error) { PlayerFailed(error); };
            player.MediaEnded += delegate { PlayerEnded(); };
            player.BufferingStarted += delegate { ShowBuffering(); };
            player.BufferingEnded += delegate { HideBuffering(); };
            player.Loaded += delegate { if (pendingPlaybackStart) { pendingPlaybackStart = false; StartCurrentCandidate(); } };
            videoGrid.Children.Add(player);
            videoGrid.ContextMenu = BuildPlayerContextMenu();
            player.ContextMenu = BuildPlayerContextMenu();

            // Fichier audio seul : aucune image à afficher, mais le lecteur garde
            // sa mise en page, ses commandes et ses sous-titres. Une surface
            // discrète remplace le cadre noir, avec le titre et le format réel.
            var audioBackdrop = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromRgb(10, 10, 18), 0),
                new GradientStop(Color.FromRgb(25, 21, 44), 0.55),
                new GradientStop(Color.FromRgb(10, 10, 18), 1)
            }, new Point(0, 0), new Point(1, 1));
            var audioStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 680
            };
            var audioDisc = new Border
            {
                Width = 104, Height = 104, CornerRadius = new CornerRadius(52),
                Background = Brush(Color.FromArgb(42, 139, 92, 246)),
                BorderBrush = Brush(Color.FromArgb(95, 139, 92, 246)), BorderThickness = new Thickness(1),
                Child = Icon(Glyphs.Music, 42, Brush(Accent)),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 22)
            };
            audioStack.Children.Add(audioDisc);
            audioOnlyTitle = Text(currentPlaying == null ? "" : currentPlaying.Name, 21, Brushes.White, true);
            audioOnlyTitle.TextAlignment = TextAlignment.Center;
            audioOnlyTitle.TextWrapping = TextWrapping.Wrap;
            audioOnlyTitle.HorizontalAlignment = HorizontalAlignment.Center;
            audioStack.Children.Add(audioOnlyTitle);
            audioOnlyDetail = Text("", 12, Brush(Muted), false);
            audioOnlyDetail.TextAlignment = TextAlignment.Center;
            audioOnlyDetail.TextWrapping = TextWrapping.Wrap;
            audioOnlyDetail.HorizontalAlignment = HorizontalAlignment.Center;
            audioOnlyDetail.Margin = new Thickness(0, 8, 0, 0);
            audioStack.Children.Add(audioOnlyDetail);
            audioOnlyPanel = new Border
            {
                Child = audioStack, Background = audioBackdrop,
                Visibility = Visibility.Collapsed, IsHitTestVisible = false
            };
            videoGrid.Children.Add(audioOnlyPanel);
            System.Windows.Controls.Panel.SetZIndex(audioOnlyPanel, 5);

            // Sous-titres : texte centré au-dessus des commandes, lisible en plein écran.
            subtitleText = Text("", 15, Brushes.White, false);
            subtitleText.TextAlignment = TextAlignment.Center;
            subtitleText.TextWrapping = TextWrapping.Wrap;
            subtitleText.LineHeight = 21;
            subtitleText.MaxWidth = 980;
            subtitleBox = new Border
            {
                Child = subtitleText, Background = Brush(Color.FromArgb(178, 0, 0, 0)), CornerRadius = new CornerRadius(7),
                Padding = new Thickness(13, 5, 13, 6), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24, 0, 24, 30),
                Visibility = Visibility.Collapsed, IsHitTestVisible = false
            };
            subtitleBox.Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 1, Opacity = 0.75, Color = Colors.Black };
            ApplySubtitleStyle();
            videoGrid.Children.Add(subtitleBox);
            System.Windows.Controls.Panel.SetZIndex(subtitleBox, 34);

            // Voile dégradé en haut, pour que le retour et le titre restent lisibles
            // sur n'importe quelle image.
            var topShade = new Border
            {
                VerticalAlignment = VerticalAlignment.Top, Height = 110, IsHitTestVisible = false,
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(180, 0, 0, 0), 0),
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 1)
                }, new Point(0.5, 0), new Point(0.5, 1))
            };
            videoGrid.Children.Add(topShade);
            System.Windows.Controls.Panel.SetZIndex(topShade, 27);

            // Bandeau supérieur minimal : un retour rond et le titre, rien de plus.
            playerTopBar = new DockPanel
            {
                LastChildFill = true, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(18, 14, 18, 0), Visibility = Visibility.Visible
            };
            playerTopBackButton = PlayerButton(Glyphs.Back, "Quitter le lecteur", PlayerBack, false, 38);
            playerTopBackButton.Margin = new Thickness(0, 0, 14, 0);
            DockPanel.SetDock(playerTopBackButton, Dock.Left);
            playerTopBar.Children.Add(playerTopBackButton);
            var topTitles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            playerTopTitle = Text(currentPlaying.Name, 15.5, Brushes.White, true);
            playerTopTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            playerTopTitle.MaxWidth = 760;
            playerTopTitle.HorizontalAlignment = HorizontalAlignment.Left;
            topTitles.Children.Add(playerTopTitle);
            topTitles.HorizontalAlignment = HorizontalAlignment.Left;
            playerTopSub = Text(PlayerSubtitleLine(), 10.5, Brush(Color.FromArgb(168, 255, 255, 255)), false);
            topTitles.Children.Add(playerTopSub);
            playerTopBar.Children.Add(topTitles);
            videoGrid.Children.Add(playerTopBar);
            System.Windows.Controls.Panel.SetZIndex(playerTopBar, 28);

            // Bouton central : rappel visuel lecture/pause.
            playerCenterButton = new Button
            {
                Width = 82, Height = 82, Padding = new Thickness(0),
                Content = Icon(Glyphs.Play, 32, Brushes.White),
                Background = Brush(Color.FromArgb(150, 10, 10, 16)),
                BorderBrush = Brush(Color.FromArgb(110, 255, 255, 255)), BorderThickness = new Thickness(1.4),
                Cursor = Cursors.Hand, Focusable = false, FocusVisualStyle = null,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Template = RoundedButtonTemplate(41), Visibility = Visibility.Collapsed,
                ToolTip = "Lecture / pause · Espace"
            };
            playerCenterButton.Click += delegate { TogglePlayback(); };
            videoGrid.Children.Add(playerCenterButton);
            System.Windows.Controls.Panel.SetZIndex(playerCenterButton, 26);

            // Retour visuel des sauts ±5 s.
            seekHintText = Text("+ 5 s", 15, Brushes.White, true);
            seekHintText.TextAlignment = TextAlignment.Center;
            seekHint = new Border
            {
                Child = seekHintText, Background = Brush(Color.FromArgb(160, 0, 0, 0)), CornerRadius = new CornerRadius(11),
                Padding = new Thickness(16, 9, 16, 10), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 84),
                Visibility = Visibility.Collapsed, IsHitTestVisible = false
            };
            videoGrid.Children.Add(seekHint);
            System.Windows.Controls.Panel.SetZIndex(seekHint, 33);

            // HUD technique façon VLC : résolution, tampon, codec et source.
            statsText = Text("", 10.5, Brush(Color.FromArgb(230, 255, 255, 255)), false);
            statsText.FontFamily = new FontFamily("Consolas, Cascadia Mono, Segoe UI");
            statsText.TextWrapping = TextWrapping.NoWrap;
            statsHud = new Border
            {
                Child = statsText, Background = Brush(Color.FromArgb(210, 6, 7, 12)),
                BorderBrush = Brush(Color.FromArgb(70, 255, 255, 255)), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 9, 14, 9),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 68, 18, 0), Visibility = Visibility.Collapsed, IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Opacity = 0.4, Color = Colors.Black }
            };
            videoGrid.Children.Add(statsHud);
            System.Windows.Controls.Panel.SetZIndex(statsHud, 35);

            videoGrid.MouseMove += delegate { ShowFullscreenControls(); };
            // Double-clic sur l'image : plein écran, comme dans un lecteur vidéo.
            videoGrid.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount != 2) return;
                if (playerControlWrap != null && playerControlWrap.IsMouseOver) return;
                if (playerTopBar != null && playerTopBar.IsMouseOver) return;
                ToggleFullScreen();
                e.Handled = true;
            };
            videoGrid.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount > 1) return;
                if (playerControlWrap != null && playerControlWrap.IsMouseOver) return;
                if (playerCenterButton != null && playerCenterButton.IsMouseOver) return;
                if (!IsInsideButton(e.OriginalSource as DependencyObject)) TogglePlayback();
                ShowFullscreenControls();
            };

            videoPanel = new Border
            {
                Child = videoGrid, Background = Brushes.Black, CornerRadius = new CornerRadius(0),
                BorderThickness = new Thickness(0), ClipToBounds = true
            };
            Grid.SetRow(videoPanel, 0);
            playerLayout.Children.Add(videoPanel);

            // Voile de chargement ou d'erreur, posé sur la vidéo.
            playerOverlay = new Border
            {
                Child = BuildPlayerOverlayContent(), Background = Brush(Color.FromArgb(210, 6, 6, 12)),
                Visibility = Visibility.Collapsed
            };
            videoGrid.Children.Add(playerOverlay);
            System.Windows.Controls.Panel.SetZIndex(playerOverlay, 36);

            // Commandes superposées, à la Netflix : dégradé sombre et masquage auto.
            var controls = new StackPanel();
            var controlWrap = new Border
            {
                Child = controls,
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                    new GradientStop(Color.FromArgb(28, 0, 0, 0), 0.22),
                    new GradientStop(Color.FromArgb(150, 0, 0, 0), 0.58),
                    new GradientStop(Color.FromArgb(236, 0, 0, 0), 0.86),
                    // Le tout dernier pixel est opaque : sans cela, l'image
                    // affleurait sous la barre et formait une bande claire
                    // au bas de l'écran en plein écran.
                    new GradientStop(Color.FromArgb(255, 0, 0, 0), 1)
                }, new Point(0.5, 0), new Point(0.5, 1)),
                Padding = new Thickness(22, 58, 22, 14),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            playerControlWrap = controlWrap;
            controlWrap.MouseEnter += delegate { StartFullscreenControlsTimer(); };
            controlWrap.MouseMove += delegate { StartFullscreenControlsTimer(); };
            controlWrap.MouseLeave += delegate { StartFullscreenControlsTimer(); };
            videoGrid.Children.Add(controlWrap);
            System.Windows.Controls.Panel.SetZIndex(controlWrap, 30);

            // Barre de temps : durée écoulée, piste fine, durée totale.
            var timeline = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            elapsedText = Text("00:00", 10.5, Brush(Color.FromArgb(198, 255, 255, 255)), false);
            elapsedText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(elapsedText, 0);
            timeline.Children.Add(elapsedText);
            playerScrubber = BuildScrubber();
            Grid.SetColumn(playerScrubber, 1);
            timeline.Children.Add(playerScrubber);
            totalText = Text("LIVE", 10.5, Brush(Color.FromArgb(198, 255, 255, 255)), false);
            totalText.HorizontalAlignment = HorizontalAlignment.Right;
            totalText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(totalText, 2);
            timeline.Children.Add(totalText);
            controls.Children.Add(timeline);

            // Commandes : lecture, sauts, volume à gauche ; pistes et plein écran à
            // droite. Boutons ronds sans cadre, dans l'esprit du lecteur Netflix.
            var row = new DockPanel { LastChildFill = true, Height = 52, Margin = new Thickness(0, 2, 0, 0) };

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            playPauseButton = PlayerButton(Glyphs.Pause, "Lecture / pause · Espace", TogglePlayback, true, 46);
            left.Children.Add(playPauseButton);

            var previous = SkipButton(-5, false);
            previous.Margin = new Thickness(14, 0, 2, 0);
            left.Children.Add(previous);
            var forward = SkipButton(5, true);
            forward.Margin = new Thickness(0, 0, 6, 0);
            left.Children.Add(forward);

            // Volume : la barre de commandes ne garde que l'icône du haut-parleur.
            // Un survol déploie une petite carte flottante au-dessus d'elle avec
            // un curseur fin et le pourcentage — plus aéré qu'un rail permanent
            // qui encombre l'image, et plus lisible.
            muteButton = PlayerButton(Glyphs.Volume, "Couper le son · M", ToggleMute, false, 38);
            muteButton.Margin = new Thickness(4, 0, 2, 0);
            volumeHost = new Grid
            {
                Width = 46, Height = 40, VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent
            };
            muteButton.HorizontalAlignment = HorizontalAlignment.Center;
            muteButton.VerticalAlignment = VerticalAlignment.Center;
            volumeHost.Children.Add(muteButton);
            volumeSlider = new Slider
            {
                Minimum = 0, Maximum = 100, Value = (wasMuted ? 0 : savedVolume) * 100, Width = 162,
                VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true,
                Style = PlayerVolumeSliderStyle(), ToolTip = "Volume"
            };
            volumeSlider.ValueChanged += delegate
            {
                if (volumeChanging) return;
                UpdateVolumeReadout();
                if (player == null) return;
                double value = volumeSlider.Value / 100.0;
                try { player.Volume = value; } catch { }
                if (value > 0) { savedVolume = value; wasMuted = false; }
                else wasMuted = true;
                settings.Volume = savedVolume;
                settings.Muted = wasMuted;
                UpdateMuteIcon();
            };
            // La carte vit dans la surface vidéo, pas dans la barre : la barre a
            // une hauteur fixe et écrasait la carte. Posée sur l'image, elle
            // s'ouvre juste au-dessus du bouton, sans jamais être rognée.
            volumeCard = BuildVolumeCard(volumeSlider);
            videoGrid.Children.Add(volumeCard);
            System.Windows.Controls.Panel.SetZIndex(volumeCard, 31);
            volumeHost.MouseEnter += delegate { ShowVolumeCard(true); };
            volumeHost.MouseLeave += delegate { ScheduleVolumeCardHide(); };
            left.Children.Add(volumeHost);
            left.Children.Add(new Border { Width = 6 });
            DockPanel.SetDock(left, Dock.Left);
            row.Children.Add(left);

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };

            episodesToggleButtonInControls = PlayerButton(Glyphs.List, "Autres épisodes · E", ScrollToEpisodes, false, 38);
            episodesToggleButtonInControls.Margin = new Thickness(0, 0, 2, 0);
            episodesToggleButtonInControls.Visibility = Visibility.Collapsed;
            right.Children.Add(episodesToggleButtonInControls);

            playerSpeedButton = PlayerLabelButton("1×", "Vitesse de lecture · [ et ]", OpenSpeedMenu);
            right.Children.Add(playerSpeedButton);

            playerLoopButton = PlayerButton(Glyphs.Repeat, "Lecture en boucle · Ctrl + L", ToggleLoopPlayback, false, 38);
            right.Children.Add(playerLoopButton);

            playerSleepButton = PlayerButton(Glyphs.Clock, "Minuterie de veille", OpenSleepMenu, false, 38);
            right.Children.Add(playerSleepButton);

            audioButton = PlayerButton(Glyphs.Music, "Pistes audio · A", OpenAudioTrackMenu, false, 38);
            right.Children.Add(audioButton);

            subtitleButton = PlayerButton(Glyphs.Subtitles, "Sous-titres · S", OpenSubtitleMenu, false, 38);
            right.Children.Add(subtitleButton);

            var aspect = new ComboBox
            {
                Width = 96, Height = 32, Style = ModernComboStyle(), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 6, 0), ToolTip = "Format d'image"
            };
            aspect.Items.Add("Adapter"); aspect.Items.Add("Remplir"); aspect.Items.Add("Étirer");
            aspect.SelectedItem = playerAspect;
            aspect.SelectionChanged += delegate
            {
                if (aspect.SelectedItem == null || player == null) return;
                playerAspect = (string)aspect.SelectedItem;
                settings.AspectMode = playerAspect;
                player.Stretch = AspectStretch();
                SaveSettings();
            };
            right.Children.Add(aspect);

            // Un seul bouton « engrenage » ouvre toutes les options avancées :
            // pistes, synchronisation, image, A-B, veille et raccourcis.
            playerOptionsButton = PlayerButton(Glyphs.Settings, "Options du lecteur · menu complet", OpenPlayerOptions, false, 38);
            right.Children.Add(playerOptionsButton);

            playerFullscreenButton = PlayerButton(Glyphs.FullScreen, "Plein écran · F11", ToggleFullScreen, false, 38);
            right.Children.Add(playerFullscreenButton);
            DockPanel.SetDock(right, Dock.Right);
            row.Children.Add(right);

            controls.Children.Add(row);

            UpdatePlayPauseButton();
            UpdateMuteIcon();
            UpdateSubtitleButton();
            UpdateAudioButton();
            UpdateSpeedButton();
            UpdateSleepButton();

            // Fiche du contenu et épisodes, posés sous la vidéo sans encadré :
            // le fond suffit, comme la fiche d'un titre Netflix.
            playerInfoCard = new Border
            {
                Child = BuildPlayerInfo(), Background = Brush(Back),
                BorderThickness = new Thickness(0), Padding = new Thickness(0),
                Visibility = Visibility.Visible
            };
            Grid.SetRow(playerInfoCard, 1);
            playerLayout.Children.Add(playerInfoCard);

            playerLayout.Loaded += delegate { UpdatePlayerVideoSize(); };
            UpdatePlayerVideoSize();

            UpdateEpisodesToggle();
            UpdatePlayerBackButton();
            if (currentPlaying != null && (currentPlaying.Kind == "Épisodes" || currentPlaying.IsSeries || !String.IsNullOrWhiteSpace(currentPlaying.SeriesId)))
                EnsureSeriesEpisodes(currentPlaying);

            ApplyPlayerImageTransform();
            playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            playbackTimer.Tick += delegate { UpdatePlaybackProgress(); };
            playbackTimer.Start();

            // Les commandes s'affichent au démarrage puis s'effacent d'elles-mêmes.
            ShowFullscreenControls();
        }

        FrameworkElement BuildPlayerOverlayContent()
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            // Aperçu de l'épisode suivant, comme la carte de fin de Netflix.
            nextEpisodeArt = new Border
            {
                Width = 320, Height = 180, CornerRadius = new CornerRadius(10), ClipToBounds = true,
                Background = Brush(Raised), BorderBrush = Brush(Color.FromArgb(70, 255, 255, 255)), BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 16), Visibility = Visibility.Collapsed,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 28, ShadowDepth = 8, Opacity = 0.5, Color = Colors.Black }
            };
            stack.Children.Add(nextEpisodeArt);
            playerOverlayText = Text("Connexion au serveur", 15, Brushes.White, true);
            playerOverlayText.TextAlignment = TextAlignment.Center;
            playerOverlayText.HorizontalAlignment = HorizontalAlignment.Center;
            playerOverlayText.MaxWidth = 620;
            stack.Children.Add(playerOverlayText);
            playerBufferBar = CreateSpinner(38);
            playerBufferBar.Margin = new Thickness(0, 18, 0, 0);
            stack.Children.Add(playerBufferBar);
            playerOverlayActions = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 0), Visibility = Visibility.Collapsed
            };
            stack.Children.Add(playerOverlayActions);
            return stack;
        }

        string PlayerSubtitleLine()
        {
            if (currentPlaying == null) return "";
            var parts = new List<string>();
            // Le groupe d'un épisode vaut souvent « Saison 1 » : inutile de le
            // répéter juste avant la saison affichée en tête.
            if (!String.IsNullOrWhiteSpace(currentPlaying.Group)
                && !currentPlaying.Group.StartsWith("Saison", StringComparison.OrdinalIgnoreCase))
                parts.Add(currentPlaying.Group);
            parts.Add(KindLabel(currentPlaying.Kind));
            if (!String.IsNullOrWhiteSpace(currentPlaying.Season))
            {
                string season = "Saison " + currentPlaying.Season;
                if (!String.IsNullOrWhiteSpace(currentPlaying.Episode)) season += " · Épisode " + currentPlaying.Episode;
                parts.Insert(0, season);
            }
            return String.Join("  ·  ", parts.ToArray());
        }

        Button SkipButton(double seconds, bool forward)
        {
            bool back = seconds < 0;
            var button = new Button
            {
                Content = IconText(back ? Glyphs.Back : Glyphs.Forward, ((int)Math.Abs(seconds)).ToString() + " s", Brushes.White, 11.5),
                Height = 38, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 2, 0),
                Background = Brush(Color.FromArgb(74, 8, 8, 14)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, FocusVisualStyle = null, SnapsToDevicePixels = true,
                Template = RoundedButtonTemplate(19),
                ToolTip = back ? "Reculer de 5 secondes · Flèche gauche" : "Avancer de 5 secondes · Flèche droite",
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            double delta = seconds;
            button.Click += delegate { SeekRelative(delta); };
            return button;
        }

        /// <summary>
        /// Carte de volume qui se déploie au-dessus du bouton du haut-parleur :
        /// intitulé discret, pourcentage courant et curseur fin. Elle flotte sur
        /// l'image au lieu d'occuper une place permanente dans la barre.
        /// </summary>
        Border BuildVolumeCard(Slider slider)
        {
            volumeValueText = Text("100 %", 10.5, Brush(TextPrimary), true);
            volumeValueText.HorizontalAlignment = HorizontalAlignment.Right;
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 7) };
            var label = Text("VOLUME", 9, Brush(Muted), true);
            DockPanel.SetDock(label, Dock.Left);
            header.Children.Add(label);
            DockPanel.SetDock(volumeValueText, Dock.Right);
            header.Children.Add(volumeValueText);

            var stack = new StackPanel();
            stack.Children.Add(header);
            stack.Children.Add(slider);

            var card = new Border
            {
                Child = stack, Width = 192, Padding = new Thickness(13, 12, 13, 10),
                CornerRadius = new CornerRadius(13),
                Background = Brush(Color.FromArgb(248, 14, 14, 21)),
                BorderBrush = Brush(WithAlpha(TextPrimary, 24)), BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(-4, 0, 0, 42), Visibility = Visibility.Collapsed, Opacity = 0,
                Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 8, Opacity = 0.45, Color = Colors.Black }
            };
            // La carte reste atteignable à la souris : on annule la sortie du
            // groupe seulement quand ni le bouton ni la carte ne sont survolés.
            card.MouseEnter += delegate { ShowVolumeCard(true); };
            card.MouseLeave += delegate { ScheduleVolumeCardHide(); };
            return card;
        }

        void ShowVolumeCard(bool show)
        {
            if (volumeCard == null) return;
            if (volumeCardHideTimer != null) { volumeCardHideTimer.Stop(); volumeCardHideTimer = null; }
            if (!show)
            {
                if (volumeSlider != null && volumeSlider.IsMouseDirectlyOver) return;
                UiMotion.Animate(volumeCard, UIElement.OpacityProperty, 0, 110);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
                timer.Tick += delegate
                {
                    timer.Stop();
                    if (volumeCard != null && volumeCard.Opacity < 0.05) volumeCard.Visibility = Visibility.Collapsed;
                };
                timer.Start();
                return;
            }
            // Repositionnement au-dessus du bouton : la barre de commandes peut
            // être décalée (plein écran, fenêtre redimensionnée), la carte suit.
            try
            {
                if (playerVideoGrid != null && volumeHost != null && volumeHost.IsVisible)
                {
                    Point origin = volumeHost.TranslatePoint(new Point(0, 0), playerVideoGrid);
                    double left = origin.X + volumeHost.ActualWidth / 2 - 94;
                    if (left < 12) left = 12;
                    double bottom = playerVideoGrid.ActualHeight - origin.Y + 8;
                    volumeCard.Margin = new Thickness(left, 0, 0, Math.Max(8, bottom));
                }
            }
            catch { }
            volumeCard.Visibility = Visibility.Visible;
            UpdateVolumeReadout();
            UiMotion.Animate(volumeCard, UIElement.OpacityProperty, 1, 130);
        }

        void ScheduleVolumeCardHide()
        {
            if (volumeCard == null) return;
            if (volumeCardHideTimer != null) { volumeCardHideTimer.Stop(); }
            volumeCardHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
            volumeCardHideTimer.Tick += delegate
            {
                volumeCardHideTimer.Stop();
                volumeCardHideTimer = null;
                ShowVolumeCard(false);
            };
            volumeCardHideTimer.Start();
        }

        void UpdateVolumeReadout()
        {
            if (volumeValueText == null) return;
            double value = volumeSlider == null ? 0 : volumeSlider.Value;
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            bool muted = wasMuted || value <= 0.5;
            volumeValueText.Text = muted ? "muet" : Math.Round(value).ToString("0") + " %";
            volumeValueText.Foreground = muted ? Brush(Muted) : Brush(TextPrimary);
        }

        Grid BuildScrubber()
        {
            var root = new Grid { Height = 28, Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            scrubTrack = new Border
            {
                Height = 6, CornerRadius = new CornerRadius(3), Background = Brush(Color.FromArgb(78, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center
            };
            root.Children.Add(scrubTrack);
            // Tampon réseau : la partie déjà téléchargée s'éclaire avant le curseur.
            scrubBuffer = new Border
            {
                Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, Width = 0, IsHitTestVisible = false,
                Background = Brush(Color.FromArgb(64, 255, 255, 255))
            };
            root.Children.Add(scrubBuffer);
            scrubFill = new Border
            {
                Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, Width = 0,
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Accent, 0),
                    new GradientStop(WithAlpha(Accent, 205), 1)
                }, new Point(0, 0.5), new Point(1, 0.5))
            };
            root.Children.Add(scrubFill);
            // Séquence A-B : un segment accentué sur la timeline.
            abRange = new Border
            {
                Height = 6, CornerRadius = new CornerRadius(3), Background = Brush(WithAlpha(Accent, 120)),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed, IsHitTestVisible = false
            };
            root.Children.Add(abRange);
            scrubThumb = new Border
            {
                Width = 15, Height = 15, CornerRadius = new CornerRadius(8), Background = Brush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed, IsHitTestVisible = false,
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Opacity = 0.55, Color = Colors.Black }
            };
            root.Children.Add(scrubThumb);
            root.Children.Add(BuildScrubTip());

            root.SizeChanged += delegate { UpdateScrubberVisual(); };
            root.MouseEnter += delegate
            {
                if (scrubTrack != null) scrubTrack.Height = 10;
                if (scrubFill != null) scrubFill.Height = 10;
                if (scrubBuffer != null) scrubBuffer.Height = 10;
                if (abRange != null) abRange.Height = 10;
                if (scrubThumb != null)
                {
                    scrubThumb.Width = 18; scrubThumb.Height = 18; scrubThumb.CornerRadius = new CornerRadius(9);
                }
                UpdateScrubberVisual();
            };
            root.MouseLeave += delegate
            {
                if (scrubTrack != null) scrubTrack.Height = 6;
                if (scrubFill != null) scrubFill.Height = 6;
                if (scrubBuffer != null) scrubBuffer.Height = 6;
                if (abRange != null) abRange.Height = 6;
                if (scrubThumb != null)
                {
                    scrubThumb.Width = 15; scrubThumb.Height = 15; scrubThumb.CornerRadius = new CornerRadius(8);
                }
                if (scrubTipBox != null && !scrubbing) scrubTipBox.Visibility = Visibility.Collapsed;
                if (!scrubbing) UpdatePlaybackProgress();
            };
            root.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (scrubDuration <= 0) return;
                root.CaptureMouse();
                BeginScrub(ScrubFraction(root, e));
                e.Handled = true;
            };
            root.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (scrubDuration <= 0) return;
                double fraction = ScrubFraction(root, e);
                if (scrubbing) BeginScrub(fraction);
                else UpdateScrubTarget(fraction);
            };
            root.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (scrubDuration <= 0) return;
                if (scrubbing) EndScrub(true);
                root.ReleaseMouseCapture();
                e.Handled = true;
            };
            root.LostMouseCapture += delegate { if (scrubbing) EndScrub(true); };
            return root;
        }

        FrameworkElement BuildScrubTip()
        {
            scrubTip = Text("00:00", 10.5, Brushes.White, true);
            scrubTipBox = new Border
            {
                Child = scrubTip, Background = Brush(Color.FromArgb(220, 12, 12, 18)), CornerRadius = new CornerRadius(6),
                BorderBrush = Brush(Color.FromArgb(70, 255, 255, 255)), BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 3, 8, 4), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -26, 0, 0),
                Visibility = Visibility.Collapsed, IsHitTestVisible = false
            };
            return scrubTipBox;
        }

        static double ScrubFraction(Grid root, MouseEventArgs e)
        {
            double width = root.ActualWidth;
            if (width <= 1) return 0;
            double x = e.GetPosition(root).X;
            double fraction = x / width;
            if (fraction < 0) fraction = 0;
            if (fraction > 1) fraction = 1;
            return fraction;
        }

        /// <summary>
        /// Les épisodes ne se cachent plus : ils vivent sous la vidéo et le bouton
        /// du lecteur y fait simplement défiler, comme la rangée « Épisodes ».
        /// </summary>
        void ToggleEpisodesSection()
        {
            if (episodesSection == null) return;
            bool available = episodesSection.Child != null && episodesSection.Child is Panel
                && ((Panel)episodesSection.Child).Children.Count > 0;
            if (!available)
            {
                ShowToast("Aucun épisode à afficher pour ce contenu.", false, false);
                return;
            }
            ScrollToEpisodes();
        }

        void StopNextEpisodeCountdown()
        {
            if (nextEpisodeTimer != null) { nextEpisodeTimer.Stop(); nextEpisodeTimer = null; }
            nextEpisodeTarget = null;
            HidePlayerOverlay();
        }

        // ---------------------------------------------------------------- infos

        FrameworkElement BuildPlayerInfo()
        {
            // La fiche occupe toute la largeur utile : sans cela, les actions de la
            // rangée d'épisodes se retrouvaient au milieu du bloc.
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(34, 26, 34, 34)
            };

            // Sur-titre en petites capitales accentuées : nature du contenu et saison.
            bool series = currentPlaying.Kind == "Épisodes" || currentPlaying.Kind == "Séries"
                || currentPlaying.IsSeries || !String.IsNullOrWhiteSpace(currentPlaying.SeriesId);
            string eyebrow = playerLive || currentPlaying.Kind == "Chaînes" ? "CHAÎNE"
                : currentPlaying.Kind == "Films" ? "FILM"
                : series ? "SÉRIE" : "MÉDIA";
            // Repère de saison façon Netflix : « S1:E2 » plutôt qu'une longue phrase.
            if (!String.IsNullOrWhiteSpace(currentPlaying.Season))
            {
                string mark = "S" + currentPlaying.Season;
                if (!String.IsNullOrWhiteSpace(currentPlaying.Episode)) mark += ":E" + currentPlaying.Episode;
                eyebrow += "  ·  " + mark;
            }
            var eyebrowRow = new WrapPanel();
            eyebrowRow.Children.Add(Text(eyebrow, 9.5, Brush(Accent), true));
            if (playerLive)
            {
                var liveBadge = Badge("EN DIRECT", Brush(WithAlpha(Success, 34)), Brush(Success));
                liveBadge.Margin = new Thickness(10, 0, 0, 0);
                eyebrowRow.Children.Add(liveBadge);
            }
            stack.Children.Add(eyebrowRow);

            currentChannel = Text(currentPlaying.Name, 26, Brush(TextPrimary), true);
            currentChannel.TextWrapping = TextWrapping.Wrap;
            currentChannel.MaxWidth = 900;
            currentChannel.HorizontalAlignment = HorizontalAlignment.Left;
            currentChannel.Margin = new Thickness(0, 7, 0, 7);
            stack.Children.Add(currentChannel);

            // Ligne méta à la Netflix : des informations séparées par des points,
            // sans pastilles, pour rester sobre.
            var facts = new List<string>();
            if (!String.IsNullOrWhiteSpace(currentPlaying.Year)) facts.Add(currentPlaying.Year);
            if (!String.IsNullOrWhiteSpace(currentPlaying.Rating)) facts.Add(currentPlaying.Rating + "/10");
            if (!String.IsNullOrWhiteSpace(currentPlaying.Runtime)) facts.Add(currentPlaying.Runtime);
            if (!String.IsNullOrWhiteSpace(currentPlaying.Episode) && String.IsNullOrWhiteSpace(currentPlaying.Season))
                facts.Add("Épisode " + currentPlaying.Episode);
            if (!String.IsNullOrWhiteSpace(currentPlaying.Genres)) facts.Add(TextUtil.Ellipsis(currentPlaying.Genres, 60));
            if (facts.Count > 0)
                stack.Children.Add(Text(String.Join("  ·  ", facts.ToArray()), 11.5, Brush(Muted), false));

            if (playerLive)
            {
                var now = CurrentProgram(currentPlaying);
                var nextProgram = NextProgram(currentPlaying);
                if (now != null)
                {
                    var program = Text(now.TimeRange + "  ·  " + now.Title, 12.5, Brush(TextPrimary), true);
                    program.Margin = new Thickness(0, 12, 0, 0);
                    stack.Children.Add(program);
                    if (!String.IsNullOrWhiteSpace(now.Description))
                        stack.Children.Add(Text(TextUtil.Ellipsis(now.Description, 220), 11.5, Brush(Muted), false));
                }
                else stack.Children.Add(Text("Aucun programme EPG disponible pour cette chaîne.", 11.5, Brush(Muted), false));
                if (nextProgram != null)
                    stack.Children.Add(Text("À suivre : " + nextProgram.Title + "  ·  " + nextProgram.TimeRange, 11, Brush(Muted), false));
            }

            var actions = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
            double saved = GetSavedPosition(currentPlaying);
            string playLabel = playerLive ? "Direct" : (saved > 5 ? "Reprendre à " + TextUtil.FormatPosition(TimeSpan.FromSeconds(saved)) : "Lecture");
            actions.Children.Add(Button(Glyphs.Play, playLabel, delegate { FocusPlayer(); }, true));
            favButton = Button(favorites.Contains(currentPlaying.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite,
                favorites.Contains(currentPlaying.Id) ? "Favori" : "Ajouter aux favoris", delegate { ToggleFavorite(currentPlaying); }, false);
            actions.Children.Add(favButton);
            actions.Children.Add(Button(Glyphs.Info, "Informations", delegate { ShowChannelInfo(currentPlaying); }, false));
            actions.Children.Add(Button(Glyphs.External, "Lecteur externe", delegate { OpenInExternalPlayer(CurrentPlaybackUrl()); }, false));
            if (series) actions.Children.Add(Button(Glyphs.List, "Épisodes", delegate { ScrollToEpisodes(); }, false));
            stack.Children.Add(actions);

            string description = !String.IsNullOrWhiteSpace(currentPlaying.Description) ? currentPlaying.Description : currentPlaying.Overview;
            if (String.IsNullOrWhiteSpace(description)) description = "Aucune description disponible pour ce contenu.";
            var body = Text(description, 12.5, Brush(TextPrimary), false);
            body.Margin = new Thickness(0, 14, 0, 0);
            body.MaxWidth = 860;
            body.HorizontalAlignment = HorizontalAlignment.Left;
            body.TextWrapping = TextWrapping.Wrap;
            body.LineHeight = 19;
            stack.Children.Add(body);

            playerStatusText = Text(playerLive ? "En direct" : "Lecture en cours", 10, Brush(Muted), false);
            playerStatusText.Margin = new Thickness(0, 16, 0, 0);
            playerStatusText.TextTrimming = TextTrimming.CharacterEllipsis;
            stack.Children.Add(playerStatusText);

            playbackInfoText = Text("Flux en attente de connexion", 10, Brush(Muted), false);
            playbackInfoText.Margin = new Thickness(0, 3, 0, 0);
            playbackInfoText.TextTrimming = TextTrimming.CharacterEllipsis;
            stack.Children.Add(playbackInfoText);

            // Les autres épisodes, sous la vidéo : on choisit la suite comme sur Netflix.
            episodesSection = new Border
            {
                Child = BuildEpisodesRow(), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 26, 0, 0),
                Visibility = showEpisodesBelowPlayer ? Visibility.Visible : Visibility.Collapsed
            };
            stack.Children.Add(episodesSection);
            return stack;
        }

        /// <summary>
        /// Retient la série à laquelle appartient l'épisode lu, avec ses épisodes :
        /// le retour ramènera à sa page d'épisodes plutôt qu'à l'accueil.
        /// </summary>
        void CaptureSeriesReturn(Channel channel)
        {
            backSeries = null;
            backSeriesEpisodes = null;
            if (channel == null) return;
            bool episode = channel.Kind == "Épisodes" || channel.Kind == "Séries"
                || channel.IsSeries || !String.IsNullOrWhiteSpace(channel.SeriesId)
                || !String.IsNullOrWhiteSpace(channel.Season);
            if (!episode) return;

            Channel series = SeriesForEpisode(channel) ?? VirtualSeriesFor(channel);
            if (series == null) return;

            // Les épisodes du cache de la série sont plus complets que la file de
            // lecture ; sinon on retombe sur les frères déjà chargés.
            List<Channel> episodes = null;
            seriesEpisodes.TryGetValue(SeriesKey(series), out episodes);
            if (episodes == null || episodes.Count == 0) episodes = CurrentEpisodesForRow();
            if (episodes == null || episodes.Count == 0) return;

            backSeries = series;
            backSeriesEpisodes = episodes;
        }

        /// <summary>Série reconstruite depuis un épisode quand la bibliothèque ne la contient pas (reprise).</summary>
        Channel VirtualSeriesFor(Channel episode)
        {
            if (episode == null) return null;
            EpisodeRecord trace = episodeHistory.FirstOrDefault(r => r != null
                && String.Equals(r.Id, episode.Id, StringComparison.OrdinalIgnoreCase));
            string seriesId = episode.SeriesId;
            string name = trace == null ? "" : trace.SeriesName;
            // Sans identifiant de série ni trace d'historique, on ne peut pas
            // reconstruire une fiche fiable : le retour ira à la catégorie.
            if (String.IsNullOrWhiteSpace(seriesId) && String.IsNullOrWhiteSpace(name)) return null;
            if (String.IsNullOrWhiteSpace(name)) name = "Série";
            return new Channel
            {
                Id = "series:" + (String.IsNullOrWhiteSpace(seriesId) ? name : seriesId),
                Name = name, Kind = "Séries", StreamId = seriesId, Group = episode.Group,
                PosterUrl = trace == null ? episode.PosterUrl : trace.PosterUrl,
                Description = episode.Description
            };
        }

        /// <summary>
        /// Quitte le lecteur vers la page d'où la lecture a été lancée, catégorie
        /// comprise. La pile de navigation sert de référence ; à défaut, l'origine
        /// mémorisée est reconstruite à l'identique.
        /// </summary>
        void RestorePlayerOrigin()
        {
            if (navHistory.Count > 0)
            {
                NavFrame frame = navHistory[navHistory.Count - 1];
                if (String.Equals(frame.Page, playerOriginPage, StringComparison.OrdinalIgnoreCase))
                {
                    NavBack();
                    return;
                }
            }
            if (String.IsNullOrWhiteSpace(playerOriginPage) || playerOriginPage == "Lecture" || playerOriginPage == "Épisodes")
            {
                NavBack();
                return;
            }

            // La page est reconstruite avec les filtres mémorisés, sans empiler un
            // doublon dans l'historique.
            if (navHistory.Count > 0 && String.Equals(navHistory[navHistory.Count - 1].Page, playerOriginPage, StringComparison.OrdinalIgnoreCase))
                navHistory.RemoveAt(navHistory.Count - 1);
            page = playerOriginPage;
            activeKind = playerOriginKind;
            activeGroup = playerOriginGroup;
            if (page != "Recherche") ClearSearch();
            RebuildSidebar();
            Navigate();
            if (playerOriginScroll > 0.5 && mainScroll != null)
            {
                double offset = playerOriginScroll;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    try { mainScroll.ScrollToVerticalOffset(offset); } catch { }
                }), DispatcherPriority.Background);
            }
            UpdateBackButton();
        }

        /// <summary>Remonte à la vidéo et reprend la lecture si elle était en pause.</summary>
        void FocusPlayer()
        {
            if (mainScroll != null) mainScroll.ScrollToTop();
            if (playbackPaused) TogglePlayback();
            ShowFullscreenControls();
        }

        /// <summary>Fait défiler la page jusqu'à la rangée d'épisodes.</summary>
        void ScrollToEpisodes()
        {
            if (episodesSection == null || mainScroll == null)
            {
                ShowToast("Aucun épisode à afficher pour ce contenu.", false, false);
                return;
            }
            try
            {
                var point = episodesSection.TranslatePoint(new Point(0, 0), playerLayout);
                mainScroll.ScrollToVerticalOffset(mainScroll.VerticalOffset + point.Y - 16);
            }
            catch { }
            if (playerControlWrap != null) ShowFullscreenControls();
        }

        /// <summary>
        /// Hauteur de la vidéo : tout l'écran en plein écran, sinon le 16:9 de la
        /// largeur sans jamais manger la fiche du contenu posée dessous.
        /// </summary>
        void UpdatePlayerVideoSize()
        {
            if (videoPanel == null) return;
            double width = 0, viewport = 0;
            if (mainScroll != null)
            {
                width = mainScroll.ViewportWidth;
                viewport = mainScroll.ViewportHeight;
            }
            if (width <= 1) width = ActualWidth;
            if (viewport <= 1) viewport = ActualHeight;
            double height;
            if (fullScreen)
            {
                // Plein écran : la vidéo occupe exactement la surface de la
                // fenêtre. On mesure la grille racine plutôt que le viewport du
                // défileur : juste après le masquage de l'en-tête et du pied de
                // page, ce viewport garde encore son ancienne valeur et laisse
                // une bande noire sous l'image.
                double surface = rootGrid != null && rootGrid.ActualHeight > 1 ? rootGrid.ActualHeight : ActualHeight;
                if (surface > 1) viewport = surface;
                height = viewport;
            }
            else
            {
                // Vidéo au 16:9 de la largeur, sans jamais coller la fiche contre
                // le bas : on garde toujours une amorce de la fiche sous l'image.
                double ideal = width * 9.0 / 16.0;
                double cap = Math.Max(320, viewport - 150);
                height = Math.Min(ideal, cap);
            }
            if (height < 240) height = 240;
            videoPanel.Height = Math.Round(height);
        }

        Border MetaChip(string label)
        {
            return new Border
            {
                Background = Brush(Raised), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 3, 9, 4), Margin = new Thickness(0, 0, 7, 7),
                Child = Text(label, 10.5, Brush(TextPrimary), false)
            };
        }

        void UpdatePlaybackInfo()
        {
            if (playbackInfoText == null) return;
            string text;
            if (currentPlaying != null && currentPlaying.Kind == "Local")
            {
                text = "Fichier local";
                MediaTrackScan scan = CurrentTrackScan();
                if (scan != null && scan.Audio.Count > 0)
                {
                    text += "  ·  audio " + scan.Audio.Count + " piste(s)";
                    if (!String.IsNullOrWhiteSpace(scan.Audio[0].CodecLabel)) text += "  ·  " + scan.Audio[0].CodecLabel;
                }
                else if (player != null && player.UsingVlc)
                {
                    var streams = player.AudioStreams();
                    if (streams.Count > 0)
                    {
                        text += "  ·  audio " + streams.Count + " piste(s)";
                        if (!String.IsNullOrWhiteSpace(streams[0].CodecText)) text += "  ·  " + streams[0].CodecText;
                    }
                }
                if (playbackOpenedAt != DateTime.MinValue && player != null && !player.HasVideo)
                    text = "Audio seul  ·  " + text;
            }
            else
            {
                text = playbackCandidateIndex > 0 ? "Flux de secours" : "Flux";
                if (playbackConnectMs > 0) text += "  ·  connexion " + playbackConnectMs.ToString() + " ms";
                string type = ShortContentType(playbackContentType);
                if (type.Length > 0) text += "  ·  " + type;
                if (playbackCandidates != null && playbackCandidates.Count > 1)
                    text += "  ·  " + (playbackCandidateIndex + 1) + "/" + playbackCandidates.Count;
            }
            playbackInfoText.Text = text;
        }

        /// <summary>
        /// Vérifie une fois la lecture démarrée si le flux contient une image.
        /// Un fichier audio seul affiche alors sa propre surface au lieu d'un
        /// cadre noir, sans gêner les contrôles ni les sous-titres.
        /// </summary>
        void ScheduleAudioOnlyCheck()
        {
            if (audioOnlyCheckTimer == null)
            {
                audioOnlyCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                audioOnlyCheckTimer.Tick += delegate
                {
                    audioOnlyCheckTimer.Stop();
                    UpdateAudioOnlySurface();
                };
            }
            audioOnlyCheckTimer.Stop();
            audioOnlyCheckTimer.Start();
        }

        void UpdateAudioOnlySurface()
        {
            if (audioOnlyPanel == null) return;
            bool audioOnly = playbackOpenedAt != DateTime.MinValue && player != null && !player.HasVideo;
            if (!audioOnly)
            {
                audioOnlyPanel.Visibility = Visibility.Collapsed;
                return;
            }
            if (audioOnlyTitle != null && currentPlaying != null) audioOnlyTitle.Text = currentPlaying.Name;
            string detail = "";
            if (player != null && player.UsingVlc)
            {
                List<AudioStream> streams = player.AudioStreams();
                if (streams.Count > 0)
                {
                    AudioStream active = null;
                    int current = player.CurrentAudioTrack;
                    foreach (AudioStream stream in streams)
                    {
                        if (stream.Id == current) { active = stream; break; }
                    }
                    if (active == null) active = streams[0];
                    detail = active.Label;
                }
            }
            if (detail.Length == 0)
            {
                MediaTrackScan scan = CurrentTrackScan();
                if (scan != null && scan.Audio.Count > 0) detail = scan.Audio[0].Label;
            }
            if (detail.Length == 0 && currentPlaying != null && currentPlaying.Kind == "Local")
            {
                string extension = Path.GetExtension(TextUtil.LocalPathFromUrl(currentPlaying.Url)).TrimStart('.').ToUpperInvariant();
                detail = extension.Length == 0 ? "Fichier audio" : "Fichier audio  ·  " + extension;
            }
            if (audioOnlyDetail != null) audioOnlyDetail.Text = detail;
            audioOnlyPanel.Visibility = Visibility.Visible;
        }

        static string ShortContentType(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            string type = value.Trim().ToLowerInvariant();
            int semi = type.IndexOf(';');
            if (semi >= 0) type = type.Substring(0, semi).Trim();
            if (type == "video/mp2t" || type == "video/mpegts" || type == "application/mp2t") return "MPEG-TS";
            if (type == "video/x-matroska") return "MKV";
            if (type == "video/mp4" || type == "video/m4v") return "MP4";
            if (type == "video/webm") return "WebM";
            if (type == "video/x-msvideo") return "AVI";
            if (type == "application/vnd.apple.mpegurl" || type == "application/x-mpegurl") return "HLS";
            if (type == "audio/mpeg") return "MP3";
            if (type == "audio/mp4" || type == "audio/x-m4a" || type == "audio/m4a") return "M4A";
            if (type == "audio/flac" || type == "audio/x-flac") return "FLAC";
            if (type == "audio/ogg" || type == "application/ogg") return "OGG";
            if (type == "audio/opus") return "Opus";
            if (type == "audio/wav" || type == "audio/x-wav" || type == "audio/wave") return "WAV";
            if (type == "audio/aac" || type == "audio/x-aac") return "AAC";
            if (type == "audio/x-ms-wma" || type == "audio/wma") return "WMA";
            if (type == "audio/ac3") return "AC-3";
            if (type == "audio/eac3") return "E-AC-3";
            if (type.StartsWith("video/", StringComparison.Ordinal)) return type.Substring(6).ToUpperInvariant();
            if (type.StartsWith("audio/", StringComparison.Ordinal)) return type.Substring(6).ToUpperInvariant();
            return type;
        }

        Stretch AspectStretch()
        {
            if (playerAspect == "Remplir") return Stretch.UniformToFill;
            if (playerAspect == "Étirer") return Stretch.Fill;
            return Stretch.Uniform;
        }

        /// <summary>
        /// Applique le format d'image choisi. Le mode « Adapter » conserve les
        /// proportions (bandes noires uniquement si le film est plus large que
        /// l'écran), « Remplir » recadre et « Étirer » déforme.
        /// </summary>
        void ApplyPlayerStretch()
        {
            if (player == null) return;
            player.Stretch = AspectStretch();
        }

        // ------------------------------------------------------------ ouverture

        void StartCurrentCandidate()
        {
            if (player == null) return;
            if (!player.IsLoaded) { pendingPlaybackStart = true; return; }
            pendingPlaybackStart = false;
            if (playbackCandidates == null || playbackCandidates.Count == 0)
            {
                ShowPlaybackError("Aucun lien de lecture n'est disponible pour ce contenu.");
                return;
            }
            if (playbackCandidateIndex >= playbackCandidates.Count)
            {
                ShowPlaybackError("Le flux ne peut pas être lu. Vérifiez la source ou réessayez.");
                return;
            }
            string url = playbackCandidates[playbackCandidateIndex];
            int generation = ++playbackProbeGeneration;
            int candidate = playbackCandidateIndex;
            playbackFailed = false;
            if (playerOverlay != null)
            {
                playerOverlay.Visibility = Visibility.Visible;
                playerOverlay.IsHitTestVisible = false;
            }
            if (playerOverlayText != null) playerOverlayText.Text = "Connexion au serveur";
            if (playerBufferBar != null) playerBufferBar.Visibility = Visibility.Visible;
            if (playerOverlayActions != null) playerOverlayActions.Visibility = Visibility.Collapsed;
            if (playerStatusText != null) playerStatusText.Text = "Connexion au serveur";
            playbackConnectMs = 0;
            playbackContentType = "";
            UpdatePlaybackInfo();

            if (!TextUtil.IsHttp(url))
            {
                BeginMedia(url, null, "", 0);
                return;
            }

            string userAgent = currentPlaying == null ? null : currentPlaying.HttpUserAgent;
            string referer = currentPlaying == null ? null : currentPlaying.HttpReferer;
            string origin = currentPlaying == null ? null : currentPlaying.HttpOrigin;
            string cookie = currentPlaying == null ? null : currentPlaying.HttpCookie;
            ThreadPool.QueueUserWorkItem(delegate
            {
                StreamProbeResult probe = StreamProxy.Probe(url, userAgent, referer, origin, cookie, 8000);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (appClosing || player == null || generation != playbackProbeGeneration || candidate != playbackCandidateIndex) return;
                    if (!probe.Ok)
                    {
                        HandleCandidateFailure(String.IsNullOrWhiteSpace(probe.Error) ? "Le flux n'a pas répondu." : probe.Error);
                        return;
                    }
                    BeginMedia(url, probe.UserAgent, probe.ContentType, probe.ElapsedMs);
                }));
            });
        }

        void BeginMedia(string url, string userAgent, string contentType, long connectMs)
        {
            if (player == null) return;
            playbackFailed = false;
            playbackConnectMs = connectMs;
            playbackContentType = contentType ?? "";
            UpdatePlaybackInfo();
            if (playerOverlayText != null) playerOverlayText.Text = "Mise en mémoire tampon";
            if (playerStatusText != null)
                playerStatusText.Text = connectMs > 0 ? "Connexion établie · " + connectMs + " ms" : "Ouverture du flux";
            try
            {
                player.Stop();
                string mediaUrl = StreamProxy.Create(
                    url,
                    String.IsNullOrWhiteSpace(userAgent) ? (currentPlaying == null ? null : currentPlaying.HttpUserAgent) : userAgent,
                    currentPlaying == null ? null : currentPlaying.HttpReferer,
                    currentPlaying == null ? null : currentPlaying.HttpOrigin,
                    currentPlaying == null ? null : currentPlaying.HttpCookie);
                player.Open(ToMediaUri(mediaUrl));
                StartPlaybackOpenTimer();
                AutoLoadSubtitle(url);
            }
            catch (Exception ex)
            {
                HandleCandidateFailure(ex.Message);
            }
        }

        void StartPlaybackOpenTimer()
        {
            StopPlaybackOpenTimer();
            playbackOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            playbackOpenTimer.Tick += delegate
            {
                StopPlaybackOpenTimer();
                HandleCandidateFailure("Le flux ne répond pas (délai d'ouverture dépassé).");
            };
            playbackOpenTimer.Start();
        }

        void StopPlaybackOpenTimer()
        {
            if (playbackOpenTimer == null) return;
            playbackOpenTimer.Stop();
            playbackOpenTimer = null;
        }

        Uri ToMediaUri(string value)
        {
            if (TextUtil.IsStreamScheme(value)) return new Uri(value);
            string path = TextUtil.LocalPathFromUrl(value);
            if (File.Exists(path)) return new Uri(Path.GetFullPath(path));
            return new Uri(value);
        }

        // ---------------------------------------------------------- sous-titres

        void ChooseSubtitle()
        {
            if (currentPlaying == null)
            {
                ShowError("Lancez une lecture avant de charger des sous-titres.");
                return;
            }
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choisir un fichier de sous-titres",
                Filter = SubtitleLoader.Filter,
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;
            LoadSubtitle(dialog.FileName, true);
        }

        void LoadSubtitle(string path, bool announce)
        {
            subtitleSelectionVersion++;
            subtitleBusy = false;
            try
            {
                var track = SubtitleLoader.Load(path);
                if (track == null)
                {
                    ShowError("Aucun sous-titre lisible n'a été trouvé dans ce fichier.");
                    return;
                }
                if (player != null) player.DisableSubtitles();
                activeSubtitle = track;
                activeSubtitlePath = Path.GetFullPath(path);
                activeEmbeddedSubtitleTrack = 0;
                UpdateSubtitleButton();
                UpdateSubtitle();
                if (announce) ShowToast("Sous-titres chargés · " + SubtitleLoader.DisplayName(track), false, true);
            }
            catch (Exception ex)
            {
                ShowError("Impossible de charger les sous-titres : " + ex.Message);
            }
        }

        void AutoLoadSubtitle(string mediaUrl)
        {
            if (!settings.AutoSubtitles) return;
            if (activeSubtitle != null || String.IsNullOrWhiteSpace(mediaUrl)) return;
            string path = TextUtil.LocalPathFromUrl(mediaUrl);
            if (!File.Exists(path)) return;
            var sidecars = SubtitleLoader.FindSidecars(path);
            if (sidecars.Count == 0) return;
            // Priorité au fichier dont le nom porte la langue choisie ; sinon on
            // garde le premier trouvé, comme avant.
            string chosen = sidecars[0];
            foreach (string candidate in sidecars)
            {
                if (SidecarLanguageMatches(Path.GetFileNameWithoutExtension(candidate), settings.SubtitleLanguage))
                {
                    chosen = candidate;
                    break;
                }
            }
            LoadSubtitle(chosen, true);
        }

        /// <summary>Reconnaît « fr », « VFF », « français », « eng »… dans un nom de fichier.</summary>
        static bool SidecarLanguageMatches(string text, string preferred)
        {
            if (String.IsNullOrWhiteSpace(text) || String.IsNullOrWhiteSpace(preferred)) return false;
            string value = TextUtil.NormalizeKey(text);
            string code = preferred.Trim().ToLowerInvariant();
            if (code.Length > 2) code = code.Substring(0, 2);
            string[] tokens;
            switch (code)
            {
                case "fr": tokens = new[] { "vff", "vf", "truefrench", "francais", "french", "fr" }; break;
                case "en": tokens = new[] { "vostfr", "vo", "english", "anglais", "eng", "en" }; break;
                case "es": tokens = new[] { "spanish", "espanol", "espagnol", "spa", "es" }; break;
                case "de": tokens = new[] { "german", "deutsch", "allemand", "ger", "deu", "de" }; break;
                case "it": tokens = new[] { "italian", "italiano", "italien", "ita", "it" }; break;
                case "pt": tokens = new[] { "portuguese", "portugues", "portugais", "por", "pt" }; break;
                case "ar": tokens = new[] { "arabic", "arabe", "ara", "ar" }; break;
                default: tokens = new[] { code }; break;
            }
            foreach (string token in tokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        static bool SubtitleLanguageMatches(string trackCode, string preferred)
        {
            if (String.IsNullOrWhiteSpace(preferred)) return false;
            string wanted = preferred.Trim().ToLowerInvariant();
            if (wanted.Length > 2) wanted = wanted.Substring(0, 2);
            string actual = (trackCode ?? "").Trim().ToLowerInvariant();
            if (actual.Length > 2)
            {
                int dash = actual.IndexOf('-');
                if (dash > 0) actual = actual.Substring(0, dash);
            }
            if (actual == "fre" || actual == "fra") actual = "fr";
            if (actual == "eng") actual = "en";
            if (actual == "spa") actual = "es";
            if (actual == "ger" || actual == "deu") actual = "de";
            if (actual == "ita") actual = "it";
            if (actual == "por") actual = "pt";
            if (actual == "ara") actual = "ar";
            return actual.Length > 0 && actual == wanted;
        }

        void DisableSubtitle()
        {
            subtitleSelectionVersion++;
            subtitleBusy = false;
            if (player != null) player.DisableSubtitles();
            activeSubtitle = null;
            activeSubtitlePath = "";
            activeEmbeddedSubtitleTrack = 0;
            if (subtitleText != null) subtitleText.Text = "";
            if (subtitleBox != null) subtitleBox.Visibility = Visibility.Collapsed;
            UpdateSubtitleButton();
        }

        /// <summary>
        /// Active une piste de sous-titres intégrée : VLC la décode et la dessine
        /// sur l'image. L'affichage interne est coupé pour éviter les doublons.
        /// </summary>
        void SelectSubtitleStream(SubtitleStream track)
        {
            if (track == null || player == null) return;
            // Fichier local avec une piste texte : le rendu interne est plus net
            // et suit le décalage de synchronisation. VLC ne sert que pour les
            // pistes image (PGS) ou les flux distants.
            if (!track.IsBitmap && TryLocalSubtitleFor(track)) return;
            if (!player.UsingVlc)
            {
                ShowToast("Le changement de sous-titres intégrés demande le moteur VLC (Paramètres → Lecture).", true, false);
                return;
            }
            player.SelectSubtitleTrack(track.Id);
            // VLC applique la sélection de façon asynchrone : on vérifie la piste
            // réellement active pendant deux secondes avant de conclure.
            ConfirmSubtitleStream(track, 0);
        }

        /// <summary>
        /// Retrouve la piste texte correspondante dans le fichier local et
        /// l'affiche avec le lecteur interne. Faux si la piste n'est pas locale,
        /// image, ou introuvable.
        /// </summary>
        bool TryLocalSubtitleFor(SubtitleStream track)
        {
            MediaTrackScan scan = CurrentTrackScan();
            if (scan == null || track.Index < 1 || track.Index > scan.Subtitles.Count) return false;
            MediaTrackInfo local = scan.Subtitles[track.Index - 1];
            if (local == null || !local.IsSelectable) return false;
            if (!String.IsNullOrWhiteSpace(track.LanguageCode) && !String.IsNullOrWhiteSpace(local.LanguageCode)
                && !SubtitleLanguageMatches(local.LanguageCode, track.LanguageCode)) return false;
            SelectEmbeddedSubtitle(local);
            return true;
        }

        /// <summary>
        /// Attend que VLC ait réellement basculé sur la piste demandée. Évite le
        /// faux échec provoqué par un code retour de libVLC peu fiable.
        /// </summary>
        void ConfirmSubtitleStream(SubtitleStream track, int attempt)
        {
            if (page != "Lecture" || player == null) return;
            if (player.CurrentSubtitleTrack == track.Id)
            {
                subtitleSelectionVersion++;
                subtitleBusy = false;
                activeSubtitle = null;
                activeSubtitlePath = "";
                activeEmbeddedSubtitleTrack = 0;
                if (subtitleText != null) subtitleText.Text = "";
                if (subtitleBox != null) subtitleBox.Visibility = Visibility.Collapsed;
                UpdateSubtitleButton();
                ShowToast("Sous-titres · " + track.Label, false, true);
                ShowFullscreenControls();
                return;
            }
            if (attempt >= 10) { FallbackEmbeddedSubtitle(track); return; }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += delegate { timer.Stop(); ConfirmSubtitleStream(track, attempt + 1); };
            timer.Start();
        }

        /// <summary>
        /// Si VLC refuse la piste, on affiche soi-même la piste texte du fichier
        /// local (même rang) plutôt que de laisser le spectateur sans sous-titres.
        /// </summary>
        void FallbackEmbeddedSubtitle(SubtitleStream track)
        {
            MediaTrackScan scan = CurrentTrackScan();
            if (scan != null && track.Index >= 1 && track.Index <= scan.Subtitles.Count)
            {
                MediaTrackInfo local = scan.Subtitles[track.Index - 1];
                if (local != null && local.IsSelectable
                    && (String.IsNullOrWhiteSpace(track.LanguageCode) || String.IsNullOrWhiteSpace(local.LanguageCode)
                        || SubtitleLanguageMatches(local.LanguageCode, track.LanguageCode)))
                {
                    SelectEmbeddedSubtitle(local);
                    return;
                }
            }
            try { HttpUtil.AppendLog("Sous-titres VLC : piste " + track.Id + " non activable (" + track.Label + ")."); } catch { }
            ShowToast("Cette piste de sous-titres n'a pas pu être activée.", true, false);
        }

        string LocalPlaybackPath()
        {
            string path = TextUtil.LocalPathFromUrl(CurrentPlaybackUrl());
            if (!String.IsNullOrWhiteSpace(path) && File.Exists(path)) return Path.GetFullPath(path);
            path = TextUtil.LocalPathFromUrl(currentPlaying == null ? "" : currentPlaying.Url);
            return !String.IsNullOrWhiteSpace(path) && File.Exists(path) ? Path.GetFullPath(path) : "";
        }

        MediaTrackScan CurrentTrackScan()
        {
            string path = LocalPlaybackPath();
            if (playbackTracks == null || path.Length == 0) return null;
            return String.Equals(playbackTracks.Path, path, StringComparison.OrdinalIgnoreCase) ? playbackTracks : null;
        }

        void StartMediaTrackScan(Channel channel)
        {
            if (channel == null) return;
            string source = channel.Url;
            if (String.IsNullOrWhiteSpace(source) && playbackCandidates != null && playbackCandidates.Count > 0)
                source = playbackCandidates[0];
            string path = TextUtil.LocalPathFromUrl(source);
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            string fullPath = Path.GetFullPath(path);
            int generation = ++playbackTracksGeneration;
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = MediaTrackScanner.Scan(fullPath); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    try { HttpUtil.AppendLog("Analyse des pistes : " + e.Error.GetBaseException().Message); } catch { }
                    return;
                }
                if (appClosing || generation != playbackTracksGeneration || page != "Lecture") return;
                playbackTracks = e.Result as MediaTrackScan;
                playbackTracksPath = playbackTracks == null ? "" : playbackTracks.Path;
                UpdateAudioButton();
                UpdateSubtitleButton();
                UpdatePlaybackInfo();
                // Sous-titre intégré automatique, dans la langue choisie par le
                // profil, sans jamais activer une piste forcée par surprise.
                if (settings.AutoSubtitles && activeSubtitle == null && playbackTracks != null)
                {
                    MediaTrackInfo automatic = playbackTracks.Subtitles.FirstOrDefault(delegate(MediaTrackInfo track)
                    {
                        return track != null && track.IsSelectable && !track.IsForced
                            && SubtitleLanguageMatches(track.LanguageCode, settings.SubtitleLanguage);
                    });
                    if (automatic != null) SelectEmbeddedSubtitle(automatic);
                }
            };
            worker.RunWorkerAsync();
        }

        void ShowTrackMenu(ContextMenu menu, Button target)
        {
            if (menu == null || target == null) return;
            menu.PlacementTarget = target;
            menu.Placement = PlacementMode.Top;
            menu.IsOpen = true;
        }

        void OpenSubtitleMenu()
        {
            OpenTrackMenu(subtitleButton, false);
        }

        /// <summary>
        /// Menu du bouton « piste audio » : les pistes réellement présentes dans
        /// le fichier, avec leur langue, leur version (VF, VO, VOSTFR…) et leur
        /// format — Français · VF · 5.1 · AC-3 — comme dans MyTVOnline.
        /// </summary>
        void OpenAudioTrackMenu()
        {
            OpenTrackMenu(audioButton, true);
        }

        /// <summary>
        /// Deux menus distincts, comme les deux boutons du lecteur :
        /// <paramref name="audio"/> choisit la piste audio, sinon les sous-titres
        /// (pistes intégrées, fichiers voisins, fichier local, OpenSubtitles).
        /// </summary>
        void OpenTrackMenu(Button anchor, bool audio)
        {
            if (currentPlaying == null || anchor == null) return;
            ShowFullscreenControls();
            MediaTrackScan scan = CurrentTrackScan();
            string path = LocalPlaybackPath();
            var menu = CreateContextMenu();
            if (audio) BuildAudioMenu(menu, scan, path);
            else BuildSubtitleMenu(menu, scan, path);

            menu.Items.Add(new Separator());
            if (!String.IsNullOrWhiteSpace(path))
                menu.Items.Add(MenuItem("Ouvrir dans le lecteur externe", delegate { OpenInExternalPlayer(CurrentPlaybackUrl()); }, Glyphs.External));
            ShowTrackMenu(menu, anchor);
        }

        void BuildAudioMenu(ContextMenu menu, MediaTrackScan scan, string path)
        {
            // Le moteur VLC connaît les pistes réelles du flux, y compris pour les
            // fichiers distants qu'aucun analyseur local ne peut ouvrir. libVLC
            // fournit aussi la langue, le codec, les canaux et le débit, même
            // quand le serveur ne nomme pas ses pistes.
            List<AudioStream> streamTracks = player == null
                ? new List<AudioStream>()
                : player.AudioStreams();
            int audioCount = scan != null && scan.Audio.Count > 0 ? scan.Audio.Count : streamTracks.Count;
            menu.Items.Add(SectionItem(audioCount > 0
                ? "PISTES AUDIO  ·  " + audioCount.ToString(CultureInfo.InvariantCulture)
                : "PISTES AUDIO"));
            if (streamTracks.Count > 0)
            {
                // Quand l'analyse du fichier local connaît les mêmes pistes, ses
                // informations (langue, canaux, codec) complètent celles de VLC.
                // Le pilotage se fait toujours par l'identifiant VLC.
                bool labelled = scan != null && scan.Audio.Count == streamTracks.Count;
                int current = player.CurrentAudioTrack;
                for (int i = 0; i < streamTracks.Count; i++)
                {
                    var captured = streamTracks[i];
                    if (labelled) captured.Enrich(scan.Audio[i]);
                    bool active = current >= 0 ? captured.Id == current : i == 0;
                    string label = captured.Label;
                    menu.Items.Add(MenuItem(label + (active ? "  ✓" : ""),
                        delegate { SwitchAudioStream(captured); }, active ? Glyphs.Check : Glyphs.Music));
                }
                menu.Items.Add(InfoItem(streamTracks.Count > 1
                    ? "Changement immédiat, sans couper la lecture. VLC décode l'AC-3, l'E-AC-3, le DTS, le TrueHD, l'AAC, l'Opus, le FLAC et le PCM."
                    : "Piste lue par VLC, avec décodage complet du codec."));
                AddAudioRepairItems(menu);
                return;
            }
            if (scan == null)
            {
                menu.Items.Add(InfoItem(String.IsNullOrWhiteSpace(path)
                    ? "Flux en direct : le serveur choisit la piste audio."
                    : "Analyse des pistes audio en cours…"));
                return;
            }
            if (scan.Audio.Count == 0)
            {
                menu.Items.Add(InfoItem("Aucune piste audio détectée dans ce fichier."));
                return;
            }
            bool hasVlc = VlcPath() != null;
            int activeIndex = scan.Audio.FindIndex(delegate(MediaTrackInfo track) { return track != null && track.IsDefault; });
            if (activeIndex < 0) activeIndex = 0;
            for (int i = 0; i < scan.Audio.Count; i++)
            {
                var captured = scan.Audio[i];
                bool active = i == activeIndex;
                string suffix = active ? "piste lue" : (hasVlc ? "lire dans VLC" : "VLC requis");
                menu.Items.Add(MenuItem(captured.Label + "  ·  " + suffix + (active ? "  ✓" : ""),
                    delegate { SelectAudioTrack(captured); }, active ? Glyphs.Check : Glyphs.Music));
            }
            if (scan.Audio.Count > 1)
                menu.Items.Add(InfoItem("Le moteur Windows ne change pas de piste dans le lecteur : les autres pistes s'ouvrent dans VLC, à la position courante."));
            AddAudioRepairItems(menu);
        }

        /// <summary>
        /// Dépannage audio : relance la sortie du moteur VLC quand un film ou un
        /// épisode joue sans son parce que le périphérique refuse le format.
        /// </summary>
        void AddAudioRepairItems(ContextMenu menu)
        {
            menu.Items.Add(new Separator());
            if (player == null) return;
            if (!player.UsingVlc)
            {
                if (VlcRuntime.Available)
                    menu.Items.Add(InfoItem("Moteur Windows : le son et les pistes sont gérés par le système. Choisissez « VLC intégré » dans les paramètres pour le décodage complet."));
                return;
            }
            if (player.AudioOutputMissing)
                menu.Items.Add(InfoItem("Aucune sortie audio active : le périphérique refuse le format de ce flux."));
            menu.Items.Add(MenuItem("Réparer le son (relancer la sortie audio)",
                delegate { RepairPlayerAudio(); }, Glyphs.Volume));
        }

        void RepairPlayerAudio()
        {
            if (player == null || !player.UsingVlc)
            {
                ShowError("La réparation du son demande le moteur VLC. Il s'active dans Paramètres → Lecture → Moteur de lecture.");
                return;
            }
            if (!player.RepairAudio())
            {
                ShowError("Aucune sortie audio n'a pu être activée. Vérifiez le périphérique de lecture de Windows ou choisissez une autre sortie dans les paramètres.");
                return;
            }
            ShowToast("Sortie audio relancée.", false, true);
            UpdateAudioButton();
        }

        void BuildSubtitleMenu(ContextMenu menu, MediaTrackScan scan, string path)
        {
            // Pistes réellement présentes dans le fichier ou le flux : libVLC les
            // connaît même à distance, avec leur langue et leur type (PGS, SubRip…).
            List<SubtitleStream> streamTracks = player == null ? new List<SubtitleStream>() : player.SubtitleStreams();
            int currentVlc = player == null ? -1 : player.CurrentSubtitleTrack;
            bool vlcActive = currentVlc >= 0;
            menu.Items.Add(SectionItem("SOUS-TITRES"));
            menu.Items.Add(MenuItem(activeSubtitle == null && !vlcActive ? "Désactiver les sous-titres  ✓" : "Désactiver les sous-titres",
                delegate { DisableSubtitle(); }, activeSubtitle == null && !vlcActive ? Glyphs.Check : Glyphs.Close));
            if (streamTracks.Count > 0)
            {
                menu.Items.Add(SectionItem("PISTES INTÉGRÉES  ·  " + streamTracks.Count.ToString(CultureInfo.InvariantCulture)));
                foreach (var track in streamTracks)
                {
                    var captured = track;
                    bool active = captured.Id == currentVlc;
                    menu.Items.Add(MenuItem(captured.Label + (active ? "  ✓" : ""),
                        delegate { SelectSubtitleStream(captured); }, active ? Glyphs.Check : Glyphs.Subtitles));
                }
                menu.Items.Add(InfoItem("Choisissez une piste : elle est décodée et affichée immédiatement, sans relancer la lecture."));
            }
            if (scan != null && scan.Subtitles.Count > 0 && streamTracks.Count == 0)
            {
                foreach (var track in scan.Subtitles)
                {
                    var captured = track;
                    bool active = activeSubtitle != null && activeEmbeddedSubtitleTrack == captured.TrackNumber;
                    var item = MenuItem(captured.Label + (active ? "  ✓" : ""),
                        delegate { SelectEmbeddedSubtitle(captured); }, active ? Glyphs.Check : Glyphs.Subtitles);
                    if (!captured.IsSelectable) item.IsEnabled = false;
                    menu.Items.Add(item);
                }
            }
            var sidecars = String.IsNullOrWhiteSpace(path) ? new List<string>() : SubtitleLoader.FindSidecars(path);
            if (sidecars.Count > 0)
            {
                menu.Items.Add(SectionItem("FICHIERS À CÔTÉ DU MÉDIA"));
                foreach (string sidecar in sidecars)
                {
                    string captured = sidecar;
                    bool active = activeSubtitle != null && activeEmbeddedSubtitleTrack == 0 &&
                        String.Equals(activeSubtitlePath, Path.GetFullPath(captured), StringComparison.OrdinalIgnoreCase);
                    var item = MenuItem(SubtitleLoader.SidecarLabel(captured) + (active ? "  ✓" : ""),
                        delegate { LoadSubtitle(captured, true); }, active ? Glyphs.Check : Glyphs.OpenFile);
                    item.ToolTip = Path.GetFileName(captured);
                    menu.Items.Add(item);
                }
            }
            if (scan == null && !String.IsNullOrWhiteSpace(path) && sidecars.Count == 0)
                menu.Items.Add(InfoItem("Analyse des pistes du fichier en cours…"));
            else if (scan != null && scan.Subtitles.Count == 0 && sidecars.Count == 0)
                menu.Items.Add(InfoItem("Aucun sous-titre intégré détecté dans ce fichier."));
            else if (scan == null && String.IsNullOrWhiteSpace(path))
                menu.Items.Add(InfoItem("Les sous-titres intégrés sont lus pour les fichiers locaux."));

            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Ouvrir un fichier de sous-titres…", ChooseSubtitle, Glyphs.OpenFile));
            menu.Items.Add(MenuItem("Chercher sur OpenSubtitles…", ShowSubtitleSearch, Glyphs.Globe));
        }

        MenuItem SectionItem(string label)
        {
            var item = MenuItem(label, null, "");
            item.IsEnabled = false;
            return item;
        }

        MenuItem InfoItem(string label)
        {
            var item = MenuItem(label, null, Glyphs.Info);
            item.IsEnabled = false;
            return item;
        }

        static string AudioStreamLabel(AudioStream track)
        {
            if (track == null) return "";
            return TextUtil.Ellipsis(track.Label, 78);
        }

        void SwitchAudioStream(AudioStream track)
        {
            if (player == null || track == null) return;
            if (!player.SelectAudioTrack(track.Id))
            {
                ShowError("Impossible de changer de piste audio sur ce flux.");
                return;
            }
            ShowToast("Piste audio · " + AudioStreamLabel(track), false, true);
            UpdateAudioButton();
            UpdateAudioOnlySurface();
        }

        /// <summary>VLC reste le moteur capable de changer de piste audio à la volée.</summary>
        string VlcPath()
        {
            string external = FindExternalPlayer();
            if (!String.IsNullOrWhiteSpace(external)
                && Path.GetFileName(external).IndexOf("vlc", StringComparison.OrdinalIgnoreCase) >= 0) return external;
            return null;
        }

        void SelectEmbeddedSubtitle(MediaTrackInfo info)
        {
            if (info == null || !info.IsSelectable)
            {
                ShowError("Ce format de sous-titres image n'est pas pris en charge par le lecteur intégré.");
                return;
            }
            MediaTrackScan scan = CurrentTrackScan();
            if (scan == null) return;
            if (activeSubtitle != null && activeEmbeddedSubtitleTrack == info.TrackNumber) return;
            string cacheKey = scan.Path + "|" + info.TrackNumber.ToString();
            SubtitleTrack cached;
            if (embeddedSubtitleCache.TryGetValue(cacheKey, out cached) && cached != null)
            {
                activeSubtitle = cached;
                activeSubtitlePath = "";
                activeEmbeddedSubtitleTrack = info.TrackNumber;
                UpdateSubtitleButton();
                UpdateSubtitle();
                return;
            }
            if (subtitleBusy) return;
            subtitleBusy = true;
            int version = ++subtitleSelectionVersion;
            int generation = playbackTracksGeneration;
            ShowToast("Extraction des sous-titres embarqués", false, false);
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = MediaTrackScanner.LoadEmbeddedSubtitle(scan, info); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                subtitleBusy = false;
                if (appClosing || page != "Lecture" || version != subtitleSelectionVersion || generation != playbackTracksGeneration) return;
                var track = e.Result as SubtitleTrack;
                if (track == null || track.Cues.Count == 0)
                {
                    ShowError("Impossible de lire cette piste de sous-titres. Utilisez un fichier SRT ou WebVTT externe.");
                    return;
                }
                embeddedSubtitleCache[cacheKey] = track;
                activeSubtitle = track;
                activeSubtitlePath = "";
                activeEmbeddedSubtitleTrack = info.TrackNumber;
                UpdateSubtitleButton();
                UpdateSubtitle();
                ShowToast("Sous-titres · " + info.Label, false, true);
            };
            worker.RunWorkerAsync();
        }


        void SelectAudioTrack(MediaTrackInfo track)
        {
            if (track == null) return;
            if (player != null && player.UsingVlc)
            {
                // L'ordinal du fichier local ne coïncide pas forcément avec
                // l'identifiant interne de VLC : on retrouve la piste par sa place.
                List<AudioStream> streams = player.AudioStreams();
                AudioStream target = track.Ordinal > 0 && track.Ordinal <= streams.Count
                    ? streams[track.Ordinal - 1] : null;
                if (target != null && player.SelectAudioTrack(target.Id))
                {
                    ShowToast("Piste audio · " + AudioStreamLabel(target), false, true);
                    UpdateAudioButton();
                    UpdateAudioOnlySurface();
                }
                else ShowError("Impossible de changer de piste audio sur ce flux.");
                return;
            }
            MediaTrackScan scan = CurrentTrackScan();
            bool isDefault = track.IsDefault || (scan != null && scan.Audio.TrueForAll(delegate(MediaTrackInfo other) { return other == null || !other.IsDefault; }) && track.Ordinal <= 1);
            if (isDefault)
            {
                ShowToast("Le lecteur intégré lit déjà cette piste audio.", false, false);
                return;
            }
            OpenAudioTrackInExternalPlayer(track);
        }

        void OpenAudioTrackInExternalPlayer(MediaTrackInfo track)
        {
            string path = LocalPlaybackPath();
            if (String.IsNullOrWhiteSpace(path))
            {
                ShowError("Le changement de piste audio n'est disponible que pour les fichiers locaux.");
                return;
            }
            // MediaElement (Media Foundation) ne sait pas changer de flux audio :
            // VLC le fait en reprenant le fichier à la même position.
            string playerPath = VlcPath();
            if (String.IsNullOrWhiteSpace(playerPath))
            {
                ShowError("Le moteur Windows ne peut pas changer de piste audio dans le lecteur. Installez VLC, ou indiquez-le dans les paramètres, pour lire cette piste à la même position.");
                return;
            }
            double position = 0;
            if (player != null)
            {
                try { position = player.Position.TotalSeconds; } catch { }
            }
            string arguments = "\"" + path + "\" --audio-track=" + (track.Ordinal - 1).ToString();
            if (position > 1) arguments += " --start-time=" + ((long)position).ToString();
            try
            {
                Process.Start(new ProcessStartInfo(playerPath, arguments) { UseShellExecute = true });
                ShowToast("Piste " + track.Ordinal + " ouverte dans VLC"
                    + (position > 1 ? " à " + TextUtil.FormatPosition(TimeSpan.FromSeconds(position)) : "") + ".", false, true);
            }
            catch (Exception ex)
            {
                ShowError("Impossible d'ouvrir le lecteur externe : " + ex.Message);
            }
        }

        // ------------------------------------------------------------- médias

        internal void NavigateForTest(string destination)
        {
            if (String.IsNullOrWhiteSpace(destination)) return;
            Nav(destination.Trim());
        }

        internal void PlayTargetForTest(string target)
        {
            PlayExternalTarget(target);
        }

        void PlayExternalTarget(string target)
        {
            if (String.IsNullOrWhiteSpace(target)) return;
            // Un identifiant de la bibliothèque (Xtream, démo, série) peut être
            // passé directement à --play pour ouvrir un contenu précis.
            Channel known = FindChannel(target);
            if (known != null) { Play(known, null, GetSavedPosition(known), false); return; }
            string name = target;
            try
            {
                if (TextUtil.IsHttp(target))
                {
                    var uri = new Uri(target);
                    name = Path.GetFileName(uri.LocalPath);
                    if (String.IsNullOrWhiteSpace(name)) name = uri.Host;
                }
                else name = Path.GetFileName(target);
            }
            catch { }
            var channel = new Channel
            {
                Name = String.IsNullOrWhiteSpace(name) ? "Flux externe" : name,
                Group = "Médias locaux", Kind = "Local", Url = target,
                Id = "cli:" + target, Description = target, Order = 0
            };
            if (File.Exists(target)) AddLocalRecent(target);
            Play(channel, null, 0, false);
        }
    }
}
