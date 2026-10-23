using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using TUComparatorLibrary;

namespace TU_Card_XML_Gen.NewCards
{
    /// <summary>What the new-card step changed, for SaveCards to write.</summary>
    internal class NewCardsOutcome
    {
        public HashSet<int> TouchedFileIndexes = new HashSet<int>();     // 1-based cards_section numbers
        public List<XElement> Units = new List<XElement>();
        public List<XElement> Recipes = new List<XElement>();
        public string Label = string.Empty;
        public List<string> Failed = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<string> Summary = new List<string>();
    }

    /// <summary>
    /// Builds the new cards in an update file and adds them to the loaded game files (in memory):
    /// units appended to the new-cards section file between label comments, its "Lowest ID available"
    /// header advanced, pictures replaced on existing cards, and recipes queued for the fusion recipes file.
    /// Nothing is changed unless the new cards' ids are confirmed free.
    /// </summary>
    internal static class NewCardStep
    {
        public static NewCardsOutcome? Run(List<UpdateBlock> newBlocks, List<XDocument> sections, List<Card> cardObjects,
            ConfigStore config, string updateFilePath)
        {
            if (newBlocks.Count == 0)
            {
                return null;
            }

            NewCardsOutcome outcome = new NewCardsOutcome();
            Console.WriteLine();
            Console.WriteLine($"=== New cards: {newBlocks.Count} card block(s) ===");

            // ---- the section file that receives new cards, and its next-id header ----
            Match sectionName = Regex.Match(config.NewCardsSectionFile, @"^cards_section_(\d+)\.xml$", RegexOptions.IgnoreCase);
            int sectionNumber = sectionName.Success ? int.Parse(sectionName.Groups[1].Value) : -1;

            XComment? header = null;
            int nextAssault = 0, nextStructure = 0;

            if (sectionNumber < 1 || sectionNumber > sections.Count)
            {
                FailAll(newBlocks, outcome, $"{config.NewCardsSectionFile} isn't one of the loaded cards_section files, so there's nowhere to put new cards.");
                return outcome;
            }

            XDocument section = sections[sectionNumber - 1];
            header = section.DescendantNodes().OfType<XComment>().FirstOrDefault(c =>
                Regex.IsMatch(c.Value, @"Assault\s+\d+") && Regex.IsMatch(c.Value, @"Structures\s+\d+"));

            if (header == null)
            {
                FailAll(newBlocks, outcome, $"{config.NewCardsSectionFile} has no readable \"Lowest ID available\" header (Assault ... / Structures ... numbers), so the next free ids are unknown.");
                return outcome;
            }

            nextAssault = int.Parse(Regex.Match(header.Value, @"Assault\s+(\d+)").Groups[1].Value);
            nextStructure = int.Parse(Regex.Match(header.Value, @"Structures\s+(\d+)").Groups[1].Value);
            Console.WriteLine($"Next free ids from {config.NewCardsSectionFile}: Assault {nextAssault}, Structure {nextStructure}");

            // ---- parse ----
            ExistingCardIndex index = ExistingCardIndex.Build(sections, Updater.fusionData);
            HashSet<string> batchNames = new HashSet<string>(newBlocks.SelectMany(b => b.Name.Split('/').Select(n => n.Trim())), StringComparer.OrdinalIgnoreCase);
            Func<string, bool> isCardName = name => batchNames.Contains(name) || index.ByName.ContainsKey(name);
            HashSet<string> skillsSetIds = new HashSet<string>(
                Updater.skillData.Select(x => x.XPathSelectElement("id")?.Value.Trim() ?? string.Empty).Where(x => x.Length > 0),
                StringComparer.OrdinalIgnoreCase);

            List<NewCardSpec> specs = new List<NewCardSpec>();
            foreach (UpdateBlock block in newBlocks)
            {
                try
                {
                    specs.Add(NewCardParser.Parse(block, block.SetId ?? config.NewCardsSetId, isCardName, skillsSetIds, config.ReorderSkills));
                }
                catch (FormatException ex)
                {
                    Fail(outcome, block.Name, $"line {block.StartLine}: {ex.Message}");
                }
            }

            // ---- build ----
            NewCardResult built = new NewCardBuilder(index, nextAssault, nextStructure).Build(specs);

            foreach ((string name, string reason) in built.Failed)
            {
                Fail(outcome, name, reason);
            }

            foreach (string note in built.Notes)
            {
                Console.WriteLine($"  {note}");
            }

            // ---- every id must be free; a stale header would otherwise overwrite real cards ----
            List<int> newIds = built.Units.SelectMany(u => new[] { (int)u.Element("id")! }
                .Concat(u.Elements("upgrade").Select(up => (int)up.Element("card_id")!))).ToList();
            int clash = newIds.FirstOrDefault(id => index.ById.ContainsKey(id));
            if (clash != 0)
            {
                string reason = $"id {clash} is already used by \"{index.ById[clash].Name}\" - the \"Lowest ID available\" header in {config.NewCardsSectionFile} is out of date. Fix the header (or re-download the XMLs in TUO) and run again.";
                foreach ((NewCardSpec spec, List<int> _) in built.Built)
                {
                    Fail(outcome, spec.DisplayName, reason);
                }

                return outcome;
            }

            if (built.Units.Count == 0)
            {
                return outcome;
            }

            outcome.Warnings.AddRange(built.Warnings);

            // ---- add the units to the section file, between label comments ----
            string label = (config.NewCardsLabel ?? DefaultLabel(updateFilePath)).Replace("--", "-");
            outcome.Label = label;
            XElement root = section.Root!;
            root.Add(new XComment($" {label} "));
            foreach (XElement unit in built.Units)
            {
                root.Add(new XElement(unit));
                cardObjects.Add(new Card(unit, sectionNumber));
            }

            root.Add(new XComment($" End of {label} "));

            string oldHeader = header.Value;
            // Only the numbers change; the header's own whitespace and text stay as they are.
            header.Value = new Regex(@"(Assault\s+)\d+").Replace(header.Value, m => m.Groups[1].Value + built.NextIds["Assault"], 1);
            header.Value = new Regex(@"(Structures\s+)\d+").Replace(header.Value, m => m.Groups[1].Value + built.NextIds["Structure"], 1);
            outcome.TouchedFileIndexes.Add(sectionNumber);

            outcome.Units = built.Units;
            outcome.Recipes = built.Recipes;
            Updater.fusionData.AddRange(built.Recipes.Select(r => new XElement(r)));

            // ---- pictures taken from existing cards ----
            List<string> pictureLines = new List<string>();
            foreach (PictureReplacement pr in built.PictureReplacements)
            {
                ReplacePicture(pr, sections, outcome, pictureLines);
            }

            // ---- summary ----
            outcome.Summary.Add($"Label: {label}");
            outcome.Summary.Add($"New cards ({built.Units.Count}):");
            foreach (XElement unit in built.Units)
            {
                int levels = 1 + unit.Elements("upgrade").Count();
                int first = (int)unit.Element("id")!;
                outcome.Summary.Add($"  {unit.Element("name")!.Value} [{first}{(levels > 1 ? $"-{first + levels - 1}" : string.Empty)}], set {unit.Element("set")!.Value}");
            }

            outcome.Summary.Add($"Fusion recipes ({built.Recipes.Count}):");
            foreach (XElement recipe in built.Recipes)
            {
                outcome.Summary.Add($"  {recipe.Element("card_id")!.Value} <- " + string.Join(" + ", recipe.Elements("resource").Select(r => $"{r.Attribute("number")!.Value}x {r.Attribute("card_id")!.Value}")));
            }

            outcome.Summary.Add($"Pictures replaced on existing cards ({pictureLines.Count}):");
            outcome.Summary.AddRange(pictureLines.Select(x => "  " + x));
            outcome.Summary.Add($"Next free ids: Assault {built.NextIds["Assault"]}, Structure {built.NextIds["Structure"]} (header was:{Regex.Replace(oldHeader, @"\s+", " ")})");

            foreach (string line in outcome.Summary)
            {
                Console.WriteLine(line);
            }

            return outcome;
        }

        private static void ReplacePicture(PictureReplacement pr, List<XDocument> sections, NewCardsOutcome outcome, List<string> pictureLines)
        {
            for (int i = 0; i < sections.Count; i++)
            {
                XElement? unit = sections[i].Root?.Elements("unit").FirstOrDefault(u => u.Element("id")?.Value.Trim() == pr.CardId.ToString(CultureInfo.InvariantCulture));
                if (unit == null)
                {
                    continue;
                }

                string actualName = unit.Element("name")?.Value ?? string.Empty;
                if (!actualName.Equals(pr.CardName, StringComparison.OrdinalIgnoreCase))
                {
                    outcome.Warnings.Add($"Replace Picture: id {pr.CardId} is \"{actualName}\", not \"{pr.CardName}\" - replaced the picture on id {pr.CardId} anyway. DOUBLE CHECK the id.");
                }

                XElement? picture = unit.Element("picture");
                if (picture == null)
                {
                    outcome.Warnings.Add($"Replace Picture: \"{actualName}\" (id {pr.CardId}) has no <picture> to replace - skipped.");
                    return;
                }

                string oldPicture = picture.Value;
                picture.Value = pr.Picture;

                if (pr.Bundle != null)
                {
                    XElement? bundle = unit.Element("asset_bundle");
                    if (bundle == null)
                    {
                        picture.AddAfterSelf(new XElement("asset_bundle", pr.Bundle));
                        outcome.Warnings.Add($"\"{actualName}\" (id {pr.CardId}) had no <asset_bundle> but the replacement gives one ({pr.Bundle}) - added it. DOUBLE CHECK this is correct.");
                    }
                    else if (bundle.Value != pr.Bundle)
                    {
                        outcome.Warnings.Add($"\"{actualName}\" (id {pr.CardId}) asset_bundle changed from {bundle.Value} to {pr.Bundle}. DOUBLE CHECK this is intentional - not every card's fusion tree shares one bundle.");
                        bundle.Value = pr.Bundle;
                    }
                }

                outcome.TouchedFileIndexes.Add(i + 1);
                pictureLines.Add($"{actualName}[{pr.CardId}] in cards_section_{i + 1}.xml: {oldPicture} -> {pr.Picture}");
                return;
            }

            outcome.Warnings.Add($"Replace Picture target \"{pr.CardName}\" (id {pr.CardId}) wasn't found in any cards_section file - change its picture by hand.");
        }

        private static string DefaultLabel(string updateFilePath)
        {
            string name = Path.GetFileNameWithoutExtension(updateFilePath).Replace('_', ' ').Trim();
            return $"{name} - {DateTime.Now.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}";
        }

        private static void Fail(NewCardsOutcome outcome, string name, string reason)
        {
            Console.WriteLine($"Failed to build new card {name}: {reason}");
            outcome.Failed.Add($"{name} (new card)");
        }

        private static void FailAll(List<UpdateBlock> blocks, NewCardsOutcome outcome, string reason)
        {
            foreach (UpdateBlock block in blocks)
            {
                Fail(outcome, block.Name, reason);
            }
        }

        /// <summary>
        /// Adds the recipes to the fusion recipes file as text, keeping the file's own formatting, encoding and
        /// line endings (the generator otherwise never rewrites this file).  Same layout as CardBuilder's publish.
        /// </summary>
        public static void AppendRecipesToFile(string recipesPath, List<XElement> recipes, string label)
        {
            byte[] bytes = File.ReadAllBytes(recipesPath);
            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text = File.ReadAllText(recipesPath);
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";

            StringBuilder block = new StringBuilder();
            block.Append(nl).Append($"\t<!-- {label} -->").Append(nl);
            foreach (XElement recipe in recipes)
            {
                block.Append("\t<fusion_recipe>").Append(nl);
                block.Append($"\t\t<card_id>{recipe.Element("card_id")!.Value}</card_id>").Append(nl);
                foreach (XElement res in recipe.Elements("resource"))
                {
                    block.Append($"\t\t<resource card_id=\"{res.Attribute("card_id")!.Value}\" number=\"{res.Attribute("number")!.Value}\"/>").Append(nl);
                }

                block.Append("\t</fusion_recipe>").Append(nl);
            }

            block.Append($"\t<!-- End of {label} -->").Append(nl);

            Match end = Regex.Match(text, @"</root>\s*$");
            if (!end.Success)
            {
                throw new InvalidOperationException($"{Path.GetFileName(recipesPath)} doesn't end with </root>, so the new recipes couldn't be added.");
            }

            string updated = text.Substring(0, end.Index) + block + "</root>" + nl;
            File.WriteAllText(recipesPath, updated, new UTF8Encoding(hasBom));
        }
    }
}
