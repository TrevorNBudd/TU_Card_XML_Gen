using System.Globalization;
using System.Text.RegularExpressions;

namespace TU_Card_XML_Gen.NewCards
{
    /// <summary>
    /// One skill on a new card, as typed (max-level values).  Mirrors CardBuilder.ps1's skill hashtable:
    /// only the fields that apply are set.
    /// </summary>
    internal class NewCardSkill
    {
        public string Id = string.Empty;
        public bool All;
        public int? Y;
        public double? X;
        public bool XConstant;          // flurry: x stays flat across levels
        public string? Trigger;         // play / attacked / death
        public int? C;
        public int? N;
        public string? S;
        public string? S2;
        public string? SummonTargetName;
        public string Original = string.Empty;
    }

    /// <summary>
    /// Skill tables and the skill-line parser, ported from CardBuilder.ps1 (player mode).
    /// The regexes are CardBuilder's own (PowerShell's -match is case-insensitive, so these are too).
    /// Before parsing, a line is normalised so it accepts the same trigger and faction spellings as
    /// buff lines in the update file; output for CardBuilder-style input is unchanged.
    /// </summary>
    internal static class NewCardSkills
    {
        // Canonical sort order (CardBuilder $skillOrder).  Trigger markers sort a triggered skill.
        public static readonly Dictionary<string, double> SkillOrder = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "flying", 0.5 }, { "evade", 1 }, { "revenge", 2 }, { "tribute", 3 },
            { "absorb", 4 }, { "barrier", 5 }, { "stasis", 6 }, { "armored", 7 }, { "wall", 8 },
            { "allegiance", 9 },
            { "avenge", 10 }, { "scavenge", 11 },
            { "subdue", 12 },
            { "counter", 14 }, { "corrosive", 15 }, { "poison", 16 },
            { "inhibit", 17 }, { "sabotage", 18 }, { "disease", 19 }, { "bravery", 20 }, { "summon", 21 },
            { "evolve", 22 }, { "enhance", 23 }, { "overload", 24 }, { "rush", 25 },
            { "fortify", 26 }, { "heal", 27 }, { "protect", 28 }, { "entrap", 29 }, { "rally", 30 },
            { "enrage", 31 }, { "enfeeble", 32 }, { "strike", 33 }, { "jam", 34 }, { "sunder", 35 },
            { "besiege", 36 }, { "mimic", 37 },
            { "legion", 38 }, { "coalition", 39 }, { "venom", 40 }, { "pierce", 41 },
            { "hunt", 42 }, { "drain", 43 }, { "leech", 44 }, { "berserk", 45 }, { "mark", 46 },
            { "flurry", 47 },
            { "refresh", 48 },
            { "payback", 2 }, { "valor", 20 }, { "mend", 26 }, { "weaken", 35 },
        };

        public static readonly Dictionary<string, double> TriggerPosition = new Dictionary<string, double>
        {
            { "play", 0 }, { "attacked", 13 }, { "death", 49 },
        };

        // Typed word -> skill id.  CardBuilder's aliases, plus "zerk" (a buff-file shortcut).
        public static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "armor", "armored" }, { "mortar", "besiege" }, { "siege", "besiege" }, { "zerk", "berserk" },
        };

        public static readonly string[][] ExclusivePairs =
        {
            new[] { "payback", "revenge" },
            new[] { "valor", "bravery" },
            new[] { "mend", "fortify" },
            new[] { "weaken", "sunder" },
        };

        public static readonly HashSet<string> FactionValidSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mend", "fortify", "heal", "protect", "entrap", "rally", "enrage", "evolve", "enhance", "overload", "rush",
        };

        // Skills CardBuilder knows about (its order table and category map).  Anything else must be in skills_set.xml.
        private static readonly HashSet<string> CardBuilderSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "rupture", "swipe",
        };

        private static readonly string[] ZeroArgSkills = { "wall", "flying" };

        private static readonly Dictionary<string, int> FactionIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Imperial", 1 }, { "Raider", 2 }, { "Bloodthirsty", 3 }, { "Xeno", 4 }, { "Righteous", 5 }, { "Progenitor", 6 },
        };

        private const RegexOptions Ci = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private const string TriggerTail = @"(?:\s+On(?<trigger>Play|Attacked|Death))?\s*$";
        private const string Factions = "imperial|raider|bloodthirsty|xeno|righteous|progenitor";

        public static string ResolveSkillId(string typedWord)
        {
            string lower = typedWord.ToLowerInvariant();
            return Aliases.TryGetValue(lower, out string? id) ? id : lower;
        }

        public static bool IsKnownSkill(string id, ISet<string> skillsSetIds)
        {
            return SkillOrder.ContainsKey(id) || CardBuilderSkills.Contains(id) || skillsSetIds.Contains(id);
        }

        /// <summary>
        /// Rewrites the accepted spellings into CardBuilder's form: triggers as a trailing " OnPlay" /
        /// " OnAttacked" / " OnDeath" (written at the start or end, with or without "On" and a colon),
        /// faction abbreviations (RT, XN, ...) as full names, and "into" as "to".
        /// </summary>
        /// <param name="isCardName">For Summon lines: true if a name is a card (in this batch or the game data),
        /// so "Summon Angel of Death" keeps "Death" as part of the name.</param>
        public static string Normalize(string line, Func<string, bool> isCardName)
        {
            string text = Regex.Replace(line.Trim(), @"\s+", " ");
            string? trigger = null;

            // Leading trigger: "On Play: Jam all", "OnPlay Jam all", "Play: Jam all".  No skill is called
            // play/attacked/death, so a leading trigger word can't be mistaken for a skill.
            Match lead = Regex.Match(text, @"^(?:on\s?)?(?<t>play|attacked|death)\s*:?\s+(?<rest>.+)$", Ci);
            if (lead.Success)
            {
                trigger = lead.Groups["t"].Value;
                text = lead.Groups["rest"].Value.Trim();
            }

            bool isSummon = Regex.IsMatch(text, @"^summon\s", Ci);

            if (trigger == null)
            {
                Match tail = Regex.Match(text, @"^(?<rest>.+?)\s+(?<on>on\s?)?(?<t>play|attacked|death)\s*:?$", Ci);
                if (tail.Success)
                {
                    bool take = true;
                    if (isSummon && !tail.Groups["on"].Success)
                    {
                        // "Summon Angel of Death": keep "Death" if that's how the card is named.
                        string fullName = text.Substring(text.IndexOf(' ') + 1).Trim();
                        string shortName = tail.Groups["rest"].Value.Substring(tail.Groups["rest"].Value.IndexOf(' ') + 1).Trim();
                        take = !isCardName(fullName) || isCardName(shortName);
                    }

                    if (take)
                    {
                        trigger = tail.Groups["t"].Value;
                        text = tail.Groups["rest"].Value.Trim();
                    }
                }
            }

            if (!isSummon)
            {
                // Faction abbreviations and "into", anywhere after the skill name.
                string[] words = text.Split(' ');
                for (int i = 1; i < words.Length; i++)
                {
                    string? faction = SkillLineTokenizer.ResolveFaction(words[i]);
                    if (faction != null)
                    {
                        words[i] = faction;
                    }
                    else if (words[i].Equals("into", StringComparison.OrdinalIgnoreCase))
                    {
                        words[i] = "to";
                    }
                }

                text = string.Join(" ", words);
            }

            if (trigger != null)
            {
                text += " On" + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trigger.ToLowerInvariant());
            }

            return text;
        }

        /// <summary>
        /// Parses one (normalised) skill line, CardBuilder's Resolve-SkillLine.  Returns null if the line
        /// isn't a skill line in any of the accepted shapes.
        /// </summary>
        public static NewCardSkill? Parse(string line)
        {
            string firstWord = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (!Regex.IsMatch(firstWord, "^[A-Za-z]+$"))
            {
                return null;
            }

            string id = ResolveSkillId(firstWord);
            Match m;

            if (ZeroArgSkills.Contains(id))
            {
                return Regex.IsMatch(line.Trim(), @"^[A-Za-z]+\s*$", Ci) ? new NewCardSkill { Id = id } : null;
            }

            if (id == "summon")
            {
                m = Regex.Match(line, @"^Summon\s+(?<name>.+?)" + TriggerTail, Ci);
                if (!m.Success) return null;
                return WithTrigger(new NewCardSkill { Id = "summon", SummonTargetName = m.Groups["name"].Value.Trim() }, m);
            }

            if (id == "enhance")
            {
                m = Regex.Match(line, @"^Enhance\s+(?:(?<n>\d+)\s+|(?<all>all)\s+)?(?<target>[A-Za-z]+)\s+(?<x>\d+)" + TriggerTail, Ci);
                if (!m.Success) return null;
                NewCardSkill s = new NewCardSkill { Id = "enhance", S = ResolveSkillId(m.Groups["target"].Value), X = double.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture) };
                SetAllOrN(s, m);
                return WithTrigger(s, m);
            }

            if (id == "jam")
            {
                if (Regex.IsMatch(line, @"^Jam\s+\d+\s*->\s*all", Ci))
                {
                    throw new FormatException("\"Jam n -> all\" is only for mission cards (CardBuilder -UseMissionSet).");
                }

                m = Regex.Match(line, @"^Jam(?:\s+(?<n>\d+)|\s+(?<all>all))?(?:\s+every\s+(?<c>\d+))?" + TriggerTail, Ci);
                if (!m.Success || !(m.Groups["n"].Success || m.Groups["c"].Success || m.Groups["all"].Success)) return null;
                NewCardSkill s = new NewCardSkill { Id = "jam" };
                SetAllOrN(s, m);
                SetC(s, m);
                return WithTrigger(s, m);
            }

            if (id == "overload")
            {
                m = Regex.Match(line, @"^Overload(?:\s+(?<n>\d+)|\s+(?<all>all))?(?:\s+(?<faction>" + Factions + "))?" + TriggerTail, Ci);
                if (!m.Success) return null;
                NewCardSkill s = new NewCardSkill { Id = "overload" };
                SetAllOrN(s, m);
                SetFaction(s, m);
                return WithTrigger(s, m);
            }

            if (id == "evolve")
            {
                m = Regex.Match(line, @"^Evolve\s+(?:(?<n>\d+)\s+)?(?<s>[A-Za-z]+)\s+(?:to\s+)?(?<s2>[A-Za-z]+)" + TriggerTail, Ci);
                if (!m.Success) return null;
                NewCardSkill s = new NewCardSkill { Id = "evolve", S = ResolveSkillId(m.Groups["s"].Value), S2 = ResolveSkillId(m.Groups["s2"].Value) };
                if (m.Groups["n"].Success && m.Groups["n"].Value != "1") s.N = int.Parse(m.Groups["n"].Value);
                return WithTrigger(s, m);
            }

            if (id == "flurry")
            {
                m = Regex.Match(line, @"^Flurry(?:\s+(?<x>\d+))?(?:\s+every\s+(?<c>\d+))?" + TriggerTail, Ci);
                if (!m.Success || !(m.Groups["x"].Success || m.Groups["c"].Success)) return null;
                NewCardSkill s = new NewCardSkill { Id = "flurry" };
                if (m.Groups["x"].Success) { s.X = double.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture); s.XConstant = true; }
                SetC(s, m);
                return WithTrigger(s, m);
            }

            // standard: <Name> [n | all] [Faction] <x> [OnTrigger]
            m = Regex.Match(line, @"^[A-Za-z]+(?:\s+(?:(?<n>\d+)|(?<all>all)))?(?:\s+(?<faction>" + Factions + @"))?\s+(?<x>\d+)" + TriggerTail, Ci);
            if (!m.Success) return null;
            if (m.Groups["n"].Success && int.Parse(m.Groups["n"].Value) < 1) return null;
            NewCardSkill std = new NewCardSkill { Id = id, X = double.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture) };
            SetAllOrN(std, m);
            SetFaction(std, m);
            return WithTrigger(std, m);
        }

        private static void SetAllOrN(NewCardSkill s, Match m)
        {
            if (m.Groups["all"].Success) s.All = true;
            else if (m.Groups["n"].Success && m.Groups["n"].Value != "1") s.N = int.Parse(m.Groups["n"].Value);
        }

        private static void SetC(NewCardSkill s, Match m)
        {
            if (m.Groups["c"].Success && m.Groups["c"].Value != "1") s.C = int.Parse(m.Groups["c"].Value);
        }

        private static void SetFaction(NewCardSkill s, Match m)
        {
            if (m.Groups["faction"].Success && FactionIds.TryGetValue(m.Groups["faction"].Value, out int y)) s.Y = y;
        }

        private static NewCardSkill WithTrigger(NewCardSkill s, Match m)
        {
            if (m.Groups["trigger"].Success) s.Trigger = m.Groups["trigger"].Value.ToLowerInvariant();
            return s;
        }

        private static string Label(NewCardSkill s) => s.Id + (s.Trigger != null ? " (on " + s.Trigger + ")" : "");

        public static double SortKey(NewCardSkill s) => SortKey(s.Id, s.Trigger);

        /// <summary>Where a skill goes in COMDEV's standard order: by its trigger if it has one, else by its id.  Unknown = last.</summary>
        public static double SortKey(string id, string? trigger)
        {
            if (trigger != null && TriggerPosition.TryGetValue(trigger, out double t)) return t;
            return SkillOrder.TryGetValue(id, out double k) ? k : 999;
        }

        /// <summary>items in standard order (a stable sort: skills with the same position keep their typed order).</summary>
        public static List<T> InStandardOrder<T>(IList<T> items, Func<T, string> id, Func<T, string?> trigger)
        {
            return items.Select((s, i) => (Skill: s, Index: i))
                .OrderBy(x => SortKey(id(x.Skill), trigger(x.Skill)))
                .ThenBy(x => x.Index)
                .Select(x => x.Skill)
                .ToList();
        }

        /// <summary>
        /// CardBuilder's Confirm-AndSort-Skills: rejects exclusive pairs and repeated skills, and returns the
        /// skills in canonical order.  reorderedNote is set when the typed order was different.
        /// </summary>
        public static List<NewCardSkill> ValidateAndSort(List<NewCardSkill> skills, out string? reorderedNote, bool reorder = true)
        {
            reorderedNote = null;

            List<string> ids = skills.Select(x => x.Id).ToList();
            foreach (string[] pair in ExclusivePairs)
            {
                if (ids.Contains(pair[0]) && ids.Contains(pair[1]))
                {
                    throw new FormatException($"\"{pair[0]}\" and \"{pair[1]}\" can't both be on the same card.");
                }
            }

            string? dupe = ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).FirstOrDefault();
            if (dupe != null)
            {
                throw new FormatException($"skill \"{dupe}\" is listed more than once (check for a name/alias typo, e.g. Siege + Mortar are the same skill).");
            }

            List<NewCardSkill> sorted = InStandardOrder(skills, x => x.Id, x => x.Trigger);

            if (!sorted.SequenceEqual(skills))
            {
                reorderedNote = reorder
                    ? "skills reordered. Corrected order: " + string.Join(", ", sorted.Select(Label))
                    : "skills kept in the order typed (REORDER: FALSE), but COMDEV's standard order would be: " + string.Join(", ", sorted.Select(Label));
            }

            return reorder ? sorted : skills;
        }
    }
}
