using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LootboxTool : MonoBehaviour
{
	public enum ArtifactEffectType
	{
		None,
		DamageCardsGainOne,
		BlockCardsGainOne,
		DrawOnBlockCardPlay,
		DrawOnDamageCardPlay,
		HealOnCardDraw,
		DamageOnHeal,
		EnergyOnDamageCardPlay,
		EnergyOnBlockCardPlay,
		HealOnBlockCardPlay,
		DamageOnBlockCardPlay,
		ExtraDrawEachTurn,
		BurnOnDamageCardPlay,
		BlockOnCardDraw,
		BlockEachTurnStart,
		HealEachTurnStart,
		DamageEachTurnStart,
		BlockOnDamageCardPlay,
		EnergyEachTurnStart,
		BurnOnBlockCardPlay,
		DrawOnHeal,
		VulnerableOnDamageCardPlay,
		VulnerableEachTurnStart,
		BonusDamageWhenEnemyVulnerable
	}

	[Serializable]
	public class ArtifactDefinition
	{
		public string id;
		public string name;
		[TextArea(2, 4)] public string description;
		public string rarity;
		public ArtifactEffectType effectType;
	}

	[Serializable]
	public class ArtifactItem
	{
		public string id;
		public string name;
		public string description;
		public string rarity;
		public ArtifactEffectType effectType;
		public string sourceItemName;
		public string sourceItemRarity;
	}

	public struct ArtifactCombatBonuses
	{
		public int damageCardBonus;
		public int blockCardBonus;
		public int drawOnBlockCardPlay;
		public int drawOnDamageCardPlay;
		public int healOnCardDrawTriggers;
		public int damageOnHealTriggers;
		public int energyOnDamageCardPlay;
		public int energyOnBlockCardPlay;
		public int healOnBlockCardPlayTriggers;
		public int damageOnBlockCardPlayTriggers;
		public int extraDrawEachTurn;
		public int burnOnDamageCardPlay;
		public int blockOnCardDraw;
		public int blockEachTurnStart;
		public int healEachTurnStart;
		public int damageEachTurnStart;
		public int blockOnDamageCardPlay;
		public int energyEachTurnStart;
		public int burnOnBlockCardPlay;
		public int drawOnHeal;
		public int vulnerableOnDamageCardPlay;
		public int vulnerableEachTurnStart;
		public int bonusDamageWhenEnemyVulnerable;
	}

	[Header("References")]
	[SerializeField] private ItemTool itemTool;
	[SerializeField] private bool useItemToolToGenerateArtifactSourceItems = true;

	[Header("Lootbox Defaults")]
	[SerializeField, Min(1)] private int defaultBoxTier = 1;
	[SerializeField, Min(1)] private int defaultBoxSize = 1;
	[SerializeField, Range(0f, 1f)] private float defaultLuck = 0f;
	[SerializeField, Min(1)] private int defaultBatchCount = 1;

	[Header("Inventory")]
	[SerializeField] private bool startInventoryOpen = false;
	[SerializeField] private bool useDebugOnGui = true;
	[SerializeField] private KeyCode toggleInventoryKey = KeyCode.I;

	[SerializeField] private List<ArtifactItem> artifactInventory = new List<ArtifactItem>();

	private readonly List<ArtifactDefinition> artifactCatalog = new List<ArtifactDefinition>();
	private const string InvalidItemDataName = "Invalid Item Data";
	private bool inventoryOpen;
	private Vector2 inventoryScroll = Vector2.zero;
	private string lastOpenSummary = string.Empty;

	public static LootboxTool Instance { get; private set; }

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else if (Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		if (itemTool == null)
		{
			itemTool = FindItemToolInScene();
		}

		BuildDefaultArtifactCatalog();
		inventoryOpen = startInventoryOpen;
	}

	private void Update()
	{
		if (WasToggleInventoryPressedThisFrame())
		{
			inventoryOpen = !inventoryOpen;
		}
	}

	private bool WasToggleInventoryPressedThisFrame()
	{
#if ENABLE_INPUT_SYSTEM
		if (Keyboard.current != null && TryGetInputSystemKey(toggleInventoryKey, out var key))
		{
			var keyControl = Keyboard.current[key];
			if (keyControl != null && keyControl.wasPressedThisFrame)
			{
				return true;
			}
		}
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
		return Input.GetKeyDown(toggleInventoryKey);
#else
		return false;
#endif
	}

#if ENABLE_INPUT_SYSTEM
	private static bool TryGetInputSystemKey(KeyCode keyCode, out Key key)
	{
		var keyName = keyCode.ToString();

		if (keyName.StartsWith("Alpha", StringComparison.Ordinal))
		{
			keyName = "Digit" + keyName.Substring("Alpha".Length);
		}
		else if (keyName.StartsWith("Keypad", StringComparison.Ordinal))
		{
			keyName = "Numpad" + keyName.Substring("Keypad".Length);
		}
		else if (string.Equals(keyName, nameof(KeyCode.Return), StringComparison.Ordinal)
			|| string.Equals(keyName, nameof(KeyCode.KeypadEnter), StringComparison.Ordinal))
		{
			keyName = nameof(Key.Enter);
		}

		if (Enum.TryParse(keyName, ignoreCase: false, out key) && key != Key.None)
		{
			return true;
		}

		key = Key.None;
		return false;
	}
#endif

	private static ItemTool FindItemToolInScene()
	{
#if UNITY_2023_1_OR_NEWER
		return UnityEngine.Object.FindFirstObjectByType<ItemTool>();
#else
		return UnityEngine.Object.FindObjectOfType<ItemTool>();
#endif
	}

	[ContextMenu("Open Default Artifact Lootbox")]
	public void OpenDefaultLootbox()
	{
		OpenLootboxAndAddToInventory(defaultBoxTier, defaultBoxSize, defaultLuck, defaultBatchCount);
	}

	public List<ArtifactItem> OpenLootboxAndAddToInventory(int rarityTier, int size, float luck, int count = 1)
	{
		var safeCount = Mathf.Max(1, count);
		var allDrops = new List<ArtifactItem>();

		for (var i = 0; i < safeCount; i++)
		{
			allDrops.AddRange(GenerateSingleLootbox(rarityTier, size, luck));
		}

		artifactInventory.AddRange(allDrops);
		lastOpenSummary = $"Opened {safeCount} box(es), gained {allDrops.Count} artifact(s). Inventory: {artifactInventory.Count}";
		return allDrops;
	}

	public ArtifactItem OpenSingleArtifactLootbox(int rarityTier, float luck)
	{
		var boxTier = Mathf.Max(1, rarityTier);
		var normalizedLuck = Mathf.Clamp01(luck);
		var rolledTier = RollDropTier(boxTier, normalizedLuck);
		var artifact = CreateArtifactForTier(rolledTier);
		if (artifact == null)
		{
			lastOpenSummary = "Lootbox failed to generate an artifact.";
			return null;
		}

		artifactInventory.Add(artifact);
		lastOpenSummary = $"Opened a lootbox and gained {artifact.name} [{artifact.rarity}]. Inventory: {artifactInventory.Count}";
		return artifact;
	}

	public ArtifactCombatBonuses GetActiveArtifactBonuses()
	{
		var bonuses = new ArtifactCombatBonuses();
		for (var i = 0; i < artifactInventory.Count; i++)
		{
			var artifact = artifactInventory[i];
			if (artifact == null)
			{
				continue;
			}

			switch (artifact.effectType)
			{
				case ArtifactEffectType.DamageCardsGainOne:
					bonuses.damageCardBonus += 1;
					break;
				case ArtifactEffectType.BlockCardsGainOne:
					bonuses.blockCardBonus += 1;
					break;
				case ArtifactEffectType.DrawOnBlockCardPlay:
					bonuses.drawOnBlockCardPlay += 1;
					break;
				case ArtifactEffectType.DrawOnDamageCardPlay:
					bonuses.drawOnDamageCardPlay += 1;
					break;
				case ArtifactEffectType.HealOnCardDraw:
					bonuses.healOnCardDrawTriggers += 1;
					break;
				case ArtifactEffectType.DamageOnHeal:
					bonuses.damageOnHealTriggers += 1;
					break;
				case ArtifactEffectType.EnergyOnDamageCardPlay:
					bonuses.energyOnDamageCardPlay += 1;
					break;
				case ArtifactEffectType.EnergyOnBlockCardPlay:
					bonuses.energyOnBlockCardPlay += 1;
					break;
				case ArtifactEffectType.HealOnBlockCardPlay:
					bonuses.healOnBlockCardPlayTriggers += 1;
					break;
				case ArtifactEffectType.DamageOnBlockCardPlay:
					bonuses.damageOnBlockCardPlayTriggers += 1;
					break;
				case ArtifactEffectType.ExtraDrawEachTurn:
					bonuses.extraDrawEachTurn += 1;
					break;
				case ArtifactEffectType.BurnOnDamageCardPlay:
					bonuses.burnOnDamageCardPlay += 1;
					break;
				case ArtifactEffectType.BlockOnCardDraw:
					bonuses.blockOnCardDraw += 1;
					break;
				case ArtifactEffectType.BlockEachTurnStart:
					bonuses.blockEachTurnStart += 1;
					break;
				case ArtifactEffectType.HealEachTurnStart:
					bonuses.healEachTurnStart += 1;
					break;
				case ArtifactEffectType.DamageEachTurnStart:
					bonuses.damageEachTurnStart += 1;
					break;
				case ArtifactEffectType.BlockOnDamageCardPlay:
					bonuses.blockOnDamageCardPlay += 1;
					break;
				case ArtifactEffectType.EnergyEachTurnStart:
					bonuses.energyEachTurnStart += 1;
					break;
				case ArtifactEffectType.BurnOnBlockCardPlay:
					bonuses.burnOnBlockCardPlay += 1;
					break;
				case ArtifactEffectType.DrawOnHeal:
					bonuses.drawOnHeal += 1;
					break;
				case ArtifactEffectType.VulnerableOnDamageCardPlay:
					bonuses.vulnerableOnDamageCardPlay += 1;
					break;
				case ArtifactEffectType.VulnerableEachTurnStart:
					bonuses.vulnerableEachTurnStart += 1;
					break;
				case ArtifactEffectType.BonusDamageWhenEnemyVulnerable:
					bonuses.bonusDamageWhenEnemyVulnerable += 2;
					break;
			}
		}

		return bonuses;
	}

	public int GetArtifactCount()
	{
		return artifactInventory.Count;
	}

	public IReadOnlyList<ArtifactItem> GetArtifactInventory()
	{
		return artifactInventory;
	}

	public void ClearInventory()
	{
		artifactInventory.Clear();
		lastOpenSummary = "Artifact inventory cleared.";
	}

	private List<ArtifactItem> GenerateSingleLootbox(int rarityTier, int size, float luck)
	{
		if (artifactCatalog.Count == 0)
		{
			BuildDefaultArtifactCatalog();
		}

		var boxTier = Mathf.Max(1, rarityTier);
		var boxSize = Mathf.Max(1, size);
		var normalizedLuck = Mathf.Clamp01(luck);

		var minItems = Mathf.Max(1, 2 * boxSize);
		var maxItems = Mathf.Max(minItems, Mathf.FloorToInt(2f * boxSize + 2f * Mathf.Sqrt(boxSize)));
		var totalItems = UnityEngine.Random.Range(minItems, maxItems + 1);

		var drops = new List<ArtifactItem>(totalItems);

		for (var i = 0; i < boxSize; i++)
		{
			drops.Add(CreateArtifactForTier(boxTier));
		}

		while (drops.Count < totalItems)
		{
			var rolledTier = RollDropTier(boxTier, normalizedLuck);
			drops.Add(CreateArtifactForTier(rolledTier));
		}

		return drops;
	}

	private int RollDropTier(int boxTier, float normalizedLuck)
	{
		if (boxTier <= 1)
		{
			return WeightedRoll(
				new[] { boxTier + 1, boxTier },
				new[] { 25f + 75f * normalizedLuck, 75f - 75f * normalizedLuck });
		}

		if (boxTier == 2)
		{
			return WeightedRoll(
				new[] { boxTier + 1, boxTier, boxTier - 1 },
				new[] { 10f + 90f * normalizedLuck, 40f - 40f * normalizedLuck, 50f - 50f * normalizedLuck });
		}

		var lowerOne = Mathf.Max(1, boxTier - 1);
		var lowerTwo = Mathf.Max(1, boxTier - 2);
		return WeightedRoll(
			new[] { boxTier + 1, boxTier, lowerOne, lowerTwo },
			new[] { 5f + 95f * normalizedLuck, 40f - 40f * normalizedLuck, 45f - 45f * normalizedLuck, 10f - 10f * normalizedLuck });
	}

	private static int WeightedRoll(int[] values, float[] weights)
	{
		if (values == null || weights == null || values.Length == 0 || values.Length != weights.Length)
		{
			return 1;
		}

		var totalWeight = 0f;
		for (var i = 0; i < weights.Length; i++)
		{
			totalWeight += Mathf.Max(0f, weights[i]);
		}

		if (totalWeight <= 0f)
		{
			return values[UnityEngine.Random.Range(0, values.Length)];
		}

		var roll = UnityEngine.Random.Range(0f, totalWeight);
		var cumulative = 0f;
		for (var i = 0; i < values.Length; i++)
		{
			cumulative += Mathf.Max(0f, weights[i]);
			if (roll <= cumulative)
			{
				return values[i];
			}
		}

		return values[values.Length - 1];
	}

	private ArtifactItem CreateArtifactForTier(int tier)
	{
		var rarity = ItemTool.TierToRarity(tier);
		var sourceItem = TryGenerateSourceItemForTier(tier, rarity);
		if (sourceItem != null && !string.IsNullOrWhiteSpace(sourceItem.Rarity))
		{
			rarity = sourceItem.Rarity;
		}

		var candidates = new List<ArtifactDefinition>();
		for (var i = 0; i < artifactCatalog.Count; i++)
		{
			var definition = artifactCatalog[i];
			if (definition == null)
			{
				continue;
			}

			if (string.Equals(definition.rarity, rarity, StringComparison.OrdinalIgnoreCase))
			{
				candidates.Add(definition);
			}
		}

		if (candidates.Count == 0)
		{
			candidates.AddRange(artifactCatalog);
		}

		var selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
		return new ArtifactItem
		{
			id = selected.id,
			name = selected.name,
			description = selected.description,
			rarity = selected.rarity,
			effectType = selected.effectType,
			sourceItemName = sourceItem != null ? sourceItem.Name : string.Empty,
			sourceItemRarity = sourceItem != null ? sourceItem.Rarity : selected.rarity
		};
	}

	private GeneratedItem TryGenerateSourceItemForTier(int tier, string fallbackRarity)
	{
		if (!useItemToolToGenerateArtifactSourceItems)
		{
			return null;
		}

		if (itemTool == null)
		{
			itemTool = FindItemToolInScene();
		}

		if (itemTool == null)
		{
			return null;
		}

		var options = new ItemTool.ItemGenerationOptions
		{
			BaseNameOverride = string.Empty,
			BaseNameCategoryOverride = string.Empty,
			LootLuckOverride = Mathf.Clamp((tier - 1) * 900, 0, 2500),
			RarityOverride = fallbackRarity
		};

		var generated = itemTool.GenerateSingleItem(options);
		return IsValidSourceItem(generated) ? generated : null;
	}

	private static bool IsValidSourceItem(GeneratedItem item)
	{
		if (item == null || string.IsNullOrWhiteSpace(item.Name))
		{
			return false;
		}

		return !string.Equals(item.Name, InvalidItemDataName, StringComparison.OrdinalIgnoreCase);
	}

	private void BuildDefaultArtifactCatalog()
	{
		artifactCatalog.Clear();
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_damage_cards_plus_one",
			name = "Sharpened Intent",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.DamageCardsGainOne,
			description = "All damage cards gain +1 in each combat."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_block_cards_plus_one",
			name = "Guardian Rhythm",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.BlockCardsGainOne,
			description = "All block cards gain +1 in each combat."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_block_draw_one",
			name = "Tactical Carapace",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.DrawOnBlockCardPlay,
			description = "When you play a block card, draw one card."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_damage_draw_one",
			name = "Hunting Loop",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.DrawOnDamageCardPlay,
			description = "When you play a damage card, draw one card."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_draw_heal_three",
			name = "Resonant Heart",
			rarity = ItemTool.RarityLegendary,
			effectType = ArtifactEffectType.HealOnCardDraw,
			description = "When you draw a card, heal 3."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_heal_damage_three",
			name = "Mercybrand",
			rarity = ItemTool.RarityLegendary,
			effectType = ArtifactEffectType.DamageOnHeal,
			description = "When you heal, deal 3 damage."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_energy_on_damage_play",
			name = "Arc Capacitor",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.EnergyOnDamageCardPlay,
			description = "When you play a damage card, gain 1 energy."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_energy_on_block_play",
			name = "Defender Battery",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.EnergyOnBlockCardPlay,
			description = "When you play a block card, gain 1 energy."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_heal_on_block_play",
			name = "Warmth Moss",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.HealOnBlockCardPlay,
			description = "When you play a block card, heal 2."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_damage_on_block_play",
			name = "Spiked Guard",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.DamageOnBlockCardPlay,
			description = "When you play a block card, deal 2 damage."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_extra_draw_each_turn",
			name = "Mind Frond",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.ExtraDrawEachTurn,
			description = "At the start of each turn, draw 1 additional card."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_burn_on_damage_play",
			name = "Cinder Loop",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.BurnOnDamageCardPlay,
			description = "When you play a damage card, apply Burn 1."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_block_on_draw",
			name = "Shell Script",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.BlockOnCardDraw,
			description = "When you draw a card, gain 1 block."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_block_turn_start",
			name = "Anchor Bark",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.BlockEachTurnStart,
			description = "At the start of each turn, gain 2 block."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_heal_turn_start",
			name = "Dew Capsule",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.HealEachTurnStart,
			description = "At the start of each turn, heal 1."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_damage_turn_start",
			name = "Pulse Thorn",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.DamageEachTurnStart,
			description = "At the start of each turn, deal 2 damage."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_block_on_damage_play",
			name = "Reactive Bark",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.BlockOnDamageCardPlay,
			description = "When you play a damage card, gain 2 block."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_energy_turn_start",
			name = "Solar Core",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.EnergyEachTurnStart,
			description = "At the start of each turn, gain 1 energy."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_burn_on_block_play",
			name = "Ashen Bulwark",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.BurnOnBlockCardPlay,
			description = "When you play a block card, apply Burn 1."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_draw_on_heal",
			name = "Echo Vial",
			rarity = ItemTool.RarityLegendary,
			effectType = ArtifactEffectType.DrawOnHeal,
			description = "When you heal, draw 1 card."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_vulnerable_on_damage_play",
			name = "Hunter Sigil",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.VulnerableOnDamageCardPlay,
			description = "When you play a damage card, apply Vulnerable 1."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_vulnerable_turn_start",
			name = "Opening Gambit",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.VulnerableEachTurnStart,
			description = "At the start of each turn, apply Vulnerable 1."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_bonus_damage_vulnerable",
			name = "Exposed Veins",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.BonusDamageWhenEnemyVulnerable,
			description = "Deal +2 damage to vulnerable enemies."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_damage_draw_predator",
			name = "Predator Chorus",
			rarity = ItemTool.RarityLegendary,
			effectType = ArtifactEffectType.DrawOnDamageCardPlay,
			description = "When you play a damage card, draw one card."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_burn_storm",
			name = "Bonefire Engine",
			rarity = ItemTool.RarityLegendary,
			effectType = ArtifactEffectType.BurnOnDamageCardPlay,
			description = "When you play a damage card, apply Burn 1."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_block_damage_warden",
			name = "Warden Knot",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.BlockOnDamageCardPlay,
			description = "When you play a damage card, gain 2 block."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_heal_start_lattice",
			name = "Lifespring Lattice",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.HealEachTurnStart,
			description = "At the start of each turn, heal 1."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_iron_tempest",
			name = "Iron Tempest",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.DamageCardsGainOne,
			description = "All damage cards gain +1 in each combat."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_aegis_spore",
			name = "Aegis Spore",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.BlockCardsGainOne,
			description = "All block cards gain +1 in each combat."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_hex_core",
			name = "Hex Core",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.VulnerableOnDamageCardPlay,
			description = "When you play a damage card, apply Vulnerable 1."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_suppressor_bloom",
			name = "Suppressor Bloom",
			rarity = ItemTool.RarityLegendary,
			effectType = ArtifactEffectType.VulnerableEachTurnStart,
			description = "At the start of each turn, apply Vulnerable 1."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_frenzy_engine",
			name = "Frenzy Engine",
			rarity = ItemTool.RarityLegendary,
			effectType = ArtifactEffectType.BonusDamageWhenEnemyVulnerable,
			description = "Deal +2 damage to vulnerable enemies."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_ember_carapace",
			name = "Ember Carapace",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.BurnOnBlockCardPlay,
			description = "When you play a block card, apply Burn 1."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_siphon_reed",
			name = "Siphon Reed",
			rarity = ItemTool.RarityEpic,
			effectType = ArtifactEffectType.HealOnCardDraw,
			description = "When you draw a card, heal 3."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_combat_dynamo",
			name = "Combat Dynamo",
			rarity = ItemTool.RarityLegendary,
			effectType = ArtifactEffectType.EnergyEachTurnStart,
			description = "At the start of each turn, gain 1 energy."
		});
		artifactCatalog.Add(new ArtifactDefinition
		{
			id = "artifact_thicket_codex",
			name = "Thicket Codex",
			rarity = ItemTool.RarityRare,
			effectType = ArtifactEffectType.DrawOnBlockCardPlay,
			description = "When you play a block card, draw one card."
		});
	}

	private void OnGUI()
	{
		if (!useDebugOnGui)
		{
			return;
		}

		var panelX = Mathf.Max(20f, Screen.width - 336f);

		var toggleText = inventoryOpen
			? $"Hide Artifacts ({artifactInventory.Count})"
			: $"Show Artifacts ({artifactInventory.Count})";

		if (GUI.Button(new Rect(panelX, 16f, 320f, 32f), toggleText))
		{
			inventoryOpen = !inventoryOpen;
		}

		if (!string.IsNullOrEmpty(lastOpenSummary))
		{
			GUI.Box(new Rect(panelX, 52f, 320f, 44f), lastOpenSummary);
		}

		if (!inventoryOpen)
		{
			return;
		}

		var listRect = new Rect(panelX, 102f, 320f, Mathf.Max(220f, Screen.height - 112f));
		GUI.Box(listRect, "Artifact Inventory");

		var viewHeight = Mathf.Max(40f, artifactInventory.Count * 62f + 8f);
		var scrollRect = new Rect(listRect.x + 8f, listRect.y + 24f, listRect.width - 16f, listRect.height - 32f);
		var viewRect = new Rect(0f, 0f, scrollRect.width - 20f, viewHeight);
		inventoryScroll = GUI.BeginScrollView(scrollRect, inventoryScroll, viewRect);

		for (var i = 0; i < artifactInventory.Count; i++)
		{
			var artifact = artifactInventory[i];
			if (artifact == null)
			{
				continue;
			}

			var y = 4f + i * 62f;
			var hasValidSourceName = !string.IsNullOrWhiteSpace(artifact.sourceItemName)
				&& !string.Equals(artifact.sourceItemName, InvalidItemDataName, StringComparison.OrdinalIgnoreCase);
			var sourceText = !hasValidSourceName
				? string.Empty
				: $" | Source: {artifact.sourceItemName}";
			var rowText = $"#{i + 1} [{artifact.rarity}] {artifact.name}{sourceText}\n{artifact.description}";
			GUI.Box(new Rect(4f, y, viewRect.width - 8f, 56f), rowText);
		}

		GUI.EndScrollView();
	}
}
