using AvalonStudio.Languages;

namespace AvalonStudio.LanguageServer.OmniSharp
{
    internal static class ContentCapabilities
    {
        public const string OmniSharpCSharp = nameof(OmniSharpCSharp);
    }

    internal class ContentTypes
    {
        [ExportContentType("C# (OmniSharp)", ContentCapabilities.OmniSharpCSharp)]
        [FileExtensions(".cs", ".csx")]
        public object OmniSharpCSharpContentType { get; }
    }
}
