TU_Card_XML_Gen  (v1.6.0)
=========================

Takes a plain-text "update file" describing new card stats and skills, and writes
those changes into the Tyrant Unleashed card XMLs for you, including every fusion
tier and every upgrade level, with stats scaled down automatically.

Requires the .NET 8 runtime.


-----------------------------------------------------------------------
1. SETUP
-----------------------------------------------------------------------

Edit config.xml (it sits next to the exe):

  <Operation>         standard        - normal run: apply the update file
                      massnerfpoison  - multiply Poison on every card by a factor

  <PathToCardXMLs>    Folder holding the game XMLs. It MUST contain:
                        cards_section_1.xml, cards_section_2.xml, ...  (numbered
                          from 1 with no gaps; loading stops at the first missing number)
                        skills_set.xml
                        fusion_recipes_cj2.xml

  <PathToUpdateFile>  Full path to your update file (standard operation only).

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

  <MassNerfPoisonConfig><Factor>   default .4 (massnerfpoison only)

Then run the exe.


NOTE - THE CARD XMLs ARE CHANGED IN PLACE

The tool overwrites the cards_section_N.xml files in PathToCardXMLs. Only files
containing a changed card are rewritten. If anything goes wrong, reset the XMLs by
downloading them again in TUO.

It also creates a folder next to the exe called output-<date-time> containing one
small XML per updated card (<card name>.xml). Those are handy for review and for
pasting into the update you send to Kongregate.

At the end the console lists any cards that failed to update, and any warnings.
Read it.


-----------------------------------------------------------------------
2. UPDATE FILE FORMAT
-----------------------------------------------------------------------

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

  - Fusion levels are counted up from the base card: fusion level 0 is the card
    with no fusion recipe, 1 is made from it, and so on. Fusion levels above the
    last range use the last range.
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
