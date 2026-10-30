using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace LyaranTV
{
    /// <summary>
    /// Traduction de l'interface. Les textes du code restent écrits en français :
    /// ils servent de clés. La table associe chaque texte français à sa version
    /// étrangère, ce qui évite de renommer des milliers de chaînes existantes et
    /// laisse le programme parfaitement lisible quand une traduction manque.
    /// L'application peut relire tout l'arbre visuel d'une fenêtre pour traduire
    /// les libellés déjà construits.
    /// </summary>
    internal static class I18n
    {
        public const string SystemDefault = "auto";

        static readonly string[] codes = { "fr", "en", "es", "ru", "pt" };
        static readonly Dictionary<string, string> table = new Dictionary<string, string>(StringComparer.Ordinal);
        // Beaucoup de libellés sont affichés en capitales (« CATALOGUE ») : la
        // recherche insensible à la casse évite de dupliquer chaque entrée.
        static readonly Dictionary<string, string> folded = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly HashSet<string> known = new HashSet<string>(StringComparer.Ordinal);
        static string code = "fr";

        public static string[] Codes { get { return codes; } }

        public static string Code { get { return code; } }

        public static bool IsTranslated { get { return code != "fr" && table.Count > 0; } }

        /// <summary>Langue de l'ordinateur, restreinte aux cinq langues prises en charge.</summary>
        public static string Detect()
        {
            try
            {
                string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                if (two == "fr" || two == "en" || two == "es" || two == "ru" || two == "pt") return two;
                // Une culture régionale non listée (ca-ES, gl-ES…) : on ne devine pas.
            }
            catch { }
            return "en";
        }

        public static string Normalize(string value)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            if (v.Length == 0 || v == SystemDefault) return Detect();
            if (v == "fr" || v == "en" || v == "es" || v == "ru" || v == "pt") return v;
            return Detect();
        }

        /// <summary>Nom de la langue dans sa propre écriture, pour le sélecteur.</summary>
        public static string Name(string value)
        {
            switch (Normalize(value))
            {
                case "fr": return "Français";
                case "es": return "Español";
                case "ru": return "Русский";
                case "pt": return "Português";
                default: return "English";
            }
        }

        public static string EnglishName(string value)
        {
            switch (Normalize(value))
            {
                case "fr": return "French";
                case "es": return "Spanish";
                case "ru": return "Russian";
                case "pt": return "Portuguese";
                default: return "English";
            }
        }

        public static void Set(string value)
        {
            string next = Normalize(value);
            if (next == code && table.Count > 0) return;
            code = next;
            table.Clear();
            switch (code)
            {
                case "en": Tables.English(table); break;
                case "es": Tables.Spanish(table); break;
                case "ru": Tables.Russian(table); break;
                case "pt": Tables.Portuguese(table); break;
            }
            folded.Clear();
            known.Clear();
            foreach (var pair in table)
            {
                if (!folded.ContainsKey(pair.Key.ToLowerInvariant())) folded[pair.Key.ToLowerInvariant()] = pair.Value;
                known.Add(pair.Value);
            }
        }

        /// <summary>Traduit un texte français ; renvoie l'original si inconnu.</summary>
        public static string T(string source)
        {
            if (String.IsNullOrEmpty(source) || table.Count == 0) return source;
            string value;
            if (table.TryGetValue(source, out value)) return value;
            string trimmed = source.Trim();
            if (table.TryGetValue(trimmed, out value)) return value;
            if (folded.TryGetValue(trimmed.ToLowerInvariant(), out value)) return value;
            return source;
        }

        /// <summary>Traduit un libellé qui contient une valeur ajoutée après un séparateur.</summary>
        public static string TPrefix(string source)
        {
            if (String.IsNullOrEmpty(source) || table.Count == 0) return source;
            string exact = T(source);
            if (!String.Equals(exact, source, StringComparison.Ordinal)) return exact;
            foreach (var pair in table)
            {
                if (pair.Key.Length < 4 || !source.StartsWith(pair.Key, StringComparison.Ordinal)) continue;
                char next = source[pair.Key.Length];
                if (next != ' ' && next != ':' && next != '·' && next != '/' && next != '(') continue;
                return pair.Value + source.Substring(pair.Key.Length);
            }
            return source;
        }

        /// <summary>Traduit l'arbre visuel d'une fenêtre, une fois construite.</summary>
        public static void Apply(DependencyObject root)
        {
            if (root == null || !IsTranslated) return;
            var pending = new Stack<DependencyObject>();
            pending.Push(root);
            int guard = 0;
            while (pending.Count > 0 && guard++ < 20000)
            {
                DependencyObject node = pending.Pop();
                TranslateNode(node);
                foreach (object child in LogicalTreeHelper.GetChildren(node))
                {
                    var element = child as DependencyObject;
                    if (element != null) pending.Push(element);
                }
            }
        }

        static void TranslateNode(DependencyObject node)
        {
            try
            {
                var window = node as Window;
                if (window != null && !String.IsNullOrEmpty(window.Title)) window.Title = TPrefix(window.Title);

                var text = node as TextBlock;
                if (text != null && !String.IsNullOrEmpty(text.Text)) { text.Text = TPrefix(text.Text); return; }

                var tooltip = node as ToolTip;
                if (tooltip != null && !String.IsNullOrEmpty(tooltip.Content as string)) { tooltip.Content = TPrefix((string)tooltip.Content); return; }

                var element = node as FrameworkElement;
                if (element != null)
                {
                    string tip = element.ToolTip as string;
                    if (!String.IsNullOrEmpty(tip)) element.ToolTip = TPrefix(tip);
                }

                var header = node as HeaderedContentControl;
                if (header != null)
                {
                    string value = header.Header as string;
                    if (!String.IsNullOrEmpty(value)) header.Header = TPrefix(value);
                }
                var headerItems = node as HeaderedItemsControl;
                if (headerItems != null)
                {
                    string value = headerItems.Header as string;
                    if (!String.IsNullOrEmpty(value)) headerItems.Header = TPrefix(value);
                }

                var content = node as ContentControl;
                if (content != null)
                {
                    string value = content.Content as string;
                    if (!String.IsNullOrEmpty(value)) content.Content = TPrefix(value);
                }
            }
            catch { }
        }

        /// <summary>
        /// Traduit automatiquement chaque fenêtre à son ouverture, y compris les
        /// dialogues créés ailleurs dans le code.
        /// </summary>
        public static void Install()
        {
            try
            {
                EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                    new RoutedEventHandler(delegate(object sender, RoutedEventArgs e)
                    {
                        var window = sender as Window;
                        if (window != null) Apply(window);
                    }));
                // Beaucoup d'éléments (bannières, cartes, listes) sont créés après
                // l'affichage de la page : on les traduit à l'instant où ils
                // entrent dans l'arbre visuel, sinon ils resteraient en français.
                EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.LoadedEvent,
                    new RoutedEventHandler(delegate(object sender, RoutedEventArgs e)
                    {
                        var text = sender as TextBlock;
                        if (text != null) TranslateNode(text);
                    }));
                EventManager.RegisterClassHandler(typeof(ContentControl), FrameworkElement.LoadedEvent,
                    new RoutedEventHandler(delegate(object sender, RoutedEventArgs e)
                    {
                        var content = sender as ContentControl;
                        if (content != null) TranslateNode(content);
                    }));
            }
            catch { }
        }
    }
}
