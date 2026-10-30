using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace LyaranTV
{
    /// <summary>
    /// Points d'entrée utilisés par la ligne de commande (--theme, --demo,
    /// --example, --detail, --play) pour préparer une capture d'écran.
    /// </summary>
    public sealed partial class MainWindow
    {
        internal void ToggleFullScreenForTest()
        {
            ToggleFullScreen();
        }

        /// <summary>
        /// Capture d'écran : ouvre la carte de volume sur une valeur de 62 % pour
        /// vérifier visuellement le curseur, sur la page Lecture.
        /// </summary>
        internal void ShowVolumeCardForTest()
        {
            if (volumeSlider == null || volumeCard == null) return;
            volumeSlider.Value = 62;
            UpdateVolumeReadout();
            ShowVolumeCard(true);
        }

        /// <summary>
        /// Relevé de mise en page du lecteur (activé par LYARANTV_GEOM=1) :
        /// permet de vérifier au pixel près que l'image et les commandes
        /// atteignent bien le bord inférieur de la fenêtre en plein écran.
        /// </summary>
        internal void LogPlayerGeometry(string tag)
        {
            try
            {
                var text = new StringBuilder();
                text.Append("GEOM[").Append(tag).Append("]");
                text.Append(" win=").Append(Math.Round(ActualWidth)).Append("x").Append(Math.Round(ActualHeight));
                text.Append(" at=").Append(Math.Round(Left)).Append(",").Append(Math.Round(Top));
                if (rootGrid != null)
                    text.Append(" root=").Append(Math.Round(rootGrid.ActualWidth)).Append("x").Append(Math.Round(rootGrid.ActualHeight));
                if (mainScroll != null)
                    text.Append(" vp=").Append(Math.Round(mainScroll.ViewportWidth)).Append("x").Append(Math.Round(mainScroll.ViewportHeight))
                        .Append(" ext=").Append(Math.Round(mainScroll.ExtentHeight))
                        .Append(" off=").Append(Math.Round(mainScroll.VerticalOffset));
                if (videoPanel != null)
                    text.Append(" panel=").Append(Math.Round(videoPanel.ActualWidth)).Append("x").Append(Math.Round(videoPanel.ActualHeight))
                        .Append(" setH=").Append(Math.Round(videoPanel.Height));
                if (player != null && player.IsLoaded)
                {
                    Point origin = player.TranslatePoint(new Point(0, 0), this);
                    text.Append(" video=").Append(Math.Round(origin.X)).Append(",").Append(Math.Round(origin.Y)).Append(" ")
                        .Append(Math.Round(player.ActualWidth)).Append("x").Append(Math.Round(player.ActualHeight));
                }
                HttpUtil.AppendLog(text.ToString());
            }
            catch { }
        }

        internal static bool GeometryLogEnabled
        {
            get { return Environment.GetEnvironmentVariable("LYARANTV_GEOM") == "1"; }
        }

        internal void SetThemeForTest(string theme)
        {
            if (String.IsNullOrWhiteSpace(theme)) return;
            ChangeTheme(theme);
        }

        internal void StartDemoForTest()
        {
            LoadDemo();
            if (page != "Bibliothèque") ShowLibrary("Bibliothèque");
        }

        internal void StartExampleForTest()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Examples", "lyarantv-demo.m3u");
            if (!File.Exists(path)) path = Path.Combine(Directory.GetCurrentDirectory(), "Examples", "lyarantv-demo.m3u");
            if (!File.Exists(path))
            {
                ShowToast("Fichier d'exemple introuvable.", true, false);
                return;
            }
            var source = new PlaylistSource { Name = "Exemple LyaranTV", Type = "M3U/M3U8", FilePath = path };
            sources.Add(source);
            SaveSources();
            RebuildSidebar();
            Activate(source);
        }

        internal void ScrollForTest(string value)
        {
            double offset;
            if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out offset)) return;
            // Le contenu peut arriver après le chargement de la source : on
            // diffère le défilement pour que la capture montre la bonne zone.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            timer.Tick += delegate
            {
                timer.Stop();
                try { if (mainScroll != null) mainScroll.ScrollToVerticalOffset(offset); } catch { }
            };
            timer.Start();
        }

        internal void OpenDetailForTest(string title)
        {
            if (String.IsNullOrWhiteSpace(title)) return;
            Channel match = channels.FirstOrDefault(c => c != null && !String.IsNullOrWhiteSpace(c.Name)
                && c.Name.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0);
            if (match != null) ShowChannelInfo(match);
        }
    }
}
