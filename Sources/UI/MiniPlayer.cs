using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LyaranTV
{
    /// <summary>
    /// Mini lecteur : la vidéo sort du lecteur plein écran dans une petite
    /// fenêtre toujours au premier plan, pour continuer à regarder en travaillant
    /// ailleurs. La surface déplacée est le même élément WPF (image VLC ou
    /// MediaElement) : la lecture ne redémarre donc pas, la position et le son
    /// sont conservés.
    /// </summary>
    public sealed partial class MainWindow
    {
        Window miniPlayerWindow;
        Grid miniPlayerRoot;
        Border miniPlayerBar;
        TextBlock miniPlayerTitle;

        /// <summary>Vrai quand la vidéo vit dans la petite fenêtre.</summary>
        bool MiniPlayerActive { get { return miniPlayerWindow != null; } }

        void ToggleMiniPlayer()
        {
            if (MiniPlayerActive) ExitMiniPlayer(true);
            else EnterMiniPlayer();
        }

        void EnterMiniPlayer()
        {
            if (MiniPlayerActive || player == null || videoPanel == null) return;
            if (fullScreen) ExitFullScreen();
            CloseDetailOverlay();

            var parent = videoPanel.Parent as Panel;
            if (parent != null) parent.Children.Remove(videoPanel);
            // Les grandes commandes n'ont pas leur place dans 440 px : la petite
            // barre de la fenêtre les remplace, et la vidéo reprend toute la place.
            playerChromeShown = false;
            if (playerControlWrap != null) playerControlWrap.Visibility = Visibility.Collapsed;
            if (playerTopBar != null) playerTopBar.Visibility = Visibility.Collapsed;

            var root = new Grid { Background = Brushes.Black, ClipToBounds = true };
            root.Children.Add(videoPanel);
            miniPlayerBar = BuildMiniPlayerBar();
            miniPlayerBar.Visibility = Visibility.Collapsed;
            root.Children.Add(miniPlayerBar);
            miniPlayerRoot = root;

            var window = new Window
            {
                Owner = this, Title = "LyaranTV · mini lecteur",
                WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanResize,
                Width = 440, Height = 272, MinWidth = 320, MinHeight = 190,
                Background = Brushes.Black, Topmost = true, ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Content = root,
                FontFamily = new FontFamily("Inter, Segoe UI")
            };
            try
            {
                Rect work = SystemParameters.WorkArea;
                window.Left = Math.Max(work.Left + 12, work.Right - window.Width - 24);
                window.Top = Math.Max(work.Top + 12, work.Bottom - window.Height - 24);
            }
            catch { }
            window.MouseEnter += delegate { if (miniPlayerBar != null) miniPlayerBar.Visibility = Visibility.Visible; };
            window.MouseLeave += delegate { if (miniPlayerBar != null) miniPlayerBar.Visibility = Visibility.Collapsed; };
            window.KeyDown += MiniPlayerKeyDown;
            // Fermeture par Alt+F4 ou par le système : la surface revient dans la
            // page avant que la fenêtre ne disparaisse.
            window.Closing += delegate
            {
                if (miniPlayerWindow == window) miniPlayerWindow = null;
                RestoreVideoPanel();
            };
            miniPlayerWindow = window;
            window.Show();
            try { HttpUtil.AppendLog("Mini lecteur ouvert."); } catch { }
        }

        /// <summary>
        /// Referme le mini lecteur et remet la vidéo dans la page. <paramref name="showPlayer"/>
        /// ramène la fenêtre principale au premier plan.
        /// </summary>
        void ExitMiniPlayer(bool showPlayer)
        {
            var window = miniPlayerWindow;
            if (window == null) return;
            miniPlayerWindow = null;
            try
            {
                if (miniPlayerRoot != null && videoPanel != null) miniPlayerRoot.Children.Remove(videoPanel);
                window.Content = null;
                window.Close();
            }
            catch { }
            miniPlayerRoot = null;
            miniPlayerBar = null;
            miniPlayerTitle = null;
            RestoreVideoPanel();
            if (playerControlWrap != null) playerControlWrap.Visibility = Visibility.Visible;
            if (playerTopBar != null) playerTopBar.Visibility = Visibility.Visible;
            if (showPlayer)
            {
                try { Activate(); } catch { }
                ShowFullscreenControls();
            }
        }

        void RestoreVideoPanel()
        {
            if (videoPanel == null || playerLayout == null) return;
            if (playerLayout.Children.Contains(videoPanel)) return;
            Grid.SetRow(videoPanel, 0);
            playerLayout.Children.Insert(0, videoPanel);
        }

        void MiniPlayerKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { ExitMiniPlayer(true); e.Handled = true; }
            else if (e.Key == Key.Space) { TogglePlayback(); e.Handled = true; }
            else if (e.Key == Key.M) { ToggleMute(); e.Handled = true; }
        }

        /// <summary>
        /// Barre de la petite fenêtre : titre, lecture/pause, retour au lecteur,
        /// fermeture. Elle sert aussi de poignée de déplacement ; les boutons en
        /// sont exclus pour ne pas déplacer la fenêtre en cliquant dessus.
        /// </summary>
        Border BuildMiniPlayerBar()
        {
            var bar = new Border
            {
                Height = 38, VerticalAlignment = VerticalAlignment.Top,
                Background = new LinearGradientBrush(Color.FromArgb(228, 8, 8, 14), Color.FromArgb(64, 8, 8, 14), 90),
                Padding = new Thickness(10, 0, 6, 0), Cursor = Cursors.SizeAll
            };
            var grid = new DockPanel { LastChildFill = true };

            var closeIcon = Icon(Glyphs.Close, 11, Brushes.White);
            var close = MiniPlayerButton(closeIcon, "Fermer le mini lecteur", delegate { ExitMiniPlayer(true); }, true);
            DockPanel.SetDock(close, Dock.Right);
            grid.Children.Add(close);

            var restoreIcon = Icon(Glyphs.FullScreen, 11, Brushes.White);
            var restore = MiniPlayerButton(restoreIcon, "Revenir au lecteur", delegate { ExitMiniPlayer(true); }, false);
            DockPanel.SetDock(restore, Dock.Right);
            grid.Children.Add(restore);

            var pauseIcon = Icon(playbackPaused ? Glyphs.Play : Glyphs.Pause, 11, Brushes.White);
            var pause = MiniPlayerButton(pauseIcon, "Lecture / pause", delegate
            {
                TogglePlayback();
                pauseIcon.Text = playbackPaused ? Glyphs.Play : Glyphs.Pause;
            }, false);
            DockPanel.SetDock(pause, Dock.Right);
            grid.Children.Add(pause);

            miniPlayerTitle = Text(currentPlaying == null ? "LyaranTV" : TextUtil.DisplayTitle(currentPlaying.Name), 12, Brushes.White, true);
            miniPlayerTitle.TextWrapping = TextWrapping.NoWrap;
            miniPlayerTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            miniPlayerTitle.Margin = new Thickness(4, 0, 8, 0);
            grid.Children.Add(miniPlayerTitle);

            bar.Child = grid;
            bar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (miniPlayerWindow == null) return;
                if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
                try { miniPlayerWindow.DragMove(); } catch { }
            };
            return bar;
        }

        Button MiniPlayerButton(TextBlock icon, string tooltip, Action click, bool close)
        {
            var button = new Button
            {
                Content = icon, Width = 30, Height = 26, Padding = new Thickness(0), Margin = new Thickness(0, 0, 2, 0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, FocusVisualStyle = null, ToolTip = tooltip,
                Template = RoundedButtonTemplate(6), SnapsToDevicePixels = true,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            if (close)
            {
                button.MouseEnter += delegate { button.Background = Brush(Danger); };
                button.MouseLeave += delegate { button.Background = Brushes.Transparent; };
            }
            if (click != null) button.Click += delegate { click(); };
            return button;
        }
    }
}
