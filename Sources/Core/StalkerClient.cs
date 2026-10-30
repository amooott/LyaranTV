using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LyaranTV
{
    internal sealed class StalkerClient
    {
        readonly Uri endpoint;
        readonly Uri portalRoot;
        readonly string mac;
        readonly string deviceType;
        readonly string userAgent;
        string token;
        readonly CookieContainer cookies = new CookieContainer();

        public StalkerClient(string portalUrl, string macAddress, string deviceType)
        {
            Uri parsed;
            if (!Uri.TryCreate((portalUrl ?? "").Trim(), UriKind.Absolute, out parsed) || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                throw new InvalidDataException("Adresse du portail invalide : indiquez une URL HTTP ou HTTPS.");
            mac = NormalizeMac(macAddress);
            endpoint = BuildEndpoint(parsed);
            string path = endpoint.AbsolutePath;
            int serverIndex = path.LastIndexOf("/server/", StringComparison.OrdinalIgnoreCase);
            string rootPath = serverIndex >= 0 ? path.Substring(0, serverIndex + 1) : path.Substring(0, Math.Max(1, path.LastIndexOf('/') + 1));
            portalRoot = new Uri(endpoint.GetLeftPart(UriPartial.Authority) + rootPath);
            string model = String.IsNullOrWhiteSpace(deviceType) ? "MAG250" : deviceType.Trim();
            this.deviceType = model;
            userAgent = "Mozilla/5.0 (QtEmbedded; Linux; U; en) AppleWebKit/533.3 (KHTML, like Gecko) " + model + " stbapp ver: 4 rev: 2721 Safari/533.3";
            cookies.Add(portalRoot, new Cookie("mac", mac));
            cookies.Add(portalRoot, new Cookie("stb_lang", "en"));
            cookies.Add(portalRoot, new Cookie("timezone", Uri.EscapeDataString(TimeZoneInfo.Local.Id)));
        }

        public static string NormalizeMac(string value)
        {
            string hex = Regex.Replace((value ?? "").Trim(), "[^0-9A-Fa-f]", "").ToUpperInvariant();
            if (hex.Length != 12) throw new InvalidDataException("Adresse MAC invalide : saisissez 12 chiffres hexadécimaux.");
            return String.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
        }

        static Uri BuildEndpoint(Uri input)
        {
            string path = input.AbsolutePath.TrimEnd('/');
            if (path.EndsWith("load.php", StringComparison.OrdinalIgnoreCase) || path.EndsWith("portal.php", StringComparison.OrdinalIgnoreCase)) return new Uri(input.GetLeftPart(UriPartial.Path));
            if (path.EndsWith("/c", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 2);
            return new Uri(input.GetLeftPart(UriPartial.Authority) + path.TrimEnd('/') + "/server/load.php");
        }

        public string TestConnection()
        {
            Handshake();
            return "Portail Stalker connecté · jeton reçu.";
        }

        public PlaylistLoadResult Load(Action<int, int, string, bool> report)
        {
            var result = new PlaylistLoadResult();
            report(5, 0, "Connexion au portail Stalker…", false);
            Handshake();
            report(18, 1, "Récupération des catégories…", false);
            var categories = Rows(Request("type=itv&action=get_genres"));
            var categoryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in categories)
            {
                string id = Value(row, "id"); string name = First(row, "title", "name", "genre_title");
                if (id.Length > 0 && name.Length > 0) categoryNames[id] = name;
            }
            report(35, 1, "Chargement des chaînes en direct…", false);
            var channels = Rows(Request("type=itv&action=get_all_channels"));
            foreach (var row in channels)
            {
                string id = First(row, "id", "ch_id"); string name = First(row, "name", "title");
                if (id.Length == 0 || name.Length == 0) continue;
                string genre = First(row, "tv_genre_id", "genre_id", "category_id"); string group;
                if (!categoryNames.TryGetValue(genre, out group)) group = First(row, "genre_title", "category_name");
                if (String.IsNullOrWhiteSpace(group)) group = "Toutes les chaînes";
                string command = First(row, "cmd", "url", "stream_url");
                string number = First(row, "number", "channel_number", "chno");
                string logo = First(row, "logo", "logo_url", "screenshot_uri");
                result.Items.Add(new Channel { Name = name, Group = group, Url = "stalker://" + Uri.EscapeDataString(id), Logo = logo, Kind = "Chaînes", Id = "stalker:" + id, StreamId = command.Length > 0 ? command : id, Number = number });
            }
            report(65, 1, result.Items.Count + " chaîne(s) chargée(s).", result.Items.Count == 0);
            report(72, 1, "Catalogue films non normalisé par tous les portails Stalker.", true);
            report(76, 1, "Catalogue séries non normalisé par tous les portails Stalker.", true);
            report(88, 2, "Le guide XMLTV sera chargé après l’ouverture de la bibliothèque.", true);
            if (result.Items.Count == 0) throw new InvalidDataException("Le portail a répondu, mais aucune chaîne n’a été renvoyée. Vérifiez l’adresse MAC et l’activation du compte.");
            return result;
        }

        public string ResolveStream(Channel channel)
        {
            string command = (channel.StreamId ?? "").Trim();
            string direct = StripCommandPrefix(command);
            if (direct.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || direct.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || direct.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) || direct.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase)) return direct;
            string response = Request("type=itv&action=create_link&cmd=" + Uri.EscapeDataString(command));
            var root = Json(response); var js = ObjectValue(root, "js") ?? root;
            string link = First(js, "cmd", "url", "stream_url"); link = StripCommandPrefix(link);
            if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !link.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) && !link.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Le portail n’a pas fourni de lien de lecture pour cette chaîne.");
            return link;
        }

        void Handshake()
        {
            string response = Request("type=stb&action=handshake&token=");
            var root = Json(response); var js = ObjectValue(root, "js") ?? root; token = First(js, "token");
            if (String.IsNullOrWhiteSpace(token)) throw new InvalidDataException("Le portail n’a pas accepté la connexion MAC. Vérifiez l’adresse du portail, la MAC et son autorisation.");
        }

        string Request(string query)
        {
            string suffix = query + "&JsHttpRequest=1-xml";
            var request = (HttpWebRequest)WebRequest.Create(new Uri(endpoint.AbsoluteUri + "?" + suffix));
            request.Method = "GET"; request.UserAgent = userAgent; request.Timeout = 30000; request.ReadWriteTimeout = 30000; request.CookieContainer = cookies; request.Referer = new Uri(portalRoot, "c/").AbsoluteUri;
            request.Headers["X-User-Agent"] = "Model: " + deviceType + "; Link: WiFi";
            if (!String.IsNullOrWhiteSpace(token)) request.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse()) using (var stream = response.GetResponseStream()) using (var reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(HttpUtil.DescribeException(ex, endpoint.AbsoluteUri), ex);
            }
        }

        static Dictionary<string, object> Json(string text)
        {
            try { var serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }; var result = serializer.DeserializeObject(text) as Dictionary<string, object>; if (result == null) throw new InvalidDataException("Réponse JSON du portail invalide."); return result; }
            catch (InvalidDataException) { throw; }
            catch (Exception ex) { throw new InvalidDataException("Réponse JSON du portail invalide.", ex); }
        }

        static List<Dictionary<string, object>> Rows(string text)
        {
            var root = Json(text); var dataRoot = ObjectValue(root, "js") ?? ObjectValue(root, "results") ?? root;
            object raw; if (!dataRoot.TryGetValue("data", out raw)) raw = dataRoot.ContainsKey("results") ? dataRoot["results"] : null;
            var list = raw as object[]; if (list == null && dataRoot == root) list = root.Values.FirstOrDefault(v => v is object[]) as object[];
            return list == null ? new List<Dictionary<string, object>>() : list.OfType<Dictionary<string, object>>().ToList();
        }

        static Dictionary<string, object> ObjectValue(Dictionary<string, object> source, string key) { object value; return source != null && source.TryGetValue(key, out value) ? value as Dictionary<string, object> : null; }
        static string Value(Dictionary<string, object> source, string key) { object value; return source != null && source.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : ""; }
        static string First(Dictionary<string, object> source, params string[] keys) { foreach (var key in keys) { string value = Value(source, key); if (!String.IsNullOrWhiteSpace(value)) return value; } return ""; }
        static string StripCommandPrefix(string command)
        {
            string value = (command ?? "").Trim();
            foreach (string prefix in new[] { "ffmpeg ", "auto ", "cutv ", "ffrt ", "ffrt2 ", "ffmpeg2 ", "mpegts ", "http ", "load ", "archive ", "play " })
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return value.Substring(prefix.Length).Trim();
            return value;
        }
    }
}
