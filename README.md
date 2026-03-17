# Ecogen

Ecogen is a Unity prototype that combines procedural ecosystem map generation with a card-combat run loop.


## Engine and Scene

- Unity version: 6000.3.4f1
- Ecosystem source data: [Assets/Scripts/ecosystem.json](Assets/Scripts/ecosystem.json)

## Web Build on GitHub Pages (Simple Manual Setup)

This path does not require GitHub Actions or Unity license secrets.

### One-time setup

1. In Unity, switch the active build target to WebGL.
2. Build the project to a `docs` folder at the repository root.
3. In Unity WebGL publishing settings, either:
	- set Compression Format to Disabled, or
	- keep compression on and enable Decompression Fallback.
4. Create an empty file named `.nojekyll` inside the `docs` folder.
5. In GitHub, open Settings -> Pages and set:
	- Source: Deploy from a branch
	- Branch: `main`
	- Folder: `/docs`

### Publishing updates

After each new WebGL build to `docs`, commit and push:

- `git add docs`
- `git commit -m "Publish WebGL build"`
- `git push origin main`

Your site will publish at: `https://mgembree.github.io/ecogen/`

## Credits and External Reference

Lootbox system reference was taken from:

- TristanChenUCSC Lootbox Generator: https://github.com/TristanChenUCSC/Lootbox-Generator
[Assets/Scripts/LootboxTool.cs](Assets/Scripts/LootboxTool.cs)

## How Procedural Generation Works

The generator pipeline is implemented in [Assets/Scripts/dungeonGenerator.cs](Assets/Scripts/dungeonGenerator.cs) and runs from `GenerateEcosystem()`.

### 1) Load ecosystem data

- Reads creature + biome data from [Assets/Scripts/ecosystem.json](Assets/Scripts/ecosystem.json).
- Builds lookups for creature sprites and creature danger ratings.

### 2) Determine room count

- Chooses a random room count between `roomMin` and `roomMax`.
- Creates nodes, then labels key nodes as `START` and `GOAL` after graph analysis.

### 3) Place room nodes with fractal noise

- Samples candidate points over an area.
- Scores points with multi-octave Perlin/fractal noise (`FractalNoise`).
- Applies jitter and minimum spacing (`noiseMinSpacing`) to avoid clumping.

### 4) Build and repair the graph

- Connects each node to `nearestNeighborsPerNode` nearest nodes.
- Forces global connectivity (`EnsureConnected`).
- Finds a farthest node from start and turns it into GOAL.
- Forces GOAL to be terminal (single path feel) while keeping rest connected (`EnsureGoalIsTerminal`).

### 5) Assign biome/zone and room occupants

- Uses selected biome (`biomeToBeGenerated`) or resolves a valid fallback biome.
- Chooses room `zoneType` from biome `zone_types`.
- Filters creatures by biome and samples them with CR weighting (`creatureCrWeightExponent`).
- Rolls creature populations per room from each creature's `population_range`, scaled by `creaturePopulationMultiplier`.

### 6) Expose map to gameplay systems

- Caches generated rooms and adjacency for combat/navigation systems.
- Optional visualization spawns room GameObjects/edges and clickable room previews.

## Combat/Run Flow

The run loop is in [Assets/Scripts/CardCombatGameState.cs](Assets/Scripts/CardCombatGameState.cs).

1. Start in map selection.
2. Choose a connected room.
3. If it is a lootbox room, open lootbox and continue.
4. Otherwise, enter turn-based card combat.
5. Defeat encounter, then choose/skip enhancement reward.
6. Continue pathing until GOAL boss is defeated.

### Controls

- Mouse UI clicks: map, combat, lootbox, rewards.
- Card hover: tooltip descriptions.
- Artifact inventory toggle: `I` (from [Assets/Scripts/LootboxTool.cs](Assets/Scripts/LootboxTool.cs)).
- Camera (if [Assets/Scripts/CameraPanZoom2D.cs](Assets/Scripts/CameraPanZoom2D.cs) is active):
- Pan with `WASD` or arrow keys
- Zoom with mouse wheel
- Drag pan with middle mouse button (right mouse optional if enabled)

## Lootbox/Artifact Notes

- Artifacts are generated from an internal catalog in [Assets/Scripts/LootboxTool.cs](Assets/Scripts/LootboxTool.cs).
- Artifact effects are converted into combat bonuses during encounters by [Assets/Scripts/CardCombatGameState.cs](Assets/Scripts/CardCombatGameState.cs).
- ItemTool/ItemNames JSON dependency has been removed from the current implementation.

## Asset credits

Free assets from CraftPix:

- Deer: https://craftpix.net/freebies/free-top-down-hunt-animals-pixel-sprite-pack/?num=1&count=1&sq=deer&pos=0
- Scorpion / Salamander / Spider: https://craftpix.net/freebies/free-chaos-monsters-32x32-icon-pack/?num=1&count=5&sq=scorpion&pos=4
- Vine Creeper / Moss / Mole: https://craftpix.net/freebies/free-low-level-monsters-pixel-icons-32x32/
- Cave Background: https://craftpix.net/freebies/free-crystal-cave-pixel-art-backgrounds/?num=1&count=57&sq=cave&pos=4

Other Free Assets:

- Slash Damage SFX - https://freesound.org/people/wesleyextreme_gamer/sounds/574821/ 
- Metal Slide Block SFX - https://freesound.org/people/LordForklift/sounds/448402/
- Page Turn Draw Card SFX - https://freesound.org/people/nsstudios/sounds/321114/
- Victory Sting - https://freesound.org/people/Rolly-SFX/sounds/626259/ 
- ICU - Battle_music(Tothedeath) - https://soundcloud.com/icu132 (Given permission by artist)

