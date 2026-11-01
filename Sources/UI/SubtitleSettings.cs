using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LyaranTV
{
    /// <summary>
    /// Réglages de sous-titres accessibles directement depuis le lecteur :
    /// taille, couleur du texte, fond (couleur et opacité) et contour. Un aperçu
    /// reprend exactement le rendu du lecteur avant application.
    /// </summary>
    public sealed partial class MainWindow
    {
        static readonly string[] SubtitlePalette =
        {
            "#FFFFFF", "#FFF176", "#80DEEA", "#A5D6A7",
            "#F48FB1", "#FFB74D", "#B39DDB", "#000000"
        };

        void OpenSubtitleSettings()
        {
            string textColor = settings.SubtitleTextColor;
            string backgroundColor = settings.SubtitleBackgroundColor;
            double opacity = settings.SubtitleBackgroundOpacity;
            bool outline = settings.SubtitleOutline;
            bool backgroundEnabled = settings.SubtitleBackground;
            double size = settings.SubtitleSize;
            if (size < 12) size = 16;
            if (size > 30) size = 30;
            if (opacity < 0) opacity = 0;
            if (opacity > 1) opacity = 1;

            var window = new Window
            {
                Title = "Réglages des sous-titres",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 590,
                SizeToContent = SizeToContent.Height,
                MaxHeight = 760,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                FontFamily = new FontFamily("Inter, Segoe UI")
            };
            var frame = new Border
            {
                Margin = new Thickness(2), Background = Brush(Panel), BorderBrush = Brush(BorderColor),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20), Padding = new Thickness(24, 20, 24, 20),
                Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 9, Opacity = 0.36, Color = Colors.Black }
            };
            window.Content = frame;
            window.MouseLeftButtonDown += delegate { try { window.DragMove(); } catch { } };

            var stack = new StackPanel();
            frame.Child = stack;

            var head = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 14) };
            var close = IconButton(Glyphs.Close, "Fermer", delegate { window.Close(); }, false);
            close.Width = 34; close.Height = 34; close.Margin = new Thickness(0);
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            var titles = new StackPanel();
            titles.Children.Add(Text("SOUS-TITRES", 12.5, Brush(Accent), true));
            titles.Children.Add(Text("Apparence des sous-titres", 23, Brush(TextPrimary), true));
            head.Children.Add(titles);
            stack.Children.Add(head);

            // Aperçu sur fond sombre, comme dans le lecteur.
            var previewText = Text("Exemple de sous-titre affiché pendant la lecture.", 18, Brushes.White, false);
            previewText.TextAlignment = TextAlignment.Center;
            previewText.TextWrapping = TextWrapping.Wrap;
            previewText.MaxWidth = 430;
            previewText.HorizontalAlignment = HorizontalAlignment.Center;
            var previewBox = new Border
            {
                Child = previewText, CornerRadius = new CornerRadius(8), Padding = new Thickness(13, 6, 13, 7),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16)
            };
            var previewStage = new Border
            {
                Background = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(12, 12, 20), 0),
                    new GradientStop(Color.FromRgb(30, 24, 52), 0.55),
                    new GradientStop(Color.FromRgb(8, 8, 14), 1)
                }, new Point(0, 0), new Point(1, 1)),
                CornerRadius = new CornerRadius(13), Padding = new Thickness(18, 22, 18, 22),
                Margin = new Thickness(0, 0, 0, 18), Child = previewBox
            };
            stack.Children.Add(previewStage);

            Action refresh = null;

            // Taille.
            var sizeRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 12) };
            var sizeLabel = Text("Taille", 13, Brush(TextPrimary), true);
            sizeLabel.Width = 150;
            DockPanel.SetDock(sizeLabel, Dock.Left);
            sizeRow.Children.Add(sizeLabel);
            var sizePicker = new ComboBox { Width = 110, Height = 34, Style = ModernComboStyle() };
            foreach (string value in new[] { "14", "16", "18", "20", "22", "24", "26", "28", "30" })
                sizePicker.Items.Add(value);
            sizePicker.SelectedItem = ((int)Math.Round(size)).ToString();
            if (sizePicker.SelectedItem == null) sizePicker.SelectedItem = "16";
            sizePicker.SelectionChanged += delegate
            {
                if (sizePicker.SelectedItem == null) return;
                double parsed;
                if (Double.TryParse((string)sizePicker.SelectedItem, out parsed)) size = parsed;
                if (refresh != null) refresh();
            };
            DockPanel.SetDock(sizePicker, Dock.Left);
            sizeRow.Children.Add(sizePicker);
            stack.Children.Add(sizeRow);

            // Couleur du texte.
            stack.Children.Add(BuildSubtitleColorRow("Couleur du texte", textColor, delegate(string value)
            {
                textColor = value;
                if (refresh != null) refresh();
            }));

            // Couleur du fond.
            stack.Children.Add(BuildSubtitleColorRow("Couleur du fond", backgroundColor, delegate(string value)
            {
                backgroundColor = value;
                if (refresh != null) refresh();
            }));

            // Fond activé.
            var backgroundToggle = new CheckBox
            {
                Content = Text("Afficher un fond derrière le texte", 13, Brush(TextPrimary), false),
                IsChecked = backgroundEnabled, Template = ModernCheckBoxTemplate(),
                Foreground = Brush(TextPrimary), Margin = new Thickness(0, 4, 0, 12), Cursor = Cursors.Hand
            };
            backgroundToggle.Checked += delegate { backgroundEnabled = true; if (refresh != null) refresh(); };
            backgroundToggle.Unchecked += delegate { backgroundEnabled = false; if (refresh != null) refresh(); };
            stack.Children.Add(backgroundToggle);

            // Opacité du fond.
            var opacityRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 12) };
            var opacityLabelTitle = Text("Opacité du fond", 13, Brush(TextPrimary), true);
            opacityLabelTitle.Width = 150;
            DockPanel.SetDock(opacityLabelTitle, Dock.Left);
            opacityRow.Children.Add(opacityLabelTitle);
            var opacitySlider = new Slider
            {
                Minimum = 0, Maximum = 100, Value = opacity * 100, Width = 220,
                Style = ModernSliderStyle(), VerticalAlignment = VerticalAlignment.Center,
                IsSnapToTickEnabled = true, TickFrequency = 5, Margin = new Thickness(0, 0, 12, 0)
            };
            var opacityValue = Text(Math.Round(opacity * 100) + " %", 13, Brush(TextPrimary), true);
            opacityValue.Width = 52;
            opacitySlider.ValueChanged += delegate
            {
                opacity = opacitySlider.Value / 100.0;
                if (refresh != null) refresh();
            };
            DockPanel.SetDock(opacitySlider, Dock.Left);
            opacityRow.Children.Add(opacitySlider);
            DockPanel.SetDock(opacityValue, Dock.Left);
            opacityRow.Children.Add(opacityValue);
            stack.Children.Add(opacityRow);

            // Contour.
            var outlineToggle = new CheckBox
            {
                Content = Text("Contour noir autour du texte (meilleur contraste sur image claire)", 13, Brush(TextPrimary), false),
                IsChecked = outline, Template = ModernCheckBoxTemplate(),
                Foreground = Brush(TextPrimary), Margin = new Thickness(0, 0, 0, 4), Cursor = Cursors.Hand
            };
            outlineToggle.Checked += delegate { outline = true; if (refresh != null) refresh(); };
            outlineToggle.Unchecked += delegate { outline = false; if (refresh != null) refresh(); };
            stack.Children.Add(outlineToggle);

            refresh = delegate
            {
                Color text;
                if (!TryParseHexColor(textColor, out text)) text = Colors.White;
                Color back;
                if (!TryParseHexColor(backgroundColor, out back)) back = Colors.Black;
                previewText.Foreground = Brush(text);
                previewText.FontSize = size;
                previewText.LineHeight = size * 1.36;
                double alpha = backgroundEnabled ? opacity : 0;
                previewBox.Background = alpha > 0.001
                    ? Brush(Color.FromArgb((byte)Math.Round(alpha * 255), back.R, back.G, back.B))
                    : Brushes.Transparent;
                previewBox.Padding = alpha > 0.001 ? new Thickness(13, 6, 13, 7) : new Thickness(8, 2, 8, 2);
                previewText.Effect = outline
                    ? new DropShadowEffect { BlurRadius = 3.5, ShadowDepth = 0, Opacity = 0.96, Color = Colors.Black }
                    : null;
                opacityValue.Text = Math.Round(opacity * 100) + " %";
                opacitySlider.IsEnabled = backgroundEnabled;
                opacitySlider.Opacity = backgroundEnabled ? 1 : 0.45;
            };
            refresh();

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var reset = Button("Réinitialiser", delegate
            {
                settings.SubtitleSize = 16;
                settings.SubtitleTextColor = "#FFFFFF";
                settings.SubtitleBackgroundColor = "#000000";
                settings.SubtitleBackgroundOpacity = 0.70;
                settings.SubtitleOutline = false;
                settings.SubtitleBackground = true;
                SaveSettings();
                ApplySubtitleStyle();
                window.Close();
                ShowToast("Sous-titres réinitialisés.", false, true);
            }, false);
            reset.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(reset);
            var cancel = Button("Annuler", delegate { window.Close(); }, false);
            cancel.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(cancel);
            actions.Children.Add(Button("Appliquer", delegate
            {
                settings.SubtitleSize = size;
                settings.SubtitleTextColor = textColor;
                settings.SubtitleBackgroundColor = backgroundColor;
                settings.SubtitleBackgroundOpacity = opacity;
                settings.SubtitleOutline = outline;
                settings.SubtitleBackground = backgroundEnabled;
                SaveSettings();
                ApplySubtitleStyle();
                window.Close();
                ShowToast("Apparence des sous-titres mise à jour.", false, true);
            }, true));
            stack.Children.Add(actions);

            window.ShowDialog();
        }

        /// <summary>Ligne « couleur » : pastilles rapides et valeur hexadécimale libre.</summary>
        FrameworkElement BuildSubtitleColorRow(string label, string current, Action<string> changed)
        {
            var row = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 12) };
            var title = Text(label, 13, Brush(TextPrimary), true);
            title.Width = 150;
            DockPanel.SetDock(title, Dock.Left);
            row.Children.Add(title);

            var hex = StyledTextBox();
            hex.Width = 96;
            hex.Text = current;
            hex.ToolTip = "Valeur hexadécimale, par exemple #FFFFFF";
            hex.TextChanged += delegate
            {
                Color parsed;
                if (TryParseHexColor(hex.Text, out parsed)) changed(hex.Text.Trim());
            };
            DockPanel.SetDock(hex, Dock.Right);
            row.Children.Add(hex);

            var swatches = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            foreach (string value in SubtitlePalette)
            {
                string captured = value;
                Color color;
                if (!TryParseHexColor(captured, out color)) color = Colors.White;
                var swatch = new Button
                {
                    Width = 25, Height = 25, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(0),
                    Background = Brush(color), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand, FocusVisualStyle = null, Template = RoundedButtonTemplate(13),
                    ToolTip = captured, SnapsToDevicePixels = true
                };
                swatch.Click += delegate { hex.Text = captured; };
                swatches.Children.Add(swatch);
            }
            row.Children.Add(swatches);
            return row;
        }
    }
}
