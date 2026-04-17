using AvalonStudio.Extensions.OpenVSX.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvalonStudio.Extensions.OpenVSX
{
    /// <summary>
    /// Client for the Open VSX registry REST API (https://open-vsx.org).
    /// </summary>
    public class OpenVsxClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private bool _disposed;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public OpenVsxClient(string baseUrl = "https://open-vsx.org", HttpClient httpClient = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = httpClient ?? new HttpClient();
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Searches for extensions in the registry.
        /// GET /api/-/search?query=&amp;category=&amp;offset=&amp;size=&amp;sortBy=&amp;sortOrder=
        /// </summary>
        public async Task<SearchResult> SearchAsync(
            string query = "",
            string category = "",
            int offset = 0,
            int size = 20,
            string sortBy = "relevance",
            string sortOrder = "desc",
            CancellationToken cancellationToken = default)
        {
            var url = $"{_baseUrl}/api/-/search?query={Uri.EscapeDataString(query ?? "")}"
                    + $"&category={Uri.EscapeDataString(category ?? "")}"
                    + $"&offset={offset}&size={size}"
                    + $"&sortBy={Uri.EscapeDataString(sortBy)}"
                    + $"&sortOrder={Uri.EscapeDataString(sortOrder)}";

            return await GetJsonAsync<SearchResult>(url, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets details for the latest version of an extension.
        /// GET /api/{namespace}/{extension}
        /// </summary>
        public async Task<ExtensionDetail> GetExtensionAsync(
            string @namespace,
            string extension,
            CancellationToken cancellationToken = default)
        {
            var url = $"{_baseUrl}/api/{Uri.EscapeDataString(@namespace)}/{Uri.EscapeDataString(extension)}";
            return await GetJsonAsync<ExtensionDetail>(url, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets details for a specific version of an extension.
        /// GET /api/{namespace}/{extension}/{version}
        /// </summary>
        public async Task<ExtensionDetail> GetExtensionVersionAsync(
            string @namespace,
            string extension,
            string version,
            CancellationToken cancellationToken = default)
        {
            var url = $"{_baseUrl}/api/{Uri.EscapeDataString(@namespace)}/{Uri.EscapeDataString(extension)}/{Uri.EscapeDataString(version)}";
            return await GetJsonAsync<ExtensionDetail>(url, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets user reviews for an extension version.
        /// GET /api/{namespace}/{extension}/{version}/reviews
        /// </summary>
        public async Task<ReviewList> GetReviewsAsync(
            string @namespace,
            string extension,
            string version,
            CancellationToken cancellationToken = default)
        {
            var url = $"{_baseUrl}/api/{Uri.EscapeDataString(@namespace)}/{Uri.EscapeDataString(extension)}/{Uri.EscapeDataString(version)}/reviews";
            return await GetJsonAsync<ReviewList>(url, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a .vsix file for an extension version to the specified path.
        /// Supports progress reporting.
        /// GET /api/{namespace}/{extension}/{version}/file/{filename}
        /// </summary>
        public async Task DownloadVsixAsync(
            string @namespace,
            string extension,
            string version,
            string destinationFilePath,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            var filename = $"{@namespace}.{extension}-{version}.vsix";
            var url = $"{_baseUrl}/api/{Uri.EscapeDataString(@namespace)}/{Uri.EscapeDataString(extension)}/{Uri.EscapeDataString(version)}/file/{Uri.EscapeDataString(filename)}";

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath) ?? ".");

            using var response = await _http.GetAsync(url,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalRead += bytesRead;

                if (totalBytes > 0)
                    progress?.Report((double)totalRead / totalBytes);
            }

            progress?.Report(1.0);
        }

        /// <summary>
        /// Queries multiple extensions in batch.
        /// POST /api/-/query
        /// </summary>
        public async Task<QueryResult> QueryAsync(
            QueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var url = $"{_baseUrl}/api/-/query";
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<QueryResult>(responseJson, _jsonOptions);
        }

        private async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
        {
            var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _http?.Dispose();
        }
    }
}
