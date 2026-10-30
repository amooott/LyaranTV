using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LyaranTV
{
    public sealed partial class MainWindow : Window
    {
        internal const string AllGroups = "Toutes les catégories";
        internal const double SeekStepSeconds = 5;
        readonly List<PlaylistSource> sources = new List<PlaylistSource>();
        readonly List<Channel> channels = new List<Channel>();
        readonly List<EpgProgram> loadedEpg = new List<EpgProgram>();
        List<UserProfile> profiles = new List<UserProfile>();
        UserProfile activeProfile;
        bool parentalUnlocked;
        bool tmdbEnriching;
        int tmdbApplied;
        DispatcherTimer tmdbRefreshTimer;
        readonly HashSet<string> favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> hiddenChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> hiddenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> recentIds = new List<string>();
        readonly List<string> recentLocal = new List<string>();
        readonly Dictionary<string, double> positions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, double> durations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        readonly List<EpisodeRecord> episodeHistory = new List<EpisodeRecord>();
        readonly Dictionary<string, List<EpgProgram>> epgById = new Dictionary<string, List<EpgProgram>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, List<EpgProgram>> epgByName = new Dictionary<string, List<EpgProgram>>(StringComparer.OrdinalIgnoreCase);

        AppSettings settings = new AppSettings();
        PlaylistSource activeSource;
        PlaylistLoadingWindow loadingWindow;
        volatile bool loadCancelled;
        /// <summary>Identifie le chargement en cours : seule la dernière
        /// tentative (manuelle ou silencieuse) peut appliquer son résultat.</summary>
        int loadGeneration;
        internal volatile bool appClosing;

        string page = "Accueil";
        string pendingPageAfterLoad = "";
        string activeGroup = AllGroups;
        string activeKind = "Tous";
        string sortMode = "Nom (A → Z)";
        bool gridView;
        string searchQuery = "";
        string pageBeforeSearch = "Accueil";
        int renderLimit = 150;
        // Pile de retour : chaque page mémorise son état (page, filtres, défilement)
        // afin que le bouton Retour revienne exactement au dernier point quitté.
        readonly List<NavFrame> navHistory = new List<NavFrame>();
        Button backButton;

        sealed class NavFrame
        {
            public string Page;
            public string Kind;
            public string Group;
            public double Scroll;
        }
        int renderedCount;
        List<Channel> currentView = new List<Channel>();

        Color Back, Panel, Raised, Accent, Secondary, Muted, TextPrimary, BorderColor, Success, Danger, Warning;
        string themeName = "Sombre";
        string accentHex = "#8B5CF6";
        bool appReady, suppressSearch, fullScreen;
        WindowStyle savedWindowStyle;
        WindowState savedWindowState;
        ResizeMode savedResizeMode;
        Rect savedWindowBounds;
        bool savedTopmost;

        Grid rootGrid;
        DockPanel shell;
        Grid toastHost;
        Border chromeTop, chromeSidebar, chromeStatus;
        StackPanel sidebar;
        ScrollViewer mainScroll;
        TextBox searchBox;
        Button refreshButton;
        TextBlock statusText, statusRight, pageTitle;

        StackPanel channelList;
        ScrollViewer resultsScroll;
        Border moreHost;
        TextBlock resultCountText;
        ComboBox sortPicker;

        PlaybackEngine player;
        Channel currentPlaying;
        List<Channel> playQueue;
        Grid playerLayout, playerVideoGrid;
        Border videoPanel, playerOverlay, playerControlWrap, playerInfoCard;
        DispatcherTimer fullscreenControlsTimer;
        TextBlock playerOverlayText, playerStatusText, currentChannel, elapsedText, totalText, scrubTip;
        TextBlock playerTopTitle, playerTopSub;
        Button playPauseButton, favButton, muteButton, playerCenterButton;
        Slider volumeSlider;
        // Carte de volume flottante (curseur + pourcentage) déployée au survol.
        Border volumeCard;
        Grid volumeHost;
        TextBlock volumeValueText;
        DispatcherTimer volumeCardHideTimer;
        // Barre de progression redessinée à la main : piste fine, remplissage
        // accentué, curseur et infobulle au survol.
        Grid playerScrubber;
        Border scrubTrack, scrubFill, scrubBuffer, scrubThumb, scrubTipBox;
        DockPanel playerTopBar;
        double scrubDuration;
        double scrubValue;
        bool scrubbing;
        DispatcherTimer playbackTimer, searchTimer;
        bool playbackPaused, playerLive, resumeApplied;
        // Volume plein par défaut : le lecteur n'atténue plus le son de 28 % comme
        // avant, ce qui rendait les pistes audio faibles et peu définies.
        double savedVolume = 1.0;
        bool wasMuted;
        string playerAspect = "Adapter";
        List<string> playbackCandidates = new List<string>();
        int playbackCandidateIndex;
        double resumePosition;
        bool resumePaused;

        Border toast;
        TextBlock toastIcon, toastText;
        DispatcherTimer toastTimer;

        DateTime epgDate = DateTime.Today;
        string epgQuery = "";

        public MainWindow()
        {
            Title = "LyaranTV"; Width = 1380; Height = 860; MinWidth = 1080; MinHeight = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Inter, Segoe UI");
            SetValue(Window.IconProperty, CreateAppIcon());
            AppPaths.EnsureDataDir();
            LoadState();
            UiMotion.Reduced = settings.ReduceMotion;
            SetThemeColors(themeName);
            ConfigureSystemResources();
            Background = Brush(Back); Foreground = Brush(TextPrimary);
            searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
            searchTimer.Tick += delegate { searchTimer.Stop(); ApplySearch(); };
            BuildShell();
            UpdateBackButton();
            if (settings.AlwaysOnTop) Topmost = true;
            appReady = true;
            if (settings.Maximized) WindowState = WindowState.Maximized;
            KeyDown += HandleWindowKeys;
            PreviewKeyDown += HandleWindowPreviewKeys;
            // Redimensionnement (ou passage en plein écran) : la vidéo garde
            // toujours la taille exacte de la surface disponible.
            SizeChanged += delegate { UpdatePlayerVideoSize(); };
            SourceInitialized += delegate { ApplyDarkTitleBar(); };
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemThemePreferenceChanged;
            Loaded += delegate
            {
                EnsureActiveProfileUnlocked();
                // Démarrage instantané : le catalogue est relu depuis le cache
                // local sous la fenêtre de progression dédiée. Sans cache, ou si
                // la copie est périmée ou incompatible, le chargement réseau avec
                // sa barre prend le relais.
                if (settings.LoadLastSource && activeSource != null && channels.Count == 0)
                {
                    Activate(activeSource);
                }
                else ShowStartPage();
                ShowStartupSplash();

                // Vérification silencieuse des mises à jour, une fois l'interface
                // ouverte : elle ne doit jamais retarder le démarrage ni bloquer
                // une lecture. Un jeton absent la rend simplement silencieuse.
                if (settings.CheckUpdatesOnStart)
                {
                    var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                    updateTimer.Tick += delegate
                    {
                        updateTimer.Stop();
                        try { CheckForUpdates(true); } catch { }
                    };
                    updateTimer.Start();
                }
            };
            Closing += delegate
            {
                appClosing = true;
                Mouse.OverrideCursor = null;
                SaveCurrentPosition();
                if (player != null) { try { player.Stop(); player.Close(); } catch { } }
                StreamProxy.Clear();
                if (playbackTimer != null) playbackTimer.Stop();
                if (toastTimer != null) toastTimer.Stop();
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemThemePreferenceChanged;
                ArtworkStore.Save();
                SaveState();
            };
        }

        void LoadState()
        {
            settings = SettingsStore.Load() ?? new AppSettings();
            settings.Normalize();
            // La langue est posée avant la construction de l'interface : tous
            // les libellés créés ensuite passent par la table de traduction.
            I18n.Set(settings.Language);
            profiles = ProfileStore.Load();
            activeProfile = profiles.FirstOrDefault(p => String.Equals(p.Name, settings.ActiveProfile, StringComparison.OrdinalIgnoreCase)) ?? profiles.FirstOrDefault();
            if (activeProfile == null)
            {
                activeProfile = new UserProfile { Name = "Principal", AvatarColor = "#8B5CF6", Created = DateTime.Now.ToString("s") };
                profiles.Add(activeProfile);
                ProfileStore.Save(profiles);
            }
            ProfileScope.Current = activeProfile.Name;
            sources.Clear(); sources.AddRange(SourceStore.Load());
            LoadProfileState();
            hiddenChannels.Clear(); foreach (string value in StateStore.ReadSet("hidden-channels.txt")) hiddenChannels.Add(value);
            hiddenGroups.Clear(); foreach (string value in StateStore.ReadSet("hidden-groups.txt")) hiddenGroups.Add(value);
            recentLocal.Clear(); recentLocal.AddRange(StateStore.ReadList("local-files.txt"));
            themeName = String.IsNullOrWhiteSpace(settings.Theme) ? "Sombre" : settings.Theme;
            accentHex = String.IsNullOrWhiteSpace(settings.Accent) ? "#8B5CF6" : settings.Accent;
            sortMode = String.IsNullOrWhiteSpace(settings.SortMode) ? "Nom (A → Z)" : settings.SortMode;
            gridView = settings.GridView;
            savedVolume = settings.Volume <= 0 || settings.Volume > 1 ? 1.0 : settings.Volume;
            wasMuted = settings.Muted;
            playerAspect = String.IsNullOrWhiteSpace(settings.AspectMode) ? "Adapter" : settings.AspectMode;
            activeSource = sources.FirstOrDefault(s => String.Equals(s.Name, settings.LastSourceName, StringComparison.OrdinalIgnoreCase)) ?? sources.FirstOrDefault();
            UiMotion.Reduced = settings.ReduceMotion;
        }

        void LoadProfileState()
        {
            favorites.Clear(); foreach (string value in StateStore.ReadSet("favorites.txt")) favorites.Add(value);
            recentIds.Clear(); recentIds.AddRange(StateStore.ReadList("recent.txt"));
            positions.Clear(); foreach (var pair in StateStore.LoadPositions()) positions[pair.Key] = pair.Value;
            durations.Clear(); foreach (var pair in StateStore.LoadDurations()) durations[pair.Key] = pair.Value;
            episodeHistory.Clear(); episodeHistory.AddRange(StateStore.LoadEpisodes());
        }

        /// <summary>Mémorise l'épisode lu pour le retrouver dans « Reprendre la lecture ».</summary>
        void RememberEpisode(Channel episode)
        {
            if (episode == null || String.IsNullOrWhiteSpace(episode.Id)) return;
            if (!String.Equals(episode.Kind, "Épisodes", StringComparison.OrdinalIgnoreCase) && String.IsNullOrWhiteSpace(episode.SeriesId)) return;
            Channel series = SeriesForEpisode(episode);
            var record = new EpisodeRecord
            {
                Id = episode.Id, Name = episode.Name, Url = episode.Url, Group = episode.Group,
                Season = episode.Season, Episode = episode.Episode, SeriesId = episode.SeriesId,
                SeriesName = series == null ? "" : series.Name,
                PosterUrl = series == null ? episode.PosterUrl : (!String.IsNullOrWhiteSpace(series.PosterUrl) ? series.PosterUrl : series.BackdropUrl)
            };
            episodeHistory.RemoveAll(r => String.Equals(r.Id, record.Id, StringComparison.OrdinalIgnoreCase));
            episodeHistory.Insert(0, record);
            while (episodeHistory.Count > 120) episodeHistory.RemoveAt(episodeHistory.Count - 1);
            StateStore.SaveEpisodes(episodeHistory);
        }

        void SaveSettings()
        {
            settings.Theme = themeName;
            settings.Accent = accentHex;
            settings.Volume = savedVolume;
            settings.Muted = wasMuted;
            settings.SortMode = sortMode;
            settings.GridView = gridView;
            settings.AspectMode = playerAspect;
            settings.LastSourceName = activeSource == null ? "" : activeSource.Name;
            settings.LastChannelId = currentPlaying == null ? settings.LastChannelId : currentPlaying.Id;
            settings.Maximized = WindowState == WindowState.Maximized;
            settings.ActiveProfile = activeProfile == null ? "Principal" : activeProfile.Name;
            SettingsStore.Save(settings);
        }

        void SaveState()
        {
            try
            {
                SaveSettings();
                SourceStore.Save(sources);
                StateStore.WriteSet("favorites.txt", favorites);
                StateStore.WriteSet("hidden-channels.txt", hiddenChannels);
                StateStore.WriteSet("hidden-groups.txt", hiddenGroups);
                StateStore.WriteList("recent.txt", recentIds);
                StateStore.WriteList("local-files.txt", recentLocal);
                StateStore.SavePositions(positions);
                StateStore.SaveDurations(durations);
                StateStore.SaveEpisodes(episodeHistory);
            }
            catch (Exception ex) { HttpUtil.AppendLog("SaveState: " + ex); }
        }

        void SaveSources()
        {
            try { SourceStore.Save(sources); } catch (Exception ex) { HttpUtil.AppendLog("SaveSources: " + ex); }
        }

        void BuildShell()
        {
            rootGrid = new Grid();
            // Fond cinématographique : dégradé sombre, halo d'accent en haut à
            // droite et lueur secondaire en bas à gauche. Les surfaces de
            // l'application restent translucides pour laisser respirer ce fond.
            var backdrop = new Grid { IsHitTestVisible = false };
            backdrop.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(TextPrimary, 10), 0),
                    new GradientStop(Back, 0.34),
                    new GradientStop(Back, 1)
                }, new Point(0.62, 0), new Point(0.08, 0.9))
            });
            var accentHalo = new System.Windows.Shapes.Ellipse
            {
                Width = 1120, Height = 1120, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -650, -360, 0), Opacity = 0.55,
                Fill = new RadialGradientBrush(WithAlpha(Accent, 58), WithAlpha(Accent, 0))
            };
            backdrop.Children.Add(accentHalo);
            backdrop.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 820, Height = 820, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(-420, 0, 0, -520),
                Fill = new RadialGradientBrush(WithAlpha(Secondary, 22), WithAlpha(Secondary, 0))
            });
            // Vignette : les bords se referment doucement, comme une salle sombre.
            backdrop.Children.Add(new Border
            {
                Background = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.42), GradientOrigin = new Point(0.5, 0.42), RadiusX = 0.9, RadiusY = 0.9,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.45),
                        new GradientStop(WithAlpha(Back, 150), 1)
                    }
                }
            });
            if (!UiMotion.Reduced)
            {
                // Dérive très lente du halo : le fond respire sans distraire.
                var pulse = new System.Windows.Media.Animation.DoubleAnimation(0.32, 0.68, TimeSpan.FromSeconds(16))
                {
                    AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
                };
                accentHalo.BeginAnimation(UIElement.OpacityProperty, pulse);
            }
            rootGrid.Children.Add(backdrop);
            shell = new DockPanel();
            rootGrid.Children.Add(shell);
            Content = rootGrid;

            var topBorder = new Border
            {
                Height = 68, BorderBrush = Brush(WithAlpha(BorderColor, 150)), BorderThickness = new Thickness(0, 0, 0, 1),
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(Panel, 236), 0),
                    new GradientStop(WithAlpha(Back, 210), 1)
                }, new Point(0, 0), new Point(0, 1))
            };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBorder.Child = top;
            DockPanel.SetDock(topBorder, Dock.Top); shell.Children.Add(topBorder); chromeTop = topBorder;

            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Cursor = Cursors.Hand };
            backButton = IconButton(Glyphs.Back, "Revenir à la page précédente", NavBack, false);
            backButton.Width = 32; backButton.Height = 32;
            backButton.Margin = new Thickness(0, 0, 10, 0);
            backButton.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(backButton);
            var mark = new Image { Source = CreateLogoMark(), Width = 43, Height = 38, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 9, 0) };
            brand.Children.Add(mark);
            var brandText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            brandText.Children.Add(Text("LyaranTV", 19, Brush(TextPrimary), true));
            brandText.Children.Add(Text("Cinéma, direct et séries", 9, Brush(Muted), false));
            brand.Children.Add(brandText);
            brand.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
                Nav("Accueil");
            };
            Grid.SetColumn(brand, 0); top.Children.Add(brand);

            var searchWrap = new Border
            {
                Height = 40, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Background = Brush(Raised), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11),
                Padding = new Thickness(11, 0, 6, 0)
            };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var searchIcon = Icon(Glyphs.Search, 16, Brush(Muted)); searchIcon.Margin = new Thickness(0, 0, 8, 0); Grid.SetColumn(searchIcon, 0); searchGrid.Children.Add(searchIcon);
            searchBox = StyledTextBox();
            searchBox.Height = 36; searchBox.Padding = new Thickness(0, 5, 0, 5); searchBox.Background = Brushes.Transparent;
            searchBox.BorderThickness = new Thickness(0); searchBox.Foreground = Brush(TextPrimary);
            searchBox.ToolTip = "Rechercher dans votre bibliothèque · Ctrl+F";
            var hint = Text("Rechercher une chaîne, un film, une série…", 12.5, Brush(Muted), false);
            hint.IsHitTestVisible = false; hint.Margin = new Thickness(1, 0, 0, 0);
            var searchHost = new Grid(); searchHost.Children.Add(searchBox); searchHost.Children.Add(hint);
            Grid.SetColumn(searchHost, 1); searchGrid.Children.Add(searchHost);
            var shortcutHint = new Border
            {
                Background = Brush(WithAlpha(TextPrimary, 9)), BorderBrush = Brush(WithAlpha(TextPrimary, 14)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 2, 6, 3),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 2, 0), IsHitTestVisible = false,
                Child = Text("Ctrl F", 8.5, Brush(Muted), true)
            };
            Grid.SetColumn(shortcutHint, 2); searchGrid.Children.Add(shortcutHint);
            searchWrap.Child = searchGrid;
            searchWrap.Margin = new Thickness(12, 0, 12, 0);
            Grid.SetColumn(searchWrap, 1); top.Children.Add(searchWrap);
            searchBox.TextChanged += delegate { ScheduleSearch(); };
            searchBox.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { OpenSearch(); e.Handled = true; }
                else if (e.Key == Key.Escape) { ClearSearch(); e.Handled = true; }
            };
            searchBox.GotKeyboardFocus += delegate { searchWrap.BorderBrush = Brush(Accent); };
            searchBox.LostKeyboardFocus += delegate { searchWrap.BorderBrush = Brush(BorderColor); };
            SetPlaceholder(searchBox, hint);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
            refreshButton = IconButton(Glyphs.Refresh, "Actualiser la playlist active · Ctrl+R", RefreshActiveSource, false);
            refreshButton.IsEnabled = activeSource != null;
            refreshButton.Background = Brushes.Transparent;
            refreshButton.BorderBrush = Brushes.Transparent;
            actions.Children.Add(refreshButton);
            var addButton = IconButton(Glyphs.Add, "Ajouter une playlist", AddPlaylist, false);
            addButton.Background = Brushes.Transparent;
            addButton.BorderBrush = Brushes.Transparent;
            actions.Children.Add(addButton);
            var settingsButton = IconButton(Glyphs.Settings, "Paramètres · thème, lecture, langues", delegate { Nav("Paramètres"); }, false);
            settingsButton.Background = Brushes.Transparent;
            settingsButton.BorderBrush = Brushes.Transparent;
            actions.Children.Add(settingsButton);
            var profileButton = ProfileAvatarButton(36);
            profileButton.Margin = new Thickness(4, 0, 0, 0);
            actions.Children.Add(profileButton);
            Grid.SetColumn(actions, 2); top.Children.Add(actions);

            var body = new DockPanel();
            var sidebarBorder = new Border
            {
                Width = 236, Background = Brush(WithAlpha(Back, 232)), BorderBrush = Brush(WithAlpha(BorderColor, 170)),
                BorderThickness = new Thickness(0, 0, 1, 0)
            };
            sidebar = new StackPanel();
            sidebarBorder.Child = new ScrollViewer { Content = sidebar, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            DockPanel.SetDock(sidebarBorder, Dock.Left); body.Children.Add(sidebarBorder); chromeSidebar = sidebarBorder;

            var statusBorder = new Border { Height = 32, Background = Brush(WithAlpha(Panel, 220)), BorderBrush = Brush(WithAlpha(BorderColor, 190)), BorderThickness = new Thickness(0, 1, 0, 0) };
            var statusGrid = new Grid { Margin = new Thickness(15, 0, 15, 0) };
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusText = Text("Prêt", 11, Brush(Muted), false); Grid.SetColumn(statusText, 0); statusGrid.Children.Add(statusText);
            statusRight = Text("", 10.5, Brush(Muted), false); statusRight.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(statusRight, 1); statusGrid.Children.Add(statusRight);
            statusBorder.Child = statusGrid;
            DockPanel.SetDock(statusBorder, Dock.Bottom); body.Children.Add(statusBorder); chromeStatus = statusBorder;

            mainScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0) };
            mainScroll.SizeChanged += delegate { UpdatePlayerVideoSize(); };
            body.Children.Add(mainScroll);
            shell.Children.Add(body);

            if (toastTimer != null) { toastTimer.Stop(); toastTimer = null; }
            toast = null; toastIcon = null; toastText = null;
            toastHost = new Grid { IsHitTestVisible = false };
            System.Windows.Controls.Panel.SetZIndex(toastHost, 200);
            rootGrid.Children.Add(toastHost);
            RebuildSidebar();
            UpdateStatusBar();
        }

        void RebuildSidebar()
        {
            if (sidebar == null) return;
            sidebar.Children.Clear();
            var nav = new StackPanel { Margin = new Thickness(0, 10, 0, 4) };
            sidebar.Children.Add(nav);
            nav.Children.Add(NavItem(Glyphs.Home, "Accueil", "Accueil", -1));
            nav.Children.Add(NavItem(Glyphs.Library, "Bibliothèque", "Bibliothèque", channels.Count));
            nav.Children.Add(NavItem(Glyphs.Favorite, "Ma liste", "Favoris", favorites.Count));
            nav.Children.Add(NavItem(Glyphs.Recent, "Récents", "Récents", recentIds.Count));
            nav.Children.Add(NavItem(Glyphs.Guide, "Guide EPG", "Guide EPG", loadedEpg.Count));
            nav.Children.Add(NavItem(Glyphs.Local, "Médias locaux", "Médias locaux", -1));

            var section = Text("MES PLAYLISTS", 9.5, Brush(Muted), true);
            section.Margin = new Thickness(22, 18, 0, 6); sidebar.Children.Add(section);
            if (sources.Count == 0)
            {
                var empty = Text("Aucune playlist ajoutée.", 11.5, Brush(Muted), false);
                empty.Margin = new Thickness(22, 4, 12, 6); sidebar.Children.Add(empty);
            }
            foreach (var source in sources)
            {
                var item = SourceSidebarItem(source);
                sidebar.Children.Add(item);
            }
            var add = Button(Glyphs.Add, "Ajouter une playlist", AddPlaylist, false);
            add.Margin = new Thickness(10, 4, 10, 4); add.HorizontalContentAlignment = HorizontalAlignment.Left;
            sidebar.Children.Add(add);

            var spacer = new Border { Height = 1, Background = Brush(BorderColor), Margin = new Thickness(18, 14, 18, 10) };
            sidebar.Children.Add(spacer);
            var manage = new StackPanel();
            sidebar.Children.Add(manage);
            manage.Children.Add(NavItem(Glyphs.Filter, "Catégories", "Catégories", -1));
            manage.Children.Add(NavItem(Glyphs.View, "Chaînes masquées", "Chaînes masquées", hiddenChannels.Count));
            manage.Children.Add(NavItem(Glyphs.Settings, "Paramètres", "Paramètres", -1));
            manage.Children.Add(NavItem(Glyphs.Help, "Guide rapide", "Guide rapide", -1));

            sidebar.Children.Add(ProfileSidebarItem());
            // Sélecteur de langue, tout en bas de la barre latérale.
            var languageHost = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            sidebar.Children.Add(languageHost);
            AddLanguageSidebarItem(languageHost);
            var footer = Text(AppInfo.Name + " " + AppInfo.Version + " · " + TextUtil.HumanCount(VisibleChannels.Count()) + " " + I18n.T("éléments"), 9.5, Brush(Muted), false);
            footer.Margin = new Thickness(22, 10, 12, 18); footer.TextWrapping = TextWrapping.Wrap;
            sidebar.Children.Add(footer);
            // La barre latérale est reconstruite à chaque navigation : on la
            // retraduit ici, sinon elle repasserait en français.
            I18n.Apply(sidebar);
        }

        Button NavItem(string glyph, string label, string destination, int count)
        {
            bool active = page == destination;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var bar = new Border { Width = 3, Height = 20, CornerRadius = new CornerRadius(2), Background = active ? Brush(Accent) : Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(bar, 0); grid.Children.Add(bar);
            var icon = Icon(glyph, 15, active ? Brush(Accent) : Brush(Muted)); icon.Margin = new Thickness(12, 0, 10, 0); Grid.SetColumn(icon, 1); grid.Children.Add(icon);
            var text = Text(label, 13, Brush(TextPrimary), active); Grid.SetColumn(text, 2); grid.Children.Add(text);
            if (count >= 0)
            {
                var badge = Text(TextUtil.HumanCount(count), 10, active ? Brush(TextPrimary) : Brush(Muted), false);
                badge.HorizontalAlignment = HorizontalAlignment.Right; badge.Margin = new Thickness(6, 0, 4, 0); Grid.SetColumn(badge, 3); grid.Children.Add(badge);
            }
            var button = new Button
            {
                Content = grid, Height = 41, Margin = new Thickness(6, 1, 6, 1), Padding = new Thickness(0), Background = active ? AccentSoftBrush : Brushes.Transparent,
                Foreground = Brush(TextPrimary), BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.Hand, FocusVisualStyle = null, Template = RoundedButtonTemplate(10)
            };
            if (!active)
            {
                button.MouseEnter += delegate
                {
                    button.Background = HoverBrush;
                    icon.Foreground = Brush(TextPrimary);
                    bar.Background = Brush(WithAlpha(Accent, 150));
                };
                button.MouseLeave += delegate
                {
                    button.Background = Brushes.Transparent;
                    icon.Foreground = Brush(Muted);
                    bar.Background = Brushes.Transparent;
                };
            }
            button.Click += delegate { Nav(destination); };
            return button;
        }

        Border SourceSidebarItem(PlaylistSource source)
        {
            bool active = activeSource == source;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var bar = new Border { Width = 3, Height = 24, CornerRadius = new CornerRadius(2), Background = active ? Brush(Accent) : Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(bar, 0); grid.Children.Add(bar);
            var icon = Icon(source.IsDemo || source.Type == "Démo" ? Glyphs.Play : source.Type == "Xtream" ? Glyphs.Globe : source.Type == "Portail MAC" ? Glyphs.TV : Glyphs.List, 15, active ? Brush(Accent) : Brush(Muted));
            icon.Margin = new Thickness(11, 0, 9, 0); Grid.SetColumn(icon, 1); grid.Children.Add(icon);
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(Text(source.Name, 12.5, Brush(TextPrimary), active));
            labels.Children.Add(Text(source.DisplayType + (source.LastCount > 0 ? " · " + TextUtil.HumanCount(source.LastCount) : ""), 9.5, Brush(Muted), false));
            Grid.SetColumn(labels, 2); grid.Children.Add(labels);
            if (active) { var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = Brush(Success), Margin = new Thickness(6, 0, 4, 0) }; Grid.SetColumn(dot, 3); grid.Children.Add(dot); }
            var button = new Button
            {
                Content = grid, Height = 49, Margin = new Thickness(6, 1, 6, 1), Padding = new Thickness(0),
                Background = active ? AccentSoftBrush : Brushes.Transparent, BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Cursor = Cursors.Hand, FocusVisualStyle = null, Template = RoundedButtonTemplate(10)
            };
            if (!active)
            {
                button.MouseEnter += delegate { button.Background = HoverBrush; };
                button.MouseLeave += delegate { button.Background = Brushes.Transparent; };
            }
            button.Click += delegate { Activate(source); };
            var menu = CreateContextMenu();
            menu.Items.Add(MenuItem("Charger", delegate { Activate(source); }, Glyphs.Play));
            menu.Items.Add(MenuItem("Actualiser", delegate { Activate(source); }, Glyphs.Refresh));
            menu.Items.Add(MenuItem("Modifier", delegate { EditSource(source); }, Glyphs.Edit));
            menu.Items.Add(MenuItem("Tester la connexion", delegate { TestSourceAsync(source); }, Glyphs.Globe));
            menu.Items.Add(MenuItem("Copier l’adresse", delegate { CopyText(source.Location); }, Glyphs.Copy));
            if (source == activeSource) menu.Items.Add(MenuItem("Exporter en M3U", delegate { ExportM3u(); }, Glyphs.Download));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Supprimer", delegate { RemoveSource(source); }, Glyphs.Delete));
            button.ContextMenu = menu;
            var wrapper = new Border { Child = button };
            return wrapper;
        }

        void Nav(string destination)
        {
            if (fullScreen) ExitFullScreen();
            if (destination != page) PushNavFrame();
            page = destination;
            if (destination != "Recherche") ClearSearch();
            if (destination == "Bibliothèque" || destination == "Favoris" || destination == "Récents")
            {
                activeGroup = AllGroups; activeKind = "Tous";
            }
            RebuildSidebar();
            Navigate();
        }

        void PushNavFrame()
        {
            // « Lecture » et « Épisodes » sont reconstruits à partir d'un contexte
            // (flux en cours, saison) : Navigate() ne peut pas les restaurer.
            if (page == "Lecture" || page == "Épisodes") return;
            var frame = new NavFrame
            {
                Page = page,
                Kind = activeKind,
                Group = activeGroup,
                Scroll = mainScroll == null ? 0 : mainScroll.VerticalOffset
            };
            if (navHistory.Count > 0)
            {
                var last = navHistory[navHistory.Count - 1];
                if (last.Page == frame.Page && last.Kind == frame.Kind && last.Group == frame.Group)
                {
                    last.Scroll = frame.Scroll;
                    UpdateBackButton();
                    return;
                }
            }
            navHistory.Add(frame);
            while (navHistory.Count > 24) navHistory.RemoveAt(0);
            UpdateBackButton();
        }

        void NavBack()
        {
            if (navHistory.Count == 0)
            {
                Nav(activeSource == null || channels.Count == 0 ? "Accueil" : "Bibliothèque");
                return;
            }
            var frame = navHistory[navHistory.Count - 1];
            navHistory.RemoveAt(navHistory.Count - 1);
            if (fullScreen) ExitFullScreen();
            page = frame.Page;
            activeKind = frame.Kind;
            activeGroup = frame.Group;
            if (page != "Recherche") ClearSearch();
            RebuildSidebar();
            Navigate();
            double offset = frame.Scroll;
            UpdateBackButton();
            if (mainScroll != null && offset > 0.5)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    try { mainScroll.ScrollToVerticalOffset(offset); } catch { }
                }), DispatcherPriority.Background);
            }
        }

        void UpdateBackButton()
        {
            if (backButton == null) return;
            bool canGoBack = navHistory.Count > 0;
            backButton.IsEnabled = canGoBack;
            backButton.Opacity = canGoBack ? 1 : 0.4;
        }

        void Navigate()
        {
            SetPageStatus();
            if (page == "Accueil") ShowHome();
            else if (page == "Bibliothèque" || page == "Favoris" || page == "Récents") ShowLibrary(page);
            else if (page == "Recherche") ShowSearch();
            else if (page == "Médias locaux") ShowLocalMedia();
            else if (page == "Catégories") ShowCategories();
            else if (page == "Chaînes masquées") ShowHiddenChannels();
            else if (page == "Guide EPG") ShowEpg();
            else if (page == "Paramètres") ShowSettings();
            else if (page == "Guide rapide") ShowGuide();
            else ShowHome();
        }

        void ShowStartPage()
        {
            if (activeSource == null || channels.Count == 0) { ShowHome(); return; }
            if (settings.StartPage == "Bibliothèque") ShowLibrary("Bibliothèque");
            else if (settings.StartPage == "Favoris") ShowLibrary("Favoris");
            else if (settings.StartPage == "Guide EPG") ShowEpg();
            else ShowHome();
        }

        void SetContent(UIElement element)
        {
            if (page != "Accueil") StopHomeHeroTimer();
            StopLibraryHeroTimer();
            if (page != "Lecture") StopFullscreenControlsTimer();
            if (page != "Lecture" && player != null)
            {
                SaveCurrentPosition();
                try { player.Stop(); player.Close(); } catch { }
                player = null;
                if (playbackTimer != null) playbackTimer.Stop();
            }
            // Les éléments du lecteur quittent l'arbre visuel : on oublie leurs
            // références pour ne jamais piloter un contrôle détaché.
            if (page != "Lecture")
            {
                volumeCard = null;
                volumeValueText = null;
                volumeHost = null;
            }
            if (mainScroll != null)
            {
                // Le lecteur défile désormais : la fiche et les épisodes vivent
                // sous la vidéo, dans la continuité de l'image. La barre de
                // défilement reste disponible partout, y compris dans la liste
                // des épisodes d'une série (molette et glisser du curseur).
                mainScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                mainScroll.Content = element;
                // Traduction de la page qui vient d'être bâtie : les libellés
                // sont traduits au moment de l'affichage, pas à la compilation.
                I18n.Apply(element);
                if (I18n.IsTranslated)
                {
                    // Les bannières et les rangées de cartes se remplissent
                    // parfois une fraction de seconde plus tard : seconde passe
                    // pour ne pas laisser de libellés en français.
                    var localize = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                    localize.Tick += delegate { localize.Stop(); try { I18n.Apply(element); } catch { } };
                    localize.Start();
                }
                mainScroll.ScrollToTop();
                // Entrée de page : fondu et léger glissement, coupés si
                // l'utilisateur a demandé moins d'animations.
                var pageElement = element as FrameworkElement;
                if (pageElement != null && !UiMotion.Reduced)
                {
                    pageElement.Opacity = 0;
                    var slide = new TranslateTransform(0, 12);
                    pageElement.RenderTransform = slide;
                    pageElement.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
                    slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(260))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                }
                else if (pageElement != null) pageElement.Opacity = 1;
            }
            ApplyPlayerChrome();
        }

        StackPanel PageRoot(string title, string subtitle)
        {
            pageTitle = Text(title, 27, Brush(TextPrimary), true);
            var root = new StackPanel { Margin = new Thickness(30, 24, 30, 30) };
            root.Children.Add(pageTitle);
            root.Children.Add(Text(subtitle, 13, Brush(Muted), false));
            var gap = new Border { Height = 20 }; root.Children.Add(gap);
            return root;
        }

        HashSet<string> LockedGroupSet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (String.IsNullOrWhiteSpace(settings.LockedGroups)) return set;
            foreach (string value in settings.LockedGroups.Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string group = value.Trim();
                if (group.Length > 0) set.Add(group);
            }
            return set;
        }

        bool IsChannelAllowed(Channel channel)
        {
            if (channel == null) return false;
            if (activeProfile == null || !activeProfile.IsChild || !settings.ParentalEnabled) return true;
            var locked = LockedGroupSet();
            if (locked.Count == 0) return true;
            return !locked.Contains(String.IsNullOrWhiteSpace(channel.Group) ? "Non classé" : channel.Group);
        }

        IEnumerable<Channel> VisibleChannels
        {
            get { return channels.Where(IsChannelAllowed); }
        }

        ContextMenu BuildProfileMenu()
        {
            var menu = CreateContextMenu();
            foreach (var item in profiles)
            {
                var captured = item;
                menu.Items.Add(MenuItem(item.Name + (item.IsChild ? "  ·  enfant" : "") + (item == activeProfile ? "  ✓" : ""),
                    delegate { SwitchProfile(captured); }, item == activeProfile ? Glyphs.Check : Glyphs.Admin));
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Gérer les profils", delegate { Nav("Paramètres"); }, Glyphs.Settings));
            return menu;
        }

        Button ProfileAvatarButton(double size)
        {
            var profile = activeProfile ?? new UserProfile { Name = "Principal", AvatarColor = "#8B5CF6" };
            Color avatar;
            if (!TryParseHexColor(profile.AvatarColor, out avatar)) avatar = Accent;
            string initial = String.IsNullOrWhiteSpace(profile.Name) ? "?" : profile.Name.Substring(0, 1).ToUpperInvariant();
            var label = Text(initial, Math.Round(size * 0.38), Brush(Color.FromRgb(255, 255, 255)), true);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextAlignment = TextAlignment.Center;
            var button = new Button
            {
                Content = label, Width = size, Height = size, Padding = new Thickness(0),
                Background = Brush(avatar), BorderBrush = Brush(WithAlpha(Colors.White, 60)), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, FocusVisualStyle = null, Template = RoundedButtonTemplate(size / 2),
                ToolTip = "Profil · " + profile.Name, HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var menu = BuildProfileMenu();
            button.ContextMenu = menu;
            button.Click += delegate
            {
                menu.PlacementTarget = button;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            };
            return button;
        }

        Border ProfileSidebarItem()
        {
            var profile = activeProfile ?? new UserProfile { Name = "Principal", AvatarColor = "#8B5CF6" };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Color avatar;
            if (!TryParseHexColor(profile.AvatarColor, out avatar)) avatar = Accent;
            var circle = new Border
            {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = Brush(avatar),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left,
                Child = Text(String.IsNullOrWhiteSpace(profile.Name) ? "?" : profile.Name.Substring(0, 1).ToUpperInvariant(), 13, Brush(Color.FromRgb(255, 255, 255)), true)
            };
            var initial = (TextBlock)circle.Child;
            initial.HorizontalAlignment = HorizontalAlignment.Center;
            initial.TextAlignment = TextAlignment.Center;
            Grid.SetColumn(circle, 0); grid.Children.Add(circle);
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(Text(profile.Name, 12, Brush(TextPrimary), true));
            labels.Children.Add(Text(profile.IsChild ? "Profil enfant" : "Profil", 9.5, Brush(Muted), false));
            Grid.SetColumn(labels, 1); grid.Children.Add(labels);
            var chevron = Icon(Glyphs.ChevronUp, 12, Brush(Muted)); Grid.SetColumn(chevron, 2); grid.Children.Add(chevron);
            var button = new Button
            {
                Content = grid, Height = 50, Margin = new Thickness(8, 5, 8, 5), Padding = new Thickness(6, 2, 8, 2),
                Background = Brush(Raised), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Cursor = Cursors.Hand,
                FocusVisualStyle = null, Template = RoundedButtonTemplate()
            };
            var menu = BuildProfileMenu();
            button.ContextMenu = menu;
            button.Click += delegate
            {
                menu.PlacementTarget = button;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                menu.IsOpen = true;
            };
            return new Border { Child = button };
        }

        bool VerifyProfilePin(UserProfile profile, string title)
        {
            if (profile == null || String.IsNullOrWhiteSpace(profile.PinProtected)) return true;
            string pin = PromptPin(title, "Saisissez le code PIN du profil « " + profile.Name + " ».");
            if (pin == null) return false;
            return ProfileStore.VerifyPin(profile, pin);
        }

        void EnsureActiveProfileUnlocked()
        {
            if (activeProfile == null || String.IsNullOrWhiteSpace(activeProfile.PinProtected)) return;
            if (VerifyProfilePin(activeProfile, "Déverrouiller LyaranTV")) return;
            var fallback = profiles.FirstOrDefault(p => p != activeProfile && String.IsNullOrWhiteSpace(p.PinProtected));
            if (fallback == null)
            {
                ShowError("Ce profil est protégé. Le contenu reste verrouillé jusqu’au prochain démarrage.");
                return;
            }
            activeProfile = fallback;
            ProfileScope.Current = fallback.Name;
            settings.ActiveProfile = fallback.Name;
            LoadProfileState();
            SaveSettings();
            RebuildSidebar(); UpdateStatusBar();
            ShowToast("Profil « " + fallback.Name + " » activé.", false, false);
        }

        void SwitchProfile(UserProfile profile)
        {
            if (profile == null) return;
            if (profile == activeProfile) { Nav("Accueil"); return; }
            if (!VerifyProfilePin(profile, "Changer de profil")) { ShowToast("Code PIN incorrect.", true, false); return; }
            SaveState();
            activeProfile = profile;
            ProfileScope.Current = profile.Name;
            settings.ActiveProfile = profile.Name;
            parentalUnlocked = false;
            LoadProfileState();
            SaveSettings();
            RebuildSidebar(); UpdateStatusBar();
            if (channels.Count > 0) ShowLibrary("Bibliothèque"); else ShowHome();
            ShowToast("Profil « " + profile.Name + " » activé.", false, true);
        }

        void StartTmdbEnrichment()
        {
            if (tmdbEnriching || channels.Count == 0 || !TmdbClient.IsConfigured(settings)) return;
            tmdbEnriching = true;
            tmdbApplied = 0;
            var snapshot = channels.ToList();
            var worker = new BackgroundWorker();
            worker.DoWork += delegate
            {
                TmdbClient.Enrich(snapshot, settings,
                    delegate(Channel channel, TmdbResult result) { tmdbApplied++; },
                    delegate(int done, int total, string title)
                    {
                        if (done != 1 && done % 10 != 0 && done != total) return;
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            SetStatus("Métadonnées TMDb · " + done + "/" + total + (String.IsNullOrWhiteSpace(title) ? "" : " · " + title));
                        }));
                    },
                    delegate { return loadCancelled; });
            };
            worker.RunWorkerCompleted += delegate
            {
                tmdbEnriching = false;
                TmdbCacheStore.Save();
                if (tmdbApplied > 0)
                {
                    ShowToast(TextUtil.HumanCount(tmdbApplied) + " fiche(s) enrichie(s) avec TMDb.", false, true);
                    ScheduleTmdbRefresh();
                }
                else SetStatus("TMDb configuré · aucune fiche correspondante trouvée.");
            };
            worker.RunWorkerAsync();
        }

        void ScheduleTmdbRefresh()
        {
            if (tmdbRefreshTimer == null)
            {
                tmdbRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
                tmdbRefreshTimer.Tick += delegate
                {
                    tmdbRefreshTimer.Stop();
                    RebuildSidebar(); UpdateStatusBar();
                    if (page == "Bibliothèque" || page == "Favoris" || page == "Récents" || page == "Recherche") RenderChannels(false);
                    else if (page == "Accueil") ShowHome();
                    else if (page == "Guide EPG") ShowEpg();
                };
            }
            tmdbRefreshTimer.Stop();
            tmdbRefreshTimer.Start();
        }

        void UpdateStatusBar()
        {
            if (statusRight == null) return;
            var visible = VisibleChannels.ToList();
            int live = visible.Count(c => c.Kind == "Chaînes");
            int movies = visible.Count(c => c.Kind == "Films");
            int series = visible.Count(c => c.Kind == "Séries");
            string text = TextUtil.HumanCount(live) + " chaînes · " + TextUtil.HumanCount(movies) + " films · " + TextUtil.HumanCount(series) + " séries · " + TextUtil.HumanCount(favorites.Count) + " favoris";
            if (activeSource != null) text += "  ·  " + activeSource.Name;
            statusRight.Text = text;
        }

        void SetStatus(string message) { if (statusText != null) statusText.Text = message; }

        void SetPageStatus()
        {
            if (statusText == null) return;
            if (page == "Accueil") SetStatus("Vue d’ensemble · " + TextUtil.HumanCount(channels.Count) + " éléments disponibles.");
            else if (page == "Bibliothèque") SetStatus(activeSource == null ? "Aucune playlist active." : activeSource.Name + " · " + TextUtil.HumanCount(channels.Count) + " éléments dans votre bibliothèque.");
            else if (page == "Favoris") SetStatus("Ma liste · " + TextUtil.HumanCount(favorites.Count) + " élément(s).");
            else if (page == "Récents") SetStatus("Récemment regardés.");
            else if (page == "Recherche") SetStatus("Recherche dans la bibliothèque active.");
            else if (page == "Médias locaux") SetStatus("Fichiers locaux et reprise de lecture.");
            else if (page == "Catégories") SetStatus("Gestion des catégories.");
            else if (page == "Chaînes masquées") SetStatus("Contenus masqués.");
            else if (page == "Guide rapide") SetStatus("Guide rapide de LyaranTV.");
            else if (page == "Lecture") SetStatus(currentPlaying == null ? "Lecteur." : "Lecture de « " + currentPlaying.Name + " ».");
        }

        void RebuildEpgIndex()
        {
            epgById.Clear(); epgByName.Clear();
            foreach (var program in loadedEpg)
            {
                if (!String.IsNullOrWhiteSpace(program.ChannelId))
                {
                    List<EpgProgram> list;
                    if (!epgById.TryGetValue(program.ChannelId.Trim(), out list)) { list = new List<EpgProgram>(); epgById[program.ChannelId.Trim()] = list; }
                    list.Add(program);
                }
                if (!String.IsNullOrWhiteSpace(program.ChannelName))
                {
                    string key = TextUtil.NormalizeKey(program.ChannelName);
                    if (key.Length > 0)
                    {
                        List<EpgProgram> list;
                        if (!epgByName.TryGetValue(key, out list)) { list = new List<EpgProgram>(); epgByName[key] = list; }
                        list.Add(program);
                    }
                }
            }
            foreach (var list in epgById.Values) list.Sort((a, b) => a.Start.CompareTo(b.Start));
            foreach (var list in epgByName.Values) list.Sort((a, b) => a.Start.CompareTo(b.Start));
        }

        List<EpgProgram> ProgramsFor(Channel channel)
        {
            if (channel == null) return new List<EpgProgram>();
            List<EpgProgram> programs;
            if (!String.IsNullOrWhiteSpace(channel.TvgId) && epgById.TryGetValue(channel.TvgId.Trim(), out programs)) return programs;
            if (!String.IsNullOrWhiteSpace(channel.Name) && epgByName.TryGetValue(TextUtil.NormalizeKey(channel.Name), out programs)) return programs;
            return new List<EpgProgram>();
        }

        EpgProgram CurrentProgram(Channel channel)
        {
            var now = DateTimeOffset.Now;
            foreach (var program in ProgramsFor(channel)) if (program.IsLive(now)) return program;
            return null;
        }

        EpgProgram NextProgram(Channel channel)
        {
            var now = DateTimeOffset.Now;
            foreach (var program in ProgramsFor(channel)) if (program.Start > now) return program;
            return null;
        }

        Channel FindChannel(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            return channels.FirstOrDefault(c => String.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        void AddRecent(Channel channel)
        {
            if (channel == null) return;
            recentIds.RemoveAll(id => String.Equals(id, channel.Id, StringComparison.OrdinalIgnoreCase));
            recentIds.Insert(0, channel.Id);
            while (recentIds.Count > 40) recentIds.RemoveAt(recentIds.Count - 1);
            StateStore.WriteList("recent.txt", recentIds);
        }

        /// <summary>
        /// Associe les contenus à leur playlist pour que la suppression d'une
        /// source efface aussi son historique. Le suivi est enregistré au
        /// chargement de la bibliothèque et à chaque lecture.
        /// </summary>
        void RememberHistoryOwnership(IEnumerable<Channel> items)
        {
            if (activeSource == null || items == null) return;
            var ids = new List<string>();
            foreach (Channel item in items)
                if (item != null && !String.IsNullOrWhiteSpace(item.Id)) ids.Add(item.Id);
            if (ids.Count > 0) StateStore.RecordHistorySources(ids, activeSource.Name);
        }

        void ToggleFavorite(Channel channel)
        {
            if (channel == null) return;
            if (!favorites.Add(channel.Id)) favorites.Remove(channel.Id);
            StateStore.WriteSet("favorites.txt", favorites);
            UpdateStatusBar();
            if (favButton != null) favButton.Content = IconText(favorites.Contains(channel.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite, favorites.Contains(channel.Id) ? "Favori" : "Ajouter aux favoris", Brush(TextPrimary), 12.5);
            if (page == "Bibliothèque" || page == "Favoris" || page == "Recherche" || page == "Récents") RenderChannels();
            RebuildSidebar();
        }

        void ScheduleSearch()
        {
            if (!appReady || suppressSearch || searchTimer == null) return;
            searchTimer.Stop(); searchTimer.Start();
        }

        void ApplySearch()
        {
            if (searchBox == null) return;
            string query = searchBox.Text.Trim();
            searchQuery = query;
            if (query.Length == 0)
            {
                if (page == "Recherche")
                {
                    page = activeSource == null ? "Accueil" : "Bibliothèque";
                    RebuildSidebar();
                    Navigate();
                }
                else if (page == "Bibliothèque" || page == "Favoris" || page == "Récents") RenderChannels();
                return;
            }
            if (page == "Lecture" || page == "Épisodes")
            {
                SetStatus("Appuyez sur Entrée pour rechercher « " + query + " »");
                return;
            }
            if (page != "Recherche")
            {
                pageBeforeSearch = page;
                page = "Recherche";
                RebuildSidebar();
                ShowSearch();
            }
            else RenderChannels();
        }

        void OpenSearch()
        {
            if (searchBox == null) return;
            string query = searchBox.Text.Trim();
            if (query.Length == 0) { searchBox.Focus(); return; }
            searchQuery = query;
            if (page != "Recherche") pageBeforeSearch = page;
            page = "Recherche";
            RebuildSidebar();
            ShowSearch();
        }

        void ClearSearch()
        {
            searchQuery = "";
            if (searchBox != null)
            {
                suppressSearch = true;
                searchBox.Text = "";
                suppressSearch = false;
            }
        }

        void HandleWindowPreviewKeys(object sender, KeyEventArgs e)
        {
            // Les flèches restent des raccourcis de lecture même quand un bouton ou
            // la timeline a le focus : sans cela, la navigation clavier de WPF
            // consomme les flèches avant le gestionnaire de la fenêtre.
            if (page != "Lecture" || player == null) return;
            if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is PasswordBox) return;
            if (e.Key == Key.Left) { SeekRelative(-SeekStepSeconds); ShowFullscreenControls(); e.Handled = true; }
            else if (e.Key == Key.Right) { SeekRelative(SeekStepSeconds); ShowFullscreenControls(); e.Handled = true; }
        }

        void HandleWindowKeys(object sender, KeyEventArgs e)
        {
            bool typing = Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is PasswordBox;
            if (e.Key == Key.F11) { ToggleFullScreen(); e.Handled = true; return; }
            if (e.Key == Key.Escape)
            {
                if (fullScreen) { ExitFullScreen(); e.Handled = true; }
                else if (page == "Lecture") { PlayerBack(); e.Handled = true; }
                else if (page == "Épisodes") { NavBack(); e.Handled = true; }
                else if (page == "Recherche") { ClearSearch(); NavBack(); e.Handled = true; }
                return;
            }
            ShowFullscreenControls();
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { if (searchBox != null) { searchBox.Focus(); searchBox.SelectAll(); } e.Handled = true; return; }
            if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control) { RefreshActiveSource(); e.Handled = true; return; }
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { AddPlaylist(); e.Handled = true; return; }
            if (typing) return;
            if (page != "Lecture" || player == null)
            {
                // Sur l'accueil, les flèches parcourent aussi les recommandations
                // du défilé, sans cliquer.
                if (page == "Accueil" && homeHeroItems.Count > 1 && Keyboard.Modifiers == ModifierKeys.None
                    && (e.Key == Key.Left || e.Key == Key.Right))
                {
                    StepHomeHero(e.Key == Key.Left ? -1 : 1);
                    e.Handled = true;
                    return;
                }
                ScrollViewer target = resultsScroll ?? mainScroll;
                if (target != null && Keyboard.Modifiers == ModifierKeys.None)
                {
                    if (e.Key == Key.PageDown) { target.PageDown(); e.Handled = true; }
                    else if (e.Key == Key.PageUp) { target.PageUp(); e.Handled = true; }
                    else if (e.Key == Key.Home) { target.ScrollToTop(); e.Handled = true; }
                    else if (e.Key == Key.End) { target.ScrollToEnd(); e.Handled = true; }
                }
                return;
            }
            if (e.Key == Key.Space) { TogglePlayback(); e.Handled = true; }
            else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Shift) { SetAbPoint(true); e.Handled = true; }
            else if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Shift) { SetAbPoint(false); e.Handled = true; }
            else if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Shift) { ClearAbLoop(); e.Handled = true; }
            else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control) { ToggleLoopPlayback(); e.Handled = true; }
            else if (e.Key == Key.J) { SeekRelative(-10); e.Handled = true; }
            else if (e.Key == Key.L) { SeekRelative(10); e.Handled = true; }
            else if (e.Key == Key.OemOpenBrackets) { ApplyPlaybackSpeed(playbackSpeed - 0.25, true); e.Handled = true; }
            else if (e.Key == Key.OemCloseBrackets) { ApplyPlaybackSpeed(playbackSpeed + 0.25, true); e.Handled = true; }
            else if (e.Key == Key.OemComma) { StepFrame(-1); e.Handled = true; }
            else if (e.Key == Key.OemPeriod) { StepFrame(1); e.Handled = true; }
            else if (e.Key == Key.OemQuestion) { ShowShortcutsDialog(); e.Handled = true; }
            else if (e.Key == Key.Up) { ChangeVolume(0.05); e.Handled = true; }
            else if (e.Key == Key.Down) { ChangeVolume(-0.05); e.Handled = true; }
            else if (e.Key == Key.M) { ToggleMute(); e.Handled = true; }
            else if (e.Key == Key.I) { ToggleStatsHud(); e.Handled = true; }
            else if (e.Key == Key.Back) { ReturnToPreviousPoint(); e.Handled = true; }
            else if (e.Key == Key.E) { ToggleEpisodesSection(); e.Handled = true; }
            else if (e.Key == Key.S) { OpenSubtitleMenu(); e.Handled = true; }
            else if (e.Key == Key.A) { OpenAudioTrackMenu(); e.Handled = true; }
            else if (e.Key == Key.PageDown || e.Key == Key.OemPlus || e.Key == Key.Add) { SwitchChannel(1); e.Handled = true; }
            else if (e.Key == Key.PageUp || e.Key == Key.OemMinus || e.Key == Key.Subtract) { SwitchChannel(-1); e.Handled = true; }
            else if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control) { ToggleFavorite(currentPlaying); e.Handled = true; }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct NativeRect { public int Left, Top, Right, Bottom; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct NativeMonitorInfo { public int cbSize; public NativeRect rcMonitor; public NativeRect rcWork; public uint dwFlags; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool GetMonitorInfo(IntPtr monitor, ref NativeMonitorInfo info);

        void ToggleFullScreen()
        {
            if (fullScreen) ExitFullScreen();
            else EnterFullScreen();
        }

        void EnterFullScreen()
        {
            if (fullScreen) return;
            fullScreen = true;
            savedWindowStyle = WindowStyle;
            savedWindowState = WindowState;
            savedResizeMode = ResizeMode;
            savedWindowBounds = new Rect(Left, Top, Width, Height);
            savedTopmost = Topmost;

            // Une vraie plein écran : fenêtre sans bordure, positionnée sur le
            // moniteur courant, au-dessus de la barre des tâches.
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            Rect bounds = CurrentMonitorBounds();
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            ApplyFullScreenLayout();
        }

        void ExitFullScreen()
        {
            if (!fullScreen) return;
            fullScreen = false;
            Mouse.OverrideCursor = null;
            ApplyFullScreenLayout();
            Topmost = savedTopmost;
            WindowStyle = savedWindowStyle;
            ResizeMode = savedResizeMode;
            if (savedWindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
                Left = savedWindowBounds.Left;
                Top = savedWindowBounds.Top;
                Width = savedWindowBounds.Width;
                Height = savedWindowBounds.Height;
            }
        }

        Rect CurrentMonitorBounds()
        {
            try
            {
                IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                IntPtr monitor = MonitorFromWindow(handle, 2);
                if (monitor != IntPtr.Zero)
                {
                    var info = new NativeMonitorInfo();
                    info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMonitorInfo));
                    if (GetMonitorInfo(monitor, ref info))
                        return new Rect(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right - info.rcMonitor.Left, info.rcMonitor.Bottom - info.rcMonitor.Top);
                }
            }
            catch { }
            return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        }

        void ApplyFullScreenLayout()
        {
            ApplyPlayerChrome();
            UpdateFullscreenButtons();
            if (mainScroll != null)
            {
                // En plein écran, aucun défilement : la vidéo remplit la fenêtre.
                mainScroll.VerticalScrollBarVisibility = fullScreen ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
            }
            if (fullScreen)
            {
                if (mainScroll != null) mainScroll.ScrollToTop();
                ShowFullscreenControls();
            }
            else
            {
                StopFullscreenControlsTimer();
                if (playerControlWrap != null) playerControlWrap.Visibility = Visibility.Visible;
                if (playerTopBar != null) playerTopBar.Visibility = Visibility.Visible;
                UpdateSubtitlePosition();
            }
            // En plein écran, le lecteur remplit la surface (pas de bandeau noir).
            ApplyPlayerStretch();
            // La mise en page (en-tête masqué, fenêtre repositionnée) n'est pas
            // encore mesurée : on réapplique la hauteur une fois le tour de
            // disposition terminé, puis au rendu suivant. Une fenêtre qui était
            // maximisée avant le plein écran peut aussi reprendre la taille de
            // l'espace de travail (barre des tâches exclue) : on réimpose les
            // bornes du moniteur juste après le premier passage.
            Dispatcher.BeginInvoke(new Action(delegate
            {
                EnsureFullScreenBounds();
                UpdatePlayerVideoSize();
            }), DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(delegate
            {
                UpdatePlayerVideoSize();
                if (GeometryLogEnabled) LogPlayerGeometry("render");
            }), DispatcherPriority.Render);
            if (GeometryLogEnabled)
            {
                var geomTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                geomTimer.Tick += delegate
                {
                    geomTimer.Stop();
                    LogPlayerGeometry("settled");
                    var late = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                    late.Tick += delegate { late.Stop(); LogPlayerGeometry("late"); };
                    late.Start();
                };
                geomTimer.Start();
            }
        }

        /// <summary>Réimpose la fenêtre sur tout l'écran tant que le plein écran est actif.</summary>
        void EnsureFullScreenBounds()
        {
            if (!fullScreen) return;
            Rect bounds = CurrentMonitorBounds();
            if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
            if (Math.Abs(Left - bounds.Left) > 0.5 || Math.Abs(Top - bounds.Top) > 0.5
                || Math.Abs(Width - bounds.Width) > 0.5 || Math.Abs(Height - bounds.Height) > 0.5)
            {
                Left = bounds.Left;
                Top = bounds.Top;
                Width = bounds.Width;
                Height = bounds.Height;
            }
        }

        /// <summary>
        /// Chrome de l'application pendant la lecture : l'en-tête, la barre latérale
        /// et le pied de page s'effacent pour ne laisser que la vidéo, comme dans un
        /// lecteur plein cadre. La fiche du contenu reste accessible en défilant et
        /// disparaît en plein écran.
        /// </summary>
        void ApplyPlayerChrome()
        {
            bool playing = page == "Lecture";
            bool hide = playing || fullScreen;
            if (chromeTop != null) chromeTop.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            if (chromeSidebar != null) chromeSidebar.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            if (chromeStatus != null) chromeStatus.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            if (!playing) return;
            if (playerInfoCard != null) playerInfoCard.Visibility = fullScreen ? Visibility.Collapsed : Visibility.Visible;
            if (episodesSection != null)
                episodesSection.Visibility = fullScreen || !showEpisodesBelowPlayer ? Visibility.Collapsed : Visibility.Visible;
            UpdatePlayerVideoSize();
            UpdatePlayerControlChrome();
        }

        void UpdateFullscreenButtons()
        {
            string glyph = fullScreen ? Glyphs.BackToWindow : Glyphs.FullScreen;
            string tip = fullScreen ? "Quitter le plein écran · F11" : "Plein écran · F11";
            if (playerHeaderFullscreenButton != null)
            {
                playerHeaderFullscreenButton.Content = Icon(glyph, 15, Brush(TextPrimary));
                playerHeaderFullscreenButton.ToolTip = tip;
            }
            if (playerFullscreenButton != null)
            {
                playerFullscreenButton.Content = Icon(glyph, 16, Brushes.White);
                playerFullscreenButton.ToolTip = tip;
            }
        }

        // Les contrôles restent superposés à la vidéo en fenêtré comme en plein
        // écran : seul change l'espacement par rapport au bord de l'image.
        void UpdatePlayerControlChrome()
        {
            if (playerControlWrap != null)
            {
                // En plein écran la barre est collée au bord : laisser une marge
                // découvrait une bande d'image non assombrie sous le dégradé,
                // qui donnait l'impression d'une image mal dimensionnée. La
                // respiration est désormais assurée par le rembourrage interne.
                playerControlWrap.Margin = fullScreen ? new Thickness(0) : new Thickness(0, 0, 0, 0);
                playerControlWrap.Padding = fullScreen ? new Thickness(30, 64, 30, 18) : new Thickness(22, 58, 22, 14);
            }
            if (playerTopBar != null)
                playerTopBar.Margin = fullScreen ? new Thickness(26, 18, 26, 0) : new Thickness(18, 14, 18, 0);
        }

        void ShowFullscreenControls()
        {
            if (playerControlWrap == null || page != "Lecture") return;
            ShowPlayerCursor();
            bool wasShown = playerChromeShown;
            playerChromeShown = true;
            playerControlWrap.IsHitTestVisible = true;
            if (wasShown)
            {
                playerControlWrap.BeginAnimation(UIElement.OpacityProperty, null);
                playerControlWrap.Opacity = 1;
                if (playerTopBar != null)
                {
                    playerTopBar.BeginAnimation(UIElement.OpacityProperty, null);
                    playerTopBar.Opacity = 1;
                    playerTopBar.Visibility = Visibility.Visible;
                }
            }
            else
            {
                playerControlWrap.Visibility = Visibility.Visible;
                if (playerTopBar != null) playerTopBar.Visibility = Visibility.Visible;
                if (UiMotion.Reduced) { playerControlWrap.Opacity = 1; if (playerTopBar != null) playerTopBar.Opacity = 1; }
                else
                {
                    playerControlWrap.Opacity = 0;
                    playerControlWrap.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                    if (playerTopBar != null)
                    {
                        playerTopBar.Opacity = 0;
                        playerTopBar.BeginAnimation(UIElement.OpacityProperty,
                            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                    }
                }
            }
            UpdateSubtitlePosition();
            StartFullscreenControlsTimer();
        }

        void HidePlayerChrome()
        {
            if (!playerChromeShown) return;
            playerChromeShown = false;
            if (volumeCard != null) { volumeCard.Visibility = Visibility.Collapsed; volumeCard.Opacity = 0; }
            HidePlayerCursor();
            if (playerControlWrap == null) return;
            playerControlWrap.IsHitTestVisible = false;
            Action finish = delegate
            {
                if (playerChromeShown) return;
                if (playerControlWrap != null) playerControlWrap.Visibility = Visibility.Collapsed;
                if (playerTopBar != null) playerTopBar.Visibility = Visibility.Collapsed;
                UpdateSubtitlePosition();
            };
            if (UiMotion.Reduced)
            {
                finish();
                return;
            }
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            fade.Completed += delegate { finish(); };
            playerControlWrap.BeginAnimation(UIElement.OpacityProperty, fade);
            if (playerTopBar != null)
                playerTopBar.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200)));
            UpdateSubtitlePosition();
        }

        void StartFullscreenControlsTimer()
        {
            if (playerControlWrap == null || page != "Lecture") return;
            if (fullscreenControlsTimer == null)
            {
                fullscreenControlsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
                fullscreenControlsTimer.Tick += delegate
                {
                    if (page != "Lecture" || playerControlWrap == null) { StopFullscreenControlsTimer(); return; }
                    // En pause, au survol ou au clavier, la barre reste affichée.
                    if (playerControlWrap.IsMouseOver || playerControlWrap.IsKeyboardFocusWithin || playbackPaused)
                    {
                        fullscreenControlsTimer.Stop();
                        fullscreenControlsTimer.Start();
                        return;
                    }
                    HidePlayerChrome();
                    fullscreenControlsTimer.Stop();
                };
            }
            fullscreenControlsTimer.Stop();
            fullscreenControlsTimer.Start();
        }

        void StopFullscreenControlsTimer()
        {
            if (fullscreenControlsTimer == null) return;
            fullscreenControlsTimer.Stop();
            fullscreenControlsTimer = null;
        }

        void ChangeTheme(string theme)
        {
            string remembered = page;
            Channel playing = currentPlaying;
            List<Channel> queue = playQueue;
            double position = 0; bool paused = playbackPaused;
            if (player != null) { try { position = player.Position.TotalSeconds; } catch { } }
            themeName = theme;
            settings.Theme = theme;
            SetThemeColors(themeName);
            SaveSettings();
            ConfigureSystemResources();
            Background = Brush(Back); Foreground = Brush(TextPrimary);
            ApplyDarkTitleBar();
            BuildShell();
            appReady = true;
            page = remembered;
            UpdateBackButton();
            RestoreSearchAfterTheme();
            RebuildSidebar();
            if (remembered == "Lecture" && playing != null) Play(playing, queue, position, paused);
            else if (remembered == "Épisodes") { page = "Bibliothèque"; ShowLibrary("Bibliothèque"); }
            else Navigate();
        }

        void ChangeAccent(Color color)
        {
            accentHex = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            settings.Accent = accentHex;
            ChangeTheme(themeName);
            Notice("Couleur d’accent appliquée.");
        }

        void RestoreSearchAfterTheme()
        {
            if (searchBox == null || String.IsNullOrWhiteSpace(searchQuery)) return;
            suppressSearch = true;
            searchBox.Text = searchQuery;
            suppressSearch = false;
        }

        void SystemThemePreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (themeName != "Automatique" || (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color)) return;
            Dispatcher.BeginInvoke(new Action(delegate { ChangeTheme("Automatique"); }));
        }

        /// <summary>
        /// Clic sur « Charger » : la bibliothèque en cache est d'abord restaurée
        /// sous une fenêtre de progression dédiée, puis le chargement réseau
        /// prend le relais avec sa propre barre si la copie doit être actualisée.
        /// Sans copie locale, le chargement réseau démarre directement.
        /// </summary>
        void Activate(PlaylistSource source)
        {
            if (source == null) return;
            if (source.IsDemo || source.Type == "Démo") { Activate(source, false); return; }
            if (LibraryCache.Exists(source)) RestoreLibrary(source);
            else Activate(source, false);
        }

        /// <summary>
        /// Charge la playlist. En mode silencieux — actualisation en arrière-plan
        /// d'une bibliothèque déjà restaurée — aucune fenêtre modale n'apparaît et
        /// la page courante n'est pas remplacée.
        /// </summary>
        void Activate(PlaylistSource source, bool silent)
        {
            if (source == null) return;
            if (loadingWindow != null && loadingWindow.IsVisible) { if (!silent) Notice("Un chargement est déjà en cours."); return; }
            if (source.IsDemo || source.Type == "Démo")
            {
                activeSource = source; settings.LastSourceName = source.Name; SaveSettings();
                ApplyLoadResult(source, PlaylistLoader.DemoLibrary());
                return;
            }
            activeSource = source; settings.LastSourceName = source.Name; SaveSettings();
            RebuildSidebar(); UpdateStatusBar();
            SetStatus(silent ? "Actualisation de « " + source.Name + " » en arrière-plan…" : "Préparation de « " + source.Name + " »…");
            loadCancelled = false;
            int generation = ++loadGeneration;
            PlaylistLoadingWindow dialog = null;
            bool finished = false;
            if (!silent)
            {
                dialog = new PlaylistLoadingWindow(source.Name, Back, Panel, Accent, Muted, TextPrimary) { Owner = this };
                loadingWindow = dialog;
                dialog.Closed += delegate { if (!finished) loadCancelled = true; if (loadingWindow == dialog) loadingWindow = null; };
                dialog.CancelRequested += delegate { loadCancelled = true; finished = true; dialog.Close(); SetStatus("Chargement annulé."); ShowToast("Chargement annulé.", false, false); };
                dialog.Show();
            }
            var worker = new BackgroundWorker { WorkerReportsProgress = true };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                var info = e.UserState as object[];
                if (info != null && dialog != null && dialog.IsVisible) dialog.Report((int)info[0], e.ProgressPercentage, (string)info[1], (bool)info[2]);
            };
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                var bg = (BackgroundWorker)sender;
                e.Result = PlaylistLoader.Load(source, delegate(int percent, int stage, string message, bool skipped)
                {
                    bg.ReportProgress(percent, new object[] { stage, message, skipped });
                }, delegate { return loadCancelled; });
                if (loadCancelled) e.Result = null;
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                // Un chargement plus récent a pris le relais : ce résultat est périmé.
                if (generation != loadGeneration) return;
                // Lire e.Result alors que le worker a échoué lève une TargetInvocationException
                // (« Une exception s'est produite lors de l'opération »). Il faut donc tester
                // e.Error avant toute lecture de e.Result.
                if (e.Error != null)
                {
                    // Message de l'exception de plus haut niveau : les erreurs réseau sont
                    // déjà traduites en français par HttpUtil.DescribeException. GetBaseException()
                    // renverrait le message brut de SocketException, illisible pour l'utilisateur.
                    string message = e.Error.Message;
                    if (String.IsNullOrWhiteSpace(message)) message = e.Error.GetBaseException().Message;
                    if (loadCancelled || e.Error.GetBaseException() is OperationCanceledException)
                    {
                        finished = true; if (dialog != null && dialog.IsVisible) dialog.Close(); loadingWindow = null;
                        SetStatus("Chargement annulé."); return;
                    }
                    HttpUtil.AppendLog("Chargement de la playlist « " + source.Name + " » : " + e.Error);
                    if (silent)
                    {
                        // Actualisation discrète : la bibliothèque déjà affichée
                        // reste intacte, on se contente de le signaler.
                        loadingWindow = null;
                        SetStatus("Bibliothèque conservée · actualisation impossible.");
                        ShowToast("Actualisation impossible (« " + source.Name + " ») : " + message + " La bibliothèque en cache reste disponible.", true, false);
                        return;
                    }
                    SetStatus("Échec du chargement de la playlist");
                    if (dialog != null && dialog.IsVisible) dialog.Fail("Le chargement a échoué : " + message);
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                    timer.Tick += delegate
                    {
                        timer.Stop(); finished = true;
                        if (dialog != null && dialog.IsVisible) dialog.Close();
                        loadingWindow = null;
                        ShowError("Impossible de charger la playlist.\n\n" + message + "\n\nVérifiez l’adresse et la connexion, puis réessayez.");
                    };
                    timer.Start();
                    return;
                }
                if (loadCancelled || e.Cancelled || e.Result == null)
                {
                    finished = true;
                    if (dialog != null && dialog.IsVisible) dialog.Close();
                    loadingWindow = null;
                    SetStatus("Chargement annulé.");
                    return;
                }
                var result = (PlaylistLoadResult)e.Result;
                if (dialog != null && dialog.IsVisible) dialog.CoversReport("mise en cache des affiches…", false);
                ApplyLoadResult(source, result, silent);
                if (silent)
                {
                    loadingWindow = null;
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        int liveCount = channels.Count(c => c.Kind == "Chaînes");
                        int movieCount = channels.Count(c => c.Kind == "Films");
                        int seriesCount = channels.Count(c => c.Kind == "Séries");
                        ShowToast("Bibliothèque actualisée · " + TextUtil.HumanCount(channels.Count) + " éléments (" +
                            TextUtil.HumanCount(liveCount) + " chaînes, " + TextUtil.HumanCount(movieCount) + " films, " +
                            TextUtil.HumanCount(seriesCount) + " séries).", false, true);
                    }));
                    return;
                }
                if (dialog != null && dialog.IsVisible) dialog.Complete("Bibliothèque prête · " + TextUtil.HumanCount(result.Items.Count) + " éléments indexés.");
                if (dialog != null && dialog.IsVisible) dialog.CoversReport("affiches prêtes", true);
                var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };
                closeTimer.Tick += delegate
                {
                    closeTimer.Stop(); finished = true;
                    if (dialog != null && dialog.IsVisible) dialog.Close();
                    loadingWindow = null;
                };
                closeTimer.Start();
            };
            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Lot de données relu depuis la copie locale, transféré du worker vers
        /// le fil d'interface.
        /// </summary>
        sealed class RestoredLibrary
        {
            public List<Channel> Channels;
            public List<EpgProgram> Guide;
            public DateTime SavedUtc;
        }

        /// <summary>
        /// Restaure la bibliothèque depuis la copie locale sous une fenêtre de
        /// progression : ouverture immédiate, sans « préparation de la playlist »
        /// complète. La relecture du fichier se fait en tâche de fond — une très
        /// grande bibliothèque peut peser des dizaines de mégaoctets — et si la
        /// copie est absente, périmée ou incompatible, le chargement réseau prend
        /// le relais avec sa propre barre.
        /// </summary>
        void RestoreLibrary(PlaylistSource source)
        {
            if (source == null) return;
            if (source.IsDemo || source.Type == "Démo") { Activate(source, false); return; }
            if (!LibraryCache.Exists(source)) { Activate(source, false); return; }

            if (loadingWindow != null && loadingWindow.IsVisible) { Notice("Un chargement est déjà en cours."); return; }
            SetStatus("Restauration de « " + source.Name + " »…");
            loadCancelled = false;
            int generation = ++loadGeneration;
            var dialog = new PlaylistLoadingWindow(source.Name, Back, Panel, Accent, Muted, TextPrimary, new[]
            {
                "Lecture de la copie locale", "Indexation de la bibliothèque", "Guide des programmes · EPG",
                "Ouverture de la bibliothèque", "Chargement des affiches de films et séries"
            }) { Owner = this };
            loadingWindow = dialog;
            bool finished = false;
            dialog.Closed += delegate { if (!finished) loadCancelled = true; if (loadingWindow == dialog) loadingWindow = null; };
            dialog.CancelRequested += delegate { loadCancelled = true; finished = true; dialog.Close(); SetStatus("Restauration annulée."); };
            dialog.Show();
            dialog.Report(0, 6, "Lecture de la copie locale…", false);

            var worker = new BackgroundWorker { WorkerReportsProgress = true };
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                var bg = (BackgroundWorker)sender;
                List<Channel> cached;
                List<EpgProgram> cachedGuide;
                DateTime savedUtc;
                if (!LibraryCache.TryLoad(source, out cached, out cachedGuide, out savedUtc) || cached == null || cached.Count == 0)
                {
                    // Copie illisible, vide ou d'une version antérieure : le
                    // chargement réseau normal s'en occupe.
                    e.Result = null;
                    return;
                }
                if (loadCancelled) { e.Cancel = true; return; }
                bg.ReportProgress(58, new object[] { 58, "Copie locale lue · " + TextUtil.HumanCount(cached.Count) + " éléments et " + TextUtil.HumanCount(cachedGuide == null ? 0 : cachedGuide.Count) + " programmes de guide.", false });
                e.Result = new RestoredLibrary { Channels = cached, Guide = cachedGuide, SavedUtc = savedUtc };
            };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                var info = e.UserState as object[];
                if (info != null && dialog.IsVisible) dialog.Report(0, (int)info[0], (string)info[1], (bool)info[2]);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                // Un chargement plus récent a pris le relais : ce résultat est périmé.
                if (generation != loadGeneration) return;
                if (e.Error != null)
                {
                    HttpUtil.AppendLog("Restauration de la bibliothèque « " + source.Name + " » : " + e.Error);
                    finished = true;
                    if (dialog.IsVisible) { dialog.Close(); }
                    loadingWindow = null;
                    Activate(source, false);
                    return;
                }
                if (e.Cancelled || loadCancelled)
                {
                    // Annulation demandée : on ne relance rien.
                    finished = true;
                    return;
                }
                var restored = e.Result as RestoredLibrary;
                if (restored == null)
                {
                    // Pas de copie exploitable : chargement réseau avec sa barre.
                    finished = true;
                    if (dialog.IsVisible) dialog.Close();
                    loadingWindow = null;
                    Activate(source, false);
                    return;
                }

                finished = true;
                dialog.Report(1, 66, "Indexation des catégories et des groupes…", false);
                channels.Clear();
                channels.AddRange(restored.Channels);
                foreach (var channel in channels) channel.SourceName = source.Name;
                loadedEpg.Clear();
                if (restored.Guide != null && restored.Guide.Count > 0) loadedEpg.AddRange(restored.Guide);
                RebuildEpgIndex();
                TmdbClient.ApplyCache(channels);
                RememberHistoryOwnership(channels);
                if (restored.Guide != null && restored.Guide.Count > 0)
                    dialog.Report(2, 80, TextUtil.HumanCount(restored.Guide.Count) + " programmes de guide indexés.", false);
                else dialog.Report(2, 80, "Aucun guide EPG enregistré — étape ignorée.", true);
                activeSource = source;
                settings.LastSourceName = source.Name;
                source.LastCount = channels.Count;
                RebuildSidebar();
                UpdateStatusBar();
                dialog.Report(3, 90, "Ouverture de la bibliothèque…", false);
                ShowStartPage();
                int live = channels.Count(c => c.Kind == "Chaînes");
                int movies = channels.Count(c => c.Kind == "Films");
                int series = channels.Count(c => c.Kind == "Séries");
                SetStatus("Bibliothèque restaurée · " + TextUtil.HumanCount(live) + " chaînes, " + TextUtil.HumanCount(movies) + " films, " + TextUtil.HumanCount(series) + " séries.");
                string stamp = restored.SavedUtc == DateTime.MinValue ? "" : restored.SavedUtc.ToLocalTime().ToString("dd/MM à HH:mm");
                // Une copie du futur (horloge modifiée) est traitée comme périmée.
                bool stale = restored.SavedUtc == DateTime.MinValue || restored.SavedUtc > DateTime.UtcNow.AddMinutes(5)
                    || DateTime.UtcNow - restored.SavedUtc > LibraryCache.MaxAge;
                dialog.Complete("Bibliothèque prête · " + TextUtil.HumanCount(channels.Count) + " éléments.");
                dialog.CoversReport("affiches prêtes", true);
                var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };
                closeTimer.Tick += delegate
                {
                    closeTimer.Stop();
                    if (dialog.IsVisible) dialog.Close();
                    if (loadingWindow == dialog) loadingWindow = null;
                    if (stale)
                    {
                        // Copie ancienne : le chargement réseau repart avec sa
                        // fenêtre de progression pour suivre l'actualisation.
                        ShowToast("Bibliothèque du " + (String.IsNullOrEmpty(stamp) ? "dernière session" : stamp) +
                            " · actualisation nécessaire…", false, true);
                        Activate(source, false);
                    }
                    else ShowToast("Bibliothèque restaurée instantanément · " + TextUtil.HumanCount(channels.Count) + " éléments.", false, true);
                };
                closeTimer.Start();
                StartTmdbEnrichment();
                PrefetchArtwork(channels, 30);
                if (settings.AutoPlayLast)
                {
                    var last = FindChannel(settings.LastChannelId);
                    if (last != null && IsChannelAllowed(last)) Play(last);
                }
            };
            worker.RunWorkerAsync();
        }

        /// <summary>Écrit la copie locale du catalogue (hors fil d'interface).</summary>
        void SaveLibraryCache(PlaylistSource source)
        {
            if (source == null || source.IsDemo || source.Type == "Démo" || channels.Count == 0) return;
            var snapshot = new List<Channel>(channels);
            var guide = new List<EpgProgram>(loadedEpg);
            DateTime savedUtc = DateTime.UtcNow;
            var worker = new BackgroundWorker();
            worker.DoWork += delegate { LibraryCache.Save(source, snapshot, guide, savedUtc); };
            worker.RunWorkerAsync();
        }

        void ApplyLoadResult(PlaylistSource source, PlaylistLoadResult result)
        {
            ApplyLoadResult(source, result, false);
        }

        void ApplyLoadResult(PlaylistSource source, PlaylistLoadResult result, bool silent)
        {
            channels.Clear(); channels.AddRange(result.Items);
            foreach (var channel in channels) channel.SourceName = source == null ? "" : source.Name;
            loadedEpg.Clear(); loadedEpg.AddRange(result.Guide);
            TmdbClient.ApplyCache(channels);
            RebuildEpgIndex();
            // Une playlist M3U reconnue comme Xtream est convertie durablement :
            // les prochains chargements utilisent directement l'API Xtream.
            if (result.SourceOverride != null)
            {
                source.Type = result.SourceOverride.Type;
                source.Url = result.SourceOverride.Url;
                source.Username = result.SourceOverride.Username;
                source.Password = result.SourceOverride.Password;
                source.FilePath = null;
            }
            source.LastCount = channels.Count;
            source.LastLoaded = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            if (!String.IsNullOrWhiteSpace(result.EpgUrl) && String.IsNullOrWhiteSpace(source.EpgUrl)) source.EpgUrl = result.EpgUrl;
            SaveSources();
            activeSource = source; settings.LastSourceName = source.Name;
            RememberHistoryOwnership(channels);
            SaveLibraryCache(source);
            // Une actualisation en arrière-plan ne touche ni aux filtres en cours
            // ni à la page ouverte : l'utilisateur ne doit rien perdre de sa place.
            if (!silent)
            {
                activeGroup = AllGroups; activeKind = "Tous"; searchQuery = "";
                if (searchBox != null) { suppressSearch = true; searchBox.Text = ""; suppressSearch = false; }
            }
            // Une lecture lancée pendant le chargement (CLI --play, reprise automatique)
            // ne doit pas être interrompue par l'ouverture de la bibliothèque.
            bool keepPlayer = page == "Lecture" && currentPlaying != null && playerLayout != null;
            if (silent)
            {
                RebuildSidebar(); UpdateStatusBar();
                if (page == "Accueil") ShowHome();
                else if (page == "Bibliothèque" || page == "Favoris" || page == "Récents") ShowLibrary(page);
                else if (page == "Guide EPG") ShowEpg();
            }
            else if (!keepPlayer)
            {
                page = "Bibliothèque";
                RebuildSidebar(); ShowLibrary("Bibliothèque"); UpdateStatusBar();
            }
            else { RebuildSidebar(); UpdateStatusBar(); }
            int live = channels.Count(c => c.Kind == "Chaînes");
            int movies = channels.Count(c => c.Kind == "Films");
            int series = channels.Count(c => c.Kind == "Séries");
            SetStatus("Playlist chargée · " + TextUtil.HumanCount(live) + " chaînes, " + TextUtil.HumanCount(movies) + " films, " + TextUtil.HumanCount(series) + " séries.");
            if (!silent)
            {
                string conversion = String.IsNullOrWhiteSpace(result.ConversionNote) ? "" : " · convertie en Xtream Codes";
                ShowToast("« " + source.Name + " » est prête : " + TextUtil.HumanCount(live) + " chaînes, " + TextUtil.HumanCount(movies) + " films, " + TextUtil.HumanCount(series) + " séries" + conversion + ".", false, true);
            }
            if (result.Warnings.Count > 0) SetStatus(SetStatusWithWarnings(result.Warnings.Count));
            if (settings.AutoPlayLast && !silent)
            {
                var last = FindChannel(settings.LastChannelId);
                if (last != null && IsChannelAllowed(last)) Play(last);
            }
            StartTmdbEnrichment();
            PrefetchArtwork(channels, 30);
            if (loadingWindow != null) loadingWindow.CoversReport("affiches prêtes", true);
            StartBackgroundGuide(source, result);
            if (!silent && !String.IsNullOrWhiteSpace(pendingPageAfterLoad))
            {
                string destination = pendingPageAfterLoad;
                pendingPageAfterLoad = "";
                Nav(destination);
            }
        }

        static string SetStatusWithWarnings(int count)
        {
            return count == 1 ? "Playlist chargée avec 1 avertissement." : "Playlist chargée avec " + count + " avertissements.";
        }

        /// <summary>
        /// Le guide XMLTV est volumineux et indépendant des catalogues : il est chargé
        /// après l'ouverture de la bibliothèque pour ne pas bloquer l'utilisateur.
        /// </summary>
        void StartBackgroundGuide(PlaylistSource source, PlaylistLoadResult result)
        {
            if (source == null || result == null) return;
            if (result.Guide != null && result.Guide.Count > 0) return;
            string location = result.EpgUrl;
            if (String.IsNullOrWhiteSpace(location)) return;
            SetStatus("Guide EPG · chargement en arrière-plan…");
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = PlaylistLoader.DownloadEpg(location);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (activeSource != source) return;
                if (e.Error != null)
                {
                    HttpUtil.AppendLog("EPG « " + source.Name + " » : " + e.Error);
                    SetStatus("Guide EPG indisponible · la bibliothèque reste utilisable.");
                    return;
                }
                var guide = e.Result as List<EpgProgram>;
                if (guide == null) return;
                loadedEpg.Clear();
                loadedEpg.AddRange(guide);
                RebuildEpgIndex();
                // Le guide est désormais dans la copie locale : au prochain
                // démarrage il s'affiche sans nouveau téléchargement.
                SaveLibraryCache(source);
                RebuildSidebar();
                SetPageStatus();
                ShowToast("Guide EPG prêt · " + TextUtil.HumanCount(guide.Count) + " programmes.", false, true);
                if (page == "Guide EPG") ShowEpg();
                else UpdateStatusBar();
            };
            worker.RunWorkerAsync();
        }

        void AddPlaylist()
        {
            var model = ShowSourceDialog(null);
            if (model == null) return;
            sources.Add(model);
            SaveSources(); RebuildSidebar();
            Activate(model);
        }

        void ImportM3uFile()
        {
            var dialog = new OpenFileDialog { Filter = "Playlists M3U (*.m3u;*.m3u8)|*.m3u;*.m3u8|Tous les fichiers (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            var source = new PlaylistSource { Name = Path.GetFileNameWithoutExtension(dialog.FileName), Type = "M3U/M3U8", FilePath = dialog.FileName };
            sources.Add(source); SaveSources(); RebuildSidebar(); Activate(source);
        }

        void EditSource(PlaylistSource source)
        {
            if (source == null) return;
            var updated = ShowSourceDialog(source);
            if (updated == null) return;
            string previousName = source.Name;
            source.Name = updated.Name; source.Type = updated.Type; source.Url = updated.Url; source.Username = updated.Username;
            source.Password = updated.Password; source.MacAddress = updated.MacAddress; source.DeviceType = updated.DeviceType;
            source.FilePath = updated.FilePath; source.EpgUrl = updated.EpgUrl; source.IsDemo = updated.IsDemo;
            if (!String.Equals(previousName, source.Name, StringComparison.OrdinalIgnoreCase))
                StateStore.RenameHistorySource(previousName, source.Name);
            SaveSources(); RebuildSidebar();
            if (source == activeSource) Activate(source);
            else { ShowToast("Playlist modifiée.", false, true); UpdateStatusBar(); }
        }

        void RemoveSource(PlaylistSource source)
        {
            if (source == null) return;
            if (!AskConfirmation("Supprimer la playlist",
                "Supprimer « " + source.Name + " » de LyaranTV ?\n\nSon historique de lecture, ses positions de reprise et ses épisodes récents seront également supprimés. Les favoris et les réglages restent enregistrés.",
                "Supprimer")) return;

            var knownIds = new List<string>();
            if (source == activeSource)
                foreach (Channel channel in channels)
                    if (channel != null && !String.IsNullOrWhiteSpace(channel.Id)) knownIds.Add(channel.Id);
            if (currentPlaying != null && !String.IsNullOrWhiteSpace(currentPlaying.Id)) knownIds.Add(currentPlaying.Id);
            if (!String.IsNullOrWhiteSpace(settings.LastChannelId)
                && String.Equals(settings.LastSourceName, source.Name, StringComparison.OrdinalIgnoreCase))
                knownIds.Add(settings.LastChannelId);

            var profileNames = profiles.Select(p => p == null ? "" : p.Name).Where(n => !String.IsNullOrWhiteSpace(n)).ToList();
            HashSet<string> purged = StateStore.PurgeHistoryForSource(source.Name, knownIds, profileNames);

            bool playingRemoved = currentPlaying != null && purged.Contains(currentPlaying.Id);
            if (playingRemoved) currentPlaying = null;
            if (!String.IsNullOrWhiteSpace(settings.LastChannelId) && purged.Contains(settings.LastChannelId))
                settings.LastChannelId = "";

            sources.Remove(source);
            LibraryCache.Delete(source);
            if (activeSource == source)
            {
                activeSource = null; channels.Clear(); loadedEpg.Clear(); RebuildEpgIndex();
                settings.LastSourceName = ""; SaveSettings();
            }
            LoadProfileState();
            SaveSources(); RebuildSidebar(); UpdateStatusBar(); ShowHome();
            ShowToast(purged.Count > 0
                ? "Playlist supprimée · historique et reprises effacés."
                : "Playlist supprimée.", false, true);
        }

        void RefreshActiveSource()
        {
            if (activeSource == null) { Notice("Ajoutez ou sélectionnez une playlist d’abord."); return; }
            if (loadingWindow != null && loadingWindow.IsVisible) { Notice("Un chargement est déjà en cours."); return; }
            Activate(activeSource);
        }

        void TestSourceAsync(PlaylistSource source)
        {
            if (source == null) return;
            SetStatus("Test de la connexion à « " + source.Name + " »…");
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = PlaylistLoader.TestSource(source); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null) { SetStatus("Test échoué."); ShowError("Test de connexion échoué.\n\n" + e.Error.Message); return; }
                SetStatus("Connexion testée.");
                ShowToast((string)e.Result, false, true);
            };
            worker.RunWorkerAsync();
        }

        void CopyText(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) { ShowToast("Rien à copier.", true, false); return; }
            try { Clipboard.SetText(text); ShowToast("Copié dans le presse-papiers.", false, true); }
            catch { ShowError("Impossible d’écrire dans le presse-papiers."); }
        }

        void ExportM3u()
        {
            if (channels.Count == 0) { ShowToast("Aucune chaîne à exporter.", true, false); return; }
            var dialog = new SaveFileDialog { Filter = "Playlist M3U (*.m3u)|*.m3u", FileName = TextUtil.SafeFileName(activeSource == null ? "lyarantv" : activeSource.Name) + ".m3u" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var builder = new StringBuilder();
                builder.AppendLine("#EXTM3U");
                if (activeSource != null && !String.IsNullOrWhiteSpace(activeSource.EpgUrl)) builder.AppendLine("# url-tvg=\"" + activeSource.EpgUrl + "\"");
                foreach (var channel in channels.Where(c => !String.IsNullOrWhiteSpace(c.Url)))
                {
                    builder.Append("#EXTINF:-1 tvg-id=\"" + TextUtil.M3uEscape(channel.TvgId) + "\" tvg-name=\"" + TextUtil.M3uEscape(channel.Name) + "\" tvg-logo=\"" + TextUtil.M3uEscape(channel.Logo) + "\" group-title=\"" + TextUtil.M3uEscape(channel.Group) + "\",");
                    builder.AppendLine(channel.Name);
                    builder.AppendLine(channel.Url);
                }
                File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
                ShowToast("Playlist exportée.", false, true);
            }
            catch (Exception ex) { ShowError("Export impossible : " + ex.Message); }
        }

        void Backup()
        {
            var dialog = new SaveFileDialog { Filter = "Sauvegarde LyaranTV (*.lyaran)|*.lyaran", FileName = "lyarantv-backup-" + DateTime.Now.ToString("yyyy-MM-dd") + ".lyaran" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                SaveState();
                StateStore.CreateBackup(dialog.FileName);
                ShowToast("Sauvegarde créée.", false, true);
            }
            catch (Exception ex) { ShowError("Sauvegarde impossible : " + ex.Message); }
        }

        void Restore()
        {
            var dialog = new OpenFileDialog { Filter = "Sauvegarde LyaranTV (*.lyaran;*.lgabak;*.zip)|*.lyaran;*.lgabak;*.zip|Tous les fichiers (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            if (!AskConfirmation("Restaurer une sauvegarde", "Les playlists et réglages actuels seront remplacés par le contenu de la sauvegarde. Continuer ?", "Restaurer")) return;
            try
            {
                StateStore.RestoreBackup(dialog.FileName);
                // Les copies locales ne correspondent plus aux playlists restaurées.
                LibraryCache.ClearAll();
                ReloadEverything();
                ShowToast("Sauvegarde restaurée.", false, true);
            }
            catch (Exception ex) { ShowError("Restauration impossible : " + ex.Message); }
        }

        void ReloadEverything()
        {
            LoadState();
            channels.Clear();
            loadedEpg.Clear();
            currentPlaying = null;
            RebuildEpgIndex();
            SetThemeColors(themeName);
            ConfigureSystemResources();
            Background = Brush(Back); Foreground = Brush(TextPrimary);
            navHistory.Clear();
            BuildShell();
            UpdateBackButton();
            appReady = true;
            searchQuery = "";
            if (settings.Maximized) WindowState = WindowState.Maximized;
            ApplyDarkTitleBar();
            ShowStartPage();
        }

        void ResetData()
        {
            if (!AskConfirmation("Réinitialiser LyaranTV", "Toutes les playlists, favoris, masquages, positions de lecture et réglages seront supprimés. Cette action est irréversible.", "Tout effacer")) return;
            StateStore.ResetAll();
            LibraryCache.ClearAll();
            settings = new AppSettings();
            themeName = settings.Theme; accentHex = settings.Accent;
            sources.Clear(); channels.Clear(); loadedEpg.Clear(); favorites.Clear(); hiddenChannels.Clear(); hiddenGroups.Clear();
            recentIds.Clear(); recentLocal.Clear(); positions.Clear(); durations.Clear(); episodeHistory.Clear(); activeSource = null; currentPlaying = null;
            navHistory.Clear();
            RebuildEpgIndex();
            SetThemeColors(themeName); ConfigureSystemResources();
            Background = Brush(Back); Foreground = Brush(TextPrimary);
            BuildShell(); appReady = true; UpdateBackButton(); ShowHome();
            ShowToast("Application réinitialisée.", false, true);
        }

        void OpenDataFolder()
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + AppPaths.DataDir + "\"") { UseShellExecute = true }); }
            catch (Exception ex) { ShowError("Impossible d’ouvrir le dossier : " + ex.Message); }
        }

        string FindExternalPlayer()
        {
            if (!String.IsNullOrWhiteSpace(settings.ExternalPlayer) && File.Exists(settings.ExternalPlayer)) return settings.ExternalPlayer;
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string[] candidates =
            {
                Path.Combine(programFiles, "VideoLAN", "VLC", "vlc.exe"),
                Path.Combine(programFilesX86, "VideoLAN", "VLC", "vlc.exe"),
                Path.Combine(programFiles, "DAUM", "PotPlayer", "PotPlayerMini64.exe"),
                Path.Combine(programFilesX86, "DAUM", "PotPlayer", "PotPlayerMini.exe")
            };
            foreach (string candidate in candidates) if (!String.IsNullOrWhiteSpace(candidate) && File.Exists(candidate)) return candidate;
            foreach (string root in new[] { "SOFTWARE\\VideoLAN\\VLC", "SOFTWARE\\WOW6432Node\\VideoLAN\\VLC" })
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(root))
                    {
                        if (key != null)
                        {
                            string install = key.GetValue("InstallDir") as string;
                            if (!String.IsNullOrWhiteSpace(install))
                            {
                                string path = Path.Combine(install, "vlc.exe");
                                if (File.Exists(path)) return path;
                            }
                        }
                    }
                }
                catch { }
            }
            return "";
        }

        void OpenInExternalPlayer(string url)
        {
            if (String.IsNullOrWhiteSpace(url)) { ShowToast("Aucun flux à ouvrir.", true, false); return; }
            string vlc = FindExternalPlayer();
            try
            {
                if (!String.IsNullOrWhiteSpace(vlc))
                {
                    Process.Start(new ProcessStartInfo(vlc, "\"" + TextUtil.LocalPathFromUrl(url) + "\"") { UseShellExecute = false });
                    ShowToast("Ouverture dans le lecteur externe…", false, true);
                }
                else
                {
                    Process.Start(new ProcessStartInfo(TextUtil.LocalPathFromUrl(url)) { UseShellExecute = true });
                    ShowToast("Ouverture avec le lecteur Windows par défaut.", false, false);
                }
            }
            catch (Exception ex) { ShowError("Impossible d’ouvrir le lecteur externe : " + ex.Message); }
        }

        void SaveCurrentPosition()
        {
            if (player == null || currentPlaying == null) return;
            if (currentPlaying.Kind == "Chaînes" || playerLive) return;
            try
            {
                if (player.NaturalDuration.HasTimeSpan)
                {
                    double seconds = player.Position.TotalSeconds;
                    double duration = player.NaturalDuration.TimeSpan.TotalSeconds;
                    if (duration > 0) durations[currentPlaying.Id] = duration;
                    if (seconds > 5 && duration > 0 && seconds < duration - 10) positions[currentPlaying.Id] = seconds;
                    else positions.Remove(currentPlaying.Id);
                }
            }
            catch { }
        }

        double GetSavedPosition(Channel channel)
        {
            if (channel == null || !settings.ResumePlayback) return 0;
            double value;
            return positions.TryGetValue(channel.Id, out value) ? value : 0;
        }

        internal static string VerifyUiBasics()
        {
            if (Application.Current == null)
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources["LyaranScrollThumb"] = Brush(Color.FromArgb(102, 139, 92, 246));
                app.Resources["LyaranScrollThumbHover"] = Brush(Color.FromArgb(179, 139, 92, 246));
                app.Resources["LyaranScrollThumbActive"] = Brush(Color.FromRgb(139, 92, 246));
            }
            var report = new StringBuilder();
            var window = new MainWindow();
            try
            {
                string[] themes = { "Sombre", "Clair", "Noir AMOLED" };
                foreach (string theme in themes)
                {
                    window.SetThemeColors(theme);
                    double primary = ContrastRatio(window.TextPrimary, window.Panel);
                    double muted = ContrastRatio(window.Muted, window.Panel);
                    if (primary < 4.5) throw new InvalidOperationException("Contraste insuffisant pour le texte principal du thème " + theme + " (" + primary.ToString("0.00") + ").");
                    if (muted < 4.5) throw new InvalidOperationException("Contraste insuffisant pour le texte secondaire du thème " + theme + " (" + muted.ToString("0.00") + ").");

                    var box = window.StyledTextBox();
                    box.Text = "Texte visible · 123";
                    box.Width = 260; box.Height = 38;
                    box.Measure(new Size(260, 38));
                    box.Arrange(new Rect(0, 0, 260, 38));
                    box.UpdateLayout();
                    var host = box.Template == null ? null : box.Template.FindName("PART_ContentHost", box) as ScrollViewer;
                    if (host == null) throw new InvalidOperationException("Le champ de texte n’expose pas PART_ContentHost (" + theme + ").");
                    if (host.Margin != new Thickness(0)) throw new InvalidOperationException("Le champ de texte applique une marge interne en double (" + theme + ").");
                    if (host.ActualWidth <= 0 || host.ActualHeight <= 0) throw new InvalidOperationException("Le champ de texte n’a pas de zone de contenu mesurable (" + theme + ").");
                    var foreground = box.Foreground as SolidColorBrush;
                    if (foreground == null || foreground.Color.A == 0) throw new InvalidOperationException("Le texte saisi serait invisible (" + theme + ").");

                    var password = window.StyledPasswordBox();
                    password.Width = 260; password.Height = 38;
                    password.Measure(new Size(260, 38));
                    password.Arrange(new Rect(0, 0, 260, 38));
                    password.UpdateLayout();
                    var passwordHost = password.Template == null ? null : password.Template.FindName("PART_ContentHost", password) as ScrollViewer;
                    if (passwordHost == null) throw new InvalidOperationException("Le champ mot de passe n’expose pas PART_ContentHost (" + theme + ").");
                    if (passwordHost.Margin != new Thickness(0)) throw new InvalidOperationException("Le champ mot de passe applique une marge interne en double (" + theme + ").");
                    report.AppendLine(theme + " · contraste " + primary.ToString("0.00") + "/" + muted.ToString("0.00") + " · champs visibles");
                }

                var scrollbar = new System.Windows.Controls.Primitives.ScrollBar
                {
                    Orientation = Orientation.Vertical, Style = window.ModernScrollBarStyle(), Height = 180, Width = 14
                };
                scrollbar.Measure(new Size(14, 180));
                scrollbar.Arrange(new Rect(0, 0, 14, 180));
                scrollbar.UpdateLayout();
                if (scrollbar.Template == null) throw new InvalidOperationException("Le style de barre de défilement n’a pas de modèle.");
                var track = scrollbar.Template.FindName("PART_Track", scrollbar) as System.Windows.Controls.Primitives.Track;
                var thumb = scrollbar.Template.FindName("Thumb", scrollbar) as System.Windows.Controls.Primitives.Thumb;
                if (track == null || thumb == null) throw new InvalidOperationException("La barre de défilement n’a pas de piste ou de curseur.");
                if (Math.Abs(thumb.Width - 6) > 0.01) throw new InvalidOperationException("Le curseur de défilement ne mesure pas 6 px au repos.");
                if (track.DecreaseRepeatButton == null || track.IncreaseRepeatButton == null)
                    throw new InvalidOperationException("La barre de défilement n’a pas de zones de clic.");
                if (track.DecreaseRepeatButton.Opacity > 0.01 || track.IncreaseRepeatButton.Opacity > 0.01)
                    throw new InvalidOperationException("Les flèches de défilement ne sont pas masquées.");

                var style = window.TryFindResource(typeof(System.Windows.Controls.Primitives.ScrollBar)) as Style;
                if (style == null) throw new InvalidOperationException("Le style global de barre de défilement n’est pas appliqué.");
                var viewerStyle = window.TryFindResource(typeof(ScrollViewer)) as Style;
                if (viewerStyle == null) throw new InvalidOperationException("Le style global de zone défilante n’est pas appliqué.");
                report.AppendLine("Scrollbars · curseur 6 px, sans flèches, style global actif");

                var combo = new ComboBox { Style = window.ModernComboStyle(), Width = 320, Height = 36 };
                combo.Items.Add("M3U / M3U8 - fichier ou URL");
                combo.Items.Add("Xtream Codes - serveur et identifiants");
                combo.SelectedIndex = 0;
                combo.Measure(new Size(320, 36));
                combo.Arrange(new Rect(0, 0, 320, 36));
                combo.UpdateLayout();
                if (combo.Template == null) throw new InvalidOperationException("La liste déroulante n’a pas de modèle.");
                var dropPopup = combo.Template.FindName("PART_Popup", combo) as System.Windows.Controls.Primitives.Popup;
                if (dropPopup == null) throw new InvalidOperationException("La liste déroulante n’expose pas PART_Popup.");
                var dropToggle = combo.Template.FindName("DropToggle", combo) as System.Windows.Controls.Primitives.ToggleButton;
                if (dropToggle == null) throw new InvalidOperationException("La liste déroulante n’a pas de bouton d’ouverture.");
                if (dropToggle.ActualWidth < 319) throw new InvalidOperationException("Le bouton de la liste déroulante ne couvre pas tout le champ (" + dropToggle.ActualWidth.ToString("0") + " px).");
                var toggleRoot = VisualTreeHelper.GetChildrenCount(dropToggle) > 0 ? VisualTreeHelper.GetChild(dropToggle, 0) as Panel : null;
                if (toggleRoot == null || toggleRoot.Background == null) throw new InvalidOperationException("La zone cliquable de la liste déroulante n’est pas cliquable sur toute sa surface.");
                report.AppendLine("Liste déroulante · bouton cliquable sur toute la surface, liste PART_Popup");
                return report.ToString();
            }
            finally
            {
                try { window.Close(); } catch { }
            }
        }

        static double ContrastRatio(Color first, Color second)
        {
            double a = RelativeLuminance(first);
            double b = RelativeLuminance(second);
            if (a < b) { double swap = a; a = b; b = swap; }
            return (a + 0.05) / (b + 0.05);
        }

        static double RelativeLuminance(Color color)
        {
            Func<byte, double> channel = delegate(byte value)
            {
                double s = value / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            };
            return 0.2126 * channel(color.R) + 0.7152 * channel(color.G) + 0.0722 * channel(color.B);
        }
    }
}
