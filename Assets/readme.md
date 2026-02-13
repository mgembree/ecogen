# Ecosystem Generator (Unity)

This project generates a connected room layout, picks a biome, and fills rooms with biome-valid creatures from a JSON data file.

## Quick start

1. Open the scene in Unity.
2. Select the GameObject that has the `dungeonGenerator` component (this is the ecosystem generator).
3. In the Inspector, assign `ecosystemJson` to `Assets/Scripts/ecosystem.json` (or your own compatible JSON).
4. (Optional) Assign room prefab/parents if you want spawned room objects.
5. Generate using one of these:
	 - Enable `generateOnStart` and press Play.
	 - Use the Inspector context menu: **Generate Ecosystem**.
	 - In Play Mode, click the on-screen **Regenerate** button.

## What gets generated

- Room graph with `START`, `GOAL`, and intermediate rooms.
- A biome (from `biomeToBeGenerated`, or fallback if not found).
- Per-room zone type based on biome zone list.
- Per-room creature occupants filtered to the selected biome.
- Optional room GameObjects, edges, labels, and click-to-preview panel.

## Core parameters (Inspector)

### Biome selection

- `biomeToBeGenerated`: preferred biome name (case-insensitive match against JSON biome `name`).
	- If found, that biome is used.
	- If not found or blank, a biome is picked randomly from `biomes[]`.

### Room count and layout

- `roomMin`, `roomMax`: random room count range (clamped to minimum 2).
- `noiseScale`, `noiseJitter`, `noiseMinSpacing`, `noiseCandidateMultiplier`, `noiseOctaves`, `noiseLacunarity`, `noiseGain`:
	control noise-based room placement before graph connectivity pass.

### Creature distribution

- `creatureCrWeightExponent`:
	- `< 1`: favors lower-CR creatures.
	- `= 1`: neutral weighting.
	- `> 1`: favors higher-CR creatures.
- `creaturePopulationMultiplier`: scales each selected creature's `population_range` when rolling room populations.

### Optional visualization

- `spawnRoomObjects`, `roomPrefab`, `roomsParent`, `spawnEdges`, `edgesParent`, `uniformRoomScale`.
- Room preview settings affect in-scene click previews only.

## Biome data model (JSON)

Biome entries live in `biomes[]` in the ecosystem JSON. Example:

```json
{
	"name": "forest",
	"description": "Dense woodland...",
	"light_level": "medium",
	"water_level": "low",
	"temperature": "moderate",
	"primary_resources": ["berries", "wood"],
	"zone_types": ["open_grove", "dense_thicket", "ancient_ruins"]
}
```

### Which biome fields are core right now

- `name` (**required**): used to select biome and to match creatures by biome.
- `zone_types` (**required for meaningful zones**): random room zone labels are picked from this list.

### Biome fields currently treated as metadata

- `description`
- `light_level`
- `water_level`
- `temperature`
- `primary_resources`

These fields are loaded and available in data, but current generation logic does not directly use them for room/creature calculations.

## Creature fields that interact with biomes

Each creature in `creatures[]` should include:

- `name`
- `biomes` (must contain selected biome name to be eligible)
- `cr` (used by CR weighting)
- `population_range` (must contain 2 ints, max must be > 0)
- `spritePath` (optional, used for preview sprite loading from `Resources`)

Notes/Extras:
- `drops` is not currently used in this, but was added as supplementary for future projects

## Adding or tuning a biome

1. Add a new biome object to `biomes[]` with unique `name` and a non-empty `zone_types` list.
2. Add/update creatures so their `biomes` arrays include that biome name.
3. Set `biomeToBeGenerated` to the new biome name and regenerate.

## Troubleshooting

- **"Ecosystem JSON not set or failed to parse."**
	- Assign `ecosystemJson` and validate JSON format.
- **Biome shown is not what you typed**
	- Name mismatch causes fallback to random biome selection.
- **Empty creature rooms**
	- No creatures matched biome, or creature `population_range` was invalid/non-positive.
- **Missing creature sprite in preview**
	- Ensure `spritePath` points to a Sprite under a `Resources/` path.

## Asset credits

Free assets from CraftPix:

- Deer: https://craftpix.net/freebies/free-top-down-hunt-animals-pixel-sprite-pack/?num=1&count=1&sq=deer&pos=0
- Scorpion / Salamander / Spider: https://craftpix.net/freebies/free-chaos-monsters-32x32-icon-pack/?num=1&count=5&sq=scorpion&pos=4
- Vine Creeper / Moss / Mole: https://craftpix.net/freebies/free-low-level-monsters-pixel-icons-32x32/

Current usage is via Unity Editor (no packaged build documented in this repo yet).