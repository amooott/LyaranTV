using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LyaranTV
{
    internal static class Glyphs
    {
        public const string Home = "\uE80F";
        public const string Library = "\uE8F1";
        public const string List = "\uE8FD";
        public const string Grid = "\uE80A";
        public const string Favorite = "\uE734";
        public const string FavoriteFill = "\uE735";
        public const string Recent = "\uE81C";
        public const string Guide = "\uE787";
        public const string Clock = "\uE823";
        public const string Local = "\uE8B7";
        public const string Settings = "\uE713";
        public const string Help = "\uE897";
        public const string Search = "\uE721";
        public const string Add = "\uE710";
        public const string Refresh = "\uE72C";
        public const string Play = "\uE768";
        public const string Pause = "\uE769";
        public const string Stop = "\uE71A";
        public const string Previous = "\uE892";
        public const string Next = "\uE893";
        public const string Volume = "\uE767";
        public const string Mute = "\uE74F";
        public const string FullScreen = "\uE740";
        public const string BackToWindow = "\uE73F";
        public const string Back = "\uE72B";
        public const string Forward = "\uE72A";
        public const string ChevronDown = "\uE70D";
        public const string ChevronUp = "\uE70E";
        public const string ChevronRight = "\uE76C";
        public const string ChevronLeft = "\uE76B";
        public const string More = "\uE712";
        public const string Filter = "\uE71C";
        public const string Sort = "\uE8CB";
        public const string TV = "\uE7F4";
        public const string Movies = "\uE8B2";
        public const string Series = "\uE8A9";
        public const string Picture = "\uE8B9";
        public const string External = "\uE8A7";
        public const string Copy = "\uE8C8";
        public const string Edit = "\uE70F";
        public const string Delete = "\uE74D";
        public const string Save = "\uE74E";
        public const string OpenFile = "\uE8E5";
        public const string Folder = "\uE8B7";
        public const string Info = "\uE946";
        public const string Warning = "\uE7BA";
        public const string Error = "\uE783";
        public const string Success = "\uE930";
        public const string Check = "\uE73E";
        // Icône « langue » (globe stylisé) : repère universel pour le sélecteur.
        public const string Language = "\uE8C1";
        public const string Close = "\uE711";
        public const string Globe = "\uE774";
        public const string Link = "\uE71B";
        public const string Download = "\uE896";
        public const string Upload = "\uE898";
        public const string Zip = "\uE739";
        public const string Repair = "\uE90F";
        public const string Admin = "\uE7EF";
        public const string View = "\uE890";
        public const string Zoom = "\uE71E";
        public const string Lock = "\uE72E";
        public const string Unlock = "\uE785";
        public const string Power = "\uE7E8";
        public const string Record = "\uE7C8";
        public const string Music = "\uE8D6";
        public const string Camera = "\uE722";
        public const string Cast = "\uE8B5";
        public const string Subtitles = "\uE8D2";
        public const string Repeat = "\uE8EE";
    }

    public sealed partial class MainWindow
    {
        static readonly Color DefaultAccent = Color.FromRgb(139, 92, 246);

        static SolidColorBrush Brush(Color c) { return new SolidColorBrush(c); }
        static Color WithAlpha(Color c, byte alpha) { return Color.FromArgb(alpha, c.R, c.G, c.B); }
        static bool TryParseHexColor(string value, out Color color)
        {
            color = DefaultAccent;
            string hex = (value ?? "").Trim().TrimStart('#');
            if (hex.Length != 6) return false;
            try
            {
                color = Color.FromRgb(Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
                return true;
            }
            catch { return false; }
        }
        Color TextOnAccentColor()
        {
            // Choisit le texte le plus lisible sur l'accent, y compris pour les
            // accents personnalisés : blanc ou encre sombre selon le contraste.
            Color dark = Color.FromRgb(10, 10, 18);
            return ContrastRatio(dark, Accent) >= ContrastRatio(Colors.White, Accent) ? dark : Colors.White;
        }
        Brush AccentSoftBrush { get { return Brush(WithAlpha(Accent, 38)); } }
        Brush HoverBrush { get { return Brush(WithAlpha(TextPrimary, 12)); } }

        TextBlock Text(string value, double size, Brush color, bool bold)
        {
            // Plancher de lisibilité : les anciennes métadonnées à 9,5 px
            // restaient illisibles sur un écran dense. Aucune information ne
            // descend désormais sous 12,5 px.
            if (size < 12.5) size = 12.5;
            return new TextBlock
            {
                // Traduction au moment de la création : les écrans reconstruits
                // en continu (bannière d'accueil, cartes) ne repassent jamais en
                // français, contrairement à une passe unique après affichage.
                Text = I18n.TPrefix(value ?? ""), FontSize = size, Foreground = color,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }
        TextBlock Text(string value, double size, Color color, bool bold) { return Text(value, size, Brush(color), bold); }

        new TextBlock Icon(string glyph, double size, Brush color)
        {
            return new TextBlock
            {
                Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets, Segoe UI Symbol"), FontSize = size,
                Foreground = color, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center
            };
        }
        new TextBlock Icon(string glyph, double size, Color color) { return Icon(glyph, size, Brush(color)); }

        StackPanel IconText(string glyph, string label, Brush color, double size)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (!String.IsNullOrWhiteSpace(glyph))
            {
                var icon = Icon(glyph, size + 1, color);
                icon.Margin = new Thickness(0, 0, 7, 0);
                panel.Children.Add(icon);
            }
            panel.Children.Add(Text(label, size, color, false));
            return panel;
        }

        Button Button(string label, Action click, bool primary)
        {
            return Button("", label, click, primary);
        }

        Button Button(string glyph, string label, Action click, bool primary)
        {
            var content = String.IsNullOrWhiteSpace(glyph) ? (UIElement)Text(label, 13, primary ? Brush(TextOnAccentColor()) : Brush(TextPrimary), true)
                : (UIElement)IconText(glyph, label, primary ? Brush(TextOnAccentColor()) : Brush(TextPrimary), 12.5);
            var button = new Button
            {
                Content = content, Height = 38, Padding = new Thickness(14, 4, 14, 4), Margin = new Thickness(0, 0, 8, 0),
                Background = Brush(primary ? Accent : Raised), Foreground = Brush(primary ? TextOnAccentColor() : TextPrimary),
                BorderBrush = Brush(primary ? Accent : BorderColor), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, FontSize = 13, FocusVisualStyle = null, SnapsToDevicePixels = true,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                Template = RoundedButtonTemplate()
            };
            if (click != null) button.Click += delegate { click(); };
            return button;
        }

        Button IconButton(string glyph, string tooltip, Action click, bool primary)
        {
            var button = new Button
            {
                Content = Icon(glyph, 15, primary ? Brush(TextOnAccentColor()) : Brush(TextPrimary)),
                Width = 38, Height = 38, Padding = new Thickness(0), Margin = new Thickness(0, 0, 7, 0),
                Background = Brush(primary ? Accent : Raised), BorderBrush = Brush(primary ? Accent : BorderColor),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, FocusVisualStyle = null,
                ToolTip = tooltip, Template = RoundedButtonTemplate(), SnapsToDevicePixels = true,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            if (click != null) button.Click += delegate { click(); };
            return button;
        }

        // Bouton rond posé sur la vidéo : pastille translucide, icône blanche et
        // surbrillance au survol, pour les contrôles du lecteur superposés.
        Button PlayerButton(string glyph, string tooltip, Action click, bool primary, double size)
        {
            var button = new Button
            {
                Content = Icon(glyph, primary ? Math.Round(size * 0.44) : 16, Brushes.White),
                Width = size, Height = size, Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 0),
                Background = primary ? Brush(Accent) : Brush(Color.FromArgb(132, 12, 12, 18)),
                BorderBrush = primary ? Brush(Accent) : Brush(Color.FromArgb(74, 255, 255, 255)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, FocusVisualStyle = null,
                ToolTip = tooltip, Template = RoundedButtonTemplate(size / 2), SnapsToDevicePixels = true,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            if (click != null) button.Click += delegate { click(); };
            return button;
        }

        Button Chip(string label, bool active, Action click)
        {
            var button = new Button
            {
                Content = Text(label, 12, Brush(TextPrimary), true),
                Height = 34, Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(0, 0, 7, 7),
                Background = Brush(active ? WithAlpha(Accent, 36) : Raised),
                Foreground = Brush(TextPrimary),
                BorderBrush = Brush(active ? Accent : BorderColor), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, FocusVisualStyle = null, Template = RoundedButtonTemplate(),
                SnapsToDevicePixels = true
            };
            if (click != null) button.Click += delegate { click(); };
            return button;
        }

        /// <summary>
        /// Onglet de filtre : texte seul, accent souligné pour l'état actif.
        /// Aucun cadre ni pastille : la navigation fait partie de la page, comme
        /// les onglets d'une plateforme de streaming.
        /// </summary>
        Button FilterTab(string label, bool active, Action click, string tooltip = null)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
            var text = Text(label, 14, active ? Brush(TextPrimary) : Brush(Muted), active);
            text.TextWrapping = TextWrapping.NoWrap;
            text.TextAlignment = TextAlignment.Center;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(text);
            stack.Children.Add(new Border
            {
                Height = 2, CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 7, 0, 0),
                Background = active ? Brush(Accent) : Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch
            });
            var button = new Button
            {
                Content = stack, Height = 42, Padding = new Thickness(15, 0, 15, 0), Margin = new Thickness(0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brush(TextPrimary),
                Cursor = Cursors.Hand, FocusVisualStyle = null, Template = RoundedButtonTemplate(8),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            if (!String.IsNullOrWhiteSpace(tooltip)) button.ToolTip = tooltip;
            if (!active)
            {
                button.MouseEnter += delegate { text.Foreground = Brush(TextPrimary); };
                button.MouseLeave += delegate { text.Foreground = Brush(Muted); };
                button.GotKeyboardFocus += delegate { text.Foreground = Brush(TextPrimary); };
                button.LostKeyboardFocus += delegate { text.Foreground = Brush(Muted); };
            }
            if (click != null) button.Click += delegate { click(); };
            return button;
        }

        /// <summary>
        /// Action discrète en texte : « Tout voir », « Effacer les filtres ».
        /// Intégrée au contenu, sans bordure ni fond, pour alléger les en-têtes.
        /// </summary>
        Button TextAction(string label, Action click, string tooltip = null)
        {
            var content = Text(label, 12.5, Brush(Muted), false);
            content.TextWrapping = TextWrapping.NoWrap;
            var button = new Button
            {
                Content = content, Height = 32, Padding = new Thickness(9, 0, 9, 0), Margin = new Thickness(0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brush(TextPrimary),
                Cursor = Cursors.Hand, FocusVisualStyle = null, Template = RoundedButtonTemplate(8),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            if (!String.IsNullOrWhiteSpace(tooltip)) button.ToolTip = tooltip;
            button.MouseEnter += delegate { content.Foreground = Brush(TextPrimary); };
            button.MouseLeave += delegate { content.Foreground = Brush(Muted); };
            if (click != null) button.Click += delegate { click(); };
            return button;
        }

        ControlTemplate RoundedButtonTemplate(double radius = 9)
        {
            var grid = new FrameworkElementFactory(typeof(Grid));
            var chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "Chrome"; chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            chrome.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            chrome.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            chrome.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            grid.AppendChild(chrome);
            var hover = new FrameworkElementFactory(typeof(Border));
            hover.Name = "Hover"; hover.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            hover.SetValue(Border.BackgroundProperty, Brushes.Transparent); hover.SetValue(UIElement.IsHitTestVisibleProperty, false);
            grid.AppendChild(hover);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            content.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            content.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            content.SetBinding(ContentPresenter.HorizontalAlignmentProperty, new Binding("HorizontalContentAlignment") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            content.SetBinding(ContentPresenter.VerticalAlignmentProperty, new Binding("VerticalContentAlignment") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            grid.AppendChild(content);
            var template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = grid };
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Color.FromArgb(18, 255, 255, 255)), "Hover"));
            template.Triggers.Add(over);
            var pressed = new Trigger { Property = System.Windows.Controls.Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Color.FromArgb(34, 255, 255, 255)), "Hover"));
            template.Triggers.Add(pressed);
            var focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focused.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Accent), "Chrome"));
            template.Triggers.Add(focused);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.46, "Chrome"));
            template.Triggers.Add(disabled);
            return template;
        }

        TextBox StyledTextBox()
        {
            return new TextBox
            {
                FontFamily = new FontFamily("Segoe UI"), FontSize = 13, Padding = new Thickness(11, 7, 11, 7),
                Height = 38, Background = Brush(Raised), Foreground = Brush(TextPrimary),
                BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1), CaretBrush = Brush(Accent),
                VerticalContentAlignment = VerticalAlignment.Center, Template = RoundedTextBoxTemplate(),
                SnapsToDevicePixels = true
            };
        }

        ControlTemplate RoundedTextBoxTemplate()
        {
            var frame = new FrameworkElementFactory(typeof(Border)); frame.Name = "Chrome";
            frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            frame.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            frame.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            frame.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var host = new FrameworkElementFactory(typeof(ScrollViewer)); host.Name = "PART_ContentHost";
            host.SetValue(ScrollViewer.FocusableProperty, false);
            host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            host.SetValue(Control.BackgroundProperty, Brushes.Transparent);
            frame.AppendChild(host);
            var template = new ControlTemplate(typeof(TextBox)) { VisualTree = frame };
            var focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focused.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Accent), "Chrome"));
            template.Triggers.Add(focused);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
            template.Triggers.Add(disabled);
            return template;
        }

        PasswordBox StyledPasswordBox()
        {
            return new PasswordBox
            {
                FontFamily = new FontFamily("Segoe UI"), FontSize = 13, Padding = new Thickness(11, 7, 11, 7),
                Height = 38, Background = Brush(Raised), Foreground = Brush(TextPrimary),
                BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1), CaretBrush = Brush(Accent),
                PasswordChar = '●', Template = RoundedPasswordBoxTemplate(), SnapsToDevicePixels = true
            };
        }

        ControlTemplate RoundedPasswordBoxTemplate()
        {
            var frame = new FrameworkElementFactory(typeof(Border)); frame.Name = "Chrome";
            frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            frame.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            frame.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            frame.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var host = new FrameworkElementFactory(typeof(ScrollViewer)); host.Name = "PART_ContentHost";
            host.SetValue(ScrollViewer.FocusableProperty, false);
            host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            host.SetValue(Control.BackgroundProperty, Brushes.Transparent);
            frame.AppendChild(host);
            var template = new ControlTemplate(typeof(PasswordBox)) { VisualTree = frame };
            var focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focused.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Accent), "Chrome"));
            template.Triggers.Add(focused);
            return template;
        }

        ControlTemplate ModernCheckBoxTemplate()
        {
            var grid = new FrameworkElementFactory(typeof(Grid)); grid.SetValue(FrameworkElement.MinHeightProperty, 26.0);
            var first = new FrameworkElementFactory(typeof(ColumnDefinition)); first.SetValue(ColumnDefinition.WidthProperty, new GridLength(24)); grid.AppendChild(first);
            grid.AppendChild(new FrameworkElementFactory(typeof(ColumnDefinition)));
            var box = new FrameworkElementFactory(typeof(Border)); box.Name = "CheckChrome";
            box.SetValue(Border.WidthProperty, 17.0); box.SetValue(Border.HeightProperty, 17.0);
            box.SetValue(Border.CornerRadiusProperty, new CornerRadius(5)); box.SetValue(Border.BackgroundProperty, Brush(Raised));
            box.SetValue(Border.BorderBrushProperty, Brush(BorderColor)); box.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); box.SetValue(Grid.ColumnProperty, 0);
            grid.AppendChild(box);
            var mark = new FrameworkElementFactory(typeof(Path)); mark.Name = "CheckMark";
            mark.SetValue(Path.DataProperty, Geometry.Parse("M 1,5 L 4,8 L 10,1"));
            mark.SetValue(Path.StrokeProperty, Brush(TextOnAccentColor())); mark.SetValue(Path.StrokeThicknessProperty, 2.0);
            mark.SetValue(Path.StrokeStartLineCapProperty, PenLineCap.Round); mark.SetValue(Path.StrokeEndLineCapProperty, PenLineCap.Round);
            mark.SetValue(FrameworkElement.WidthProperty, 11.0); mark.SetValue(FrameworkElement.HeightProperty, 9.0);
            mark.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            mark.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            mark.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            box.AppendChild(mark);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            content.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            content.SetValue(FrameworkElement.MarginProperty, new Thickness(3, 0, 0, 0));
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(Grid.ColumnProperty, 1); grid.AppendChild(content);
            var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = grid };
            var checkedState = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            checkedState.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Accent), "CheckChrome"));
            checkedState.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Accent), "CheckChrome"));
            checkedState.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "CheckMark"));
            template.Triggers.Add(checkedState);
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Accent), "CheckChrome"));
            template.Triggers.Add(hover);
            return template;
        }

        Style ModernComboStyle()
        {
            var style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brush(Raised)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(TextPrimary)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(BorderColor)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 5, 32, 5)));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 36.0));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brush(Panel)));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush(TextPrimary)));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 7, 9, 7)));
            itemStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            var itemBorder = new FrameworkElementFactory(typeof(Border)); itemBorder.Name = "ItemBorder";
            itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            itemBorder.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var itemContent = new FrameworkElementFactory(typeof(ContentPresenter));
            itemContent.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            itemContent.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            itemContent.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            itemBorder.AppendChild(itemContent);
            var itemTemplate = new ControlTemplate(typeof(ComboBoxItem)) { VisualTree = itemBorder };
            var highlighted = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
            highlighted.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Accent), "ItemBorder"));
            highlighted.Setters.Add(new Setter(Control.ForegroundProperty, Brush(TextOnAccentColor())));
            itemTemplate.Triggers.Add(highlighted);
            var selected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Accent), "ItemBorder"));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, Brush(TextOnAccentColor())));
            itemTemplate.Triggers.Add(selected);
            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));
            style.Setters.Add(new Setter(ItemsControl.ItemContainerStyleProperty, itemStyle));

            var root = new FrameworkElementFactory(typeof(Grid));
            var chrome = new FrameworkElementFactory(typeof(Border)); chrome.Name = "Chrome";
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            chrome.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            chrome.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            chrome.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            root.AppendChild(chrome);
            var selection = new FrameworkElementFactory(typeof(ContentPresenter));
            selection.SetBinding(ContentPresenter.ContentProperty, new Binding("SelectionBoxItem") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            selection.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 0, 34, 0));
            selection.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            selection.SetValue(UIElement.IsHitTestVisibleProperty, false);
            root.AppendChild(selection);
            var toggle = new FrameworkElementFactory(typeof(ToggleButton)); toggle.Name = "DropToggle";
            toggle.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            toggle.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            toggle.SetValue(Control.BackgroundProperty, Brushes.Transparent); toggle.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            toggle.SetValue(UIElement.FocusableProperty, false); toggle.SetValue(ButtonBase.ClickModeProperty, ClickMode.Press);
            toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.TwoWay });
            // Le fond transparent du modèle rend toute la zone cliquable : sans lui,
            // seul le trait de la flèche réagissait au clic.
            var arrowRoot = new FrameworkElementFactory(typeof(Grid));
            arrowRoot.SetValue(Grid.BackgroundProperty, Brushes.Transparent);
            var arrow = new FrameworkElementFactory(typeof(Path));
            arrow.SetValue(Path.DataProperty, Geometry.Parse("M 0,0 L 5,5 L 10,0"));
            arrow.SetValue(Path.StrokeProperty, Brush(Muted)); arrow.SetValue(Path.StrokeThicknessProperty, 1.5);
            arrow.SetValue(FrameworkElement.WidthProperty, 10.0); arrow.SetValue(FrameworkElement.HeightProperty, 6.0);
            arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 12, 0));
            arrowRoot.AppendChild(arrow);
            var arrowTemplate = new ControlTemplate(typeof(ToggleButton)) { VisualTree = arrowRoot };
            toggle.SetValue(Control.TemplateProperty, arrowTemplate); root.AppendChild(toggle);
            var popup = new FrameworkElementFactory(typeof(Popup)); popup.Name = "PART_Popup";
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom); popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.FocusableProperty, false); popup.SetValue(Popup.VerticalOffsetProperty, 4.0);
            popup.SetBinding(Popup.PlacementTargetProperty, new Binding("") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.BackgroundProperty, Brush(Panel)); popupBorder.SetValue(Border.BorderBrushProperty, Brush(BorderColor));
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1)); popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            popupBorder.SetValue(Border.PaddingProperty, new Thickness(4));
            popupBorder.SetBinding(FrameworkElement.MinWidthProperty, new Binding("ActualWidth") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var scroll = new FrameworkElementFactory(typeof(ScrollViewer)); scroll.SetValue(ScrollViewer.MaxHeightProperty, 300.0);
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, true); scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
            popupBorder.AppendChild(scroll); popup.AppendChild(popupBorder); root.AppendChild(popup);
            var comboTemplate = new ControlTemplate(typeof(ComboBox)) { VisualTree = root };
            var open = new Trigger { Property = ComboBox.IsDropDownOpenProperty, Value = true };
            open.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Accent), "Chrome"));
            comboTemplate.Triggers.Add(open);
            style.Setters.Add(new Setter(Control.TemplateProperty, comboTemplate));
            return style;
        }

        /// <summary>
        /// Curseur de l'application : barre fine arrondie, remplissage accentué et
        /// bouton qui grossit au survol, dans le même langage que la barre de lecture.
        /// </summary>
        Style ModernSliderStyle()
        {
            return SliderStyle(WithAlpha(TextPrimary, 46), TextPrimary, Accent);
        }

        /// <summary>
        /// Curseur de volume du lecteur, dans le même langage que la barre de
        /// lecture : rail fin et translucide sur une carte toujours sombre, un
        /// seul remplissage couleur d'accent, une pastille blanche discrète qui
        /// s'éclaire au survol et un anneau accent pendant le glissement.
        /// </summary>
        Style PlayerVolumeSliderStyle()
        {
            var style = new Style(typeof(Slider));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 30.0));
            style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Control.TemplateProperty, VolumeSliderTemplate()));
            return style;
        }

        ControlTemplate VolumeSliderTemplate()
        {
            string accent = Hex(Accent);
            string accentLight = Hex(LightMix(Accent, Colors.White, 0.3));
            // Le réglage vit sur une carte toujours sombre : la piste reste
            // blanc translucide quel que soit le thème (TextPrimary serait noir
            // en mode clair et ferait disparaître le rail).
            string rail = Hex(WithAlpha(Colors.White, 36));
            string railHover = Hex(WithAlpha(Colors.White, 62));
            string halo = Hex(WithAlpha(Accent, 58));
            return ParseTemplate(
                "<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"{x:Type Slider}\">" +
                "<Grid Height=\"30\" Background=\"Transparent\">" +
                "<Border x:Name=\"Rail\" Height=\"4\" CornerRadius=\"2\" VerticalAlignment=\"Center\" Background=\"" + rail + "\"/>" +
                "<Track x:Name=\"PART_Track\" VerticalAlignment=\"Center\" Height=\"30\">" +
                "<Track.DecreaseRepeatButton>" +
                "<RepeatButton Command=\"{x:Static Slider.DecreaseLarge}\" Focusable=\"False\" IsTabStop=\"False\">" +
                "<RepeatButton.Template><ControlTemplate TargetType=\"{x:Type RepeatButton}\">" +
                "<Grid Background=\"Transparent\">" +
                "<Border Height=\"4\" CornerRadius=\"2\" VerticalAlignment=\"Center\">" +
                "<Border.Background><LinearGradientBrush StartPoint=\"0,0\" EndPoint=\"1,0\">" +
                "<GradientStop Color=\"" + accent + "\" Offset=\"0\"/>" +
                "<GradientStop Color=\"" + accentLight + "\" Offset=\"1\"/>" +
                "</LinearGradientBrush></Border.Background>" +
                "</Border></Grid></ControlTemplate></RepeatButton.Template></RepeatButton>" +
                "</Track.DecreaseRepeatButton>" +
                "<Track.Thumb>" +
                "<Thumb x:Name=\"Knob\" Focusable=\"False\">" +
                "<Thumb.Template><ControlTemplate TargetType=\"{x:Type Thumb}\">" +
                "<Grid Width=\"26\" Height=\"30\" Background=\"Transparent\">" +
                "<Ellipse x:Name=\"Halo\" Width=\"22\" Height=\"22\" Fill=\"" + halo + "\" Opacity=\"0\"/>" +
                "<Border x:Name=\"Dot\" Width=\"11\" Height=\"11\" CornerRadius=\"5.5\" Background=\"#FFFFFFFF\" " +
                "HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\">" +
                "<Border.Effect><DropShadowEffect Color=\"Black\" BlurRadius=\"9\" ShadowDepth=\"0\" Opacity=\"0.5\"/></Border.Effect>" +
                "</Border></Grid>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property=\"IsMouseOver\" Value=\"True\">" +
                "<Setter TargetName=\"Dot\" Property=\"Width\" Value=\"13\"/><Setter TargetName=\"Dot\" Property=\"Height\" Value=\"13\"/><Setter TargetName=\"Dot\" Property=\"CornerRadius\" Value=\"6.5\"/>" +
                "<Setter TargetName=\"Halo\" Property=\"Opacity\" Value=\"0.85\"/></Trigger>" +
                "<Trigger Property=\"IsDragging\" Value=\"True\">" +
                "<Setter TargetName=\"Dot\" Property=\"Width\" Value=\"13\"/><Setter TargetName=\"Dot\" Property=\"Height\" Value=\"13\"/><Setter TargetName=\"Dot\" Property=\"CornerRadius\" Value=\"6.5\"/>" +
                "<Setter TargetName=\"Halo\" Property=\"Opacity\" Value=\"1\"/></Trigger>" +
                "</ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb>" +
                "</Track.Thumb>" +
                "<Track.IncreaseRepeatButton>" +
                "<RepeatButton Command=\"{x:Static Slider.IncreaseLarge}\" Focusable=\"False\" IsTabStop=\"False\">" +
                "<RepeatButton.Template><ControlTemplate TargetType=\"{x:Type RepeatButton}\"><Grid Background=\"Transparent\"/></ControlTemplate></RepeatButton.Template></RepeatButton>" +
                "</Track.IncreaseRepeatButton>" +
                "</Track></Grid>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property=\"IsMouseOver\" Value=\"True\">" +
                "<Setter TargetName=\"Rail\" Property=\"Background\" Value=\"" + railHover + "\"/></Trigger>" +
                "</ControlTemplate.Triggers></ControlTemplate>");
        }

        static string Hex(Color value)
        {
            return "#" + value.A.ToString("X2") + value.R.ToString("X2") + value.G.ToString("X2") + value.B.ToString("X2");
        }

        static Color LightMix(Color from, Color to, double amount)
        {
            if (amount < 0) amount = 0;
            if (amount > 1) amount = 1;
            return Color.FromArgb(255,
                (byte)Math.Round(from.R + (to.R - from.R) * amount),
                (byte)Math.Round(from.G + (to.G - from.G) * amount),
                (byte)Math.Round(from.B + (to.B - from.B) * amount));
        }

        Style SliderStyle(Color track, Color thumb, Color fill)
        {
            var style = new Style(typeof(Slider));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Accent)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 26.0));
            style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Control.TemplateProperty, SliderTemplate(track, thumb, fill)));
            return style;
        }

        static ControlTemplate SliderTemplate(Color track, Color thumb, Color fill)
        {
            string trackHex = track.ToString();
            string thumbHex = thumb.ToString();
            string fillHex = fill.ToString();
            return ParseTemplate(
                "<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"{x:Type Slider}\">" +
                "<Grid Height=\"26\" Background=\"Transparent\">" +
                "<Border Height=\"5\" CornerRadius=\"3\" VerticalAlignment=\"Center\" Background=\"" + trackHex + "\"/>" +
                "<Track x:Name=\"PART_Track\" VerticalAlignment=\"Center\" Height=\"26\">" +
                "<Track.DecreaseRepeatButton>" +
                "<RepeatButton Command=\"{x:Static Slider.DecreaseLarge}\" Focusable=\"False\" IsTabStop=\"False\">" +
                "<RepeatButton.Template><ControlTemplate TargetType=\"{x:Type RepeatButton}\">" +
                "<Grid Background=\"Transparent\">" +
                "<Border Height=\"5\" CornerRadius=\"3\" VerticalAlignment=\"Center\" Background=\"" + fillHex + "\">" +
                "<Border.Effect><DropShadowEffect Color=\"" + fillHex + "\" BlurRadius=\"7\" ShadowDepth=\"0\" Opacity=\"0.5\"/></Border.Effect>" +
                "</Border></Grid></ControlTemplate></RepeatButton.Template></RepeatButton>" +
                "</Track.DecreaseRepeatButton>" +
                "<Track.Thumb>" +
                "<Thumb x:Name=\"Knob\" Focusable=\"False\">" +
                "<Thumb.Template><ControlTemplate TargetType=\"{x:Type Thumb}\">" +
                "<Grid Width=\"26\" Height=\"26\" Background=\"Transparent\">" +
                "<Border x:Name=\"Dot\" Width=\"13\" Height=\"13\" CornerRadius=\"7\" Background=\"" + thumbHex + "\" " +
                "HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\">" +
                "<Border.Effect><DropShadowEffect Color=\"Black\" BlurRadius=\"6\" ShadowDepth=\"1\" Opacity=\"0.42\"/></Border.Effect>" +
                "</Border></Grid>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property=\"IsMouseOver\" Value=\"True\"><Setter TargetName=\"Dot\" Property=\"Width\" Value=\"15\"/><Setter TargetName=\"Dot\" Property=\"Height\" Value=\"15\"/></Trigger>" +
                "<Trigger Property=\"IsDragging\" Value=\"True\"><Setter TargetName=\"Dot\" Property=\"Width\" Value=\"15\"/><Setter TargetName=\"Dot\" Property=\"Height\" Value=\"15\"/></Trigger>" +
                "</ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb>" +
                "</Track.Thumb>" +
                "<Track.IncreaseRepeatButton>" +
                "<RepeatButton Command=\"{x:Static Slider.IncreaseLarge}\" Focusable=\"False\" IsTabStop=\"False\">" +
                "<RepeatButton.Template><ControlTemplate TargetType=\"{x:Type RepeatButton}\"><Grid Background=\"Transparent\"/></ControlTemplate></RepeatButton.Template>" +
                "</RepeatButton></Track.IncreaseRepeatButton>" +
                "</Track></Grid></ControlTemplate>");
        }

        Style ModernScrollBarStyle()
        {
            var style = new Style(typeof(ScrollBar));
            style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 14.0));
            style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 14.0));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.TemplateProperty, ParseTemplate(VerticalScrollBarTemplateXaml())));
            var horizontal = new Trigger { Property = ScrollBar.OrientationProperty, Value = Orientation.Horizontal };
            horizontal.Setters.Add(new Setter(FrameworkElement.WidthProperty, Double.NaN));
            horizontal.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 0.0));
            horizontal.Setters.Add(new Setter(FrameworkElement.HeightProperty, 14.0));
            horizontal.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 14.0));
            horizontal.Setters.Add(new Setter(Control.TemplateProperty, ParseTemplate(HorizontalScrollBarTemplateXaml())));
            style.Triggers.Add(horizontal);
            return style;
        }

        static ControlTemplate ParseTemplate(string xaml)
        {
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        static string ScrollBarTemplateXaml(bool horizontal)
        {
            string direction = horizontal ? "False" : "True";
            string thumbSize = horizontal ? "Height=\"6\" MinWidth=\"40\" Margin=\"2,4,2,4\" HorizontalAlignment=\"Stretch\" VerticalAlignment=\"Center\""
                : "Width=\"6\" MinHeight=\"40\" Margin=\"4,2,4,2\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Stretch\"";
            string hoverSize = horizontal ? "Height" : "Width";
            string decrease = horizontal ? "ScrollBar.PageLeftCommand" : "ScrollBar.PageUpCommand";
            string increase = horizontal ? "ScrollBar.PageRightCommand" : "ScrollBar.PageDownCommand";
            return "<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"{x:Type ScrollBar}\">" +
                "<Grid Background=\"Transparent\">" +
                "<Track x:Name=\"PART_Track\" IsDirectionReversed=\"" + direction + "\">" +
                "<Track.DecreaseRepeatButton><RepeatButton Command=\"{x:Static " + decrease + "}\" Opacity=\"0\" Focusable=\"False\" IsTabStop=\"False\"/></Track.DecreaseRepeatButton>" +
                "<Track.Thumb><Thumb x:Name=\"Thumb\" " + thumbSize + ">" +
                "<Thumb.Template><ControlTemplate TargetType=\"{x:Type Thumb}\">" +
                "<Border x:Name=\"ThumbBorder\" CornerRadius=\"3\" Background=\"{DynamicResource LyaranScrollThumb}\">" +
                "<Border.Effect><DropShadowEffect Color=\"{DynamicResource LyaranScrollGlow}\" BlurRadius=\"8\" ShadowDepth=\"0\" Opacity=\"0.28\"/></Border.Effect>" +
                "</Border>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property=\"IsMouseOver\" Value=\"True\"><Setter TargetName=\"ThumbBorder\" Property=\"Background\" Value=\"{DynamicResource LyaranScrollThumbHover}\"/></Trigger>" +
                "<Trigger Property=\"IsDragging\" Value=\"True\"><Setter TargetName=\"ThumbBorder\" Property=\"Background\" Value=\"{DynamicResource LyaranScrollThumbActive}\"/></Trigger>" +
                "</ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>" +
                "<Track.IncreaseRepeatButton><RepeatButton Command=\"{x:Static " + increase + "}\" Opacity=\"0\" Focusable=\"False\" IsTabStop=\"False\"/></Track.IncreaseRepeatButton>" +
                "</Track></Grid>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property=\"IsMouseOver\" Value=\"True\"><Setter TargetName=\"Thumb\" Property=\"" + hoverSize + "\" Value=\"10\"/></Trigger>" +
                "</ControlTemplate.Triggers></ControlTemplate>";
        }

        static string VerticalScrollBarTemplateXaml() { return ScrollBarTemplateXaml(false); }
        static string HorizontalScrollBarTemplateXaml() { return ScrollBarTemplateXaml(true); }

        Style ModernScrollViewerStyle()
        {
            var style = new Style(typeof(ScrollViewer));
            style.Setters.Add(new Setter(ModernScroll.EnabledProperty, true));
            return style;
        }

        ProgressBar StyledProgressBar()
        {
            var grid = new FrameworkElementFactory(typeof(Grid));
            var track = new FrameworkElementFactory(typeof(Border));
            track.SetValue(Border.BackgroundProperty, Brush(Raised)); track.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            track.SetValue(FrameworkElement.HeightProperty, 6.0); track.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            grid.AppendChild(track);
            var indicator = new FrameworkElementFactory(typeof(Border)); indicator.Name = "PART_Indicator";
            indicator.SetValue(Border.BackgroundProperty, Brush(Accent)); indicator.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            indicator.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            indicator.SetValue(FrameworkElement.HeightProperty, 6.0); indicator.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            grid.AppendChild(indicator);
            var template = new ControlTemplate(typeof(ProgressBar)) { VisualTree = grid };
            return new ProgressBar { Height = 6, Minimum = 0, Maximum = 100, Template = template, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        }

        Border Card(UIElement child, double padding)
        {
            return new Border
            {
                Child = child, Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14), Padding = new Thickness(padding), Margin = new Thickness(0, 0, 0, 14),
                SnapsToDevicePixels = true
            };
        }

        Border StatCard(string glyph, string value, string label, Color tint)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var iconBorder = new Border
            {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = Brush(WithAlpha(tint, 34)),
                VerticalAlignment = VerticalAlignment.Top, Child = Icon(glyph, 16, tint), HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(iconBorder, 0); grid.Children.Add(iconBorder);
            var stack = new StackPanel(); Grid.SetColumn(stack, 1); grid.Children.Add(stack);
            stack.Children.Add(Text(value, 22, Brush(TextPrimary), true));
            stack.Children.Add(Text(label, 11, Brush(Muted), false));
            var card = Card(grid, 15);
            card.Width = 178; card.Margin = new Thickness(0, 0, 12, 12);
            return card;
        }

        Border Badge(string label, Brush background, Brush foreground)
        {
            return new Border
            {
                Background = background, CornerRadius = new CornerRadius(99), Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center, Child = Text(label, 10.5, foreground, true)
            };
        }

        DockPanel SectionHeader(string title, string subtitle, Button action)
        {
            // Un titre d'étagère suffit : la phrase d'explication sous chaque
            // rangée encombrait l'accueil. Elle reste affichée seulement pour
            // les en-têtes qui n'ont pas d'action (« Personnes », résultats…),
            // où elle porte une information utile.
            var dock = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 12, 0, 12) };
            if (action != null) { action.Margin = new Thickness(10, 0, 0, 0); DockPanel.SetDock(action, Dock.Right); dock.Children.Add(action); }
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(Text(title, 19, Brush(TextPrimary), true));
            if (action == null && !String.IsNullOrWhiteSpace(subtitle)) stack.Children.Add(Text(subtitle, 12, Brush(Muted), false));
            dock.Children.Add(stack);
            return dock;
        }

        Border EmptyState(string glyph, string title, string message, Button action)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 10, 20, 10) };
            var icon = Icon(glyph, 38, Brush(Accent)); icon.HorizontalAlignment = HorizontalAlignment.Center; icon.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(icon);
            var heading = Text(title, 18, Brush(TextPrimary), true); heading.HorizontalAlignment = HorizontalAlignment.Center; heading.TextAlignment = TextAlignment.Center;
            stack.Children.Add(heading);
            var body = Text(message, 12, Brush(Muted), false); body.HorizontalAlignment = HorizontalAlignment.Center; body.TextAlignment = TextAlignment.Center;
            body.MaxWidth = 520; body.Margin = new Thickness(0, 7, 0, 14); stack.Children.Add(body);
            if (action != null) { action.HorizontalAlignment = HorizontalAlignment.Center; action.Margin = new Thickness(0); stack.Children.Add(action); }
            return Card(stack, 28);
        }

        ContextMenu CreateContextMenu()
        {
            var menu = new ContextMenu
            {
                Background = Brush(Panel), Foreground = Brush(TextPrimary), BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1), Padding = new Thickness(4), HasDropShadow = true
            };
            var itemStyle = new Style(typeof(MenuItem));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush(TextPrimary)));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 12, 6)));
            itemStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            var itemBorder = new FrameworkElementFactory(typeof(Border)); itemBorder.Name = "ItemChrome";
            itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            itemBorder.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var itemContent = new FrameworkElementFactory(typeof(ContentPresenter));
            itemContent.SetBinding(ContentPresenter.ContentProperty, new Binding("Header") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            itemContent.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 3, 10, 3));
            itemContent.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            itemBorder.AppendChild(itemContent);
            var itemTemplate = new ControlTemplate(typeof(System.Windows.Controls.MenuItem)) { VisualTree = itemBorder };
            var hover = new Trigger { Property = System.Windows.Controls.MenuItem.IsHighlightedProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Raised), "ItemChrome"));
            itemTemplate.Triggers.Add(hover);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Muted)));
            itemTemplate.Triggers.Add(disabled);
            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));
            // Style implicite : il ne s'applique qu'aux MenuItem, jamais aux Separator.
            menu.Resources.Add(typeof(MenuItem), itemStyle);
            var separatorStyle = new Style(typeof(Separator));
            var separatorLine = new FrameworkElementFactory(typeof(Border));
            separatorLine.SetValue(Border.HeightProperty, 1.0);
            separatorLine.SetValue(Border.MarginProperty, new Thickness(6, 5, 6, 5));
            separatorLine.SetValue(Border.BackgroundProperty, Brush(BorderColor));
            separatorStyle.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Separator)) { VisualTree = separatorLine }));
            menu.Resources.Add(typeof(Separator), separatorStyle);
            var frame = new FrameworkElementFactory(typeof(Border));
            frame.SetValue(Border.BackgroundProperty, Brush(Panel)); frame.SetValue(Border.BorderBrushProperty, Brush(BorderColor));
            frame.SetValue(Border.BorderThicknessProperty, new Thickness(1)); frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            frame.SetValue(Border.PaddingProperty, new Thickness(4));
            frame.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
            menu.Template = new ControlTemplate(typeof(ContextMenu)) { VisualTree = frame };
            return menu;
        }

        MenuItem MenuItem(string header, Action click, string glyph)
        {
            var item = new MenuItem { Header = String.IsNullOrWhiteSpace(glyph) ? (object)header : IconText(glyph, header, Brush(TextPrimary), 12) };
            if (click != null) item.Click += delegate { click(); };
            return item;
        }

        /// <summary>Arrondit réellement le contenu d'un cadre (WPF ne découpe pas
        /// automatiquement les enfants sur le rayon de bordure).</summary>
        void RoundClip(FrameworkElement element, double radius)
        {
            if (element == null) return;
            Action apply = delegate
            {
                double width = element.ActualWidth, height = element.ActualHeight;
                if (width < 1 || height < 1) return;
                element.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
            };
            element.SizeChanged += delegate { apply(); };
            element.Loaded += delegate { apply(); };
            apply();
        }

        void AddHover(Border card, Brush normalBackground, Brush hoverBackground, Brush normalBorder, Brush hoverBorder)
        {
            card.MouseEnter += delegate { card.Background = hoverBackground; card.BorderBrush = hoverBorder; };
            card.MouseLeave += delegate { card.Background = normalBackground; card.BorderBrush = normalBorder; };
        }

        static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T)) current = VisualTreeHelper.GetParent(current);
            return current as T;
        }

        static bool IsInsideButton(DependencyObject current) { return FindAncestor<Button>(current) != null; }

        void ShowToast(string message, bool error, bool success)
        {
            if (toastHost == null) { if (statusText != null) statusText.Text = message; return; }
            if (toast == null)
            {
                toast = new Border
                {
                    CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 11, 16, 11), MaxWidth = 430,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 24, 48), Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.35, Color = Colors.Black }
                };
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                toastIcon = Icon(Glyphs.Info, 16, Brush(Accent)); toastIcon.Margin = new Thickness(0, 0, 9, 0);
                toastText = Text("", 12.5, Brush(TextPrimary), false); toastText.MaxWidth = 350; toastText.TextWrapping = TextWrapping.Wrap;
                panel.Children.Add(toastIcon); panel.Children.Add(toastText); toast.Child = panel;
                System.Windows.Controls.Panel.SetZIndex(toast, 200); toastHost.Children.Add(toast);
            }
            toast.Visibility = Visibility.Visible;
            toast.Background = Brush(Panel);
            toast.BorderBrush = Brush(error ? Danger : success ? Success : Accent);
            toast.BorderThickness = new Thickness(1);
            toastIcon.Text = error ? Glyphs.Error : success ? Glyphs.Success : Glyphs.Info;
            toastIcon.Foreground = Brush(error ? Danger : success ? Success : Accent);
            toastText.Text = message;
            toast.Opacity = 0;
            var translate = new TranslateTransform(0, 10); toast.RenderTransform = translate;
            toast.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(180)));
            if (toastTimer != null) toastTimer.Stop();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.2) };
            timer.Tick += delegate
            {
                timer.Stop();
                if (toast == null) return;
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                fade.Completed += delegate { if (toast != null) toast.Visibility = Visibility.Collapsed; };
                toast.BeginAnimation(UIElement.OpacityProperty, fade);
            };
            toastTimer = timer;
            timer.Start();
        }

        void Notice(string message) { ShowToast(message, false, true); }
        void ShowError(string message) { ShowToast(message, true, false); }

        bool AskConfirmation(string title, string message, string confirmLabel)
        {
            return ShowThemedDialog(title, message, confirmLabel, true, false) == true;
        }

        bool? ShowThemedDialog(string title, string message, string primaryLabel, bool showCancel, bool error)
        {
            var dialog = new Window
            {
                Title = title, Width = 500, SizeToContent = SizeToContent.Height, MaxHeight = 640,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None, Background = Brushes.Transparent, AllowsTransparency = true,
                FontFamily = new FontFamily("Segoe UI"), ShowInTaskbar = false
            };
            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16), Padding = new Thickness(24)
            };
            dialog.Content = frame;
            var root = new StackPanel(); frame.Child = root;
            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 14) };
            var close = IconButton(Glyphs.Close, "Fermer", delegate { dialog.DialogResult = false; }, false);
            close.Width = 32; close.Height = 30; close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var headStack = new StackPanel { Orientation = Orientation.Horizontal };
            var headIcon = Icon(error ? Glyphs.Error : Glyphs.Info, 18, Brush(error ? Danger : Accent)); headIcon.Margin = new Thickness(0, 0, 9, 0);
            headStack.Children.Add(headIcon); headStack.Children.Add(Text(title, 18, Brush(TextPrimary), true));
            header.Children.Add(headStack); root.Children.Add(header);
            var scroll = new ScrollViewer { MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var body = Text(message, 13, Brush(Muted), false); body.Margin = new Thickness(0, 0, 0, 22); body.TextWrapping = TextWrapping.Wrap;
            scroll.Content = body; root.Children.Add(scroll);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            if (showCancel)
            {
                var cancel = Button("Annuler", delegate { dialog.DialogResult = false; }, false); cancel.Margin = new Thickness(0, 0, 8, 0); actions.Children.Add(cancel);
            }
            var primary = Button(primaryLabel, delegate { dialog.DialogResult = true; }, true); primary.Margin = new Thickness(0); actions.Children.Add(primary);
            root.Children.Add(actions);
            return dialog.ShowDialog();
        }

        void ApplyDarkTitleBar()
        {
            try
            {
                int dark = 1, caption = ((int)Back.R << 16) | ((int)Back.G << 8) | Back.B, text = ((int)TextPrimary.R << 16) | ((int)TextPrimary.G << 8) | TextPrimary.B;
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)) != 0) DwmSetWindowAttribute(hwnd, 19, ref dark, sizeof(int));
                DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int)); DwmSetWindowAttribute(hwnd, 36, ref text, sizeof(int));
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        static void DrawLyaranMark(DrawingContext dc, double size, bool appIcon)
        {
            // Logo « Égaliseur » : cinq barres verticales et un triangle de lecture.
            // Le dessin reste vectoriel pour être net à toutes les tailles.
            double s = size / 64.0;
            dc.PushTransform(new ScaleTransform(s, s));
            var bounds = new Rect(3.5, 3.5, 57, 57);
            var background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            background.GradientStops.Add(new GradientStop(Color.FromRgb(96, 52, 169), 0));
            background.GradientStops.Add(new GradientStop(Color.FromRgb(57, 33, 113), 0.55));
            background.GradientStops.Add(new GradientStop(Color.FromRgb(27, 25, 63), 1));
            dc.DrawRoundedRectangle(background, null, bounds, 12.8, 12.8);

            var glow = new LinearGradientBrush { StartPoint = new Point(1, 0), EndPoint = new Point(0.15, 0.75) };
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(160, 62, 78, 166), 0));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(36, 62, 78, 166), 0.6));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, 62, 78, 166), 1));
            dc.DrawRoundedRectangle(glow, null, bounds, 12.8, 12.8);

            var white = new Pen(Brushes.White, 2.7);
            white.StartLineCap = PenLineCap.Round; white.EndLineCap = PenLineCap.Round;
            var play = new SolidColorBrush(Color.FromRgb(34, 211, 238));

            if (size < 30)
            {
                // Version simplifiée pour les icônes 16–24 px : trois barres
                // épaisses et un triangle plus large restent lisibles.
                var compact = new Pen(Brushes.White, 4.2);
                compact.StartLineCap = PenLineCap.Round; compact.EndLineCap = PenLineCap.Round;
                dc.DrawLine(compact, new Point(19.5, 26), new Point(19.5, 38));
                dc.DrawLine(compact, new Point(27, 17.5), new Point(27, 46.5));
                dc.DrawLine(compact, new Point(34.5, 26), new Point(34.5, 38));
                dc.DrawGeometry(play, null, Geometry.Parse("M 40,23.5 L 56,32 L 40,40.5 Z"));
            }
            else
            {
                var lavender = new Pen(new SolidColorBrush(Color.FromRgb(196, 181, 253)), 2.7);
                lavender.StartLineCap = PenLineCap.Round; lavender.EndLineCap = PenLineCap.Round;

                dc.DrawLine(white, new Point(18.5, 25.5), new Point(18.5, 38.5));
                dc.DrawLine(lavender, new Point(23.25, 21.25), new Point(23.25, 42.75));
                dc.DrawLine(white, new Point(28, 17.25), new Point(28, 46.75));
                dc.DrawLine(lavender, new Point(32.75, 21.25), new Point(32.75, 42.75));
                dc.DrawLine(white, new Point(37.5, 25.5), new Point(37.5, 38.5));
                dc.DrawGeometry(play, null, Geometry.Parse("M 40,23.1 L 55.4,32 L 40,40.9 Z"));
            }

            if (appIcon)
            {
                var rim = new Pen(new SolidColorBrush(Color.FromArgb(58, 255, 255, 255)), 0.35);
                dc.DrawRoundedRectangle(null, rim, new Rect(3.75, 3.75, 56.5, 56.5), 12.5, 12.5);
            }
            dc.Pop();
        }

        static BitmapSource CreateAppIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) DrawLyaranMark(dc, 64, true);
            var bitmap = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); bitmap.Freeze(); return bitmap;
        }

        internal static DrawingImage CreateLogoMark()
        {
            var group = new DrawingGroup();
            using (var dc = group.Open()) DrawLyaranMark(dc, 64, false);
            var drawing = new DrawingImage(group); drawing.Freeze(); return drawing;
        }

        void SetThemeColors(string theme)
        {
            themeName = String.IsNullOrWhiteSpace(theme) ? "Sombre" : theme;
            if (themeName != "Sombre" && themeName != "Clair" && themeName != "Noir AMOLED" && themeName != "Automatique") themeName = "Sombre";
            string resolved = themeName == "Automatique" ? DetectSystemTheme() : themeName;
            if (resolved == "Clair")
            {
                // Blanc cassé neutre : les affiches gardent leurs couleurs sans
                // être teintées par l'interface.
                Back = Color.FromRgb(244, 245, 248); Panel = Color.FromRgb(255, 255, 255); Raised = Color.FromRgb(236, 238, 244);
                Muted = Color.FromRgb(94, 100, 114); TextPrimary = Color.FromRgb(16, 18, 24); BorderColor = Color.FromRgb(218, 221, 230);
            }
            else if (resolved == "Noir AMOLED")
            {
                Back = Color.FromRgb(0, 0, 0); Panel = Color.FromRgb(9, 10, 14); Raised = Color.FromRgb(20, 22, 29);
                Muted = Color.FromRgb(154, 160, 173); TextPrimary = Color.FromRgb(245, 247, 251); BorderColor = Color.FromRgb(31, 35, 44);
            }
            else
            {
                // Charcoal cinéma : noir neutre plutôt que violet, pour que
                // l'accent reste la seule couleur forte de l'interface.
                Back = Color.FromRgb(7, 8, 11); Panel = Color.FromRgb(16, 18, 24); Raised = Color.FromRgb(24, 27, 35);
                Muted = Color.FromRgb(155, 163, 180); TextPrimary = Color.FromRgb(245, 247, 251); BorderColor = Color.FromRgb(36, 40, 51);
            }
            Success = Color.FromRgb(74, 222, 128); Danger = Color.FromRgb(248, 113, 113); Warning = Color.FromRgb(251, 191, 36);
            Secondary = Color.FromRgb(34, 211, 238);
            Color parsed;
            Accent = TryParseHexColor(accentHex, out parsed) ? parsed : DefaultAccent;
        }

        static string DetectSystemTheme()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    if (value is int && (int)value == 1) return "Clair";
                }
            }
            catch { }
            return "Sombre";
        }

        void ConfigureSystemResources()
        {
            if (Application.Current == null) return;
            var resources = Application.Current.Resources;
            resources[SystemColors.HighlightBrushKey] = Brush(Accent);
            resources[SystemColors.ControlBrushKey] = Brush(Raised);
            resources[SystemColors.ControlDarkBrushKey] = Brush(BorderColor);
            resources[SystemColors.ControlLightBrushKey] = Brush(Panel);
            resources[SystemColors.ScrollBarBrushKey] = Brush(WithAlpha(Accent, 102));
            resources[SystemColors.WindowBrushKey] = Brush(Panel);
            resources[SystemColors.WindowTextBrushKey] = Brush(TextPrimary);
            resources[SystemColors.ActiveBorderBrushKey] = Brush(Accent);
            resources["LyaranScrollThumb"] = Brush(WithAlpha(Accent, 102));
            resources["LyaranScrollThumbHover"] = Brush(WithAlpha(Accent, 179));
            resources["LyaranScrollThumbActive"] = Brush(Accent);
            resources["LyaranScrollGlow"] = Accent;
            resources[typeof(ScrollBar)] = ModernScrollBarStyle();
            resources[typeof(ScrollViewer)] = ModernScrollViewerStyle();
            var tip = new Style(typeof(ToolTip));
            tip.Setters.Add(new Setter(Control.BackgroundProperty, Brush(Panel)));
            tip.Setters.Add(new Setter(Control.ForegroundProperty, Brush(TextPrimary)));
            tip.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(BorderColor)));
            tip.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            tip.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 6, 9, 6)));
            tip.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            resources[typeof(ToolTip)] = tip;
        }

        void SetPlaceholder(TextBox box, TextBlock hint)
        {
            box.TextChanged += delegate { hint.Visibility = String.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed; };
            box.GotFocus += delegate { if (String.IsNullOrEmpty(box.Text)) hint.Visibility = Visibility.Visible; };
            hint.Visibility = String.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    internal static class UiMotion
    {
        public static bool Reduced;
        public static void Animate(UIElement element, DependencyProperty property, double to, int milliseconds)
        {
            if (element == null) return;
            element.BeginAnimation(property, null);
            if (Reduced || milliseconds <= 0) { element.SetValue(property, to); return; }
            element.BeginAnimation(property, new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        }
    }

    internal static class ModernScroll
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(ModernScroll), new PropertyMetadata(false, OnEnabledChanged));
        static readonly DependencyProperty TimerProperty = DependencyProperty.RegisterAttached(
            "Timer", typeof(DispatcherTimer), typeof(ModernScroll), new PropertyMetadata(null));
        static readonly DependencyProperty HooksProperty = DependencyProperty.RegisterAttached(
            "Hooks", typeof(bool), typeof(ModernScroll), new PropertyMetadata(false));

        public static bool GetEnabled(DependencyObject element) { return (bool)element.GetValue(EnabledProperty); }
        public static void SetEnabled(DependencyObject element, bool value) { element.SetValue(EnabledProperty, value); }

        static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var viewer = d as ScrollViewer;
            if (viewer == null) return;
            if ((bool)e.NewValue)
            {
                viewer.Loaded += ViewerLoaded;
                viewer.ScrollChanged += ViewerScrollChanged;
                viewer.MouseEnter += ViewerMouseEnter;
            }
            else
            {
                viewer.Loaded -= ViewerLoaded;
                viewer.ScrollChanged -= ViewerScrollChanged;
                viewer.MouseEnter -= ViewerMouseEnter;
            }
        }

        static void ViewerLoaded(object sender, RoutedEventArgs e)
        {
            var viewer = sender as ScrollViewer;
            if (viewer == null) return;
            viewer.ApplyTemplate();
            AttachBars(viewer);
            foreach (var bar in FindBars(viewer)) bar.Opacity = 0;
        }

        static void ViewerMouseEnter(object sender, MouseEventArgs e)
        {
            Reveal((ScrollViewer)sender);
        }

        static void ViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var viewer = sender as ScrollViewer;
            if (viewer == null) return;
            AttachBars(viewer);
            Reveal(viewer);
        }

        static void AttachBars(ScrollViewer viewer)
        {
            foreach (var bar in FindBars(viewer))
            {
                if ((bool)bar.GetValue(HooksProperty)) continue;
                bar.SetValue(HooksProperty, true);
                bar.MouseEnter += BarMouseEnter;
                bar.MouseLeave += BarMouseLeave;
            }
        }

        static void BarMouseEnter(object sender, MouseEventArgs e)
        {
            var bar = (ScrollBar)sender;
            var timer = bar.GetValue(TimerProperty) as DispatcherTimer;
            if (timer != null) timer.Stop();
            SetOpacity(bar, 1, true);
        }

        static void BarMouseLeave(object sender, MouseEventArgs e)
        {
            var bar = (ScrollBar)sender;
            ScheduleFade(bar);
        }

        static void Reveal(ScrollViewer viewer)
        {
            foreach (var bar in FindBars(viewer))
            {
                SetOpacity(bar, 1, true);
                ScheduleFade(bar);
            }
        }

        static void ScheduleFade(ScrollBar bar)
        {
            var old = bar.GetValue(TimerProperty) as DispatcherTimer;
            if (old != null) old.Stop();
            if (UiMotion.Reduced) { SetOpacity(bar, 1, false); return; }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += delegate
            {
                timer.Stop();
                if (!bar.IsMouseOver) SetOpacity(bar, 0, true);
            };
            bar.SetValue(TimerProperty, timer);
            timer.Start();
        }

        static void SetOpacity(UIElement element, double value, bool animate)
        {
            if (element == null) return;
            element.BeginAnimation(UIElement.OpacityProperty, null);
            if (!animate || UiMotion.Reduced) { element.Opacity = value; return; }
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(value, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        }

        static IEnumerable<ScrollBar> FindBars(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var bar = child as ScrollBar;
                if (bar != null) yield return bar;
                foreach (var nested in FindBars(child)) yield return nested;
            }
        }
    }
}
