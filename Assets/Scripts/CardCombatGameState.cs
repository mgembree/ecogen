using System;
using System.Collections;
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

	[Header("Difficulty")]
	[SerializeField, Range(1f, 3f)] private float enemyHpDifficultyMultiplier = 1.4f;
	[SerializeField, Range(1f, 3f)] private float enemyDamageDifficultyMultiplier = 1.45f;
	[SerializeField, Min(0)] private int flatEnemyHpDifficultyBonus = 6;
	[SerializeField, Min(0)] private int flatEnemyDamageDifficultyBonus = 2;
	[SerializeField, Range(0.5f, 0.95f)] private float enemyMinDamageRatio = 0.65f;

	[Header("Enemy AI")]
	[SerializeField, Range(0f, 1f)] private float enemyActionAttackWeight = 0.55f;
	[SerializeField, Range(0f, 1f)] private float enemyActionDefendWeight = 0.25f;
	[SerializeField, Range(0f, 1f)] private float enemyActionBuffWeight = 0.2f;
	[SerializeField, Min(0)] private int enemyDefendBaseBlock = 4;
	[SerializeField, Min(0)] private int enemyBuffDamagePerStack = 1;
	[SerializeField, Min(1)] private int enemyMaxBuffStacks = 6;

	[Header("Status Effects")]
	[SerializeField, Range(1f, 3f)] private float enemyVulnerableDamageMultiplier = 1.5f;

	[Header("Danger Progression")]
	[SerializeField] private float dangerPerDepthStep = 1.45f;
	[SerializeField] private float occupantDangerVarianceMax = 0.65f;

	[Header("Early Encounter Ramp")]
	[SerializeField, Min(0)] private int earlyEncounterRampCount = 6;
	[SerializeField] private float earlyEncounterHpRamp = 3f;
	[SerializeField] private float earlyEncounterDamageRamp = 0.9f;

	[Header("Danger Damage Scaling")]
	[SerializeField, Min(0f)] private float extraEnemyDamagePerDangerStep = 0.45f;

	[Header("Quick UI")]
	[SerializeField] private bool useDebugOnGui = true;
	[SerializeField] private bool logRoomSelectionIssues = true;
	[SerializeField] private bool colorCodeRoomButtonsByDanger = true;
	[SerializeField] private Color lowDangerButtonColor = new Color(0.5f, 0.85f, 0.45f, 1f);
	[SerializeField] private Color mediumDangerButtonColor = new Color(0.93f, 0.82f, 0.35f, 1f);
	[SerializeField] private Color highDangerButtonColor = new Color(0.9f, 0.38f, 0.3f, 1f);

	[Header("Map Highlight")]
	[SerializeField] private bool highlightCurrentRoomOnMap = true;
	[SerializeField] private Color currentRoomMapTintColor = new Color(0.2f, 0.9f, 0.2f, 1f);

	[Header("Lootbox Rooms")]
	[SerializeField, Range(0f, 1f)] private float lootboxRoomChance = 0.2f;
	[SerializeField, Min(0)] private int minLootboxRooms = 1;
	[SerializeField, Min(0)] private int maxLootboxRooms = 2;
	[SerializeField] private bool grantLootboxOnEncounterDefeat = false;

	[Header("Combat Presentation")]
	[SerializeField, Range(0f, 0.95f)] private float combatBackgroundDimAlpha = 0.72f;
	[SerializeField, Range(0.05f, 0.8f)] private float hitFlashDuration = 0.28f;
	[SerializeField, Range(0.1f, 0.8f)] private float hitFlashAlpha = 0.45f;
	[SerializeField] private Texture2D globalBackgroundTexture;
	[SerializeField] private Texture2D lootboxRoomBackgroundTexture;
	[SerializeField] private Texture2D combatRoomBackgroundTexture;

	[Header("Title Screen")]
	[SerializeField] private string gameTitle = "Ecohunter";
	[SerializeField] private string gameSubtitle = "Build your deck, harvest creatures and relics, and survive a living dungeon.";

	[Header("Audio")]
	[SerializeField] private AudioSource musicAudioSource;
	[SerializeField] private AudioClip gameplayMusic;
	[SerializeField, Range(0f, 1f)] private float gameplayMusicVolume = 0.65f;
	[SerializeField, Min(0f)] private float gameplayMusicFadeInSeconds = 2f;
	[SerializeField] private AudioSource sfxAudioSource;
	[SerializeField] private AudioClip attackSoundEffect;
	[SerializeField, Range(0f, 1f)] private float attackSoundEffectVolume = 0.9f;
	[SerializeField] private AudioClip blockSoundEffect;
	[SerializeField, Range(0f, 1f)] private float blockSoundEffectVolume = 0.85f;
	[SerializeField] private AudioClip drawCardSoundEffect;
	[SerializeField, Range(0f, 1f)] private float drawCardSoundEffectVolume = 0.8f;
	[SerializeField] private AudioClip victorySoundEffect;
	[SerializeField, Range(0f, 1f)] private float victorySoundEffectVolume = 0.95f;

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
	private int turnDamageModifier;
	private int enemyBurnDamagePerTurn;
	private int enemyBurnTurnsRemaining;
	private int enemyVulnerableTurnsRemaining;
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
	private int artifactHealAtTurnStartAmount;
	private int artifactDamageAtTurnStartAmount;
	private int artifactBlockOnDamageCardPlayAmount;
	private int artifactEnergyEachTurnStartBonus;
	private int artifactBurnOnBlockCardPlay;
	private int artifactDrawOnHealAmount;
	private int artifactVulnerableOnDamageCardPlay;
	private int artifactVulnerableAtTurnStartAmount;
	private int artifactBonusDamageVsVulnerable;
	private int artifactHealOnDamageCardPlayAmount;
	private int artifactBlockOnHealAmount;
	private int artifactDrawOnGainEnergyCardPlay;
	private int artifactBurnDamageBonus;
	private int enemyLastDamageTaken;
	private int playerLastDamageTaken;
	private float enemyHitFlashEndTime;
	private float playerHitFlashEndTime;
	private GUIStyle cardTooltipStyle;
	private Texture2D generatedCaveFallbackTexture;
	private Coroutine musicFadeCoroutine;
	private bool gameplayMusicStarted;

	private enum RunPhase
	{
		TitleScreen,
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
		MetabolicSurge,
		BloodrootSap,
		BastionBloom,
		EmberCircuit,
		WildOverclock,
		PredatorsMark,
		LeechingVine,
		ThornguardPulse,
		ForesightBurst,
		RuinSpores,
		IronBastion,
		OverclockedReflex,
		Plaguebrand,
		BloodFrenzy,
		BoneWard,
		SerratedBloom,
		AdaptiveGuard,
		PredatoryFocus,
		VitalSurge,
		FinisherClaw,
		FortressDebt,
		HemorrhageBurst,
		OverdrawLoop,
		ShatterLunge,
		RecoveryCrash
	}

	private enum EnemyActionType
	{
		Attack,
		Defend,
		Buff
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
		public int currentBlock;
		public int damageBuff;
		public string lastActionSummary;
		public int activeEnemyIndex;
		public int totalEnemyCount;
		public List<EncounterEnemyState> enemies = new List<EncounterEnemyState>();
	}

	private class EncounterEnemyState
	{
		public string enemyName;
		public int maxHp;
		public int currentHp;
		public int minDamage;
		public int maxDamage;
		public int currentBlock;
		public int damageBuff;
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
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.BloodrootSap,
			name = "Bloodroot Sap",
			description = "Heal 2 HP when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.BastionBloom,
			name = "Bastion Bloom",
			description = "Gain 3 block when this card is played, or 5 block if this is a block card."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.EmberCircuit,
			name = "Ember Circuit",
			description = "Apply Burn 1 and draw 1 card when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.WildOverclock,
			name = "Wild Overclock",
			description = "Gain 1 energy and +1 damage for this fight when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.PredatorsMark,
			name = "Predator's Mark",
			description = "Apply Vulnerable 2 to the enemy when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.LeechingVine,
			name = "Leeching Vine",
			description = "Deal 2 damage and heal 2 HP when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.ThornguardPulse,
			name = "Thornguard Pulse",
			description = "Gain 4 block and draw 1 card when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.ForesightBurst,
			name = "Foresight Burst",
			description = "Draw 2 cards when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.RuinSpores,
			name = "Ruin Spores",
			description = "If the enemy is vulnerable, deal 5 damage. Otherwise apply Vulnerable 1."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.IronBastion,
			name = "Iron Bastion",
			description = "Gain 6 block and 1 energy when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.OverclockedReflex,
			name = "Overclocked Reflex",
			description = "Draw 1 card and gain 1 energy when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.Plaguebrand,
			name = "Plaguebrand",
			description = "Apply Burn 2 and Vulnerable 1 when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.BloodFrenzy,
			name = "Blood Frenzy",
			description = "Deal 6 damage if enemy HP is 50% or less, otherwise deal 3 damage."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.BoneWard,
			name = "Bone Ward",
			description = "Gain 8 block. If enemy is vulnerable, draw 1 card."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.SerratedBloom,
			name = "Serrated Bloom",
			description = "Deal 3 damage and apply Burn 1 when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.AdaptiveGuard,
			name = "Adaptive Guard",
			description = "Gain 4 block when this card is played, or 8 block if the enemy is vulnerable."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.PredatoryFocus,
			name = "Predatory Focus",
			description = "Apply Vulnerable 1 and draw 1 card when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.VitalSurge,
			name = "Vital Surge",
			description = "Heal 2 HP and gain 1 energy when this card is played."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.FinisherClaw,
			name = "Finisher Claw",
			description = "Deal 7 damage if enemy HP is 40% or less, otherwise deal 3 damage."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.FortressDebt,
			name = "Fortress Debt",
			description = "Gain 25 block, but your damage is reduced by 4 for the rest of this turn."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.HemorrhageBurst,
			name = "Hemorrhage Burst",
			description = "Deal 14 damage and gain 1 energy, then lose 4 HP."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.OverdrawLoop,
			name = "Overdraw Loop",
			description = "Draw 3 cards, then lose 1 energy."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.ShatterLunge,
			name = "Shatter Lunge",
			description = "Deal 16 damage, but the enemy gains 5 block."
		},
		new RewardDrop
		{
			enhancement = CardEnhancement.RecoveryCrash,
			name = "Recovery Crash",
			description = "Heal 8 HP, then lose 6 block."
		}
	};

	private void Start()
	{
		phase = RunPhase.TitleScreen;
		playerHp = playerMaxHp;
		playerBlock = 0;
		playerEnergy = 0;

		if (lootboxTool == null)
		{
			lootboxTool = FindLootboxToolInScene();
		}

		if (ecosystemGenerator == null)
		{
			ecosystemGenerator = FindGeneratorInScene();
		}
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

	private AudioSource EnsureMusicSource()
	{
		if (musicAudioSource == null)
		{
			musicAudioSource = GetComponent<AudioSource>();
		}

		if (musicAudioSource == null)
		{
			musicAudioSource = gameObject.AddComponent<AudioSource>();
		}

		musicAudioSource.playOnAwake = false;
		musicAudioSource.loop = true;
		musicAudioSource.spatialBlend = 0f;
		return musicAudioSource;
	}

	private AudioSource EnsureSfxSource()
	{
		if (sfxAudioSource == null)
		{
			sfxAudioSource = gameObject.AddComponent<AudioSource>();
		}

		sfxAudioSource.playOnAwake = false;
		sfxAudioSource.loop = false;
		sfxAudioSource.spatialBlend = 0f;
		return sfxAudioSource;
	}

	private void PlaySfxOneShot(AudioClip clip, float volume)
	{
		if (clip == null)
		{
			return;
		}

		var source = EnsureSfxSource();
		if (source == null)
		{
			return;
		}

		source.PlayOneShot(clip, Mathf.Clamp01(volume));
	}

	private void PlayAttackSoundEffect()
	{
		PlaySfxOneShot(attackSoundEffect, attackSoundEffectVolume);
	}

	private void PlayBlockSoundEffect()
	{
		PlaySfxOneShot(blockSoundEffect, blockSoundEffectVolume);
	}

	private void PlayDrawCardSoundEffect()
	{
		PlaySfxOneShot(drawCardSoundEffect, drawCardSoundEffectVolume);
	}

	private void PlayVictorySoundEffect()
	{
		PlaySfxOneShot(victorySoundEffect, victorySoundEffectVolume);
	}

	private void StartGameplayMusicWithFadeIn()
	{
		if (gameplayMusicStarted)
		{
			return;
		}

		var source = EnsureMusicSource();
		if (source == null)
		{
			return;
		}

		if (gameplayMusic == null)
		{
			Debug.LogWarning("CardCombatGameState: No gameplay music clip is assigned.");
			return;
		}

		source.clip = gameplayMusic;
		source.volume = 0f;
		source.Play();

		if (musicFadeCoroutine != null)
		{
			StopCoroutine(musicFadeCoroutine);
		}

		musicFadeCoroutine = StartCoroutine(FadeInMusicRoutine(source, Mathf.Max(0f, gameplayMusicFadeInSeconds), Mathf.Clamp01(gameplayMusicVolume)));
		gameplayMusicStarted = true;
	}

	private IEnumerator FadeInMusicRoutine(AudioSource source, float durationSeconds, float targetVolume)
	{
		if (source == null)
		{
			yield break;
		}

		if (durationSeconds <= 0f)
		{
			source.volume = targetVolume;
			musicFadeCoroutine = null;
			yield break;
		}

		var elapsed = 0f;
		while (elapsed < durationSeconds)
		{
			if (source == null)
			{
				musicFadeCoroutine = null;
				yield break;
			}

			elapsed += Time.unscaledDeltaTime;
			var t = Mathf.Clamp01(elapsed / durationSeconds);
			source.volume = Mathf.Lerp(0f, targetVolume, t);
			yield return null;
		}

		source.volume = targetVolume;
		musicFadeCoroutine = null;
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
			RefreshCurrentRoomMapHighlight();
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
		ResetCombatFeedbackState();
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
		RefreshCurrentRoomMapHighlight();
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

		var varianceCap = Mathf.Clamp(occupantDangerVarianceMax, 0f, Mathf.Max(0.25f, depthStep) * 1.25f);
		var depthDanger = 1f + nonStartDepth * depthStep;
		var occupantDanger = Mathf.Max(1f, CalculateRoomDanger(room));
		var occupantLog = Mathf.Log(1f + occupantDanger, 2f);
		var normalizedOccupantDanger = Mathf.InverseLerp(1f, 6f, occupantLog);

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
			case CardEnhancement.BloodrootSap:
				return "Bloodroot Sap";
			case CardEnhancement.BastionBloom:
				return "Bastion Bloom";
			case CardEnhancement.EmberCircuit:
				return "Ember Circuit";
			case CardEnhancement.WildOverclock:
				return "Wild Overclock";
			case CardEnhancement.PredatorsMark:
				return "Predator's Mark";
			case CardEnhancement.LeechingVine:
				return "Leeching Vine";
			case CardEnhancement.ThornguardPulse:
				return "Thornguard Pulse";
			case CardEnhancement.ForesightBurst:
				return "Foresight Burst";
			case CardEnhancement.RuinSpores:
				return "Ruin Spores";
			case CardEnhancement.IronBastion:
				return "Iron Bastion";
			case CardEnhancement.OverclockedReflex:
				return "Overclocked Reflex";
			case CardEnhancement.Plaguebrand:
				return "Plaguebrand";
			case CardEnhancement.BloodFrenzy:
				return "Blood Frenzy";
			case CardEnhancement.BoneWard:
				return "Bone Ward";
			case CardEnhancement.SerratedBloom:
				return "Serrated Bloom";
			case CardEnhancement.AdaptiveGuard:
				return "Adaptive Guard";
			case CardEnhancement.PredatoryFocus:
				return "Predatory Focus";
			case CardEnhancement.VitalSurge:
				return "Vital Surge";
			case CardEnhancement.FinisherClaw:
				return "Finisher Claw";
			case CardEnhancement.FortressDebt:
				return "Fortress Debt";
			case CardEnhancement.HemorrhageBurst:
				return "Hemorrhage Burst";
			case CardEnhancement.OverdrawLoop:
				return "Overdraw Loop";
			case CardEnhancement.ShatterLunge:
				return "Shatter Lunge";
			case CardEnhancement.RecoveryCrash:
				return "Recovery Crash";
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

	private static string GetCardEffectDescription(Card card)
	{
		if (card == null)
		{
			return "No effect.";
		}

		switch (card.effect)
		{
			case CardEffect.Damage:
				return $"Deal {card.value} damage.";
			case CardEffect.Block:
				return $"Gain {card.value} block.";
			case CardEffect.Heal:
				return $"Heal {card.value} HP.";
			case CardEffect.Draw:
				return $"Draw {card.value} card(s).";
			case CardEffect.GainEnergy:
				return $"Gain {card.value} energy.";
			default:
				return "No effect.";
		}
	}

	private static string GetEnhancementDescription(CardEnhancement enhancement)
	{
		if (enhancement == CardEnhancement.None)
		{
			return "No enhancement.";
		}

		for (var i = 0; i < PossibleDrops.Length; i++)
		{
			var drop = PossibleDrops[i];
			if (drop != null && drop.enhancement == enhancement)
			{
				return drop.description;
			}
		}

		return GetEnhancementLabel(enhancement);
	}

	private string GetCardTooltipText(Card card)
	{
		if (card == null)
		{
			return "Unknown card.";
		}

		var cost = GetEffectiveCardCost(card);
		var baseText = GetCardEffectDescription(card);
		var enhancementText = GetEnhancementDescription(card.enhancement);
		return $"{card.title}\nCost: {cost}\nEffect: {baseText}\nEnhancement: {enhancementText}";
	}

	private GUIStyle GetCardTooltipStyle()
	{
		if (cardTooltipStyle != null)
		{
			return cardTooltipStyle;
		}

		cardTooltipStyle = new GUIStyle(GUI.skin.box)
		{
			alignment = TextAnchor.UpperLeft,
			fontSize = 12,
			wordWrap = true
		};
		cardTooltipStyle.padding = new RectOffset(8, 8, 6, 6);
		return cardTooltipStyle;
	}

	private void DrawCardTooltip()
	{
		var tooltip = GUI.tooltip;
		if (string.IsNullOrWhiteSpace(tooltip))
		{
			return;
		}

		var lineCount = 1;
		for (var i = 0; i < tooltip.Length; i++)
		{
			if (tooltip[i] == '\n')
			{
				lineCount++;
			}
		}

		var width = 360f;
		var height = Mathf.Clamp(26f + lineCount * 18f, 72f, 220f);
		var pointer = Event.current.mousePosition;
		var x = Mathf.Min(pointer.x + 18f, Screen.width - width - 12f);
		var y = Mathf.Min(pointer.y + 18f, Screen.height - height - 12f);
		GUI.Box(new Rect(x, y, width, height), tooltip, GetCardTooltipStyle());
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
		var clearedLootboxNeighbors = new List<int>();
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
				continue;
			}

			if (IsLootboxRoom(roomId))
			{
				clearedLootboxNeighbors.Add(roomId);
			}
		}

		for (var i = 0; i < clearedLootboxNeighbors.Count; i++)
		{
			var roomId = clearedLootboxNeighbors[i];
			if (!availableRoomChoices.Contains(roomId))
			{
				availableRoomChoices.Add(roomId);
			}
		}

		if (availableRoomChoices.Count == 0)
		{
			for (var i = 0; i < neighbors.Length; i++)
			{
				var roomId = neighbors[i];
				if (roomId != currentRoomId && !availableRoomChoices.Contains(roomId))
				{
					availableRoomChoices.Add(roomId);
				}
			}
		}
	}

	private bool IsLootboxRoom(int roomId)
	{
		return lootboxRoomIds.Contains(roomId);
	}

	private void RefreshCurrentRoomMapHighlight()
	{
		if (ecosystemGenerator == null)
		{
			return;
		}

		if (!highlightCurrentRoomOnMap)
		{
			ecosystemGenerator.HighlightRoomById(-1, currentRoomMapTintColor);
			return;
		}

		ecosystemGenerator.HighlightRoomById(currentRoomId, currentRoomMapTintColor);
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
			RefreshCurrentRoomMapHighlight();
			return;
		}

		if (roomDepthById.Count != rooms.Count)
		{
			BuildRoomDepthMap(FindStartRoomId());
			AssignLootboxRooms();
		}

		if (GetRoomById(currentRoomId) != null)
		{
			RefreshCurrentRoomMapHighlight();
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
			RefreshCurrentRoomMapHighlight();
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
			RefreshCurrentRoomMapHighlight();
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
		RefreshCurrentRoomMapHighlight();
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
		GetLootboxRarityWeightsByDepth(depthT, out var commonWeight, out var rareWeight, out var epicWeight, out var legendaryWeight);
		var depthLuck = Mathf.Clamp01(0.05f + depthT * 0.35f);
		var artifactLuck = Mathf.Clamp01(lootboxTool.GetLootboxRoomLuckBonus());
		var totalLuck = Mathf.Clamp01(depthLuck + artifactLuck);
		ApplyLuckToLootboxRarityWeights(totalLuck, ref commonWeight, ref rareWeight, ref epicWeight, ref legendaryWeight);

		var bonusArtifacts = Mathf.Max(0, lootboxTool.ConsumeNextArtifactRoomBonusDrops());
		var totalArtifactsToRoll = 1 + bonusArtifacts;
		LootboxTool.ArtifactItem firstArtifact = null;
		var generatedCount = 0;
		for (var i = 0; i < totalArtifactsToRoll; i++)
		{
			var rolledArtifact = lootboxTool.OpenSingleArtifactLootboxWithRarityWeights(commonWeight, rareWeight, epicWeight, legendaryWeight);
			if (rolledArtifact == null)
			{
				continue;
			}

			generatedCount++;
			if (firstArtifact == null)
			{
				firstArtifact = rolledArtifact;
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

		lootboxRoomStatusMessage = generatedCount > 1
			? $"You found {firstArtifact.name} [{firstArtifact.rarity}] and {generatedCount - 1} additional artifact(s)."
			: $"You found {firstArtifact.name} [{firstArtifact.rarity}].";
	}

	private static void GetLootboxRarityWeightsByDepth(float depthT, out float commonWeight, out float rareWeight, out float epicWeight, out float legendaryWeight)
	{
		var normalizedDepth = Mathf.Clamp01(depthT);
		if (normalizedDepth < 0.35f)
		{
			commonWeight = 62f;
			rareWeight = 30f;
			epicWeight = 7f;
			legendaryWeight = 1f;
			return;
		}

		if (normalizedDepth < 0.7f)
		{
			commonWeight = 35f;
			rareWeight = 40f;
			epicWeight = 20f;
			legendaryWeight = 5f;
			return;
		}

		commonWeight = 12f;
		rareWeight = 33f;
		epicWeight = 38f;
		legendaryWeight = 17f;
	}

	private static void ApplyLuckToLootboxRarityWeights(float luck, ref float commonWeight, ref float rareWeight, ref float epicWeight, ref float legendaryWeight)
	{
		var luckT = Mathf.Clamp01(luck);
		if (luckT <= 0f)
		{
			return;
		}

		var commonToHigher = Mathf.Min(commonWeight, commonWeight * 0.55f * luckT);
		commonWeight -= commonToHigher;
		rareWeight += commonToHigher * 0.45f;
		epicWeight += commonToHigher * 0.35f;
		legendaryWeight += commonToHigher * 0.2f;

		var rareToHigher = Mathf.Min(rareWeight, rareWeight * 0.35f * luckT);
		rareWeight -= rareToHigher;
		epicWeight += rareToHigher * 0.7f;
		legendaryWeight += rareToHigher * 0.3f;
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
		ResetCombatFeedbackState();
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

		var safeDanger = Mathf.Max(1f, danger);
		var hpDangerScale = Mathf.Pow(safeDanger, 0.85f) - 1f;
		var damageDangerScale = Mathf.Pow(safeDanger, 0.75f) - 1f;
		var baseEnemyHpValue = Mathf.RoundToInt(baseEnemyHp + hpDangerScale * hpPerDanger);
		var baseMaxDamageValue = Mathf.Max(maxEnemyDamageFloor, Mathf.RoundToInt(minEnemyDamage + damageDangerScale * damagePerDanger));

		if (isBoss)
		{
			baseEnemyHpValue = Mathf.RoundToInt(baseEnemyHpValue * Mathf.Max(1f, bossHpMultiplier));
			baseMaxDamageValue = Mathf.RoundToInt(baseMaxDamageValue * Mathf.Max(1f, bossDamageMultiplier));
		}
		else
		{
			var encounterRampCount = Mathf.Min(GetClearedNonBossEncounterCount(), Mathf.Max(0, earlyEncounterRampCount));
			if (encounterRampCount > 0)
			{
				baseEnemyHpValue += Mathf.RoundToInt(encounterRampCount * Mathf.Max(0f, earlyEncounterHpRamp));
				baseMaxDamageValue += Mathf.RoundToInt(encounterRampCount * Mathf.Max(0f, earlyEncounterDamageRamp));
			}
		}

		baseEnemyHpValue = Mathf.RoundToInt(baseEnemyHpValue * Mathf.Max(1f, enemyHpDifficultyMultiplier)) + Mathf.Max(0, flatEnemyHpDifficultyBonus);
		baseMaxDamageValue = Mathf.RoundToInt(baseMaxDamageValue * Mathf.Max(1f, enemyDamageDifficultyMultiplier)) + Mathf.Max(0, flatEnemyDamageDifficultyBonus);
		var dangerDamageMultiplier = 1f + Mathf.Max(0f, safeDanger - 1f) * Mathf.Max(0f, extraEnemyDamagePerDangerStep);
		baseMaxDamageValue = Mathf.Max(1, Mathf.RoundToInt(baseMaxDamageValue * dangerDamageMultiplier));

		const int enemyCount = 1;
		var enemyNames = BuildEncounterEnemyNames(room, isBoss, enemyCount);

		var encounter = new Encounter
		{
			roomId = room.id,
			roomLabel = room.label,
			isBoss = isBoss,
			dangerRating = danger,
			lastActionSummary = "Preparing attack",
			activeEnemyIndex = 0,
			totalEnemyCount = Mathf.Max(1, enemyCount),
			enemies = new List<EncounterEnemyState>(Mathf.Max(1, enemyCount))
		};

		for (var i = 0; i < enemyCount; i++)
		{
			var hpJitter = UnityEngine.Random.Range(0.95f, 1.08f);
			var damageJitter = UnityEngine.Random.Range(0.92f, 1.12f);
			var enemyHp = Mathf.Max(6, Mathf.RoundToInt(baseEnemyHpValue * hpJitter));
			var maxDamage = Mathf.Max(1, Mathf.RoundToInt(baseMaxDamageValue * damageJitter));
			var minDamage = Mathf.Max(1, Mathf.RoundToInt(maxDamage * Mathf.Clamp(enemyMinDamageRatio, 0.5f, 0.95f)));
			maxDamage = Mathf.Max(minDamage, maxDamage);

			encounter.enemies.Add(new EncounterEnemyState
			{
				enemyName = enemyNames[Mathf.Min(i, enemyNames.Count - 1)],
				maxHp = enemyHp,
				currentHp = enemyHp,
				minDamage = minDamage,
				maxDamage = maxDamage,
				currentBlock = 0,
				damageBuff = 0
			});
		}

		if (encounter.enemies.Count == 0)
		{
			return null;
		}

		ApplyEnemyStateToEncounter(encounter, encounter.enemies[0]);
		return encounter;
	}

	private List<string> BuildEncounterEnemyNames(dungeonGenerator.RoomData room, bool isBoss, int desiredCount)
	{
		var targetCount = Mathf.Max(1, desiredCount);
		var pool = new List<string>();

		if (room != null && room.occupants != null)
		{
			for (var i = 0; i < room.occupants.Count; i++)
			{
				var baseName = ExtractCreatureName(room.occupants[i]);
				var repeats = Mathf.Clamp(ExtractCreatureCount(room.occupants[i]), 1, targetCount + 1);
				for (var r = 0; r < repeats; r++)
				{
					pool.Add(baseName);
				}
			}
		}

		if (pool.Count == 0)
		{
			pool.Add("Unknown Creature");
		}

		ShuffleEnemyNames(pool);

		var names = new List<string>(targetCount);
		if (isBoss)
		{
			names.Add($"{pool[0]} Boss");
			return names;
		}

		for (var i = 0; i < targetCount; i++)
		{
			var baseName = pool[i % pool.Count];
			names.Add(targetCount > 1 ? $"{baseName} {i + 1}" : baseName);
		}

		return names;
	}

	private static void ShuffleEnemyNames(List<string> values)
	{
		if (values == null)
		{
			return;
		}

		for (var i = values.Count - 1; i > 0; i--)
		{
			var j = UnityEngine.Random.Range(0, i + 1);
			var temp = values[i];
			values[i] = values[j];
			values[j] = temp;
		}
	}

	private static void ApplyEnemyStateToEncounter(Encounter encounter, EncounterEnemyState enemyState)
	{
		if (encounter == null || enemyState == null)
		{
			return;
		}

		encounter.enemyName = enemyState.enemyName;
		encounter.maxHp = enemyState.maxHp;
		encounter.currentHp = enemyState.currentHp;
		encounter.minDamage = enemyState.minDamage;
		encounter.maxDamage = enemyState.maxDamage;
		encounter.currentBlock = enemyState.currentBlock;
		encounter.damageBuff = enemyState.damageBuff;
	}

	private EncounterEnemyState GetActiveEnemyState()
	{
		if (currentEncounter == null || currentEncounter.enemies == null || currentEncounter.enemies.Count == 0)
		{
			return null;
		}

		currentEncounter.activeEnemyIndex = Mathf.Clamp(currentEncounter.activeEnemyIndex, 0, currentEncounter.enemies.Count - 1);
		return currentEncounter.enemies[currentEncounter.activeEnemyIndex];
	}

	private void SyncEncounterFromActiveEnemy()
	{
		var activeEnemy = GetActiveEnemyState();
		if (activeEnemy == null)
		{
			return;
		}

		ApplyEnemyStateToEncounter(currentEncounter, activeEnemy);
	}

	private void SyncActiveEnemyFromEncounter()
	{
		var activeEnemy = GetActiveEnemyState();
		if (currentEncounter == null || activeEnemy == null)
		{
			return;
		}

		activeEnemy.enemyName = currentEncounter.enemyName;
		activeEnemy.maxHp = currentEncounter.maxHp;
		activeEnemy.currentHp = currentEncounter.currentHp;
		activeEnemy.minDamage = currentEncounter.minDamage;
		activeEnemy.maxDamage = currentEncounter.maxDamage;
		activeEnemy.currentBlock = currentEncounter.currentBlock;
		activeEnemy.damageBuff = currentEncounter.damageBuff;
	}

	private int GetClearedNonBossEncounterCount()
	{
		if (ecosystemGenerator == null || clearedRooms.Count == 0)
		{
			return 0;
		}

		var count = 0;
		foreach (var roomId in clearedRooms)
		{
			var room = GetRoomById(roomId);
			if (room == null)
			{
				continue;
			}

			if (string.Equals(room.label, "START", StringComparison.OrdinalIgnoreCase)
				|| IsGoalRoom(room)
				|| IsLootboxRoom(room.id))
			{
				continue;
			}

			count++;
		}

		return count;
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

		SyncEncounterFromActiveEnemy();

		phase = RunPhase.PlayerTurn;
		playerBlock = 0;
		turnDamageModifier = 0;
		playerEnergy = Mathf.Max(0, energyPerTurn + fightEnergyPerTurnBonus + artifactEnergyEachTurnStartBonus);
		if (artifactVulnerableAtTurnStartAmount > 0)
		{
			ApplyVulnerableToEnemy(artifactVulnerableAtTurnStartAmount);
		}
		if (artifactHealAtTurnStartAmount > 0)
		{
			HealPlayer(artifactHealAtTurnStartAmount, true);
		}
		if (artifactDamageAtTurnStartAmount > 0)
		{
			DealDamageToEnemy(artifactDamageAtTurnStartAmount);
		}
		if (artifactBlockAtTurnStartAmount > 0)
		{
			GainBlock(artifactBlockAtTurnStartAmount);
		}
		DrawCards(Mathf.Max(0, handSize + artifactExtraDrawEachTurn));
		if (currentEncounter != null && currentEncounter.currentHp <= 0)
		{
			HandleEnemyDefeatInEncounter();
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

	private void ApplyVulnerableToEnemy(int turns)
	{
		if (currentEncounter == null || turns <= 0)
		{
			return;
		}

		enemyVulnerableTurnsRemaining += turns;
	}

	private bool IsEnemyVulnerable()
	{
		return currentEncounter != null && enemyVulnerableTurnsRemaining > 0;
	}

	private string GetEnemyStatusText()
	{
		var statuses = new List<string>();
		if (currentEncounter != null && currentEncounter.damageBuff > 0)
		{
			statuses.Add($"Enrage +{currentEncounter.damageBuff}");
		}

		if (enemyBurnTurnsRemaining > 0 && enemyBurnDamagePerTurn > 0)
		{
			statuses.Add($"Burn {enemyBurnDamagePerTurn} ({enemyBurnTurnsRemaining}t)");
		}

		if (enemyVulnerableTurnsRemaining > 0)
		{
			var bonusPercent = Mathf.RoundToInt((Mathf.Max(1f, enemyVulnerableDamageMultiplier) - 1f) * 100f);
			statuses.Add($"Vulnerable +{bonusPercent}% ({enemyVulnerableTurnsRemaining}t)");
		}

		if (statuses.Count == 0)
		{
			return "None";
		}

		return string.Join(" | ", statuses);
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

	private void RegisterEnemyHitFeedback(int damage)
	{
		if (damage <= 0)
		{
			return;
		}

		enemyLastDamageTaken = damage;
		enemyHitFlashEndTime = Time.unscaledTime + Mathf.Max(0.05f, hitFlashDuration);
	}

	private void RegisterPlayerHitFeedback(int damage)
	{
		if (damage <= 0)
		{
			return;
		}

		playerLastDamageTaken = damage;
		playerHitFlashEndTime = Time.unscaledTime + Mathf.Max(0.05f, hitFlashDuration);
	}

	private void DealDamageToEnemy(int amount, bool isAttackAction = false)
	{
		if (currentEncounter == null || amount <= 0)
		{
			return;
		}

		if (isAttackAction)
		{
			PlayAttackSoundEffect();
		}

		var totalDamage = Mathf.Max(0, amount + fightDamageBonus + turnDamageModifier);
		if (IsEnemyVulnerable())
		{
			totalDamage += Mathf.Max(0, artifactBonusDamageVsVulnerable);
			totalDamage = Mathf.RoundToInt(totalDamage * Mathf.Max(1f, enemyVulnerableDamageMultiplier));
		}

		if (currentEncounter.currentBlock > 0)
		{
			var absorbed = Mathf.Min(totalDamage, currentEncounter.currentBlock);
			currentEncounter.currentBlock -= absorbed;
			totalDamage -= absorbed;
		}

		var hpDamage = 0;
		if (totalDamage > 0)
		{
			currentEncounter.currentHp -= totalDamage;
			hpDamage = totalDamage;
		}

		SyncActiveEnemyFromEncounter();

		if (hpDamage > 0)
		{
			RegisterEnemyHitFeedback(hpDamage);
		}
	}

	private void GainBlock(int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		var totalBlock = Mathf.Max(0, amount + fightBlockBonus);
		if (totalBlock <= 0)
		{
			return;
		}

		playerBlock += totalBlock;
		PlayBlockSoundEffect();
	}

	private void LosePlayerHp(int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		var hpBefore = playerHp;
		playerHp = Mathf.Max(0, playerHp - amount);
		var hpLost = hpBefore - playerHp;
		if (hpLost > 0)
		{
			RegisterPlayerHitFeedback(hpLost);
		}

		if (playerHp <= 0)
		{
			phase = RunPhase.Defeat;
		}
	}

	private void OnCardDrawn()
	{
		PlayDrawCardSoundEffect();

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

		if (healedAmount > 0 && artifactBlockOnHealAmount > 0)
		{
			GainBlock(artifactBlockOnHealAmount);
		}

		if (healedAmount > 0 && artifactDrawOnHealAmount > 0)
		{
			DrawCards(artifactDrawOnHealAmount);
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
		artifactHealAtTurnStartAmount = 0;
		artifactDamageAtTurnStartAmount = 0;
		artifactBlockOnDamageCardPlayAmount = 0;
		artifactEnergyEachTurnStartBonus = 0;
		artifactBurnOnBlockCardPlay = 0;
		artifactDrawOnHealAmount = 0;
		artifactVulnerableOnDamageCardPlay = 0;
		artifactVulnerableAtTurnStartAmount = 0;
		artifactBonusDamageVsVulnerable = 0;
		artifactHealOnDamageCardPlayAmount = 0;
		artifactBlockOnHealAmount = 0;
		artifactDrawOnGainEnergyCardPlay = 0;
		artifactBurnDamageBonus = 0;

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
		artifactHealAtTurnStartAmount = artifactBonuses.healEachTurnStart;
		artifactDamageAtTurnStartAmount = artifactBonuses.damageEachTurnStart * 2;
		artifactBlockOnDamageCardPlayAmount = artifactBonuses.blockOnDamageCardPlay * 2;
		artifactEnergyEachTurnStartBonus = artifactBonuses.energyEachTurnStart;
		artifactBurnOnBlockCardPlay = artifactBonuses.burnOnBlockCardPlay;
		artifactDrawOnHealAmount = artifactBonuses.drawOnHeal;
		artifactVulnerableOnDamageCardPlay = artifactBonuses.vulnerableOnDamageCardPlay;
		artifactVulnerableAtTurnStartAmount = artifactBonuses.vulnerableEachTurnStart;
		artifactBonusDamageVsVulnerable = artifactBonuses.bonusDamageWhenEnemyVulnerable;
		artifactHealOnDamageCardPlayAmount = artifactBonuses.healOnDamageCardPlayTriggers;
		artifactBlockOnHealAmount = artifactBonuses.blockOnHealTriggers * 2;
		artifactDrawOnGainEnergyCardPlay = artifactBonuses.drawOnGainEnergyCardPlay;
		artifactBurnDamageBonus = artifactBonuses.burnDamageBonus;
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

		SyncEncounterFromActiveEnemy();

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
			HandleEnemyDefeatInEncounter();
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
				if (artifactVulnerableOnDamageCardPlay > 0)
				{
					ApplyVulnerableToEnemy(artifactVulnerableOnDamageCardPlay);
				}
				DealDamageToEnemy(card.value + artifactDamageCardBonus, true);
				if (artifactBlockOnDamageCardPlayAmount > 0)
				{
					GainBlock(artifactBlockOnDamageCardPlayAmount);
				}
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
				if (artifactHealOnDamageCardPlayAmount > 0)
				{
					HealPlayer(artifactHealOnDamageCardPlayAmount, true);
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
				if (artifactBurnOnBlockCardPlay > 0)
				{
					enemyBurnDamagePerTurn += artifactBurnOnBlockCardPlay;
					enemyBurnTurnsRemaining += artifactBurnOnBlockCardPlay;
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
				if (artifactDrawOnGainEnergyCardPlay > 0)
				{
					DrawCards(artifactDrawOnGainEnergyCardPlay);
				}
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
				DealDamageToEnemy(4, true);
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
			case CardEnhancement.BloodrootSap:
				HealPlayer(2, true);
				break;
			case CardEnhancement.BastionBloom:
				GainBlock(card.effect == CardEffect.Block ? 5 : 3);
				break;
			case CardEnhancement.EmberCircuit:
				enemyBurnDamagePerTurn += 1;
				enemyBurnTurnsRemaining += 1;
				DrawCards(1);
				break;
			case CardEnhancement.WildOverclock:
				playerEnergy += 1;
				fightDamageBonus += 1;
				break;
			case CardEnhancement.PredatorsMark:
				ApplyVulnerableToEnemy(2);
				break;
			case CardEnhancement.LeechingVine:
				DealDamageToEnemy(2, true);
				HealPlayer(2, true);
				break;
			case CardEnhancement.ThornguardPulse:
				GainBlock(4);
				DrawCards(1);
				break;
			case CardEnhancement.ForesightBurst:
				DrawCards(2);
				break;
			case CardEnhancement.RuinSpores:
				if (IsEnemyVulnerable())
				{
					DealDamageToEnemy(5, true);
				}
				else
				{
					ApplyVulnerableToEnemy(1);
				}
				break;
			case CardEnhancement.IronBastion:
				GainBlock(6);
				playerEnergy += 1;
				break;
			case CardEnhancement.OverclockedReflex:
				DrawCards(1);
				playerEnergy += 1;
				break;
			case CardEnhancement.Plaguebrand:
				enemyBurnDamagePerTurn += 2;
				enemyBurnTurnsRemaining += 2;
				ApplyVulnerableToEnemy(1);
				break;
			case CardEnhancement.BloodFrenzy:
				if (currentEncounter != null && currentEncounter.currentHp <= Mathf.RoundToInt(currentEncounter.maxHp * 0.5f))
				{
					DealDamageToEnemy(6, true);
				}
				else
				{
					DealDamageToEnemy(3, true);
				}
				break;
			case CardEnhancement.BoneWard:
				GainBlock(8);
				if (IsEnemyVulnerable())
				{
					DrawCards(1);
				}
				break;
			case CardEnhancement.SerratedBloom:
				DealDamageToEnemy(3, true);
				enemyBurnDamagePerTurn += 1;
				enemyBurnTurnsRemaining += 1;
				break;
			case CardEnhancement.AdaptiveGuard:
				GainBlock(IsEnemyVulnerable() ? 8 : 4);
				break;
			case CardEnhancement.PredatoryFocus:
				ApplyVulnerableToEnemy(1);
				DrawCards(1);
				break;
			case CardEnhancement.VitalSurge:
				HealPlayer(2, true);
				playerEnergy += 1;
				break;
			case CardEnhancement.FinisherClaw:
				if (currentEncounter != null && currentEncounter.currentHp <= Mathf.RoundToInt(currentEncounter.maxHp * 0.4f))
				{
					DealDamageToEnemy(7, true);
				}
				else
				{
					DealDamageToEnemy(3, true);
				}
				break;
			case CardEnhancement.FortressDebt:
				GainBlock(25);
				turnDamageModifier -= 4;
				break;
			case CardEnhancement.HemorrhageBurst:
				DealDamageToEnemy(14, true);
				playerEnergy += 1;
				LosePlayerHp(4);
				break;
			case CardEnhancement.OverdrawLoop:
				DrawCards(3);
				playerEnergy = Mathf.Max(0, playerEnergy - 1);
				break;
			case CardEnhancement.ShatterLunge:
				DealDamageToEnemy(16, true);
				if (currentEncounter != null)
				{
					currentEncounter.currentBlock += 5;
					PlayBlockSoundEffect();
					SyncActiveEnemyFromEncounter();
				}
				break;
			case CardEnhancement.RecoveryCrash:
				HealPlayer(8, true);
				playerBlock = Mathf.Max(0, playerBlock - 6);
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

		SyncEncounterFromActiveEnemy();

		if (ApplyEnemyStartOfTurnEffects())
		{
			lastEnemyRoll = 0;
			return;
		}

		var actionType = RollEnemyActionType(currentEncounter);
		switch (actionType)
		{
			case EnemyActionType.Defend:
				ExecuteEnemyDefend(currentEncounter);
				break;
			case EnemyActionType.Buff:
				ExecuteEnemyBuff(currentEncounter);
				break;
			default:
				ExecuteEnemyAttack(currentEncounter, 1f, "Attack");
				break;
		}

		if (enemyVulnerableTurnsRemaining > 0)
		{
			enemyVulnerableTurnsRemaining = Mathf.Max(0, enemyVulnerableTurnsRemaining - 1);
		}

		if (playerHp <= 0)
		{
			playerHp = 0;
			phase = RunPhase.Defeat;
		}
	}

	private EnemyActionType RollEnemyActionType(Encounter encounter)
	{
		if (encounter == null)
		{
			return EnemyActionType.Attack;
		}

		var attackWeight = Mathf.Max(0f, enemyActionAttackWeight);
		var defendWeight = Mathf.Max(0f, enemyActionDefendWeight);
		var buffWeight = Mathf.Max(0f, enemyActionBuffWeight);

		if (encounter.currentHp <= Mathf.RoundToInt(encounter.maxHp * 0.45f))
		{
			defendWeight += 0.2f;
		}

		var maxBuffAmount = Mathf.Max(1, enemyMaxBuffStacks * Mathf.Max(1, enemyBuffDamagePerStack));
		if (encounter.damageBuff >= maxBuffAmount)
		{
			attackWeight += 0.2f;
			buffWeight *= 0.25f;
		}
		else
		{
			buffWeight += 0.1f;
		}

		if (encounter.isBoss)
		{
			attackWeight += 0.12f;
			buffWeight += 0.08f;
		}

		var totalWeight = attackWeight + defendWeight + buffWeight;
		if (totalWeight <= 0f)
		{
			return EnemyActionType.Attack;
		}

		var roll = UnityEngine.Random.Range(0f, totalWeight);
		if (roll < attackWeight)
		{
			return EnemyActionType.Attack;
		}

		roll -= attackWeight;
		if (roll < defendWeight)
		{
			return EnemyActionType.Defend;
		}

		return EnemyActionType.Buff;
	}

	private void ExecuteEnemyAttack(Encounter encounter, float attackScale, string actionLabel)
	{
		if (encounter == null)
		{
			return;
		}

		PlayAttackSoundEffect();

		var baseRoll = UnityEngine.Random.Range(encounter.minDamage, encounter.maxDamage + 1);
		var scaledRoll = Mathf.RoundToInt(baseRoll * Mathf.Max(0.35f, attackScale));
		lastEnemyRoll = Mathf.Max(0, scaledRoll + Mathf.Max(0, encounter.damageBuff));
		var damageAfterBlock = Mathf.Max(0, lastEnemyRoll - playerBlock);
		playerHp -= damageAfterBlock;
		if (damageAfterBlock > 0)
		{
			RegisterPlayerHitFeedback(damageAfterBlock);
		}
		encounter.lastActionSummary = $"{actionLabel} for {lastEnemyRoll}";
	}

	private void ExecuteEnemyDefend(Encounter encounter)
	{
		if (encounter == null)
		{
			return;
		}

		lastEnemyRoll = 0;
		var rangeBlock = Mathf.RoundToInt(Mathf.Lerp(encounter.minDamage, encounter.maxDamage, 0.75f));
		var dangerBlock = Mathf.RoundToInt(encounter.dangerRating * 0.6f);
		var blockGain = Mathf.Max(2, rangeBlock + Mathf.Max(0, enemyDefendBaseBlock) + dangerBlock);
		if (encounter.isBoss)
		{
			blockGain += 3;
		}

		encounter.currentBlock += blockGain;
		PlayBlockSoundEffect();
		encounter.lastActionSummary = $"Defend gained {blockGain} block";
		SyncActiveEnemyFromEncounter();
	}

	private void ExecuteEnemyBuff(Encounter encounter)
	{
		if (encounter == null)
		{
			return;
		}

		lastEnemyRoll = 0;
		var perStack = Mathf.Max(1, enemyBuffDamagePerStack);
		var maxBuffAmount = Mathf.Max(perStack, enemyMaxBuffStacks * perStack);
		var previous = encounter.damageBuff;
		encounter.damageBuff = Mathf.Min(maxBuffAmount, encounter.damageBuff + perStack);
		var gained = Mathf.Max(0, encounter.damageBuff - previous);

		if (encounter.isBoss)
		{
			ExecuteEnemyAttack(encounter, 0.7f, gained > 0 ? $"Enrage +{gained}, then strike" : "Strike");
			return;
		}

		encounter.lastActionSummary = gained > 0
			? $"Enrage +{gained} damage"
			: "Enrage is already at max";
		SyncActiveEnemyFromEncounter();
	}

	private bool ApplyEnemyStartOfTurnEffects()
	{
		if (currentEncounter == null)
		{
			return false;
		}

		if (enemyBurnTurnsRemaining > 0 && enemyBurnDamagePerTurn > 0)
		{
			var burnDamage = enemyBurnDamagePerTurn + Mathf.Max(0, artifactBurnDamageBonus);
			DealDamageToEnemy(burnDamage);
			enemyBurnTurnsRemaining = Mathf.Max(0, enemyBurnTurnsRemaining - 1);
		}

		if (currentEncounter.currentHp > 0)
		{
			return false;
		}

		HandleEnemyDefeatInEncounter();
		return true;
	}

	private void ResetEncounterModifiers()
	{
		fightDamageBonus = 0;
		fightBlockBonus = 0;
		fightEnergyPerTurnBonus = 0;
		enemyBurnDamagePerTurn = 0;
		enemyBurnTurnsRemaining = 0;
		enemyVulnerableTurnsRemaining = 0;
	}

	private void ResetCombatFeedbackState()
	{
		enemyLastDamageTaken = 0;
		playerLastDamageTaken = 0;
		enemyHitFlashEndTime = 0f;
		playerHitFlashEndTime = 0f;
	}

	private void DiscardHand()
	{
		for (var i = 0; i < hand.Count; i++)
		{
			discardPile.Add(hand[i]);
		}

		hand.Clear();
	}

	private void HandleEnemyDefeatInEncounter()
	{
		if (currentEncounter == null)
		{
			return;
		}

		currentEncounter.currentHp = 0;
		SyncActiveEnemyFromEncounter();
		OnEncounterDefeated();
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
			PlayVictorySoundEffect();
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

	private Texture2D GetGeneratedCaveFallbackTexture()
	{
		if (generatedCaveFallbackTexture != null)
		{
			return generatedCaveFallbackTexture;
		}

		const int textureSize = 192;
		generatedCaveFallbackTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
		{
			wrapMode = TextureWrapMode.Repeat,
			filterMode = FilterMode.Bilinear,
			name = "GeneratedCaveBackdrop"
		};

		var deepShade = new Color(0.08f, 0.1f, 0.12f, 1f);
		var midShade = new Color(0.16f, 0.18f, 0.2f, 1f);
		var highlightShade = new Color(0.24f, 0.26f, 0.29f, 1f);

		for (var y = 0; y < textureSize; y++)
		{
			var ny = (float)y / (textureSize - 1);
			for (var x = 0; x < textureSize; x++)
			{
				var nx = (float)x / (textureSize - 1);
				var largeNoise = Mathf.PerlinNoise(nx * 3.2f + 0.21f, ny * 2.9f + 0.37f);
				var fineNoise = Mathf.PerlinNoise(nx * 11.4f + 1.13f, ny * 10.8f + 0.74f);
				var strata = 0.5f + 0.5f * Mathf.Sin(ny * 14f + largeNoise * 2.4f);
				var colorT = Mathf.Clamp01(largeNoise * 0.7f + fineNoise * 0.2f + strata * 0.1f);

				var pixelColor = Color.Lerp(deepShade, midShade, colorT);
				if (fineNoise > 0.7f)
				{
					var highlightT = Mathf.InverseLerp(0.7f, 1f, fineNoise) * 0.5f;
					pixelColor = Color.Lerp(pixelColor, highlightShade, highlightT);
				}

				generatedCaveFallbackTexture.SetPixel(x, y, pixelColor);
			}
		}

		generatedCaveFallbackTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		return generatedCaveFallbackTexture;
	}

	private Texture2D GetBackgroundTextureForCurrentPhase()
	{
		if (phase == RunPhase.LootboxRoom)
		{
			return lootboxRoomBackgroundTexture != null ? lootboxRoomBackgroundTexture : globalBackgroundTexture;
		}

		if (phase == RunPhase.PlayerTurn || phase == RunPhase.EnemyTurn || phase == RunPhase.RewardSelection)
		{
			return combatRoomBackgroundTexture != null ? combatRoomBackgroundTexture : globalBackgroundTexture;
		}

		return globalBackgroundTexture;
	}

	private void DrawGlobalBackground(Texture2D preferredBackground, float alpha)
	{
		var clampedAlpha = Mathf.Clamp01(alpha);
		if (clampedAlpha <= 0f)
		{
			return;
		}

		var previousColor = GUI.color;
		var backgroundTexture = preferredBackground != null ? preferredBackground : globalBackgroundTexture;
		if (backgroundTexture != null)
		{
			GUI.color = new Color(1f, 1f, 1f, clampedAlpha);
			GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), backgroundTexture, ScaleMode.ScaleAndCrop);
		}
		else
		{
			var fallbackTexture = GetGeneratedCaveFallbackTexture();
			if (fallbackTexture != null)
			{
				GUI.color = new Color(1f, 1f, 1f, 0.96f * clampedAlpha);
				GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fallbackTexture, ScaleMode.ScaleAndCrop);
			}
			else
			{
				GUI.color = new Color(0.12f, 0.14f, 0.17f, clampedAlpha);
				GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
			}
		}

		GUI.color = previousColor;
	}

	private void DrawCombatBackdropDim()
	{
		var previousColor = GUI.color;
		GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(combatBackgroundDimAlpha));
		GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
		GUI.color = previousColor;
	}

	private void OnDestroy()
	{
		if (generatedCaveFallbackTexture != null)
		{
			Destroy(generatedCaveFallbackTexture);
			generatedCaveFallbackTexture = null;
		}
	}

	private void DrawHitFlash(Rect targetRect, float flashEndTime, int lastDamage, Color flashColor)
	{
		if (lastDamage <= 0)
		{
			return;
		}

		var duration = Mathf.Max(0.05f, hitFlashDuration);
		var remaining = flashEndTime - Time.unscaledTime;
		if (remaining <= 0f)
		{
			return;
		}

		var t = Mathf.Clamp01(remaining / duration);
		var previousColor = GUI.color;
		flashColor.a = Mathf.Clamp01(hitFlashAlpha) * t;
		GUI.color = flashColor;
		GUI.DrawTexture(targetRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
		GUI.color = previousColor;

		GUI.Label(new Rect(targetRect.xMax - 82f, targetRect.y + 8f, 74f, 24f), $"-{lastDamage}");
	}

	private static Color GetEnemyActionColor(string actionSummary)
	{
		if (string.IsNullOrWhiteSpace(actionSummary))
		{
			return new Color(0.83f, 0.88f, 0.96f, 1f);
		}

		if (actionSummary.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
			|| actionSummary.IndexOf("strike", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return new Color(0.96f, 0.45f, 0.33f, 1f);
		}

		if (actionSummary.IndexOf("defend", StringComparison.OrdinalIgnoreCase) >= 0
			|| actionSummary.IndexOf("block", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return new Color(0.42f, 0.78f, 0.98f, 1f);
		}

		if (actionSummary.IndexOf("enrage", StringComparison.OrdinalIgnoreCase) >= 0
			|| actionSummary.IndexOf("buff", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return new Color(0.88f, 0.6f, 0.96f, 1f);
		}

		return new Color(0.83f, 0.88f, 0.96f, 1f);
	}

	private void DrawJuiceOverlay(Rect targetRect, Color tint, float minAlpha, float maxAlpha, float speed)
	{
		var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
		var alpha = Mathf.Lerp(Mathf.Clamp01(minAlpha), Mathf.Clamp01(maxAlpha), pulse);
		var previousColor = GUI.color;
		tint.a = alpha;
		GUI.color = tint;
		GUI.DrawTexture(targetRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
		GUI.color = previousColor;
	}

	private void DrawPanelAccent(Rect targetRect, Color accentColor)
	{
		var previousColor = GUI.color;
		accentColor.a = 1f;
		GUI.color = accentColor;
		GUI.DrawTexture(new Rect(targetRect.x + 2f, targetRect.y + 2f, Mathf.Max(2f, targetRect.width - 4f), 3f), Texture2D.whiteTexture, ScaleMode.StretchToFill);
		GUI.color = previousColor;
	}

	private void OnGUI()
	{
		if (!useDebugOnGui)
		{
			return;
		}

		var backgroundAlpha = phase == RunPhase.MapSelection ? 0.28f : 1f;
		var phaseBackground = GetBackgroundTextureForCurrentPhase();
		DrawGlobalBackground(phaseBackground, backgroundAlpha);

		if (phase != RunPhase.TitleScreen)
		{
			DrawRunHeader();
		}

		if (phase == RunPhase.TitleScreen)
		{
			DrawTitleScreen();
		}
		else if (phase == RunPhase.MapSelection)
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

	private void DrawTitleScreen()
	{
		var panelWidth = Mathf.Min(720f, Screen.width - 48f);
		var panelHeight = 260f;
		var panelRect = new Rect((Screen.width - panelWidth) * 0.5f, Mathf.Max(80f, Screen.height * 0.22f), panelWidth, panelHeight);

		GUI.Box(panelRect, string.Empty);
		DrawPanelAccent(panelRect, new Color(0.84f, 0.93f, 0.98f, 1f));

		var titleStyle = new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.MiddleCenter,
			fontSize = Mathf.Clamp(Mathf.RoundToInt(panelWidth * 0.08f), 28, 56),
			fontStyle = FontStyle.Bold
		};

		var subtitleStyle = new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.UpperCenter,
			fontSize = 16,
			wordWrap = true
		};

		GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 24f, panelRect.width - 32f, 70f), gameTitle, titleStyle);
		GUI.Label(new Rect(panelRect.x + 40f, panelRect.y + 102f, panelRect.width - 80f, 48f), gameSubtitle, subtitleStyle);

		var playButtonRect = new Rect(panelRect.x + (panelRect.width - 220f) * 0.5f, panelRect.yMax - 72f, 220f, 40f);
		if (GUI.Button(playButtonRect, "Play Game"))
		{
			StartGameplayMusicWithFadeIn();
			InitializeRun();
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
		RefreshCurrentRoomMapHighlight();
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

		GUI.Box(new Rect(16f, 86f, 1040f, 84f), $"Lootbox Room: {room.label}\nOpen the room lootbox to gain at least one artifact.");
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
		DrawCombatBackdropDim();

		if (currentEncounter == null)
		{
			GUI.Box(new Rect(16f, 86f, 520f, 60f), "No active reward.");
			if (GUI.Button(new Rect(16f, 154f, 220f, 34f), "Continue"))
			{
				ContinueAfterEncounter();
			}
			return;
		}

		var rewardHeaderRect = new Rect(16f, 86f, 1040f, 72f);
		GUI.Box(rewardHeaderRect, $"{currentEncounter.enemyName} dropped a card enhancement.");
		DrawPanelAccent(rewardHeaderRect, new Color(0.63f, 0.81f, 1f, 1f));

		if (!rewardDropRevealed)
		{
			var previousBackgroundColor = GUI.backgroundColor;
			var revealPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
			GUI.backgroundColor = Color.Lerp(new Color(0.28f, 0.45f, 0.75f, 1f), new Color(0.42f, 0.62f, 0.93f, 1f), revealPulse);
			if (GUI.Button(new Rect(16f, 166f, 280f, 34f), "Reveal Creature Drop"))
			{
				rewardDropRevealed = true;
				BuildRewardEligibleCardIndices();
			}
			GUI.backgroundColor = previousBackgroundColor;

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

		var dropRect = new Rect(16f, 208f, 1040f, 64f);
		GUI.Box(dropRect, $"Drop: {pendingDrop.name}\nEffect: {pendingDrop.description}");
		DrawJuiceOverlay(dropRect, new Color(1f, 0.82f, 0.25f, 1f), 0.02f, 0.08f, 2.8f);
		DrawPanelAccent(dropRect, new Color(1f, 0.9f, 0.4f, 1f));

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

		SyncEncounterFromActiveEnemy();

		DrawCombatBackdropDim();

		var enemyLabel = currentEncounter.isBoss ? "Boss" : "Enemy";
		var enemyStatusText = GetEnemyStatusText();
		var enemyActionText = currentEncounter.lastActionSummary;
		var enemyActionColor = GetEnemyActionColor(enemyActionText);
		var enemyPackText = currentEncounter.totalEnemyCount > 1
			? $"Pack {currentEncounter.activeEnemyIndex + 1}/{currentEncounter.totalEnemyCount}"
			: "Solo";
		var enemyExactDamage = Mathf.Max(0, currentEncounter.maxDamage + Mathf.Max(0, currentEncounter.damageBuff));
		var enemyRect = new Rect(16f, 86f, 520f, 118f);
		var playerRect = new Rect(544f, 86f, 512f, 84f);
		GUI.Box(
			enemyRect,
			$"{enemyLabel}: {currentEncounter.enemyName}\nHP: {currentEncounter.currentHp}/{currentEncounter.maxHp}  Block: {currentEncounter.currentBlock}  {enemyPackText}\nDamage: {enemyExactDamage}  (Danger {currentEncounter.dangerRating:F1})\nStatus: {enemyStatusText}\nAction: {enemyActionText}");
		DrawPanelAccent(enemyRect, enemyActionColor);
		DrawJuiceOverlay(new Rect(enemyRect.x + 4f, enemyRect.y + 6f, enemyRect.width - 8f, 4f), enemyActionColor, 0.15f, 0.42f, 6f);
		var turnDamageText = turnDamageModifier >= 0 ? $"+{turnDamageModifier}" : turnDamageModifier.ToString();

		GUI.Box(
			playerRect,
			$"Player\nHP: {playerHp}/{playerMaxHp}  Block: {playerBlock}  Energy: {playerEnergy}\nBonuses: +{fightDamageBonus} dmg  +{fightBlockBonus} block  +{fightEnergyPerTurnBonus} energy/turn  Turn dmg mod: {turnDamageText}");
		DrawPanelAccent(playerRect, new Color(0.45f, 0.86f, 0.55f, 1f));

		DrawHitFlash(enemyRect, enemyHitFlashEndTime, enemyLastDamageTaken, new Color(1f, 0.2f, 0.2f, 1f));
		DrawHitFlash(playerRect, playerHitFlashEndTime, playerLastDamageTaken, new Color(1f, 0.45f, 0.2f, 1f));

		GUI.Box(new Rect(16f, 212f, 520f, 36f), $"Draw: {drawPile.Count}  Discard: {discardPile.Count}  Last enemy hit: {lastEnemyRoll}");
		GUI.Box(new Rect(544f, 212f, 512f, 36f), "Hover cards for tooltips. Enhanced cards include extra effects.");

		var handTitle = "Hand (play cards and enhanced effects):";
		GUI.Box(new Rect(16f, 254f, 1040f, 228f), handTitle);

		var x = 24f;
		var y = 286f;
		for (var i = 0; i < hand.Count; i++)
		{
			var card = hand[i];
			var cardRect = new Rect(x, y, 150f, 64f);
			var effectiveCost = GetEffectiveCardCost(card);
			var canPlay = phase == RunPhase.PlayerTurn && playerEnergy >= effectiveCost;
			var content = new GUIContent(GetCardDisplayText(card), GetCardTooltipText(card));
			var previousColor = GUI.backgroundColor;
			if (!canPlay)
			{
				GUI.backgroundColor = new Color(0.62f, 0.62f, 0.62f, 1f);
			}
			if (GUI.Button(cardRect, content) && canPlay)
			{
				PlayCard(i);
				GUI.backgroundColor = previousColor;
				break;
			}
			GUI.backgroundColor = previousColor;

			x += 158f;
			if (x > 900f)
			{
				x = 24f;
				y += 72f;
			}
		}

		if (phase == RunPhase.PlayerTurn)
		{
			var previousButtonColor = GUI.backgroundColor;
			var endTurnPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
			GUI.backgroundColor = Color.Lerp(new Color(0.82f, 0.3f, 0.26f, 1f), new Color(0.96f, 0.52f, 0.34f, 1f), endTurnPulse);
			if (GUI.Button(new Rect(16f, 472f, 220f, 34f), "End Turn"))
			{
				EndPlayerTurn();
			}
			GUI.backgroundColor = previousButtonColor;
		}

		DrawCardTooltip();
	}
}



