using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using System;
using System.Collections.Generic;

namespace AvalonStudio.Extensions.EditorBridge
{
    /// <summary>
    /// Applies TextMate-based syntax highlighting to AvaloniaEdit via IVisualLineTransformer.
    /// Consumes token data from ExtensionGrammarRegistry.
    /// </summary>
    public class TextMateHighlighter : DocumentColorizingTransformer
    {
        private readonly ExtensionGrammarRegistry _grammarRegistry;
        private readonly string _scopeName;

        public TextMateHighlighter(ExtensionGrammarRegistry grammarRegistry, string scopeName)
        {
            _grammarRegistry = grammarRegistry ?? throw new ArgumentNullException(nameof(grammarRegistry));
            _scopeName = scopeName ?? throw new ArgumentNullException(nameof(scopeName));
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (_grammarRegistry == null) return;

            var document = CurrentContext.Document;
            var lineText = document.GetText(line.Offset, line.Length);

            var tokens = _grammarRegistry.TokenizeLine(_scopeName, lineText);

            foreach (var token in tokens)
            {
                int start = line.Offset + Math.Min(token.StartIndex, line.Length);
                int end = line.Offset + Math.Min(token.EndIndex, line.Length);

                if (start >= end) continue;

                ChangeLinePart(start, end, element =>
                {
                    if (token.ForegroundColor.HasValue)
                    {
                        var color = token.ForegroundColor.Value;
                        element.TextRunProperties.SetForegroundBrush(
                            new Avalonia.Media.SolidColorBrush(
                                new Avalonia.Media.Color(255, color.R, color.G, color.B)));
                    }

                    if (token.IsBold)
                    {
                        element.TextRunProperties.SetTypeface(
                            new Avalonia.Media.Typeface(
                                element.TextRunProperties.Typeface.FontFamily,
                                Avalonia.Media.FontStyle.Normal,
                                Avalonia.Media.FontWeight.Bold));
                    }

                    if (token.IsItalic)
                    {
                        element.TextRunProperties.SetTypeface(
                            new Avalonia.Media.Typeface(
                                element.TextRunProperties.Typeface.FontFamily,
                                Avalonia.Media.FontStyle.Italic,
                                element.TextRunProperties.Typeface.Weight));
                    }
                });
            }
        }
    }

    public class TextMateToken
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public string ScopeName { get; set; }
        public (byte R, byte G, byte B)? ForegroundColor { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
    }
}
