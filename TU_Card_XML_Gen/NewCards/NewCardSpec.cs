using System.Text.RegularExpressions;

namespace TU_Card_XML_Gen.NewCards
{
    internal class RecipeResource
    {
        public int Id;
        public int Number = 1;
    }

    internal class PictureReplacement
    {
        public string CardName = string.Empty;
        public int CardId;
        public string Picture = string.Empty;
        public string? Bundle;
    }

    /// <summary>
    /// One new-card block from the update file, parsed.  A chain ("A / B / C") is one spec with several names.
    /// </summary>
    internal class NewCardSpec
    {
        public List<string> Names = new List<string>();
        public int StartLine;
        public int Faction;
        public int Rarity;
        public string CardType = "Assault";     // Assault or Structure (Commander/Dominion are built as Structure)
        public int? ExplicitFusionTier;
        public double? Attack;                  // null for Structures
        public double Health;
        public string Cost = "0";
        public List<NewCardSkill> Skills = new List<NewCardSkill>();
        public List<int>? Upgrades;
        public string SetId = "2500";
        public List<RecipeResource>? Recipe;
        public string Picture = string.Empty;
        public string? Bundle;
        public List<PictureReplacement> ReplacePictures = new List<PictureReplacement>();
        public List<string> Notes = new List<string>();       // informational (reordered skills)
        public List<string> Warnings = new List<string>();    // needs attention, card still built

        public string DisplayName => string.Join(" / ", Names);
    }

    /// <summary>
    /// Reads a new-card block, following CardBuilder.ps1's player-mode reader (FIND_TYPE .. FIND_REPLACE_PICTURE).
    /// Lines arrive trimmed with comments removed.  Anything CardBuilder would skip the card for throws a
    /// FormatException with the reason.
    /// </summary>
    internal static class NewCardParser
    {
        private const RegexOptions Ci = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly Dictionary<string, int> FactionIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Imperial", 1 }, { "Raider", 2 }, { "Bloodthirsty", 3 }, { "Xeno", 4 }, { "Righteous", 5 }, { "Progenitor", 6 },
        };

        private static readonly Dictionary<string, int> RarityIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Epic", 3 }, { "Legendary", 4 }, { "Vindicator", 5 }, { "Mythic", 6 },
        };

        /// <summary>Lines that only appear in new-card blocks.  Used to tell new cards from buffs.</summary>
        public static readonly Regex NewCardLine = new Regex(@"^(Picture|Replace\s+Picture|Upgrades|Set|Fusion\s+Recipe)\s*:", Ci);

        public static bool LooksLikeNewCard(IEnumerable<string> lines) => lines.Skip(1).Any(l => NewCardLine.IsMatch(l));

        public static NewCardSpec Parse(UpdateBlock block, string defaultSetId, Func<string, bool> isCardName, ISet<string> skillsSetIds, bool reorderSkills = true)
        {
            List<string> lines = block.Lines;
            NewCardSpec spec = new NewCardSpec { StartLine = block.StartLine, SetId = defaultSetId };

            spec.Names = lines[0].Split('/').Select(x => x.Trim()).ToList();
            if (spec.Names.Any(string.IsNullOrEmpty))
            {
                throw new FormatException($"Name line \"{lines[0]}\" has an empty name.");
            }

            if (lines.Count < 2)
            {
                throw new FormatException("needs a faction/rarity line, a stats line and a Picture: line.");
            }

            // ---- line 2: <Faction> <Rarity> [Structure|Commander|Dominion] [Fusion0|1|2] [BaseID:n] ----
            Match type = Regex.Match(lines[1], @"^(?<faction>\w+)\s+(?<rarity>\w+)(?:\s+(?<cardtype>Structure|Commander|Dominion|Fortress))?(?:\s+Fusion(?<fusion>[0-2]))?(?:\s+BaseID:(?<baseid>\d+))?\s*$", Ci);
            if (!type.Success)
            {
                throw new FormatException($"Invalid type/rarity line: \"{lines[1]}\". Write it like \"Xeno Vindicator\" or \"Raider Epic Structure\".");
            }

            string cardType = type.Groups["cardtype"].Success ? type.Groups["cardtype"].Value : "Assault";
            if (cardType.Equals("Fortress", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("\"Fortress\" is only for mission cards (player/guild fortresses aren't supported).");
            }

            if (cardType.Equals("Commander", StringComparison.OrdinalIgnoreCase) || cardType.Equals("Dominion", StringComparison.OrdinalIgnoreCase))
            {
                spec.Warnings.Add($"COMDEV doesn't currently create player-collectible {cardType}s, so this is being built as a Structure instead. MANUAL EDITING IS REQUIRED to turn it into a real {cardType}. The stats line must be in Structure format (health/cost, no attack).");
                cardType = "Structure";
            }

            spec.CardType = cardType.Equals("Structure", StringComparison.OrdinalIgnoreCase) ? "Structure" : "Assault";

            if (!FactionIds.TryGetValue(type.Groups["faction"].Value, out spec.Faction) || !RarityIds.TryGetValue(type.Groups["rarity"].Value, out spec.Rarity))
            {
                throw new FormatException($"Invalid faction/rarity line: \"{lines[1]}\". Factions: Imperial, Raider, Bloodthirsty, Xeno, Righteous, Progenitor. Rarities: Epic, Legendary, Vindicator, Mythic.");
            }

            if (type.Groups["fusion"].Success)
            {
                spec.ExplicitFusionTier = int.Parse(type.Groups["fusion"].Value);
            }

            // ---- line 3: stats ----
            if (lines.Count < 3)
            {
                throw new FormatException("needs a stats line and a Picture: line.");
            }

            string stats = Regex.Replace(lines[2], @"\s*/\s*", "/");
            if (spec.CardType == "Structure")
            {
                Match m = Regex.Match(stats, @"^(\d+)/(\d+)$");
                if (!m.Success)
                {
                    throw new FormatException($"Expected a Structure health/cost line like \"40/1\" (no attack slot), got: \"{lines[2]}\".");
                }

                spec.Health = double.Parse(m.Groups[1].Value);
                spec.Cost = m.Groups[2].Value;
            }
            else
            {
                Match m = Regex.Match(stats, @"^(\d+)/(\d+)/(\d+)$");
                if (!m.Success)
                {
                    throw new FormatException($"Invalid attack/health/cost: \"{lines[2]}\". Write it like 20/38/3.");
                }

                spec.Attack = double.Parse(m.Groups[1].Value);
                spec.Health = double.Parse(m.Groups[2].Value);
                spec.Cost = m.Groups[3].Value;
            }

            // ---- line 4+: skills, Upgrades:, Set:, Fusion Recipe:, then Picture: and Replace Picture: ----
            string mode = "skills";   // skills -> afterRecipe -> afterPicture

            for (int i = 3; i < lines.Count; i++)
            {
                string line = lines[i];

                if (mode == "afterPicture")
                {
                    Match rp = Regex.Match(line, @"^Replace\s+Picture:\s*(?<name>[^\[]+)\[(?<id>\d+)\]\s*[:;]\s*(?<picture>[^:]+?)(?::(?<bundle>\d+))?\s*$", Ci);
                    if (!rp.Success)
                    {
                        throw new FormatException($"Only Replace Picture: lines can follow Picture:, got \"{line}\". Write it like \"Replace Picture: Old Card[1234]: newpic_lv1:99\".");
                    }

                    spec.ReplacePictures.Add(new PictureReplacement
                    {
                        CardName = rp.Groups["name"].Value.Trim(),
                        CardId = int.Parse(rp.Groups["id"].Value),
                        Picture = rp.Groups["picture"].Value.Trim(),
                        Bundle = rp.Groups["bundle"].Success ? rp.Groups["bundle"].Value : null
                    });
                    continue;
                }

                Match set = Regex.Match(line, @"^Set:\s*(?<setid>\d+)$", Ci);
                if (set.Success)
                {
                    spec.SetId = set.Groups["setid"].Value;
                    continue;
                }

                if (Regex.IsMatch(line, @"^Picture:", Ci))
                {
                    Match pic = Regex.Match(line, @"^Picture:\s*(?<picture>[^:]+?)(?::(?<bundle>\d+))?$", Ci);
                    if (!pic.Success)
                    {
                        throw new FormatException($"Invalid picture: \"{line}\". Write it like \"Picture: tick_lv3:137\" (file, then an optional :bundle number).");
                    }

                    spec.Picture = pic.Groups["picture"].Value.Trim();
                    spec.Bundle = pic.Groups["bundle"].Success ? pic.Groups["bundle"].Value : null;
                    mode = "afterPicture";
                    continue;
                }

                if (mode == "afterRecipe")
                {
                    throw new FormatException($"After Fusion Recipe: only Set: and Picture: can follow, got \"{line}\". Put skills and Upgrades: before the recipe.");
                }

                Match up = Regex.Match(line, @"^Upgrades:\s*(?<counts>[\d\s/]+)$", Ci);
                if (up.Success)
                {
                    List<string> parts = up.Groups["counts"].Value.Split('/').Select(x => x.Trim()).ToList();
                    if (parts.Count != spec.Names.Count || parts.Any(x => !int.TryParse(x, out _)))
                    {
                        throw new FormatException($"Upgrades: line has {parts.Count} value(s) but the name line has {spec.Names.Count}.");
                    }

                    spec.Upgrades = parts.Select(int.Parse).ToList();
                    continue;
                }

                if (Regex.IsMatch(line, @"^(StubLevel|MinRatio|ID|NextAvailable\w*ID):", Ci))
                {
                    throw new FormatException($"\"{line.Split(':')[0]}:\" is only for mission cards. Player cards take their ids from the cards_section_21.xml header.");
                }

                Match recipe = Regex.Match(line, @"^Fusion\s+Recipe:\s*(.*)$", Ci);
                if (recipe.Success)
                {
                    spec.Recipe = new List<RecipeResource>();
                    foreach (string resource in recipe.Groups[1].Value.Split(',', '+').Select(x => x.Trim()))
                    {
                        Match res = Regex.Match(resource, @"^.+?\[(\d+)\]");
                        if (!res.Success)
                        {
                            throw new FormatException($"Invalid fusion recipe resource \"{resource}\". Write each one as Card Name[max-level id].");
                        }

                        spec.Recipe.Add(new RecipeResource { Id = int.Parse(res.Groups[1].Value), Number = 1 });
                    }

                    mode = "afterRecipe";
                    continue;
                }

                // skill line
                string normalised = NewCardSkills.Normalize(line, isCardName);
                NewCardSkill? skill = NewCardSkills.Parse(normalised);
                if (skill == null)
                {
                    throw new FormatException($"Can't read \"{line}\" - not a skill line in a shape the new-card builder knows (see the readme), and not Upgrades:/Set:/Fusion Recipe:/Picture:.");
                }

                skill.Original = line;
                foreach (string id in new[] { skill.Id, skill.S, skill.S2 }.OfType<string>())
                {
                    if (!NewCardSkills.IsKnownSkill(id, skillsSetIds))
                    {
                        throw new FormatException($"Unknown skill \"{id}\" in \"{line}\".");
                    }
                }

                spec.Skills.Add(skill);
            }

            if (mode != "afterPicture")
            {
                throw new FormatException("needs a Picture: line (new cards always need one - GetAvailablePictures.ps1 can suggest one).");
            }

            spec.Skills = NewCardSkills.ValidateAndSort(spec.Skills, out string? note, block.ReorderSkills ?? reorderSkills);
            if (note != null)
            {
                spec.Notes.Add(note);
            }

            foreach (NewCardSkill s in spec.Skills.Where(x => x.Y != null && !NewCardSkills.FactionValidSkills.Contains(x.Id)))
            {
                spec.Warnings.Add($"skill \"{s.Id}\" has a faction, but only these skills can carry one: {string.Join(", ", NewCardSkills.FactionValidSkills)}. The game ignores it.");
            }

            if (spec.Names.Count > 3)
            {
                throw new FormatException($"Name chains support 2 or 3 names, got {spec.Names.Count}.");
            }

            return spec;
        }
    }
}
