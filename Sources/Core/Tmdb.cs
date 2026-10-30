using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace LyaranTV
{
    internal sealed class TmdbResult
    {
        public string TmdbId = "";
        public string MediaType = "movie";
        public string Title = "";
        public string PosterUrl = "";
        public string BackdropUrl = "";
        public string Overview = "";
        public string Genres = "";
        public string Year = "";
        public double Rating;
        public int VoteCount;
        public bool IsSeries { get { return MediaType == "tv"; } }
    }

    internal static class TmdbTitleCleaner
    {
        static readonly Regex YearRegex = new Regex(@"(?<!\d)(19|20)\d{2}(?!\d)");
        static readonly Regex EpisodeRegex = new Regex(@"[ ._-]*\bS\d{1,2}[ ._-]?E\d{1,3}\b.*$", RegexOptions.IgnoreCase);
        static readonly Regex QualityRegex = new Regex(
            @"\b(4K|UHD|FHD|HD|SD|1080P?|720P?|2160P?|480P?|HDR10?|DV|DOLBY\s*VISION|WEB[- .]?DL|WEBRIP|BLURAY|BRRIP|BDRIP|DVDRIP|HDRIP|HDTV|REMUX|X264|X265|H264|H265|HEVC|AVC|AAC\d?|AC3|EAC3|DTS(?:[- ]?HD)?|TRUEHD|ATMOS|MULTI|TRUEFRENCH|FRENCH|VFF|VFQ|VF|VOSTFR|VOST|SUBFRENCH|EXTENDED|UNRATED|REMASTERED|IMAX|PROPER|REPACK|10BIT|8BIT)\b",
            RegexOptions.IgnoreCase);
        static readonly Regex BracketRegex = new Regex(@"\[[^\]]*\]|\([^)]*\)|\{[^}]*\}");
        // Préfixe de langue très courant dans les playlists IPTV : « FR - »,
        // « IT | », « EN : ». Sans ce nettoyage, la recherche TMDb ne trouvait
        // aucune correspondance et le contenu restait sans affiche ni bannière.
        static readonly Regex LeadingTagRegex = new Regex(@"^\s*(?:[A-Z]{2,4}\s*(?:[|:·]|[-–—])\s+|\|+\s*|[-–—]\s*)");
        static readonly Regex SeparatorRegex = new Regex(@"[._]+");
        static readonly Regex SpacesRegex = new Regex(@"\s{2,}");
        static readonly Regex TrailingNoiseRegex = new Regex(@"\s*[-–—|:]\s*$");

        public static string CleanTitle(string raw)
        {
            string value = (raw ?? "").Trim();
            if (value.Length == 0) return "";
            int extensionIndex = value.LastIndexOf('.');
            if (extensionIndex > 0 && extensionIndex < value.Length - 1)
            {
                string extension = value.Substring(extensionIndex);
                if (extension.Length <= 6 && TextUtil.IsVideoFile("x" + extension))
                    value = value.Substring(0, extensionIndex);
            }
            value = SeparatorRegex.Replace(value, " ");
            value = BracketRegex.Replace(value, " ");
            value = EpisodeRegex.Replace(value, " ");
            for (int i = 0; i < 3; i++) value = LeadingTagRegex.Replace(value, "");
            value = QualityRegex.Replace(value, " ");
            value = YearRegex.Replace(value, " ");
            value = SpacesRegex.Replace(value, " ");
            value = TrailingNoiseRegex.Replace(value, "");
            value = value.Trim(' ', '-', '–', '—', '|', ':', ',', ';');
            if (value.Length == 0)
            {
                value = BracketRegex.Replace((raw ?? "").Trim(), " ");
                value = SpacesRegex.Replace(SeparatorRegex.Replace(value, " "), " ").Trim();
            }
            return value;
        }

        public static int ExtractYear(string raw)
        {
            Match match = YearRegex.Match(raw ?? "");
            int year;
            return match.Success && Int32.TryParse(match.Value, out year) ? year : 0;
        }

        public static bool LooksLikeSeries(string name, string kind)
        {
            if (kind == "Séries" || kind == "Épisodes") return true;
            return Regex.IsMatch(name ?? "", @"\bS\d{1,2}[ ._-]?E\d{1,3}\b", RegexOptions.IgnoreCase);
        }
    }

    [DataContract]
    internal sealed class TmdbCacheEntry
    {
        [DataMember] public string Key = "";
        [DataMember] public string TmdbId = "";
        [DataMember] public string MediaType = "movie";
        [DataMember] public string Title = "";
        [DataMember] public string PosterPath = "";
        [DataMember] public string BackdropPath = "";
        [DataMember] public string Overview = "";
        [DataMember] public string Genres = "";
        [DataMember] public string Year = "";
        [DataMember] public double Rating;
        [DataMember] public int VoteCount;
        [DataMember] public string Updated = "";

        public TmdbResult ToResult()
        {
            return new TmdbResult
            {
                TmdbId = TmdbId ?? "", MediaType = MediaType ?? "movie", Title = Title ?? "",
                PosterUrl = String.IsNullOrWhiteSpace(PosterPath) ? "" : TmdbClient.ImageBase + "w500" + PosterPath,
                BackdropUrl = String.IsNullOrWhiteSpace(BackdropPath) ? "" : TmdbClient.ImageBase + "w780" + BackdropPath,
                Overview = Overview ?? "", Genres = Genres ?? "", Year = Year ?? "",
                Rating = Rating, VoteCount = VoteCount
            };
        }

        public static TmdbCacheEntry FromResult(string key, TmdbResult result)
        {
            return new TmdbCacheEntry
            {
                Key = key, TmdbId = result.TmdbId, MediaType = result.MediaType, Title = result.Title,
                PosterPath = PathFromUrl(result.PosterUrl, "w500"), BackdropPath = PathFromUrl(result.BackdropUrl, "w780"),
                Overview = result.Overview, Genres = result.Genres, Year = result.Year,
                Rating = result.Rating, VoteCount = result.VoteCount, Updated = DateTime.Now.ToString("s")
            };
        }

        static string PathFromUrl(string url, string size)
        {
            if (String.IsNullOrWhiteSpace(url)) return "";
            string marker = "/t/p/" + size;
            int index = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? url.Substring(index + marker.Length) : "";
        }
    }

    internal static class TmdbCacheStore
    {
        static readonly object Gate = new object();
        static Dictionary<string, TmdbCacheEntry> entries;

        public static string CacheKey(string title, bool series)
        {
            return TextUtil.NormalizeKey(title) + "|" + (series ? "tv" : "movie");
        }

        public static string CacheKey(Channel channel)
        {
            if (channel == null) return "";
            bool series = channel.Kind == "Séries" || channel.IsSeries;
            return CacheKey(TmdbTitleCleaner.CleanTitle(channel.Name), series);
        }

        static void EnsureLoaded()
        {
            if (entries != null) return;
            entries = new Dictionary<string, TmdbCacheEntry>(StringComparer.OrdinalIgnoreCase);
            string file = AppPaths.File("tmdb-cache.json");
            if (!File.Exists(file)) return;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<TmdbCacheEntry>));
                using (var stream = File.OpenRead(file))
                {
                    var loaded = serializer.ReadObject(stream) as List<TmdbCacheEntry>;
                    if (loaded != null) foreach (var entry in loaded)
                        if (entry != null && !String.IsNullOrWhiteSpace(entry.Key)) entries[entry.Key] = entry;
                }
            }
            catch { }
        }

        public static TmdbResult Get(string key)
        {
            if (String.IsNullOrWhiteSpace(key)) return null;
            lock (Gate)
            {
                EnsureLoaded();
                TmdbCacheEntry entry;
                return entries.TryGetValue(key, out entry) && !String.IsNullOrWhiteSpace(entry.TmdbId) ? entry.ToResult() : null;
            }
        }

        public static void Put(string key, TmdbResult result)
        {
            if (String.IsNullOrWhiteSpace(key) || result == null || String.IsNullOrWhiteSpace(result.TmdbId)) return;
            lock (Gate)
            {
                EnsureLoaded();
                entries[key] = TmdbCacheEntry.FromResult(key, result);
            }
        }

        public static int Count
        {
            get { lock (Gate) { EnsureLoaded(); return entries.Count; } }
        }

        public static void Save()
        {
            lock (Gate)
            {
                if (entries == null) return;
                try
                {
                    AppPaths.EnsureDataDir();
                    var serializer = new DataContractJsonSerializer(typeof(List<TmdbCacheEntry>));
                    using (var stream = File.Create(AppPaths.File("tmdb-cache.json")))
                        serializer.WriteObject(stream, entries.Values.OrderBy(e => e.Key).ToList());
                }
                catch { }
            }
        }

        public static void Trim(int limit)
        {
            lock (Gate)
            {
                EnsureLoaded();
                if (limit <= 0 || entries.Count <= limit) return;
                foreach (string key in entries.OrderBy(e => e.Value.Updated).Select(e => e.Key).Take(entries.Count - limit).ToList())
                    entries.Remove(key);
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                entries = new Dictionary<string, TmdbCacheEntry>(StringComparer.OrdinalIgnoreCase);
                try { if (File.Exists(AppPaths.File("tmdb-cache.json"))) File.Delete(AppPaths.File("tmdb-cache.json")); } catch { }
            }
        }
    }

    internal static class TmdbClient
    {
        public const string ImageBase = "https://image.tmdb.org/t/p/";
        public const string Attribution = "Ce produit utilise l’API TMDb mais n’est ni approuvé ni certifié par TMDb.";

        static readonly Dictionary<int, string> Genres = new Dictionary<int, string>
        {
            { 28, "Action" }, { 12, "Aventure" }, { 16, "Animation" }, { 35, "Comédie" }, { 80, "Crime" },
            { 99, "Documentaire" }, { 18, "Drame" }, { 10751, "Famille" }, { 14, "Fantastique" }, { 36, "Histoire" },
            { 27, "Horreur" }, { 10402, "Musique" }, { 9648, "Mystère" }, { 10749, "Romance" }, { 878, "Science-fiction" },
            { 10770, "Téléfilm" }, { 53, "Thriller" }, { 10752, "Guerre" }, { 37, "Western" },
            { 10759, "Action & Aventure" }, { 10762, "Jeunesse" }, { 10763, "Actualités" }, { 10764, "Téléréalité" },
            { 10765, "Science-fiction & Fantastique" }, { 10766, "Feuilleton" }, { 10767, "Talk-show" }, { 10768, "Guerre & Politique" }
        };

        public static string ResolveKey(AppSettings settings)
        {
            string key = settings == null ? "" : settings.TmdbApiKey;
            if (String.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("LYARANTV_TMDB_KEY");
            return (key ?? "").Trim();
        }

        public static bool IsConfigured(AppSettings settings)
        {
            return settings != null && settings.TmdbEnabled && ResolveKey(settings).Length > 0;
        }

        public static string Test(AppSettings settings)
        {
            string key = ResolveKey(settings);
            if (String.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Ajoutez d’abord une clé API TMDb (v3).");
            string url = "https://api.themoviedb.org/3/configuration?api_key=" + Uri.EscapeDataString(key);
            string json = HttpUtil.GetString(url, 15000, "LyaranTV/2.0");
            var root = JsonObject(json);
            if (root == null || !root.ContainsKey("images")) throw new InvalidDataException("Réponse TMDb inattendue. Vérifiez la clé.");
            return "Clé TMDb valide. Les affiches et notes peuvent être récupérées.";
        }

        public static List<TmdbResult> Search(AppSettings settings, string title, int year, bool series, int limit)
        {
            var results = new List<TmdbResult>();
            string key = ResolveKey(settings);
            if (String.IsNullOrWhiteSpace(key) || String.IsNullOrWhiteSpace(title)) return results;
            string url = "https://api.themoviedb.org/3/search/" + (series ? "tv" : "movie") +
                "?api_key=" + Uri.EscapeDataString(key) +
                "&language=" + Uri.EscapeDataString(String.IsNullOrWhiteSpace(settings.TmdbLanguage) ? "fr-FR" : settings.TmdbLanguage) +
                "&query=" + Uri.EscapeDataString(title) +
                "&include_adult=false";
            if (year > 0) url += (series ? "&first_air_date_year=" : "&year=") + year.ToString(CultureInfo.InvariantCulture);
            string json = HttpUtil.GetString(url, 15000, "LyaranTV/2.0");
            var root = JsonObject(json);
            if (root == null) return results;
            object raw;
            if (!root.TryGetValue("results", out raw)) return results;
            var array = raw as object[];
            if (array == null) return results;
            foreach (object item in array)
            {
                var obj = item as Dictionary<string, object>;
                if (obj == null) continue;
                var result = FromJson(obj, series);
                if (result != null && !String.IsNullOrWhiteSpace(result.TmdbId)) results.Add(result);
                if (results.Count >= Math.Max(1, limit)) break;
            }
            return results;
        }

        public static TmdbResult Lookup(AppSettings settings, string title, int year, bool series)
        {
            var results = Search(settings, title, year, series, 6);
            if (results.Count == 0 && year > 0) results = Search(settings, title, 0, series, 6);
            return PickBest(results, title, year);
        }

        static TmdbResult PickBest(List<TmdbResult> results, string title, int year)
        {
            if (results == null || results.Count == 0) return null;
            string wanted = TextUtil.NormalizeKey(title);
            TmdbResult best = null;
            int bestScore = Int32.MinValue;
            foreach (var candidate in results)
            {
                int score = 0;
                string candidateTitle = TextUtil.NormalizeKey(candidate.Title);
                if (candidateTitle == wanted) score += 12;
                else if (candidateTitle.StartsWith(wanted, StringComparison.Ordinal) || wanted.StartsWith(candidateTitle, StringComparison.Ordinal)) score += 5;
                else if (candidateTitle.Contains(wanted) || wanted.Contains(candidateTitle)) score += 2;
                if (year > 0 && candidate.Year == year.ToString(CultureInfo.InvariantCulture)) score += 6;
                if (candidate.VoteCount > 200) score += 1;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            return best;
        }

        static TmdbResult FromJson(Dictionary<string, object> obj, bool series)
        {
            string id = Val(obj, "id");
            if (String.IsNullOrWhiteSpace(id)) return null;
            var result = new TmdbResult
            {
                TmdbId = id,
                MediaType = series ? "tv" : "movie",
                Title = series ? Val(obj, "name") : Val(obj, "title"),
                PosterUrl = Image(Val(obj, "poster_path"), "w500"),
                BackdropUrl = Image(Val(obj, "backdrop_path"), "w780"),
                Overview = Val(obj, "overview"),
                Rating = Num(obj, "vote_average"),
                VoteCount = (int)Num(obj, "vote_count")
            };
            if (String.IsNullOrWhiteSpace(result.Title)) result.Title = series ? Val(obj, "original_name") : Val(obj, "original_title");
            string date = series ? Val(obj, "first_air_date") : Val(obj, "release_date");
            if (date.Length >= 4) result.Year = date.Substring(0, 4);
            result.Genres = GenreNames(obj);
            return result;
        }

        static string Image(string path, string size)
        {
            return String.IsNullOrWhiteSpace(path) ? "" : ImageBase + size + path;
        }

        static string GenreNames(Dictionary<string, object> obj)
        {
            object raw;
            if (!obj.TryGetValue("genre_ids", out raw)) return "";
            var ids = raw as object[];
            if (ids == null) return "";
            var names = new List<string>();
            foreach (object id in ids)
            {
                int value;
                if (Int32.TryParse(Convert.ToString(id, CultureInfo.InvariantCulture), out value))
                {
                    string name;
                    if (Genres.TryGetValue(value, out name) && !names.Contains(name)) names.Add(name);
                }
            }
            return String.Join(", ", names.ToArray());
        }

        public static void Apply(Channel channel, TmdbResult result)
        {
            if (channel == null || result == null) return;
            channel.TmdbId = result.TmdbId;
            channel.PosterUrl = result.PosterUrl;
            channel.BackdropUrl = result.BackdropUrl;
            channel.Overview = result.Overview;
            channel.Genres = result.Genres;
            channel.TmdbRating = result.Rating;
            channel.VoteCount = result.VoteCount;
            channel.TmdbChecked = true;
            if (String.IsNullOrWhiteSpace(channel.Year) && !String.IsNullOrWhiteSpace(result.Year)) channel.Year = result.Year;
            if (String.IsNullOrWhiteSpace(channel.Description) && !String.IsNullOrWhiteSpace(result.Overview)) channel.Description = result.Overview;
            if (String.IsNullOrWhiteSpace(channel.Rating) && result.Rating > 0)
                channel.Rating = result.Rating.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static int ApplyCache(IEnumerable<Channel> channels)
        {
            int applied = 0;
            if (channels == null) return 0;
            foreach (var channel in channels)
            {
                if (!IsEligible(channel)) continue;
                string key = TmdbCacheStore.CacheKey(channel);
                var result = TmdbCacheStore.Get(key);
                if (result == null) continue;
                Apply(channel, result);
                applied++;
            }
            return applied;
        }

        public static bool IsEligible(Channel channel)
        {
            if (channel == null) return false;
            if (channel.Kind != "Films" && channel.Kind != "Séries") return false;
            if (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode)) return false;
            if (channel.Kind == "Séries" && !channel.IsSeries && !String.IsNullOrWhiteSpace(channel.Url) && !String.IsNullOrWhiteSpace(channel.Episode)) return false;
            return !String.IsNullOrWhiteSpace(TmdbTitleCleaner.CleanTitle(channel.Name));
        }

        public static void Enrich(IList<Channel> channels, AppSettings settings, Action<Channel, TmdbResult> applied,
            Action<int, int, string> progress, Func<bool> cancelled)
        {
            if (channels == null || !IsConfigured(settings)) return;
            var groups = channels.Where(IsEligible).GroupBy(c => TmdbCacheStore.CacheKey(c), StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Key = g.Key, Items = g.ToList(), Title = TmdbTitleCleaner.CleanTitle(g.First().Name), Year = TmdbTitleCleaner.ExtractYear(g.First().Name), Series = g.First().Kind == "Séries" })
                .Where(g => g.Key.Length > 0 && g.Items.Any(c => !c.TmdbChecked))
                .Take(200).ToList();
            if (groups.Count == 0) return;
            int done = 0;
            foreach (var group in groups)
            {
                if (cancelled != null && cancelled()) break;
                TmdbResult result = TmdbCacheStore.Get(group.Key);
                bool fromCache = result != null;
                if (result == null)
                {
                    try
                    {
                        result = Lookup(settings, group.Title, group.Year, group.Series);
                        if (result != null)
                        {
                            TmdbCacheStore.Put(group.Key, result);
                            TmdbCacheStore.Trim(settings.TmdbCacheLimit);
                        }
                    }
                    catch { result = null; }
                }
                if (result != null)
                {
                    foreach (var channel in group.Items)
                    {
                        Apply(channel, result);
                        if (applied != null) applied(channel, result);
                    }
                }
                else
                {
                    foreach (var channel in group.Items) channel.TmdbChecked = true;
                }
                done++;
                if (progress != null) progress(done, groups.Count, group.Title);
                if (!fromCache) { try { Thread.Sleep(120); } catch { } }
            }
            TmdbCacheStore.Save();
        }

        public static void ApplyOverride(Channel channel, TmdbResult result)
        {
            if (channel == null || result == null) return;
            TmdbCacheStore.Put(TmdbCacheStore.CacheKey(channel), result);
            TmdbCacheStore.Save();
            Apply(channel, result);
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

        static string Val(Dictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
        }

        static double Num(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value) || value == null) return 0;
            double number;
            return Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : 0;
        }
    }
}
