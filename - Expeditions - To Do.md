# To Do: Expeditions Rules System

## Phase 1: Create Print Materials
- [ ] Standard Premade Character Printout Sheets (7 backgrounds + loadouts, double-sided like SNExpeditions)
- [ ] Blank Character / Custom Background and Loadout Sheets

## Phase 2: Rules Completion
- [ ] Finish Trade Skill Recipes — use alchemist as example
- Rules Review
  - [ ] Trade Skill Recipes & Perks
  - [ ] Skill Perks
  - [ ] Ability Text
- [ ] Ensure rules index and navigation is up to date and working for web
- [ ] Guide Rules booklet (GM-facing — currently rough outline)

## Phase 3: SNExpeditions
- [ ] Create SNExpeditions: Snailfolk Starter Adventure and format for print


## Phase 4: Tooling (Side Project)
- [ ] Update UI to WPF with XML layout definition — fork to general tool?

---

# To Do: Uncharted Waters Campaign

## Phase 1: Launch Prep
- [ ] Major review of all Faction Hooks and Rumors.
- [ ] Finalize the **Core Truths of the World**
  - [ ] The ocean is bounded, not endless. It's sealed by an outer ring of impassable fog ("The Great Fog") and a central magical whirlpool ("The Deep One," placeholder name). Nothing is known to survive beyond either.
  - [ ] Generations ago, **The Cataclysm** cut the world off. The event is remembered and referenced by that name, but its cause is lost even to people living now
  - [ ] Ships are only now venturing back out. This is a world being remapped after going dark, not a virgin unexplored frontier. Old charts may be wrong, outdated, or describe places that no longer exist
  - [ ] Whether "The Deep One" is a mindless phenomenon or a sealed, possibly-aware entity is left deliberately open for now. Decide only once a hook needs it
  - [ ] Ancient advanced civilization left behind magical technology. Open question: are they connected to the cataclysm (cause, victim, or unrelated)?
  - [ ] The Waystone network is being established
  - [ ] The world is highly magical: everyone possesses magic
  - [ ] Ships use wind magic to sail in any direction
  - [ ] Magic-infused technology exists up to an early gunpowder era
  - [ ] Airships exist but are uncommon
  - [ ] People are born with a random magical affinity
    - [ ] Fire magic is feared
    - [ ] Air magic is prized and respected
    - [ ] Water magic is common
- [ ] Review & Develop Depth Crawl table. Feeds Setting Zine Ch. 26 (Hex Crawls & Depth Crawls)
- [ ] Split out each ship and faction into separate pages. Looks done already (each has its own file); confirm and check off
- [ ] Refine Starting Location Options (embed images)
- [ ] Write more runnable expeditions. Only "What Lies Beneath" is a complete adventure; the other 20 seeds in Expedition Concepts and the generator are one-liners, not session-ready

## Phase 2: Core World
- [ ] 36x48 Hex Map
  - [ ] Define Key Locations
  - [ ] Define Danger Tiers (1–5 stars like quests)
  - [ ] Count Random Locations needed for pulling
  - [ ] Outer Ring: "The Great Fog"
  - [ ] Inner Ring: "The Deep One"
  - [ ] Current Guide Sea Hex Map (121-hex) is unrelated placeholder content (generic D&D plane names like Ysgard, Limbo, and Nine Hells, plus stock fantasy dressing) with zero overlap with any written location, faction, or ship. Needs to be rebuilt against the real lore, not just filled in. Once rebuilt, add real location markers to Setting Zine Ch. 3 (World Map), which currently only has the Great Fog / current / Deep One boundary text
- [ ] Refine Factions (embed logos & scene images). Feeds Setting Zine Ch. 5-18
- [ ] Refine Ships (embed images). Feeds Setting Zine Ch. 11-18
  - [ ] Add ship stat/combat rules. The Ship Events table implies raids, mutinies, fires, mechanical breakdowns, but there's no mechanical resolution for ship-level conflict
- [ ] Table of known locations, factions, and ships. Feeds Setting Zine's Front Cover Inside "At a Glance" table
  - [ ] Ships/ has no index page (unlike Locations, Factions, NPCs). The 7 faction ships aren't linked from anywhere. Decide if that's intentional (revealed as discovered in play, matching how the Locations index currently reads "no locations discovered yet") or an oversight to fix now

## Phase 3: Visual Assets
- [ ] Make map-ready top-down perspective images of each location and ship. Feeds Setting Zine map spread (Ch. 3) and Ch. 5-18

## Phase 4: Encounters & Creatures
- [ ] Create Base Set of Premade Monsters. Feeds Setting Zine Ch. 19
  - [ ] Species half sheets (like background half sheets)
  - [ ] Generate species ideas
  - [ ] Only 1 adversary exists right now (Steam Crab). Need a starter roster before launch

## Phase 5: Setting Zine (Print)
Same production pipeline as the Player/Guide Rules booklets: 5.5"x8.5" zine, `MarkdownPdfConverter`, `\newpage`/`vspace` directives. Single unified book (GM-facing detail; no player/guide split), matching the "Expeditions: Uncharted Waters (Setting Zine)" SKU already listed in Product Design.md. System agnostic: no stat blocks or numeric content, built to pair with Expeditions but usable with any fantasy RPG. Shell files for all pages exist in `UnchartedWaters/Setting Guide/`.

Chapter numbers below match the actual files in `UnchartedWaters/Setting Guide/` right now. Don't cascade-renumber the whole folder every time this list changes; append new chapters where they fit and do one real reordering pass later, once the chapter list has mostly stabilized.

**First draft status:** Front matter through Ch. 10 are drafted (About, The World, World Map boundary text, The Emerald Dawn & Crew, and all six location spreads with their Rumors/Hooks tables). **Next up: Ch. 11-18, the eight faction deep-dive pages.**

- [ ] Front Cover Outside: logo/title art (image only, no markdown content)
- [ ] Front Cover Inside: "At a Glance" quick-reference table (locations, factions, leaders, ships)
- [ ] Ch. 11-18: Faction Deep Dives, one spread per faction with a key-NPC page, a ship page, a Rumors table, and a Hooks table (8 spreads, now 2-3 pages each instead of 2; revisit the divisible-by-4 page count once these are drafted)
- [ ] Ch. 19: Adversaries, monster roster (depends on Phase 4 Encounters & Creatures work)
- [ ] Ch. 20: GM Toolkit, cross-cutting hooks and expedition seeds not tied to one location, pulled from Quest Givers and the expedition generator/concepts
- [ ] Ch. 21: Running an Open Table (appended for now; belongs earlier in reading order, but slotting it in without a full renumber). General open-table advice, plus how it works specifically in this world: session structure, what persists between sessions, how factions react to the party's absence
- [ ] Ch. 22: Random Encounters, a section on building good random encounters plus the actual tables
- [ ] Ch. 23: Magical Artifacts, a list of setting-specific magic items and roughly where each can be found
- [ ] Ch. 26: Hex Crawls & Depth Crawls (appended for now; belongs earlier in reading order, near Ch. 3 and Ch. 21). How to run the sea as a hex crawl using the World Map, plus the Depth Crawl procedure itself, blocked on the Depth Crawl table review above
- [ ] Back Cover Inside: Glossary (Waystone, Peace-Bond, Catalogue, Depth Crawl, etc.) plus Acknowledgements
- [ ] Back Cover Outside: quick-reference card (Faction Tensions table plus map legend)

## Phase 6: Future Campaign Arcs
- [ ] Session 0 Gazetteer / Player's Handbook: a lighter, player-facing companion to hand out at session 0. Teases the world without the GM-only depth of the Setting Zine
### Uncharted Waters: Beyond the Fog
### Uncharted Waters: Into the Deeps
### Uncharted Waters: The Skies Above
