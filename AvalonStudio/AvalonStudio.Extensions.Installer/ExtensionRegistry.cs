using AvalonStudio.Extensions.Installer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AvalonStudio.Extensions.Installer
{
    /// <summary>
    /// Tracks all installed VS Code extensions and their enabled/disabled state.
    /// </summary>
    public class ExtensionRegistry
    {
        private readonly string _extensionsBaseDir;
        private readonly Dictionary<string, InstalledExtension> _extensions
            = new Dictionary<string, InstalledExtension>(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public ExtensionRegistry(string extensionsBaseDir = null)
        {
            _extensionsBaseDir = extensionsBaseDir
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".avalonstudio", "extensions");
        }

        public IReadOnlyCollection<InstalledExtension> InstalledExtensions =>
            _extensions.Values.ToList().AsReadOnly();

        /// <summary>
        /// Scans the extensions directory and loads all installed extensions by reading
        /// package.json directly from each extension directory.
        /// </summary>
        public async Task RefreshAsync()
        {
            _extensions.Clear();

            if (!Directory.Exists(_extensionsBaseDir))
                return;

            foreach (var dir in Directory.GetDirectories(_extensionsBaseDir))
            {
                var packageJsonPath = Path.Combine(dir, "package.json");
                var metaPath = Path.Combine(dir, ".avalonstudio-extension.json");

                if (!File.Exists(packageJsonPath))
                    continue;

                try
                {
                    var packageJson = await Task.Run(() => File.ReadAllText(packageJsonPath))
                        .ConfigureAwait(false);
                    var manifest = JsonSerializer.Deserialize<ExtensionManifest>(packageJson, _jsonOptions);

                    if (manifest == null) continue;

                    bool enabled = true;
                    if (File.Exists(metaPath))
                    {
                        var metaJson = await Task.Run(() => File.ReadAllText(metaPath))
                            .ConfigureAwait(false);
                        var meta = JsonSerializer.Deserialize<ExtensionMeta>(metaJson, _jsonOptions);
                        enabled = meta?.Enabled ?? true;
                    }

                    _extensions[manifest.UniqueId] = new InstalledExtension
                    {
                        Manifest = manifest,
                        InstallDirectory = dir,
                        IsEnabled = enabled
                    };
                }
                catch
                {
                    // Skip extensions that fail to load
                }
            }
        }

        /// <summary>
        /// Loads installed extensions by reading package.json directly from each extension directory.
        /// </summary>
        public Task RefreshFromDirectoriesAsync() => RefreshAsync();

        public bool IsInstalled(string uniqueId) => _extensions.ContainsKey(uniqueId);

        public InstalledExtension GetExtension(string uniqueId)
        {
            _extensions.TryGetValue(uniqueId, out var ext);
            return ext;
        }

        public async Task SetEnabledAsync(string uniqueId, bool enabled)
        {
            if (!_extensions.TryGetValue(uniqueId, out var ext))
                return;

            ext.IsEnabled = enabled;

            var metaPath = Path.Combine(ext.InstallDirectory, ".avalonstudio-extension.json");
            var meta = new ExtensionMeta
            {
                UniqueId = uniqueId,
                Enabled = enabled
            };

            var json = JsonSerializer.Serialize(meta);
            await Task.Run(() => File.WriteAllText(metaPath, json)).ConfigureAwait(false);
        }
    }

    public class InstalledExtension
    {
        public ExtensionManifest Manifest { get; set; }
        public string InstallDirectory { get; set; }
        public bool IsEnabled { get; set; }
    }

    internal class ExtensionMeta
    {
        [JsonPropertyName("uniqueId")]
        public string UniqueId { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }
}
