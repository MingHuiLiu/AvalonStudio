using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AvalonStudio.Extensions.Installer.Models
{
    /// <summary>
    /// Represents the contents of a VS Code extension's package.json.
    /// </summary>
    public class ExtensionManifest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("publisher")]
        public string Publisher { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; }

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new List<string>();

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new List<string>();

        [JsonPropertyName("activationEvents")]
        public List<string> ActivationEvents { get; set; } = new List<string>();

        [JsonPropertyName("extensionDependencies")]
        public List<string> ExtensionDependencies { get; set; } = new List<string>();

        [JsonPropertyName("main")]
        public string Main { get; set; }

        [JsonPropertyName("contributes")]
        public ExtensionContributes Contributes { get; set; }

        [JsonPropertyName("engines")]
        public Dictionary<string, string> Engines { get; set; } = new Dictionary<string, string>();

        /// <summary>Unique identifier: {publisher}.{name}</summary>
        public string UniqueId => $"{Publisher}.{Name}";
    }

    public class ExtensionContributes
    {
        [JsonPropertyName("languages")]
        public List<LanguageContribution> Languages { get; set; } = new List<LanguageContribution>();

        [JsonPropertyName("grammars")]
        public List<GrammarContribution> Grammars { get; set; } = new List<GrammarContribution>();

        [JsonPropertyName("commands")]
        public List<CommandContribution> Commands { get; set; } = new List<CommandContribution>();

        [JsonPropertyName("themes")]
        public List<ThemeContribution> Themes { get; set; } = new List<ThemeContribution>();

        [JsonPropertyName("iconThemes")]
        public List<IconThemeContribution> IconThemes { get; set; } = new List<IconThemeContribution>();

        [JsonPropertyName("snippets")]
        public List<SnippetContribution> Snippets { get; set; } = new List<SnippetContribution>();

        [JsonPropertyName("configuration")]
        public ConfigurationContribution Configuration { get; set; }

        [JsonPropertyName("keybindings")]
        public List<KeybindingContribution> Keybindings { get; set; } = new List<KeybindingContribution>();

        [JsonPropertyName("debuggers")]
        public List<DebuggerContribution> Debuggers { get; set; } = new List<DebuggerContribution>();

        [JsonPropertyName("taskDefinitions")]
        public List<TaskDefinitionContribution> TaskDefinitions { get; set; } = new List<TaskDefinitionContribution>();
    }

    public class LanguageContribution
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("aliases")]
        public List<string> Aliases { get; set; } = new List<string>();

        [JsonPropertyName("extensions")]
        public List<string> Extensions { get; set; } = new List<string>();

        [JsonPropertyName("filenames")]
        public List<string> Filenames { get; set; } = new List<string>();

        [JsonPropertyName("configuration")]
        public string Configuration { get; set; }
    }

    public class GrammarContribution
    {
        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("scopeName")]
        public string ScopeName { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }
    }

    public class CommandContribution
    {
        [JsonPropertyName("command")]
        public string Command { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; }
    }

    public class ThemeContribution
    {
        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("uiTheme")]
        public string UiTheme { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }
    }

    public class IconThemeContribution
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }
    }

    public class SnippetContribution
    {
        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }
    }

    public class ConfigurationContribution
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, ConfigurationProperty> Properties { get; set; }
            = new Dictionary<string, ConfigurationProperty>();
    }

    public class ConfigurationProperty
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("default")]
        public System.Text.Json.JsonElement? Default { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("enum")]
        public List<string> Enum { get; set; }
    }

    public class KeybindingContribution
    {
        [JsonPropertyName("command")]
        public string Command { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("mac")]
        public string Mac { get; set; }

        [JsonPropertyName("when")]
        public string When { get; set; }
    }

    public class DebuggerContribution
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("program")]
        public string Program { get; set; }

        [JsonPropertyName("runtime")]
        public string Runtime { get; set; }
    }

    public class TaskDefinitionContribution
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = new List<string>();
    }
}
