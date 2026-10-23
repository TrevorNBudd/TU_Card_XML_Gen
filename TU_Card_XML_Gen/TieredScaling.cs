using System.Globalization;

namespace TU_Card_XML_Gen
{
    /// <summary>
    /// A percentage range, e.g. 50..60: level 1 of a tier gets Low% of max, the tier's max level gets High%.
    /// </summary>
    public class ScaleRange
    {
        public double Low;
        public double High;

        public ScaleRange(double low, double high)
        {
            Low = low;
            High = high;
        }

        /// <summary>Parses "50..60", "50 .. 60%", or a single "75" (same value at every level).</summary>
        public static ScaleRange Parse(string text)
        {
            string cleaned = text.Replace("%", "").Trim();
            string[] parts = cleaned.Split("..", StringSplitOptions.TrimEntries);

            if (parts.Length < 1 || parts.Length > 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double low)
                || !double.TryParse(parts[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double high))
            {
                throw new FormatException($"\"{text}\" isn't a scale range - write it like 50..60");
            }

            if (low < 0 || high < low)
            {
                throw new FormatException($"Scale range \"{text}\" must go from low to high, and not below 0.");
            }

            return new ScaleRange(low, high);
        }

        public override string ToString() => $"{Low.ToString(CultureInfo.InvariantCulture)}..{High.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Tiered scaling: each fusion level has its own percentage range of the max values given in the update file,
    /// spread linearly from level 1 to max level of that tier.  Summoned cards have their own range.
    /// </summary>
    public class TieredScaling
    {
        /// <summary>Index 0 = fusion level 0 (the base card), 1 = first fusion, ...  Higher fusion levels use the last entry.</summary>
        public List<ScaleRange> FusionLevels = new List<ScaleRange>();
        public ScaleRange Summoned = new ScaleRange(50, 100);

        public static TieredScaling BuiltInDefault()
        {
            return new TieredScaling
            {
                FusionLevels = new List<ScaleRange> { new ScaleRange(50, 60), new ScaleRange(60, 80), new ScaleRange(80, 100) },
                Summoned = new ScaleRange(50, 100)
            };
        }

        /// <summary>
        /// Which range each stage of a chain uses, lowest stage first.  -1 = no range: leave that stage unchanged.
        ///
        /// The top card's own fusion level (its &lt;fusion_level&gt; tag; none = 0) picks its range, and each stage below
        /// it takes the range below.  With the usual three ranges (fusion levels 0, 1, 2) that is simply the card's
        /// fusion level, so a lone Fusion 2 card gets 80..100 and a normal chain gets 0, 1, 2.  A chain with more stages
        /// than ranges (a four-stage commander: Epic, Legendary, Fusion 1, Fusion 2) scales its last stages and leaves
        /// the first unchanged.  Extra ranges (four or more) are for those extra lower stages: the last three ranges
        /// stay fusion levels 0, 1, 2.  A single range applies to every stage.
        /// </summary>
        public List<int> RangeIndexes(int stages, int topFusionLevel)
        {
            int ranges = FusionLevels.Count;
            if (ranges == 1)
            {
                return Enumerable.Repeat(0, stages).ToList();
            }

            int top = Math.Clamp(ranges - 3 + Math.Clamp(topFusionLevel, 0, 2), 0, ranges - 1);
            return Enumerable.Range(0, stages).Select(i => top - (stages - 1 - i)).ToList();
        }

        /// <summary>
        /// Fraction of max (0..1) for one level of one tier.
        /// </summary>
        /// <param name="fusionLevel">0 for the base card, 1 for its first fusion, ...</param>
        /// <param name="summoned">Summoned cards use the Summoned range on every tier.</param>
        /// <param name="level">1-based upgrade level within the tier.</param>
        /// <param name="levelCount">Number of upgrade levels in the tier (base card counts as level 1).</param>
        public double Factor(int fusionLevel, bool summoned, int level, int levelCount)
        {
            ScaleRange range = summoned ? Summoned : FusionLevels[Math.Min(fusionLevel, FusionLevels.Count - 1)];

            double position = levelCount <= 1 ? 1.0 : (double)(level - 1) / (levelCount - 1);

            return (range.Low + (range.High - range.Low) * position) / 100.0;
        }

        /// <summary>
        /// Parses the text after "SCALE:" in an update-file comment.  Returns null for DEFAULT (go back to config.xml's setting).
        ///   50..100                         every card, including summoned cards
        ///   50..60, 60..80, 80..100         fusion levels 0, 1, 2; summoned cards keep the config's range
        ///   ..., SUMMON 50..100             also sets the summoned range
        /// </summary>
        public static TieredScaling? ParseDirective(string text, TieredScaling configTiered)
        {
            string trimmed = text.Trim();

            if (trimmed.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            TieredScaling result = new TieredScaling();
            ScaleRange? summoned = null;

            foreach (string rawItem in trimmed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (rawItem.StartsWith("summon", StringComparison.OrdinalIgnoreCase))
                {
                    // "SUMMON 50..100", "Summon: 50..100", "Summoned 50..100"
                    string rangeText = rawItem.TrimStart('S', 's', 'U', 'u', 'M', 'm', 'O', 'o', 'N', 'n', 'E', 'e', 'D', 'd')
                        .Trim().TrimStart(':').Trim();
                    summoned = ScaleRange.Parse(rangeText);
                }
                else
                {
                    result.FusionLevels.Add(ScaleRange.Parse(rawItem));
                }
            }

            if (result.FusionLevels.Count == 0)
            {
                throw new FormatException($"SCALE needs at least one range, e.g. \"SCALE: 50..60, 60..80, 80..100\" or \"SCALE: DEFAULT\".");
            }

            // One range on its own means every card, summoned ones included.
            result.Summoned = summoned ?? (result.FusionLevels.Count == 1 ? result.FusionLevels[0] : configTiered.Summoned);

            return result;
        }

        public override string ToString()
        {
            return $"fusion levels {string.Join(", ", FusionLevels)}; summoned {Summoned}";
        }
    }
}
