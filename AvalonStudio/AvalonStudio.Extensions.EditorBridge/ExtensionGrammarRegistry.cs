using System;
using System.Collections.Generic;
using System.IO;

namespace AvalonStudio.Extensions.EditorBridge
{
    /// <summary>
    /// Registry for TextMate grammars contributed by VS Code extensions.
    /// Parses contributes.grammars from extension manifests and provides tokenization.
    /// </summary>
    public class ExtensionGrammarRegistry
    {
        private readonly Dictionary<string, string> _scopeToGrammarPath
            = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly Dictionary<string, string> _languageIdToScope
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a TextMate grammar file for a given scope name.
        /// </summary>
        public void RegisterGrammar(string scopeName, string grammarFilePath, string languageId = null)
        {
            if (string.IsNullOrEmpty(scopeName))
                throw new ArgumentNullException(nameof(scopeName));

            _scopeToGrammarPath[scopeName] = grammarFilePath;

            if (!string.IsNullOrEmpty(languageId))
                _languageIdToScope[languageId] = scopeName;
        }

        /// <summary>
        /// Gets the scope name for a given VS Code language ID.
        /// </summary>
        public string GetScopeForLanguage(string languageId)
        {
            _languageIdToScope.TryGetValue(languageId, out var scope);
            return scope;
        }

        /// <summary>
        /// Tokenizes a line of text using the registered grammar for the given scope.
        /// Returns TextMate tokens with color information.
        /// </summary>
        public IEnumerable<TextMateToken> TokenizeLine(string scopeName, string lineText)
        {
            if (string.IsNullOrEmpty(lineText))
                return Array.Empty<TextMateToken>();

            // Basic tokenization stub - returns no tokens.
            // In production, integrate TextMateSharp for full tokenization:
            //   var grammar = _grammarCache.GetOrLoad(scopeName, _scopeToGrammarPath[scopeName]);
            //   var result = grammar.TokenizeLine(lineText, previousLineState);
            //   map result.tokens to TextMateToken instances with theme colors
            return Array.Empty<TextMateToken>();
        }

        /// <summary>
        /// Loads grammars contributed by a specific extension.
        /// </summary>
        /// <param name="extensionDir">Root directory of the installed extension</param>
        /// <param name="grammarsContribution">The contributes.grammars array from package.json</param>
        public void LoadFromExtension(string extensionDir, IEnumerable<GrammarContribution> grammarsContribution)
        {
            if (grammarsContribution == null) return;

            foreach (var grammar in grammarsContribution)
            {
                if (string.IsNullOrEmpty(grammar.ScopeName) || string.IsNullOrEmpty(grammar.Path))
                    continue;

                var fullPath = Path.Combine(extensionDir, grammar.Path.TrimStart('/', '\\'));

                if (File.Exists(fullPath))
                {
                    RegisterGrammar(grammar.ScopeName, fullPath, grammar.Language);
                }
            }
        }
    }

    public class GrammarContribution
    {
        public string Language { get; set; }
        public string ScopeName { get; set; }
        public string Path { get; set; }
    }
}
