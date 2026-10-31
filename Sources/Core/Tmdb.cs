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
        public string OriginalTitle = "";
        public string PosterUrl = "";
        public string BackdropUrl = "";
        public string Overview = "";
        public string Genres = "";
        public string Year = "";
        public string Runtime = "";
        public string Certification = "";
        public string TrailerUrl = "";
        public string Cast = "";
        public string Director = "";
        public string Countries = "";
        public double Rating;
        public int VoteCount;
        public bool DetailsLoaded;
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
        [DataMember] public string OriginalTitle = "";
        [DataMember] public string PosterPath = "";
        [DataMember] public string BackdropPath = "";
        [DataMember] public string Overview = "";
        [DataMember] public string Genres = "";
        [DataMember] public string Year = "";
        [DataMember] public string Runtime = "";
        [DataMember] public string Certification = "";
        [DataMember] public string TrailerUrl = "";
        [DataMember] public string Cast = "";
        [DataMember] public string Director = "";
        [DataMember] public string Countries = "";
        [DataMember] public bool DetailsLoaded;
        [DataMember] public double Rating;
        [DataMember] public int VoteCount;
        [DataMember] public string Updated = "";

        public TmdbResult ToResult()
        {
            return new TmdbResult
            {
                TmdbId = TmdbId ?? "", MediaType = MediaType ?? "movie", Title = Title ?? "", OriginalTitle = OriginalTitle ?? "",
                PosterUrl = String.IsNullOrWhiteSpace(PosterPath) ? "" : TmdbClient.ImageBase + "w500" + PosterPath,
                BackdropUrl = String.IsNullOrWhiteSpace(BackdropPath) ? "" : TmdbClient.ImageBase + "w780" + BackdropPath,
                Overview = Overview ?? "", Genres = Genres ?? "", Year = Year ?? "", Runtime = Runtime ?? "",
                Certification = Certification ?? "", TrailerUrl = TrailerUrl ?? "", Cast = Cast ?? "",
                Director = Director ?? "", Countries = Countries ?? "", DetailsLoaded = DetailsLoaded,
                Rating = Rating, VoteCount = VoteCount
            };
        }

        public static TmdbCacheEntry FromResult(string key, TmdbResult result)
        {
            return new TmdbCacheEntry
            {
                Key = key, TmdbId = result.TmdbId, MediaType = result.MediaType, Title = result.Title, OriginalTitle = result.OriginalTitle,
                PosterPath = PathFromUrl(result.PosterUrl, "w500"), BackdropPath = PathFromUrl(result.BackdropUrl, "w780"),
                Overview = result.Overview, Genres = result.Genres, Year = result.Year,
                Runtime = result.Runtime, Certification = result.Certification, TrailerUrl = result.TrailerUrl,
                Cast = result.Cast, Director = result.Director, Countries = result.Countries,
                DetailsLoaded = result.DetailsLoaded, Rating = result.Rating, VoteCount = result.VoteCount, Updated = DateTime.Now.ToString("s")
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
            int year = TmdbTitleCleaner.ExtractYear(channel.Name);
            string key = CacheKey(TmdbTitleCleaner.CleanTitle(channel.Name), series);
            return year > 0 ? key + "|" + year.ToString(CultureInfo.InvariantCulture) : key;
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

        public static TmdbResult GetForChannel(Channel channel)
        {
            if (channel == null) return null;
            bool series = channel.Kind == "Séries" || channel.IsSeries;
            string key = CacheKey(channel);
            TmdbResult result = Get(key);
            if (result != null) return result;
            int year = TmdbTitleCleaner.ExtractYear(channel.Name);
            if (year == 0) Int32.TryParse(channel.Year, out year);
            if (year == 0) return Get(CacheKey(TmdbTitleCleaner.CleanTitle(channel.Name), series));
            result = Get(CacheKey(TmdbTitleCleaner.CleanTitle(channel.Name), series));
            return result != null && (String.IsNullOrWhiteSpace(result.Year) || result.Year == year.ToString(CultureInfo.InvariantCulture)) ? result : null;
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

        public static void Remove(string key)
        {
            if (String.IsNullOrWhiteSpace(key)) return;
            lock (Gate)
            {
                EnsureLoaded();
                if (!entries.Remove(key)) return;
                try
                {
                    AppPaths.EnsureDataDir();
                    var serializer = new DataContractJsonSerializer(typeof(List<TmdbCacheEntry>));
                    using (var stream = File.Create(AppPaths.File("tmdb-cache.json")))
                        serializer.WriteObject(stream, entries.Values.OrderBy(entry => entry.Key).ToList());
                }
                catch { }
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

    internal static class TmdbMatchStore
    {
        static readonly object Gate = new object();
        static readonly Dictionary<string, Dictionary<string, TmdbCacheEntry>> Profiles = new Dictionary<string, Dictionary<string, TmdbCacheEntry>>(StringComparer.OrdinalIgnoreCase);

        static Dictionary<string, TmdbCacheEntry> Entries()
        {
            string profile = ProfileScope.Sanitize(ProfileScope.Current);
            Dictionary<string, TmdbCacheEntry> entries;
            if (Profiles.TryGetValue(profile, out entries)) return entries;
            entries = new Dictionary<string, TmdbCacheEntry>(StringComparer.OrdinalIgnoreCase);
            string path = StateStore.ScopedFile("tmdb-matches.json");
            if (File.Exists(path))
            {
                try
                {
                    var serializer = new DataContractJsonSerializer(typeof(List<TmdbCacheEntry>));
                    using (var stream = File.OpenRead(path))
                    {
                        var loaded = serializer.ReadObject(stream) as List<TmdbCacheEntry>;
                        if (loaded != null)
                            foreach (var entry in loaded)
                                if (entry != null && !String.IsNullOrWhiteSpace(entry.Key)) entries[entry.Key] = entry;
                    }
                }
                catch { }
            }
            Profiles[profile] = entries;
            return entries;
        }

        static string Key(Channel channel)
        {
            string key = TmdbCacheStore.CacheKey(channel);
            if (!String.IsNullOrWhiteSpace(channel.SourceName)) key += "|source:" + TextUtil.NormalizeKey(channel.SourceName);
            string identity = !String.IsNullOrWhiteSpace(channel.Id) ? channel.Id : channel.StreamId;
            if (!String.IsNullOrWhiteSpace(identity)) key += "|id:" + TextUtil.NormalizeKey(identity);
            return key;
        }

        public static int Count { get { lock (Gate) { return Entries().Count; } } }

        public static TmdbResult Get(Channel channel)
        {
            if (channel == null) return null;
            lock (Gate)
            {
                TmdbCacheEntry entry;
                return Entries().TryGetValue(Key(channel), out entry) ? entry.ToResult() : null;
            }
        }

        public static void Put(Channel channel, TmdbResult result)
        {
            if (channel == null || result == null || String.IsNullOrWhiteSpace(result.TmdbId)) return;
            lock (Gate)
            {
                var entries = Entries();
                string key = Key(channel);
                entries[key] = TmdbCacheEntry.FromResult(key, result);
                Save(entries);
            }
        }

        public static void Remove(Channel channel)
        {
            if (channel == null) return;
            lock (Gate)
            {
                var entries = Entries();
                if (entries.Remove(Key(channel))) Save(entries);
            }
        }

        public static void Forget(string profile)
        {
            lock (Gate) Profiles.Remove(ProfileScope.Sanitize(profile));
        }

        public static void ForgetAll()
        {
            lock (Gate) Profiles.Clear();
        }

        static void Save(Dictionary<string, TmdbCacheEntry> entries)
        {
            try
            {
                AppPaths.EnsureDataDir();
                var serializer = new DataContractJsonSerializer(typeof(List<TmdbCacheEntry>));
                using (var stream = File.Create(StateStore.ScopedFile("tmdb-matches.json")))
                    serializer.WriteObject(stream, entries.Values.OrderBy(entry => entry.Key).ToList());
            }
            catch { }
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

        public static TmdbResult PickBest(List<TmdbResult> results, string title, int year)
        {
            if (results == null || results.Count == 0) return null;
            var ranked = results.Where(candidate => candidate != null)
                .Select(candidate => new { Result = candidate, Score = MatchScore(title, year, candidate) })
                .OrderByDescending(item => item.Score).ToList();
            if (ranked.Count == 0 || ranked[0].Score < 86) return null;
            if (ranked.Count > 1 && ranked[0].Score - ranked[1].Score < 8) return null;
            return ranked[0].Result;
        }

        public static int MatchScore(string title, int year, TmdbResult candidate)
        {
            if (candidate == null) return 0;
            string wanted = ComparableTitle(title);
            string localized = ComparableTitle(candidate.Title);
            string original = ComparableTitle(candidate.OriginalTitle);
            if (wanted.Length == 0 || (localized.Length == 0 && original.Length == 0)) return 0;
            int titleScore = Math.Max(TitleSimilarity(wanted, localized), TitleSimilarity(wanted, original));
            int score = titleScore;
            int candidateYear = TextUtil.ParseInt(candidate.Year, 0);
            if (year > 0 && candidateYear > 0)
                score += year == candidateYear ? 10 : -28;
            return Math.Max(0, Math.Min(100, score));
        }

        public static string MatchConfidence(int score)
        {
            if (score >= 90) return "Forte";
            if (score >= 76) return "À vérifier";
            return "Faible · vérifiez avant de choisir";
        }

        static string ComparableTitle(string value)
        {
            string normalized = TextUtil.NormalizeKey(TmdbTitleCleaner.CleanTitle(value));
            normalized = Regex.Replace(normalized, @"^(the|le|la|les|un|une|a|an)\s+", "", RegexOptions.IgnoreCase);
            return Regex.Replace(normalized, @"\s+", " ").Trim();
        }

        static int TitleSimilarity(string wanted, string candidate)
        {
            if (wanted.Length == 0 || candidate.Length == 0) return 0;
            if (wanted == candidate) return 90;
            string[] wantedWords = wanted.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string[] candidateWords = candidate.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (wantedWords.OrderBy(word => word).SequenceEqual(candidateWords.OrderBy(word => word))) return 82;
            int distance = EditDistance(wanted, candidate);
            double similarity = 1.0 - (double)distance / Math.Max(wanted.Length, candidate.Length);
            if (wanted.Contains(candidate) || candidate.Contains(wanted)) similarity = Math.Max(similarity, (double)Math.Min(wanted.Length, candidate.Length) / Math.Max(wanted.Length, candidate.Length) * 0.92);
            return Math.Max(0, (int)Math.Round(similarity * 78));
        }

        static int EditDistance(string left, string right)
        {
            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];
            for (int j = 0; j <= right.Length; j++) previous[j] = j;
            for (int i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= right.Length; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
                var swap = previous; previous = current; current = swap;
            }
            return previous[right.Length];
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
                OriginalTitle = series ? Val(obj, "original_name") : Val(obj, "original_title"),
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
            if (!obj.TryGetValue("genres", out raw) && !obj.TryGetValue("genre_ids", out raw)) return "";
            var ids = raw as object[];
            if (ids == null) return "";
            var names = new List<string>();
            foreach (object id in ids)
            {
                var genre = id as Dictionary<string, object>;
                if (genre != null)
                {
                    string genreName = Val(genre, "name");
                    if (!String.IsNullOrWhiteSpace(genreName) && !names.Contains(genreName)) names.Add(genreName);
                    continue;
                }
                int value;
                if (Int32.TryParse(Convert.ToString(id, CultureInfo.InvariantCulture), out value))
                {
                    string name;
                    if (Genres.TryGetValue(value, out name) && !names.Contains(name)) names.Add(name);
                }
            }
            return String.Join(", ", names.ToArray());
        }

        public static TmdbResult LoadDetails(AppSettings settings, TmdbResult result)
        {
            if (settings == null || result == null || String.IsNullOrWhiteSpace(result.TmdbId)) return result;
            string endpoint = result.IsSeries ? "tv/" : "movie/";
            string url = "https://api.themoviedb.org/3/" + endpoint + Uri.EscapeDataString(result.TmdbId) +
                "?api_key=" + Uri.EscapeDataString(ResolveKey(settings)) +
                "&language=" + Uri.EscapeDataString(String.IsNullOrWhiteSpace(settings.TmdbLanguage) ? "fr-FR" : settings.TmdbLanguage) +
                "&append_to_response=credits,videos,release_dates,content_ratings";
            var obj = JsonObject(HttpUtil.GetString(url, 15000, "LyaranTV/2.0"));
            if (obj == null) return result;
            TmdbResult detailed = FromJson(obj, result.IsSeries);
            detailed.TmdbId = result.TmdbId;
            detailed.MediaType = result.MediaType;
            detailed.DetailsLoaded = true;
            if (String.IsNullOrWhiteSpace(detailed.Title)) detailed.Title = result.Title;
            if (String.IsNullOrWhiteSpace(detailed.OriginalTitle)) detailed.OriginalTitle = result.OriginalTitle;
            if (String.IsNullOrWhiteSpace(detailed.PosterUrl)) detailed.PosterUrl = result.PosterUrl;
            if (String.IsNullOrWhiteSpace(detailed.BackdropUrl)) detailed.BackdropUrl = result.BackdropUrl;
            if (String.IsNullOrWhiteSpace(detailed.Overview)) detailed.Overview = result.Overview;
            if (String.IsNullOrWhiteSpace(detailed.Genres)) detailed.Genres = result.Genres;
            if (String.IsNullOrWhiteSpace(detailed.Year)) detailed.Year = result.Year;
            detailed.Runtime = Runtime(obj, result.IsSeries);
            detailed.Countries = Countries(obj);
            var credits = Object(obj, "credits");
            var cast = Array(credits, "cast");
            detailed.Cast = String.Join(", ", cast.Select(item => Val(item, "name")).Where(name => name.Length > 0).Take(8).ToArray());
            if (result.IsSeries)
            {
                var creators = Array(obj, "created_by");
                detailed.Director = String.Join(", ", creators.Select(item => Val(item, "name")).Where(name => name.Length > 0).Take(3).ToArray());
                if (String.IsNullOrWhiteSpace(detailed.Director)) detailed.Director = CrewNames(credits, "Director", 3);
                detailed.Certification = Certification(obj, true);
            }
            else
            {
                detailed.Director = CrewNames(credits, "Director", 3);
                detailed.Certification = Certification(obj, false);
            }
            var videos = Object(obj, "videos");
            var videoItems = Array(videos, "results");
            var trailer = videoItems.FirstOrDefault(item => String.Equals(Val(item, "site"), "YouTube", StringComparison.OrdinalIgnoreCase)
                && String.Equals(Val(item, "type"), "Trailer", StringComparison.OrdinalIgnoreCase));
            if (trailer == null) trailer = videoItems.FirstOrDefault(item => String.Equals(Val(item, "site"), "YouTube", StringComparison.OrdinalIgnoreCase));
            if (trailer != null && !String.IsNullOrWhiteSpace(Val(trailer, "key"))) detailed.TrailerUrl = "https://www.youtube.com/watch?v=" + Val(trailer, "key");
            if (detailed.Rating <= 0) detailed.Rating = result.Rating;
            if (detailed.VoteCount <= 0) detailed.VoteCount = result.VoteCount;
            return detailed;
        }

        static Dictionary<string, object> Object(Dictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        static List<Dictionary<string, object>> Array(Dictionary<string, object> source, string key)
        {
            object value;
            var array = source != null && source.TryGetValue(key, out value) ? value as object[] : null;
            return array == null ? new List<Dictionary<string, object>>() : array.OfType<Dictionary<string, object>>().ToList();
        }

        static string CrewNames(Dictionary<string, object> credits, string job, int limit)
        {
            return String.Join(", ", Array(credits, "crew").Where(item => String.Equals(Val(item, "job"), job, StringComparison.OrdinalIgnoreCase))
                .Select(item => Val(item, "name")).Where(name => name.Length > 0).Distinct().Take(limit).ToArray());
        }

        static string Runtime(Dictionary<string, object> obj, bool series)
        {
            int minutes = (int)Num(obj, "runtime");
            if (series)
            {
                object value;
                var runtimes = obj != null && obj.TryGetValue("episode_run_time", out value) ? value as object[] : null;
                if (runtimes != null && runtimes.Length > 0) Int32.TryParse(Convert.ToString(runtimes[0], CultureInfo.InvariantCulture), out minutes);
            }
            return minutes > 0 ? minutes.ToString(CultureInfo.InvariantCulture) + " min" : "";
        }

        static string Countries(Dictionary<string, object> obj)
        {
            var countries = Array(obj, "production_countries").Select(item => Val(item, "name")).Where(name => name.Length > 0).Distinct().ToList();
            if (countries.Count == 0)
            {
                object raw;
                var values = obj != null && obj.TryGetValue("origin_country", out raw) ? raw as object[] : null;
                if (values != null) countries = values.Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)).Where(name => name.Length > 0).Distinct().ToList();
            }
            return String.Join(", ", countries.Take(5).ToArray());
        }

        static string Certification(Dictionary<string, object> obj, bool series)
        {
            string key = series ? "content_ratings" : "release_dates";
            var envelope = Object(obj, key);
            var rows = Array(envelope, "results");
            var preferred = rows.FirstOrDefault(item => String.Equals(Val(item, "iso_3166_1"), "FR", StringComparison.OrdinalIgnoreCase));
            if (preferred == null) preferred = rows.FirstOrDefault(item => String.Equals(Val(item, "iso_3166_1"), "US", StringComparison.OrdinalIgnoreCase));
            if (preferred == null && rows.Count > 0) preferred = rows[0];
            if (preferred == null) return "";
            if (series) return Val(preferred, "rating");
            var releases = Array(preferred, "release_dates");
            var rated = releases.Select(item => Val(item, "certification")).FirstOrDefault(value => !String.IsNullOrWhiteSpace(value));
            return rated ?? "";
        }

        public static void Apply(Channel channel, TmdbResult result)
        {
            if (channel == null || result == null) return;
            channel.TmdbId = result.TmdbId;
            if (!String.IsNullOrWhiteSpace(result.OriginalTitle)) channel.OriginalTitle = result.OriginalTitle;
            channel.PosterUrl = result.PosterUrl;
            channel.BackdropUrl = result.BackdropUrl;
            channel.Overview = result.Overview;
            channel.Genres = result.Genres;
            channel.TmdbRating = result.Rating;
            channel.VoteCount = result.VoteCount;
            channel.TmdbDetailsLoaded = channel.TmdbDetailsLoaded || result.DetailsLoaded;
            channel.Runtime = String.IsNullOrWhiteSpace(result.Runtime) ? channel.Runtime : result.Runtime;
            channel.Certification = String.IsNullOrWhiteSpace(result.Certification) ? channel.Certification : result.Certification;
            channel.TrailerUrl = String.IsNullOrWhiteSpace(result.TrailerUrl) ? channel.TrailerUrl : result.TrailerUrl;
            channel.Cast = String.IsNullOrWhiteSpace(result.Cast) ? channel.Cast : result.Cast;
            channel.Director = String.IsNullOrWhiteSpace(result.Director) ? channel.Director : result.Director;
            channel.Countries = String.IsNullOrWhiteSpace(result.Countries) ? channel.Countries : result.Countries;
            channel.TmdbChecked = true;
            if (String.IsNullOrWhiteSpace(channel.Year) && !String.IsNullOrWhiteSpace(result.Year)) channel.Year = result.Year;
            if (String.IsNullOrWhiteSpace(channel.Description) && !String.IsNullOrWhiteSpace(result.Overview)) channel.Description = result.Overview;
            if (String.IsNullOrWhiteSpace(channel.Rating) && result.Rating > 0)
                channel.Rating = result.Rating.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Applique le cache TMDb au catalogue relu depuis la copie locale. Pour
        /// un compte de 240 000 éléments, le calcul du titre normalisé domine :
        /// il n'est fait qu'une fois par couple titre/type/année, puis réutilisé
        /// par tous les doublons de la playlist. Les contenus déjà enrichis sont
        /// sautés immédiatement.
        /// </summary>
        public static int ApplyCache(IEnumerable<Channel> channels)
        {
            int applied = 0;
            if (channels == null) return 0;
            bool hasManualMatches = TmdbMatchStore.Count > 0;
            var resolved = new Dictionary<string, TmdbResult>(StringComparer.OrdinalIgnoreCase);
            // Les playlists IPTV répètent souvent le même nom des dizaines de
            // fois (qualités, langues, doublons) : le nettoyage du titre et sa
            // normalisation ne sont calculés qu'une fois par nom brut.
            var cleanNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var baseKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var channel in channels)
            {
                if (channel == null) continue;
                if (channel.Kind != "Films" && channel.Kind != "Séries") continue;
                if (!String.IsNullOrWhiteSpace(channel.Season) || !String.IsNullOrWhiteSpace(channel.Episode)) continue;
                if (channel.TmdbChecked && !String.IsNullOrWhiteSpace(channel.TmdbId)) continue;
                string rawName = channel.Name ?? "";
                string clean;
                if (!cleanNames.TryGetValue(rawName, out clean)) { clean = TmdbTitleCleaner.CleanTitle(rawName); cleanNames[rawName] = clean; }
                if (String.IsNullOrWhiteSpace(clean)) continue;
                bool series = channel.Kind == "Séries" || channel.IsSeries;
                int year = TmdbTitleCleaner.ExtractYear(rawName);
                string baseKey;
                if (!baseKeys.TryGetValue(rawName, out baseKey))
                {
                    baseKey = TextUtil.NormalizeKey(clean) + "|" + (series ? "tv" : "movie");
                    baseKeys[rawName] = baseKey;
                }
                string key = year > 0 ? baseKey + "|" + year.ToString(CultureInfo.InvariantCulture) : baseKey;
                TmdbResult result;
                if (!resolved.TryGetValue(key, out result))
                {
                    result = hasManualMatches ? TmdbMatchStore.Get(channel) : null;
                    if (result == null) result = TmdbCacheStore.Get(key);
                    if (result == null && year > 0) result = TmdbCacheStore.Get(baseKey);
                    if (result != null && year > 0 && !String.IsNullOrWhiteSpace(result.Year)
                        && result.Year != year.ToString(CultureInfo.InvariantCulture)) result = null;
                    resolved[key] = result;
                }
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
                // Automatic metadata is shared by title/year. Manual choices are scoped to
                // an individual playlist item and must never leak to its title peers.
                TmdbResult automaticResult = TmdbCacheStore.GetForChannel(group.Items[0]);
                bool fromCache = automaticResult != null;
                bool lookedUp = false;
                bool needsLookup = automaticResult == null && group.Items.Any(channel =>
                    !channel.TmdbChecked && TmdbMatchStore.Get(channel) == null);
                if (needsLookup)
                {
                    lookedUp = true;
                    try
                    {
                        automaticResult = Lookup(settings, group.Title, group.Year, group.Series);
                        if (automaticResult != null)
                        {
                            TmdbCacheStore.Put(group.Key, automaticResult);
                            TmdbCacheStore.Trim(settings.TmdbCacheLimit);
                        }
                    }
                    catch { automaticResult = null; }
                }
                foreach (var channel in group.Items)
                {
                    TmdbResult result = TmdbMatchStore.Get(channel) ?? automaticResult;
                    if (result != null)
                    {
                        Apply(channel, result);
                        if (applied != null) applied(channel, result);
                    }
                    else channel.TmdbChecked = true;
                }
                done++;
                if (progress != null) progress(done, groups.Count, group.Title);
                if (lookedUp && !fromCache) { try { Thread.Sleep(120); } catch { } }
            }
            TmdbCacheStore.Save();
        }

        public static void ApplyOverride(Channel channel, TmdbResult result)
        {
            if (channel == null || result == null) return;
            TmdbMatchStore.Put(channel, result);
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
