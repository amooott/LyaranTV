using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LyaranTV
{
    /// <summary>
    /// Résultat de la reconnaissance automatique d'une source Xtream Codes
    /// derrière une playlist M3U. Les identifiants restent en mémoire ; ils ne
    /// sont jamais écrits dans les journaux ni affichés dans l'interface.
    /// </summary>
    internal sealed class XtreamConversion
    {
        public string BaseUrl = "";
        public string Username = "";
        public string Password = "";
        /// <summary>Origine de la détection : « URL get.php », « flux /live/ »…</summary>
        public string Reason = "";

        /// <summary>Crée la source Xtream correspondante en conservant le nom et l'EPG.</summary>
        public PlaylistSource Apply(PlaylistSource original)
        {
            var converted = new PlaylistSource
            {
                Name = original == null ? "Xtream" : original.Name,
                Type = "Xtream",
                Url = BaseUrl,
                Username = Username,
                Password = Password,
                EpgUrl = original == null ? "" : (original.EpgUrl ?? ""),
                IsDemo = false
            };
            return converted;
        }

        /// <summary>Message lisible, sans identifiant ni mot de passe.</summary>
        public string Note
        {
            get
            {
                return "Source M3U convertie automatiquement en Xtream Codes"
                    + (String.IsNullOrWhiteSpace(Reason) ? "." : " (" + Reason + ").");
            }
        }
    }

    /// <summary>
    /// Reconnaît les playlists M3U qui proviennent en réalité d'un serveur
    /// Xtream Codes : URL get.php avec identifiants, ou flux /live/, /movie/,
    /// /series/ contenant le couple utilisateur/mot de passe. La conversion
    /// reste facultative : si le serveur ne répond pas à player_api.php, le
    /// chargement M3U classique reprend la main.
    /// </summary>
    internal static class PlaylistConverter
    {
        /// <summary>Détecte Xtream dans une URL (get.php, player_api.php ou flux).</summary>
        public static XtreamConversion FromUrl(string value)
        {
            string url = (value ?? "").Trim();
            if (url.Length == 0 || !TextUtil.IsHttp(url)) return null;

            Uri parsed;
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsed)) return null;
            string path = parsed.AbsolutePath ?? "";

            XtreamConversion fromApi = FromApiUrl(parsed, path, url);
            if (fromApi != null) return fromApi;
            return FromStreamUrl(parsed, path);
        }

        /// <summary>Détecte Xtream à partir de l'URL ou du contenu d'une playlist M3U.</summary>
        public static XtreamConversion FromLocation(string location)
        {
            return FromUrl(location);
        }

        /// <summary>
        /// Parcourt les URL de la playlist (une seule fois, sans téléchargement
        /// supplémentaire) et renvoie la première source Xtream reconnue.
        /// </summary>
        public static XtreamConversion FromPlaylist(string text, string location)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            foreach (string raw in text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int pipe = line.IndexOf('|');
                if (pipe > 0) line = line.Substring(0, pipe).Trim();
                if (line.Length == 0) continue;
                string resolved = TextUtil.ResolveUrl(line, location);
                XtreamConversion conversion = FromUrl(resolved);
                if (conversion != null) return conversion;
            }
            return null;
        }

        static XtreamConversion FromApiUrl(Uri parsed, string path, string fullUrl)
        {
            string lower = (path ?? "").ToLowerInvariant();
            bool getPhp = lower.EndsWith("/get.php", StringComparison.Ordinal);
            bool playerApi = lower.EndsWith("/player_api.php", StringComparison.Ordinal);
            if (!getPhp && !playerApi) return null;

            Dictionary<string, string> query = ParseQuery(fullUrl);
            string username = QueryValue(query, "username");
            string password = QueryValue(query, "password");
            if (username.Length == 0 || password.Length == 0) return null;

            string baseUrl = BaseFromPath(parsed, path, getPhp ? "get.php" : "player_api.php");
            return new XtreamConversion
            {
                BaseUrl = PlaylistLoader.NormalizeXtreamBase(baseUrl),
                Username = username,
                Password = password,
                Reason = getPhp ? "URL get.php" : "URL player_api.php"
            };
        }

        static XtreamConversion FromStreamUrl(Uri parsed, string path)
        {
            string[] segments = (path ?? "").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 2 < segments.Length; i++)
            {
                string kind = segments[i].ToLowerInvariant();
                if (kind != "live" && kind != "movie" && kind != "series") continue;
                string username = SafeUnescape(segments[i + 1]);
                string password = SafeUnescape(segments[i + 2]);
                if (username.Length == 0 || password.Length == 0) continue;
                // Le dernier segment est l'identifiant du flux (101.ts, 201.mkv…).
                if (i + 3 >= segments.Length) continue;
                if (!Regex.IsMatch(segments[i + 3], @"^\d+(?:\.[A-Za-z0-9]{1,8})?$")) continue;

                string prefix = "/" + String.Join("/", segments, 0, i);
                string baseUrl = parsed.GetLeftPart(UriPartial.Authority) + (prefix == "/" ? "" : prefix);
                return new XtreamConversion
                {
                    BaseUrl = PlaylistLoader.NormalizeXtreamBase(baseUrl),
                    Username = username,
                    Password = password,
                    Reason = "flux /" + kind + "/"
                };
            }
            return null;
        }

        static string BaseFromPath(Uri parsed, string path, string fileName)
        {
            string directory = path ?? "";
            int slash = directory.LastIndexOf('/');
            if (slash >= 0) directory = directory.Substring(0, slash);
            return parsed.GetLeftPart(UriPartial.Authority) + directory;
        }

        static Dictionary<string, string> ParseQuery(string url)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int question = url.IndexOf('?');
            if (question < 0) return values;
            string query = url.Substring(question + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            foreach (string part in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int equals = part.IndexOf('=');
                if (equals <= 0) continue;
                string key = SafeUnescape(part.Substring(0, equals)).Trim();
                string value = SafeUnescape(part.Substring(equals + 1));
                if (key.Length > 0 && !values.ContainsKey(key)) values[key] = value;
            }
            return values;
        }

        static string QueryValue(Dictionary<string, string> values, string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? (value ?? "").Trim() : "";
        }

        static string SafeUnescape(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            try { return Uri.UnescapeDataString(value); }
            catch { return value; }
        }
    }
}
