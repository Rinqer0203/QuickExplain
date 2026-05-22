using MdXaml;
using QuickExplain.Services;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using WpfBrush = System.Windows.Media.Brush;
using WpfControl = System.Windows.Controls.Control;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfApplication = System.Windows.Application;

namespace QuickExplain
{
    public sealed class SafeMarkdownToFlowDocumentConverter : IValueConverter
    {
        private static readonly Regex FencedCodeBlockRegex = new(
            @"(?m)^(?<indent>[ \t]*)(?<fence>`{3,}|~{3,})(?<language>[^\r\n]*)\r?\n(?<code>[\s\S]*?)\r?\n\k<indent>\k<fence>[ \t]*$",
            RegexOptions.Compiled);
        private static readonly Regex AtxHeadingRegex = new(
            @"(?m)^[ \t]{0,3}(?<level>#{1,6})[ \t]+(?<text>.*?)[ \t#]*$",
            RegexOptions.Compiled);
        private static readonly Regex PlainUrlRegex = new(
            @"(?<![\(\[""=])\bhttps?://[^\s<>()]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private const string CodeBlockTag = "CodeBlock";
        private const string CodeBlockTokenPrefix = "QE_CODE_BLOCK_";
        private static string? selectedCodeBlockToken;

        public Markdown? Markdown { get; set; }

        public string ForegroundResourceKey { get; set; } = "MaterialDesignBody";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string ?? string.Empty;

            try
            {
                var extracted = ExtractFencedCodeBlocks(text);
                var document = (Markdown ?? new Markdown()).Transform(extracted.Markdown);
                RestoreFencedCodeBlocks(document, extracted.CodeBlocks);
                ApplyThemeToGeneratedDocument(document, extracted.Headings);
                return document;
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("Markdown transform failed", ex);
                var document = new FlowDocument(new Paragraph(new Run(text)));
                ApplyThemeToGeneratedDocument(document, Array.Empty<HeadingInfo>());
                return document;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private void ApplyThemeToGeneratedDocument(FlowDocument document, IReadOnlyList<HeadingInfo> headings)
        {
            var codeBackground = FindBrush("MarkdownCodeBackground");
            var codeForeground = FindBrush("MarkdownCodeForeground");
            var codeFontFamily = new WpfFontFamily("Cascadia Mono, Consolas, Yu Gothic UI");
            var headingIndex = 0;
            document.SetResourceReference(TextElement.ForegroundProperty, ForegroundResourceKey);

            foreach (var block in document.Blocks.ToArray())
                ApplyThemeToBlock(block, ForegroundResourceKey, headings, ref headingIndex, codeBackground, codeForeground, codeFontFamily);
        }

        private static void ApplyThemeToBlock(Block block, string foregroundResourceKey, IReadOnlyList<HeadingInfo> headings, ref int headingIndex, WpfBrush? codeBackground, WpfBrush? codeForeground, WpfFontFamily codeFontFamily)
        {
            if (block.Tag as string == CodeBlockTag)
            {
                block.FontFamily = codeFontFamily;
                if (codeBackground != null)
                    block.Background = codeBackground;
                if (codeForeground != null)
                    block.Foreground = codeForeground;
            }
            else
            {
                block.SetResourceReference(TextElement.ForegroundProperty, foregroundResourceKey);
            }

            if (block is Paragraph paragraph)
            {
                var headingLevel = TryApplyHeadingStyle(paragraph, headings, ref headingIndex);
                foreach (var inline in paragraph.Inlines.ToArray())
                    ApplyThemeToInline(inline, foregroundResourceKey, headingLevel, codeBackground, codeForeground, codeFontFamily);
            }
            else if (block is Section section)
            {
                foreach (var child in section.Blocks.ToArray())
                    ApplyThemeToBlock(child, foregroundResourceKey, headings, ref headingIndex, codeBackground, codeForeground, codeFontFamily);
            }
            else if (block is List list)
            {
                list.SetResourceReference(TextElement.ForegroundProperty, foregroundResourceKey);
                foreach (var item in list.ListItems.ToArray())
                {
                    item.SetResourceReference(TextElement.ForegroundProperty, foregroundResourceKey);
                    foreach (var child in item.Blocks.ToArray())
                        ApplyThemeToBlock(child, foregroundResourceKey, headings, ref headingIndex, codeBackground, codeForeground, codeFontFamily);
                }
            }
            else if (block is Table table)
            {
                table.SetResourceReference(TextElement.ForegroundProperty, foregroundResourceKey);
                foreach (var rowGroup in table.RowGroups)
                {
                    rowGroup.SetResourceReference(TextElement.ForegroundProperty, foregroundResourceKey);
                    foreach (var row in rowGroup.Rows)
                    {
                        row.SetResourceReference(TextElement.ForegroundProperty, foregroundResourceKey);
                        foreach (var cell in row.Cells)
                        {
                            cell.SetResourceReference(TextElement.ForegroundProperty, foregroundResourceKey);
                            foreach (var child in cell.Blocks.ToArray())
                                ApplyThemeToBlock(child, foregroundResourceKey, headings, ref headingIndex, codeBackground, codeForeground, codeFontFamily);
                        }
                    }
                }
            }
            else if (block is BlockUIContainer container)
            {
                ApplyThemeToCodeControl(container.Child, codeBackground, codeForeground, codeFontFamily);
            }
        }

        private static void ApplyThemeToInline(Inline inline, string foregroundResourceKey, int? headingLevel, WpfBrush? codeBackground, WpfBrush? codeForeground, WpfFontFamily codeFontFamily)
        {
            if (inline is Hyperlink hyperlink)
            {
                ConfigureHyperlink(hyperlink);
            }
            else if (inline.Tag as string == "CodeSpan")
            {
                inline.FontFamily = codeFontFamily;
                if (codeBackground != null)
                    inline.SetResourceReference(TextElement.BackgroundProperty, "MarkdownCodeBackground");
                if (codeForeground != null)
                    inline.SetResourceReference(TextElement.ForegroundProperty, "MarkdownCodeForeground");
            }
            else
            {
                inline.SetResourceReference(TextElement.ForegroundProperty, foregroundResourceKey);
                if (headingLevel != null)
                {
                    inline.SetResourceReference(TextElement.FontSizeProperty, GetHeadingFontSizeResourceKey(headingLevel.Value));
                    inline.FontWeight = FontWeights.SemiBold;
                }
            }

            if (inline is Span span)
            {
                foreach (var child in span.Inlines.ToArray())
                    ApplyThemeToInline(child, foregroundResourceKey, headingLevel, codeBackground, codeForeground, codeFontFamily);
            }
            else if (inline is InlineUIContainer container)
            {
                ApplyThemeToCodeControl(container.Child, codeBackground, codeForeground, codeFontFamily);
            }
        }

        private static void ConfigureHyperlink(Hyperlink hyperlink)
        {
            var uri = GetHyperlinkUri(hyperlink);
            if (uri == null)
                return;

            hyperlink.NavigateUri = uri;
            hyperlink.Command = null;
            hyperlink.Cursor = System.Windows.Input.Cursors.Hand;
            hyperlink.Focusable = true;
            hyperlink.TextDecorations = TextDecorations.Underline;
            hyperlink.PreviewMouseLeftButtonDown -= OnHyperlinkPreviewMouseLeftButtonDown;
            hyperlink.PreviewMouseLeftButtonDown += OnHyperlinkPreviewMouseLeftButtonDown;
            hyperlink.PreviewMouseLeftButtonUp -= OnHyperlinkPreviewMouseLeftButtonUp;
            hyperlink.PreviewMouseLeftButtonUp += OnHyperlinkPreviewMouseLeftButtonUp;
            hyperlink.RequestNavigate -= OnHyperlinkRequestNavigate;
            hyperlink.RequestNavigate += OnHyperlinkRequestNavigate;
        }

        private static void OnHyperlinkPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (GetEventHyperlink(sender, e) == null)
                return;

            e.Handled = true;
        }

        private static void OnHyperlinkPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (GetEventHyperlink(sender, e) is not { } hyperlink)
                return;

            var uri = GetHyperlinkUri(hyperlink);
            if (uri == null)
                return;

            OpenHyperlink(uri);
            e.Handled = true;
        }

        private static void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri == null || IsSupportedExternalUri(e.Uri) == false)
                return;

            OpenHyperlink(e.Uri);
            e.Handled = true;
        }

        private static Hyperlink? GetEventHyperlink(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink senderHyperlink)
                return senderHyperlink;
            if (e.OriginalSource is DependencyObject dependencyObject)
                return FindParentHyperlink(dependencyObject);
            return null;
        }

        private static Hyperlink? FindParentHyperlink(DependencyObject dependencyObject)
        {
            while (dependencyObject != null)
            {
                if (dependencyObject is Hyperlink hyperlink)
                    return hyperlink;

                if (dependencyObject is FrameworkContentElement contentElement)
                    dependencyObject = contentElement.Parent;
                else
                    dependencyObject = LogicalTreeHelper.GetParent(dependencyObject);
            }

            return null;
        }

        private static Uri? GetHyperlinkUri(Hyperlink hyperlink)
        {
            if (TryCreateSupportedUri(hyperlink.NavigateUri, out var navigateUri))
                return navigateUri;
            if (TryCreateSupportedUri(hyperlink.CommandParameter, out var commandParameterUri))
                return commandParameterUri;
            if (TryCreateSupportedUri(hyperlink.Tag, out var tagUri))
                return tagUri;

            return null;
        }

        private static bool TryCreateSupportedUri(object? value, out Uri? uri)
        {
            uri = value switch
            {
                Uri existing => existing,
                string text when Uri.TryCreate(text, UriKind.Absolute, out var parsed) => parsed,
                _ => null
            };

            return uri != null && IsSupportedExternalUri(uri);
        }

        private static void OpenHyperlink(Uri uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("Failed to open hyperlink", ex);
            }
        }

        private static bool IsSupportedExternalUri(Uri uri) =>
            uri.IsAbsoluteUri
            && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps
                || uri.Scheme == Uri.UriSchemeMailto);

        private static void ApplyThemeToCodeControl(UIElement? element, WpfBrush? codeBackground, WpfBrush? codeForeground, WpfFontFamily codeFontFamily)
        {
            if (element == null)
                return;

            var tag = element is FrameworkElement frameworkElement ? frameworkElement.Tag as string : null;
            var typeName = element.GetType().FullName ?? string.Empty;
            var isCodeElement = tag == CodeBlockTag || typeName.Contains("AvalonEdit", StringComparison.Ordinal);
            if (!isCodeElement)
                return;

            if (element is WpfControl control)
            {
                control.FontFamily = codeFontFamily;
                if (codeBackground != null)
                    control.Background = codeBackground;
                if (codeForeground != null)
                    control.Foreground = codeForeground;
            }
            else
            {
                SetPropertyIfAvailable(element, "FontFamily", codeFontFamily);
                if (codeBackground != null)
                    SetPropertyIfAvailable(element, "Background", codeBackground);
                if (codeForeground != null)
                    SetPropertyIfAvailable(element, "Foreground", codeForeground);
            }
        }

        private static void SetPropertyIfAvailable(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property?.CanWrite == true && property.PropertyType.IsInstanceOfType(value))
                property.SetValue(target, value);
        }

        private static WpfBrush? FindBrush(string key) =>
            WpfApplication.Current.TryFindResource(key) as WpfBrush;

        private static ExtractedMarkdown ExtractFencedCodeBlocks(string markdown)
        {
            var codeBlocks = new List<CodeBlockInfo>();
            var replaced = FencedCodeBlockRegex.Replace(markdown, match =>
            {
                var token = $"{CodeBlockTokenPrefix}{codeBlocks.Count:D4}";
                codeBlocks.Add(new CodeBlockInfo(
                    token,
                    match.Groups["language"].Value.Trim(),
                    NormalizeCodeBlockText(match.Groups["code"].Value)));
                return Environment.NewLine + token + Environment.NewLine;
            });

            var linked = AutoLinkPlainUrls(replaced);
            return new ExtractedMarkdown(linked, codeBlocks, ExtractHeadings(linked));
        }

        private static string AutoLinkPlainUrls(string markdown) =>
            PlainUrlRegex.Replace(markdown, match =>
            {
                var url = match.Value.TrimEnd('.', ',', ';', ':', '!', '?');
                var suffix = match.Value[url.Length..];
                return $"<{url}>{suffix}";
            });

        private static List<HeadingInfo> ExtractHeadings(string markdown)
        {
            var headings = new List<HeadingInfo>();
            foreach (Match match in AtxHeadingRegex.Matches(markdown))
            {
                headings.Add(new HeadingInfo(
                    match.Groups["level"].Value.Length,
                    NormalizeInlineText(match.Groups["text"].Value)));
            }

            return headings;
        }

        private static int? TryApplyHeadingStyle(Paragraph paragraph, IReadOnlyList<HeadingInfo> headings, ref int headingIndex)
        {
            if (headingIndex >= headings.Count)
                return null;

            var text = NormalizeInlineText(GetParagraphText(paragraph));
            if (string.IsNullOrEmpty(text))
                return null;

            var heading = headings[headingIndex];
            if (text != heading.Text)
                return null;

            headingIndex++;
            paragraph.FontWeight = FontWeights.SemiBold;
            paragraph.Margin = heading.Level switch
            {
                1 => new Thickness(0, 18, 0, 10),
                2 => new Thickness(0, 16, 0, 8),
                3 => new Thickness(0, 14, 0, 7),
                _ => new Thickness(0, 12, 0, 6)
            };
            paragraph.SetResourceReference(TextElement.FontSizeProperty, GetHeadingFontSizeResourceKey(heading.Level));
            return heading.Level;
        }

        private static string GetParagraphText(Paragraph paragraph)
        {
            var builder = new StringBuilder();
            foreach (var inline in paragraph.Inlines)
                AppendInlineText(builder, inline);

            return builder.ToString();
        }

        private static void AppendInlineText(StringBuilder builder, Inline inline)
        {
            switch (inline)
            {
                case Run run:
                    builder.Append(run.Text);
                    break;
                case Span span:
                    foreach (var child in span.Inlines)
                        AppendInlineText(builder, child);
                    break;
            }
        }

        private static string NormalizeInlineText(string text)
        {
            var normalized = text.Trim();
            normalized = Regex.Replace(normalized, @"!\[([^\]]*)\]\([^)]+\)", "$1");
            normalized = Regex.Replace(normalized, @"\[([^\]]+)\]\([^)]+\)", "$1");
            normalized = normalized.Replace("**", string.Empty)
                .Replace("__", string.Empty)
                .Replace("*", string.Empty)
                .Replace("_", string.Empty)
                .Replace("`", string.Empty);
            return Regex.Replace(normalized, @"\s+", " ");
        }

        private static string GetHeadingFontSizeResourceKey(int level) =>
            level switch
            {
                1 => "MarkdownHeading1FontSize",
                2 => "MarkdownHeading2FontSize",
                3 => "MarkdownHeading3FontSize",
                4 => "MarkdownHeading4FontSize",
                5 => "MarkdownHeading5FontSize",
                _ => "MarkdownHeading6FontSize"
            };

        private static void RestoreFencedCodeBlocks(FlowDocument document, IReadOnlyList<CodeBlockInfo> codeBlocks)
        {
            if (codeBlocks.Count == 0)
                return;

            for (var index = document.Blocks.FirstBlock; index != null;)
            {
                var current = index;
                index = current.NextBlock;

                if (TryGetTokenParagraphText(current, out var token) == false)
                    continue;

                var codeBlock = codeBlocks.FirstOrDefault(block => block.Token == token);
                if (codeBlock == null)
                    continue;

                document.Blocks.InsertBefore(current, CreateCodeBlock(codeBlock));
                document.Blocks.Remove(current);
            }
        }

        private static bool TryGetTokenParagraphText(Block block, out string token)
        {
            token = string.Empty;
            if (block is not Paragraph paragraph)
                return false;

            var builder = new StringBuilder();
            foreach (var inline in paragraph.Inlines)
            {
                if (inline is Run run)
                    builder.Append(run.Text);
            }

            token = builder.ToString().Trim();
            return token.StartsWith(CodeBlockTokenPrefix, StringComparison.Ordinal);
        }

        private static Block CreateCodeBlock(CodeBlockInfo codeBlock)
        {
            var textBox = new WpfTextBox
            {
                Text = codeBlock.Code,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 8, 10, 8),
                FontFamily = new WpfFontFamily("Cascadia Mono, Consolas, Yu Gothic UI"),
                FontSize = 14,
                Tag = CodeBlockTag,
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = true,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            textBox.RequestBringIntoView += (_, e) => e.Handled = true;
            textBox.PreviewMouseLeftButtonDown += (_, e) =>
            {
                selectedCodeBlockToken = codeBlock.Token;
                FocusAndSelectAll(textBox);
                e.Handled = true;
            };
            textBox.GotKeyboardFocus += (_, _) =>
            {
                selectedCodeBlockToken = codeBlock.Token;
                textBox.SelectAll();
            };
            textBox.LostKeyboardFocus += (_, e) =>
            {
                if (e.NewFocus != null && IsCodeBlockTextBox(e.NewFocus) == false)
                    selectedCodeBlockToken = null;
            };
            textBox.Loaded += (_, _) => RestoreCodeBlockSelection(textBox, codeBlock.Token);
            textBox.SetResourceReference(WpfControl.BackgroundProperty, "MarkdownCodeBackground");
            textBox.SetResourceReference(WpfControl.ForegroundProperty, "MarkdownCodeForeground");
            textBox.SetResourceReference(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, "MarkdownCodeForeground");

            return new BlockUIContainer(textBox)
            {
                Margin = new Thickness(0, 0, 0, 12),
                Tag = CodeBlockTag
            };
        }

        private static void RestoreCodeBlockSelection(WpfTextBox textBox, string token)
        {
            if (selectedCodeBlockToken != token)
                return;

            textBox.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (selectedCodeBlockToken == token)
                        FocusAndSelectAll(textBox);
                }),
                System.Windows.Threading.DispatcherPriority.Input);
        }

        private static void FocusAndSelectAll(WpfTextBox textBox)
        {
            if (textBox.IsKeyboardFocusWithin == false)
                textBox.Focus();

            textBox.SelectAll();
        }

        private static bool IsCodeBlockTextBox(object value) =>
            value is WpfTextBox { Tag: string tag } && tag == CodeBlockTag;

        private static string NormalizeCodeBlockText(string code) =>
            code.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n');

        private sealed record ExtractedMarkdown(string Markdown, List<CodeBlockInfo> CodeBlocks, List<HeadingInfo> Headings);

        private sealed record CodeBlockInfo(string Token, string Language, string Code);

        private sealed record HeadingInfo(int Level, string Text);
    }
}
