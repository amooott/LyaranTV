using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace LyaranTV
{
    [DataContract]
    internal sealed class LetterboxdActivity
    {
        [DataMember] public string Title = "";
        [DataMember] public string Year = "";
        [DataMember] public string Uri = "";
        [DataMember] public double Rating;
        [DataMember] public bool Watched;
        [DataMember] public bool Watchlisted;
        [DataMember] public string Review = "";
        [DataMember] public string Date = "";

        public string Key { get { return ActivityKey(Title, Year); } }

        public static string ActivityKey(string title, string year)
        {
            return TextUtil.NormalizeKey(TmdbTitleCleaner.CleanTitle(title)) + "|" + (year ?? "").Trim();
        }
    }

    [DataContract]
    internal sealed class LetterboxdProfile
    {
        [DataMember] public string Username = "";
        [DataMember] public string PublicUsernames = "";
    }

    [DataContract]
    internal sealed class LetterboxdPublicReview
    {
        [DataMember] public string Username = "";
        [DataMember] public string ProfileUsername = "";
        [DataMember] public string Title = "";
        [DataMember] public string Year = "";
        [DataMember] public string Uri = "";
        [DataMember] public string Review = "";
        [DataMember] public string Date = "";
        [DataMember] public double Rating;
    }

    [DataContract]
    internal sealed class LetterboxdRssCache
    {
        [DataMember] public string Updated = "";
        [DataMember] public string Usernames = "";
        [DataMember] public bool HasSuccessfulRefresh;
        [DataMember] public List<LetterboxdPublicReview> Reviews = new List<LetterboxdPublicReview>();
    }

    internal static class LetterboxdActivityStore
    {
        static readonly object Gate = new object();
        static readonly object RssRefreshGate = new object();
        static string activityCachePath = "";
        static List<LetterboxdActivity> activityCache;
        static string rssCachePath = "";
        static LetterboxdRssCache rssCache;

        public static string Username
        {
            get
            {
                lock (Gate)
                {
                    var profile = ReadProfile();
                    return profile == null ? "" : NormalizeUsername(profile.Username);
                }
            }
        }

        public static string PublicUsernames
        {
            get
            {
                lock (Gate)
                {
                    var profile = ReadProfile();
                    return profile == null ? "" : profile.PublicUsernames ?? "";
                }
            }
        }

        public static void SetUsername(string username)
        {
            lock (Gate)
            {
                var profile = ReadProfile() ?? new LetterboxdProfile();
                profile.Username = NormalizeUsername(username);
                SaveProfile(profile);
            }
        }

        public static void SetPublicUsernames(string usernames)
        {
            lock (Gate)
            {
                var profile = ReadProfile() ?? new LetterboxdProfile();
                profile.PublicUsernames = NormalizeUsernames(usernames);
                SaveProfile(profile);
            }
        }

        static LetterboxdProfile ReadProfile()
        {
            string path = StateStore.ScopedFile("letterboxd-profile.json");
            if (!File.Exists(path)) return null;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(LetterboxdProfile));
                using (var stream = File.OpenRead(path)) return serializer.ReadObject(stream) as LetterboxdProfile;
            }
            catch { return null; }
        }

        static void SaveProfile(LetterboxdProfile profile)
        {
            try
            {
                AppPaths.EnsureDataDir();
                var serializer = new DataContractJsonSerializer(typeof(LetterboxdProfile));
                using (var stream = File.Create(StateStore.ScopedFile("letterboxd-profile.json"))) serializer.WriteObject(stream, profile);
            }
            catch { }
        }

        static string NormalizeUsername(string username)
        {
            return (username ?? "").Trim().TrimStart('@').TrimEnd('/');
        }

        static string TitleKey(string title)
        {
            return TextUtil.NormalizeKey(TmdbTitleCleaner.CleanTitle(title));
        }

        static string NormalizeUsernames(string usernames)
        {
            return String.Join(", ", (usernames ?? "").Split(new[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeUsername).Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray());
        }

        public static List<string> GetPublicUsernames()
        {
            var names = new List<string>();
            string own = Username;
            if (own.Length > 0) names.Add(own);
            names.AddRange((PublicUsernames ?? "").Split(new[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(NormalizeUsername));
            return names.Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        }

        public static bool NeedsRssRefresh
        {
            get
            {
                if (GetPublicUsernames().Count == 0) return false;
                var cache = LoadRssCache();
                DateTime updated;
                string signature = String.Join("|", GetPublicUsernames().ToArray());
                if (!String.Equals(cache.Usernames, signature, StringComparison.OrdinalIgnoreCase)) return true;
                TimeSpan ttl = cache.HasSuccessfulRefresh ? TimeSpan.FromHours(6) : TimeSpan.FromMinutes(15);
                return !DateTime.TryParse(cache.Updated, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out updated)
                    || DateTime.UtcNow - updated.ToUniversalTime() > ttl;
            }
        }

        public static List<LetterboxdPublicReview> PublicReviews(Channel channel)
        {
            if (channel == null) return new List<LetterboxdPublicReview>();
            return FindPublicReviews(channel, ConfiguredRssReviews(LoadRssCache().Reviews));
        }

        static List<LetterboxdPublicReview> ConfiguredRssReviews(IEnumerable<LetterboxdPublicReview> source)
        {
            var configured = new HashSet<string>(GetPublicUsernames(), StringComparer.OrdinalIgnoreCase);
            return (source ?? Enumerable.Empty<LetterboxdPublicReview>()).Where(review => review != null
                && configured.Contains(String.IsNullOrWhiteSpace(review.ProfileUsername) ? review.Username : review.ProfileUsername)).ToList();
        }

        internal static List<LetterboxdPublicReview> FindPublicReviews(Channel channel, IEnumerable<LetterboxdPublicReview> source)
        {
            if (channel == null || source == null) return new List<LetterboxdPublicReview>();
            string wanted = TextUtil.NormalizeKey(TmdbTitleCleaner.CleanTitle(channel.Name));
            int year = TmdbTitleCleaner.ExtractYear(channel.Name);
            if (year == 0) Int32.TryParse(channel.Year, out year);
            var matches = source.Where(review => review != null && TextUtil.NormalizeKey(review.Title) == wanted).ToList();
            var years = matches.Where(review => !String.IsNullOrWhiteSpace(review.Year)).Select(review => review.Year).Distinct().ToList();
            if (year == 0 && years.Count > 1) return new List<LetterboxdPublicReview>();
            return matches.Where(review =>
                    year == 0 ? (years.Count <= 1 || String.IsNullOrWhiteSpace(review.Year)) :
                    (String.IsNullOrWhiteSpace(review.Year) ? years.Count <= 1 : review.Year == year.ToString(CultureInfo.InvariantCulture)))
                .OrderByDescending(review => review.Date).ThenBy(review => review.Username)
                .GroupBy(review => review.Username, StringComparer.OrdinalIgnoreCase).Select(group => group.First())
                .Take(8).ToList();
        }

        public static List<Channel> WatchlistChannels(IEnumerable<Channel> channels)
        {
            var records = Load().Where(item => item.Watchlisted)
                .GroupBy(item => TitleKey(item.Title), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            var matched = new List<Channel>();
            foreach (var channel in channels ?? Enumerable.Empty<Channel>())
            {
                if (channel == null || channel.Kind != "Films") continue;
                List<LetterboxdActivity> candidates;
                if (!records.TryGetValue(TitleKey(channel.Name), out candidates)) continue;
                var activity = Find(channel, candidates);
                if (activity != null && activity.Watchlisted) matched.Add(channel);
            }
            return matched.GroupBy(channel => TextUtil.NormalizeKey(TmdbTitleCleaner.CleanTitle(channel.Name)) + "|" + (channel.Year ?? ""), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(channel => channel.TmdbRating).First()).Take(30).ToList();
        }

        public static List<Channel> RecentPublicReviewChannels(IEnumerable<Channel> channels, int limit)
        {
            var reviews = ConfiguredRssReviews(LoadRssCache().Reviews)
                .GroupBy(review => TitleKey(review.Title), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            return (channels ?? Enumerable.Empty<Channel>())
                .Where(channel => channel != null && channel.Kind == "Films" && String.IsNullOrWhiteSpace(channel.Season))
                .Select(channel =>
                {
                    List<LetterboxdPublicReview> candidates;
                    return new { Channel = channel, Review = reviews.TryGetValue(TitleKey(channel.Name), out candidates)
                        ? FindPublicReviews(channel, candidates).FirstOrDefault() : null };
                })
                .Where(item => item.Review != null)
                .GroupBy(item => !String.IsNullOrWhiteSpace(item.Channel.TmdbId) ? "tmdb:" + item.Channel.TmdbId
                    : TextUtil.NormalizeKey(TmdbTitleCleaner.CleanTitle(item.Channel.Name)) + "|" + (item.Channel.Year ?? ""), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.Review.Date).ThenByDescending(item => item.Channel.TmdbRating).First())
                .OrderByDescending(item => item.Review.Date).ThenBy(item => item.Channel.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(Math.Max(1, limit)).Select(item => item.Channel).ToList();
        }

        public static HashSet<string> SearchMatches(IEnumerable<Channel> channels, string query)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (String.IsNullOrWhiteSpace(query)) return result;
            var activities = Load().GroupBy(item => TitleKey(item.Title), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            var reviews = ConfiguredRssReviews(LoadRssCache().Reviews)
                .GroupBy(item => TitleKey(item.Title), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (var channel in channels ?? Enumerable.Empty<Channel>())
            {
                if (channel == null || channel.Kind != "Films") continue;
                List<LetterboxdActivity> matchingActivities;
                List<LetterboxdPublicReview> matchingReviews;
                activities.TryGetValue(TitleKey(channel.Name), out matchingActivities);
                reviews.TryGetValue(TitleKey(channel.Name), out matchingReviews);
                var activity = Find(channel, matchingActivities ?? new List<LetterboxdActivity>());
                var publicReviews = FindPublicReviews(channel, matchingReviews ?? new List<LetterboxdPublicReview>());
                if (activity == null && publicReviews.Count == 0) continue;
                var searchable = new StringBuilder();
                if (activity != null)
                {
                    searchable.Append(' ').Append(activity.Title).Append(' ').Append(activity.Review).Append(' ').Append(activity.Date);
                    if (activity.Rating > 0) searchable.Append(' ').Append(activity.Rating.ToString("0.0", CultureInfo.InvariantCulture));
                    if (activity.Watched) searchable.Append(" vu watched");
                    if (activity.Watchlisted) searchable.Append(" à voir watchlist");
                }
                foreach (var review in publicReviews)
                {
                    searchable.Append(' ').Append(review.Username).Append(' ').Append(review.Review).Append(' ').Append(review.Date);
                    if (review.Rating > 0) searchable.Append(' ').Append(review.Rating.ToString("0.0", CultureInfo.InvariantCulture));
                }
                if (TextUtil.Contains(searchable.ToString(), query)) result.Add(channel.Id);
            }
            return result;
        }

        public static bool RefreshPublicReviews()
        {
            return RefreshPublicReviews(true);
        }

        public static bool RefreshPublicReviewsIfNeeded()
        {
            return RefreshPublicReviews(false);
        }

        static bool RefreshPublicReviews(bool force)
        {
            lock (RssRefreshGate)
            {
                if (!force && !NeedsRssRefresh) return true;
                var usernames = GetPublicUsernames();
                if (usernames.Count == 0) return false;
                var cache = CopyRssCache(LoadRssCache());
                if (cache.Reviews == null) cache.Reviews = new List<LetterboxdPublicReview>();
                int succeeded = 0;
                foreach (string username in usernames)
                {
                    try
                    {
                        string url = "https://letterboxd.com/" + Uri.EscapeDataString(username) + "/rss/";
                        string xml = HttpUtil.GetString(url, 7000, "LyaranTV/2.0");
                        var reviews = ParseRss(xml, username);
                        cache.Reviews.RemoveAll(review => String.Equals(review.Username, username, StringComparison.OrdinalIgnoreCase));
                        cache.Reviews.AddRange(reviews);
                        succeeded++;
                    }
                    catch { }
                }
                cache.Updated = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                cache.Usernames = String.Join("|", usernames.ToArray());
                cache.HasSuccessfulRefresh = succeeded > 0;
                SaveRssCache(cache);
                return succeeded > 0;
            }
        }

        internal static List<LetterboxdPublicReview> ParseRss(string xml, string username)
        {
            var reviews = new List<LetterboxdPublicReview>();
            if (String.IsNullOrWhiteSpace(xml)) return reviews;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2 * 1024 * 1024 };
            var document = new XmlDocument { XmlResolver = null };
            using (var text = new StringReader(xml))
            using (var reader = XmlReader.Create(text, settings)) document.Load(reader);
            foreach (XmlNode item in document.GetElementsByTagName("item"))
            {
                string fullTitle = ChildText(item, "title");
                if (String.IsNullOrWhiteSpace(fullTitle)) continue;
                string title = fullTitle.Trim();
                double rating = ParseRatingSuffix(ref title);
                string year = "";
                Match yearMatch = Regex.Match(title, @"(?:\(|,\s*|\s)((?:18|19|20|21)\d{2})\)?\s*$");
                if (yearMatch.Success)
                {
                    year = yearMatch.Groups[1].Value;
                    title = title.Substring(0, yearMatch.Index).TrimEnd(' ', ',', '(', '-', '–', '—');
                }
                title = TmdbTitleCleaner.CleanTitle(title);
                if (title.Length == 0) continue;
                string uri = ChildText(item, "link");
                if (!IsLetterboxdUri(uri)) uri = "";
                string author = ChildText(item, "creator");
                if (author.Length == 0) author = ChildText(item, "author");
                if (author.Length == 0) author = username ?? "";
                string date = ChildText(item, "pubDate");
                DateTime parsedDate;
                if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsedDate)) date = parsedDate.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                string review = CleanRssText(ChildText(item, "encoded"));
                if (review.Length == 0) review = CleanRssText(ChildText(item, "description"));
                reviews.Add(new LetterboxdPublicReview { Username = NormalizeUsername(author), ProfileUsername = NormalizeUsername(username), Title = title, Year = year, Uri = uri, Review = review, Date = date, Rating = rating });
                if (reviews.Count >= 100) break;
            }
            return reviews;
        }

        static double ParseRatingSuffix(ref string title)
        {
            int split = Math.Max(title.LastIndexOf(" - ", StringComparison.Ordinal), Math.Max(title.LastIndexOf(" – ", StringComparison.Ordinal), title.LastIndexOf(" — ", StringComparison.Ordinal)));
            if (split < 0) return 0;
            string suffix = title.Substring(split + 3).Trim();
            if (suffix.Length == 0 || suffix.Any(character => character != '★' && character != '☆' && character != '½' && character != ' ')) return 0;
            double rating = suffix.Count(character => character == '★') + (suffix.Contains("½") ? 0.5 : 0);
            title = title.Substring(0, split).TrimEnd();
            return Math.Min(5, rating);
        }

        static string ChildText(XmlNode parent, string localName)
        {
            foreach (XmlNode child in parent.ChildNodes)
                if (String.Equals(child.LocalName, localName, StringComparison.OrdinalIgnoreCase)) return child.InnerText ?? "";
            return "";
        }

        static string CleanRssText(string value)
        {
            string text = Regex.Replace(value ?? "", @"(?is)<(script|style)\b[^>]*>.*?</\1>", " ");
            text = Regex.Replace(text, @"(?i)<br\s*/?>|</p\s*>|</div\s*>", "\n");
            text = Regex.Replace(text, @"<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);
            return Regex.Replace(text ?? "", @"[\t\r\f\v ]+", " ").Trim();
        }

        static LetterboxdRssCache LoadRssCache()
        {
            lock (Gate)
            {
                string path = StateStore.ScopedFile("letterboxd-rss.json");
                if (rssCache != null && String.Equals(rssCachePath, path, StringComparison.OrdinalIgnoreCase)) return rssCache;
                rssCachePath = path;
                if (!File.Exists(path)) return rssCache = new LetterboxdRssCache();
                try
                {
                    var serializer = new DataContractJsonSerializer(typeof(LetterboxdRssCache));
                    using (var stream = File.OpenRead(path)) return rssCache = serializer.ReadObject(stream) as LetterboxdRssCache ?? new LetterboxdRssCache();
                }
                catch { return rssCache = new LetterboxdRssCache(); }
            }
        }

        static LetterboxdRssCache CopyRssCache(LetterboxdRssCache source)
        {
            if (source == null) return new LetterboxdRssCache();
            return new LetterboxdRssCache
            {
                Updated = source.Updated ?? "",
                Usernames = source.Usernames ?? "",
                HasSuccessfulRefresh = source.HasSuccessfulRefresh,
                Reviews = (source.Reviews ?? new List<LetterboxdPublicReview>()).Where(review => review != null)
                    .Select(review => new LetterboxdPublicReview
                    {
                        Username = review.Username ?? "", ProfileUsername = review.ProfileUsername ?? "", Title = review.Title ?? "",
                        Year = review.Year ?? "", Uri = review.Uri ?? "", Review = review.Review ?? "", Date = review.Date ?? "", Rating = review.Rating
                    }).ToList()
            };
        }

        static void SaveRssCache(LetterboxdRssCache cache)
        {
            lock (Gate)
            {
                try
                {
                    AppPaths.EnsureDataDir();
                    var serializer = new DataContractJsonSerializer(typeof(LetterboxdRssCache));
                    rssCachePath = StateStore.ScopedFile("letterboxd-rss.json");
                    using (var stream = File.Create(rssCachePath)) serializer.WriteObject(stream, cache);
                    rssCache = cache;
                }
                catch { }
            }
        }

        public static List<LetterboxdActivity> Load()
        {
            lock (Gate)
            {
                string path = StateStore.ScopedFile("letterboxd-activity.json");
                if (activityCache != null && String.Equals(activityCachePath, path, StringComparison.OrdinalIgnoreCase)) return activityCache;
                activityCachePath = path;
                if (!File.Exists(path)) return activityCache = new List<LetterboxdActivity>();
                try
                {
                    var serializer = new DataContractJsonSerializer(typeof(List<LetterboxdActivity>));
                    using (var stream = File.OpenRead(path))
                        return activityCache = (serializer.ReadObject(stream) as List<LetterboxdActivity> ?? new List<LetterboxdActivity>())
                            .Where(item => item != null && !String.IsNullOrWhiteSpace(item.Title)).ToList();
                }
                catch { return activityCache = new List<LetterboxdActivity>(); }
            }
        }

        public static LetterboxdActivity Find(Channel channel)
        {
            return Find(channel, Load());
        }

        static LetterboxdActivity Find(Channel channel, IEnumerable<LetterboxdActivity> source)
        {
            if (channel == null || source == null) return null;
            string title = LetterboxdActivity.ActivityKey(TmdbTitleCleaner.CleanTitle(channel.Name), "");
            int year = TmdbTitleCleaner.ExtractYear(channel.Name);
            if (year == 0) Int32.TryParse(channel.Year, out year);
            var matches = source.Where(item => item != null && LetterboxdActivity.ActivityKey(item.Title, "") == title).ToList();
            if (year == 0 && matches.Where(item => !String.IsNullOrWhiteSpace(item.Year)).Select(item => item.Year).Distinct().Count() > 1) return null;
            return matches
                .Where(item => year == 0 || String.IsNullOrWhiteSpace(item.Year) || item.Year == year.ToString(CultureInfo.InvariantCulture))
                .OrderByDescending(item => year > 0 && item.Year == year.ToString(CultureInfo.InvariantCulture))
                .FirstOrDefault();
        }

        public static string FilmUrl(Channel channel)
        {
            var activity = Find(channel);
            if (activity != null && IsLetterboxdUri(activity.Uri)) return activity.Uri;
            string title = TmdbTitleCleaner.CleanTitle(channel == null ? "" : channel.Name);
            return "https://letterboxd.com/search/films/" + Uri.EscapeDataString(title) + "/";
        }

        public static int ImportFiles(IEnumerable<string> paths)
        {
            int accepted = 0;
            lock (Gate)
            {
                var records = Load();
                var byKey = records.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
                foreach (string path in paths ?? Enumerable.Empty<string>())
                {
                    if (String.IsNullOrWhiteSpace(path) || !File.Exists(path) || !String.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)) continue;
                    string kind = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    foreach (var row in ReadCsv(path))
                    {
                        string name = Value(row, "Name", "Title");
                        if (String.IsNullOrWhiteSpace(name)) continue;
                        string year = Value(row, "Year");
                        string key = LetterboxdActivity.ActivityKey(name, year);
                        LetterboxdActivity record;
                        if (!byKey.TryGetValue(key, out record))
                        {
                            record = new LetterboxdActivity { Title = name.Trim(), Year = year.Trim() };
                            byKey[key] = record;
                        }
                        string uri = Value(row, "Letterboxd URI", "URI");
                        if (IsLetterboxdUri(uri)) record.Uri = uri;
                        double rating;
                        if (Double.TryParse(Value(row, "Rating"), NumberStyles.Float, CultureInfo.InvariantCulture, out rating)) record.Rating = Math.Max(0, Math.Min(5, rating));
                        string date = Value(row, "Watched Date", "Date");
                        if (date.Length > 0 || kind == "ratings" || kind == "diary" || kind == "reviews") record.Watched = true;
                        if (kind == "watchlist") record.Watchlisted = true;
                        string review = Value(row, "Review");
                        if (!String.IsNullOrWhiteSpace(review)) record.Review = review.Trim();
                        if (date.Length > 0) record.Date = date;
                        accepted++;
                    }
                }
                Save(byKey.Values);
            }
            return accepted;
        }

        public static LetterboxdActivity ToggleWatchlist(Channel channel)
        {
            return Update(channel, delegate(LetterboxdActivity item) { item.Watchlisted = !item.Watchlisted; });
        }

        public static LetterboxdActivity MarkWatched(Channel channel)
        {
            return Update(channel, delegate(LetterboxdActivity item) { item.Watched = !item.Watched; });
        }

        static LetterboxdActivity Update(Channel channel, Action<LetterboxdActivity> update)
        {
            if (channel == null) return null;
            lock (Gate)
            {
                int year = TmdbTitleCleaner.ExtractYear(channel.Name);
                if (year == 0) Int32.TryParse(channel.Year, out year);
                string yearText = year > 0 ? year.ToString(CultureInfo.InvariantCulture) : "";
                string title = TmdbTitleCleaner.CleanTitle(channel.Name);
                var records = Load();
                var matches = records.Where(record => LetterboxdActivity.ActivityKey(record.Title, "") == LetterboxdActivity.ActivityKey(title, "")).ToList();
                if (yearText.Length == 0 && matches.Where(record => !String.IsNullOrWhiteSpace(record.Year)).Select(record => record.Year).Distinct().Count() > 1)
                    matches = matches.Where(record => String.IsNullOrWhiteSpace(record.Year)).ToList();
                var item = matches.FirstOrDefault(record =>
                    yearText.Length == 0 || record.Year == yearText || String.IsNullOrWhiteSpace(record.Year));
                if (item == null)
                {
                    item = new LetterboxdActivity { Title = title, Year = yearText };
                    records.Add(item);
                }
                update(item);
                Save(records);
                return item;
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                foreach (string file in new[] { "letterboxd-activity.json", "letterboxd-profile.json", "letterboxd-rss.json" })
                {
                    try
                    {
                        string path = StateStore.ScopedFile(file);
                        if (File.Exists(path)) File.Delete(path);
                    }
                    catch { }
                }
                activityCachePath = StateStore.ScopedFile("letterboxd-activity.json");
                activityCache = new List<LetterboxdActivity>();
                rssCachePath = StateStore.ScopedFile("letterboxd-rss.json");
                rssCache = new LetterboxdRssCache();
            }
        }

        static void Save(IEnumerable<LetterboxdActivity> records)
        {
            try
            {
                AppPaths.EnsureDataDir();
                var serializer = new DataContractJsonSerializer(typeof(List<LetterboxdActivity>));
                activityCachePath = StateStore.ScopedFile("letterboxd-activity.json");
                activityCache = records.OrderBy(item => item.Title).ThenBy(item => item.Year).ToList();
                using (var stream = File.Create(activityCachePath)) serializer.WriteObject(stream, activityCache);
            }
            catch { }
        }

        static IEnumerable<Dictionary<string, string>> ReadCsv(string path)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                var rows = ParseCsv(reader.ReadToEnd());
                if (rows.Count == 0) yield break;
                var headers = rows[0];
                for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
                {
                    var values = rows[rowIndex];
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int index = 0; index < headers.Count; index++)
                        row[headers[index].Trim()] = index < values.Count ? values[index] : "";
                    yield return row;
                }
            }
        }

        static List<List<string>> ParseCsv(string content)
        {
            var rows = new List<List<string>>();
            var values = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;
            string text = content ?? "";
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if (current == '"')
                {
                    if (quoted && index + 1 < text.Length && text[index + 1] == '"') { value.Append('"'); index++; }
                    else quoted = !quoted;
                }
                else if (current == ',' && !quoted) { values.Add(value.ToString()); value.Length = 0; }
                else if ((current == '\r' || current == '\n') && !quoted)
                {
                    values.Add(value.ToString()); value.Length = 0;
                    rows.Add(values); values = new List<string>();
                    if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                }
                else value.Append(current);
            }
            if (value.Length > 0 || values.Count > 0)
            {
                values.Add(value.ToString());
                rows.Add(values);
            }
            return rows;
        }

        static string Value(Dictionary<string, string> row, params string[] names)
        {
            foreach (string name in names)
            {
                string value;
                if (row.TryGetValue(name, out value) && !String.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return "";
        }

        static bool IsLetterboxdUri(string value)
        {
            Uri uri;
            if (!System.Uri.TryCreate(value, UriKind.Absolute, out uri)) return false;
            return String.Equals(uri.Host, "letterboxd.com", StringComparison.OrdinalIgnoreCase)
                || String.Equals(uri.Host, "www.letterboxd.com", StringComparison.OrdinalIgnoreCase)
                || String.Equals(uri.Host, "boxd.it", StringComparison.OrdinalIgnoreCase);
        }
    }
}
