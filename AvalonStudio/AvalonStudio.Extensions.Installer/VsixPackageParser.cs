using AvalonStudio.Extensions.Installer.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;

namespace AvalonStudio.Extensions.Installer
{
    /// <summary>
    /// Parses VS Code extension packages (.vsix files, which are ZIP archives).
    /// </summary>
    public class VsixPackageParser
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Parses a .vsix file and returns the extension manifest.
        /// </summary>
        public async Task<ExtensionManifest> ParseAsync(string vsixFilePath)
        {
            if (!File.Exists(vsixFilePath))
                throw new FileNotFoundException("VSIX file not found.", vsixFilePath);

            using var zip = ZipFile.OpenRead(vsixFilePath);

            var packageJsonEntry = zip.GetEntry("extension/package.json");
            if (packageJsonEntry == null)
                throw new InvalidOperationException("Invalid VSIX: missing extension/package.json");

            using var stream = packageJsonEntry.Open();
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            var json = await reader.ReadToEndAsync().ConfigureAwait(false);

            return JsonSerializer.Deserialize<ExtensionManifest>(json, _jsonOptions)
                   ?? throw new InvalidOperationException("Failed to parse extension manifest.");
        }

        /// <summary>
        /// Extracts a .vsix file to the specified directory.
        /// </summary>
        public void ExtractTo(string vsixFilePath, string destinationDir)
        {
            if (!File.Exists(vsixFilePath))
                throw new FileNotFoundException("VSIX file not found.", vsixFilePath);

            Directory.CreateDirectory(destinationDir);

            using var zip = ZipFile.OpenRead(vsixFilePath);

            foreach (var entry in zip.Entries)
            {
                // Only extract files inside "extension/" prefix
                if (!entry.FullName.StartsWith("extension/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativePath = entry.FullName.Substring("extension/".Length);
                if (string.IsNullOrEmpty(relativePath)) continue;

                // Sanitize path to prevent path traversal
                var fullPath = Path.GetFullPath(Path.Combine(destinationDir, relativePath));
                if (!fullPath.StartsWith(Path.GetFullPath(destinationDir), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Zip entry '{entry.FullName}' is outside destination directory.");

                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(fullPath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, overwrite: true);
                }
            }
        }
    }
}
