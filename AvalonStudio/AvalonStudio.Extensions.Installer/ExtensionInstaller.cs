using AvalonStudio.Extensions.Installer.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace AvalonStudio.Extensions.Installer
{
    /// <summary>
    /// Installs VS Code extensions from .vsix packages into the AvalonStudio extensions directory.
    /// </summary>
    public class ExtensionInstaller
    {
        private readonly string _extensionsBaseDir;
        private readonly VsixPackageParser _parser;

        public ExtensionInstaller(string extensionsBaseDir = null)
        {
            _extensionsBaseDir = extensionsBaseDir
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".avalonstudio", "extensions");

            _parser = new VsixPackageParser();

            Directory.CreateDirectory(_extensionsBaseDir);
        }

        public string ExtensionsBaseDir => _extensionsBaseDir;

        /// <summary>
        /// Installs a .vsix package, optionally verifying its SHA256 hash.
        /// </summary>
        /// <param name="vsixFilePath">Path to the .vsix file</param>
        /// <param name="expectedSha256">Optional expected SHA256 hash for verification</param>
        /// <param name="progress">Optional progress reporter (0.0-1.0)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The installed extension manifest</returns>
        public async Task<ExtensionManifest> InstallAsync(
            string vsixFilePath,
            string expectedSha256 = null,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(0.0);

            if (!File.Exists(vsixFilePath))
                throw new FileNotFoundException("VSIX file not found.", vsixFilePath);

            // Verify SHA256 if provided
            if (!string.IsNullOrEmpty(expectedSha256))
            {
                progress?.Report(0.1);
                await VerifySha256Async(vsixFilePath, expectedSha256).ConfigureAwait(false);
            }

            progress?.Report(0.2);

            cancellationToken.ThrowIfCancellationRequested();

            // Parse manifest
            var manifest = await _parser.ParseAsync(vsixFilePath).ConfigureAwait(false);

            progress?.Report(0.4);

            cancellationToken.ThrowIfCancellationRequested();

            // Determine install directory: {publisher}.{name}-{version}
            var installDirName = $"{manifest.UniqueId}-{manifest.Version}";
            var installDir = Path.Combine(_extensionsBaseDir, installDirName);

            // Remove existing installation of same version
            if (Directory.Exists(installDir))
                Directory.Delete(installDir, recursive: true);

            progress?.Report(0.5);

            // Extract
            _parser.ExtractTo(vsixFilePath, installDir);

            progress?.Report(0.9);

            // Write a metadata file for the registry
            var metaPath = Path.Combine(installDir, ".avalonstudio-extension.json");
            var meta = System.Text.Json.JsonSerializer.Serialize(new
            {
                manifest.UniqueId,
                manifest.Version,
                manifest.Publisher,
                manifest.Name,
                manifest.DisplayName,
                InstallDate = DateTime.UtcNow,
                Enabled = true
            });
            await Task.Run(() => File.WriteAllText(metaPath, meta)).ConfigureAwait(false);

            progress?.Report(1.0);

            return manifest;
        }

        /// <summary>
        /// Uninstalls an extension by its unique ID (publisher.name).
        /// </summary>
        public void Uninstall(string uniqueId)
        {
            var dirs = Directory.GetDirectories(_extensionsBaseDir, $"{uniqueId}-*");
            foreach (var dir in dirs)
                Directory.Delete(dir, recursive: true);
        }

        private static async Task VerifySha256Async(string filePath, string expectedHash)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = await Task.Run(() => sha256.ComputeHash(stream)).ConfigureAwait(false);
            var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            if (!string.Equals(actualHash, expectedHash.ToLowerInvariant(), StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"SHA256 mismatch: expected {expectedHash}, got {actualHash}");
        }
    }
}
