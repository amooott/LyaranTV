using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LyaranTV
{
    /// <summary>
    /// Sélecteur de langue, posé en bas de la barre latérale. Le libellé reste
    /// compréhensible même si l'interface est dans une écriture qu'on ne lit pas :
    /// l'icône « langue » et le nom de chaque langue dans sa propre écriture
    /// (Français, English, Español, Русский, Português) servent de repère.
    /// </summary>
    public sealed partial class MainWindow
    {
        static readonly string[] LanguageOrder = { I18n.SystemDefault, "fr", "en", "es", "ru", "pt" };

        void AddLanguageSidebarItem(StackPanel host)
        {
            var button = LanguageRow(I18n.Name(settings.Language), I18n.T("Langue de l'interface"), false, delegate { OpenLanguageDialog(); });
            button.Margin = new Thickness(6, 2, 6, 2);
            button.ToolTip = I18n.T("Langue de l'interface") + " · " + I18n.T("Langue");
            host.Children.Add(button);
        }

        Button LanguageRow(string title, string subtitle, bool active, Action click)
        {
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = Icon(Glyphs.Language, 15, active ? Brush(Accent) : Brush(Muted));
            icon.Margin = new Thickness(4, 0, 11, 0);
            icon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(icon, 0); grid.Children.Add(icon);

            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(Text(title, 12.5, Brush(TextPrimary), active));
            if (!String.IsNullOrEmpty(subtitle)) labels.Children.Add(Text(subtitle, 9.5, Brush(Muted), false));
            Grid.SetColumn(labels, 1); grid.Children.Add(labels);

            if (active)
            {
                var check = Icon(Glyphs.Check, 13, Brush(Accent));
                check.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(check, 2); grid.Children.Add(check);
            }

            var button = new Button
            {
                Content = grid, Height = String.IsNullOrEmpty(subtitle) ? 40 : 46,
                Padding = new Thickness(10, 0, 10, 0), Background = active ? AccentSoftBrush : Brushes.Transparent,
                BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.Hand, FocusVisualStyle = null, Template = RoundedButtonTemplate(10)
            };
            if (!active)
            {
                button.MouseEnter += delegate { button.Background = HoverBrush; };
                button.MouseLeave += delegate { button.Background = Brushes.Transparent; };
            }
            button.Click += delegate { if (click != null) click(); };
            return button;
        }

        void OpenLanguageDialog()
        {
            var window = new Window
            {
                Title = I18n.T("Langue"),
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Height,
                Width = 470,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                Background = Brush(Panel),
                ShowInTaskbar = false
            };

            var root = new StackPanel { Margin = new Thickness(20, 18, 20, 16) };
            var title = Text(I18n.T("Langue de l'interface"), 17, Brush(TextPrimary), true);
            root.Children.Add(title);
            var hint = Text("Choisissez la langue des menus, des réglages et du lecteur. La langue de l'ordinateur est proposée par défaut.", 11, Brush(Muted), false);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 6, 0, 12);
            root.Children.Add(hint);

            string current = I18n.Normalize(settings.Language);
            bool followsSystem = String.Equals((settings.Language ?? "").Trim(), I18n.SystemDefault, StringComparison.OrdinalIgnoreCase);

            foreach (string code in LanguageOrder)
            {
                bool isSystem = code == I18n.SystemDefault;
                string value = isSystem ? I18n.Detect() : code;
                bool active = isSystem ? followsSystem : (!followsSystem && current == value);
                string subtitle = isSystem
                    ? I18n.T("Langue de l'ordinateur") + " · " + I18n.Name(I18n.Detect())
                    : I18n.Name(value);
                string title2 = isSystem ? I18n.T("Automatique") : I18n.Name(value);
                string target = isSystem ? I18n.SystemDefault : value;
                var row = LanguageRow(title2, subtitle, active, delegate
                {
                    window.Close();
                    ApplyLanguage(target);
                });
                row.Margin = new Thickness(0, 2, 0, 2);
                root.Children.Add(row);
            }

            var close = Button(I18n.T("Fermer"), delegate { window.Close(); }, false);
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 12, 0, 0);
            root.Children.Add(close);

            window.Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            window.ShowDialog();
        }

        /// <summary>Applique la langue immédiatement, sans redémarrer.</summary>
        internal void ApplyLanguage(string code)
        {
            string target = String.IsNullOrWhiteSpace(code) ? I18n.SystemDefault : code.Trim().ToLowerInvariant();
            settings.Language = target;
            settings.Normalize();
            SettingsStore.Save(settings);
            I18n.Set(settings.Language);

            RebuildSidebar();
            UpdateStatusBar();
            I18n.Apply(this);

            // Reconstruire la page en cours traduit aussi ses libellés fixes. On
            // évite la page de lecture : elle relancerait le flux en cours.
            if (page == "Lecture" || page == "Épisodes")
            {
                ShowToast("Langue · " + I18n.Name(settings.Language), false, true);
                return;
            }
            Nav(page);
            ShowToast("Langue · " + I18n.Name(settings.Language), false, true);
        }
    }
}
