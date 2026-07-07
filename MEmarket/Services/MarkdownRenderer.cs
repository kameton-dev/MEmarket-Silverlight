using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

// TODO: ДОДЕЛАТЬ РЕНДЕРИНГ, ДОБАВИТЬ РЕНДЕР СЕПАРАТОРА

namespace MEmarket.Services
{
    public static class MarkdownRenderer
    {
        private static readonly Regex InlineRegex = new Regex(
            @"(?<bold>\*\*(?<boldtext>[^*]+)\*\*)|" +
            @"(?<bold2>__(?<boldtext2>[^_]+)__)|" +
            @"(?<italic>\*(?<italictext>[^*]+)\*)|" +
            @"(?<italic2>_(?<italictext2>[^_]+)_)|" +
            @"(?<link>\[(?<linktext>[^\]]+)\]\((?<linkurl>[^)]+)\))",
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

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                
                if (string.IsNullOrEmpty(line))
                {
                    currentParagraph = null;
                    lastLineWasEmpty = true;
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
                        continue;
                    }
                }
                
                bool isListItem = false;
                if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("• "))
                {
                    isListItem = true;
                    line = "  • " + line.Substring(2).Trim();
                }
                
                if (currentParagraph == null || isListItem)
                {
                    if (lastLineWasEmpty && richTextBox.Blocks.Count > 0)
                    {
                        var spacer = new Paragraph();
                        spacer.Inlines.Add(new LineBreak());
                        richTextBox.Blocks.Add(spacer);
                    }
                    
                    currentParagraph = new Paragraph();
                    richTextBox.Blocks.Add(currentParagraph);
                }
                else
                {
                    currentParagraph.Inlines.Add(new LineBreak());
                }
                
                var inlines = ParseInlines(line);
                foreach (var inline in inlines)
                {
                    currentParagraph.Inlines.Add(inline);
                }
                
                if (isListItem)
                {
                    currentParagraph = null;
                }
                
                lastLineWasEmpty = false;
            }
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