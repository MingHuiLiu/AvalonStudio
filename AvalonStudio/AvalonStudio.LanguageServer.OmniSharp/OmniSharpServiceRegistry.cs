using AvalonStudio.Projects;
using AvalonStudio.Utils;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace AvalonStudio.LanguageServer.OmniSharp
{
    /// <summary>
    /// Maintains a one-to-one mapping of AvalonStudio <see cref="ISolution"/> to
    /// <see cref="OmniSharpServerManager"/> instances so that a single OmniSharp process
    /// is shared by all C# editors belonging to the same solution.
    /// </summary>
    public static class OmniSharpServiceRegistry
    {
        private static readonly ConcurrentDictionary<string, OmniSharpServerManager> s_servers
            = new ConcurrentDictionary<string, OmniSharpServerManager>(StringComparer.OrdinalIgnoreCase);

        public static OmniSharpServerManager GetOrCreateServer(ISolution solution)
        {
            string key = solution?.CurrentDirectory ?? AppContext.BaseDirectory;

            return s_servers.GetOrAdd(key, k =>
            {
                string solutionFile = FindSolutionFile(k);
                return new OmniSharpServerManager(solutionFile ?? k);
            });
        }

        public static void DisposeServer(ISolution solution)
        {
            string key = solution?.CurrentDirectory ?? AppContext.BaseDirectory;

            if (s_servers.TryRemove(key, out var mgr))
            {
                mgr.Dispose();
            }
        }

        private static string FindSolutionFile(string directory)
        {
            if (!Directory.Exists(directory)) return null;

            foreach (string f in Directory.GetFiles(directory, "*.sln"))
                return f;

            // Fall back to the directory itself
            return directory;
        }
    }
}
