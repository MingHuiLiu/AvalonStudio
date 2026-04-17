using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AvalonStudio.Extensions.OpenVSX.Models
{
    public class SearchResult
    {
        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("totalSize")]
        public int TotalSize { get; set; }

        [JsonPropertyName("extensions")]
        public List<ExtensionEntry> Extensions { get; set; } = new List<ExtensionEntry>();
    }

    public class ExtensionEntry
    {
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new List<string>();

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonPropertyName("averageRating")]
        public double? AverageRating { get; set; }

        [JsonPropertyName("downloadCount")]
        public long? DownloadCount { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; }

        [JsonPropertyName("files")]
        public Dictionary<string, string> Files { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("allVersions")]
        public Dictionary<string, string> AllVersions { get; set; } = new Dictionary<string, string>();

        /// <summary>Full identifier: namespace.name</summary>
        public string UniqueId => $"{Namespace}.{Name}";
    }

    public class ExtensionDetail : ExtensionEntry
    {
        [JsonPropertyName("publisher")]
        public PublisherInfo Publisher { get; set; }

        [JsonPropertyName("readme")]
        public string Readme { get; set; }

        [JsonPropertyName("changelog")]
        public string Changelog { get; set; }

        [JsonPropertyName("dependencies")]
        public List<string> Dependencies { get; set; } = new List<string>();

        [JsonPropertyName("bundledExtensions")]
        public List<string> BundledExtensions { get; set; } = new List<string>();

        [JsonPropertyName("engines")]
        public Dictionary<string, string> Engines { get; set; } = new Dictionary<string, string>();
    }

    public class PublisherInfo
    {
        [JsonPropertyName("loginName")]
        public string LoginName { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("avatarUrl")]
        public string AvatarUrl { get; set; }
    }

    public class ReviewList
    {
        [JsonPropertyName("postUrl")]
        public string PostUrl { get; set; }

        [JsonPropertyName("reviews")]
        public List<Review> Reviews { get; set; } = new List<Review>();
    }

    public class Review
    {
        [JsonPropertyName("reviewer")]
        public ReviewerInfo Reviewer { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("comment")]
        public string Comment { get; set; }

        [JsonPropertyName("rating")]
        public int Rating { get; set; }
    }

    public class ReviewerInfo
    {
        [JsonPropertyName("loginName")]
        public string LoginName { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }
    }

    public class QueryRequest
    {
        [JsonPropertyName("extensionId")]
        public List<string> ExtensionId { get; set; }

        [JsonPropertyName("extensionVersion")]
        public List<string> ExtensionVersion { get; set; }

        [JsonPropertyName("queryType")]
        public int QueryType { get; set; }

        [JsonPropertyName("flags")]
        public int Flags { get; set; }
    }

    public class QueryResult
    {
        [JsonPropertyName("extensions")]
        public List<ExtensionEntry> Extensions { get; set; } = new List<ExtensionEntry>();
    }
}
