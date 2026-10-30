using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace LyaranTV
{
    /// <summary>Un fichier de sous-titres proposé par OpenSubtitles.</summary>
    internal sealed class OnlineSubtitle
    {
        public int FileId;
        public string FileName = "";
        public string LanguageCode = "";
        public string LanguageName = "";
        public string Release = "";
        public string Format = "srt";
        public int Downloads;
        public bool HearingImpaired;

        public string Label
        {
            get
            {
                if (!String.IsNullOrWhiteSpace(Release)) return Release.Trim();
                if (!String.IsNullOrWhiteSpace(FileName)) return FileName;
                return "Fichier " + FileId.ToString(CultureInfo.InvariantCulture);
            }
        }

        public string Detail
        {
            get
            {
                var parts = new List<string>();
                string language = !String.IsNullOrWhiteSpace(LanguageName) ? LanguageName : (LanguageCode ?? "").ToUpperInvariant();
                if (!String.IsNullOrWhiteSpace(language)) parts.Add(language);
                if (!String.IsNullOrWhiteSpace(Format)) parts.Add(Format.ToUpperInvariant());
                if (Downloads > 0) parts.Add(Downloads.ToString("N0", CultureInfo.InvariantCulture) + " téléchargements");
                if (HearingImpaired) parts.Add("malentendants");
                return String.Join("  ·  ", parts.ToArray());
            }
        }
    }

    /// <summary>
    /// Recherche et téléchargement de sous-titres sur OpenSubtitles.com (API v1).
    /// Aucune requête n'est faite sans action de l'utilisateur : la clé n'est
    /// utilisée qu'au moment d'une recherche ou d'un téléchargement.
    /// </summary>
    internal static class OpenSubtitlesClient
    {
        const string Endpoint = "https://api.opensubtitles.com/api/v1";
        const string Agent = "LyaranTV/2.0";
        public const string Attribution = "Sous-titres fournis par OpenSubtitles.com";

        static readonly string[] Readable =
        {
            ".srt", ".ass", ".ssa", ".vtt", ".sub", ".txt"
        };

        public static string Key(AppSettings settings)
        {
            if (settings != null && !String.IsNullOrWhiteSpace(settings.OpenSubtitlesKey)) return settings.OpenSubtitlesKey.Trim();
            string fromEnvironment = (Environment.GetEnvironmentVariable("LYARANTV_OPENSUBTITLES_KEY") ?? "").Trim();
            return fromEnvironment;
        }

        public static bool IsConfigured(AppSettings settings) { return Key(settings).Length > 0; }

        /// <summary>
        /// Nettoie un nom de playlist pour n'en garder que le titre exploitable :
        /// « FR - Le Film (2021) - » → « Le Film (2021) ».
        /// </summary>
        public static string CleanTitle(string raw)
        {
            string value = (raw ?? "").Trim();
            if (value.Length == 0) return "";
            value = value.TrimEnd('-', ':', '·', '|', ' ', '\t');
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0 && dash <= 4) value = value.Substring(dash + 3).Trim();
            value = value.TrimStart('-', ':', '·', '|', ' ');
            // Un suffixe saison/épisode est redondant : la saison et l'épisode
            // sont envoyés séparément à l'API.
            int marker = value.LastIndexOf(" S", StringComparison.OrdinalIgnoreCase);
            if (marker > 0 && value.Length - marker <= 9) value = value.Substring(0, marker).Trim();
            return value.TrimEnd('-', ' ', '·');
        }

        public static List<OnlineSubtitle> Search(AppSettings settings, string query, string languageCode,
            bool episode, string season, string episodeNumber)
        {
            string key = Key(settings);
            if (key.Length == 0) throw new InvalidDataException("Ajoutez une clé API OpenSubtitles dans Paramètres → Métadonnées.");
            if (String.IsNullOrWhiteSpace(query)) throw new InvalidDataException("Indiquez le titre du film ou de la série.");

            var builder = new StringBuilder();
            builder.Append("query=").Append(Uri.EscapeDataString(query.Trim()));
            string language = (languageCode ?? "").Trim().ToLowerInvariant();
            if (language.Length > 0 && language != "all") builder.Append("&languages=").Append(Uri.EscapeDataString(language));
            builder.Append(episode ? "&type=episode" : "&type=movie");
            builder.Append(NumberParameter("&season_number=", season));
            builder.Append(NumberParameter("&episode_number=", episodeNumber));
            builder.Append("&order_by=download_count");

            string text = Request(Endpoint + "/subtitles?" + builder, key, "GET", null);
            return ParseResults(text);
        }

        /// <summary>Décode la réponse de /subtitles (séparé de la requête pour être testable).</summary>
        internal static List<OnlineSubtitle> ParseResults(string text)
        {
            var root = Json(text);
            var data = Arr(root, "data");
            var results = new List<OnlineSubtitle>();
            var seen = new HashSet<int>();
            foreach (object entry in data)
            {
                var item = entry as Dictionary<string, object>;
                var attributes = Child(item, "attributes");
                if (attributes == null) continue;
                var files = Arr(attributes, "files");
                var file = files.Length > 0 ? files[0] as Dictionary<string, object> : null;
                if (file == null) continue;
                int fileId = (int)Num(file, "file_id");
                if (fileId <= 0 || !seen.Add(fileId)) continue;
                // Le nom renvoyé est un nom de release, presque toujours sans
                // extension : l'API ne documente pas de champ « format », et le
                // téléchargement est forcé en SubRip.
                string format = Val(attributes, "format").Trim().ToLowerInvariant();
                string fileName = Val(file, "file_name");
                if (!IsReadableExtension(format)) format = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
                if (!IsReadableExtension(format)) format = "srt";
                if (!IsReadable(fileName)) fileName = (String.IsNullOrWhiteSpace(fileName) ? "sous-titre" : fileName) + "." + format;
                string code = Val(attributes, "language");
                results.Add(new OnlineSubtitle
                {
                    FileId = fileId,
                    FileName = fileName,
                    LanguageCode = code,
                    LanguageName = MediaTrackScanner.LanguageName(code, code.ToUpperInvariant()),
                    Release = Val(attributes, "release"),
                    Format = format,
                    Downloads = (int)Num(attributes, "download_count"),
                    HearingImpaired = Bool(attributes, "hearing_impaired")
                });
            }
            return results;
        }

        /// <summary>Télécharge un fichier, le décompresse au besoin et le pose dans le dossier des sous-titres.</summary>
        public static string Download(AppSettings settings, OnlineSubtitle subtitle)
        {
            if (subtitle == null || subtitle.FileId <= 0) throw new InvalidDataException("Aucun fichier sélectionné.");
            string key = Key(settings);
            if (key.Length == 0) throw new InvalidDataException("Ajoutez une clé API OpenSubtitles dans Paramètres → Métadonnées.");
            string body = "{\"file_id\":" + subtitle.FileId.ToString(CultureInfo.InvariantCulture) + ",\"sub_format\":\"srt\"}";
            var root = Json(Request(Endpoint + "/download", key, "POST", body));
            string link = Val(root, "link");
            string name = Val(root, "file_name");
            if (link.Length == 0) throw new InvalidDataException("OpenSubtitles n'a pas renvoyé de lien de téléchargement (quota atteint ?).");
            if (name.Length == 0) name = subtitle.FileName;
            byte[] bytes = HttpUtil.GetBytes(link, 90000, Agent);
            return Save(bytes, name);
        }

        public static string Save(byte[] bytes, string fileName)
        {
            if (bytes == null || bytes.Length == 0) throw new InvalidDataException("Le fichier de sous-titres téléchargé est vide.");
            string name = Sanitize(fileName);
            if (bytes.Length > 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
            {
                bytes = UnGzip(bytes);
                name = TrimExtension(name, ".gz");
            }
            else if (bytes.Length > 4 && bytes[0] == 'P' && bytes[1] == 'K')
            {
                bytes = UnZip(bytes, ref name);
            }
            if (!IsReadable(name)) name += ".srt";
            string directory = Directory();
            string path = Path.Combine(directory, name);
            int index = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(directory, Path.GetFileNameWithoutExtension(name) + " (" + index.ToString(CultureInfo.InvariantCulture) + ")" + Path.GetExtension(name));
                index++;
            }
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public static string Directory()
        {
            string path = Path.Combine(AppPaths.DataDir, "sous-titres");
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        static byte[] UnGzip(byte[] bytes)
        {
            using (var input = new MemoryStream(bytes))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return output.ToArray();
            }
        }

        static byte[] UnZip(byte[] bytes, ref string name)
        {
            using (var input = new MemoryStream(bytes))
            using (var archive = new ZipArchive(input, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!IsReadable(entry.FullName)) continue;
                    using (var stream = entry.Open())
                    using (var output = new MemoryStream())
                    {
                        stream.CopyTo(output);
                        name = Sanitize(Path.GetFileName(entry.FullName));
                        return output.ToArray();
                    }
                }
            }
            throw new InvalidDataException("L'archive téléchargée ne contient aucun sous-titre lisible.");
        }

        static bool IsReadable(string fileName)
        {
            return IsReadableExtension(Path.GetExtension(fileName ?? "").TrimStart('.').ToLowerInvariant());
        }

        static bool IsReadableExtension(string extension)
        {
            if (String.IsNullOrWhiteSpace(extension)) return false;
            string value = "." + extension.Trim().TrimStart('.');
            foreach (string allowed in Readable) if (value == allowed) return true;
            return false;
        }

        static string TrimExtension(string name, string suffix)
        {
            return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - suffix.Length) : name;
        }

        static string Sanitize(string fileName)
        {
            string value = Path.GetFileName((fileName ?? "").Trim());
            if (value.Length == 0) value = "sous-titre.srt";
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (Char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == ' ' || c == '(' || c == ')' || c == '[' || c == ']') builder.Append(c);
                else builder.Append('_');
            }
            string clean = builder.ToString().Trim(' ', '.');
            if (clean.Length == 0) clean = "sous-titre.srt";
            if (clean.Length > 110) clean = clean.Substring(clean.Length - 110).TrimStart(' ', '.', '-');
            return clean;
        }

        static string NumberParameter(string prefix, string value)
        {
            int number;
            if (!Int32.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number <= 0) return "";
            return prefix + number.ToString(CultureInfo.InvariantCulture);
        }

        static string Request(string url, string key, string method, string body)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.UserAgent = Agent;
            request.Headers["Api-Key"] = key;
            request.Accept = "application/json";
            request.Timeout = 25000;
            request.ReadWriteTimeout = 25000;
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            if (body != null)
            {
                byte[] payload = Encoding.UTF8.GetBytes(body);
                request.ContentType = "application/json";
                request.ContentLength = payload.Length;
                using (var stream = request.GetRequestStream()) stream.Write(payload, 0, payload.Length);
            }
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var memory = new MemoryStream())
                {
                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) memory.Write(buffer, 0, read);
                    return HttpUtil.DecodeText(memory.ToArray());
                }
            }
            catch (WebException ex)
            {
                string detail = "";
                try
                {
                    if (ex.Response != null)
                    {
                        using (var stream = ex.Response.GetResponseStream())
                        using (var reader = new StreamReader(stream)) detail = reader.ReadToEnd();
                    }
                }
                catch { }
                throw new InvalidDataException(Describe(ex, detail), ex);
            }
        }

        static string Describe(WebException ex, string detail)
        {
            string message = Val(Json(detail), "message");
            var response = ex.Response as HttpWebResponse;
            int code = response == null ? 0 : (int)response.StatusCode;
            if (code == 401 || code == 403) return "Clé API OpenSubtitles refusée ou non autorisée pour ce fichier. Vérifiez votre clé dans les paramètres.";
            if (code == 406) return "Ce fichier n'est pas téléchargeable avec votre compte OpenSubtitles (quota ou droits insuffisants).";
            if (code == 429) return "Trop de demandes à OpenSubtitles. Patientez une minute puis réessayez.";
            if (code == 503) return "OpenSubtitles est momentanément indisponible. Réessayez plus tard.";
            if (code == 404) return "Aucun fichier correspondant chez OpenSubtitles.";
            if (!String.IsNullOrWhiteSpace(message)) return message;
            return HttpUtil.DescribeException(ex, "https://api.opensubtitles.com/");
        }

        static Dictionary<string, object> Json(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                return serializer.DeserializeObject(text) as Dictionary<string, object>;
            }
            catch { return null; }
        }

        static Dictionary<string, object> Child(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value)) return null;
            return value as Dictionary<string, object>;
        }

        static object[] Arr(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value)) return new object[0];
            var array = value as object[];
            return array ?? new object[0];
        }

        static string Val(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) || value == null) return "";
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }

        static double Num(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) || value == null) return 0;
            double number;
            return Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : 0;
        }

        static bool Bool(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) || value == null) return false;
            if (value is bool) return (bool)value;
            bool parsed;
            return Boolean.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) && parsed;
        }
    }
}
