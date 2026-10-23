TU_Card_XML_Gen  (v1.8.0)
=========================

Takes a plain-text "update file" and writes it into the Tyrant Unleashed card XMLs
for you. It does two things, and one update file can mix them:

  - BUFFS: new stats and skills for EXISTING cards, written into every fusion tier
    and every upgrade level, with stats scaled down automatically (sections 2-8).
  - NEW CARDS: complete new cards - every level, ids, fusion recipes and pictures -
    built the same way as CardBuilder.ps1 (section 9).

Requires the .NET 8 runtime.


-----------------------------------------------------------------------
1. SETUP
-----------------------------------------------------------------------

FOLDERS
The "main folder" is the folder config.xml is in (the tool looks for config.xml
in the folder you run it from, then next to the exe). Under it:

    config.xml
    data\      your update files (default update file: data\updatefile.txt)
    results\   one folder per run, named by date and time (e.g.
               results\2026-10-23-11-10-05) with review.html and a copy of every
               changed or new card
    logs\      everything the tool prints, one file per day (2026-10-23.log);
               later runs on the same day are added to the end

The folders are made for you. Their names and places are set in config.xml
under <Folders>; a relative path there (or in PathToCardXMLs and
PathToUpdateFile) starts at the main folder.

CLEANUP
At the start of every run, anything directly inside data, results and logs that
hasn't changed for <CleanupMonths> months (default 3) is DELETED, and the
console and log list what was removed. 0 = never delete. The update file in
use and today's log are always kept, and a folder that is, holds or contains
the game data (PathToCardXMLs or any cards_section file), the main folder, or a
folder above it, is never cleaned.

Edit config.xml:

  <Operation>         standard        - normal run: apply the update file
                      massnerfpoison  - multiply Poison on every card by a factor

  <PathToCardXMLs>    Folder holding the game XMLs. It MUST contain:
                        cards_section_1.xml, cards_section_2.xml, ...  (numbered
                          from 1 with no gaps; loading stops at the first missing number)
                        skills_set.xml
                        fusion_recipes_cj2.xml

  <PathToUpdateFile>  Your update file (standard operation only). Default
                      data\updatefile.txt; a full path also works.

  <Folders>           <Data> data, <Results> results, <Logs> logs,
                      <CleanupMonths> 3 (see FOLDERS and CLEANUP above).

  <AutoStats>         Percentages for autostats scaling (see section 6).
    <PercentAcrossTier>    default 15
    <PercentBetweenTiers>  default 5

  <Scaling>           Which scaling to use (see section 6).
    <Mode>                 autostats (default) or tiered
    <Tiered>               Ranges for tiered scaling:
      <FusionLevel0>         default 50..60
      <FusionLevel1>         default 60..80
      <FusionLevel2>         default 80..100
      <Summoned>             default 50..100

  <NewCards>          Settings for new cards (see section 9):
    <SetId>                default 2500
    <SectionFile>          default cards_section_21.xml
    <Label>                default: update file name + month

  <OpenReviewPage>    true (default) = open review.html in the browser after each
                      run; false = only write it (see section 10)

  <MassNerfPoisonConfig><Factor>   default .4 (massnerfpoison only)

Then run the exe.


NOTE - THE CARD XMLs ARE CHANGED IN PLACE

The tool overwrites the cards_section_N.xml files in PathToCardXMLs. Only files
containing a changed card are rewritten. New cards also add to
fusion_recipes_cj2.xml. If anything goes wrong, reset the XMLs by downloading them
again in TUO.

Each run's results folder (results\<date-time>) holds one small XML per updated
or new card (<card name>.xml), review.html, and for new cards
new_cards_recipes.xml and new_cards_summary.txt. Those are handy for review and
for pasting into the update you send to Kongregate.

At the end the console lists any cards that failed to update, and any warnings.
Read it. Then check the REVIEW PAGE that opens in your browser (section 10).


-----------------------------------------------------------------------
2. UPDATE FILE FORMAT
-----------------------------------------------------------------------

This section, and sections 3-8, are about BUFFS (changing existing cards). New
cards use CardBuilder's format - see section 9.

Each card is a block of lines:

    Line 1:  Card name
    Line 2:  Rarity / faction  (required as a placeholder, but IGNORED by the tool)
    Line 3:  Stats
    Line 4+: Skills, one per line

BETWEEN CARDS put one or more blank lines and/or "---" lines. Any mix of blank,
"---" and comment lines between two cards counts as a single gap.

"---" (three or more dashes) means "the next card is summoned by the card before
it". The tool checks this and prints a WARNING if the card before the --- doesn't
have a Summon for the card after it. The cards are still updated either way.

COMMENTS start with // and run to the end of the line. They can be on their own
line or after anything else. A comment line inside a card doesn't split the card.

SCALE COMMENTS change the scaling for every card that follows (section 6):
    // SCALE: 50..60, 60..80, 80..100
    // SCALE: 50..100
    // SCALE: DEFAULT

Either Windows (CRLF) or Unix (LF) line endings are fine.

Spaces and tabs at the start or end of any line are ignored, so a line holding only
spaces counts as blank. Spaces around the / in stats are fine too (10 / 20 / 1).

(Some card names in the examples below are made up.)


EXAMPLE - assault cards, plus a summon pair:

    // Righteous rework, Oct 2026
    Pure Benediction            // top tier; lower tiers are found automatically
    Legendary Righteous
    60/200/4
    Evade 20
    Rally all Righteous 100 attacked
    Mortar all 60

    Utopia Prayer
    Mythic Righteous
    20/200/3
    On Play: Jam all every 4
    Bravery 50
    Refresh 60

    Hive Mother
    Epic Xeno
    50/100/2
    Summon Hive Drone play
    ---
    Hive Drone                  // summoned by Hive Mother
    Epic Xeno
    10/20/1
    Strike 5


EXAMPLE - commander (health only, no attack, no delay):

    Malika Ascended
    Legendary Imperial
    1200HP
    Heal all Imperial 50
    Enrage 2 Xeno 30 death


EXAMPLE - structure (health/delay, no attack):

    Bulwark Tower
    Epic Raider
    800/2
    Armor 30
    Counter 20


CARD NAME (line 1)
  - Must match the in-game name EXACTLY, including capitalisation and spacing.
  - Use the name of the HIGHEST fusion tier you want changed (e.g. the Pure/Holy
    version). The tool walks the fusion recipes backwards and updates every lower
    tier for you. Commanders are followed through their ascension recipes too
    (Neocyte Fusion Core, Neocyte Core, Altar).
  - The old "Name1|Name2|Name3" pipe syntax is gone. Don't use it.

STATS (line 3) - these are the MAX stats (see section 6 for how they scale):
    attack/health/delay      assaults               e.g. 60/200/4
    health/delay             structures, commanders e.g. 800/2   or  1200/-
    <health>HP               commanders             e.g. 1200HP  (same as 1200/-)
  - Use  -  for delay to leave the existing delay alone:  60/200/-
  - Delay is only written to cards that already have one; attack only to cards
    that already have one. So commanders won't suddenly grow an attack stat.

SKILLS (line 4+)
  - The skills you list REPLACE all existing skills on the card, on every tier and
    level. List every skill the card should have, not just the changed ones.
  - Skills are written to the XML in the order you list them.
  - Values are the MAX values; lower levels and tiers scale down (section 6).
  - Nothing is case-sensitive: skill names, "all", "every", factions and triggers
    can be written in any case ("RALLY ALL rt 100 On Attacked" is fine).
  - A word the tool doesn't recognise (e.g. a misspelt skill) makes that card FAIL
    with a message saying which word, rather than writing bad XML.


-----------------------------------------------------------------------
3. SKILL SYNTAX
-----------------------------------------------------------------------

<value> = the skill amount        <n>       = number of targets
<cd>    = cooldown ("every" turns) <faction> = Imperial, Raider, Bloodthirsty,
[ ]     = optional                              Xeno, Righteous, Progenitor

No parameters:
    Wall
    Rush

One value:
    Armor, Counter, Pierce, Poison, Leech, Evade, Berserk, Corrosive, Inhibit,
    Valor, Legion, Payback, Avenge, Refresh, Venom, Mend, Swipe, Rupture,
    Allegiance, Drain, Stasis, Revenge, Coalition, Sabotage, Barrier, Subdue,
    Tribute, Bravery, Absorb, Disease, Mark, Fortify, Hunt, Scavenge
        Armor 40

Value + cooldown:
    Flurry [<value>] [every <cd>]
    Mimic  [<value>] [every <cd>]
        Flurry 2 every 4      Flurry every 6      Mimic 50

Value, optionally hitting all:
    Strike / Weaken / Siege / Mortar / Enfeeble / Sunder   [all] <value>
        Strike all 50         Weaken 30

Value, optional all, optional faction:
    Heal / Rally / Protect   [all] [<faction>] <value>
        Rally all Righteous 100      Heal 20      Protect all 40

Jam:
    Jam <n or all>              Jam 2      Jam all
    Jam every <cd>              Jam every 4
    Jam <n or all> every <cd>   Jam all every 5
  NOTE: "Jam 4" means 4 TARGETS, not a cooldown. Use "Jam every 4" for cooldown.

Overload:
    Overload <n or all> [<faction>]
        Overload all      Overload 2 Xeno

Enrage / Entrap:
    Enrage <value>
    Enrage <all, n, or faction> <value>
    Enrage <all or n> <faction> <value>
        Enrage 30    Enrage all 30    Enrage Xeno 30    Entrap 2 Raider 40

Enhance:
    Enhance [<n or all>] <skill> <value>
        Enhance Armor 20      Enhance all Counter 15

Evolve:
    Evolve [<n or all>] <skill> [to] <skill>
        Evolve Armor to Counter     Evolve all Armor to Counter
        Evolve 2 Armor Counter      Evolve Armor into Counter

Summon:
    Summon <card name>
        Summon Hive Drone play
  The summoned card is looked up by name (any case). Lower tiers/levels of the
  summoner get a lower upgrade level of the summoned card; the top gets the max.


-----------------------------------------------------------------------
4. TRIGGERS
-----------------------------------------------------------------------

A trigger can go at the END or the START of a skill line, in any case:

    Jam all every 4 play             On Play: Jam all every 4
    Rally all RT 100 OnAttacked      on attacked Rally all RT 100
    Enrage 2 Xeno 30 On Death        Death: Enrage 2 Xeno 30

Recognised words: play, attacked, death - optionally preceded by "On" or "On "
and optionally followed by a colon.

Summon lines are smart about card names that end in a trigger word:
"Summon Angel of Death" summons Angel of Death (no trigger) if that card exists.


-----------------------------------------------------------------------
5. SHORTCUTS
-----------------------------------------------------------------------

Faction abbreviations (any case):
    XN -> Xeno     Imp -> Imperial     RD -> Raider     BT -> Bloodthirsty
    RT -> Righteous     Prog -> Progenitor
Skill abbreviation:
    Zerk -> Berserk

Use the skill names you see in game (Armor, Mortar, ...). They're converted to
their XML ids (armored, besiege, ...) using skills_set.xml. Typing the XML id
works too.


-----------------------------------------------------------------------
6. HOW SCALING WORKS
-----------------------------------------------------------------------

The values you type are the MAX values. Lower levels and lower fusion tiers get a
percentage of them. There are two ways to work out that percentage, chosen by
<Scaling><Mode> in config.xml and by SCALE comments in the update file.

For both:
  - Applies to attack, health, skill values, and target counts (n).
  - A max of 0 stays 0 at every level (e.g. 0 attack, Rally 0).
  - Anything else is rounded (see below), with a minimum of 1.
  - NOT scaled: delay, cooldowns, factions, all, skill-to-skill choices.
  - The console prints which scaling each card used.
  - If you need exact values at specific levels, run the tool, then hand-edit
    those levels in the output.


AUTOSTATS (Mode autostats - the original behaviour)

Counted down from the top tier. With the defaults (15 across, 5 between), for a
card with three fusion tiers:

    Tier 3, max level  100%        Tier 3, level 1   85%
    Tier 2, max level   80%        Tier 2, level 1   65%
    Tier 1, max level   60%        Tier 1, level 1   45%

Results round DOWN.


TIERED (Mode tiered, or any SCALE comment other than DEFAULT)

Each fusion level has its own range. Level 1 of the tier gets the first number,
the tier's max level gets the second, and the levels between are spread evenly.
Defaults:

    Fusion level 0 (base card)      50% .. 60% of max
    Fusion level 1                  60% .. 80% of max
    Fusion level 2                  80% .. 100% of max
    Summoned card (every tier)      50% .. 100% of max

  - Each card uses the range for ITS OWN fusion level, read from the top card's
    <fusion_level> (none = 0) and counting down the chain. So a lone Fusion 2
    card such as Revenant gets 80..100, and a normal 3-card chain gets 0, 1, 2.
  - Four-stage commanders (Epic, Legendary, Fusion 1, Fusion 2, e.g. Malika /
    Malika / Vengeful Malika / Insurgent Malika): the last three stages are
    scaled and the FIRST STAGE IS LEFT UNCHANGED (the console says so). To scale
    it too, give four ranges (FusionLevel0..FusionLevel3 in config.xml, or four
    in // SCALE:): the first is then for that extra lowest stage, and the last
    three stay fusion levels 0, 1, 2.
  - A "summoned card" is one that comes right after a "---" in the update file,
    or one in set 9500.
  - Results round to the NEAREST whole number (halves round up).

Example, Pure Benediction with 200 health and 3 levels per tier:
    Benediction        100, 110, 120
    Holy Benediction   120, 140, 160
    Pure Benediction   160, 180, 200


SCALE COMMENTS

A comment starting with SCALE: changes the scaling for every card that starts
after it, until the next SCALE comment. If it's on a card's name line, that card
is included. Not case-sensitive; % signs are optional.

    // SCALE: 50..60, 60..80, 80..100
        Tiered. Fusion levels 0, 1, 2 use these ranges. Summoned cards keep the
        Summoned range from config.xml.
    // SCALE: 50..100
        Tiered. One range for everything, summoned cards included.
    // SCALE: 50..60, 60..80, 80..100, SUMMON 50..100
        Tiered, and also sets the summoned-card range.
    // SCALE: DEFAULT
        Back to whatever config.xml says.

A SCALE comment the tool can't read stops the whole run before anything is
changed, and says which line is wrong.


-----------------------------------------------------------------------
7. MASS NERF POISON
-----------------------------------------------------------------------

Set <Operation>massnerfpoison</Operation>. Every card whose max level has Poison
gets Poison multiplied by <Factor> on every level, rounded UP. The update file is
not used. The XMLs are changed in place, as above.


-----------------------------------------------------------------------
8. GOTCHAS
-----------------------------------------------------------------------

  - Line 2 must exist even though it's ignored; skipping it makes the tool read
    your first skill as stats.
  - Card names (line 1) are matched exactly and the first match wins. A typo
    fails the card with "Can't find a card named ...", and suggests the right
    name if only the capitals are wrong.
  - A card that fails is left exactly as it was in the XMLs, along with every
    fusion tier the tool found for it. Fix the update file and run that card again.
  - A card can only be updated once per run. If a later card shares fusion tiers
    with an earlier one (e.g. Pure Benediction, then Holy Benediction), the later
    one fails without changing anything. Do them in separate runs.
  - "Jam 4" is four targets, not "every 4".


-----------------------------------------------------------------------
9. NEW CARDS
-----------------------------------------------------------------------

New cards are built the way CardBuilder.ps1 (player mode) builds them: same input
format, same level curves, ids, recipes and pictures. The output was checked
against CardBuilder on the same input. PowerShell is not needed.

NEW OR BUFF?
A block is a NEW card if it has a Picture:, Replace Picture:, Upgrades:, Set: or
Fusion Recipe: line (every new card needs Picture:). Anything else is a buff.
To say it outright for the cards that follow:

    // NEW: TRUE        the cards below are new cards
    // BUFF: TRUE       the cards below are buffs
    // NEW: AUTO        back to deciding from each card's lines

("// NEW" and "// BUFF" on their own work too.) New cards are built before the
buffs, so a buff or a summon in the same file can refer to a new card.

THE CARD BLOCK

    Nexor's Eye                      name, or "A / B / C" for a fusing chain
    Xeno Vindicator                  Faction Rarity [Structure] [Fusion0|1|2]
    17/30/2                          attack/health/cost   (Structure: health/cost)
    Evade 5                          skills (any order - see below)
    Avenge 17
    Leech 22
    Upgrades: 5                      optional, default 5 (= 6 levels). Chain: 3 / 5 / 0
    Set: 9500                        optional, this card's set
    Fusion Recipe: Card A[12345], Card B[23456]     optional
    Picture: tick_lv3:137            REQUIRED: file[:bundle]
    Replace Picture: Old Card[4321]: other_lv1:99   optional, repeatable

  - Factions: Imperial, Raider, Bloodthirsty, Xeno, Righteous, Progenitor.
    Rarities: Epic, Legendary, Vindicator, Mythic.
  - "Commander" or "Dominion" on line 2 builds a Structure, with a warning that it
    needs editing by hand (COMDEV doesn't create player Commanders/Dominions).
  - Order: skills and Upgrades:/Set: first, then Fusion Recipe:, then Picture:,
    then Replace Picture: lines. Nothing else may follow Picture:.
  - "// SET: n" between cards sets the set for the new cards after it. Otherwise
    the set is config.xml <NewCards><SetId> (2500), or the card's own Set: line.
  - Comments, blank lines, "---", spaces and capitals work as for buffs.

NUMBERS ARE THE CEILING
The attack, health and skill numbers are for the card's highest tier at max level.
Every level gets a share of them, rounded UP (0 stays 0); cost is never scaled:

    Fusion 2, or a card with a Fusion Recipe:   80% .. 100%
    Fusion 1                                     60% .. 80%
    Fusion 0                                     50% .. 60%
    Standalone / summoned (no recipe, no FusionN) 50% .. 100%

A chain "A / B / C" is tiers 0, 1, 2 (two names = tiers 1, 2); each later card gets
a recipe of 2x the previous card's max-level id. A one-level card (Upgrades: 0)
gets the curve's first value (e.g. 50% for a standalone card).
// SCALE: comments only affect buffs.

SKILLS ON NEW CARDS
Mostly the same as buffs (section 3), with CardBuilder's rules:
  - Most skills: <Skill> [n | all] [faction] <value>   e.g. Sunder 3 15 (3 targets),
    Protect all Righteous 25. Only mend/fortify, heal, protect, entrap, rally,
    enrage, evolve, enhance, overload and rush can carry a faction.
  - Jam [n | all] [every c]: c counts DOWN from c+4 at level 1 to c at max;
    n counts UP from 1 to n.  Flurry [x] [every c]: x stays the same at every level.
  - Enhance [n | all] <skill> <value>.  Overload [n | all] [faction].
  - Evolve [n] <skill> [to] <skill>  (no "all" on new cards).
  - Summon <card name>: a card in this file or an existing card. Level N of a single
    card summons level N; over a chain the summoned card's levels are spread evenly.
  - Wall, Flying: no value.
  - Triggers can be written as for buffs (section 4).
  - Skills are put in COMDEV's standard order for you (the console says so).
  - A card is NOT built if it has a skill twice (Siege + Mortar count), or both of
    Payback/Revenge, Valor/Bravery, Mend/Fortify or Weaken/Sunder.

IDS, RECIPES AND PICTURES
  - Ids come from the "Lowest ID available" header in cards_section_21.xml
    (<NewCards><SectionFile>). Each card takes Upgrades+1 ids in a row, from the
    Assault or Structure pool, and the header is moved on.
  - The new cards are added at the end of that section file, between
    <!-- label --> and <!-- End of label --> comments.
  - Recipes are added to fusion_recipes_cj2.xml the same way; the rest of that file
    is left exactly as it was.
  - Fusion Recipe: resources must be MAX-level ids. You get a warning when an id is
    a base id, isn't found, or mixes the Base Fusion and Chain families.
  - Replace Picture: changes the base picture (and bundle, with a DOUBLE CHECK
    warning) of that existing card. To find free pictures and the Replace Picture:
    lines to go with them, run CardBuilder's GetAvailablePictures.ps1.

SAFETY CHECKS (because the XMLs are changed in place)
  - A new card whose name already exists is NOT built. Running the same update file
    twice therefore can't add the cards twice. (To start over, re-download the XMLs.)
  - If any id it would use is already taken, NO new cards are built: the header is
    out of date.
  - A Summon of a card that isn't in the file or the game data fails the card,
    instead of writing card_id="PENDING".
  - A card that fails takes no ids, and the cards after it still build.

NOT SUPPORTED: mission cards (CardBuilder -UseMissionSet), real Commanders and
Dominions, and finding pictures (use GetAvailablePictures.ps1).


-----------------------------------------------------------------------
10. REVIEW PAGE (review.html)
-----------------------------------------------------------------------

After every run the tool writes review.html into that run's results folder
and opens it in your browser (turn that off with <OpenReviewPage>false). It is
the Card XML Viewer showing only the cards this run CHANGED or CREATED, each
compared with the same card from before the run.

Any card that breaks a rule gets a warning triangle next to its name, and the
cell at fault (a level's HP, a skill, the cost, an id) gets one too. Hover over a
triangle to see why. Amber = worth a look, red = almost certainly wrong.

What it checks:
  - Skill count isn't 3 at max level (a heads-up only - some cards have more or
    fewer).
  - Level ids aren't one per level, in a row; levels out of order; a missing id.
  - Attack, HP, or a skill's x or n DROPS between levels, or one level sticks out
    (a big jump then a dip = probably a typo). A cooldown (c) going UP.
  - Cost outside 0-4.
  - Compared with before the run: a skill the card had is gone (this is what
    catches a skill left out of the update file by mistake), or upgrade levels
    were lost.
  - A level that lists its skills but leaves one out.
  - The same skill twice; Payback+Revenge, Valor+Bravery, Mend+Fortify or
    Weaken+Sunder together; a faction on a skill that can't carry one; all and n
    together; a Summon with no real card id.
  - Skills not in COMDEV's standard order.
  - The id range says Structure but the card has attack, or Commander but it
    has attack or cost; no picture; no health at level 1.
  - Fusion recipes: a resource that isn't the max-level id, and a 2x-fused card
    whose level 1 is weaker than the max level of the card it's fused from.

Filters at the top: "Only cards with warnings" and "Only changed cards".

THE VIEWER ON ITS OWN
CardXmlViewer.html (next to the exe) is the same page for any XML files:
  1. Open XML File(s): e.g. a cards_section file after an update.
  2. Compare with before...: the same file from before the update (e.g. a fresh
     TUO download you kept). Changed cards get a CHANGED badge and lost skills or
     levels are flagged; tick "Only changed cards" to see just those.
This works for files changed by any tool, including older versions of this one.


-----------------------------------------------------------------------
CHANGES IN 1.8.0
-----------------------------------------------------------------------

  - Review page: review.html opens after every run with the changed and new cards,
    their before versions and rule checks (section 10). <OpenReviewPage> turns the
    automatic opening off.
  - The Card XML Viewer is included as CardXmlViewer.html, with the same checks
    and a "Compare with before..." button.
  - Tiered scaling: each card uses the range for its own fusion level (from the
    top card's <fusion_level>), so lone Fusion 2 cards (e.g. Revenant) get
    80..100 instead of 50..60, and Base Fusion pairs (e.g. Stag Beetle ->
    Forsaken Beetle) get 60..80 / 80..100. Four-stage commanders leave their
    first stage unchanged unless four ranges are given.
  - Standard folders under the main folder: data (update files), results (one
    folder per run) and logs (one log per day, runs appended). Set in <Folders>.
  - Cleanup: data, results and logs older than <CleanupMonths> (3) months are
    deleted at the start of each run.
  - config.xml is found in the current folder or next to the exe; relative paths
    in it start at its folder. "Press any key" no longer crashes when the input is
    redirected.


-----------------------------------------------------------------------
CHANGES IN 1.7.0
-----------------------------------------------------------------------

  - New cards: complete player cards built in the same run as buffs (section 9),
    ported from CardBuilder.ps1 and checked against it.
  - "// NEW: TRUE", "// BUFF: TRUE", "// NEW: AUTO" and "// SET: n" comments.
  - Differences from CardBuilder: "Upgrades: 0" on a single card makes one level
    (CardBuilder makes 6), and names with & or < are written as valid XML.


-----------------------------------------------------------------------
CHANGES IN 1.6.0
-----------------------------------------------------------------------

  - Comments: // to end of line.
  - Cards can be separated by any number of blank, comment and --- lines.
  - --- marks a summoned card; warns if the card before doesn't summon it.
  - Skills, factions, keywords and triggers are no longer case-sensitive.
  - Triggers can be written at the start or end of a skill line.
  - Evolve works (it couldn't be parsed before). "to"/"into" are optional.
  - Commander health can be written as 1200HP.
  - Unknown skills or words fail the card with a clear message instead of writing
    bad XML or silently dropping a trigger.
  - Fixed "Prog" turning "Progenitor" into "Progenitorenitor".
  - Summon card names containing a skill name (e.g. "... Mortar ...") now work.
  - Windows and Unix line endings both work in the update file.
  - New tiered scaling option (config.xml <Scaling>), with per-fusion-level and
    summoned-card ranges.
  - "// SCALE:" comments change the scaling for the cards that follow.
  - A max value of 0 stays 0 at every level (it used to become 1).
  - A card that shares fusion tiers with an earlier card in the same run now
    fails cleanly; before, the shared tiers were left half-changed.
  - A card that fails is no longer left half-changed. Before, a failed card could
    lose all its upgrade levels and get new stats with old skills, and that was
    saved if any other card in the same cards_section file was updated.
  - A card name that isn't found now fails and is listed, instead of being
    silently skipped (this happened with trailing spaces on the name line).
  - Skill lines that can't be read now say which line.
  - The summary counts cards as well as files ("15 cards updated in 10 files");
    before, "10 updated" was the number of files.
