using System.Collections.Generic;

namespace AvalonStudio.Languages
{
    /// <summary>
    /// Represents a single code lens annotation displayed above a symbol in the editor.
    /// </summary>
    public class CodeLens
    {
        /// <summary>1-based line number where the lens should appear.</summary>
        public int Line { get; set; }

        /// <summary>1-based start column of the symbol span.</summary>
        public int Column { get; set; }

        /// <summary>Human-readable label shown in the editor (e.g. "3 references").</summary>
        public string Label { get; set; }

        /// <summary>Optional tooltip / description.</summary>
        public string Description { get; set; }

        /// <summary>
        /// When the user activates this lens, navigate to this list of locations.
        /// Each entry is a file path + location string such as "file.cs:10:5".
        /// </summary>
        public IList<string> NavigationTargets { get; set; } = new List<string>();
    }
}
