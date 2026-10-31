using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace LyaranTV
{
    public sealed partial class MainWindow
    {
        DispatcherTimer libraryHeroTimer;
        List<Channel> libraryHeroPool = new List<Channel>();
        int libraryHeroIndex;
        DateTime libraryHeroHoldUntil = DateTime.MinValue;
        AsyncImage libraryHeroImage, libraryHeroLogo;
        TextBlock libraryHeroGlyph, libraryHeroKicker, libraryHeroTitle, libraryHeroMeta, libraryHeroDescription;
        StackPanel libraryHeroContent, libraryHeroActions, libraryHeroDots;
        Channel libraryHeroChannel;
        int libraryLoadGeneration;
        // « Tout parcourir » impose la grille sans modifier la préférence
        // liste/grille enregistrée par l'utilisateur.
        bool collectionGrid;

        void ShowLibrary(string mode)
        {
            page = mode;
            // La présentation enregistrée fait foi dès qu'on revient sur une
            // page de découverte : « Tout parcourir » peut forcer la grille
            // sans modifier les préférences de l'utilisateur.
            collectionGrid = false;
            gridView = settings.GridView;
            StopLibraryHeroTimer();
            RebuildSidebar(); UpdateStatusBar(); SetPageStatus();
            channelList = null; resultsScroll = null; moreHost = null;
            currentView = null;

            if (activeSource != null && channels.Count >= 4000)
            {
                // Gros catalogue : on affiche tout de suite une page de chargement
                // et on prépare les listes hors du fil d'interface pour que la
                // fenêtre reste réactive pendant l'indexation.
                int generation = ++libraryLoadGeneration;
                var worker = new BackgroundWorker();
                List<Channel> snapshot = new List<Channel>(channels);
                string kind = activeKind, group = activeGroup, query = searchQuery, sort = sortMode;
                List<Channel> modeBase = null, filtered = null;
                long dataMs = 0;
                // La page de chargement ne s'affiche que si la préparation
                // dépasse un court instant : sur un petit compte, la
                // bibliothèque s'ouvre directement, sans flash.
                var loadingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                loadingTimer.Tick += delegate
                {
                    loadingTimer.Stop();
                    if (appClosing || generation != libraryLoadGeneration || page != mode) return;
                    ShowLibraryLoading(mode);
                };
                loadingTimer.Start();
                worker.DoWork += delegate
                {
                    var watch = Stopwatch.StartNew();
                    modeBase = BaseViewForMode(mode, snapshot).ToList();
                    filtered = LibraryFilteredView(modeBase, kind, group, query, sort);
                    dataMs = watch.ElapsedMilliseconds;
                };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    loadingTimer.Stop();
                    if (appClosing || generation != libraryLoadGeneration || page != mode) return;
                    if (e.Error != null || modeBase == null || filtered == null)
                    {
                        try
                        {
                            HttpUtil.AppendLog("Bibliothèque : " + (e.Error == null ? "préparation vide" : e.Error.GetBaseException().Message));
                        }
                        catch { }
                        // Repli direct : mieux vaut une page un peu lente qu'une page vide.
                        modeBase = BaseViewForMode(mode).ToList();
                        filtered = LibraryFilteredView(modeBase);
                        dataMs = 0;
                    }
                    BuildLibraryPage(mode, modeBase, filtered, dataMs);
                    SetPageStatus();
                };
                worker.RunWorkerAsync();
                return;
            }

            var syncBase = activeSource == null ? new List<Channel>() : BaseViewForMode(mode).ToList();
            var syncFiltered = activeSource == null ? new List<Channel>() : LibraryFilteredView(syncBase);
            BuildLibraryPage(mode, syncBase, syncFiltered, 0);
            SetPageStatus();
        }

        void ShowLibraryLoading(string mode)
        {
            currentView = null;
            string title = LibraryTitleForMode(mode);
            string subtitle = activeSource == null
                ? "Aucune playlist active"
                : activeSource.Name;

            var root = new StackPanel { Margin = new Thickness(28, 22, 28, 38) };
            root.Children.Add(BuildLibraryHeader(mode, title, subtitle));

            var card = new Border
            {
                MaxWidth = 560,
                Padding = new Thickness(34, 30, 34, 32),
                CornerRadius = new CornerRadius(20),
                Background = Brush(Panel),
                BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 34, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var stack = new StackPanel();
            var spinner = CreateSpinner(46);
            spinner.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(spinner);

            var heading = Text("Chargement de la bibliothèque…", 18, Brush(TextPrimary), true);
            heading.HorizontalAlignment = HorizontalAlignment.Center;
            heading.Margin = new Thickness(0, 18, 0, 6);
            stack.Children.Add(heading);

            string detailText = String.IsNullOrWhiteSpace(searchQuery)
                ? "Indexation de " + TextUtil.HumanCount(channels.Count) + " éléments et préparation des catégories."
                : "Recherche de « " + searchQuery.Trim() + " » dans " + TextUtil.HumanCount(channels.Count) + " éléments.";
            var detail = Text(detailText, 11.5, Brush(Muted), false);
            detail.TextAlignment = TextAlignment.Center;
            detail.TextWrapping = TextWrapping.Wrap;
            detail.MaxWidth = 440;
            detail.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(detail);

            var hint = Text("La page s'ouvrira automatiquement dès qu'elle est prête.", 10.5, Brush(Muted), false);
            hint.TextAlignment = TextAlignment.Center;
            hint.Margin = new Thickness(0, 10, 0, 0);
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(hint);

            card.Child = stack;
            root.Children.Add(card);
            SetContent(root, false);
            SetStatus("Chargement de la bibliothèque…");
        }

        void BuildLibraryPage(string mode, List<Channel> modeBase, List<Channel> filtered, long dataMs)
        {
            var libraryPerf = Stopwatch.StartNew();
            currentView = filtered;
            // Les jaquettes des étagères partent avant la construction du hero
            // et des cartes : elles ont ainsi le temps d'arriver en mémoire.
            WarmLibraryCovers(filtered);

            string title = LibraryTitleForMode(mode);
            string subtitle = activeSource == null ? "Aucune playlist active" : activeSource.Name;
            var root = new StackPanel { Margin = new Thickness(28, 22, 28, 38) };
            root.Children.Add(BuildLibraryHeader(mode, title, subtitle));
            long headerMs = libraryPerf.ElapsedMilliseconds;

            if (activeSource == null || channels.Count == 0)
            {
                var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                actions.Children.Add(Button(Glyphs.Add, "Ajouter une playlist", AddPlaylist, true));
                actions.Children.Add(Button(Glyphs.Play, "Essayer la démo", LoadDemo, false));
                root.Children.Add(EmptyState(Glyphs.Library, "Votre bibliothèque est vide", "Ajoutez une playlist pour retrouver vos chaînes, films et séries dans un espace pensé pour le canapé.", null));
                root.Children.Add(actions);
                SetContent(root);
                return;
            }

            if (modeBase.Count == 0)
            {
                bool favoritesMode = mode == "Favoris";
                var discover = Button(Glyphs.Library, "Parcourir la bibliothèque", delegate { Nav("Bibliothèque"); }, true);
                root.Children.Add(EmptyState(favoritesMode ? Glyphs.Favorite : Glyphs.Recent,
                    favoritesMode ? "Aucun favori pour le moment" : "Aucun contenu récent",
                    favoritesMode ? "Ajoutez des films, séries ou chaînes à vos favoris depuis la bibliothèque ou une fiche." : "Les contenus que vous ouvrez apparaîtront ici pour reprendre la lecture en un survol.",
                    discover));
                SetContent(root);
                return;
            }

            if (filtered.Count == 0)
            {
                root.Children.Add(BuildLibraryFilters(mode, modeBase));
                var reset = Button(Glyphs.Refresh, "Tout réafficher", delegate
                {
                    ClearSearch(); activeKind = "Tous"; activeGroup = AllGroups; ShowLibrary(mode);
                }, true);
                root.Children.Add(EmptyState(Glyphs.Filter, "Aucun contenu avec ces filtres", "Changez de catégorie ou réinitialisez les filtres pour retrouver votre bibliothèque.", reset));
                SetContent(root);
                return;
            }

            root.Children.Add(BuildLibraryFilters(mode, modeBase));
            long filtersMs = libraryPerf.ElapsedMilliseconds;

            if (gridView)
            {
                // Mode grille : la sélection courante s'affiche d'un bloc, sans
                // hero ni étagères. La bascule liste/grille des filtres change
                // donc réellement la page, et « Afficher plus » complète la
                // liste par tranches sans recharger le reste.
                channelList = new StackPanel();
                resultsScroll = null;
                moreHost = null;
                resultCountText = Text("", 12, Brush(Muted), false);
                resultCountText.Margin = new Thickness(0, 0, 0, 12);
                root.Children.Add(resultCountText);
                root.Children.Add(channelList);
                RenderChannels();
                SetContent(root);
                try
                {
                    HttpUtil.AppendLog("Perf bibliothèque (grille) @" + DateTime.Now.ToString("HH:mm:ss.fff")
                        + " · préparation " + dataMs + " · filtres " + (filtersMs - headerMs)
                        + " · rendu " + libraryPerf.ElapsedMilliseconds + " ms");
                }
                catch { }
                return;
            }

            root.Children.Add(BuildLibraryHero());
            long heroMs = libraryPerf.ElapsedMilliseconds;
            AddLibraryShelves(root, filtered, modeBase, mode);
            long shelvesMs = libraryPerf.ElapsedMilliseconds;

            BuildLibraryHeroPool(filtered);
            if (libraryHeroPool.Count > 0)
            {
                int index = libraryHeroIndex;
                if (index < 0 || index >= libraryHeroPool.Count) index = 0;
                libraryHeroIndex = index;
                SetLibraryHero(libraryHeroPool[index], false);
            }

            SetContent(root);
            try
            {
                HttpUtil.AppendLog("Perf bibliothèque @" + DateTime.Now.ToString("HH:mm:ss.fff") + " · préparation " + dataMs
                    + " · en-tête " + headerMs + " · hero " + (heroMs - headerMs)
                    + " · filtres " + (filtersMs - heroMs) + " · étagères " + (shelvesMs - filtersMs)
                    + " · pool " + (libraryPerf.ElapsedMilliseconds - shelvesMs) + " · rendu " + libraryPerf.ElapsedMilliseconds
                    + " · total " + (dataMs + libraryPerf.ElapsedMilliseconds) + " ms");
            }
            catch { }
            StartLibraryHeroTimer();
        }

        FrameworkElement BuildLibraryHeader(string mode, string title, string subtitle)
        {
            // En-tête volontairement réduit au titre et à une ligne de contexte :
            // le compteur et l'eyebrow répétaient des informations déjà visibles
            // dans la barre latérale et dans la barre d'état.
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            var heading = Text(title, 28, Brush(TextPrimary), true);
            heading.LineHeight = 33;
            pageTitle = heading;
            header.Children.Add(heading);
            var sub = Text(subtitle, 12.5, Brush(Muted), false);
            sub.Margin = new Thickness(0, 3, 0, 0);
            header.Children.Add(sub);
            return header;
        }

        List<Channel> LibraryFilteredView(List<Channel> modeBase)
        {
            return LibraryFilteredView(modeBase, activeKind, activeGroup, searchQuery, sortMode);
        }

        List<Channel> LibraryFilteredView(List<Channel> modeBase, string kind, string group, string queryValue, string sort)
        {
            IEnumerable<Channel> view = modeBase;
            if (kind != "Tous") view = view.Where(c => c.Kind == kind);
            if (group != AllGroups) view = view.Where(c => String.Equals(c.Group, group, StringComparison.OrdinalIgnoreCase));
            string query = String.IsNullOrWhiteSpace(queryValue) ? "" : queryValue.Trim();
            if (query.Length > 0) view = view.Where(c => c.MatchesSearch(query));
            return SortChannels(view, sort).ToList();
        }

        FrameworkElement BuildLibraryFilters(string mode, List<Channel> modeBase)
        {
            var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 18) };
            var tools = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

            var category = new ComboBox { Width = 182, Height = 33, Style = ModernComboStyle(), Margin = new Thickness(0, 0, 6, 0) };
            category.Items.Add("Toutes les catégories");
            var groups = modeBase
                .Where(c => activeKind == "Tous" || c.Kind == activeKind)
                .GroupBy(c => String.IsNullOrWhiteSpace(c.Group) ? "Non classé" : c.Group, StringComparer.OrdinalIgnoreCase)
                .Where(g => !hiddenGroups.Contains(g.Key))
                .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => g.Key)
                .ToList();
            foreach (string group in groups) category.Items.Add(group);
            if (activeGroup != AllGroups && !groups.Contains(activeGroup, StringComparer.OrdinalIgnoreCase)) category.Items.Add(activeGroup);
            category.SelectedItem = activeGroup == AllGroups ? "Toutes les catégories" : activeGroup;
            category.SelectionChanged += delegate
            {
                if (category.SelectedItem == null) return;
                string value = Convert.ToString(category.SelectedItem, CultureInfo.InvariantCulture);
                string nextGroup = value == "Toutes les catégories" ? AllGroups : value;
                if (String.Equals(activeGroup, nextGroup, StringComparison.OrdinalIgnoreCase)) return;
                activeGroup = nextGroup;
                ShowLibrary(mode);
            };
            tools.Children.Add(category);

            sortPicker = new ComboBox { Width = 150, Height = 33, Style = ModernComboStyle(), Margin = new Thickness(0, 0, 6, 0) };
            foreach (string option in SortOptions()) sortPicker.Items.Add(option);
            sortPicker.SelectedItem = sortMode;
            sortPicker.SelectionChanged += delegate
            {
                if (sortPicker.SelectedItem == null) return;
                string chosen = Convert.ToString(sortPicker.SelectedItem, CultureInfo.InvariantCulture);
                if (chosen == sortMode) return;
                sortMode = chosen; settings.SortMode = chosen; SaveSettings();
                ShowLibrary(mode);
            };
            tools.Children.Add(sortPicker);

            // Bascule liste/grille : le choix est enregistré et reste celui
            // appliqué au retour du lecteur ou d'une fiche de contenu.
            var viewToggle = IconButton(gridView ? Glyphs.List : Glyphs.Grid,
                gridView ? "Afficher en liste" : "Afficher en grille", delegate
                {
                    gridView = !gridView;
                    settings.GridView = gridView;
                    SaveSettings();
                    ShowLibrary(mode);
                }, false);
            viewToggle.Margin = new Thickness(0);
            tools.Children.Add(viewToggle);
            DockPanel.SetDock(tools, Dock.Right);
            bar.Children.Add(tools);

            // Navigation par type en onglets texte soulignés : les compteurs
            // restent disponibles en infobulle au lieu de saturer la barre.
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            AddLibraryKindTab(tabs, "Tout", "Tous", modeBase.Count, mode);
            AddLibraryKindTab(tabs, "Direct", "Chaînes", modeBase.Count(c => c.Kind == "Chaînes"), mode);
            AddLibraryKindTab(tabs, "Films", "Films", modeBase.Count(c => c.Kind == "Films"), mode);
            AddLibraryKindTab(tabs, "Séries", "Séries", modeBase.Count(c => c.Kind == "Séries"), mode);
            if (activeKind != "Tous" || activeGroup != AllGroups)
            {
                var clear = TextAction("Effacer les filtres", delegate
                {
                    activeKind = "Tous"; activeGroup = AllGroups; ShowLibrary(mode);
                }, "Retirer le filtre de type et de catégorie");
                clear.Margin = new Thickness(12, 0, 0, 0);
                tabs.Children.Add(clear);
            }
            bar.Children.Add(tabs);
            return bar;
        }

        void AddLibraryKindTab(Panel panel, string label, string kind, int count, string mode)
        {
            string captured = kind;
            string tooltip = label + "  ·  " + TextUtil.HumanCount(count) + " élément(s)";
            var tab = FilterTab(label, activeKind == captured, delegate
            {
                activeKind = captured; activeGroup = AllGroups;
                ShowLibrary(mode);
            }, tooltip);
            panel.Children.Add(tab);
        }

        FrameworkElement BuildLibraryHero()
        {
            // Le hero reste volontairement cinématographique et sombre dans tous les
            // thèmes : les affiches et les titres y gagnent en contraste.
            Color heroBase = Color.FromRgb(8, 8, 14);
            var visual = new Grid { Height = 364, ClipToBounds = true };
            visual.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(22),
                Background = new LinearGradientBrush(WithAlpha(Accent, 76), heroBase, 28)
            });

            // Halo et cercles dessinés : le hero garde une présence graphique
            // même sans photo ni logo.
            visual.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 640, Height = 640, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -250, -110, 0),
                Fill = new RadialGradientBrush(WithAlpha(Accent, 68), WithAlpha(Accent, 0))
            });
            visual.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 400, Height = 400, Stroke = Brush(WithAlpha(Accent, 46)), StrokeThickness = 1,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 120, 30)
            });

            libraryHeroImage = new AsyncImage { Stretch = Stretch.UniformToFill, Opacity = 0 };
            visual.Children.Add(libraryHeroImage);
            // Signature discrète de l'application, toujours présente derrière l'affiche.
            visual.Children.Add(new Image
            {
                Source = CreateLogoMark(), Width = 290, Height = 260, Stretch = Stretch.Uniform, Opacity = 0.07,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 46, 10), IsHitTestVisible = false
            });

            visual.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(heroBase, 246), 0),
                    new GradientStop(WithAlpha(heroBase, 214), 0.34),
                    new GradientStop(WithAlpha(heroBase, 48), 0.72),
                    new GradientStop(WithAlpha(heroBase, 0), 1)
                }, new Point(0, 0.5), new Point(1, 0.5))
            });
            visual.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.38),
                    new GradientStop(WithAlpha(heroBase, 238), 1)
                }, new Point(0.5, 0), new Point(0.5, 1))
            });

            libraryHeroGlyph = new TextBlock
            {
                FontSize = 46, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Opacity = 0.1,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 48, 18), TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap, MaxWidth = 350, LineHeight = 49
            };
            visual.Children.Add(libraryHeroGlyph);

            libraryHeroLogo = new AsyncImage
            {
                Stretch = Stretch.Uniform, Width = 222, Height = 118, Opacity = 0.94,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                // Sous les flèches du défilé, pour ne jamais les chevaucher.
                Margin = new Thickness(0, 80, 38, 0)
            };
            visual.Children.Add(libraryHeroLogo);

            libraryHeroContent = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(36, 26, 36, 30), MaxWidth = 690
            };

            var kickerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 9) };
            var line = new Border { Width = 20, Height = 2, Background = Brush(Accent), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) };
            kickerRow.Children.Add(line);
            libraryHeroKicker = Text("", 10.5, Brush(Accent), true);
            libraryHeroKicker.TextWrapping = TextWrapping.NoWrap;
            kickerRow.Children.Add(libraryHeroKicker);
            libraryHeroContent.Children.Add(kickerRow);

            libraryHeroTitle = Text("", 35, Brushes.White, true);
            libraryHeroTitle.LineHeight = 39;
            libraryHeroTitle.MaxWidth = 680;
            libraryHeroTitle.Margin = new Thickness(0, 0, 0, 7);
            libraryHeroContent.Children.Add(libraryHeroTitle);

            libraryHeroMeta = Text("", 12, Brush(Color.FromArgb(216, 255, 255, 255)), true);
            libraryHeroMeta.MaxWidth = 660;
            libraryHeroMeta.Margin = new Thickness(0, 0, 0, 8);
            libraryHeroContent.Children.Add(libraryHeroMeta);

            libraryHeroDescription = Text("", 13, Brush(Color.FromArgb(188, 255, 255, 255)), false);
            libraryHeroDescription.MaxWidth = 650;
            libraryHeroDescription.MaxHeight = 48;
            libraryHeroDescription.LineHeight = 19;
            libraryHeroDescription.Margin = new Thickness(0, 0, 0, 15);
            libraryHeroContent.Children.Add(libraryHeroDescription);

            libraryHeroActions = new StackPanel { Orientation = Orientation.Horizontal };
            libraryHeroContent.Children.Add(libraryHeroActions);

            visual.Children.Add(libraryHeroContent);

            libraryHeroDots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 34, 28) };
            visual.Children.Add(libraryHeroDots);

            // Flèches du défilé de la bibliothèque : mêmes gestes que sur l'accueil.
            var previous = HeroArrow(Glyphs.ChevronLeft, false);
            previous.HorizontalAlignment = HorizontalAlignment.Right;
            previous.VerticalAlignment = VerticalAlignment.Top;
            previous.Margin = new Thickness(0, 24, 66, 0);
            previous.Click += delegate { StepLibraryHero(-1); };
            System.Windows.Controls.Panel.SetZIndex(previous, 40);
            visual.Children.Add(previous);
            var next = HeroArrow(Glyphs.ChevronRight, true);
            next.HorizontalAlignment = HorizontalAlignment.Right;
            next.VerticalAlignment = VerticalAlignment.Top;
            next.Margin = new Thickness(0, 24, 16, 0);
            next.Click += delegate { StepLibraryHero(1); };
            System.Windows.Controls.Panel.SetZIndex(next, 40);
            visual.Children.Add(next);

            var heroBorder = new Border
            {
                CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(1),
                BorderBrush = Brush(WithAlpha(Accent, 64)), ClipToBounds = true, Child = visual,
                Margin = new Thickness(0, 0, 0, 20)
            };
            RoundClip(heroBorder, 22);
            heroBorder.MouseEnter += delegate { libraryHeroHoldUntil = DateTime.UtcNow.AddSeconds(30); };
            heroBorder.MouseLeave += delegate { libraryHeroHoldUntil = DateTime.UtcNow.AddSeconds(4); };
            return heroBorder;
        }

        /// <summary>Flèche du défilé de la bibliothèque.</summary>
        Button HeroArrow(string glyph, bool forward)
        {
            var button = new Button
            {
                Content = Icon(glyph, 17, Brushes.White), Width = 40, Height = 40, Padding = new Thickness(0),
                Background = Brush(Color.FromArgb(150, 8, 9, 13)),
                BorderBrush = Brush(Color.FromArgb(76, 255, 255, 255)), BorderThickness = new Thickness(1),
                Template = RoundedButtonTemplate(20), Cursor = Cursors.Hand, FocusVisualStyle = null,
                IsTabStop = false, Opacity = 0.62, ToolTip = forward ? "Contenu suivant" : "Contenu précédent"
            };
            button.MouseEnter += delegate
            {
                UiMotion.Animate(button, UIElement.OpacityProperty, 1, 120);
                button.Background = Brush(Color.FromArgb(214, 18, 19, 26));
            };
            button.MouseLeave += delegate
            {
                UiMotion.Animate(button, UIElement.OpacityProperty, 0.62, 160);
                button.Background = Brush(Color.FromArgb(150, 8, 9, 13));
            };
            return button;
        }

        /// <summary>Passe au contenu suivant ou précédent du défilé de la bibliothèque.</summary>
        void StepLibraryHero(int delta)
        {
            if (libraryHeroPool.Count == 0 || delta == 0) return;
            int next = libraryHeroIndex + delta;
            if (next < 0) next = libraryHeroPool.Count - 1;
            if (next >= libraryHeroPool.Count) next = 0;
            SetLibraryHero(libraryHeroPool[next], true);
        }

        void BuildLibraryHeroPool(List<Channel> view)
        {
            libraryHeroPool.Clear();
            libraryHeroIndex = 0;
            if (view == null || view.Count == 0) { UpdateLibraryHeroDots(); return; }
            var ranked = view.Select(c => new { Channel = c, Score = LibraryHeroScore(c) })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Channel.Order)
                .Take(9)
                .ToList();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in ranked)
            {
                if (!seen.Add(item.Channel.Id)) continue;
                libraryHeroPool.Add(item.Channel);
            }
            var last = LastPlayedChannel();
            if (last != null)
            {
                int index = libraryHeroPool.FindIndex(c => String.Equals(c.Id, last.Id, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) libraryHeroIndex = index;
            }
            UpdateLibraryHeroDots();
        }

        int LibraryHeroScore(Channel channel)
        {
            int score = 0;
            if (favorites.Contains(channel.Id)) score += 7;
            if (recentIds.Contains(channel.Id)) score += 4;
            if (!String.IsNullOrWhiteSpace(channel.BackdropUrl)) score += 5;
            if (!String.IsNullOrWhiteSpace(channel.PosterUrl)) score += 3;
            if (!String.IsNullOrWhiteSpace(channel.Logo)) score += 1;
            if (channel.TmdbRating >= 7) score += 2;
            if (channel.Kind == "Films" || channel.Kind == "Séries") score += 1;
            return score;
        }

        void SetLibraryHero(Channel channel, bool userDriven)
        {
            if (channel == null || libraryHeroTitle == null) return;
            libraryHeroChannel = channel;
            libraryHeroHoldUntil = userDriven ? DateTime.UtcNow.AddSeconds(14) : DateTime.UtcNow;

            int index = libraryHeroPool.FindIndex(c => String.Equals(c.Id, channel.Id, StringComparison.OrdinalIgnoreCase));
            if (index < 0 && userDriven)
            {
                libraryHeroPool.Insert(0, channel);
                while (libraryHeroPool.Count > 10) libraryHeroPool.RemoveAt(libraryHeroPool.Count - 1);
                index = 0;
            }
            libraryHeroIndex = index;

            string kicker = channel.Kind == "Chaînes" ? "EN DIRECT" : channel.Kind == "Films" ? "FILM" : channel.Kind == "Séries" ? "SÉRIE" : "MÉDIA";
            libraryHeroKicker.Text = kicker;
            libraryHeroTitle.Text = TextUtil.DisplayTitle(channel.Name);

            if (channel.Kind == "Chaînes")
            {
                var now = CurrentProgram(channel);
                string meta = now != null
                    ? now.Title + "  ·  " + now.TimeRange
                    : "En direct" + (String.IsNullOrWhiteSpace(channel.Group) ? "" : "  ·  " + channel.Group);
                libraryHeroMeta.Text = TextUtil.Ellipsis(meta, 120);
                string description = now != null ? now.Description : channel.Description;
                if (String.IsNullOrWhiteSpace(description)) description = "Regardez cette chaîne en direct dans le lecteur intégré.";
                libraryHeroDescription.Text = TextUtil.Ellipsis(description, 220);
            }
            else
            {
                var parts = new List<string>();
                if (!String.IsNullOrWhiteSpace(channel.Year)) parts.Add(channel.Year);
                if (channel.TmdbRating > 0) parts.Add("★ " + channel.TmdbRating.ToString("0.0", CultureInfo.InvariantCulture));
                if (!String.IsNullOrWhiteSpace(channel.Runtime)) parts.Add(channel.Runtime);
                if (parts.Count == 0) parts.Add(KindLabel(channel.Kind));
                libraryHeroMeta.Text = String.Join("  ·  ", parts.ToArray());
                string description = String.IsNullOrWhiteSpace(channel.Overview) ? channel.Description : channel.Overview;
                if (String.IsNullOrWhiteSpace(description)) description = "Aucun synopsis n’est disponible pour ce contenu.";
                libraryHeroDescription.Text = TextUtil.Ellipsis(description, 240);
            }

            // Bannière authentique : backdrop, sinon capture d'épisode, sinon
            // l'affiche recadrée en bandeau plutôt qu'un simple fond flou.
            string imageUrl = !String.IsNullOrWhiteSpace(channel.BackdropUrl) ? channel.BackdropUrl
                : !String.IsNullOrWhiteSpace(channel.StillUrl) ? channel.StillUrl : channel.PosterUrl;
            bool hasImage = !String.IsNullOrWhiteSpace(imageUrl);
            libraryHeroImage.Url = imageUrl;
            libraryHeroImage.Effect = null;
            libraryHeroImage.Stretch = Stretch.UniformToFill;
            libraryHeroImage.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
            if (hasImage)
            {
                libraryHeroImage.Opacity = 0.55;
                UiMotion.Animate(libraryHeroImage, UIElement.OpacityProperty, 0.9, 280);
            }
            libraryHeroLogo.Url = hasImage ? null : channel.Logo;
            libraryHeroLogo.Visibility = !hasImage && !String.IsNullOrWhiteSpace(channel.Logo) ? Visibility.Visible : Visibility.Collapsed;
            libraryHeroGlyph.Text = TextUtil.Ellipsis(TextUtil.DisplayTitle(channel.Name).ToUpperInvariant(), 34);
            libraryHeroGlyph.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;

            libraryHeroActions.Children.Clear();
            double saved = GetSavedPosition(channel);
            bool series = channel.IsSeries || (channel.Kind == "Séries" && String.IsNullOrWhiteSpace(channel.Url));
            bool needsInfo = series || (channel.Kind == "Films" && String.IsNullOrWhiteSpace(channel.Url));
            string primaryLabel = series ? "Voir les épisodes" : needsInfo ? "Voir la fiche" : saved > 0 ? "Reprendre  ·  " + TextUtil.FormatPosition(TimeSpan.FromSeconds(saved)) : "Regarder";
            Channel captured = channel;
            double capturedSaved = saved;
            bool capturedNeedsInfo = needsInfo;
            var primary = Button(Glyphs.Play, primaryLabel, delegate
            {
                if (capturedNeedsInfo) ShowChannelInfo(captured);
                else Play(captured, currentView, capturedSaved, false);
            }, true);
            primary.Height = 42;
            primary.Padding = new Thickness(18, 4, 18, 4);
            primary.Margin = new Thickness(0, 0, 9, 0);
            libraryHeroActions.Children.Add(primary);

            if (!needsInfo && (channel.Kind == "Films" || channel.Kind == "Séries"))
            {
                var details = Button(Glyphs.Info, "Détails", delegate { ShowChannelInfo(captured); }, false);
                details.Height = 42;
                details.Margin = new Thickness(0, 0, 9, 0);
                libraryHeroActions.Children.Add(details);
            }

            var favorite = IconButton(favorites.Contains(channel.Id) ? Glyphs.FavoriteFill : Glyphs.Favorite,
                favorites.Contains(channel.Id) ? "Retirer des favoris" : "Ajouter aux favoris",
                delegate { ToggleFavorite(captured); SetLibraryHero(captured, true); }, false);
            favorite.Width = 42; favorite.Height = 42; favorite.Margin = new Thickness(0);
            libraryHeroActions.Children.Add(favorite);

            UpdateLibraryHeroDots();
            libraryHeroContent.Opacity = 0.3;
            UiMotion.Animate(libraryHeroContent, UIElement.OpacityProperty, 1, 260);
        }

        void UpdateLibraryHeroDots()
        {
            if (libraryHeroDots == null) return;
            libraryHeroDots.Children.Clear();
            for (int i = 0; i < libraryHeroPool.Count; i++)
            {
                bool active = i == libraryHeroIndex;
                var dot = new Border
                {
                    Width = active ? 18 : 6, Height = 6, CornerRadius = new CornerRadius(3),
                    Background = Brush(active ? Accent : Color.FromArgb(72, 255, 255, 255)),
                    Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = libraryHeroPool[i].Name
                };
                libraryHeroDots.Children.Add(dot);
            }
        }

        void StartLibraryHeroTimer()
        {
            StopLibraryHeroTimer();
            if (UiMotion.Reduced || libraryHeroPool.Count < 2) return;
            libraryHeroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8.5) };
            libraryHeroTimer.Tick += delegate
            {
                if (libraryHeroPool.Count < 2) { StopLibraryHeroTimer(); return; }
                if (page != "Bibliothèque" && page != "Favoris" && page != "Récents") { StopLibraryHeroTimer(); return; }
                if (DateTime.UtcNow < libraryHeroHoldUntil) return;
                int next = libraryHeroIndex + 1;
                if (next < 0 || next >= libraryHeroPool.Count) next = 0;
                SetLibraryHero(libraryHeroPool[next], false);
            };
            libraryHeroTimer.Start();
        }

        void StopLibraryHeroTimer()
        {
            if (libraryHeroTimer == null) return;
            libraryHeroTimer.Stop();
            libraryHeroTimer = null;
        }

        void AddLibraryShelves(StackPanel root, List<Channel> view, List<Channel> modeBase, string mode)
        {
            bool filtered = activeKind != "Tous" || activeGroup != AllGroups;
            if (filtered)
            {
                var shelf = BuildLibraryShelf("SÉLECTION", CollectionTitle(activeKind, activeGroup), "Tout le contenu correspondant à vos filtres.", view.Take(26).ToList(), false, mode, activeKind, activeGroup, view.Count);
                if (shelf != null) root.Children.Add(shelf);
            }
            else
            {
                // Séries comprises : la liste est construite depuis les positions
                // enregistrées, puis filtrée sur la vue courante (favoris, récents…).
                var resume = BuildResumeItems(20);
                if (mode == "Favoris")
                    resume = resume.Where(c => favorites.Contains(c.Id) || (SeriesForEpisode(c) != null && favorites.Contains(SeriesForEpisode(c).Id))).ToList();
                if (mode == "Récents")
                    resume = resume.Where(c => recentIds.Contains(c.Id) || (SeriesForEpisode(c) != null && recentIds.Contains(SeriesForEpisode(c).Id))).ToList();
                if (resume.Count > 0)
                {
                    var shelf = BuildLibraryShelf("CONTINUER", "Reprendre la lecture", "Séries, films et épisodes commencés.", resume, false, mode, activeKind, activeGroup, resume.Count);
                    if (shelf != null) root.Children.Add(shelf);
                }

                if (favorites.Count > 0 || recentIds.Count > 0)
                {
                    var curated = BuildCurated(view, 16);
                    if (curated.Count >= 3)
                    {
                        var shelf = BuildLibraryShelf("POUR VOUS", "Votre sélection", "Inspirée de vos favoris et de votre historique.", curated, false, mode, activeKind, activeGroup, view.Count);
                        if (shelf != null) root.Children.Add(shelf);
                    }
                }

                int filmCount = view.Count(c => c.Kind == "Films");
                int seriesCount = view.Count(c => c.Kind == "Séries");
                int liveCount = view.Count(c => c.Kind == "Chaînes");
                int shelves = 0;
                if (filmCount > 0 && (filmCount >= 3 || view.Count <= 18))
                {
                    var films = view.Where(c => c.Kind == "Films").Take(18).ToList();
                    var shelf = BuildLibraryShelf("CATALOGUE", "Films", TextUtil.HumanCount(filmCount) + " films à découvrir", films, false, mode, "Films", AllGroups, filmCount);
                    if (shelf != null) { root.Children.Add(shelf); shelves++; }
                }
                if (seriesCount > 0 && (seriesCount >= 3 || view.Count <= 18))
                {
                    var series = view.Where(c => c.Kind == "Séries").Take(18).ToList();
                    var shelf = BuildLibraryShelf("CATALOGUE", "Séries et épisodes", TextUtil.HumanCount(seriesCount) + " contenus à suivre", series, false, mode, "Séries", AllGroups, seriesCount);
                    if (shelf != null) { root.Children.Add(shelf); shelves++; }
                }
                if (liveCount > 0 && (liveCount >= 3 || view.Count <= 18))
                {
                    var live = view.Where(c => c.Kind == "Chaînes").Take(18).ToList();
                    var shelf = BuildLibraryShelf("EN DIRECT", "Chaînes", TextUtil.HumanCount(liveCount) + " chaînes disponibles", live, true, mode, "Chaînes", AllGroups, liveCount);
                    if (shelf != null) { root.Children.Add(shelf); shelves++; }
                }
                if (shelves == 0 || view.Count <= 18)
                {
                    var shelf = BuildLibraryShelf("EXPLORER", "Tout le contenu", "Parcourez l’ensemble de votre bibliothèque.", view.Take(20).ToList(), false, mode, activeKind, activeGroup, view.Count);
                    if (shelf != null) root.Children.Add(shelf);
                }
            }

            if (activeGroup == AllGroups)
            {
                var categories = BuildLibraryCategories(modeBase, mode);
                if (categories != null) root.Children.Add(categories);
            }
        }

        List<Channel> BuildCurated(List<Channel> view, int count)
        {
            var result = new List<Channel>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<Channel> add = delegate(Channel channel)
            {
                if (channel == null || result.Count >= count) return;
                if (!seen.Add(channel.Id)) return;
                result.Add(channel);
            };
            foreach (var channel in view.Where(c => favorites.Contains(c.Id))) add(channel);
            foreach (string id in recentIds)
            {
                var channel = view.FirstOrDefault(c => String.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
                add(channel);
            }
            foreach (var channel in view.Where(c => !String.IsNullOrWhiteSpace(c.PosterUrl) || !String.IsNullOrWhiteSpace(c.BackdropUrl)).OrderByDescending(c => c.TmdbRating)) add(channel);
            foreach (var channel in view) add(channel);
            return result;
        }

        FrameworkElement BuildLibraryShelf(string eyebrow, string title, string subtitle, List<Channel> items, bool wide, string mode, string kindFilter, string groupFilter, int total)
        {
            if (items == null || items.Count == 0) return null;
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };

            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
            var seeAll = TextAction("Tout voir", delegate
            {
                ShowLibraryCollection(CollectionTitle(kindFilter, groupFilter), kindFilter, groupFilter);
            }, "Voir toute la sélection");
            seeAll.Margin = new Thickness(0);
            DockPanel.SetDock(seeAll, Dock.Right);
            header.Children.Add(seeAll);

            var labels = new StackPanel();
            labels.Children.Add(Text(title, 19, Brush(TextPrimary), true));
            header.Children.Add(labels);
            section.Children.Add(header);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            foreach (var channel in items) row.Children.Add(CreateLibraryCard(channel, wide, true));
            section.Children.Add(WrapLibraryScroller(row));
            return section;
        }

        FrameworkElement BuildLibraryCategories(List<Channel> modeBase, string mode)
        {
            var section = new StackPanel { Margin = new Thickness(0, 2, 0, 10) };
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 18) };
            var manage = TextAction("Gérer", delegate { Nav("Catégories"); }, "Gérer les catégories");
            manage.Margin = new Thickness(0);
            DockPanel.SetDock(manage, Dock.Right); header.Children.Add(manage);
            var labels = new StackPanel();
            labels.Children.Add(Text("Toutes les catégories", 22, Brush(TextPrimary), true));
            header.Children.Add(labels);
            section.Children.Add(header);

            string[] kinds = { "Chaînes", "Films", "Séries" };
            int rendered = 0;
            for (int k = 0; k < kinds.Length; k++)
            {
                string kind = kinds[k];
                if (activeKind != "Tous" && !String.Equals(activeKind, kind, StringComparison.OrdinalIgnoreCase)) continue;
                var groups = modeBase
                    .Where(c => String.Equals(c.Kind, kind, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(c => String.IsNullOrWhiteSpace(c.Group) ? "Non classé" : c.Group, StringComparer.OrdinalIgnoreCase)
                    .Where(g => !hiddenGroups.Contains(g.Key))
                    .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                if (groups.Count == 0) continue;
                rendered++;
                section.Children.Add(BuildLibraryCategoryKindHeader(kind, groups));

                var grid = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
                for (int i = 0; i < groups.Count; i++)
                {
                    string name = groups[i].Key;
                    int count = groups[i].Count();
                    grid.Children.Add(CreateLibraryCategoryCard(name, count, i, mode, kind));
                }
                section.Children.Add(grid);
            }
            return rendered == 0 ? null : section;
        }

        FrameworkElement BuildLibraryCategoryKindHeader(string kind, List<IGrouping<string, Channel>> groups)
        {
            int count = 0;
            for (int i = 0; i < groups.Count; i++) count += groups[i].Count();
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
            var seeAll = Chip("Tout voir", false, delegate
            {
                ShowLibraryCollection(CollectionTitle(kind, AllGroups), kind, AllGroups);
            });
            seeAll.Margin = new Thickness(0);
            DockPanel.SetDock(seeAll, Dock.Right);
            header.Children.Add(seeAll);

            var labels = new StackPanel();
            var title = new StackPanel { Orientation = Orientation.Horizontal };
            title.Children.Add(Text(kind, 18, Brush(TextPrimary), true));
            var badge = Badge(TextUtil.HumanCount(groups.Count) + (groups.Count > 1 ? " CATÉGORIES" : " CATÉGORIE"), Brush(WithAlpha(TextPrimary, 10)), Brush(Muted));
            badge.Margin = new Thickness(10, 0, 0, 0);
            title.Children.Add(badge);
            labels.Children.Add(title);
            labels.Children.Add(Text(TextUtil.HumanCount(count) + (count > 1 ? " éléments répartis dans toutes les catégories." : " élément dans cette catégorie."), 10.5, Brush(Muted), false));
            header.Children.Add(labels);
            return header;
        }

        Border CreateLibraryCategoryCard(string name, int count, int index, string mode, string kind)
        {
            Color tint = index % 3 == 0 ? Accent : index % 3 == 1 ? Secondary : Color.FromRgb(244, 114, 182);
            var grid = new Grid { Height = 116 };
            grid.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = new LinearGradientBrush(WithAlpha(tint, 72), Panel, 24)
            });
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(16, 12, 16, 15) };
            string kindGlyph = kind == "Chaînes" ? Glyphs.TV : kind == "Films" ? Glyphs.Movies : Glyphs.Series;
            var icon = Icon(kindGlyph, 18, Brush(tint));
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            icon.Margin = new Thickness(0, 0, 0, 9);
            text.Children.Add(icon);
            text.Children.Add(Text(name, 15, Brush(TextPrimary), true));
            text.Children.Add(Text(TextUtil.HumanCount(count) + " élément(s)", 10, Brush(Muted), false));
            grid.Children.Add(text);
            var chevron = Icon(Glyphs.ChevronRight, 14, Brush(WithAlpha(TextPrimary, 120)));
            chevron.HorizontalAlignment = HorizontalAlignment.Right; chevron.VerticalAlignment = VerticalAlignment.Center;
            chevron.Margin = new Thickness(0, 0, 14, 0);
            grid.Children.Add(chevron);

            string captured = name;
            string capturedKind = kind;
            Action apply = delegate { activeKind = capturedKind; activeGroup = captured; ShowLibrary(mode); };
            var card = new Border
            {
                Child = grid, Width = 214, Height = 126, Background = Brush(Panel), BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Margin = new Thickness(0, 0, 12, 14),
                Cursor = Cursors.Hand, Focusable = true, ToolTip = kind + "  ·  " + name + "  ·  " + TextUtil.HumanCount(count) + " élément(s)"
            };
            var lift = new TranslateTransform();
            card.RenderTransform = lift;
            Action hover = delegate
            {
                card.BorderBrush = Brush(tint); card.Background = Brush(Raised); System.Windows.Controls.Panel.SetZIndex(card, 4);
                if (UiMotion.Reduced) lift.Y = -3;
                else lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-3, TimeSpan.FromMilliseconds(130)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                card.Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 6, Opacity = 0.3, Color = Colors.Black };
            };
            Action reset = delegate
            {
                if (card.IsKeyboardFocusWithin) return;
                card.BorderBrush = Brush(BorderColor); card.Background = Brush(Panel); System.Windows.Controls.Panel.SetZIndex(card, 0);
                if (UiMotion.Reduced) lift.Y = 0;
                else lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(130)));
                card.Effect = null;
            };
            card.MouseEnter += delegate { hover(); };
            card.MouseLeave += delegate { reset(); };
            card.MouseLeftButtonDown += delegate { card.Focus(); };
            card.GotKeyboardFocus += delegate { hover(); card.BringIntoView(); };
            card.LostKeyboardFocus += delegate { reset(); };
            card.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
                apply();
            };
            card.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space) { apply(); e.Handled = true; }
                else if (e.Key == Key.Left || e.Key == Key.Right) { MoveLibraryFocus(card, e.Key == Key.Right ? 1 : -1); e.Handled = true; }
            };
            return card;
        }

        Border CreateLibraryCard(Channel channel, bool wide, bool heroHook)
        {
            // Grandes cartes : 236 × 354 pour les films et séries (2:3 strict),
            // 280 × 158 pour les chaînes (16:9). L'affiche remplit le cadre de
            // bord à bord ; le titre est posé dessus, comme sur une vignette
            // de plateforme de streaming.
            double width = wide ? 280 : 236;
            double artHeight = wide ? 158 : 354;

            var art = new Grid { Height = artHeight, ClipToBounds = true };
            var artBorder = new Border { CornerRadius = new CornerRadius(15), Background = Brush(Raised), ClipToBounds = true };
            RoundClip(artBorder, 15);
            art.Children.Add(artBorder);
            // Le visuel de repli est toujours présent sous la cover : une carte
            // n'est jamais vide, même quand le serveur d'affiches est lent.
            artBorder.Child = CoverArt(channel, width, artHeight, wide);
            art.Children.Add(new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.34),
                    new GradientStop(Color.FromArgb(190, 0, 0, 0), 1)
                }, new Point(0.5, 0), new Point(0.5, 1))
            });

            if (channel.TmdbRating > 0)
            {
                var rating = Badge("★ " + channel.TmdbRating.ToString("0.0", CultureInfo.InvariantCulture), Brush(Color.FromArgb(170, 8, 8, 16)), Brush(Color.FromRgb(251, 191, 36)));
                rating.HorizontalAlignment = HorizontalAlignment.Right; rating.VerticalAlignment = VerticalAlignment.Top;
                rating.Margin = new Thickness(0, 10, 10, 0);
                art.Children.Add(rating);
            }

            var hoverLayer = new Border
            {
                Opacity = 0, IsHitTestVisible = false,
                Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(138, 0, 0, 0), 90)
            };
            var playIcon = Icon(Glyphs.Play, 19, Brush(Color.FromRgb(10, 11, 15)));
            playIcon.HorizontalAlignment = HorizontalAlignment.Center; playIcon.VerticalAlignment = VerticalAlignment.Center;
            hoverLayer.Child = new Border
            {
                Width = 48, Height = 48, CornerRadius = new CornerRadius(24), Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Child = playIcon, Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.35, Color = Colors.Black }
            };
            art.Children.Add(hoverLayer);

            var title = Text(TextUtil.DisplayTitle(channel.Name), wide ? 14 : 14.5, Brushes.White, true);
            title.TextWrapping = TextWrapping.NoWrap;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.Margin = new Thickness(0, 0, 0, 2);

            string meta;
            if (channel.Kind == "Chaînes")
            {
                var now = CurrentProgram(channel);
                meta = now != null ? now.Title : (String.IsNullOrWhiteSpace(channel.Group) ? "En direct" : channel.Group);
            }
            else
            {
                // Série ou épisode : la saison et l'épisode passent devant le
                // reste, pour distinguer deux cartes d'une même série.
                string episodeTag = EpisodeTag(channel);
                double saved = GetSavedPosition(channel);
                if (saved > 0) meta = (episodeTag.Length > 0 ? episodeTag + "  ·  " : "")
                    + "Reprendre  ·  " + TextUtil.FormatPosition(TimeSpan.FromSeconds(saved));
                else meta = episodeTag.Length > 0 ? episodeTag : channel.Year;
            }
            var metaText = Text(meta, 12, Brush(Color.FromArgb(206, 255, 255, 255)), false);
            metaText.TextWrapping = TextWrapping.NoWrap;
            metaText.TextTrimming = TextTrimming.CharacterEllipsis;
            metaText.Margin = new Thickness(0);

            var caption = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(14, 0, 14, 13)
            };
            caption.Children.Add(title);
            caption.Children.Add(metaText);
            art.Children.Add(caption);

            double progress = wide ? 0 : ResumeProgress(channel);
            if (progress > 0)
            {
                var progressHost = new Grid { Height = 3, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 0) };
                progressHost.Children.Add(new Border { Background = Brush(Color.FromArgb(80, 255, 255, 255)) });
                progressHost.Children.Add(new Border
                {
                    Background = Brush(Accent), HorizontalAlignment = HorizontalAlignment.Left,
                    Width = Math.Max(6, width * progress)
                });
                art.Children.Add(progressHost);
            }

            var card = new Border
            {
                Child = art, Width = width, Background = Brushes.Transparent, BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 14, 18), Cursor = Cursors.Hand, Focusable = true,
                ToolTip = channel.Group + "  ·  " + channel.Name + (channel.TmdbRating > 0 ? "  ·  ★ " + channel.TmdbRating.ToString("0.0", CultureInfo.InvariantCulture) : "")
            };
            var lift = new TranslateTransform();
            card.RenderTransform = lift;
            bool hovering = false;

            Action hover = delegate
            {
                hovering = true;
                card.Background = Brushes.Transparent; card.BorderBrush = Brush(Accent); System.Windows.Controls.Panel.SetZIndex(card, 5);
                if (UiMotion.Reduced) lift.Y = -4;
                else lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-4, TimeSpan.FromMilliseconds(140)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                UiMotion.Animate(hoverLayer, UIElement.OpacityProperty, 1, 150);
                card.Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 7, Opacity = 0.32, Color = Colors.Black };
            };
            Action reset = delegate
            {
                if (card.IsKeyboardFocusWithin) return;
                card.Background = Brushes.Transparent; card.BorderBrush = Brush(BorderColor); System.Windows.Controls.Panel.SetZIndex(card, 0);
                if (UiMotion.Reduced) lift.Y = 0;
                else lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(140)));
                UiMotion.Animate(hoverLayer, UIElement.OpacityProperty, 0, 150);
                card.Effect = null;
            };

            card.MouseEnter += delegate
            {
                hover();
                if (heroHook) SetLibraryHero(channel, true);
            };
            card.MouseLeave += delegate { hovering = false; reset(); };
            card.MouseLeftButtonDown += delegate { card.Focus(); };
            card.GotKeyboardFocus += delegate
            {
                hover();
                if (heroHook) SetLibraryHero(channel, true);
                card.BringIntoView();
            };
            card.LostKeyboardFocus += delegate
            {
                if (hovering) hover(); else reset();
            };
            card.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
                OpenLibraryChannel(channel);
            };
            card.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space) { OpenLibraryChannel(channel); e.Handled = true; }
                else if (e.Key == Key.Left || e.Key == Key.Right) { MoveLibraryFocus(card, e.Key == Key.Right ? 1 : -1); e.Handled = true; }
            };

            var menu = CreateContextMenu();
            menu.Items.Add(MenuItem(channel.IsSeries ? "Voir les épisodes" : "Lire", delegate { Play(channel, currentView); }, Glyphs.Play));
            menu.Items.Add(MenuItem(favorites.Contains(channel.Id) ? "Retirer des favoris" : "Ajouter aux favoris", delegate { ToggleFavorite(channel); }, Glyphs.Favorite));
            if (channel.Kind == "Films" || channel.Kind == "Séries")
            {
                menu.Items.Add(MenuItem("Voir la fiche", delegate { ShowChannelInfo(channel); }, Glyphs.Info));
                if (TmdbClient.IsConfigured(settings))
                    menu.Items.Add(MenuItem("Corriger TMDb", delegate { ShowTmdbMatchDialog(channel); }, Glyphs.Search));
            }
            menu.Items.Add(MenuItem("Masquer", delegate
            {
                hiddenChannels.Add(channel.Id); SaveState(); ShowLibrary(page);
            }, Glyphs.View));
            if (!String.IsNullOrWhiteSpace(channel.Url))
            {
                menu.Items.Add(MenuItem("Copier l’URL du flux", delegate { CopyText(channel.Url); }, Glyphs.Copy));
                menu.Items.Add(MenuItem("Ouvrir dans le lecteur externe", delegate { OpenInExternalPlayer(channel.Url); }, Glyphs.External));
            }
            card.ContextMenu = menu;
            return card;
        }

        FrameworkElement LibraryCardArt(Channel channel, double height, bool wide)
        {
            var grid = new Grid();
            grid.Children.Add(new Border { Background = new LinearGradientBrush(WithAlpha(Accent, 48), Raised, 34) });
            var halo = new System.Windows.Shapes.Ellipse
            {
                Width = height * 0.82, Height = height * 0.82, Stroke = Brush(WithAlpha(Accent, 56)), StrokeThickness = 1,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(halo);
            var fallback = Icon(channel.Kind == "Films" ? Glyphs.Movies : channel.Kind == "Séries" ? Glyphs.Series : Glyphs.TV,
                wide ? 30 : 36, Brush(WithAlpha(Accent, 150)));
            fallback.HorizontalAlignment = HorizontalAlignment.Center;
            fallback.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(fallback);
            if (!String.IsNullOrWhiteSpace(channel.Logo))
            {
                // Les affiches de films et de séries remplissent tout le cadre,
                // même lorsqu'elles arrivent par le champ `logo` du fournisseur.
                // Seuls les vrais logos de chaînes gardent une marge de respiration.
                bool live = channel.Kind == "Chaînes";
                grid.Children.Add(new AsyncImage
                {
                    Url = channel.Logo, Stretch = live ? Stretch.Uniform : Stretch.UniformToFill,
                    Margin = live ? new Thickness(26, 16, 26, 16) : new Thickness(0),
                    Opacity = 1.0
                });
            }
            else
            {
                var initials = Text(LibraryInitials(channel.Name), wide ? 28 : 30, Brush(WithAlpha(TextPrimary, 120)), true);
                initials.HorizontalAlignment = HorizontalAlignment.Center;
                initials.VerticalAlignment = VerticalAlignment.Center;
                initials.TextAlignment = TextAlignment.Center;
                grid.Children.Add(initials);
                // Même sans affiche ni logo, la carte porte le titre : elle reste
                // lisible et identifiable comme une vignette.
                var caption = Text(TextUtil.Ellipsis(channel.Name, 48), wide ? 10.5 : 10, Brush(WithAlpha(TextPrimary, 178)), true);
                caption.TextWrapping = TextWrapping.Wrap;
                caption.MaxHeight = 30;
                caption.Margin = new Thickness(10, 0, 10, 9);
                caption.VerticalAlignment = VerticalAlignment.Bottom;
                caption.HorizontalAlignment = HorizontalAlignment.Left;
                grid.Children.Add(caption);
            }
            return grid;
        }

        FrameworkElement WrapLibraryScroller(UIElement content)
        {
            var scroll = new ScrollViewer
            {
                Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false,
                PanningMode = PanningMode.HorizontalOnly, Focusable = false
            };
            scroll.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e)
            {
                double target = scroll.HorizontalOffset - e.Delta * 0.9;
                if (target < 0) target = 0;
                if (target > scroll.ScrollableWidth) target = scroll.ScrollableWidth;
                if (Math.Abs(target - scroll.HorizontalOffset) < 0.5) return;
                scroll.ScrollToHorizontalOffset(target);
                e.Handled = true;
            };
            var host = new Grid();
            host.Children.Add(scroll);
            host.Children.Add(new Border
            {
                Width = 30, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false,
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(Back, 232), 0),
                    new GradientStop(WithAlpha(Back, 0), 1)
                }, new Point(0, 0.5), new Point(1, 0.5))
            });
            host.Children.Add(new Border
            {
                Width = 30, HorizontalAlignment = HorizontalAlignment.Right, IsHitTestVisible = false,
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(WithAlpha(Back, 0), 0),
                    new GradientStop(WithAlpha(Back, 232), 1)
                }, new Point(0, 0.5), new Point(1, 0.5))
            });

            // Flèches de rangée : elles n'apparaissent qu'au survol, comme sur
            // un service de streaming, et font défiler d'une page à l'autre.
            var left = ShelfArrow(Glyphs.ChevronLeft);
            left.HorizontalAlignment = HorizontalAlignment.Left;
            left.Margin = new Thickness(4, 0, 0, 0);
            var right = ShelfArrow(Glyphs.ChevronRight);
            right.HorizontalAlignment = HorizontalAlignment.Right;
            right.Margin = new Thickness(0, 0, 4, 0);
            host.Children.Add(left);
            host.Children.Add(right);

            Action update = delegate
            {
                bool canLeft = scroll.HorizontalOffset > 4;
                bool canRight = scroll.HorizontalOffset < scroll.ScrollableWidth - 4;
                left.Visibility = canLeft ? Visibility.Visible : Visibility.Collapsed;
                right.Visibility = canRight ? Visibility.Visible : Visibility.Collapsed;
            };
            left.Click += delegate
            {
                scroll.ScrollToHorizontalOffset(Math.Max(0, scroll.HorizontalOffset - scroll.ViewportWidth * 0.82));
            };
            right.Click += delegate
            {
                scroll.ScrollToHorizontalOffset(Math.Min(scroll.ScrollableWidth, scroll.HorizontalOffset + scroll.ViewportWidth * 0.82));
            };
            scroll.ScrollChanged += delegate { update(); };
            scroll.SizeChanged += delegate { update(); };
            host.MouseEnter += delegate
            {
                update();
                left.IsHitTestVisible = true; right.IsHitTestVisible = true;
                UiMotion.Animate(left, UIElement.OpacityProperty, 1, 140);
                UiMotion.Animate(right, UIElement.OpacityProperty, 1, 140);
            };
            host.MouseLeave += delegate
            {
                left.IsHitTestVisible = false; right.IsHitTestVisible = false;
                UiMotion.Animate(left, UIElement.OpacityProperty, 0, 160);
                UiMotion.Animate(right, UIElement.OpacityProperty, 0, 160);
            };
            left.IsHitTestVisible = false;
            right.IsHitTestVisible = false;
            update();
            return host;
        }

        Button ShelfArrow(string glyph)
        {
            var button = new Button
            {
                Content = Icon(glyph, 18, Brushes.White), Width = 38, Height = 54, Padding = new Thickness(0),
                Background = Brush(Color.FromArgb(196, 8, 9, 13)),
                BorderBrush = Brush(Color.FromArgb(80, 255, 255, 255)), BorderThickness = new Thickness(1),
                Template = RoundedButtonTemplate(8), Cursor = Cursors.Hand, FocusVisualStyle = null,
                VerticalAlignment = VerticalAlignment.Center, Opacity = 0, IsTabStop = false
            };
            return button;
        }

        void MoveLibraryFocus(Border card, int delta)
        {
            if (card == null) return;
            var parent = VisualTreeHelper.GetParent(card) as Panel;
            if (parent == null) return;
            int index = parent.Children.IndexOf(card);
            if (index < 0) return;
            int next = index + delta;
            if (next < 0 || next >= parent.Children.Count) return;
            var target = parent.Children[next] as UIElement;
            if (target == null) return;
            target.Focus();
            Keyboard.Focus(target);
        }

        void OpenLibraryChannel(Channel channel)
        {
            if (channel == null) return;
            bool series = channel.IsSeries || (channel.Kind == "Séries" && String.IsNullOrWhiteSpace(channel.Url));
            if (series || channel.Kind == "Films") ShowChannelInfo(channel);
            else Play(channel, currentView);
        }

        void ShowLibraryCollection(string title, string kind, string group)
        {
            StopLibraryHeroTimer();
            // « Tout voir » peut partir de l'accueil : la page de collection
            // appartient alors à la bibliothèque, et le bouton retour ramène à
            // l'écran d'origine.
            string origin = page;
            if (origin == "Accueil") page = "Bibliothèque";
            activeKind = kind; activeGroup = group;
            channelList = new StackPanel(); resultsScroll = null; moreHost = null;
            // « Tout parcourir » est toujours en grille, mais sans écraser le
            // choix de l'utilisateur : le retour à la découverte restaure la
            // présentation qu'il avait enregistrée.
            collectionGrid = true;
            renderLimit = 150;
            RebuildSidebar(); UpdateStatusBar(); SetPageStatus();

            var modeBase = BaseViewForMode(page).ToList();
            currentView = LibraryFilteredView(modeBase);
            var root = new StackPanel { Margin = new Thickness(28, 22, 28, 38) };

            var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var back = IconButton(Glyphs.Back, "Revenir à la découverte", delegate
            {
                collectionGrid = false;
                activeKind = "Tous"; activeGroup = AllGroups;
                if (origin == "Accueil") ShowHome(); else ShowLibrary(origin);
            }, false);
            back.Margin = new Thickness(0, 1, 14, 0);
            Grid.SetColumn(back, 0); header.Children.Add(back);

            var labels = new StackPanel();
            labels.Children.Add(Text("TOUT PARCOURIR", 9.5, Brush(Accent), true));
            var heading = Text(title, 27, Brush(TextPrimary), true);
            pageTitle = heading;
            labels.Children.Add(heading);
            labels.Children.Add(Text("Filtrez, triez et ouvrez un contenu sans quitter la bibliothèque.", 12, Brush(Muted), false));
            Grid.SetColumn(labels, 1); header.Children.Add(labels);
            root.Children.Add(header);

            root.Children.Add(BuildCollectionFilters(modeBase));
            resultCountText = Text("", 12, Brush(Muted), false);
            resultCountText.Margin = new Thickness(0, 0, 0, 10);
            root.Children.Add(resultCountText);
            root.Children.Add(channelList);
            RenderChannels();
            SetContent(root);
        }

        FrameworkElement BuildCollectionFilters(List<Channel> modeBase)
        {
            var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 14) };
            var tools = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };

            var category = new ComboBox { Width = 210, Height = 35, Style = ModernComboStyle(), Margin = new Thickness(0, 0, 7, 0) };
            category.Items.Add("Toutes les catégories");
            var groups = modeBase
                .Where(c => activeKind == "Tous" || c.Kind == activeKind)
                .GroupBy(c => String.IsNullOrWhiteSpace(c.Group) ? "Non classé" : c.Group, StringComparer.OrdinalIgnoreCase)
                .Where(g => !hiddenGroups.Contains(g.Key))
                .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => g.Key)
                .ToList();
            foreach (string group in groups) category.Items.Add(group);
            if (activeGroup != AllGroups && !groups.Contains(activeGroup, StringComparer.OrdinalIgnoreCase)) category.Items.Add(activeGroup);
            category.SelectedItem = activeGroup == AllGroups ? "Toutes les catégories" : activeGroup;
            category.SelectionChanged += delegate
            {
                if (category.SelectedItem == null) return;
                string value = Convert.ToString(category.SelectedItem, CultureInfo.InvariantCulture);
                string nextGroup = value == "Toutes les catégories" ? AllGroups : value;
                if (String.Equals(activeGroup, nextGroup, StringComparison.OrdinalIgnoreCase)) return;
                ShowLibraryCollection(CollectionTitle(activeKind, nextGroup), activeKind, nextGroup);
            };
            tools.Children.Add(category);

            var sorter = new ComboBox { Width = 162, Height = 35, Style = ModernComboStyle(), Margin = new Thickness(0, 0, 0, 0) };
            foreach (string option in SortOptions()) sorter.Items.Add(option);
            sorter.SelectedItem = sortMode;
            sorter.SelectionChanged += delegate
            {
                if (sorter.SelectedItem == null) return;
                string chosen = Convert.ToString(sorter.SelectedItem, CultureInfo.InvariantCulture);
                if (chosen == sortMode) return;
                sortMode = chosen; settings.SortMode = chosen; SaveSettings();
                RenderChannels();
            };
            tools.Children.Add(sorter);
            DockPanel.SetDock(tools, Dock.Right); bar.Children.Add(tools);

            var tabs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            AddCollectionKindTab(tabs, "Tout", "Tous", modeBase.Count);
            AddCollectionKindTab(tabs, "Direct", "Chaînes", modeBase.Count(c => c.Kind == "Chaînes"));
            AddCollectionKindTab(tabs, "Films", "Films", modeBase.Count(c => c.Kind == "Films"));
            AddCollectionKindTab(tabs, "Séries", "Séries", modeBase.Count(c => c.Kind == "Séries"));
            bar.Children.Add(tabs);
            return bar;
        }

        void AddCollectionKindTab(Panel panel, string label, string kind, int count)
        {
            string captured = kind;
            string tooltip = label + "  ·  " + TextUtil.HumanCount(count) + " élément(s)";
            var tab = FilterTab(label, activeKind == captured, delegate
            {
                ShowLibraryCollection(CollectionTitle(captured, AllGroups), captured, AllGroups);
            }, tooltip);
            panel.Children.Add(tab);
        }

        string CollectionTitle(string kind, string group)
        {
            if (group != AllGroups) return group;
            if (kind == "Chaînes") return "Chaînes en direct";
            if (kind == "Films") return "Films";
            if (kind == "Séries") return "Séries";
            return "Toute la bibliothèque";
        }

        string LibraryTitleForMode(string mode)
        {
            if (mode == "Favoris") return "Ma liste";
            if (mode == "Récents") return "Récemment regardés";
            return "Bibliothèque";
        }

        static string LibraryInitials(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return "L";
            string[] parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var words = new List<string>();
            foreach (string part in parts)
            {
                if (part.Length == 0) continue;
                if (!Char.IsLetterOrDigit(part[0])) continue;
                words.Add(part);
            }
            if (words.Count == 0) return "L";
            if (words.Count == 1) return words[0].Substring(0, 1).ToUpperInvariant();
            return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();
        }
    }
}
