using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace LyaranTV
{
    /// <summary>
    /// Finitions du lecteur : menu contextuel, indicateur de chargement animé,
    /// curseur discret en plein écran et sous-titres mis en forme selon les
    /// préférences. Le lecteur doit se comporter comme une application vidéo
    /// classique, pas comme une simple page web encapsulée.
    /// </summary>
    public sealed partial class MainWindow
    {
        ContextMenu BuildPlayerContextMenu()
        {
            var menu = CreateContextMenu();
            menu.Items.Add(MenuItem("Lecture / pause  ·  Espace", TogglePlayback, Glyphs.Play));
            menu.Items.Add(MenuItem("Reculer de 10 secondes  ·  J", delegate { SeekRelative(-10); }, Glyphs.Back));
            menu.Items.Add(MenuItem("Avancer de 10 secondes  ·  L", delegate { SeekRelative(10); }, Glyphs.Forward));
            if (playerLive) menu.Items.Add(MenuItem("Revenir au direct", ResyncLiveStream, Glyphs.Refresh));
            menu.Items.Add(MenuItem("Revenir au point précédent  ·  Retour arrière", ReturnToPreviousPoint, Glyphs.Back));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Pistes audio  ·  A", OpenAudioTrackMenu, Glyphs.Music));
            menu.Items.Add(MenuItem("Sous-titres  ·  S", OpenSubtitleMenu, Glyphs.Subtitles));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Ralentir  ·  [", delegate { ApplyPlaybackSpeed(playbackSpeed - 0.25, true); }, Glyphs.Clock));
            menu.Items.Add(MenuItem("Accélérer  ·  ]", delegate { ApplyPlaybackSpeed(playbackSpeed + 0.25, true); }, Glyphs.Clock));
            menu.Items.Add(MenuItem("Vitesse normale", delegate { ApplyPlaybackSpeed(1.0, true); }, Glyphs.Refresh));
            menu.Items.Add(MenuItem(loopPlayback ? "Désactiver la lecture en boucle" : "Lire en boucle  ·  Ctrl+L",
                ToggleLoopPlayback, Glyphs.Repeat));
            menu.Items.Add(MenuItem("Boucle A-B · définir le point A  ·  Maj+A", delegate { SetAbPoint(true); }, Glyphs.Repeat));
            menu.Items.Add(MenuItem("Boucle A-B · définir le point B  ·  Maj+B", delegate { SetAbPoint(false); }, Glyphs.Repeat));
            menu.Items.Add(MenuItem("Effacer la boucle A-B  ·  Maj+X", ClearAbLoop, Glyphs.Close));
            menu.Items.Add(MenuItem("Minuterie de veille…", OpenSleepMenu, Glyphs.Clock));
            menu.Items.Add(new Separator());
            menu.Items.Add(SectionItem("IMAGE ET SYNCHRONISATION"));
            menu.Items.Add(MenuItem("Zoom avant", delegate { ChangeZoom(0.1); }, Glyphs.Zoom));
            menu.Items.Add(MenuItem("Zoom arrière", delegate { ChangeZoom(-0.1); }, Glyphs.Zoom));
            menu.Items.Add(MenuItem("Rotation 90°", RotatePlayerImage, Glyphs.Refresh));
            menu.Items.Add(MenuItem(playerFlip ? "Image en miroir  ✓" : "Image en miroir", FlipPlayerImage, Glyphs.View));
            menu.Items.Add(MenuItem(SubtitleDelayLabel(), null, Glyphs.Subtitles));
            menu.Items.Add(MenuItem("Sous-titres · avancer de 0,5 s", delegate { ShiftSubtitleDelay(-0.5); }, Glyphs.ChevronLeft));
            menu.Items.Add(MenuItem("Sous-titres · retarder de 0,5 s", delegate { ShiftSubtitleDelay(0.5); }, Glyphs.ChevronRight));
            menu.Items.Add(MenuItem(SubtitleDelayLabel() == "Sous-titres synchronisés" ? "Synchronisation normale  ✓" : "Synchronisation normale", delegate { subtitleDelay = 0; UpdateSubtitle(); ShowToast("Sous-titres synchronisés.", false, true); }, Glyphs.Clock));
            menu.Items.Add(MenuItem("Réinitialiser l'image", ResetPlayerImage, Glyphs.Refresh));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Image précédente  ·  ,", delegate { StepFrame(-1); }, Glyphs.ChevronLeft));
            menu.Items.Add(MenuItem("Image suivante  ·  .", delegate { StepFrame(1); }, Glyphs.ChevronRight));
            menu.Items.Add(MenuItem("Format d'image · " + playerAspect, CycleAspect, Glyphs.Zoom));
            menu.Items.Add(MenuItem(statsVisible ? "Masquer les statistiques  ✓" : "Statistiques de lecture  ·  I", ToggleStatsHud, Glyphs.Info));
            menu.Items.Add(MenuItem("Plein écran  ·  F11", ToggleFullScreen, Glyphs.FullScreen));
            menu.Items.Add(MenuItem("Tous les raccourcis  ·  ?", ShowShortcutsDialog, Glyphs.Help));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Fiche du contenu", delegate { ScrollToPlayerInfo(); }, Glyphs.Info));
            if (currentPlaying != null && !String.IsNullOrWhiteSpace(currentPlaying.Url))
                menu.Items.Add(MenuItem("Ouvrir dans le lecteur externe", delegate { OpenInExternalPlayer(currentPlaying.Url); }, Glyphs.External));
            return menu;
        }

        void CycleAspect()
        {
            if (player == null) return;
            if (playerAspect == "Adapter") playerAspect = "Remplir";
            else if (playerAspect == "Remplir") playerAspect = "Étirer";
            else playerAspect = "Adapter";
            ApplyPlayerStretch();
            settings.AspectMode = playerAspect;
            SaveSettings();
            ShowToast("Format d'image · " + playerAspect, false, false);
            ShowFullscreenControls();
        }

        /// <summary>Anneau de chargement : deux cercles, une rotation continue.</summary>
        FrameworkElement CreateSpinner(double size)
        {
            var host = new Grid
            {
                Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false
            };
            host.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = size, Height = size, Stroke = Brush(Color.FromArgb(46, 255, 255, 255)),
                StrokeThickness = 2.5
            });
            var arc = new System.Windows.Shapes.Ellipse
            {
                Width = size, Height = size, Stroke = Brush(Accent), StrokeThickness = 2.8,
                StrokeDashArray = new DoubleCollection { 9, 5 }, StrokeDashCap = PenLineCap.Round,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var spin = new RotateTransform(0);
            arc.RenderTransform = spin;
            host.Children.Add(arc);
            if (!UiMotion.Reduced)
            {
                spin.BeginAnimation(RotateTransform.AngleProperty,
                    new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(1050)) { RepeatBehavior = RepeatBehavior.Forever });
            }
            else
            {
                // Version statique lisible : un quart de cercle accentué.
                arc.StrokeDashArray = new DoubleCollection { 18, 40 };
            }
            return host;
        }

        void ApplySubtitleStyle()
        {
            if (subtitleBox == null || subtitleText == null) return;
            double size = settings.SubtitleSize <= 0 ? 16 : settings.SubtitleSize;
            if (size < 12) size = 12;
            if (size > 30) size = 30;

            Color textColor;
            if (!TryParseHexColor(settings.SubtitleTextColor, out textColor)) textColor = Colors.White;
            Color backgroundColor;
            if (!TryParseHexColor(settings.SubtitleBackgroundColor, out backgroundColor)) backgroundColor = Colors.Black;
            double opacity = settings.SubtitleBackgroundOpacity;
            if (opacity < 0) opacity = 0;
            if (opacity > 1) opacity = 1;

            bool background = settings.SubtitleBackground && opacity > 0.001;
            subtitleBox.Background = background
                ? Brush(Color.FromArgb((byte)Math.Round(opacity * 255), backgroundColor.R, backgroundColor.G, backgroundColor.B))
                : Brushes.Transparent;
            subtitleBox.Padding = background ? new Thickness(13, 5, 13, 6) : new Thickness(8, 2, 8, 2);
            subtitleText.Foreground = Brush(textColor);
            subtitleText.FontSize = fullScreen ? size + 4 : size;
            subtitleText.LineHeight = (fullScreen ? size + 4 : size) * 1.36;
            // Un contour noir fin détache le texte des images claires sans
            // ajouter de seconde couche de texte.
            subtitleText.Effect = settings.SubtitleOutline
                ? new DropShadowEffect { BlurRadius = 3.5, ShadowDepth = 0, Opacity = 0.96, Color = Colors.Black }
                : null;
        }

        void ShowPlayerCursor()
        {
            if (fullScreen && Mouse.OverrideCursor == Cursors.None) Mouse.OverrideCursor = null;
        }

        void HidePlayerCursor()
        {
            if (fullScreen) Mouse.OverrideCursor = Cursors.None;
        }
    }
}
