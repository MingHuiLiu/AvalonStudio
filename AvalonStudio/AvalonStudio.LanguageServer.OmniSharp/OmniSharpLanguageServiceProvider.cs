using AvalonStudio.Languages;

namespace AvalonStudio.LanguageServer.OmniSharp
{
    [ExportLanguageServiceProvider(ContentCapabilities.OmniSharpCSharp)]
    internal class OmniSharpLanguageServiceProvider : ILanguageServiceProvider
    {
        public ILanguageService CreateLanguageService() => new OmniSharpLanguageService();
    }
}
