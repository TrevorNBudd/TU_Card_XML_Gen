using System.Text.RegularExpressions;

namespace TU_Card_XML_Gen
{
    /// <summary>
    /// One card's worth of lines from the update file, with comments already removed.
    /// </summary>
    internal class UpdateBlock
    {
        public List<string> Lines = new List<string>();

        /// <summary>1-based line number in the update file where this card starts, for error messages.</summary>
        public int StartLine;

        /// <summary>True when the gap before this card contained a "---" line, i.e. this card is summoned by the card before it.</summary>
        public bool SummonedByPrevious;

        /// <summary>Tiered scaling set by a "// SCALE:" comment above this card, or null to use config.xml's setting.</summary>
        public TieredScaling? Scaling;

        /// <summary>Set id from a "// SET: n" comment above this card (new cards only), or null for config.xml's.</summary>
        public string? SetId;

        /// <summary>From "// NEW: TRUE" / "// BUFF: TRUE" comments above this card.  Auto = decide from the lines.</summary>
        public BlockKind Kind = BlockKind.Auto;

        /// <summary>From "// REORDER: TRUE|FALSE" (or REORDERSKILLS) above this card.  null (or AUTO) = config.xml's ReorderSkills.</summary>
        public bool? ReorderSkills;

        public string Name => Lines.Count > 0 ? Lines[0] : string.Empty;
    }

    internal enum BlockKind
    {
        Auto,
        New,
        Buff
    }

    /// <summary>
    /// Splits an update file into cards.
    ///
    /// Rules:
    ///  - Everything from "//" to the end of a line is a comment and is ignored.
    ///  - A line that is only a comment is dropped entirely, so it never splits a card.
    ///  - Cards are separated by one or more blank lines and/or "---" lines (three or more dashes).
    ///    Any mix of blank, comment and "---" lines between two cards counts as one gap.
    ///  - If the gap contains a "---" line, the next card is marked as summoned by the previous one.
    ///  - A "// SCALE: ..." comment sets tiered scaling for every card that starts after it
    ///    (including a card whose name line it's on), until the next SCALE comment.
    ///  - "// SET: n" sets the set id of the new cards that follow (like CardBuilder's directive).
    ///  - "// NEW: TRUE" / "// BUFF: TRUE" (or NEW / BUFF on their own) make the cards that follow new
    ///    cards / buffs; "// NEW: AUTO" goes back to deciding from each card's lines.
    ///  - "// REORDER: TRUE" / "FALSE" (or REORDERSKILLS) puts the skills of the cards that follow into
    ///    COMDEV's standard order / keeps them as typed; "// REORDER: AUTO" goes back to config.xml's setting.
    /// Works with both Windows (CRLF) and Unix (LF) line endings.
    /// </summary>
    internal static class UpdateFileReader
    {
        private static readonly Regex SummonSeparator = new Regex(@"^-{3,}$");
        private static readonly Regex ScaleDirective = new Regex(@"^\s*scale\s*:(.*)$", RegexOptions.IgnoreCase);
        private static readonly Regex SetDirective = new Regex(@"^\s*set\s*:\s*(?<set>\d+)\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex ReorderDirective = new Regex(@"^\s*\[?\s*reorder(?:skills)?\s*(?::\s*\[?\s*(?<value>true|false|auto)\s*\]?)?\s*\]?\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex KindDirective = new Regex(@"^\s*\[?\s*(?<kind>new|buff)\s*(?::\s*(?<value>true|false|auto))?\s*\]?\s*$", RegexOptions.IgnoreCase);

        public static List<UpdateBlock> Read(string path, TieredScaling configTiered)
        {
            return Parse(File.ReadAllLines(path), configTiered);
        }

        public static List<UpdateBlock> Parse(IEnumerable<string> rawLines, TieredScaling configTiered)
        {
            List<UpdateBlock> blocks = new List<UpdateBlock>();
            UpdateBlock? current = null;
            bool gapHasSummonSeparator = false;
            TieredScaling? currentScaling = null;
            string? currentSet = null;
            BlockKind currentKind = BlockKind.Auto;
            bool? currentReorder = null;
            int lineNumber = 0;

            foreach (string rawLine in rawLines)
            {
                lineNumber++;

                bool hadComment = rawLine.Contains("//");

                if (hadComment)
                {
                    string commentText = rawLine.Substring(rawLine.IndexOf("//", StringComparison.Ordinal) + 2);

                    Match setDirective = SetDirective.Match(commentText);
                    if (setDirective.Success)
                    {
                        currentSet = setDirective.Groups["set"].Value;
                        Console.WriteLine($"Line {lineNumber}: new cards that follow use set {currentSet}.");
                    }

                    Match kindDirective = KindDirective.Match(commentText);
                    if (kindDirective.Success)
                    {
                        bool isNew = kindDirective.Groups["kind"].Value.Equals("new", StringComparison.OrdinalIgnoreCase);
                        string value = kindDirective.Groups["value"].Success ? kindDirective.Groups["value"].Value.ToLowerInvariant() : "true";

                        currentKind = value == "auto" ? BlockKind.Auto
                            : (isNew == (value == "true")) ? BlockKind.New : BlockKind.Buff;

                        Console.WriteLine($"Line {lineNumber}: the cards that follow are "
                            + (currentKind == BlockKind.Auto ? "detected automatically (new cards have a Picture: line)" : currentKind == BlockKind.New ? "new cards" : "buffs") + ".");
                    }

                    Match reorderDirective = ReorderDirective.Match(commentText);
                    if (reorderDirective.Success)
                    {
                        string value = reorderDirective.Groups["value"].Success ? reorderDirective.Groups["value"].Value.ToLowerInvariant() : "true";
                        currentReorder = value == "auto" ? null : value == "true";
                        Console.WriteLine($"Line {lineNumber}: skills of the cards that follow are "
                            + (currentReorder == null ? "reordered or not as config.xml's ReorderSkills says" : currentReorder.Value ? "put into COMDEV's standard order" : "kept in the order typed") + ".");
                    }

                    Match directive = ScaleDirective.Match(commentText);
                    if (directive.Success)
                    {
                        try
                        {
                            currentScaling = TieredScaling.ParseDirective(directive.Groups[1].Value, configTiered);
                            Console.WriteLine($"Line {lineNumber}: scaling for the cards that follow is now "
                                + (currentScaling == null ? "the config.xml default" : $"tiered ({currentScaling})") + ".");
                        }
                        catch (FormatException ex)
                        {
                            // A bad SCALE line would quietly mis-scale every card after it, so stop the run instead.
                            throw new FormatException($"Line {lineNumber} of the update file: {ex.Message}", ex);
                        }
                    }
                }

                string line = StripComment(rawLine).Trim();

                if (line.Length == 0 && hadComment)
                {
                    // comment-only line: ignore it completely, inside or between cards.
                    continue;
                }

                if (line.Length == 0 || SummonSeparator.IsMatch(line))
                {
                    // end of a card (or more of an existing gap)
                    if (SummonSeparator.IsMatch(line))
                    {
                        gapHasSummonSeparator = true;
                    }

                    current = null;
                    continue;
                }

                if (current == null)
                {
                    current = new UpdateBlock
                    {
                        StartLine = lineNumber,
                        SummonedByPrevious = gapHasSummonSeparator,
                        Scaling = currentScaling,
                        SetId = currentSet,
                        Kind = currentKind,
                        ReorderSkills = currentReorder
                    };
                    blocks.Add(current);
                    gapHasSummonSeparator = false;
                }

                current.Lines.Add(line);
            }

            return blocks;
        }

        public static string StripComment(string line)
        {
            int commentStart = line.IndexOf("//", StringComparison.Ordinal);
            return commentStart >= 0 ? line.Substring(0, commentStart) : line;
        }
    }
}
