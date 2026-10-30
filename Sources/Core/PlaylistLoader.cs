using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace LyaranTV
{
    internal static class PlaylistLoader
    {
        public static PlaylistLoadResult Load(PlaylistSource source, Action<int, int, string, bool> report, Func<bool> cancelled)
        {
            if (source == null) throw new InvalidDataException("Aucune playlist sélectionnée.");
            if (source.IsDemo || source.Type == "Démo") return DemoLibrary();
            if (source.Type == "Xtream") return LoadXtream(source, report, cancelled);
            if (source.Type == "Portail MAC") return LoadStalker(source, report, cancelled);
            return LoadM3u(source, report, cancelled);
        }

        static void CheckCancel(Func<bool> cancelled)
        {
            if (cancelled != null && cancelled()) throw new OperationCanceledException("Chargement annulé.");
        }

        static PlaylistLoadResult LoadM3u(PlaylistSource source, Action<int, int, string, bool> report, Func<bool> cancelled)
        {
            var result = new PlaylistLoadResult();
            string location = source.Location;
            if (String.IsNullOrWhiteSpace(location)) throw new InvalidDataException("Indiquez un fichier ou une URL de playlist M3U.");

            // Une URL get.php?username=…&password=… est déjà une adresse Xtream.
            // On tente la conversion avant même de télécharger la playlist M3U.
            XtreamConversion direct = PlaylistConverter.FromLocation(location);
            bool conversionAttempted = direct != null;
            if (direct != null)
            {
                PlaylistLoadResult converted = TryConvertedXtream(source, direct, report, cancelled);
                if (converted != null) return converted;
            }

            report(6, 0, "Connexion à la source…", false);
            // Les playlists volumineuses (plusieurs dizaines de Mo) peuvent dépasser
            // largement 20 s sur une connexion lente.
            string text = HttpUtil.DownloadToString(location, 60000);
            CheckCancel(cancelled);
            report(16, 0, "Playlist récupérée (" + TextUtil.HumanCount(text.Length / 1024) + " Ko).", false);

            // Beaucoup de fournisseurs distribuent une playlist M3U dont les flux
            // /live/utilisateur/motdepasse/… trahissent un serveur Xtream. La
            // conversion apporte alors les catégories, les fiches VOD et l'EPG.
            if (!conversionAttempted)
            {
                XtreamConversion detected = PlaylistConverter.FromPlaylist(text, location);
                if (detected != null)
                {
                    PlaylistLoadResult converted = TryConvertedXtream(source, detected, report, cancelled);
                    if (converted != null) return converted;
                }
            }

            string detectedEpg;
            result.Items = ParseM3uData(text, location, out detectedEpg);
            int liveCount = result.Items.Count(c => c.Kind == "Chaînes");
            int movieCount = result.Items.Count(c => c.Kind == "Films");
            int seriesCount = result.Items.Count(c => c.Kind == "Séries");
            report(58, 1, TextUtil.HumanCount(result.Items.Count) + " élément(s) indexé(s) · "
                + TextUtil.HumanCount(liveCount) + " chaînes, "
                + TextUtil.HumanCount(movieCount) + " films, "
                + TextUtil.HumanCount(seriesCount) + " séries.", result.Items.Count == 0);
            CheckCancel(cancelled);

            string epgLocation = !String.IsNullOrWhiteSpace(source.EpgUrl) ? source.EpgUrl.Trim() : detectedEpg;
            if (!String.IsNullOrWhiteSpace(epgLocation))
            {
                result.EpgUrl = TextUtil.ResolveUrl(epgLocation, location);
                report(90, 2, "Guide XMLTV chargé en arrière-plan.", true);
            }
            else report(90, 2, "Aucune source EPG trouvée — étape ignorée.", true);

            CheckCancel(cancelled);
            report(96, 3, "Finalisation de la bibliothèque…", false);
            if (result.Items.Count == 0) throw new InvalidDataException("La playlist ne contient aucun élément exploitable.");
            return result;
        }

        /// <summary>
        /// Tente la lecture Xtream d'une playlist M3U reconnue. En cas d'échec
        /// (serveur sans player_api.php, compte M3U seul…), renvoie null pour
        /// laisser le chargement M3U classique continuer.
        /// </summary>
        static PlaylistLoadResult TryConvertedXtream(PlaylistSource source, XtreamConversion conversion,
            Action<int, int, string, bool> report, Func<bool> cancelled)
        {
            if (conversion == null) return null;
            PlaylistSource xtream = conversion.Apply(source);
            report(8, 0, "Playlist M3U reconnue · conversion automatique en Xtream Codes…", false);
            try
            {
                PlaylistLoadResult result = LoadXtream(xtream, report, cancelled);
                result.SourceOverride = xtream;
                result.ConversionNote = conversion.Note;
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                report(10, 0, "Conversion Xtream indisponible · lecture M3U classique…", false);
                HttpUtil.AppendLog("Conversion M3U vers Xtream ignorée : " + ex.GetBaseException().Message);
                return null;
            }
        }

        sealed class XtreamSlice
        {
            public string CategoryAction, StreamAction, Kind, Unit;
            public List<Channel> Items = new List<Channel>();
            public string Error = "";
            public bool Cancelled;
        }

        static PlaylistLoadResult LoadXtream(PlaylistSource source, Action<int, int, string, bool> report, Func<bool> cancelled)
        {
            var result = new PlaylistLoadResult();
            string baseUrl = NormalizeXtreamBase(source.Url);
            if (String.IsNullOrWhiteSpace(source.Username) || String.IsNullOrWhiteSpace(source.Password))
                throw new InvalidDataException("Renseignez le nom d’utilisateur et le mot de passe Xtream.");
            string auth = "?username=" + Uri.EscapeDataString(source.Username) + "&password=" + Uri.EscapeDataString(source.Password);

            report(4, 0, "Connexion au compte Xtream…", false);
            string info = DownloadXtream(baseUrl, auth, null);
            CheckCancel(cancelled);
            string account = CheckXtreamAccount(info);
            report(12, 0, account.Length > 0 ? account : "Connexion Xtream réussie.", false);

            // Les trois catalogues sont indépendants : les charger en parallèle divise
            // le temps d'attente par deux ou trois sur les serveurs lents.
            var slices = new[]
            {
                new XtreamSlice { CategoryAction = "get_live_categories", StreamAction = "get_live_streams", Kind = "Chaînes", Unit = "chaîne(s)" },
                new XtreamSlice { CategoryAction = "get_vod_categories", StreamAction = "get_vod_streams", Kind = "Films", Unit = "film(s)" },
                new XtreamSlice { CategoryAction = "get_series_categories", StreamAction = "get_series", Kind = "Séries", Unit = "série(s)" }
            };
            report(16, 1, "Chargement parallèle des catégories et catalogues…", false);
            int completed = 0;
            var tasks = new Task[slices.Length];
            for (int i = 0; i < slices.Length; i++)
            {
                XtreamSlice slice = slices[i];
                tasks[i] = Task.Factory.StartNew(delegate
                {
                    try
                    {
                        CheckCancel(cancelled);
                        string categories = DownloadXtream(baseUrl, auth, slice.CategoryAction);
                        CheckCancel(cancelled);
                        string json = DownloadXtream(baseUrl, auth, slice.StreamAction);
                        CheckCancel(cancelled);
                        slice.Items = ParseXtreamData(json, categories, baseUrl, source.Username, source.Password, slice.Kind);
                    }
                    catch (OperationCanceledException) { slice.Cancelled = true; }
                    catch (Exception ex) { slice.Error = ex.GetBaseException().Message; }
                    int done = Interlocked.Increment(ref completed);
                    bool skipped = slice.Error.Length > 0 || slice.Items.Count == 0;
                    string message = slice.Error.Length > 0
                        ? "Catalogue " + slice.Kind.ToLowerInvariant() + " indisponible."
                        : TextUtil.HumanCount(slice.Items.Count) + " " + slice.Unit + " chargée(s).";
                    report(16 + done * 22, 1, message, skipped);
                });
            }
            try { Task.WaitAll(tasks); }
            catch (AggregateException) { }
            if (slices.Any(s => s.Cancelled)) CheckCancel(cancelled);

            foreach (XtreamSlice slice in slices)
            {
                if (slice.Error.Length > 0)
                {
                    result.Warnings.Add("Catalogue " + slice.Kind.ToLowerInvariant() + " indisponible : " + slice.Error);
                    continue;
                }
                result.Items.AddRange(slice.Items);
            }
            report(84, 1, "Catalogues prêts · " + TextUtil.HumanCount(result.Items.Count) + " élément(s).", result.Items.Count == 0);

            string epgLocation = !String.IsNullOrWhiteSpace(source.EpgUrl) ? source.EpgUrl.Trim() : baseUrl + "/xmltv.php" + auth;
            result.EpgUrl = epgLocation;
            report(90, 2, "Guide des programmes chargé en arrière-plan.", true);
            report(96, 3, "Finalisation de la bibliothèque…", false);
            if (result.Items.Count == 0) throw new InvalidDataException("Le serveur Xtream n’a renvoyé aucune chaîne, aucun film ni aucune série. Vérifiez le compte et l’adresse du serveur.");
            return result;
        }

        static PlaylistLoadResult LoadStalker(PlaylistSource source, Action<int, int, string, bool> report, Func<bool> cancelled)
        {
            CheckCancel(cancelled);
            var client = new StalkerClient(source.Url, source.MacAddress, source.DeviceType);
            var result = client.Load(delegate(int percent, int stage, string message, bool skipped)
            {
                CheckCancel(cancelled);
                report(percent, stage, message, skipped);
            });
            if (!String.IsNullOrWhiteSpace(source.EpgUrl))
            {
                result.EpgUrl = source.EpgUrl;
                report(90, 2, "Guide XMLTV chargé en arrière-plan.", true);
            }
            else report(90, 2, "Aucune source EPG — étape ignorée.", true);
            report(96, 3, "Finalisation de la bibliothèque…", false);
            return result;
        }

        public static string NormalizeXtreamBase(string value)
        {
            string url = (value ?? "").Trim();
            if (url.Length == 0) throw new InvalidDataException("Indiquez l’adresse du serveur Xtream.");
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) url = "http://" + url;
            Uri parsed;
            if (Uri.TryCreate(url, UriKind.Absolute, out parsed))
            {
                string path = parsed.AbsolutePath.TrimEnd('/');
                if (path.EndsWith("/player_api.php", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/get.php", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/panel_api.php", StringComparison.OrdinalIgnoreCase))
                    path = path.Substring(0, path.LastIndexOf('/'));
                return parsed.GetLeftPart(UriPartial.Authority) + path;
            }
            return url.TrimEnd('/');
        }

        static string DownloadXtream(string baseUrl, string auth, string action)
        {
            string url = baseUrl + "/player_api.php" + auth + (String.IsNullOrWhiteSpace(action) ? "" : "&action=" + action);
            return HttpUtil.GetString(url, 45000, "LyaranTV/2.0");
        }

        static string CheckXtreamAccount(string info)
        {
            if (String.IsNullOrWhiteSpace(info)) return "";
            var root = JsonObject(info);
            if (root == null) return "";
            var user = ObjectValue(root, "user_info");
            if (user == null) return "";
            string auth = Val(user, "auth");
            if (auth == "0") throw new InvalidDataException("Identifiants Xtream refusés. Vérifiez le nom d’utilisateur et le mot de passe.");
            string status = Val(user, "status");
            string lower = (status ?? "").ToLowerInvariant();
            if (lower.Contains("disabled") || lower.Contains("expired") || lower.Contains("banned"))
                throw new InvalidDataException("Compte Xtream indisponible (statut : " + status + ").");
            string expiration = "";
            string exp = Val(user, "exp_date");
            long seconds;
            if (Int64.TryParse(exp, out seconds) && seconds > 0)
            {
                try { expiration = " · expire le " + new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds).ToLocalTime().ToString("dd/MM/yyyy"); } catch { }
            }
            string connections = "";
            string maxConnections = Val(user, "max_connections");
            if (maxConnections.Length > 0) connections = " · " + maxConnections + " connexion(s)";
            return "Compte Xtream connecté" + (status.Length > 0 ? " (" + status + ")" : "") + expiration + connections + ".";
        }

        public static List<Channel> ParseXtreamData(string json, string categoryJson, string baseUrl, string username, string password, string kind)
        {
            var categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in JsonArray(categoryJson))
            {
                var row = item as Dictionary<string, object>;
                if (row == null) continue;
                string id = Val(row, "category_id");
                string name = Val(row, "category_name");
                if (id.Length > 0 && name.Length > 0) categories[id] = name;
            }

            var rows = JsonArray(json);
            if (rows == null) throw new InvalidDataException("Réponse serveur IPTV invalide pour « " + kind + " ».");
            var parsed = new List<Channel>();
            int order = 0;
            foreach (var item in rows)
            {
                var row = item as Dictionary<string, object>;
                if (row == null) continue;
                string rawId = kind == "Séries" ? Val(row, "series_id") : Val(row, "stream_id");
                string name = Val(row, "name");
                if (rawId.Length == 0 || name.Length == 0) continue;
                string group = Val(row, "category_name");
                string categoryId = Val(row, "category_id");
                if (group.Length == 0) categories.TryGetValue(categoryId, out group);
                if (String.IsNullOrWhiteSpace(group)) group = kind;
                string extension = Val(row, "container_extension");
                if (extension.Length == 0) extension = kind == "Chaînes" ? "m3u8" : "mp4";
                string url = "", alternate = "";
                if (kind == "Chaînes")
                {
                    url = BuildXtreamLiveUrl(baseUrl, username, password, rawId, "m3u8");
                    alternate = BuildXtreamLiveUrl(baseUrl, username, password, rawId, "ts");
                }
                else if (kind == "Films")
                {
                    url = baseUrl.TrimEnd('/') + "/movie/" + Uri.EscapeDataString(username) + "/" + Uri.EscapeDataString(password) + "/" + Uri.EscapeDataString(rawId) + "." + extension;
                }
                string logo = Val(row, "stream_icon");
                if (logo.Length == 0) logo = Val(row, "cover");
                if (logo.Length == 0) logo = Val(row, "movie_image");
                logo = ResolveLogo(logo, baseUrl);
                string year = Val(row, "year");
                if (year.Length == 0)
                {
                    string release = Val(row, "releaseDate");
                    if (release.Length >= 4) year = release.Substring(0, 4);
                }
                parsed.Add(new Channel
                {
                    Name = name, Group = group, Url = url, AlternateUrl = alternate, Logo = logo,
                    Kind = kind, Id = kind + ":" + rawId, StreamId = rawId,
                    TvgId = kind == "Chaînes" ? Val(row, "epg_channel_id") : "",
                    Number = Val(row, "num"), Description = Val(row, "plot"), Rating = Val(row, "rating"),
                    Year = year, SeriesId = kind == "Séries" ? rawId : "", IsSeries = kind == "Séries",
                    Order = order++
                });
            }
            return parsed;
        }

        static string BuildXtreamLiveUrl(string baseUrl, string username, string password, string streamId, string extension)
        {
            return baseUrl.TrimEnd('/') + "/live/" + Uri.EscapeDataString(username) + "/" + Uri.EscapeDataString(password) + "/" + Uri.EscapeDataString(streamId) + "." + extension;
        }

        static string ResolveLogo(string logo, string baseUrl)
        {
            if (String.IsNullOrWhiteSpace(logo)) return "";
            logo = logo.Trim();
            if (logo.StartsWith("//", StringComparison.Ordinal)) return "http:" + logo;
            if (logo.StartsWith("/", StringComparison.Ordinal) && TextUtil.IsHttp(baseUrl))
            {
                try { return new Uri(new Uri(baseUrl), logo).AbsoluteUri; } catch { }
            }
            return logo;
        }

        public static List<Channel> ParseM3uData(string text, string sourceLocation, out string epgUrl)
        {
            epgUrl = DetectEpgUrl(text);
            if (!String.IsNullOrWhiteSpace(epgUrl)) epgUrl = TextUtil.ResolveUrl(epgUrl, sourceLocation);
            var parsed = new List<Channel>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string name = null, group = "Non classé", logo = null, tvgId = null, number = null, mediaType = null, extGroup = null;
            string httpUserAgent = null, httpReferer = null, httpOrigin = null, httpCookie = null;
            foreach (string raw in (text ?? "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("#EXTVLCOPT:", StringComparison.OrdinalIgnoreCase))
                {
                    string option = line.Substring("#EXTVLCOPT:".Length).Trim();
                    int equals = option.IndexOf('=');
                    if (equals > 0)
                    {
                        string key = option.Substring(0, equals).Trim().ToLowerInvariant();
                        string value = option.Substring(equals + 1).Trim();
                        if (key == "http-user-agent" || key == "http-useragent") httpUserAgent = value;
                        else if (key == "http-referrer" || key == "http-referer" || key == "referer") httpReferer = value;
                        else if (key == "http-origin") httpOrigin = value;
                        else if (key == "http-cookie" || key == "cookie") httpCookie = value;
                    }
                    continue;
                }
                if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
                {
                    int comma = line.LastIndexOf(',');
                    name = comma >= 0 ? line.Substring(comma + 1).Trim() : "";
                    string parsedGroup = M3uAttribute(line, "group-title");
                    group = parsedGroup.Length > 0 ? parsedGroup : (!String.IsNullOrWhiteSpace(extGroup) ? extGroup : "Non classé");
                    string tvgName = M3uAttribute(line, "tvg-name");
                    if (name.Length == 0) name = tvgName;
                    if (name.Length == 0) name = "Sans nom";
                    logo = M3uAttribute(line, "tvg-logo");
                    if (logo.Length == 0) logo = M3uAttribute(line, "logo");
                    tvgId = M3uAttribute(line, "tvg-id");
                    number = M3uAttribute(line, "tvg-chno");
                    if (number.Length == 0) number = M3uAttribute(line, "tvg-number");
                    if (number.Length == 0) number = M3uAttribute(line, "channel-number");
                    mediaType = M3uAttribute(line, "tvg-type");
                    if (mediaType.Length == 0) mediaType = M3uAttribute(line, "media-type");
                    if (mediaType.Length == 0) mediaType = M3uAttribute(line, "type");
                    continue;
                }
                if (line.StartsWith("#EXTGRP", StringComparison.OrdinalIgnoreCase))
                {
                    extGroup = line.Substring(7).Trim().TrimStart(':').Trim();
                    if (name != null) group = extGroup;
                    continue;
                }
                if (line.StartsWith("#")) continue;
                if (name == null) continue;
                string rawUrl = line;
                int pipe = rawUrl.IndexOf('|');
                if (pipe > 0)
                {
                    string pipeHeaders = rawUrl.Substring(pipe + 1);
                    if (pipeHeaders.IndexOf('=') > 0)
                    {
                        rawUrl = rawUrl.Substring(0, pipe);
                        foreach (string part in pipeHeaders.Split('&'))
                        {
                            int equals = part.IndexOf('=');
                            if (equals <= 0) continue;
                            string key = part.Substring(0, equals).Trim().ToLowerInvariant();
                            string value = Uri.UnescapeDataString(part.Substring(equals + 1).Trim());
                            if (key == "user-agent" || key == "http-user-agent" || key == "useragent") httpUserAgent = value;
                            else if (key == "referer" || key == "referrer" || key == "http-referer" || key == "http-referrer") httpReferer = value;
                            else if (key == "origin" || key == "http-origin") httpOrigin = value;
                            else if (key == "cookie" || key == "http-cookie") httpCookie = value;
                        }
                    }
                }
                string resolved = TextUtil.ResolveUrl(rawUrl, sourceLocation);
                if (String.IsNullOrWhiteSpace(resolved)) { name = null; continue; }
                string kind = ClassifyM3u(group, name, resolved, mediaType);
                string id = "m3u:" + TextUtil.Hash(resolved + "|" + name + "|" + group);
                if (!seen.Add(id)) { name = null; continue; }
                parsed.Add(new Channel
                {
                    Name = name.Trim(), Group = String.IsNullOrWhiteSpace(group) ? "Non classé" : group.Trim(),
                    Url = resolved, Logo = String.IsNullOrWhiteSpace(logo) ? "" : logo.Trim(),
                    TvgId = String.IsNullOrWhiteSpace(tvgId) ? "" : tvgId.Trim(),
                    Kind = kind, Id = id, StreamId = resolved,
                    Number = String.IsNullOrWhiteSpace(number) ? "" : number.Trim(), Order = parsed.Count,
                    HttpUserAgent = httpUserAgent, HttpReferer = httpReferer, HttpOrigin = httpOrigin, HttpCookie = httpCookie
                });
                name = null; logo = null; tvgId = null; number = null; mediaType = null;
                httpUserAgent = null; httpReferer = null; httpOrigin = null; httpCookie = null;
            }
            return parsed;
        }

        public static string ClassifyM3u(string group, string name, string url, string mediaType)
        {
            string text = ((group ?? "") + " " + (name ?? "") + " " + (url ?? "") + " " + (mediaType ?? "")).ToLowerInvariant();
            if (Regex.IsMatch(text, "\\bS\\d{1,2}\\s*[._ -]?\\s*E\\d{1,2}\\b", RegexOptions.IgnoreCase)
                || Regex.IsMatch(text, "\\b\\d{1,2}x\\d{1,2}\\b", RegexOptions.IgnoreCase)
                || text.Contains("series") || text.Contains("série") || text.Contains("serie") || text.Contains("season") || text.Contains("episode")
                || text.Contains("tv show") || text.Contains("/series/") || text.Contains("/serie/") || text.Contains("/episodes/"))
                return "Séries";
            if (text.Contains("movie") || text.Contains("film") || text.Contains("vod") || text.Contains("cinema") || text.Contains("cinéma")
                || text.Contains("video club") || text.Contains("videoclub") || text.Contains("/movie/") || text.Contains("/movies/") || text.Contains("/vod/"))
                return "Films";
            return "Chaînes";
        }

        static string M3uAttribute(string line, string attribute)
        {
            var match = Regex.Match(line, "(?:^|\\s)" + Regex.Escape(attribute) + "\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s,]+))", RegexOptions.IgnoreCase);
            return !match.Success ? "" : match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
        }

        public static string DetectEpgUrl(string playlist)
        {
            var match = Regex.Match(playlist ?? "", "(?:url-tvg|x-tvg-url|tvg-url)\\s*=\\s*[\"']?([^\"'\\s>]+)", RegexOptions.IgnoreCase);
            if (!match.Success) return "";
            string value = match.Groups[1].Value.Trim().TrimEnd(',');
            int comma = value.IndexOf(',');
            if (comma > 0) value = value.Substring(0, comma);
            return value;
        }

        public static List<Channel> ParseSeriesEpisodes(string json, Channel series, PlaylistSource source)
        {
            var root = JsonObject(json);
            if (root == null) throw new InvalidDataException("Aucun détail d’épisode renvoyé.");
            object raw;
            if (!root.TryGetValue("episodes", out raw) || raw == null) throw new InvalidDataException("Cette série ne contient pas de liste d’épisodes.");
            string baseUrl = NormalizeXtreamBase(source.Url);
            var all = new List<Channel>();
            var seasons = raw as Dictionary<string, object>;
            if (seasons != null)
            {
                foreach (var season in seasons) AddEpisodeRows(all, season.Value as object[], season.Key, series, source, baseUrl);
            }
            else
            {
                var episodes = raw as object[];
                if (episodes != null) AddEpisodeRows(all, episodes, "1", series, source, baseUrl);
            }
            if (all.Count == 0) throw new InvalidDataException("Aucun épisode disponible pour cette série.");
            return all.OrderBy(e => TextUtil.ParseInt(e.Season, 1)).ThenBy(e => TextUtil.ParseInt(e.Episode, 0)).ToList();
        }

        static void AddEpisodeRows(List<Channel> target, object[] rows, string season, Channel series, PlaylistSource source, string baseUrl)
        {
            if (rows == null) return;
            foreach (var item in rows)
            {
                var row = item as Dictionary<string, object>;
                if (row == null) continue;
                string id = Val(row, "id");
                if (id.Length == 0) continue;
                string title = Val(row, "title");
                string episodeNumber = Val(row, "episode_num");
                if (title.Length == 0) title = "Épisode " + (episodeNumber.Length > 0 ? episodeNumber : "?");
                var info = ObjectValue(row, "info");
                string extension = Val(row, "container_extension");
                if (extension.Length == 0 && info != null) extension = Val(info, "container_extension");
                if (extension.Length == 0) extension = "mp4";
                string url = baseUrl.TrimEnd('/') + "/series/" + Uri.EscapeDataString(source.Username) + "/" + Uri.EscapeDataString(source.Password) + "/" + Uri.EscapeDataString(id) + "." + extension;
                // « movie_image » est justement l'aperçu 16:9 de l'épisode : il est
                // conservé à part pour ne pas le confondre avec le logo de la série.
                string still = info == null ? "" : Val(info, "movie_image");
                target.Add(new Channel
                {
                    Name = title, Group = "Saison " + season, Url = url, Logo = series.Logo, Kind = "Épisodes",
                    Id = "episode:" + id, StreamId = id, TvgId = series.TvgId, SeriesId = series.StreamId,
                    Season = season, Episode = episodeNumber, Description = info == null ? "" : Val(info, "plot"),
                    Rating = info == null ? "" : Val(info, "rating"), StillUrl = still, Order = target.Count,
                    Runtime = info == null ? "" : Val(info, "duration")
                });
            }
        }

        public static List<EpgProgram> DownloadEpg(string location)
        {
            return ParseEpgBytes(HttpUtil.DownloadBytes(location, 45000));
        }

        public static List<EpgProgram> ParseEpgFile(string path)
        {
            return ParseEpgBytes(File.ReadAllBytes(path));
        }

        public static List<EpgProgram> ParseEpgBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) throw new InvalidDataException("Le fichier XMLTV est vide.");
            var input = new MemoryStream(bytes);
            Stream xmlStream = bytes.Length > 2 && bytes[0] == 0x1f && bytes[1] == 0x8b ? (Stream)new GZipStream(input, CompressionMode.Decompress) : input;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 256L * 1024L * 1024L, IgnoreComments = true };
            try
            {
                using (xmlStream) using (var reader = XmlReader.Create(xmlStream, settings))
                {
                    var list = new List<EpgProgram>();
                    var channelNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    while (list.Count < 100000 && reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element) continue;
                        if (reader.LocalName == "channel")
                        {
                            using (var subtree = reader.ReadSubtree())
                            {
                                var channel = XElement.Load(subtree);
                                string id = (string)channel.Attribute("id");
                                var display = channel.Elements().FirstOrDefault(e => e.Name.LocalName == "display-name");
                                if (!String.IsNullOrWhiteSpace(id) && display != null) channelNames[id] = display.Value.Trim();
                            }
                        }
                        else if (reader.LocalName == "programme")
                        {
                            using (var subtree = reader.ReadSubtree())
                            {
                                var element = XElement.Load(subtree);
                                var start = ParseXmlTvTime((string)element.Attribute("start"));
                                var stop = ParseXmlTvTime((string)element.Attribute("stop"));
                                if (!start.HasValue || !stop.HasValue || stop.Value <= start.Value) continue;
                                string title = PickLocalized(element, "title");
                                if (title.Length == 0) continue;
                                string channelId = (string)element.Attribute("channel") ?? "";
                                string channelName;
                                channelNames.TryGetValue(channelId, out channelName);
                                var icon = element.Elements().FirstOrDefault(e => e.Name.LocalName == "icon");
                                list.Add(new EpgProgram
                                {
                                    ChannelId = channelId, ChannelName = channelName ?? "", Title = title,
                                    Description = PickLocalized(element, "desc"), Category = PickLocalized(element, "category"),
                                    Episode = PickLocalized(element, "episode-num"), Icon = icon == null ? "" : ((string)icon.Attribute("src") ?? ""),
                                    Start = start.Value, Stop = stop.Value
                                });
                            }
                        }
                    }
                    return list;
                }
            }
            catch (XmlException ex) { throw new InvalidDataException("Guide XMLTV invalide : " + ex.Message, ex); }
        }

        static string PickLocalized(XElement element, string name)
        {
            var items = element.Elements().Where(e => e.Name.LocalName == name).ToList();
            if (items.Count == 0) return "";
            var french = items.FirstOrDefault(e => ((string)e.Attribute("lang") ?? "").StartsWith("fr", StringComparison.OrdinalIgnoreCase));
            return (french ?? items[0]).Value.Trim();
        }

        public static DateTimeOffset? ParseXmlTvTime(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            string normalized = Regex.Replace(text.Trim(), "\\s+", "");
            if (Regex.IsMatch(normalized, "^\\d{14}[+-]\\d{4}$"))
                normalized = normalized.Substring(0, 14) + " " + normalized.Substring(14, 3) + ":" + normalized.Substring(17, 2);
            else if (Regex.IsMatch(normalized, "^\\d{12}[+-]\\d{4}$"))
                normalized = normalized.Substring(0, 12) + " " + normalized.Substring(12, 3) + ":" + normalized.Substring(15, 2);
            DateTimeOffset value;
            string[] formats = { "yyyyMMddHHmmss zzz", "yyyyMMddHHmm zzz", "yyyyMMddHHmmss", "yyyyMMddHHmm" };
            if (DateTimeOffset.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value)) return value;
            if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value)) return value;
            return null;
        }

        public static string TestSource(PlaylistSource source)
        {
            if (source == null) throw new InvalidDataException("Source invalide.");
            if (source.IsDemo || source.Type == "Démo") return "La bibliothèque de démonstration est prête.";
            if (source.Type == "Xtream") return TestXtream(source);
            if (source.Type == "Portail MAC")
            {
                var client = new StalkerClient(source.Url, source.MacAddress, source.DeviceType);
                return client.TestConnection();
            }
            string location = source.Location;
            if (String.IsNullOrWhiteSpace(location)) throw new InvalidDataException("Indiquez un fichier ou une URL.");

            // Même logique que le chargement : une URL get.php ou des flux
            // /live/utilisateur/motdepasse/… sont testés d'abord en Xtream.
            XtreamConversion direct = PlaylistConverter.FromLocation(location);
            bool conversionAttempted = direct != null;
            if (direct != null)
            {
                string converted = TryTestXtream(source, direct);
                if (converted.Length > 0) return converted;
            }

            string text = HttpUtil.DownloadToString(location, 30000);
            if (!conversionAttempted)
            {
                XtreamConversion detected = PlaylistConverter.FromPlaylist(text, location);
                if (detected != null)
                {
                    string converted = TryTestXtream(source, detected);
                    if (converted.Length > 0) return converted;
                }
            }
            string epg;
            var items = ParseM3uData(text, location, out epg);
            if (items.Count == 0) throw new InvalidDataException("La playlist est accessible mais ne contient aucun élément.");
            return "Playlist accessible : " + TextUtil.HumanCount(items.Count(c => c.Kind == "Chaînes")) + " chaînes, "
                + TextUtil.HumanCount(items.Count(c => c.Kind == "Films")) + " films, "
                + TextUtil.HumanCount(items.Count(c => c.Kind == "Séries")) + " séries." + (String.IsNullOrWhiteSpace(epg) ? "" : " Guide XMLTV détecté.");
        }

        static string TestXtream(PlaylistSource source)
        {
            string baseUrl = NormalizeXtreamBase(source.Url);
            string auth = "?username=" + Uri.EscapeDataString(source.Username ?? "") + "&password=" + Uri.EscapeDataString(source.Password ?? "");
            string info = DownloadXtream(baseUrl, auth, null);
            string message = CheckXtreamAccount(info);
            return message.Length > 0 ? message : "Le serveur Xtream répond correctement.";
        }

        static string TryTestXtream(PlaylistSource source, XtreamConversion conversion)
        {
            if (conversion == null) return "";
            try
            {
                string message = TestXtream(conversion.Apply(source));
                return message + " Conversion M3U vers Xtream automatique (" + conversion.Reason + ").";
            }
            catch (Exception ex)
            {
                HttpUtil.AppendLog("Test de conversion M3U vers Xtream ignoré : " + ex.GetBaseException().Message);
                return "";
            }
        }

        static string ExampleMedia()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Examples", "Media", "lyarantv-demo.mp4");
                if (File.Exists(path)) return path;
            }
            catch { }
            return "";
        }

        public static PlaylistLoadResult DemoLibrary()
        {
            var result = new PlaylistLoadResult { EpgUrl = "" };
            string localExample = ExampleMedia();
            string[] remoteLiveUrls =
            {
                "http://media.w3.org/2010/05/sintel/trailer.mp4",
                "http://media.w3.org/2010/05/bunny/trailer.mp4",
                "http://media.w3.org/2010/05/video/movie_300.mp4",
                "http://vjs.zencdn.net/v/oceans.mp4"
            };
            string[] liveUrls = new string[remoteLiveUrls.Length];
            for (int i = 0; i < liveUrls.Length; i++) liveUrls[i] = String.IsNullOrWhiteSpace(localExample) ? remoteLiveUrls[i] : localExample;
            string[] liveNames = { "Démo · Cinéma", "Démo · Découverte", "Démo · Séries", "Démo · Musique" };
            string[] liveGroups = { "Généralistes", "Découverte", "Séries", "Musique" };
            for (int i = 0; i < liveNames.Length; i++)
            {
                result.Items.Add(new Channel
                {
                    Name = liveNames[i], Group = liveGroups[i], Kind = "Chaînes", Url = liveUrls[i],
                    Logo = "", TvgId = "demo." + i, Id = "demo:live:" + i, StreamId = liveUrls[i], Number = (i + 1).ToString(),
                    Description = "Extrait de démonstration public. Remplacez-le par votre playlist pour regarder vos chaînes.", Order = result.Items.Count
                });
            }
            string[] movies =
            {
                "http://media.w3.org/2010/05/sintel/trailer.mp4",
                "http://media.w3.org/2010/05/bunny/trailer.mp4",
                "http://media.w3.org/2010/05/video/movie_300.mp4",
                "http://vjs.zencdn.net/v/oceans.mp4"
            };
            if (!String.IsNullOrWhiteSpace(localExample)) for (int i = 0; i < movies.Length; i++) movies[i] = localExample;
            string[] movieNames = { "Big Buck Bunny", "Sintel", "Tears of Steel", "Elephants Dream" };
            for (int i = 0; i < movieNames.Length; i++)
            {
                result.Items.Add(new Channel
                {
                    Name = movieNames[i], Group = "Films de démonstration", Kind = "Films", Url = movies[i], Logo = "",
                    Id = "demo:movie:" + i, StreamId = movies[i], Year = "2008", Rating = "8.0",
                    Description = "Film libre de démonstration. La lecture utilise Windows Media Foundation.", Order = result.Items.Count
                });
            }
            string[] episodes = { "Épisode 1 · Le début", "Épisode 2 · La suite" };
            for (int i = 0; i < episodes.Length; i++)
            {
                result.Items.Add(new Channel
                {
                    Name = episodes[i], Group = "Saison 1", Kind = "Séries", Url = movies[i], Logo = "",
                    Id = "demo:series:1:" + i, StreamId = movies[i], Season = "1", Episode = (i + 1).ToString(),
                    Description = "Série de démonstration.", Order = result.Items.Count
                });
            }
            DateTimeOffset now = DateTimeOffset.Now;
            for (int i = 0; i < liveNames.Length; i++)
            {
                result.Guide.Add(new EpgProgram
                {
                    ChannelId = "demo." + i, ChannelName = liveNames[i], Title = "Programme de démonstration " + (i + 1),
                    Description = "Ce guide est généré localement pour montrer l’affichage EPG. Importez un fichier XMLTV pour vos vraies chaînes.",
                    Category = "Démonstration", Start = now.AddMinutes(-35), Stop = now.AddMinutes(25)
                });
                result.Guide.Add(new EpgProgram
                {
                    ChannelId = "demo." + i, ChannelName = liveNames[i], Title = "À suivre · " + (i + 1),
                    Description = "Programme suivant de la démonstration.", Category = "Démonstration",
                    Start = now.AddMinutes(25), Stop = now.AddMinutes(85)
                });
            }
            return result;
        }

        static Dictionary<string, object> JsonObject(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                return serializer.DeserializeObject(text) as Dictionary<string, object>;
            }
            catch { return null; }
        }

        static object[] JsonArray(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return new object[0];
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                object value = serializer.DeserializeObject(text);
                var array = value as object[];
                if (array != null) return array;
                var root = value as Dictionary<string, object>;
                if (root != null)
                {
                    foreach (string key in new[] { "data", "results", "items" })
                    {
                        object nested;
                        if (root.TryGetValue(key, out nested) && nested is object[]) return (object[])nested;
                    }
                }
                return new object[0];
            }
            catch (Exception ex) { throw new InvalidDataException("Réponse JSON invalide : " + ex.Message, ex); }
        }

        static Dictionary<string, object> ObjectValue(Dictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        static string Val(Dictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
        }
    }
}
