using AvalonStudio.Extensions.OpenVSX.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvalonStudio.Extensions.OpenVSX
{
    /// <summary>
    /// Manages downloading extensions from Open VSX with local caching.
    /// </summary>
    public class ExtensionDownloadManager : IDisposable
    {
        private readonly OpenVsxClient _client;
        private readonly string _cacheDir;
        private bool _disposed;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public ExtensionDownloadManager(string cacheDir = null, OpenVsxClient client = null)
        {
            _cacheDir = cacheDir
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".avalonstudio", "cache", "openvsx");

            _client = client ?? new OpenVsxClient();

            Directory.CreateDirectory(_cacheDir);
        }

        /// <summary>
        /// Downloads a .vsix for the given extension, using local cache if available.
        /// Returns the path to the downloaded .vsix file.
        /// </summary>
        public async Task<string> GetVsixAsync(
            string @namespace,
            string extension,
            string version,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            var filename = $"{@namespace}.{extension}-{version}.vsix";
            var cachedPath = Path.Combine(_cacheDir, filename);

            if (File.Exists(cachedPath))
            {
                progress?.Report(1.0);
                return cachedPath;
            }

            await _client.DownloadVsixAsync(
                @namespace, extension, version,
                cachedPath, progress, cancellationToken).ConfigureAwait(false);

            return cachedPath;
        }

        /// <summary>
        /// Gets extension metadata, using a local JSON cache to avoid repeated API calls.
        /// </summary>
        public async Task<ExtensionDetail> GetExtensionMetadataAsync(
            string @namespace,
            string extension,
            string version = null,
            CancellationToken cancellationToken = default)
        {
            var cacheKey = version == null
                ? $"{@namespace}.{extension}.json"
                : $"{@namespace}.{extension}-{version}.json";

            var cachePath = Path.Combine(_cacheDir, "meta", cacheKey);

            if (File.Exists(cachePath))
            {
                var cacheInfo = new FileInfo(cachePath);
                if (DateTime.UtcNow - cacheInfo.LastWriteTimeUtc < TimeSpan.FromHours(1))
                {
                    var cached = File.ReadAllText(cachePath);
                    return JsonSerializer.Deserialize<ExtensionDetail>(cached, _jsonOptions);
                }
            }

            var detail = version == null
                ? await _client.GetExtensionAsync(@namespace, extension, cancellationToken).ConfigureAwait(false)
                : await _client.GetExtensionVersionAsync(@namespace, extension, version, cancellationToken).ConfigureAwait(false);

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
            File.WriteAllText(cachePath, JsonSerializer.Serialize(detail, _jsonOptions));

            return detail;
        }

        /// <summary>
        /// Checks if a newer version is available for an installed extension.
        /// </summary>
        public async Task<string> GetLatestVersionAsync(
            string @namespace,
            string extension,
            string currentVersion,
            CancellationToken cancellationToken = default)
        {
            var detail = await GetExtensionMetadataAsync(
                @namespace, extension, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (detail == null) return null;

            if (!string.Equals(detail.Version, currentVersion, StringComparison.Ordinal))
                return detail.Version;

            return null; // Up to date
        }

        public void ClearCache()
        {
            if (Directory.Exists(_cacheDir))
                Directory.Delete(_cacheDir, recursive: true);

            Directory.CreateDirectory(_cacheDir);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client?.Dispose();
        }
    }
}
