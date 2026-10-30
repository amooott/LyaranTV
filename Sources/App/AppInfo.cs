using System;
using System.Globalization;

namespace LyaranTV
{
    /// <summary>
    /// Identité unique du programme : version affichée, dépôt GitHub des mises à
    /// jour et chemins d'installation. Une seule source de vérité, pour que la
    /// fenêtre « À propos », le setup et le vérificateur de mise à jour ne
    /// puissent jamais annoncer des versions différentes.
    /// </summary>
    internal static class AppInfo
    {
        public const string Name = "LyaranTV";
        public const string Vendor = "Amooott";
        public const string Description = "Lecteur IPTV et vidéo : M3U, Xtream, portails MAC, guide EPG, films et séries.";

        /// <summary>Version commerciale, sans métadonnée de pré-version.</summary>
        public const string Version = "1.1.2";

        /// <summary>Dépôt GitHub consulté pour les mises à jour.</summary>
        public const string Repository = "amooott/LyaranTV";

        public const string Website = "https://github.com/amooott/LyaranTV";

        public static Version Current
        {
            get
            {
                Version parsed;
                return System.Version.TryParse(Version, out parsed) ? parsed : new Version(0, 0, 0);
            }
        }

        /// <summary>« 1.0.0 » à partir du texte renvoyé par GitHub (« v1.0.1 »).</summary>
        public static Version Parse(string tag)
        {
            if (String.IsNullOrWhiteSpace(tag)) return null;
            string value = tag.Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
            int cut = value.IndexOfAny(new[] { '-', '+', ' ' });
            if (cut > 0) value = value.Substring(0, cut);
            int parts = 0;
            for (int i = 0; i < value.Length; i++) if (value[i] == '.') parts++;
            while (parts < 2) { value += ".0"; parts++; }
            Version parsed;
            return System.Version.TryParse(value, out parsed) ? parsed : null;
        }

        public static string Inline(string separator)
        {
            return Name + " " + Version + (String.IsNullOrEmpty(separator) ? "" : separator + Vendor);
        }

        /// <summary>Dossier du programme : %LocalAppData%\LyaranTV (sans droits administrateur).</summary>
        public static string InstallDir
        {
            get
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return System.IO.Path.Combine(local, Name);
            }
        }
    }
}
