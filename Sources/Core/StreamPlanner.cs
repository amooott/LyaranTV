using System;
using System.Collections.Generic;

namespace LyaranTV
{
    /// <summary>
    /// Choisit l'ordre des liens de lecture pour le lecteur intégré.
    /// MediaElement décode bien le MPEG-TS, le MKV et le MP4 progressifs, mais pas
    /// le HLS : les liens .m3u8 sont donc essayés en dernier. Ils restent disponibles
    /// pour le lecteur externe et comme repli.
    /// </summary>
    internal static class StreamPlanner
    {
        public static List<string> Candidates(Channel channel)
        {
            var list = new List<string>();
            if (channel == null) return list;
            if (!String.IsNullOrWhiteSpace(channel.Url)) list.Add(channel.Url.Trim());
            if (!String.IsNullOrWhiteSpace(channel.AlternateUrl) && !list.Contains(channel.AlternateUrl.Trim()))
                list.Add(channel.AlternateUrl.Trim());
            if (list.Count < 2) return list;

            var ordered = new List<string>();
            foreach (string url in list) if (!IsHls(url)) ordered.Add(url);
            foreach (string url in list) if (IsHls(url)) ordered.Add(url);
            return ordered;
        }

        public static bool IsHls(string url)
        {
            if (String.IsNullOrWhiteSpace(url)) return false;
            string path = url;
            try { path = new Uri(url).AbsolutePath; } catch { }
            return path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
                || url.IndexOf(".m3u8", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
