using System.Xml.Linq;
using System.Xml.XPath;
using TU_Card_XML_Gen;
using TU_Card_XML_Gen.NewCards;
using TU_Card_XML_Gen.Review;

namespace TUComparatorLibrary
{
    public class Updater
    {

        public const string Version = "1.8.0";
        public static List<XElement> skillData;
        public static List<XElement> factionData;
        public static List<XElement> fusionData;
        internal List<XDocument> oldCardXmls = new List<XDocument>();
        internal List<Card> oldCardObjects = new List<Card>();
        internal Dictionary<int, XElement> newXmls = new Dictionary<int, XElement>();
        internal Dictionary<int, int> fileForCardId = new Dictionary<int, int>();

        string oldXmlDirectory;

        private static List<string> keywordOrder = new List<string>()
        {
            "play",
            "attacked",
            "death"
        };

        private static List<string> skillKeywordOrder = new List<string>()
        {
            "play", //trigger
            "evade",
            "payback",
            "revenge",
            "tribute",
            "absorb",
            "barrier",
            "stasis",
            "armored",
            "wall",
            "allegiance",
            "avenge",
            "scavenge",
            "subdue",
            "attacked", //trigger
            "counter",
            "corrosive",
            "poison",
            "inhibit",
            "sabotage",
            "disease",
            "valor",
            "bravery",
            "summon",
            "evolve",
            "enhance",
            "overload",
            "rush",
            "mend",
            "fortify",
            "heal",
            "protect",
            "entrap",
            "rally",
            "enrage",
            "enfeeble",
            "strike",
            "jam",
            "weaken",
            "sunder",
            "siege",
            "besiege",
            "mimic",
            "legion",
            "coalition",
            "venom",
            "pierce",
            "hunt",
            "swipe",
            "drain",
            "leech",
            "berserk",
            "mark",
            "flurry",
            "refresh",
            "death" //trigger
        };

        /// <summary>This run's folder inside the results folder, e.g. results\2026-10-23-11-10-05.</summary>
        private string outputDirectory = string.Empty;

        private ConfigStore config;

        /// <summary>Title for review.html: the update file's name, or the operation.</summary>
        private string reviewTitle = "Update";

        public void Run(string oldXmlDirectory, string updateFilePath, ConfigStore config, string resultsFolder)
        {
            outputDirectory = Path.Combine(resultsFolder, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture));

            Console.WriteLine($"Running TU_Card_XML_Gen version {Version}");
            Console.WriteLine($"Results for this run: {Path.GetFullPath(outputDirectory)}");
            Console.WriteLine($"Operation: {config.Operation}");

            this.oldXmlDirectory = oldXmlDirectory;
            reviewTitle = config.Operation == ConfigStore.UpdaterOperation.Standard
                ? Path.GetFileNameWithoutExtension(updateFilePath)
                : config.Operation.ToString();

            Initialize(config);

            switch (config.Operation)
            {
                case ConfigStore.UpdaterOperation.Standard:
                    RunStandardUpdate(updateFilePath, config);
                    break;
                case ConfigStore.UpdaterOperation.MassNerfPoison:
                    RunMassNerfPoisonUpdate(config);
                    break;
                default:
                    throw new NotImplementedException("Config indicates an operation that does not exist.  Check your spelling.");
            }
        }

        private void RunMassNerfPoisonUpdate(ConfigStore config)
        {
            string skillUnderTest = "poison";

            List<string> cardsFailedToUpdate = new List<string>();

            // loads into oldCardObjects
            LoadOldCards(oldXmlDirectory);

            //modify the card objects to have the values we want.

            //Find all cards with poison
            // TODO: This query is a little wonky, verify it.
            List<Card> cardsWithPoison = oldCardObjects.Where(x => x.upgradeLevels.Last().Value.skillList.Where(y => y.id.Equals(skillUnderTest)).Count() > 0).ToList();

            //find the associated XMLs and update them.
            List<XElement> cardXMLsToUpdate = new List<XElement>();

            foreach (Card cardToUpdate in cardsWithPoison)
            {
                XElement cardXmlToUpdate = oldCardXmls[cardToUpdate.fileIndex - 1].XPathSelectElement($@"//unit[id='{cardToUpdate.id}']");

                cardXMLsToUpdate.Add(cardXmlToUpdate);
            }

            Console.WriteLine($@"Found {cardXMLsToUpdate.Count} cards with {skillUnderTest} to update.");

            // TODO: You really need to write a method that takes an XML and a Card and updates the former with values from the latter, but that sounds like more work than I want to do right now.
            // Still, noting it for when I do the real mass-nerfer

            for (int i = 0; i < cardXMLsToUpdate.Count; i++)
            {
                string name = "unknown";

                try
                {
                    bool cardWasChanged = false;

                    // get the card (can't foreach because we need to key on the index)
                    XElement cardXmlToUpdate = cardXMLsToUpdate[i];
                    name = cardXmlToUpdate.XPathSelectElement("name").Value;
                    Console.WriteLine($@"Updating {name}...");

                    // for both the base card, and each upgrade level, look for existing poison.
                    // For each that is found, multiply it by the scaling factor, round up, then set the value, as well as a flag that indicates it was changed.
                    XElement basePoison = cardXmlToUpdate.XPathSelectElement($@"skill[@id='{skillUnderTest}']");
                    if (basePoison != null)
                    {
                        decimal newValue = decimal.Parse(basePoison.Attribute("x").Value) * config.MassNerfFactor;

                        int roundedValue = (int)Math.Ceiling(newValue);

                        cardXmlToUpdate.XPathSelectElement($@"skill[@id='{skillUnderTest}']").SetAttributeValue("x", roundedValue);
                        cardWasChanged = true;
                    }

                    List<XElement> upgrades = cardXmlToUpdate.XPathSelectElements("upgrade").ToList();
                    cardXmlToUpdate.XPathSelectElements("upgrade").Remove();

                    foreach (XElement upgrade in upgrades)
                    {
                        XElement upgradePoison = upgrade.XPathSelectElement($@"skill[@id='{skillUnderTest}']");
                        if (upgradePoison != null)
                        {
                            decimal newValue = decimal.Parse(upgradePoison.Attribute("x").Value) * config.MassNerfFactor;

                            int roundedValue = (int)Math.Ceiling(newValue);

                            upgrade.XPathSelectElement($@"skill[@id='{skillUnderTest}']").SetAttributeValue("x", roundedValue);
                            cardWasChanged = true;
                        }

                        cardXmlToUpdate.Add(upgrade);
                    }

                    if (cardWasChanged)
                    {
                        newXmls.Add(int.Parse(cardXmlToUpdate.XPathSelectElement("id").Value), cardXmlToUpdate);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    //Console.WriteLine(ex.StackTrace);
                    cardsFailedToUpdate.Add(name);
                }
            }

            Console.WriteLine($@"{newXmls.Count} cards received updates.  Saving...");


            // Add the file for card ID to the end?
            foreach (Card cardToUpdate in cardsWithPoison)
            {
                // find the root card xml
                fileForCardId.Add(cardToUpdate.id, cardToUpdate.fileIndex);
            }

            SaveCards(cardsFailedToUpdate, newXmls, fileForCardId);
        }

        private void RunStandardUpdate(string updateFilePath, ConfigStore config)
        {
            List<string> cardsFailedToUpdate = new List<string>();
            List<string> warnings = new List<string>();

            LoadOldCards(oldXmlDirectory);

            // Load the update file, split into cards.  Comments, blank lines, "---" lines and "// SCALE:" are handled by the reader.
            List<UpdateBlock> updateCards;
            try
            {
                updateCards = UpdateFileReader.Read(updateFilePath, config.Tiered);
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Stopping - nothing was changed. {ex.Message}");
                return;
            }

            Console.WriteLine($"Update file has {updateCards.Count} cards.");

            CheckSummonSeparators(updateCards, warnings);

            // New cards (a Picture: line, or after "// NEW: TRUE") are built first, so buffs and summons in the same
            // file can refer to them.  Everything else is a buff of an existing card.
            List<UpdateBlock> newCardBlocks = new List<UpdateBlock>();
            List<UpdateBlock> buffBlocks = new List<UpdateBlock>();

            foreach (UpdateBlock block in updateCards)
            {
                bool looksNew = NewCardParser.LooksLikeNewCard(block.Lines);
                BlockKind kind = block.Kind == BlockKind.Auto ? (looksNew ? BlockKind.New : BlockKind.Buff) : block.Kind;

                if (kind == BlockKind.Buff && looksNew)
                {
                    Console.WriteLine($"Failed to parse {block.Name}: it has new-card lines (Picture:, Set:, Upgrades:, Fusion Recipe:) but \"// BUFF\" is in effect. Remove those lines, or put \"// NEW: TRUE\" (or \"// NEW: AUTO\") above it.");
                    cardsFailedToUpdate.Add(block.Name);
                    continue;
                }

                (kind == BlockKind.New ? newCardBlocks : buffBlocks).Add(block);
            }

            Console.WriteLine($"{newCardBlocks.Count} new card(s), {buffBlocks.Count} buff(s).");

            NewCardsOutcome? newCards = NewCardStep.Run(newCardBlocks, oldCardXmls, oldCardObjects, config, updateFilePath);
            if (newCards != null)
            {
                cardsFailedToUpdate.AddRange(newCards.Failed);
                warnings.AddRange(newCards.Warnings);
                Console.WriteLine();
                Console.WriteLine($"=== Buffs: {buffBlocks.Count} card(s) ===");
            }

            foreach (UpdateBlock updateCard in buffBlocks)
            {
                // the card name should be the first line
                string[] updateCardLines = updateCard.Lines.ToArray();

                try
                {
                    Console.WriteLine($"Updating {updateCardLines[0]}");

                    if (updateCardLines.Length < 3)
                    {
                        throw new FormatException($"Card at line {updateCard.StartLine} needs at least a name line, a rarity/faction line and a stats line.");
                    }

                    // New way - use the name given to look up the ID.  Use the ID to see if it has any children.  Recurse through that until we're out of cards to load.

                    // find the card objects.
                    List<Card> cardsToUpdate = new List<Card>();
                    bool cardFindError = false;

                    string cardName = updateCardLines[0];

                    Card cardToUpdateRequest = oldCardObjects.Where(x => x.name.Equals(cardName)).FirstOrDefault();

                    if (cardToUpdateRequest == null)
                    {
                        // Used to be skipped silently.  Fail it so it shows in the failed list.
                        Card? differentCase = oldCardObjects.FirstOrDefault(x => x.name.Equals(cardName, StringComparison.InvariantCultureIgnoreCase));
                        throw new FormatException($"Can't find a card named \"{cardName}\"."
                            + (differentCase != null ? $" Did you mean \"{differentCase.name}\"? Names must match exactly, including capitals." : " Check the spelling."));
                    }

                    try
                    {
                        cardsToUpdate = FindCardsToUpdateRecursive(cardToUpdateRequest);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Can't find a card with name {cardName}, or one of it's children.  Check your spelling.  Exception:");
                        Console.WriteLine(ex.ToString());
                        cardFindError = true;
                    }

                    if (cardFindError)
                    {
                        continue;
                    }

                    // A card can only be updated once per run.  Check before touching anything, otherwise the shared
                    // lower tiers would be half-changed when the second update fails.
                    Card? alreadyUpdated = cardsToUpdate.FirstOrDefault(x => newXmls.ContainsKey(x.id));
                    if (alreadyUpdated != null)
                    {
                        throw new FormatException($"{alreadyUpdated.name} was already updated earlier in this run (it's a fusion tier of an earlier card). Put these cards in separate runs.");
                    }

                    List<XElement> cardXMLsToUpdate = new List<XElement>();

                    foreach (Card cardToUpdate in cardsToUpdate)
                    {
                        // Work on a COPY.  The loaded file is only changed in SaveCards, and only for cards whose whole
                        // fusion chain succeeded.  Editing in place left failed cards half-changed (upgrade levels
                        // removed, new stats, old skills) and that damage was saved if anything else in the file changed.
                        XElement cardXmlToUpdate = new XElement(oldCardXmls[cardToUpdate.fileIndex - 1].XPathSelectElement($@"//unit[id='{cardToUpdate.id}']")!);

                        cardXMLsToUpdate.Add(cardXmlToUpdate);
                    }

                    // Finished copies for this chain.  Only added to newXmls once every tier has been built.
                    Dictionary<int, XElement> chainXmls = new Dictionary<int, XElement>();

                    //parse the ending attack, health, and delay from the update file
                    string[] updateStats = updateCardLines[2].Split('/').Select(x => x.Trim()).ToArray();

                    int attack = -1;
                    int health = -1;
                    int delay = -1;

                    // Commander / structure health on its own, e.g. "1200HP" or "1200 hp".  Delay is left unchanged.
                    System.Text.RegularExpressions.Match healthOnly = System.Text.RegularExpressions.Regex.Match(updateCardLines[2], @"^\s*(\d+)\s*hp\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    try
                    {
                        if (healthOnly.Success)
                        {
                            health = int.Parse(healthOnly.Groups[1].Value);
                        }
                        else if (updateStats.Length == 3)
                        {
                            attack = int.Parse(updateStats[0]);
                            health = int.Parse(updateStats[1]);
                            if (updateStats[2] != "-")
                            {
                                delay = int.Parse(updateStats[2]);
                            }
                        }
                        else if (updateStats.Length == 2)
                        {
                            health = int.Parse(updateStats[0]);
                            if (updateStats[1] != "-")
                            {
                                delay = int.Parse(updateStats[1]);
                            }
                        }
                        else
                        {
                            throw new FormatException($"Stats line \"{updateCardLines[2]}\" should look like attack/health/delay, health/delay or <health>HP.");
                        }
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine($"Failed to parse {updateCardLines[0]} - attack, health, or delay were not a number.");
                        Console.WriteLine(ex.Message);
                        cardsFailedToUpdate.Add(updateCardLines[0]);
                        continue;
                    }

                    // Read every skill line up front, so a typo fails the card before any XML is touched.
                    List<SkillLine> skillLines = new List<SkillLine>();
                    for (int j = 3; j < updateCardLines.Length; j++)
                    {
                        skillLines.Add(SkillLineTokenizer.Tokenize(updateCardLines[j], IsCardName));
                    }

                    // COMDEV's standard skill order (CardBuilder's table).  On by default; "// REORDER: FALSE" keeps the typed order.
                    List<SkillLine> standardOrder = TU_Card_XML_Gen.NewCards.NewCardSkills.InStandardOrder(skillLines, x => x.SkillId, x => x.Trigger);
                    if (!standardOrder.SequenceEqual(skillLines))
                    {
                        string Order(IEnumerable<SkillLine> list) => string.Join(", ", list.Select(x => x.SkillId + (x.Trigger != null ? " (on " + x.Trigger + ")" : "")));
                        if (updateCard.ReorderSkills ?? config.ReorderSkills)
                        {
                            Console.WriteLine($"  Skills reordered: {Order(skillLines)}  ->  {Order(standardOrder)}");
                            skillLines = standardOrder;
                        }
                        else
                        {
                            Console.WriteLine($"  Skills kept in the order typed (REORDER: FALSE): {Order(skillLines)}. COMDEV's standard order would be: {Order(standardOrder)}");
                        }
                    }

                    // Tiered scaling comes from a "// SCALE:" comment above this card, or from config.xml.  null = AutoStats.
                    TieredScaling? tiered = updateCard.Scaling ?? (config.ScalingMode == ConfigStore.ScalingModes.Tiered ? config.Tiered : null);

                    // Summoned cards: marked with "---" in the update file, or in the summoned-card set.
                    bool isSummoned = updateCard.SummonedByPrevious || cardXMLsToUpdate.Last().XPathSelectElement("set")?.Value.Trim() == "9500";

                    roundToNearest = tiered != null;

                    // Which tiered range each stage gets (index into tiered.FusionLevels); see TieredScaling.RangeIndexes.
                    // A stage with no range (the first stage of a four-stage commander when only three ranges are set)
                    // is left exactly as it is.
                    List<int> rangeIndex = Enumerable.Repeat(0, cardXMLsToUpdate.Count).ToList();
                    if (tiered != null && !isSummoned)
                    {
                        int.TryParse(cardXMLsToUpdate.Last().Element("fusion_level")?.Value.Trim(), out int topFusionLevel);
                        rangeIndex = tiered.RangeIndexes(cardXMLsToUpdate.Count, topFusionLevel);

                        for (int s = cardXMLsToUpdate.Count - 1; s >= 0; s--)
                        {
                            if (rangeIndex[s] < 0)
                            {
                                Console.WriteLine($"  {cardsToUpdate[s].name} [{cardsToUpdate[s].id}] left unchanged: {cardXMLsToUpdate.Count} stages but only {tiered.FusionLevels.Count} tiered ranges. Add a range for it (a fourth FusionLevel in config.xml, or a fourth range in // SCALE:) to scale it too.");
                                cardXMLsToUpdate.RemoveAt(s);
                                cardsToUpdate.RemoveAt(s);
                                rangeIndex.RemoveAt(s);
                            }
                        }
                    }

                    Console.WriteLine(tiered == null
                        ? "  Scaling: autostats"
                        : isSummoned
                            ? $"  Scaling: tiered, summoned card {tiered.Summoned}"
                            : "  Scaling: tiered, " + string.Join(", ", cardsToUpdate.Select((c, s) => $"{c.name} {tiered.FusionLevels[rangeIndex[s]]}")));

                    for (int i = 0; i < cardXMLsToUpdate.Count; i++)
                    {
                        // get the card (can't foreach because we need to key on the index)
                        XElement cardXmlToUpdate = cardXMLsToUpdate[i];

                        // build the scaling factor
                        int tiersBelowMax = (cardXMLsToUpdate.Count - 1) - i;
                        double tierScaling = ((tiersBelowMax * config.AutoStats.PercentBetweenTiers) + (tiersBelowMax * config.AutoStats.PercentAcrossTier)) * 0.01;

                        // summons need different tier scaling
                        double summonTierScaling = ((double)i / cardXMLsToUpdate.Count);

                        // get the upgrades
                        List<XElement> upgrades = cardXmlToUpdate.XPathSelectElements("upgrade").ToList();
                        cardXmlToUpdate.XPathSelectElements("upgrade").Remove();

                        // build the level scaling factor
                        int levelsBelowMax = (upgrades.Count);  // Don't need a -1, as there's one extra that's at the card level that cancels out the off-by-one
                        double levelScaling = (levelsBelowMax * config.AutoStats.PercentAcrossTier * 0.01) / (upgrades.Count <= 0 ? 1 : upgrades.Count);

                        double summonLevelScaling = ((double)1 / cardXMLsToUpdate.Count) * (1 - ((double)levelsBelowMax / (upgrades.Count <= 0 ? 1 : upgrades.Count)));

                        double scalingFactor = (1 - (tierScaling + levelScaling));
                        double summonScalingFactor = summonLevelScaling + summonTierScaling;

                        if (tiered != null)
                        {
                            // this stage's range (see TieredScaling.RangeIndexes); the chain is ordered from the base card up.
                            scalingFactor = tiered.Factor(rangeIndex[i], isSummoned, 1, upgrades.Count + 1);
                        }

                        // Level 1 is on the card
                        // remove the delay node, and add it if needed.
                        if (cardXmlToUpdate.XPathSelectElement("cost") != null && delay != -1)
                        {
                            cardXmlToUpdate.XPathSelectElement("cost")?.Remove();

                            cardXmlToUpdate.Add(new XElement("cost", delay));
                        }

                        // Base card healths and attacks
                        cardXmlToUpdate.XPathSelectElement("health")?.Remove();

                        cardXmlToUpdate.Add(new XElement("health", ScaleStat(health, scalingFactor)));

                        // remove the attack node, and replace it if needed.
                        if (cardXmlToUpdate.XPathSelectElement("attack") != null && attack != -1)
                        {
                            cardXmlToUpdate.XPathSelectElement("attack")?.Remove();

                            cardXmlToUpdate.Add(new XElement("attack", ScaleStat(attack, scalingFactor)));
                        }

                        List<XElement> outputSkills = new List<XElement>();

                        //For skills, it's not so simple.
                        foreach (SkillLine skillLine in skillLines)
                        {
                            // the last X lines are skills, already converted to skill IDs.
                            // Base them off of the last card.
                            outputSkills.Add(BuildSingleSkillXml(skillLine, scalingFactor, summonScalingFactor));
                        }

                        cardXmlToUpdate.XPathSelectElements("skill").Remove();

                        cardXmlToUpdate.Add(outputSkills.ToArray());

                        // Levels 2+ are in the upgrades.

                        for (int k = 0; k < upgrades.Count; k++)
                        {
                            XElement upgrade = upgrades[k];

                            // build the level scaling factor
                            levelsBelowMax = (upgrades.Count - (k + 1));  // Don't need a -1, as there's one extra that's at the card level that cancels out the off-by-one
                            levelScaling = (levelsBelowMax * config.AutoStats.PercentAcrossTier * 0.01) / upgrades.Count;

                            summonLevelScaling = ((double)1 / cardXMLsToUpdate.Count) * (1 - ((double)levelsBelowMax / upgrades.Count));

                            scalingFactor = (1 - (tierScaling + levelScaling));
                            summonScalingFactor = summonLevelScaling + summonTierScaling;

                            if (tiered != null)
                            {
                                // upgrade k is level k + 2 (the base card is level 1)
                                scalingFactor = tiered.Factor(rangeIndex[i], isSummoned, k + 2, upgrades.Count + 1);
                            }

                            // Base card healths and attacks
                            upgrade.XPathSelectElement("health")?.Remove();

                            upgrade.Add(new XElement("health", ScaleStat(health, scalingFactor)));

                            // remove the attack node, and replace it if needed.
                            if (attack != -1)
                            {
                                if (upgrade.XPathSelectElement("attack") != null)
                                {
                                    upgrade.XPathSelectElement("attack")?.Remove();
                                }

                                upgrade.Add(new XElement("attack", ScaleStat(attack, scalingFactor)));
                            }

                            List<XElement> upgradeOutputSkills = new List<XElement>();

                            //For skills, it's not so simple.
                            foreach (SkillLine skillLine in skillLines)
                            {
                                // the last X lines are skills, already converted to skill IDs.
                                // Base them off of the last card.
                                upgradeOutputSkills.Add(BuildSingleSkillXml(skillLine, scalingFactor, summonScalingFactor));
                            }

                            upgrade.XPathSelectElements("skill").Remove();

                            upgrade.Add(upgradeOutputSkills.ToArray());

                            cardXmlToUpdate.Add(upgrade);
                        }

                        chainXmls.Add(int.Parse(cardXmlToUpdate.XPathSelectElement("id").Value), cardXmlToUpdate);
                    }

                    // Every tier built - commit the chain.
                    foreach (KeyValuePair<int, XElement> built in chainXmls)
                    {
                        newXmls.Add(built.Key, built.Value);
                    }

                    // Add the file for card ID to the end?
                    foreach (Card cardToUpdate in cardsToUpdate)
                    {
                        // find the root card xml
                        fileForCardId.Add(cardToUpdate.id, cardToUpdate.fileIndex);
                    }
                }
                catch (FormatException ex)
                {
                    // Problems with what was typed in the update file - the message says what's wrong.
                    Console.WriteLine($"Failed to parse {updateCardLines[0]}: {ex.Message}");
                    cardsFailedToUpdate.Add(updateCardLines[0]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    //Console.WriteLine(ex.StackTrace);
                    cardsFailedToUpdate.Add(updateCardLines[0]);
                }

            }

            SaveCards(cardsFailedToUpdate, newXmls, fileForCardId, warnings, newCards);
        }

        /// <summary>True while the current card uses tiered scaling, which rounds to nearest instead of down.</summary>
        private bool roundToNearest = false;

        /// <summary>
        /// Scales a max value.  A max of 0 stays 0 at every level; anything else is rounded
        /// (down for autostats, to nearest for tiered) with a minimum of 1.
        /// </summary>
        private int ScaleStat(int maxValue, double scalingFactor)
        {
            if (maxValue == 0)
            {
                return 0;
            }

            double scaled = maxValue * scalingFactor;
            int rounded = roundToNearest
                ? Convert.ToInt32(Math.Round(scaled, MidpointRounding.AwayFromZero))
                : Convert.ToInt32(Math.Floor(scaled));

            return Math.Max(rounded, 1);
        }

        private bool IsCardName(string name)
        {
            return oldCardObjects.Any(x => x.name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// A "---" between two cards means the second is summoned by the first.  Warn if the first card's skills don't summon it.
        /// </summary>
        private void CheckSummonSeparators(List<UpdateBlock> updateCards, List<string> warnings)
        {
            for (int i = 0; i < updateCards.Count; i++)
            {
                UpdateBlock summoned = updateCards[i];

                if (!summoned.SummonedByPrevious)
                {
                    continue;
                }

                if (i == 0)
                {
                    warnings.Add($"\"---\" before the first card ({summoned.Name}, line {summoned.StartLine}) - there is no card before it to summon it.");
                    continue;
                }

                UpdateBlock summoner = updateCards[i - 1];
                bool summons = false;

                foreach (string line in summoner.Lines.Skip(3))
                {
                    try
                    {
                        SkillLine skillLine = SkillLineTokenizer.Tokenize(line, IsCardName);

                        if (skillLine.SkillId == "summon" && skillLine.Tokens.Length > 1
                            && skillLine.Tokens[1].Equals(summoned.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            summons = true;
                        }
                    }
                    catch (FormatException)
                    {
                        // bad skill lines are reported when that card is processed.
                    }
                }

                if (!summons)
                {
                    warnings.Add($"{summoner.Name} is followed by \"---\" but doesn't summon {summoned.Name} (line {summoned.StartLine}).");
                }
            }

            foreach (string warning in warnings)
            {
                Console.WriteLine($"WARNING: {warning}");
            }
        }

        private void Initialize(ConfigStore config)
        {
            this.config = config;

            //Create the folder for the run.
            Directory.CreateDirectory(outputDirectory);

            string skillDataString = File.ReadAllText($@"{oldXmlDirectory}//skills_set.xml");
            XDocument skillDataDoc = XDocument.Parse(skillDataString);
            Updater.skillData = skillDataDoc.XPathSelectElements("//root/skillType").ToList();
            Updater.factionData = skillDataDoc.XPathSelectElements("//root/unitType").ToList();

            // get the fusion data, to replace how we're loading cards to change.
            string fusionDataString = File.ReadAllText($@"{oldXmlDirectory}//fusion_recipes_cj2.xml");
            XDocument fusionDataDoc = XDocument.Parse(fusionDataString);
            Updater.fusionData = fusionDataDoc.XPathSelectElements("//root/fusion_recipe").ToList();
        }

        private void SaveCards(List<string> cardsFailedToUpdate, Dictionary<int, XElement> newXmls, Dictionary<int, int> fileForCardId, List<string>? warnings = null, NewCardsOutcome? newCards = null)
        {
            int cardsUpdated = 0;
            List<(XElement After, XElement? Before)> reviewCards = new List<(XElement, XElement?)>();

            // we have a list of all the cards to update, and the files they're in.
            // for each file, check for updates, and if there are some, do them and then write to file.
            for (int i = 0; i < oldCardXmls.Count; i++)
            {
                bool hasUpdate = false;

                foreach (int cardId in fileForCardId.Keys)
                {
                    if (fileForCardId[cardId] == i + 1)
                    {
                        hasUpdate = true;
                    }
                }

                // new cards added, or a picture replaced, in this file
                if (newCards != null && newCards.TouchedFileIndexes.Contains(i + 1))
                {
                    hasUpdate = true;
                }

                if (hasUpdate)
                {
                    // find the card IDs where this file is relevant.
                    foreach (int cardId in fileForCardId.Keys)
                    {
                        if (fileForCardId[cardId] == i + 1)
                        {
                            // get the old XML and delete it.
                            XElement newXml = newXmls[cardId];
                            string cardName = newXml.XPathSelectElement("name")?.Value.ToString();

                            // keep the card as it was, for the review page (buffs work on copies, so this is the original)
                            XElement? beforeXml = oldCardXmls[i].XPathSelectElement($@"//unit[id='{cardId}']");
                            reviewCards.Add((newXml, beforeXml == null || ReferenceEquals(beforeXml, newXml) ? null : new XElement(beforeXml)));

                            //add the new node
                            beforeXml?.ReplaceWith(newXml);

                            // Write to new file in the folder.
                            File.WriteAllText($"{outputDirectory}//{cardName}.xml", newXml.ToString());
                        }
                    }

                    // write file to disk, replacing existing.

                    string filename = $@"cards_section_{i + 1}.xml";
                    File.WriteAllText($@"{oldXmlDirectory}//{filename}", oldCardXmls[i].ToString());
                    cardsUpdated++;

                    Console.WriteLine($"file {i + 1} had changes.");
                }
            }

            if (newCards != null && newCards.Units.Count > 0)
            {
                SaveNewCards(newCards);
            }

            Console.WriteLine($"{fileForCardId.Count} cards updated"
                + (newCards != null ? $" and {newCards.Units.Count} new cards added" : string.Empty)
                + $" in {cardsUpdated} files.  The following cards failed to update:");

            foreach (string failedCard in cardsFailedToUpdate)
            {
                Console.WriteLine(failedCard);
            }

            if (warnings != null && warnings.Count > 0)
            {
                Console.WriteLine($"{warnings.Count} warning(s):");

                foreach (string warning in warnings)
                {
                    Console.WriteLine($"WARNING: {warning}");
                }
            }

            WriteReviewPage(reviewCards, newCards?.Units ?? new List<XElement>());
        }

        /// <summary>
        /// review.html in the output folder: the changed and new cards in the Card XML Viewer, with its rule checks
        /// (skill count, levels in order, values that dip, cost, lost skills ...).  Never fails the run.
        /// </summary>
        private void WriteReviewPage(List<(XElement After, XElement? Before)> changed, List<XElement> created)
        {
            if (changed.Count == 0 && created.Count == 0)
            {
                return;
            }

            try
            {
                string path = ReviewPage.Write(outputDirectory, reviewTitle, changed, created, Updater.fusionData);
                Console.WriteLine();
                Console.WriteLine($"Review page (check every card marked with a warning triangle): {path}");
                if (config.OpenReviewPage)
                {
                    ReviewPage.Open(path);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn't write the review page: {ex.Message}");
            }
        }

        /// <summary>
        /// New cards: recipes go into the fusion recipes file (its own formatting kept), and a copy of every new
        /// card, the recipes and a summary go into the output folder for review.
        /// </summary>
        private void SaveNewCards(NewCardsOutcome newCards)
        {
            if (newCards.Recipes.Count > 0)
            {
                string recipesPath = $@"{oldXmlDirectory}//fusion_recipes_cj2.xml";
                NewCardStep.AppendRecipesToFile(recipesPath, newCards.Recipes, newCards.Label);
                Console.WriteLine($"fusion_recipes_cj2.xml: {newCards.Recipes.Count} new recipe(s) added.");
            }

            foreach (XElement unit in newCards.Units)
            {
                string fileName = string.Concat(unit.Element("name")!.Value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                File.WriteAllText($"{outputDirectory}//{fileName}.xml", unit.ToString());
            }

            File.WriteAllText($"{outputDirectory}//new_cards_recipes.xml", new XElement("root", newCards.Recipes).ToString());
            File.WriteAllLines($"{outputDirectory}//new_cards_summary.txt", newCards.Summary.Concat(newCards.Warnings.Select(w => "WARNING: " + w)));
        }

        private void LoadOldCards(string oldXmlDirectory)
        {
            // load the XML files
            oldCardXmls = new List<XDocument>();

            Console.WriteLine("Reading old XMLs from file");

            // load old XMLs
            int counter = 1;
            bool lastLoadFailed = false;
            do
            {
                //try to load the file at the counter location.  If it exists, keep going.  If it doesn't, then stop
                string filename = $@"cards_section_{counter}.xml";

                try
                {
                    string file = File.ReadAllText($@"{oldXmlDirectory}//{filename}");
                    XDocument fileXml = XDocument.Parse(file);
                    oldCardXmls.Add(fileXml);
                    counter++;
                }
                catch (Exception e)
                {
                    lastLoadFailed = true;
                    Console.WriteLine(e.Message);
                }
            } while (!lastLoadFailed);

            Console.WriteLine($@"oldXmlDirectory found {oldCardXmls.Count} files");

            Console.WriteLine("Parsing old XMLs into card XMLs.");

            oldCardObjects = new List<Card>();

            for (int i = 0; i < oldCardXmls.Count(); i++)
            {
                XDocument oldCardXml = oldCardXmls[i];
                List<XElement> extractedCards = oldCardXml.XPathSelectElements("//root/unit").ToList();

                //immediately extract them into cards, while we have the index.
                foreach (XElement cardXml in extractedCards)
                {
                    oldCardObjects.Add(new Card(cardXml, i + 1));
                }
            }

            Console.WriteLine($@"Found {oldCardObjects.Count} cards in old XML.");
        }

        static string neocyteFusionCoreId = "42745";
        static string neocyteCoreId = "31184";
        static string altarId = "49970";

        static List<string> ascentionMaterials = new List<string>()
        {
            neocyteFusionCoreId,
            neocyteCoreId,
            altarId
        };

        private List<Card> FindCardsToUpdateRecursive(Card card)
        {
            // find the card objects.
            List<Card> cardsToUpdate = new List<Card>();

            if (card != null)
            {
                // basic XPath search to find the recipies that can create this card
                List<XElement> relevantRecipies = fusionData.Where(x => x.XPathSelectElement("card_id").Value.Equals(card.id.ToString())).ToList();

                if (relevantRecipies.Count > 0)
                {
                    foreach (XElement recipe in relevantRecipies)
                    {
                        List<XElement> resources = recipe.XPathSelectElements("resource").ToList();

                        // Do any of the ones have only a double of one other card?
                        if (resources.Count == 1 && resources.First().Attributes("number")?.FirstOrDefault()?.Value == "2")
                        {
                            string cardId = resources.First().Attributes("card_id").First().Value;
                            Card? cardToUpdate = oldCardObjects.Where(x => x.id.Equals(int.Parse(cardId)) || x.upgradeLevels.Values.Where(y => y.id.Equals(int.Parse(cardId))).ToList().Count >= 1).FirstOrDefault();

                            if (cardToUpdate != null)
                            {
                                cardsToUpdate = FindCardsToUpdateRecursive(cardToUpdate);
                            }
                            else
                            {
                                Console.WriteLine($"Can't find card with ID {cardId}.  Skipping.");
                            }
                        }

                        // Try to find commanders
                        if (resources.Count == 2)
                        {
                            // look at the recipie - we should have one resource that is the card of one level down and one that is the ascention material
                            int countWithAscentionMaterials = resources.Where(x => (x.Attributes("number")?.FirstOrDefault()?.Value.Equals("1") ?? false) && ascentionMaterials.Contains(x.Attributes("card_id")?.FirstOrDefault()?.Value)).Count();
                            int countWithoutAscentionMaterials = resources.Where(x => (x.Attributes("number")?.FirstOrDefault()?.Value.Equals("1") ?? false) && !(ascentionMaterials.Contains(x.Attributes("card_id")?.FirstOrDefault()?.Value))).Count();

                            if (countWithAscentionMaterials == 1 && countWithoutAscentionMaterials == 1)
                            {
                                // get the card ID that isn't the ascention material
                                string? cardId = resources.Where(x => (x.Attributes("number")?.FirstOrDefault()?.Value.Equals("1") ?? false) && !(ascentionMaterials.Contains(x.Attributes("card_id")?.FirstOrDefault()?.Value))).FirstOrDefault()?.Attributes("card_id")?.FirstOrDefault().Value;

                                if (!string.IsNullOrEmpty(cardId))
                                {
                                    Card? cardToUpdate = oldCardObjects.Where(x => x.id.Equals(int.Parse(cardId)) || x.upgradeLevels.Values.Where(y => y.id.Equals(int.Parse(cardId))).ToList().Count >= 1).FirstOrDefault();

                                    if (cardToUpdate != null)
                                    {
                                        cardsToUpdate = FindCardsToUpdateRecursive(cardToUpdate);

                                    }
                                    else
                                    {
                                        Console.WriteLine($"Can't find card with ID {cardId}.  Skipping.");

                                    }
                                }
                            }
                        }
                    }
                }

                Console.WriteLine($"Found child card {card.name}");
                cardsToUpdate.Add(card);
            }

            return cardsToUpdate;
        }

        private XElement BuildSingleSkillXml(SkillLine skillLine, double scalingFactor, double summonScalingFactor)
        {
            // get the card data to build
            OutputSkillData skillData;
            try
            {
                skillData = ProcessSingleSkill(skillLine, scalingFactor, summonScalingFactor);
            }
            catch (Exception ex) when (ex is FormatException || ex is IndexOutOfRangeException)
            {
                // e.g. "Enrage all 21 Xeno" (faction after the value) or "Strike" with no value
                throw new FormatException($"Can't read skill line \"{skillLine.Original}\" - check the order and the numbers against the readme. ({ex.Message})", ex);
            }

            XElement newSkillXml = new XElement("skill");

            newSkillXml.SetAttributeValue("id", skillData.skillId);

            if (skillData.x != null)
            {
                newSkillXml.SetAttributeValue("x", skillData.x);
            }

            if (skillData.y != null)
            {
                newSkillXml.SetAttributeValue("y", skillData.y);
            }

            if (skillData.all != null)
            {
                newSkillXml.SetAttributeValue("all", skillData.all);
            }

            if (skillData.c != null)
            {
                newSkillXml.SetAttributeValue("c", skillData.c);
            }

            if (skillData.s != null)
            {
                newSkillXml.SetAttributeValue("s", skillData.s);
            }

            if (skillData.s2 != null)
            {
                newSkillXml.SetAttributeValue("s2", skillData.s2);
            }

            if (skillData.n != null)
            {
                newSkillXml.SetAttributeValue("n", skillData.n);
            }

            if (skillData.card_id != null)
            {
                newSkillXml.SetAttributeValue("card_id", skillData.card_id);
            }

            if (skillData.trigger != null)
            {
                newSkillXml.SetAttributeValue("trigger", skillData.trigger);
            }

            return newSkillXml;
        }

        private OutputSkillData ProcessSingleSkill(SkillLine skillLine, double scalingFactor, double summonScalingFactor)
        {
            // Tokens are already normalised by SkillLineTokenizer: skill ids are lower case, factions are
            // full names, "all"/"every"/"to" are lower case, and the trigger has been taken off the end.
            string[] skillString = skillLine.Tokens;

            string skillId = skillLine.SkillId;

            int? newValue = null;
            int? newFaction = null;
            int? newAll = null;
            int? newCooldown = null;
            string? newSkill = null;
            string? newSkill2 = null;
            int? newNumber = null;
            Card summonCard = null;
            string? newTrigger = skillLine.Trigger;

            int? scaledValue = null;
            int? scaledNumber = null;

            int factionMod = 0;

            switch (skillId)
            {
                // for Wall and Rush, there are none.
                case "wall":
                case "rush":
                    //cool.
                    break;
                // for flurry and mimic, there are two, value and cooldown
                case "flurry":
                case "mimic":
                    switch (skillString.Length)
                    {
                        case 1:
                            //e.g. "Flurry".  Assume 1 value, no cooldown.
                            newValue = 1;

                            break;
                        case 2:
                            //e.g. "Mimic 50".  Assume value X, no cooldown.
                            newValue = int.Parse(skillString[1]);

                            break;
                        case 3:
                            // e.g. Flurry every 6.  Assume no value, X cooldown
                            newCooldown = int.Parse(skillString[2]);

                            break;
                        case 4:
                            // e.g. Flurry 2 every 4.  Assume X/Y
                            newValue = int.Parse(skillString[1]);
                            newCooldown = int.Parse(skillString[3]);

                            break;
                        default:
                            // No idea how we could get here.
                            break;
                    }
                    break;
                // two, value and all
                case "strike":
                case "weaken":
                case "siege":
                case "enfeeble":
                case "besiege":
                case "sunder":
                    if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newAll = 1;
                        newValue = int.Parse(skillString[2]);
                    }
                    else
                    {
                        newAll = 0;
                        newValue = int.Parse(skillString[1]);
                    }

                    break;
                // three params, value, faction, and all
                case "heal":
                case "rally":
                case "protect":
                    if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newAll = 1;
                    }
                    else
                    {
                        newAll = 0;
                    }

                    factionMod = 0;

                    if (skillString.Length > 1 + newAll.Value && SkillLineTokenizer.IsFaction(skillString[1 + newAll.Value]))
                    {
                        newFaction = SkillLineTokenizer.GetFactionId(skillString[1 + newAll.Value]);

                        factionMod = 1;
                    }

                    newValue = int.Parse(skillString[1 + newAll.Value + factionMod]);

                    break;
                // three params (all, cooldown, number)
                case "jam":
                    switch (skillString.Length)
                    {
                        case 2:
                            //e.g. Jam 4, or Jam all.  One value is either all or number, no CD.
                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            break;
                        case 3:
                            // e.g. Jam every 5.  One value is the cooldown

                            newCooldown = int.Parse(skillString[2]);

                            break;
                        case 4:
                            // e.g. Jam all every 4.  First is number or all, second is cooldown.

                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            newCooldown = int.Parse(skillString[3]);

                            break;
                        default:
                            // no idea what happened here.
                            break;
                    }

                    break;
                // three params (faction, all, number)
                case "overload":
                    // 1 is either a number or all.
                    if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newAll = 1;
                    }
                    else
                    {
                        newNumber = int.Parse(skillString[1]);
                    }

                    if (skillString.Length >= 3 && SkillLineTokenizer.IsFaction(skillString[2]))
                    {
                        newFaction = SkillLineTokenizer.GetFactionId(skillString[2]);
                    }

                    break;
                // four params (value, faction, all, number)
                case "enrage":
                case "entrap":
                    switch (skillString.Length)
                    {
                        case 2:
                            //e.g. both must have a value, so this is that.
                            newValue = int.Parse(skillString[1]);

                            break;
                        case 3:
                            // This could either be all, a number, or a faction.  Third value is always a value, though.
                            newValue = int.Parse(skillString[2]);

                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else if (SkillLineTokenizer.IsFaction(skillString[1]))
                            {
                                newFaction = SkillLineTokenizer.GetFactionId(skillString[1]);
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            break;
                        case 4:
                            // Has either all or number, a faction, and a value.
                            newValue = int.Parse(skillString[3]);

                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            newFaction = SkillLineTokenizer.GetFactionId(skillString[2]);

                            break;
                        default:

                            break;
                    }

                    break;
                // four params (value, all, skill, number)
                case "enhance":
                    switch (skillString.Length)
                    {
                        case 3:
                            // Enhance skill value
                            newSkill = skillString[1];

                            newValue = int.Parse(skillString[2]);

                            break;
                        case 4:
                            // Enhance all/number skill value
                            if (skillString[1].Equals("all", StringComparison.InvariantCultureIgnoreCase))
                            {
                                newAll = 1;
                            }
                            else
                            {
                                newNumber = int.Parse(skillString[1]);
                            }

                            newSkill = skillString[2];

                            newValue = int.Parse(skillString[3]);

                            break;
                        default:

                            break;
                    }

                    break;
                // four (all, skill 1, skill 2, number)
                case "evolve":
                    {
                        // Evolve [all/number] skill1 [to] skill2.  "to" is optional; skills are already converted to IDs.
                        List<string> evolveParts = skillString.Skip(1).Where(x => x != "to").ToList();

                        if (evolveParts.Count == 3)
                        {
                            if (evolveParts[0] == "all")
                            {
                                newAll = 1;
                            }
                            else
                            {
                                newNumber = int.Parse(evolveParts[0]);
                            }

                            evolveParts.RemoveAt(0);
                        }

                        if (evolveParts.Count != 2 || !SkillLineTokenizer.IsSkillId(evolveParts[0]) || !SkillLineTokenizer.IsSkillId(evolveParts[1]))
                        {
                            throw new FormatException("Evolve should look like \"Evolve [all/number] <skill> to <skill>\".");
                        }

                        newSkill = evolveParts[0];
                        newSkill2 = evolveParts[1];
                    }
                    break;
                // summon sucks
                case "summon":
                    //Hoo boy.  Here we go.
                    // The first segment will be the Summon keyword.  The last, if it's a trigger, is by necessity, the trigger, and is removed earlier.
                    // So, to get the card name of the summon, we can concatinate everything else?
                    string[] nameSections = skillString.Skip(1).Take(skillString.Length - 1).ToArray();
                    string summonCardName = string.Join(" ", nameSections);

                    summonCard = oldCardObjects.Where(x => x.name.Equals(summonCardName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                    if (summonCard == null)
                    {
                        Console.WriteLine($"Can't find summon card \"{summonCardName}\"");
                        throw new Exception($"Can't find summon card \"{summonCardName}\"");
                    }
                    // With the summon card retrieved, we'll worry about scaling it below.

                    break;
                // everything else just has one param, value
                default:
                    if (skillString.Length < 2)
                    {
                        throw new FormatException($"{skillId} needs a value, e.g. \"{skillId} 10\".");
                    }

                    newValue = int.Parse(skillString[1]);

                    break;
            }

            // scale the values.
            if (newValue != null)
            {
                scaledValue = ScaleStat(newValue.Value, scalingFactor);
            }

            if (newNumber != null)
            {
                scaledNumber = ScaleStat(newNumber.Value, scalingFactor);
            }

            int? cardId = null;

            // scale the summon card ID
            if (summonCard != null)
            {
                int levelCount = summonCard.upgradeLevels.Count; // include the base card

                int scaledLevelCount = Convert.ToInt32(Math.Floor((levelCount - 1) * summonScalingFactor)) + 1;

                if (scaledLevelCount == 1)
                {
                    // base card ID
                    cardId = summonCard.id;
                }
                else
                {
                    // upgrade level
                    cardId = summonCard.upgradeLevels[scaledLevelCount].id; // watch this - I think I indexed by level but am not sure.
                }
            }

            return new OutputSkillData(skillId, scaledValue, newFaction, newAll, newCooldown, newSkill, newSkill2, scaledNumber, newTrigger, cardId);


        }
    }
}
