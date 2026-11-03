using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LyaranTV
{
    /// <summary>
    /// Historique des recherches : les dernières requêtes validées, de la plus
    /// récente à la plus ancienne. Il vit dans un petit fichier texte du profil
    /// de données et peut être effacé depuis la page de recherche.
    /// </summary>
    internal static class SearchHistoryStore
    {
        const int Limit = 12;

        static string FilePath { get { return AppPaths.File("search-history.txt"); } }

        public static List<string> Load()
        {
            var list = new List<string>();
            try
            {
                if (!File.Exists(FilePath)) return list;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string value = (raw ?? "").Trim();
                    if (value.Length == 0) continue;
                    if (list.Any(x => String.Equals(x, value, StringComparison.OrdinalIgnoreCase))) continue;
                    list.Add(value);
                    if (list.Count >= Limit) break;
                }
            }
            catch { }
            return list;
        }

        public static void Add(string query)
        {
            string value = (query ?? "").Trim();
            if (value.Length == 0) return;
            var list = Load();
            list.RemoveAll(x => String.Equals(x, value, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, value);
            while (list.Count > Limit) list.RemoveAt(list.Count - 1);
            Save(list);
        }

        public static void Clear()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }

        static void Save(List<string> list)
        {
            try
            {
                var clean = (list ?? new List<string>())
                    .Where(x => !String.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Take(Limit)
                    .ToArray();
                File.WriteAllLines(FilePath, clean, new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}
