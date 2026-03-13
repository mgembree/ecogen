using System;
using System.Collections.Generic;
using UnityEngine;

public class CardCombatGameState : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private dungeonGenerator ecosystemGenerator;
	[SerializeField] private LootboxTool lootboxTool;
	[SerializeField] private bool autoGenerateMapIfMissing = true;

	[Header("Player")]
	[SerializeField] private int playerMaxHp = 60;
	[SerializeField] private int handSize = 6;
	[SerializeField] private int energyPerTurn = 4;

	[Header("Starter Deck")]
	[SerializeField] private int attackCardCount = 8;
	[SerializeField] private int blockCardCount = 8;
	[SerializeField] private int cardCost = 1;
	[SerializeField] private int attackDamage = 5;
	[SerializeField] private int blockAmount = 5;

	[Header("Expanded Cards")]
	[SerializeField] private int heavyStrikeCount = 2;
	[SerializeField] private int heavyStrikeDamage = 9;
	[SerializeField] private int heavyStrikeCost = 2;
	[SerializeField] private int shieldWallCount = 2;
	[SerializeField] private int shieldWallBlock = 10;
	[SerializeField] private int shieldWallCost = 2;
	[SerializeField] private int mendCount = 2;
	[SerializeField] private int mendHeal = 4;
	[SerializeField] private int mendCost = 1;
	[SerializeField] private int insightCount = 2;
	[SerializeField] private int insightDraw = 2;
	[SerializeField] private int insightCost = 1;
	[SerializeField] private int sparkCount = 2;
	[SerializeField] private int sparkEnergyGain = 2;
	[SerializeField] private int sparkCost = 1;

	[Header("Enemy Tuning")]
	[SerializeField] private int baseEnemyHp = 18;
	[SerializeField] private float hpPerDanger = 8f;
	[SerializeField] private int minEnemyDamage = 1;
	[SerializeField] private int maxEnemyDamageFloor = 4;
	[SerializeField] private float damagePerDanger = 2f;
	[SerializeField] private float bossDangerMultiplier = 1.75f;
	[SerializeField] private float bossHpMultiplier = 1.8f;
	[SerializeField] private float bossDamageMultiplier = 1.5f;

	[Header("Danger Progression")]
	[SerializeField] private float dangerPerDepthStep = 1.2f;
	[SerializeField] private float occupantDangerVarianceMax = 0.4f;

	[Header("Quick UI")]
	[SerializeField] private bool useDebugOnGui = true;
	[SerializeField] private bool logRoomSelectionIssues = true;
	[SerializeField] private bool colorCodeRoomButtonsByDanger = true;
	[SerializeField] private Color lowDangerButtonColor = new Color(0.5f, 0.85f, 0.45f, 1f);
	[SerializeField] private Color mediumDangerButtonColor = new Color(0.93f, 0.82f, 0.35f, 1f);
	[SerializeField] private Color highDangerButtonColor = new Color(0.9f, 0.38f, 0.3f, 1f);

	[Header("Lootbox Rooms")]
	[SerializeField, Range(0f, 1f)] private float lootboxRoomChance = 0.2f;
	[SerializeField, Min(0)] private int minLootboxRooms = 1;
	[SerializeField, Min(0)] private int maxLootboxRooms = 2;
	[SerializeField, Min(1)] private int lootboxRoomArtifactCount = 1;
	[SerializeField] private bool grantLootboxOnEncounterDefeat = false;

	private readonly List<Card> deckTemplate = new List<Card>();
	private readonly List<Card> drawPile = new List<Card>();
	private readonly List<Card> discardPile = new List<Card>();
	private readonly List<Card> hand = new List<Card>();
	private readonly HashSet<int> clearedRooms = new HashSet<int>();
	private readonly HashSet<int> lootboxRoomIds = new HashSet<int>();
	private readonly List<int> availableRoomChoices = new List<int>();
	private readonly Dictionary<int, int> roomDepthById = new Dictionary<int, int>();
	private readonly List<int> rewardEligibleCardIndices = new List<int>();

	private RunPhase phase;
	private Encounter currentEncounter;
	private int currentRoomId = -1;
	private float highestNonBossDanger = 1f;
	private int maxDepthFromStart = 1;
	private RewardDrop pendingDrop;
	private bool rewardDropRevealed;
	private bool rewardAppliedThisReward;
	private string rewardStatusMessage = string.Empty;
	private Vector2 rewardCardListScroll = Vector2.zero;
	private bool lootboxOpenedForCurrentRoom;
	private string lootboxRoomStatusMessage = string.Empty;

	private int playerHp;
	private int playerBlock;
	private int playerEnergy;
	private int lastEnemyRoll;
	private int fightDamageBonus;
	private int fightBlockBonus;
	private int fightEnergyPerTurnBonus;
	private int enemyBurnDamagePerTurn;
	private int enemyBurnTurnsRemaining;
	private int artifactDamageCardBonus;
	private int artifactBlockCardBonus;
	private int artifactDrawCardsOnBlockCardPlay;
	private int artifactDrawCardsOnDamageCardPlay;
	private int artifactHealOnDrawAmount;
	private int artifactDamageOnHealAmount;
	private int artifactEnergyOnDamageCardPlay;
	private int artifactEnergyOnBlockCardPlay;
	private int artifactHealOnBlockCardPlayAmount;
	private int artifactDamageOnBlockCardPlayAmount;
	private int artifactExtraDrawEachTurn;
	private int artifactBurnOnDamageCardPlay;
	private int artifactBlockOnCardDrawAmount;
	private int artifactBlockAtTurnStartAmount;

	private enum RunPhase
	{
		MapSelection,
		LootboxRoom,
		PlayerTurn,
		EnemyTurn,
		RewardSelection,
		RunWon,
		Defeat
	}

	private enum CardEffect
	{
		Damage,
		Block,
		Heal,
		Draw,
		GainEnergy
	}

	private enum CardEnhancement
	{
		None,
		HardenedCarapace,
		KeenInstinct,
		LightweightCore,
		VenomEdge,
		EchoShell,
		AdrenalGland,
		RegenerativeSap,
		BurningEmber,
		PredatorInstinct,
		AnimalBrain,
		PredatoryMomentum,
		RhythmicHeartbeat,
		MetabolicSurge
	}

	private class Card
	{
		public string title;
		public CardEffect effect;
		public int value;
		public int cost;
		public CardEnhancement enhancement;

		public Card Clone()
		{
			return new Card
			{
				title = title,
				effect = effect,
				value = value,
				cost = cost,
				enhancement = enhancement
			};
		}
	}

	private class Encounter
	{
		public int roomId;
		public string roomLabel;
		public string enemyName;
		public bool isBoss;
		public float dangerRating;
		public int maxHp;
		public int currentHp;
		public int minDamage;
		public int maxDamage;
	}

	private class RewardDrop
	{
		public CardEnhancement enhancement;
		public string name;
		public string description;
	}

	private static readonly RewardDrop[] PossibleDrops =
	{
		new RewardDrop
		{
			enhancement = CardEnhancement.HardenedCarapace,
			name = "Hardened Carapace",
			description = "This card also blocks 4. If it already blocks, it blocks +4 more."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.KeenInstinct,
			name = "Keen Instinct",
			description = "Draw 1 card when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.LightweightCore,
			name = "Lightweight Core",
			description = "This card costs 1 less energy."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.VenomEdge,
			name = "Venom Edge",
			description = "This card also deals 4 damage. If it already deals damage, it deals +4 more."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.EchoShell,
			name = "Echo Shell",
			description = "This card is played an additional time for free."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.AdrenalGland,
			name = "Adrenal Gland",
			description = "Gain 1 energy when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.RegenerativeSap,
			name = "Regenerative Sap",
			description = "Heal 3 HP when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.BurningEmber,
			name = "Burning Ember",
			description = "Applies Burn status to the enemy, dealing 3 damage at the start of their turn for 3 turns."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.PredatorInstinct,
			name = "Predator Instinct",
			description = "Draw 1 card that deals damage when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.AnimalBrain,
			name = "Animal Brain",
			description = "Draw 1 card that blocks when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.PredatoryMomentum,
			name = "Predatory Momentum",
			description = "For this fight, all your damage gains +1 when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.RhythmicHeartbeat,
			name = "Rhythmic Heartbeat",
			description = "For this fight, all your block gains +1 when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.MetabolicSurge,
			name = "Metabolic Surge",
			description = "For this fight, gain +1 additional energy at the start of each turn when this card is played."
		}
	};

	private void Start()
	{
		InitializeRun();
	}

	private static dungeonGenerator FindGeneratorInScene()
	{
#if UNITY_2023_1_OR_NEWER
		return UnityEngine.Object.FindFirstObjectByType<dungeonGenerator>();
#else
		return UnityEngine.Object.FindObjectOfType<dungeonGenerator>();
#endif
	}

	private static LootboxTool FindLootboxToolInScene()
	{
#if UNITY_2023_1_OR_NEWER
		return UnityEngine.Object.FindFirstObjectByType<LootboxTool>();
#else
		return UnityEngine.Object.FindObjectOfType<LootboxTool>();
#endif
	}

	public void InitializeRun()
	{
		if (lootboxTool == null)
		{
			lootboxTool = FindLootboxToolInScene();
		}

		if (ecosystemGenerator == null)
		{
			ecosystemGenerator = FindGeneratorInScene();
		}

		if (ecosystemGenerator == null)
		{
			Debug.LogError("CardCombatGameState: Missing dungeonGenerator reference.");
			phase = RunPhase.MapSelection;
			currentRoomId = -1;
			availableRoomChoices.Clear();
			roomDepthById.Clear();
			maxDepthFromStart = 1;
			pendingDrop = null;
			rewardDropRevealed = false;
			rewardAppliedThisReward = false;
			rewardStatusMessage = string.Empty;
			rewardCardListScroll = Vector2.zero;
			rewardEligibleCardIndices.Clear();
			lootboxRoomIds.Clear();
			lootboxOpenedForCurrentRoom = false;
			lootboxRoomStatusMessage = string.Empty;
			return;
		}

		if ((ecosystemGenerator.GetGeneratedRooms() == null || ecosystemGenerator.GetGeneratedRooms().Count == 0) && autoGenerateMapIfMissing)
		{
			ecosystemGenerator.GenerateEcosystem();
		}

		var rooms = ecosystemGenerator.GetGeneratedRooms();
		if (rooms == null || rooms.Count == 0)
		{
			if (logRoomSelectionIssues)
			{
				Debug.LogWarning("CardCombatGameState: No generated rooms found yet. Waiting for map generation.");
			}

			phase = RunPhase.MapSelection;
			currentRoomId = -1;
			availableRoomChoices.Clear();
			currentEncounter = null;
			roomDepthById.Clear();
			maxDepthFromStart = 1;
			pendingDrop = null;
			rewardDropRevealed = false;
			rewardAppliedThisReward = false;
			rewardStatusMessage = string.Empty;
			rewardCardListScroll = Vector2.zero;
			rewardEligibleCardIndices.Clear();
			lootboxRoomIds.Clear();
			lootboxOpenedForCurrentRoom = false;
			lootboxRoomStatusMessage = string.Empty;
			return;
		}

		BuildStarterDeckTemplate();
		playerHp = playerMaxHp;
		playerBlock = 0;
		playerEnergy = 0;
		lastEnemyRoll = 0;
		currentEncounter = null;
		pendingDrop = null;
		rewardDropRevealed = false;
		rewardAppliedThisReward = false;
		rewardStatusMessage = string.Empty;
		rewardCardListScroll = Vector2.zero;
		rewardEligibleCardIndices.Clear();
		lootboxOpenedForCurrentRoom = false;
		lootboxRoomStatusMessage = string.Empty;
		ResetEncounterModifiers();
		RefreshArtifactCombatBonuses();

		clearedRooms.Clear();
		currentRoomId = FindStartRoomId();
		if (currentRoomId >= 0)
		{
			clearedRooms.Add(currentRoomId);
		}

		BuildRoomDepthMap(currentRoomId);
		highestNonBossDanger = CalculateHighestNonBossDanger();
		AssignLootboxRooms();
		phase = RunPhase.MapSelection;
		RefreshRoomChoices();
	}

	private int FindStartRoomId()
	{
		if (ecosystemGenerator == null)
		{
			return -1;
		}

		var rooms = ecosystemGenerator.GetGeneratedRooms();
		if (rooms == null || rooms.Count == 0)
		{
			return -1;
		}

		for (var i = 0; i < rooms.Count; i++)
		{
			var room = rooms[i];
			if (room != null && string.Equals(room.label, "START", StringComparison.OrdinalIgnoreCase))
			{
				return room.id;
			}
		}

		for (var i = 0; i < rooms.Count; i++)
		{
			if (rooms[i] != null)
			{
				return rooms[i].id;
			}
		}

		return -1;
	}

	private float CalculateHighestNonBossDanger()
	{
		var rooms = ecosystemGenerator.GetGeneratedRooms();
		if (rooms == null || rooms.Count == 0)
		{
			return 1f;
		}

		var maxDanger = 1f;
		for (var i = 0; i < rooms.Count; i++)
		{
			var room = rooms[i];
			if (room == null || IsGoalRoom(room))
			{
				continue;
			}

			maxDanger = Mathf.Max(maxDanger, CalculateEncounterDanger(room));
		}

		return maxDanger;
	}

	private void AssignLootboxRooms()
	{
		lootboxRoomIds.Clear();
		if (ecosystemGenerator == null)
		{
			return;
		}

		var rooms = ecosystemGenerator.GetGeneratedRooms();
		if (rooms == null || rooms.Count == 0)
		{
			return;
		}

		var eligible = new List<int>();
		for (var i = 0; i < rooms.Count; i++)
		{
			var room = rooms[i];
			if (room == null)
			{
				continue;
			}

			if (string.Equals(room.label, "START", StringComparison.OrdinalIgnoreCase) || IsGoalRoom(room))
			{
				continue;
			}

			eligible.Add(room.id);
		}

		if (eligible.Count == 0)
		{
			return;
		}

		var minCount = Mathf.Max(0, minLootboxRooms);
		var maxCount = Mathf.Max(minCount, maxLootboxRooms);
		var byChance = Mathf.RoundToInt(eligible.Count * Mathf.Clamp01(lootboxRoomChance));
		var targetCount = Mathf.Clamp(byChance, minCount, maxCount);
		targetCount = Mathf.Clamp(targetCount, 0, eligible.Count);

		for (var i = eligible.Count - 1; i > 0; i--)
		{
			var j = UnityEngine.Random.Range(0, i + 1);
			var tmp = eligible[i];
			eligible[i] = eligible[j];
			eligible[j] = tmp;
		}

		for (var i = 0; i < targetCount; i++)
		{
			lootboxRoomIds.Add(eligible[i]);
		}
	}

	private void BuildRoomDepthMap(int startRoomId)
	{
		roomDepthById.Clear();
		maxDepthFromStart = 1;

		if (ecosystemGenerator == null || startRoomId < 0)
		{
			return;
		}

		var queue = new Queue<int>();
		queue.Enqueue(startRoomId);
		roomDepthById[startRoomId] = 0;

		while (queue.Count > 0)
		{
			var roomId = queue.Dequeue();
			if (!roomDepthById.TryGetValue(roomId, out var depth))
			{
				continue;
			}

			maxDepthFromStart = Mathf.Max(maxDepthFromStart, depth);
			var neighbors = ecosystemGenerator.GetNeighborRoomIds(roomId);
			for (var i = 0; i < neighbors.Length; i++)
			{
				var neighborId = neighbors[i];
				if (roomDepthById.ContainsKey(neighborId))
				{
					continue;
				}

				roomDepthById[neighborId] = depth + 1;
				queue.Enqueue(neighborId);
			}
		}

		var rooms = ecosystemGenerator.GetGeneratedRooms();
		if (rooms == null || rooms.Count == 0)
		{
			return;
		}

		for (var i = 0; i < rooms.Count; i++)
		{
			if (rooms[i] == null)
			{
				continue;
			}

			if (!roomDepthById.ContainsKey(rooms[i].id))
			{
				roomDepthById[rooms[i].id] = maxDepthFromStart + 1;
			}
		}

		foreach (var depth in roomDepthById.Values)
		{
			maxDepthFromStart = Mathf.Max(maxDepthFromStart, depth);
		}
	}

	private int GetRoomDepth(int roomId)
	{
		if (roomDepthById.TryGetValue(roomId, out var depth))
		{
			return depth;
		}

		return Mathf.Max(1, maxDepthFromStart);
	}

	private float CalculateEncounterDanger(dungeonGenerator.RoomData room)
	{
		if (room == null)
		{
			return 1f;
		}

		var depth = Mathf.Max(1, GetRoomDepth(room.id));
		var nonStartDepth = Mathf.Max(0, depth - 1);
		var depthStep = Mathf.Max(0.25f, dangerPerDepthStep);

		// Keep depth progression dominant so deeper rooms remain more dangerous.
		var varianceCap = Mathf.Clamp(occupantDangerVarianceMax, 0f, depthStep * 0.9f);
		var depthDanger = 1f + nonStartDepth * depthStep;
		var occupantDanger = CalculateRoomDanger(room);
		var normalizedOccupantDanger = Mathf.Clamp01(Mathf.Log10(1f + occupantDanger));

		return depthDanger + normalizedOccupantDanger * varianceCap;
	}

	private Color GetRoomDangerButtonColor(dungeonGenerator.RoomData room)
	{
		if (!colorCodeRoomButtonsByDanger || room == null)
		{
			return Color.white;
		}

		if (IsGoalRoom(room))
		{
			return highDangerButtonColor;
		}

		var depth = Mathf.Max(1, GetRoomDepth(room.id));
		var maxDepth = Mathf.Max(2, maxDepthFromStart);
		var t = Mathf.InverseLerp(1f, maxDepth, depth);
		if (t < 0.5f)
		{
			return Color.Lerp(lowDangerButtonColor, mediumDangerButtonColor, t * 2f);
		}

		return Color.Lerp(mediumDangerButtonColor, highDangerButtonColor, (t - 0.5f) * 2f);
	}

	private static Color GetReadableTextColor(Color background)
	{
		var luminance = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
		return luminance > 0.62f ? Color.black : Color.white;
	}

	private void BuildStarterDeckTemplate()
	{
		deckTemplate.Clear();
		var normalCost = Mathf.Max(0, cardCost);

		AddCardsToDeckTemplate("Strike", CardEffect.Damage, Mathf.Max(0, attackDamage), normalCost, Mathf.Max(1, attackCardCount));
		AddCardsToDeckTemplate("Guard", CardEffect.Block, Mathf.Max(0, blockAmount), normalCost, Mathf.Max(1, blockCardCount));

		AddCardsToDeckTemplate("Heavy Strike", CardEffect.Damage, Mathf.Max(0, heavyStrikeDamage), Mathf.Max(0, heavyStrikeCost), Mathf.Max(0, heavyStrikeCount));
		AddCardsToDeckTemplate("Shield Wall", CardEffect.Block, Mathf.Max(0, shieldWallBlock), Mathf.Max(0, shieldWallCost), Mathf.Max(0, shieldWallCount));
		AddCardsToDeckTemplate("Mend", CardEffect.Heal, Mathf.Max(0, mendHeal), Mathf.Max(0, mendCost), Mathf.Max(0, mendCount));
		AddCardsToDeckTemplate("Insight", CardEffect.Draw, Mathf.Max(0, insightDraw), Mathf.Max(0, insightCost), Mathf.Max(0, insightCount));
		AddCardsToDeckTemplate("Spark", CardEffect.GainEnergy, Mathf.Max(0, sparkEnergyGain), Mathf.Max(0, sparkCost), Mathf.Max(0, sparkCount));
	}

	private void AddCardsToDeckTemplate(string baseTitle, CardEffect effect, int value, int cost, int count)
	{
		var clampedCount = Mathf.Max(0, count);
		for (var i = 0; i < clampedCount; i++)
		{
			deckTemplate.Add(new Card
			{
				title = $"{baseTitle} ({value})",
				effect = effect,
				value = value,
				cost = cost,
				enhancement = CardEnhancement.None
			});
		}
	}

	private int GetEffectiveCardCost(Card card)
	{
		if (card == null)
		{
			return 0;
		}

		var cost = card.cost;
		if (card.enhancement == CardEnhancement.LightweightCore)
		{
			cost -= 1;
		}

		return Mathf.Max(0, cost);
	}

	private static string GetEnhancementLabel(CardEnhancement enhancement)
	{
		switch (enhancement)
		{
			case CardEnhancement.HardenedCarapace:
				return "Hardened Carapace";
			case CardEnhancement.KeenInstinct:
				return "Keen Instinct";
			case CardEnhancement.LightweightCore:
				return "Lightweight Core";
			case CardEnhancement.VenomEdge:
				return "Venom Edge";
			case CardEnhancement.EchoShell:
				return "Echo Shell";
			case CardEnhancement.AdrenalGland:
				return "Adrenal Gland";
			case CardEnhancement.RegenerativeSap:
				return "Regenerative Sap";
			case CardEnhancement.BurningEmber:
				return "Burning Ember";
			case CardEnhancement.PredatorInstinct:
				return "Predator Instinct";
			case CardEnhancement.AnimalBrain:
				return "Animal Brain";
			case CardEnhancement.PredatoryMomentum:
				return "Predatory Momentum";
			case CardEnhancement.RhythmicHeartbeat:
				return "Rhythmic Heartbeat";
			case CardEnhancement.MetabolicSurge:
				return "Metabolic Surge";
			default:
				return "None";
		}
	}

	private string GetCardDisplayText(Card card)
	{
		if (card == null)
		{
			return "Unknown";
		}

		var cost = GetEffectiveCardCost(card);
		if (card.enhancement == CardEnhancement.None)
		{
			return $"{card.title}\nCost {cost}";
		}

		return $"{card.title}\nCost {cost} | {GetEnhancementLabel(card.enhancement)}";
	}

	private RewardDrop RollCreatureDrop()
	{
		if (PossibleDrops == null || PossibleDrops.Length == 0)
		{
			return null;
		}

		var index = UnityEngine.Random.Range(0, PossibleDrops.Length);
		var baseDrop = PossibleDrops[index];
		return new RewardDrop
		{
			enhancement = baseDrop.enhancement,
			name = baseDrop.name,
			description = baseDrop.description
		};
	}

	private void BeginRewardSelection()
	{
		pendingDrop = RollCreatureDrop();
		rewardDropRevealed = false;
		rewardAppliedThisReward = false;
		rewardStatusMessage = string.Empty;
		rewardCardListScroll = Vector2.zero;
		BuildRewardEligibleCardIndices();
		phase = RunPhase.RewardSelection;
	}

	private void BuildRewardEligibleCardIndices()
	{
		rewardEligibleCardIndices.Clear();
		for (var i = 0; i < deckTemplate.Count; i++)
		{
			var card = deckTemplate[i];
			if (card == null)
			{
				continue;
			}

			if (card.enhancement == CardEnhancement.None)
			{
				rewardEligibleCardIndices.Add(i);
			}
		}
	}

	private bool TryApplyDropToCard(int deckIndex)
	{
		if (pendingDrop == null || deckIndex < 0 || deckIndex >= deckTemplate.Count)
		{
			return false;
		}

		var card = deckTemplate[deckIndex];
		if (card == null || card.enhancement != CardEnhancement.None)
		{
			return false;
		}

		card.enhancement = pendingDrop.enhancement;
		rewardAppliedThisReward = true;
		rewardStatusMessage = $"Applied {pendingDrop.name} to {card.title}.";
		BuildRewardEligibleCardIndices();
		return true;
	}

	private void PrepareDeckForEncounter()
	{
		drawPile.Clear();
		discardPile.Clear();
		hand.Clear();

		for (var i = 0; i < deckTemplate.Count; i++)
		{
			drawPile.Add(deckTemplate[i].Clone());
		}

		Shuffle(drawPile);
	}

	private void RefreshRoomChoices()
	{
		availableRoomChoices.Clear();
		if (currentRoomId < 0)
		{
			return;
		}

		var neighbors = ecosystemGenerator.GetNeighborRoomIds(currentRoomId);
		for (var i = 0; i < neighbors.Length; i++)
		{
			var roomId = neighbors[i];
			if (roomId == currentRoomId)
			{
				continue;
			}

			if (!clearedRooms.Contains(roomId))
			{
				availableRoomChoices.Add(roomId);
			}
		}

		if (availableRoomChoices.Count == 0)
		{
			for (var i = 0; i < neighbors.Length; i++)
			{
				if (neighbors[i] != currentRoomId)
				{
					availableRoomChoices.Add(neighbors[i]);
				}
			}
		}
	}

	private bool IsLootboxRoom(int roomId)
	{
		return lootboxRoomIds.Contains(roomId);
	}

	private void EnsureNavigationStateIsValid()
	{
		if (ecosystemGenerator == null)
		{
			ecosystemGenerator = FindGeneratorInScene();
			if (ecosystemGenerator == null)
			{
				currentRoomId = -1;
				availableRoomChoices.Clear();
				return;
			}
		}

		var rooms = ecosystemGenerator.GetGeneratedRooms();
		if (rooms == null || rooms.Count == 0)
		{
			currentRoomId = -1;
			availableRoomChoices.Clear();
			roomDepthById.Clear();
			maxDepthFromStart = 1;
			return;
		}

		if (roomDepthById.Count != rooms.Count)
		{
			BuildRoomDepthMap(FindStartRoomId());
			AssignLootboxRooms();
		}

		if (GetRoomById(currentRoomId) != null)
		{
			return;
		}

		var recoveredStart = FindStartRoomId();
		if (recoveredStart >= 0)
		{
			if (logRoomSelectionIssues)
			{
				Debug.LogWarning($"CardCombatGameState: Current room {currentRoomId} is invalid. Recovering to START room {recoveredStart}.");
			}

			currentRoomId = recoveredStart;
			clearedRooms.Add(currentRoomId);
			RefreshRoomChoices();
		}
	}

	public void ChooseRoom(int roomId)
	{
		if (phase != RunPhase.MapSelection)
		{
			return;
		}

		EnsureNavigationStateIsValid();

		var neighbors = ecosystemGenerator.GetNeighborRoomIds(currentRoomId);
		if (neighbors.Length == 0)
		{
			var oldRoomId = currentRoomId;
			currentRoomId = FindStartRoomId();
			if (currentRoomId >= 0)
			{
				clearedRooms.Add(currentRoomId);
			}

			if (logRoomSelectionIssues)
			{
				Debug.LogWarning($"CardCombatGameState: Room {oldRoomId} had no neighbors. Recovered to room {currentRoomId}.");
			}

			RefreshRoomChoices();
			neighbors = ecosystemGenerator.GetNeighborRoomIds(currentRoomId);
		}

		var allowed = false;
		for (var i = 0; i < neighbors.Length; i++)
		{
			if (neighbors[i] == roomId)
			{
				allowed = true;
				break;
			}
		}

		if (!allowed)
		{
			if (logRoomSelectionIssues)
			{
				Debug.LogWarning($"CardCombatGameState: Ignored room selection {roomId}. Current room {currentRoomId} neighbors: [{string.Join(",", neighbors)}]");
			}

			RefreshRoomChoices();
			return;
		}

		currentRoomId = roomId;
		StartSelectedRoom();
	}

	private void StartSelectedRoom()
	{
		var room = GetRoomById(currentRoomId);
		if (room == null)
		{
			phase = RunPhase.MapSelection;
			RefreshRoomChoices();
			return;
		}

		if (IsLootboxRoom(room.id))
		{
			currentEncounter = null;
			pendingDrop = null;
			rewardDropRevealed = false;
			rewardAppliedThisReward = false;
			rewardStatusMessage = string.Empty;
			rewardEligibleCardIndices.Clear();
			lootboxOpenedForCurrentRoom = clearedRooms.Contains(room.id);
			lootboxRoomStatusMessage = lootboxOpenedForCurrentRoom
				? "This lootbox room was already opened."
				: "Open the room lootbox to gain an artifact.";
			phase = RunPhase.LootboxRoom;
			return;
		}

		StartEncounterForCurrentRoom();
	}

	private void OpenCurrentRoomLootbox()
	{
		if (phase != RunPhase.LootboxRoom || lootboxOpenedForCurrentRoom)
		{
			return;
		}

		var room = GetRoomById(currentRoomId);
		if (room == null)
		{
			lootboxRoomStatusMessage = "This room is no longer valid.";
			return;
		}

		if (lootboxTool == null)
		{
			lootboxTool = FindLootboxToolInScene();
		}

		if (lootboxTool == null)
		{
			lootboxRoomStatusMessage = "LootboxTool was not found in the scene.";
			return;
		}

		var depth = Mathf.Max(1, GetRoomDepth(room.id));
		var depthT = Mathf.InverseLerp(1f, Mathf.Max(1f, maxDepthFromStart), depth);
		var tier = depthT > 0.8f ? 3 : (depthT > 0.45f ? 2 : 1);
		var luck = Mathf.Clamp01(0.15f + depthT * 0.55f);

		LootboxTool.ArtifactItem firstArtifact = null;
		var requested = Mathf.Max(1, lootboxRoomArtifactCount);
		for (var i = 0; i < requested; i++)
		{
			var drop = lootboxTool.OpenSingleArtifactLootbox(tier, luck);
			if (firstArtifact == null && drop != null)
			{
				firstArtifact = drop;
			}
		}

		lootboxOpenedForCurrentRoom = true;
		clearedRooms.Add(room.id);
		RefreshArtifactCombatBonuses();

		if (firstArtifact == null)
		{
			lootboxRoomStatusMessage = "The lootbox opened, but no artifact was generated.";
			return;
		}

		lootboxRoomStatusMessage = $"You found {firstArtifact.name} [{firstArtifact.rarity}].";
	}

	private void ContinueAfterLootboxRoom()
	{
		if (phase != RunPhase.LootboxRoom)
		{
			return;
		}

		currentEncounter = null;
		lootboxRoomStatusMessage = string.Empty;
		phase = RunPhase.MapSelection;
		RefreshRoomChoices();
	}

	private void StartEncounterForCurrentRoom()
	{
		var room = GetRoomById(currentRoomId);
		if (room == null)
		{
			phase = RunPhase.MapSelection;
			RefreshRoomChoices();
			return;
		}

		if (string.Equals(room.label, "START", StringComparison.OrdinalIgnoreCase))
		{
			phase = RunPhase.MapSelection;
			RefreshRoomChoices();
			return;
		}

		currentEncounter = BuildEncounter(room);
		if (currentEncounter == null)
		{
			clearedRooms.Add(currentRoomId);
			phase = RunPhase.MapSelection;
			RefreshRoomChoices();
			return;
		}

		ResetEncounterModifiers();
		RefreshArtifactCombatBonuses();
		PrepareDeckForEncounter();
		BeginPlayerTurn();
	}

	private Encounter BuildEncounter(dungeonGenerator.RoomData room)
	{
		if (room == null || room.occupants == null || room.occupants.Count == 0)
		{
			return null;
		}

		var danger = CalculateEncounterDanger(room);
		var isBoss = IsGoalRoom(room);
		if (isBoss)
		{
			danger = Mathf.Max(danger * Mathf.Max(1f, bossDangerMultiplier), highestNonBossDanger + 2f);
		}

		var enemyName = GetEncounterEnemyName(room, isBoss);
		var enemyHp = Mathf.RoundToInt(baseEnemyHp + danger * hpPerDanger);
		var maxDamage = Mathf.Max(maxEnemyDamageFloor, Mathf.RoundToInt(minEnemyDamage + danger * damagePerDanger));

		if (isBoss)
		{
			enemyHp = Mathf.RoundToInt(enemyHp * Mathf.Max(1f, bossHpMultiplier));
			maxDamage = Mathf.RoundToInt(maxDamage * Mathf.Max(1f, bossDamageMultiplier));
		}

		var minDamage = Mathf.Max(1, Mathf.RoundToInt(maxDamage * 0.5f));
		maxDamage = Mathf.Max(minDamage, maxDamage);

		return new Encounter
		{
			roomId = room.id,
			roomLabel = room.label,
			enemyName = enemyName,
			isBoss = isBoss,
			dangerRating = danger,
			maxHp = enemyHp,
			currentHp = enemyHp,
			minDamage = minDamage,
			maxDamage = maxDamage
		};
	}

	private float CalculateRoomDanger(dungeonGenerator.RoomData room)
	{
		if (room == null)
		{
			return 1f;
		}

		var totalDanger = 0f;
		var occupants = room.occupants;
		if (occupants != null)
		{
			for (var i = 0; i < occupants.Count; i++)
			{
				var name = ExtractCreatureName(occupants[i]);
				var count = ExtractCreatureCount(occupants[i]);
				var creatureDanger = ecosystemGenerator.GetCreatureDangerRating(name);
				totalDanger += creatureDanger * Mathf.Max(1, count);
			}
		}

		return Mathf.Max(1f, totalDanger);
	}

	private string GetEncounterEnemyName(dungeonGenerator.RoomData room, bool isBoss)
	{
		var baseName = "Unknown Creature";
		if (room != null && room.occupants != null && room.occupants.Count > 0)
		{
			baseName = ExtractCreatureName(room.occupants[0]);
		}

		if (isBoss)
		{
			return $"{baseName} Boss";
		}

		return baseName;
	}

	private static string ExtractCreatureName(string occupantText)
	{
		if (string.IsNullOrWhiteSpace(occupantText))
		{
			return "Unknown Creature";
		}

		var trimmed = occupantText.Trim();
		var index = trimmed.LastIndexOf(" x", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			return trimmed.Substring(0, index).Trim();
		}

		return trimmed;
	}

	private static int ExtractCreatureCount(string occupantText)
	{
		if (string.IsNullOrWhiteSpace(occupantText))
		{
			return 1;
		}

		var trimmed = occupantText.Trim();
		var index = trimmed.LastIndexOf(" x", StringComparison.OrdinalIgnoreCase);
		if (index < 0)
		{
			return 1;
		}

		var countText = trimmed.Substring(index + 2).Trim();
		if (!int.TryParse(countText, out var count))
		{
			return 1;
		}

		return Mathf.Max(1, count);
	}

	private void BeginPlayerTurn()
	{
		if (currentEncounter == null)
		{
			phase = RunPhase.MapSelection;
			return;
		}

		phase = RunPhase.PlayerTurn;
		playerBlock = 0;
		playerEnergy = Mathf.Max(0, energyPerTurn + fightEnergyPerTurnBonus);
		if (artifactBlockAtTurnStartAmount > 0)
		{
			GainBlock(artifactBlockAtTurnStartAmount);
		}
		DrawCards(Mathf.Max(0, handSize + artifactExtraDrawEachTurn));
		if (currentEncounter != null && currentEncounter.currentHp <= 0)
		{
			currentEncounter.currentHp = 0;
			OnEncounterDefeated();
		}
	}

	private void DrawCards(int count)
	{
		for (var i = 0; i < count; i++)
		{
			if (drawPile.Count == 0)
			{
				ReshuffleDiscardIntoDrawPile();
			}

			if (drawPile.Count == 0)
			{
				return;
			}

			var index = drawPile.Count - 1;
			var card = drawPile[index];
			drawPile.RemoveAt(index);
			hand.Add(card);
			OnCardDrawn();
		}
	}

	private void DrawCardByEffect(CardEffect targetEffect)
	{
		if (TryDrawSpecificCardFromPile(drawPile, targetEffect))
		{
			return;
		}

		ReshuffleDiscardIntoDrawPile();
		TryDrawSpecificCardFromPile(drawPile, targetEffect);
	}

	private bool TryDrawSpecificCardFromPile(List<Card> sourcePile, CardEffect targetEffect)
	{
		for (var i = sourcePile.Count - 1; i >= 0; i--)
		{
			var card = sourcePile[i];
			if (card == null || card.effect != targetEffect)
			{
				continue;
			}

			sourcePile.RemoveAt(i);
			hand.Add(card);
			OnCardDrawn();
			return true;
		}

		return false;
	}

	private void DealDamageToEnemy(int amount)
	{
		if (currentEncounter == null || amount <= 0)
		{
			return;
		}

		var totalDamage = Mathf.Max(0, amount + fightDamageBonus);
		currentEncounter.currentHp -= totalDamage;
	}

	private void GainBlock(int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		var totalBlock = Mathf.Max(0, amount + fightBlockBonus);
		playerBlock += totalBlock;
	}

	private void OnCardDrawn()
	{
		if (artifactBlockOnCardDrawAmount > 0)
		{
			GainBlock(artifactBlockOnCardDrawAmount);
		}

		if (artifactHealOnDrawAmount > 0)
		{
			HealPlayer(artifactHealOnDrawAmount, true);
		}
	}

	private int HealPlayer(int amount, bool triggerArtifactDamageOnHeal)
	{
		var safeAmount = Mathf.Max(0, amount);
		if (safeAmount <= 0)
		{
			return 0;
		}

		var hpBefore = playerHp;
		playerHp = Mathf.Min(playerMaxHp, playerHp + safeAmount);
		var healedAmount = playerHp - hpBefore;

		if (healedAmount > 0 && triggerArtifactDamageOnHeal && artifactDamageOnHealAmount > 0)
		{
			DealDamageToEnemy(artifactDamageOnHealAmount);
		}

		return healedAmount;
	}

	private void RefreshArtifactCombatBonuses()
	{
		artifactDamageCardBonus = 0;
		artifactBlockCardBonus = 0;
		artifactDrawCardsOnBlockCardPlay = 0;
		artifactDrawCardsOnDamageCardPlay = 0;
		artifactHealOnDrawAmount = 0;
		artifactDamageOnHealAmount = 0;
		artifactEnergyOnDamageCardPlay = 0;
		artifactEnergyOnBlockCardPlay = 0;
		artifactHealOnBlockCardPlayAmount = 0;
		artifactDamageOnBlockCardPlayAmount = 0;
		artifactExtraDrawEachTurn = 0;
		artifactBurnOnDamageCardPlay = 0;
		artifactBlockOnCardDrawAmount = 0;
		artifactBlockAtTurnStartAmount = 0;

		if (lootboxTool == null)
		{
			lootboxTool = FindLootboxToolInScene();
		}

		if (lootboxTool == null)
		{
			return;
		}

		var artifactBonuses = lootboxTool.GetActiveArtifactBonuses();
		artifactDamageCardBonus = artifactBonuses.damageCardBonus;
		artifactBlockCardBonus = artifactBonuses.blockCardBonus;
		artifactDrawCardsOnBlockCardPlay = artifactBonuses.drawOnBlockCardPlay;
		artifactDrawCardsOnDamageCardPlay = artifactBonuses.drawOnDamageCardPlay;
		artifactHealOnDrawAmount = artifactBonuses.healOnCardDrawTriggers * 3;
		artifactDamageOnHealAmount = artifactBonuses.damageOnHealTriggers * 3;
		artifactEnergyOnDamageCardPlay = artifactBonuses.energyOnDamageCardPlay;
		artifactEnergyOnBlockCardPlay = artifactBonuses.energyOnBlockCardPlay;
		artifactHealOnBlockCardPlayAmount = artifactBonuses.healOnBlockCardPlayTriggers * 2;
		artifactDamageOnBlockCardPlayAmount = artifactBonuses.damageOnBlockCardPlayTriggers * 2;
		artifactExtraDrawEachTurn = artifactBonuses.extraDrawEachTurn;
		artifactBurnOnDamageCardPlay = artifactBonuses.burnOnDamageCardPlay;
		artifactBlockOnCardDrawAmount = artifactBonuses.blockOnCardDraw;
		artifactBlockAtTurnStartAmount = artifactBonuses.blockEachTurnStart * 2;
	}

	private void ReshuffleDiscardIntoDrawPile()
	{
		if (discardPile.Count == 0)
		{
			return;
		}

		// Recycle discarded cards when the draw pile runs out.
		drawPile.AddRange(discardPile);
		discardPile.Clear();
		Shuffle(drawPile);
	}

	private static void Shuffle(List<Card> cards)
	{
		for (var i = cards.Count - 1; i > 0; i--)
		{
			var j = UnityEngine.Random.Range(0, i + 1);
			var tmp = cards[i];
			cards[i] = cards[j];
			cards[j] = tmp;
		}
	}

	public void PlayCard(int handIndex)
	{
		if (phase != RunPhase.PlayerTurn || currentEncounter == null)
		{
			return;
		}

		if (handIndex < 0 || handIndex >= hand.Count)
		{
			return;
		}

		var card = hand[handIndex];
		var effectiveCost = GetEffectiveCardCost(card);
		if (playerEnergy < effectiveCost)
		{
			return;
		}

		playerEnergy -= effectiveCost;
		ResolveCardWithEnhancement(card);

		hand.RemoveAt(handIndex);
		discardPile.Add(card);

		if (currentEncounter.currentHp <= 0)
		{
			currentEncounter.currentHp = 0;
			OnEncounterDefeated();
		}
	}

	private void ResolveCardWithEnhancement(Card card)
	{
		var repeats = card.enhancement == CardEnhancement.EchoShell ? 2 : 1;
		for (var i = 0; i < repeats; i++)
		{
			ResolveCardEffect(card);
			ApplyEnhancementBonus(card);
		}
	}

	private void ResolveCardEffect(Card card)
	{
		switch (card.effect)
		{
			case CardEffect.Damage:
				DealDamageToEnemy(card.value + artifactDamageCardBonus);
				if (artifactBurnOnDamageCardPlay > 0)
				{
					enemyBurnDamagePerTurn += artifactBurnOnDamageCardPlay;
					enemyBurnTurnsRemaining += artifactBurnOnDamageCardPlay;
				}
				if (artifactDrawCardsOnDamageCardPlay > 0)
				{
					DrawCards(artifactDrawCardsOnDamageCardPlay);
				}
				if (artifactEnergyOnDamageCardPlay > 0)
				{
					playerEnergy += artifactEnergyOnDamageCardPlay;
				}
				break;
			case CardEffect.Block:
				GainBlock(card.value + artifactBlockCardBonus);
				if (artifactDrawCardsOnBlockCardPlay > 0)
				{
					DrawCards(artifactDrawCardsOnBlockCardPlay);
				}
				if (artifactEnergyOnBlockCardPlay > 0)
				{
					playerEnergy += artifactEnergyOnBlockCardPlay;
				}
				if (artifactHealOnBlockCardPlayAmount > 0)
				{
					HealPlayer(artifactHealOnBlockCardPlayAmount, true);
				}
				if (artifactDamageOnBlockCardPlayAmount > 0)
				{
					DealDamageToEnemy(artifactDamageOnBlockCardPlayAmount);
				}
				break;
			case CardEffect.Heal:
				HealPlayer(card.value, true);
				break;
			case CardEffect.Draw:
				DrawCards(card.value);
				break;
			case CardEffect.GainEnergy:
				playerEnergy += card.value;
				break;
			default:
				break;
		}
	}

	private void ApplyEnhancementBonus(Card card)
	{
		switch (card.enhancement)
		{
			case CardEnhancement.HardenedCarapace:
				GainBlock(4);
				break;
			case CardEnhancement.KeenInstinct:
				DrawCards(1);
				break;
			case CardEnhancement.VenomEdge:
				DealDamageToEnemy(4);
				break;
			case CardEnhancement.AdrenalGland:
				playerEnergy += 1;
				break;
			case CardEnhancement.RegenerativeSap:
				HealPlayer(3, true);
				break;
			case CardEnhancement.BurningEmber:
				enemyBurnDamagePerTurn += 3;
				enemyBurnTurnsRemaining += 3;
				break;
			case CardEnhancement.PredatorInstinct:
				DrawCardByEffect(CardEffect.Damage);
				break;
			case CardEnhancement.AnimalBrain:
				DrawCardByEffect(CardEffect.Block);
				break;
			case CardEnhancement.PredatoryMomentum:
				fightDamageBonus += 1;
				break;
			case CardEnhancement.RhythmicHeartbeat:
				fightBlockBonus += 1;
				break;
			case CardEnhancement.MetabolicSurge:
				fightEnergyPerTurnBonus += 1;
				break;
			default:
				break;
		}
	}

	public void EndPlayerTurn()
	{
		if (phase != RunPhase.PlayerTurn || currentEncounter == null)
		{
			return;
		}

		DiscardHand();
		phase = RunPhase.EnemyTurn;
		ResolveEnemyAction();

		if (phase == RunPhase.Defeat || phase == RunPhase.RewardSelection || phase == RunPhase.RunWon)
		{
			return;
		}

		BeginPlayerTurn();
	}

	private void ResolveEnemyAction()
	{
		if (currentEncounter == null)
		{
			return;
		}

		if (ApplyEnemyStartOfTurnEffects())
		{
			lastEnemyRoll = 0;
			return;
		}

		lastEnemyRoll = UnityEngine.Random.Range(currentEncounter.minDamage, currentEncounter.maxDamage + 1);
		var damageAfterBlock = Mathf.Max(0, lastEnemyRoll - playerBlock);
		playerHp -= damageAfterBlock;
		if (playerHp <= 0)
		{
			playerHp = 0;
			phase = RunPhase.Defeat;
		}
	}

	private bool ApplyEnemyStartOfTurnEffects()
	{
		if (currentEncounter == null)
		{
			return false;
		}

		if (enemyBurnTurnsRemaining > 0 && enemyBurnDamagePerTurn > 0)
		{
			DealDamageToEnemy(enemyBurnDamagePerTurn);
			enemyBurnTurnsRemaining = Mathf.Max(0, enemyBurnTurnsRemaining - 1);
		}

		if (currentEncounter.currentHp > 0)
		{
			return false;
		}

		currentEncounter.currentHp = 0;
		OnEncounterDefeated();
		return true;
	}

	private void ResetEncounterModifiers()
	{
		fightDamageBonus = 0;
		fightBlockBonus = 0;
		fightEnergyPerTurnBonus = 0;
		enemyBurnDamagePerTurn = 0;
		enemyBurnTurnsRemaining = 0;
	}

	private void DiscardHand()
	{
		for (var i = 0; i < hand.Count; i++)
		{
			discardPile.Add(hand[i]);
		}

		hand.Clear();
	}

	private void OnEncounterDefeated()
	{
		if (grantLootboxOnEncounterDefeat)
		{
			GrantEncounterLootbox();
		}
		DiscardHand();
		BeginRewardSelection();
	}

	private void GrantEncounterLootbox()
	{
		if (currentEncounter == null)
		{
			return;
		}

		if (lootboxTool == null)
		{
			lootboxTool = FindLootboxToolInScene();
		}

		if (lootboxTool == null)
		{
			return;
		}

		var boxTier = currentEncounter.isBoss ? 3 : 2;
		var luck = Mathf.Clamp01((currentEncounter.dangerRating - 1f) * 0.15f);
		lootboxTool.OpenSingleArtifactLootbox(boxTier, luck);
		RefreshArtifactCombatBonuses();
	}

	public void ContinueAfterEncounter()
	{
		if (phase != RunPhase.RewardSelection || currentEncounter == null)
		{
			return;
		}

		clearedRooms.Add(currentEncounter.roomId);
		if (currentEncounter.isBoss)
		{
			currentEncounter = null;
			phase = RunPhase.RunWon;
			return;
		}

		phase = RunPhase.MapSelection;
		currentEncounter = null;
		pendingDrop = null;
		rewardDropRevealed = false;
		rewardAppliedThisReward = false;
		rewardStatusMessage = string.Empty;
		rewardCardListScroll = Vector2.zero;
		rewardEligibleCardIndices.Clear();
		RefreshRoomChoices();
	}

	private dungeonGenerator.RoomData GetRoomById(int roomId)
	{
		if (ecosystemGenerator == null || roomId < 0)
		{
			return null;
		}

		var rooms = ecosystemGenerator.GetGeneratedRooms();
		if (rooms == null || rooms.Count == 0)
		{
			return null;
		}

		for (var i = 0; i < rooms.Count; i++)
		{
			if (rooms[i] != null && rooms[i].id == roomId)
			{
				return rooms[i];
			}
		}

		return null;
	}

	private static bool IsGoalRoom(dungeonGenerator.RoomData room)
	{
		return room != null && string.Equals(room.label, "GOAL", StringComparison.OrdinalIgnoreCase);
	}

	private void OnGUI()
	{
		if (!useDebugOnGui)
		{
			return;
		}

		DrawRunHeader();

		if (phase == RunPhase.MapSelection)
		{
			DrawMapSelection();
		}
		else if (phase == RunPhase.LootboxRoom)
		{
			DrawLootboxRoom();
		}
		else if (phase == RunPhase.PlayerTurn || phase == RunPhase.EnemyTurn)
		{
			DrawCombat();
		}
		else if (phase == RunPhase.RewardSelection)
		{
			DrawRewardSelection();
		}
		else if (phase == RunPhase.RunWon)
		{
			GUI.Box(new Rect(16f, 86f, 520f, 80f), "You defeated the boss and cleared the run.");
			if (GUI.Button(new Rect(24f, 120f, 200f, 32f), "Start New Run"))
			{
				InitializeRun();
			}
		}
		else if (phase == RunPhase.Defeat)
		{
			GUI.Box(new Rect(16f, 86f, 520f, 80f), "You were defeated.");
			if (GUI.Button(new Rect(24f, 120f, 200f, 32f), "Retry Run"))
			{
				InitializeRun();
			}
		}
	}

	private void DrawRunHeader()
	{
		var roomLabel = "Unknown";
		var room = GetRoomById(currentRoomId);
		if (room != null)
		{
			roomLabel = room.label;
		}

		var artifactCount = lootboxTool != null ? lootboxTool.GetArtifactCount() : 0;

		var text = $"State: {phase}  |  Room: {roomLabel}  |  HP: {playerHp}/{playerMaxHp}  |  Block: {playerBlock}  |  Energy: {playerEnergy}  |  Artifacts: {artifactCount}";
		GUI.Box(new Rect(16f, 16f, 1040f, 60f), text);
	}

	private void DrawMapSelection()
	{
		EnsureNavigationStateIsValid();
		if (currentRoomId < 0)
		{
			GUI.Box(new Rect(16f, 86f, 520f, 70f), "No map data is available. Generate a map to continue.");
			if (ecosystemGenerator != null && GUI.Button(new Rect(16f, 164f, 220f, 34f), "Generate / Refresh Map"))
			{
				ecosystemGenerator.GenerateEcosystem();
				InitializeRun();
			}
			return;
		}

		RefreshRoomChoices();

		GUI.Box(new Rect(16f, 86f, 520f, 60f), "Map: Choose a connected room.");

		var y = 154f;
		if (availableRoomChoices.Count == 0)
		{
			GUI.Box(new Rect(16f, y, 520f, 32f), "No available connected rooms.");
			return;
		}

		for (var i = 0; i < availableRoomChoices.Count; i++)
		{
			var room = GetRoomById(availableRoomChoices[i]);
			if (room == null)
			{
				continue;
			}

			var isLootbox = IsLootboxRoom(room.id);
			var bossTag = IsGoalRoom(room) ? " (BOSS)" : string.Empty;
			var lootboxTag = isLootbox ? " (LOOTBOX)" : string.Empty;
			var clearedTag = clearedRooms.Contains(room.id) ? " [Cleared]" : string.Empty;
			var projectedDanger = CalculateEncounterDanger(room);
			var roomDetail = isLootbox ? "Artifact Room" : $"Danger {projectedDanger:F1}";
			var roomColor = GetRoomDangerButtonColor(room);
			var previousBackgroundColor = GUI.backgroundColor;
			var previousContentColor = GUI.contentColor;
			GUI.backgroundColor = roomColor;
			GUI.contentColor = GetReadableTextColor(roomColor);
			var buttonText = $"Enter {room.label}{bossTag}{lootboxTag}{clearedTag}  {roomDetail}";
			if (GUI.Button(new Rect(16f, y, 520f, 32f), buttonText))
			{
				ChooseRoom(room.id);
			}
			GUI.backgroundColor = previousBackgroundColor;
			GUI.contentColor = previousContentColor;

			y += 38f;
		}
	}

	private void DrawLootboxRoom()
	{
		var room = GetRoomById(currentRoomId);
		if (room == null)
		{
			GUI.Box(new Rect(16f, 86f, 520f, 60f), "No active lootbox room.");
			if (GUI.Button(new Rect(16f, 154f, 220f, 34f), "Back To Map"))
			{
				ContinueAfterLootboxRoom();
			}
			return;
		}

		GUI.Box(new Rect(16f, 86f, 1040f, 84f), $"Lootbox Room: {room.label}\nOpen the room lootbox to gain artifact rewards.");
		GUI.Box(new Rect(16f, 176f, 1040f, 52f), lootboxRoomStatusMessage);

		if (!lootboxOpenedForCurrentRoom)
		{
			if (GUI.Button(new Rect(16f, 236f, 240f, 34f), "Open Room Lootbox"))
			{
				OpenCurrentRoomLootbox();
			}
		}

		if (lootboxOpenedForCurrentRoom)
		{
			if (GUI.Button(new Rect(16f, 278f, 240f, 34f), "Continue"))
			{
				ContinueAfterLootboxRoom();
			}
		}
	}

	private void DrawRewardSelection()
	{
		if (currentEncounter == null)
		{
			GUI.Box(new Rect(16f, 86f, 520f, 60f), "No active reward.");
			if (GUI.Button(new Rect(16f, 154f, 220f, 34f), "Continue"))
			{
				ContinueAfterEncounter();
			}
			return;
		}

		GUI.Box(new Rect(16f, 86f, 1040f, 72f), $"{currentEncounter.enemyName} dropped a card enhancement.");

		if (!rewardDropRevealed)
		{
			if (GUI.Button(new Rect(16f, 166f, 280f, 34f), "Reveal Creature Drop"))
			{
				rewardDropRevealed = true;
				BuildRewardEligibleCardIndices();
			}

			if (GUI.Button(new Rect(304f, 166f, 220f, 34f), "Skip Reward"))
			{
				rewardStatusMessage = "Reward skipped.";
				rewardAppliedThisReward = true;
			}
		}

		if (!rewardDropRevealed)
		{
			if (rewardAppliedThisReward && GUI.Button(new Rect(16f, 208f, 220f, 34f), "Continue"))
			{
				ContinueAfterEncounter();
			}
			return;
		}

		if (pendingDrop == null)
		{
			GUI.Box(new Rect(16f, 208f, 1040f, 42f), "No valid drop was generated.");
			if (GUI.Button(new Rect(16f, 258f, 220f, 34f), "Continue"))
			{
				ContinueAfterEncounter();
			}
			return;
		}

		GUI.Box(new Rect(16f, 208f, 1040f, 64f), $"Drop: {pendingDrop.name}\nEffect: {pendingDrop.description}");

		if (rewardAppliedThisReward)
		{
			GUI.Box(new Rect(16f, 278f, 1040f, 36f), rewardStatusMessage);
			if (GUI.Button(new Rect(16f, 322f, 220f, 34f), "Continue"))
			{
				ContinueAfterEncounter();
			}
			return;
		}

		BuildRewardEligibleCardIndices();
		if (rewardEligibleCardIndices.Count == 0)
		{
			GUI.Box(new Rect(16f, 278f, 1040f, 36f), "All cards already have an enhancement. No valid card targets remain.");
			rewardAppliedThisReward = true;
			rewardStatusMessage = "No eligible cards. Reward converted to nothing.";
			if (GUI.Button(new Rect(16f, 322f, 220f, 34f), "Continue"))
			{
				ContinueAfterEncounter();
			}
			return;
		}

		GUI.Box(new Rect(16f, 278f, 1040f, 36f), "Choose a card without an enhancement (scroll to see all cards):");

		var listRect = new Rect(16f, 322f, 1040f, 360f);
		var rowHeight = 36f;
		var viewHeight = Mathf.Max(listRect.height - 8f, rewardEligibleCardIndices.Count * rowHeight + 8f);
		var viewRect = new Rect(0f, 0f, listRect.width - 20f, viewHeight);
		var selectedDeckIndex = -1;

		rewardCardListScroll = GUI.BeginScrollView(listRect, rewardCardListScroll, viewRect);
		for (var i = 0; i < rewardEligibleCardIndices.Count; i++)
		{
			var deckIndex = rewardEligibleCardIndices[i];
			if (deckIndex < 0 || deckIndex >= deckTemplate.Count)
			{
				continue;
			}

			var card = deckTemplate[deckIndex];
			if (card == null)
			{
				continue;
			}

			var y = 4f + i * rowHeight;
			var buttonText = $"Apply to #{deckIndex + 1}: {card.title} | Cost {GetEffectiveCardCost(card)}";
			if (GUI.Button(new Rect(4f, y, viewRect.width - 8f, 32f), buttonText))
			{
				selectedDeckIndex = deckIndex;
			}
		}
		GUI.EndScrollView();

		if (selectedDeckIndex >= 0)
		{
			TryApplyDropToCard(selectedDeckIndex);
		}
	}

	private void DrawCombat()
	{
		if (currentEncounter == null)
		{
			GUI.Box(new Rect(16f, 86f, 520f, 40f), "No active encounter.");
			return;
		}

		var enemyLabel = currentEncounter.isBoss ? "Boss" : "Enemy";
		GUI.Box(
			new Rect(16f, 86f, 520f, 84f),
			$"{enemyLabel}: {currentEncounter.enemyName}\nHP: {currentEncounter.currentHp}/{currentEncounter.maxHp}\nDamage: {currentEncounter.minDamage}-{currentEncounter.maxDamage} (Danger {currentEncounter.dangerRating:F1})");

		GUI.Box(new Rect(16f, 176f, 520f, 40f), $"Draw: {drawPile.Count}  Discard: {discardPile.Count}  Last enemy hit: {lastEnemyRoll}");

		var handTitle = "Hand (play cards and enhanced effects):";
		GUI.Box(new Rect(16f, 222f, 1040f, 228f), handTitle);

		var x = 24f;
		var y = 254f;
		for (var i = 0; i < hand.Count; i++)
		{
			var card = hand[i];
			var cardRect = new Rect(x, y, 150f, 64f);
			var effectiveCost = GetEffectiveCardCost(card);
			var canPlay = phase == RunPhase.PlayerTurn && playerEnergy >= effectiveCost;
			GUI.enabled = canPlay;
			if (GUI.Button(cardRect, GetCardDisplayText(card)))
			{
				PlayCard(i);
				break;
			}
			GUI.enabled = true;

			x += 158f;
			if (x > 900f)
			{
				x = 24f;
				y += 72f;
			}
		}

		if (phase == RunPhase.PlayerTurn)
		{
			if (GUI.Button(new Rect(16f, 458f, 220f, 34f), "End Turn"))
			{
				EndPlayerTurn();
			}
		}
	}
}