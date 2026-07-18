using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MEmarket.Services
{
    public static class MarkdownRenderer
    {
        private static readonly Regex InlineRegex = new Regex(
            @"(?<bold>\*\*(?<boldtext>[^*]+)\*\*)|" +
            @"(?<bold2>__(?<boldtext2>[^_]+)__)|" +
            @"(?<italic>\*(?<italictext>[^*]+)\*)|" +
            @"(?<italic2>_(?<italictext2>[^_]+)_)|" +
            @"(?<code>`(?<codetext>[^`]+)`)|" +
            @"(?<link>\[(?<linktext>[^\]]+)\]\((?<linkurl>[^)]+)\))",
            RegexOptions.Compiled);

        private static readonly Regex SeparatorRegex = new Regex(
            @"^\s*([-*_])(\s*\1){2,}\s*$",
            RegexOptions.Compiled);

        public static void Render(RichTextBox richTextBox, string markdownText)
        {
            if (richTextBox == null) return;

            richTextBox.Blocks.Clear();

            if (string.IsNullOrEmpty(markdownText)) return;

            markdownText = markdownText.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = markdownText.Split('\n');

            Paragraph currentParagraph = null;
            bool lastLineWasEmpty = false;
            bool inBlockquoteParagraph = false;

            Brush subtleBrush = null;
            if (Application.Current.Resources.Contains("PhoneSubtleBrush"))
            {
                try { subtleBrush = (Brush)Application.Current.Resources["PhoneSubtleBrush"]; } catch { }
            }
            if (subtleBrush == null)
            {
                subtleBrush = new SolidColorBrush(Colors.Gray);
            }

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();

                if (string.IsNullOrEmpty(line))
                {
                    currentParagraph = null;
                    lastLineWasEmpty = true;
                    continue;
                }

                if (SeparatorRegex.IsMatch(line))
                {
                    if (lastLineWasEmpty && richTextBox.Blocks.Count > 0)
                    {
                        var spacer = new Paragraph();
                        spacer.Inlines.Add(new LineBreak());
                        richTextBox.Blocks.Add(spacer);
                    }

                    var separatorParagraph = CreateSeparatorParagraph(richTextBox);
                    richTextBox.Blocks.Add(separatorParagraph);
                    currentParagraph = null;
                    lastLineWasEmpty = false;
                    inBlockquoteParagraph = false;
                    continue;
                }

                if (line.StartsWith("#"))
                {
                    int headerLevel = 0;
                    while (headerLevel < line.Length && line[headerLevel] == '#')
                    {
                        headerLevel++;
                    }

                    if (headerLevel > 0 && headerLevel < line.Length && line[headerLevel] == ' ')
                    {
                        string headerText = line.Substring(headerLevel + 1).Trim();

                        if (lastLineWasEmpty && richTextBox.Blocks.Count > 0)
                        {
                            var spacer = new Paragraph();
                            spacer.Inlines.Add(new LineBreak());
                            richTextBox.Blocks.Add(spacer);
                        }

                        var headerParagraph = CreateHeaderParagraph(headerText, headerLevel);
                        richTextBox.Blocks.Add(headerParagraph);
                        currentParagraph = null;
                        lastLineWasEmpty = false;
                        inBlockquoteParagraph = false;
                        continue;
                    }
                }

                bool isListItem = false;
                if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("• "))
                {
                    isListItem = true;
                    line = "  • " + line.Substring(2).Trim();
                }

                bool isBlockquote = false;
                if (line.StartsWith(">"))
                {
                    isBlockquote = true;
                    line = line.Substring(1).Trim();
                }

                bool needNewParagraph = currentParagraph == null || isListItem || (isBlockquote != inBlockquoteParagraph);

                if (needNewParagraph)
                {
                    if (lastLineWasEmpty && richTextBox.Blocks.Count > 0)
                    {
                        var spacer = new Paragraph();
                        spacer.Inlines.Add(new LineBreak());
                        richTextBox.Blocks.Add(spacer);
                    }

                    currentParagraph = new Paragraph();
                    inBlockquoteParagraph = isBlockquote;

                    richTextBox.Blocks.Add(currentParagraph);
                }
                else
                {
                    currentParagraph.Inlines.Add(new LineBreak());
                }

                if (isBlockquote)
                {
                    line = "│  " + line;
                }

                var inlines = ParseInlines(line);
                foreach (var inline in inlines)
                {
                    if (isBlockquote)
                    {
                        ApplySubtleBrushToInline(inline, subtleBrush);
                    }
                    currentParagraph.Inlines.Add(inline);
                }

                if (isListItem)
                {
                    currentParagraph = null;
                }

                lastLineWasEmpty = false;
            }
        }

        private static void ApplySubtleBrushToInline(Inline inline, Brush brush)
        {
            if (inline == null || brush == null) return;

            var run = inline as Run;
            if (run != null)
            {
                if (run.FontFamily != null && run.FontFamily.Source == "Courier New")
                {
                    return;
                }
                run.Foreground = brush;
                return;
            }

            var hl = inline as Hyperlink;
            if (hl != null)
            {
                hl.Foreground = brush;
            }
        }

        private static Paragraph CreateSeparatorParagraph(RichTextBox richTextBox)
        {
            var p = new Paragraph();
            var container = new InlineUIContainer();

            var rect = new System.Windows.Shapes.Rectangle
            {
                Height = 1.5,
                Margin = new Thickness(0, 10, 0, 10),
            };

            Brush brush = null;
            if (Application.Current.Resources.Contains("PhoneBorderBrush"))
            {
                try { brush = (Brush)Application.Current.Resources["PhoneBorderBrush"]; } catch { }
            }
            if (brush == null && Application.Current.Resources.Contains("PhoneSubtleBrush"))
            {
                try { brush = (Brush)Application.Current.Resources["PhoneSubtleBrush"]; } catch { }
            }
            rect.Fill = brush ?? new SolidColorBrush(Colors.Gray);

            try
            {
                var binding = new System.Windows.Data.Binding("ActualWidth")
                {
                    Source = richTextBox
                };
                rect.SetBinding(System.Windows.Shapes.Rectangle.WidthProperty, binding);
            }
            catch { }

            container.Child = rect;
            p.Inlines.Add(container);
            return p;
        }

        private static Paragraph CreateHeaderParagraph(string text, int level)
        {
            var p = new Paragraph();

            double baseFontSize = 20;
            if (Application.Current.Resources.Contains("PhoneFontSizeNormal"))
            {
                try
                {
                    baseFontSize = (double)Application.Current.Resources["PhoneFontSizeNormal"];
                }
                catch { }
            }

            double fontSize;
            switch (level)
            {
                case 1: fontSize = baseFontSize * 1.5; break;
                case 2: fontSize = baseFontSize * 1.3; break;
                case 3: fontSize = baseFontSize * 1.15; break;
                default: fontSize = baseFontSize * 1.05; break;
            }

            p.Inlines.Add(new Run
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold
            });

            return p;
        }

        private static List<Inline> ParseInlines(string text)
        {
            var inlines = new List<Inline>();
            int lastIndex = 0;

            var matches = InlineRegex.Matches(text);
            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    inlines.Add(new Run { Text = text.Substring(lastIndex, match.Index - lastIndex) });
                }

                if (match.Groups["bold"].Success)
                {
                    string boldText = match.Groups["boldtext"].Value;
                    inlines.Add(new Run { Text = boldText, FontWeight = FontWeights.Bold });
                }
                else if (match.Groups["bold2"].Success)
                {
                    string boldText = match.Groups["boldtext2"].Value;
                    inlines.Add(new Run { Text = boldText, FontWeight = FontWeights.Bold });
                }
                else if (match.Groups["italic"].Success)
                {
                    string italicText = match.Groups["italictext"].Value;
                    inlines.Add(new Run { Text = italicText, FontStyle = FontStyles.Italic });
                }
                else if (match.Groups["italic2"].Success)
                {
                    string italicText = match.Groups["italictext2"].Value;
                    inlines.Add(new Run { Text = italicText, FontStyle = FontStyles.Italic });
                }
                else if (match.Groups["code"].Success)
                {
                    string codeText = match.Groups["codetext"].Value;
                    var run = new Run
                    {
                        Text = codeText,
                        FontFamily = new FontFamily("Courier New")
                    };

                    if (Application.Current.Resources.Contains("PhoneAccentBrush"))
                    {
                        try { run.Foreground = (Brush)Application.Current.Resources["PhoneAccentBrush"]; } catch { }
                    }
                    else
                    {
                        run.Foreground = new SolidColorBrush(Color.FromArgb(255, 217, 83, 79));
                    }

                    inlines.Add(run);
                }
                else if (match.Groups["link"].Success)
                {
                    string linkText = match.Groups["linktext"].Value;
                    string linkUrl = match.Groups["linkurl"].Value;

                    var hl = new Hyperlink();
                    hl.Inlines.Add(new Run { Text = linkText });

                    try
                    {
                        hl.NavigateUri = new Uri(linkUrl, UriKind.RelativeOrAbsolute);
                        hl.TargetName = "_blank";
                    }
                    catch { }

                    if (Application.Current.Resources.Contains("PhoneAccentBrush"))
                    {
                        try
                        {
                            hl.Foreground = (Brush)Application.Current.Resources["PhoneAccentBrush"];
                        }
                        catch { }
                    }

                    inlines.Add(hl);
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                inlines.Add(new Run { Text = text.Substring(lastIndex) });
            }

            return inlines;
        }
    }
}