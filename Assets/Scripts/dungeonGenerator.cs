using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class dungeonGenerator : MonoBehaviour
{
	[Header("Data")]
	[SerializeField] private TextAsset ecosystemJson;

	[Header("Generation")]
	[SerializeField] private int roomMin = 8;
	[SerializeField] private int roomMax = 14;
	[SerializeField] private bool generateOnStart = true;
	[SerializeField] private string biomeToBeGenerated = "forest";
	[Tooltip("CR bias ratio. < 1 favors safer creatures, > 1 favors dangerous creatures.")]
	[SerializeField] private float creatureCrWeightExponent = 1.0f;
	[SerializeField] private float creaturePopulationMultiplier = 1.0f;
	[Header("Optional 2D Visualization")]
	[SerializeField] private bool spawnRoomObjects = false;
	[SerializeField] private GameObject roomPrefab;
	[SerializeField] private Transform roomsParent;
	[SerializeField] private bool spawnEdges = true;
	[SerializeField] private Transform edgesParent;
	[SerializeField] private float uniformRoomScale = 1f;
	[Header("Room Preview")]
	[SerializeField] private bool enableRoomPreview = true;
	[SerializeField] private Vector3 previewAnchor = new Vector3(10f, -6f, 0f);
	[SerializeField] private Vector2 previewRoomSize = new Vector2(4f, 3f);
	[SerializeField] private float previewPadding = 0.3f;
	[SerializeField] private float previewCreatureScale = 1f;
	[SerializeField] private float previewLabelOffset = 0.25f;
	[SerializeField] private float previewZOffset = -0.5f;
	[SerializeField] private Color previewRoomColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
	[SerializeField] private Color previewTextColor = Color.white;
	[SerializeField] private int previewSortingOrder = 200;
	[SerializeField] private Sprite previewRoomSprite;
	[SerializeField] private Sprite previewFallbackCreatureSprite;
	[SerializeField] private List<CreatureIcon> creatureIcons = new List<CreatureIcon>();
	[Header("Noise Settings")]
	[SerializeField] private float noiseScale = 0.18f;
	[SerializeField] private float noiseJitter = 0.5f;
	[SerializeField] private float noiseMinSpacing = 0.9f;
	[SerializeField] private int noiseCandidateMultiplier = 6;
	[SerializeField] private int noiseOctaves = 4;
	[SerializeField] private float noiseLacunarity = 2.0f;
	[SerializeField] private float noiseGain = 0.5f;

	private float roomSpacing = 1.6f;
	private Vector2 defaultRoomSizeRange = new Vector2(1f, 1.5f);
	private BiomeRoomSize[] biomeRoomSizes;
	private float roomScaleMultiplier = 0.8f;
	private bool showRoomLabels = true;
	private int labelFontSize = 9;
	private float labelYOffset = 0.6f;
	private Color labelColor = Color.red;
	private float edgeWidth = 0.05f;
	private Color edgeColor = Color.red;
	private int overlapIterations = 40;
	private float overlapPadding = 0.18f;
	private bool showRegenerateButton = true;
	private string regenerateButtonText = "Regenerate";
	private Vector2 regenerateButtonPosition = new Vector2(16f, 16f);
	private Vector2 regenerateButtonSize = new Vector2(140f, 40f);
	private bool showBiomeLabel = true;
	private Vector2 biomeLabelPosition = new Vector2(16f, 64f);
	private Vector2 biomeLabelSize = new Vector2(240f, 24f);
	private bool showRoomInfoPanel = true;
	private Vector2 roomInfoPosition = new Vector2(16f, 96f);
	private Vector2 roomInfoSize = new Vector2(320f, 140f);
	[TextArea(6, 20)] private string lastOutput;

	private readonly List<GameObject> spawnedRooms = new List<GameObject>();
	private readonly List<GameObject> spawnedEdges = new List<GameObject>();
	private RoomInfo selectedRoom;
	private string currentBiome = "";
	private GameObject roomPreviewRoot;
	private static Sprite previewSprite;
	private readonly Dictionary<string, Sprite> creatureSpritesByName = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

	private void Start()
	{
		currentBiome = biomeToBeGenerated;
		if (generateOnStart)
		{
			GenerateEcosystem();
		}
	}

	private void Update()
	{
		HandleRoomClick();
	}

	private void OnGUI()
	{
		if (!showRegenerateButton)
		{
			return;
		}

		var rect = new Rect(regenerateButtonPosition, regenerateButtonSize);
		if (GUI.Button(rect, regenerateButtonText))
		{
			GenerateEcosystem();
		}

		if (showBiomeLabel)
		{
			var biomeRect = new Rect(biomeLabelPosition, biomeLabelSize);
			GUI.Label(biomeRect, $"Biome: {currentBiome}");
		}

		if (showRoomInfoPanel)
		{
			var infoRect = new Rect(roomInfoPosition, roomInfoSize);
			var text = selectedRoom == null
				? "Click a room to see creatures."
				: $"Room: {selectedRoom.label}\nZone: {selectedRoom.zoneType}\nSize: {selectedRoom.size}\nCreatures: {(selectedRoom.occupants != null && selectedRoom.occupants.Length > 0 ? string.Join(", ", selectedRoom.occupants) : "None")}\nNeighbors: {(selectedRoom.neighbors != null && selectedRoom.neighbors.Length > 0 ? string.Join(", ", selectedRoom.neighbors) : "None")}";
			GUI.Box(infoRect, text);
		}
	}

	[ContextMenu("Generate Ecosystem")]
	public void GenerateEcosystem()
	{
		var ecosystem = LoadEcosystem();
		if (ecosystem == null)
		{
			Debug.LogError("Ecosystem JSON not set or failed to parse.");
			return;
		}

		BuildCreatureSpriteLookup(ecosystem);

		var output = new StringBuilder();
		ClearSpawnedRooms();
		ClearSpawnedEdges();
		ClearRoomPreview();
		selectedRoom = null;
		currentBiome = biomeToBeGenerated;

		var roomCount = GetRoomCount();
		var positions = GenerateNoisePositions(roomCount);
		var graph = new Graph
		{
			nodes = CreateNodes(roomCount),
			adjacency = BuildAdjacencyByNearest(positions, roomCount, 2)
		};
		EnsureConnected(graph.adjacency, positions, 0, roomCount - 1);
		EnsureGoalIsTerminal(graph.adjacency, positions, 0, roomCount - 1);
		var assignment = AssignRooms(graph, ecosystem, biomeToBeGenerated);
		currentBiome = assignment.biome;
		var text = RenderRooms(graph, assignment.biome, assignment.rooms);

		output.AppendLine(text);

		if (spawnRoomObjects)
		{
			var maxDiameter = ComputeMaxRoomDiameter(positions);
			var scales = ComputeRoomScales(assignment.rooms, maxDiameter);
			var baseSize = GetPrefabBaseSize();
			ResolveOverlaps(positions, scales, baseSize);
			SpawnRooms(assignment.rooms, graph.adjacency, positions, scales);
			if (spawnEdges)
			{
				SpawnEdges(assignment.rooms, graph.adjacency, positions);
			}
		}

		lastOutput = output.ToString().TrimEnd();
		Debug.Log(lastOutput);
	}

	private void HandleRoomClick()
	{
		if (!spawnRoomObjects || !GetMouseClickDown())
		{
			return;
		}

		var cam = Camera.main;
		if (cam == null)
		{
			return;
		}

		var mousePos = GetMousePosition();
		var world = cam.ScreenToWorldPoint(mousePos);
		var hit2D = Physics2D.Raycast(world, Vector2.zero);
		if (hit2D.collider != null)
		{
			selectedRoom = hit2D.collider.GetComponentInParent<RoomInfo>();
			ShowRoomPreview(selectedRoom);
			return;
		}

		var ray = cam.ScreenPointToRay(mousePos);
		if (Physics.Raycast(ray, out var hit3D))
		{
			selectedRoom = hit3D.collider.GetComponentInParent<RoomInfo>();
			ShowRoomPreview(selectedRoom);
			return;
		}

		selectedRoom = null;
		ClearRoomPreview();
	}

	private static bool GetMouseClickDown()
	{
#if ENABLE_INPUT_SYSTEM
		return UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#else
		return Input.GetMouseButtonDown(0);
#endif
	}

	private static Vector3 GetMousePosition()
	{
#if ENABLE_INPUT_SYSTEM
		return UnityEngine.InputSystem.Mouse.current != null
			? (Vector3)UnityEngine.InputSystem.Mouse.current.position.ReadValue()
			: Vector3.zero;
#else
		return Input.mousePosition;
#endif
	}

	private EcosystemData LoadEcosystem()
	{
		if (ecosystemJson == null)
		{
			return null;
		}

		try
		{
			return JsonUtility.FromJson<EcosystemData>(ecosystemJson.text);
		}
		catch (Exception ex)
		{
			Debug.LogError($"Failed to parse ecosystem JSON: {ex.Message}");
			return null;
		}
	}

	private int GetRoomCount()
	{
		var min = Mathf.Max(2, roomMin);
		var max = Mathf.Max(2, roomMax);
		if (min > max)
		{
			var tmp = min;
			min = max;
			max = tmp;
		}

		return RandInt(min, max);
	}

	private List<RoomNode> CreateNodes(int roomCount)
	{
		var nodes = new List<RoomNode>(roomCount);
		for (var i = 0; i < roomCount; i++)
		{
			var label = i == 0 ? "START" : (i == roomCount - 1 ? "GOAL" : $"R{i}");
			nodes.Add(new RoomNode { id = i, label = label });
		}
		return nodes;
	}


	private Vector3[] GenerateNoisePositions(int count)
	{
		var positions = new Vector3[count];
		if (count == 0)
		{
			return positions;
		}

		var area = new Vector2(roomSpacing * count, roomSpacing * count);
		area.x = Mathf.Max(4f, area.x);
		area.y = Mathf.Max(4f, area.y);

		var candidateCount = Mathf.Max(count * noiseCandidateMultiplier, count + 8);
		var candidates = new List<(Vector3 pos, float score)>(candidateCount);
		var noiseOffset = UnityEngine.Random.insideUnitCircle * 1000f;
		for (var i = 0; i < candidateCount; i++)
		{
			var u = UnityEngine.Random.value;
			var v = UnityEngine.Random.value;
			var x = Mathf.Lerp(-area.x * 0.5f, area.x * 0.5f, u);
			var y = Mathf.Lerp(-area.y * 0.5f, area.y * 0.5f, v);
			var n = FractalNoise((x + noiseOffset.x) * noiseScale, (y + noiseOffset.y) * noiseScale);
			var score = n + UnityEngine.Random.Range(-noiseJitter, noiseJitter) * 0.1f;
			candidates.Add((new Vector3(x, y, 0f), score));
		}

		candidates.Sort((a, b) => b.score.CompareTo(a.score));
		var minSpacing = Mathf.Max(0.5f, noiseMinSpacing);
		var picked = new List<Vector3>(count);
		var candidateIndex = 0;
		while (picked.Count < count && candidateIndex < candidates.Count)
		{
			var candidate = candidates[candidateIndex].pos;
			candidateIndex++;
			if (IsFarEnough(candidate, picked, minSpacing))
			{
				picked.Add(candidate);
			}
		}

		while (picked.Count < count)
		{
			var fallback = new Vector3(
				UnityEngine.Random.Range(-area.x * 0.5f, area.x * 0.5f),
				UnityEngine.Random.Range(-area.y * 0.5f, area.y * 0.5f),
				0f);
			picked.Add(fallback);
		}

		for (var i = 0; i < count; i++)
		{
			positions[i] = picked[i];
		}

		return positions;
	}

	private float FractalNoise(float x, float y)
	{
		var amplitude = 0.5f;
		var frequency = 1f;
		var sum = 0f;
		var max = 0f;
		var octaves = Mathf.Max(1, noiseOctaves);
		for (var i = 0; i < octaves; i++)
		{
			sum += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
			max += amplitude;
			frequency *= Mathf.Max(1f, noiseLacunarity);
			amplitude *= Mathf.Clamp01(noiseGain);
		}

		return max > 0f ? sum / max : sum;
	}

	private static bool IsFarEnough(Vector3 candidate, List<Vector3> positions, float minDistance)
	{
		var minSq = minDistance * minDistance;
		for (var i = 0; i < positions.Count; i++)
		{
			if ((positions[i] - candidate).sqrMagnitude < minSq)
			{
				return false;
			}
		}
		return true;
	}

	private static List<HashSet<int>> BuildAdjacencyByNearest(Vector3[] positions, int nodeCount, int neighborsPerNode)
	{
		var adjacency = new List<HashSet<int>>(nodeCount);
		for (var i = 0; i < nodeCount; i++)
		{
			adjacency.Add(new HashSet<int>());
		}

		if (positions == null || positions.Length < nodeCount || nodeCount <= 1)
		{
			return adjacency;
		}

		var k = Mathf.Clamp(neighborsPerNode, 0, Mathf.Max(0, nodeCount - 1));
		for (var i = 0; i < nodeCount; i++)
		{
			var list = new List<(int id, float dist)>();
			for (var j = 0; j < nodeCount; j++)
			{
				if (i == j)
				{
					continue;
				}
				var dist = (positions[i] - positions[j]).sqrMagnitude;
				list.Add((j, dist));
			}

			list.Sort((a, b) => a.dist.CompareTo(b.dist));
			for (var n = 0; n < k && n < list.Count; n++)
			{
				var other = list[n].id;
				adjacency[i].Add(other);
				adjacency[other].Add(i);
			}
		}

		return adjacency;
	}

	private static void EnsureConnected(List<HashSet<int>> adjacency, Vector3[] positions, int startId, int goalId)
	{
		if (adjacency == null || positions == null || adjacency.Count == 0 || positions.Length < adjacency.Count)
		{
			return;
		}

		if (IsValidNode(startId, adjacency.Count) && IsValidNode(goalId, adjacency.Count))
		{
			adjacency[startId].Remove(goalId);
			adjacency[goalId].Remove(startId);
		}

		var components = GetComponents(adjacency);
		if (components.Count <= 1)
		{
			return;
		}

		// Connect components by linking the closest pair of nodes between components.
		while (components.Count > 1)
		{
			var bestA = -1;
			var bestB = -1;
			var bestDist = float.PositiveInfinity;

			for (var c = 0; c < components.Count; c++)
			{
				for (var d = c + 1; d < components.Count; d++)
				{
					var compA = components[c];
					var compB = components[d];
					for (var i = 0; i < compA.Count; i++)
					{
						var nodeA = compA[i];
						for (var j = 0; j < compB.Count; j++)
						{
							var nodeB = compB[j];
							if (IsForbiddenPair(nodeA, nodeB, startId, goalId))
							{
								continue;
							}
							var dist = (positions[nodeA] - positions[nodeB]).sqrMagnitude;
							if (dist < bestDist)
							{
								bestDist = dist;
								bestA = nodeA;
								bestB = nodeB;
							}
						}
					}
				}
			}

			if (bestA < 0 || bestB < 0)
			{
				break;
			}

			adjacency[bestA].Add(bestB);
			adjacency[bestB].Add(bestA);
			components = GetComponents(adjacency);
		}
	}

	private static void EnsureGoalIsTerminal(List<HashSet<int>> adjacency, Vector3[] positions, int startId, int goalId)
	{
		if (adjacency == null || positions == null || adjacency.Count == 0 || positions.Length < adjacency.Count)
		{
			return;
		}

		if (!IsValidNode(goalId, adjacency.Count))
		{
			return;
		}

		if (IsValidNode(startId, adjacency.Count))
		{
			adjacency[startId].Remove(goalId);
			adjacency[goalId].Remove(startId);
		}

		if (adjacency[goalId].Count == 0)
		{
			var nearest = FindNearestNode(goalId, positions, adjacency.Count, goalId);
			if (nearest >= 0)
			{
				adjacency[goalId].Add(nearest);
				adjacency[nearest].Add(goalId);
			}
		}

		if (adjacency[goalId].Count > 1)
		{
			var neighbors = new List<int>(adjacency[goalId]);
			var keep = GetClosestNeighbor(goalId, neighbors, positions);
			for (var i = 0; i < neighbors.Count; i++)
			{
				var neighbor = neighbors[i];
				if (neighbor == keep)
				{
					continue;
				}
				adjacency[goalId].Remove(neighbor);
				adjacency[neighbor].Remove(goalId);
			}
		}

		EnsureConnectedExcluding(adjacency, positions, goalId);

		if (adjacency[goalId].Count == 0)
		{
			var fallback = FindNearestNode(goalId, positions, adjacency.Count, goalId);
			if (fallback >= 0)
			{
				adjacency[goalId].Add(fallback);
				adjacency[fallback].Add(goalId);
			}
		}
	}

	private static void EnsureConnectedExcluding(List<HashSet<int>> adjacency, Vector3[] positions, int excludedId)
	{
		if (adjacency == null || positions == null || adjacency.Count == 0 || positions.Length < adjacency.Count)
		{
			return;
		}

		var components = GetComponentsExcluding(adjacency, excludedId);
		if (components.Count <= 1)
		{
			return;
		}

		while (components.Count > 1)
		{
			var bestA = -1;
			var bestB = -1;
			var bestDist = float.PositiveInfinity;

			for (var c = 0; c < components.Count; c++)
			{
				for (var d = c + 1; d < components.Count; d++)
				{
					var compA = components[c];
					var compB = components[d];
					for (var i = 0; i < compA.Count; i++)
					{
						var nodeA = compA[i];
						for (var j = 0; j < compB.Count; j++)
						{
							var nodeB = compB[j];
							var dist = (positions[nodeA] - positions[nodeB]).sqrMagnitude;
							if (dist < bestDist)
							{
								bestDist = dist;
								bestA = nodeA;
								bestB = nodeB;
							}
						}
					}
				}
			}

			if (bestA < 0 || bestB < 0)
			{
				break;
			}

			adjacency[bestA].Add(bestB);
			adjacency[bestB].Add(bestA);
			components = GetComponentsExcluding(adjacency, excludedId);
		}
	}

	private static List<List<int>> GetComponentsExcluding(List<HashSet<int>> adjacency, int excludedId)
	{
		var count = adjacency.Count;
		var visited = new bool[count];
		var components = new List<List<int>>();
		for (var i = 0; i < count; i++)
		{
			if (i == excludedId || visited[i])
			{
				continue;
			}

			var component = new List<int>();
			var stack = new Stack<int>();
			stack.Push(i);
			visited[i] = true;
			while (stack.Count > 0)
			{
				var node = stack.Pop();
				if (node == excludedId)
				{
					continue;
				}
				component.Add(node);
				foreach (var neighbor in adjacency[node])
				{
					if (neighbor == excludedId || visited[neighbor])
					{
						continue;
					}
					visited[neighbor] = true;
					stack.Push(neighbor);
				}
			}

			components.Add(component);
		}

		return components;
	}

	private static int FindNearestNode(int fromId, Vector3[] positions, int count, int excludeId)
	{
		var best = -1;
		var bestDist = float.PositiveInfinity;
		for (var i = 0; i < count; i++)
		{
			if (i == fromId || i == excludeId)
			{
				continue;
			}
			var dist = (positions[fromId] - positions[i]).sqrMagnitude;
			if (dist < bestDist)
			{
				bestDist = dist;
				best = i;
			}
		}

		return best;
	}

	private static int GetClosestNeighbor(int fromId, List<int> neighbors, Vector3[] positions)
	{
		var best = -1;
		var bestDist = float.PositiveInfinity;
		for (var i = 0; i < neighbors.Count; i++)
		{
			var neighbor = neighbors[i];
			var dist = (positions[fromId] - positions[neighbor]).sqrMagnitude;
			if (dist < bestDist)
			{
				bestDist = dist;
				best = neighbor;
			}
		}

		return best;
	}

	private static bool IsForbiddenPair(int a, int b, int startId, int goalId)
	{
		return (a == startId && b == goalId) || (a == goalId && b == startId);
	}

	private static bool IsValidNode(int id, int count)
	{
		return id >= 0 && id < count;
	}

	private string ResolveBiome(BiomeData[] biomes, string preferredBiome)
	{
		if (!string.IsNullOrWhiteSpace(preferredBiome))
		{
			for (var i = 0; i < biomes.Length; i++)
			{
				if (biomes[i] != null && string.Equals(biomes[i].name, preferredBiome, StringComparison.OrdinalIgnoreCase))
				{
					return biomes[i].name;
				}
			}
		}

		var picked = Pick(biomes);
		return picked != null && !string.IsNullOrWhiteSpace(picked.name) ? picked.name : "unknown";
	}

	private static BiomeData GetBiomeData(BiomeData[] biomes, string biomeName)
	{
		if (biomes == null || string.IsNullOrWhiteSpace(biomeName))
		{
			return null;
		}

		for (var i = 0; i < biomes.Length; i++)
		{
			if (biomes[i] != null && string.Equals(biomes[i].name, biomeName, StringComparison.OrdinalIgnoreCase))
			{
				return biomes[i];
			}
		}

		return null;
	}

	private static string PickZoneType(BiomeData biomeData)
	{
		if (biomeData?.zone_types == null || biomeData.zone_types.Length == 0)
		{
			return "unknown";
		}

		var zone = Pick(biomeData.zone_types);
		return string.IsNullOrWhiteSpace(zone) ? "unknown" : zone;
	}

	private CreatureData PickCreatureWeighted(List<CreatureData> pool)
	{
		if (pool == null || pool.Count == 0)
		{
			return null;
		}

		var bias = Mathf.Clamp(creatureCrWeightExponent, 0.000001f, 1000000f);
		var minCr = float.PositiveInfinity;
		var maxCr = float.NegativeInfinity;
		for (var i = 0; i < pool.Count; i++)
		{
			if (pool[i] == null)
			{
				continue;
			}
			var cr = pool[i].cr;
			if (cr < minCr)
			{
				minCr = cr;
			}
			if (cr > maxCr)
			{
				maxCr = cr;
			}
		}

		if (float.IsInfinity(minCr) || float.IsInfinity(maxCr))
		{
			return pool[RandInt(0, pool.Count - 1)];
		}

		var total = 0f;
		for (var i = 0; i < pool.Count; i++)
		{
			total += GetCreatureWeight(pool[i], minCr, maxCr, bias);
		}

		if (total <= 0f)
		{
			return pool[RandInt(0, pool.Count - 1)];
		}

		var roll = UnityEngine.Random.value * total;
		for (var i = 0; i < pool.Count; i++)
		{
			roll -= GetCreatureWeight(pool[i], minCr, maxCr, bias);
			if (roll <= 0f)
			{
				return pool[i];
			}
		}

		return pool[pool.Count - 1];
	}

	private static float GetCreatureWeight(CreatureData creature, float minCr, float maxCr, float bias)
	{
		if (creature == null)
		{
			return 0f;
		}

		var range = Mathf.Max(0.000001f, maxCr - minCr);
		var t = Mathf.Clamp01((creature.cr - minCr) / range);
		var logBias = Mathf.Log(bias);
		return Mathf.Exp(logBias * t);
	}

	private static List<List<int>> GetComponents(List<HashSet<int>> adjacency)
	{
		var count = adjacency.Count;
		var visited = new bool[count];
		var components = new List<List<int>>();
		for (var i = 0; i < count; i++)
		{
			if (visited[i])
			{
				continue;
			}

			var component = new List<int>();
			var stack = new Stack<int>();
			stack.Push(i);
			visited[i] = true;
			while (stack.Count > 0)
			{
				var node = stack.Pop();
				component.Add(node);
				foreach (var neighbor in adjacency[node])
				{
					if (!visited[neighbor])
					{
						visited[neighbor] = true;
						stack.Push(neighbor);
					}
				}
			}
			components.Add(component);
		}

		return components;
	}


	private RoomAssignment AssignRooms(Graph graph, EcosystemData ecosystem, string preferredBiome)
	{
		if (ecosystem == null || ecosystem.biomes == null || ecosystem.biomes.Length == 0)
		{
			return new RoomAssignment { biome = "unknown", rooms = new List<RoomData>() };
		}

		var biome = ResolveBiome(ecosystem.biomes, preferredBiome);
		var biomeData = GetBiomeData(ecosystem.biomes, biome);
		var options = new List<CreatureData>();
		if (ecosystem.creatures != null)
		{
			foreach (var creature in ecosystem.creatures)
			{
				if (creature == null || creature.population_range == null || creature.population_range.Length < 2)
				{
					continue;
				}

				if (creature.population_range[1] <= 0)
				{
					continue;
				}

				if (HasBiome(creature, biome))
				{
					options.Add(creature);
				}
			}
		}

		var rooms = new List<RoomData>(graph.nodes.Count);
		foreach (var node in graph.nodes)
		{
			var size = DetermineRoomSize(node, graph.adjacency);
			var zoneType = PickZoneType(biomeData);
			var maxPick = Mathf.Min(3, options.Count);
			var selectedCount = maxPick > 0 ? RandInt(1, maxPick) : 0;
			var selected = new List<CreatureData>();
			var pool = new List<CreatureData>(options);
			for (var i = 0; i < selectedCount; i++)
			{
				var pick = PickCreatureWeighted(pool);
				if (pick == null)
				{
					break;
				}
				selected.Add(pick);
				pool.Remove(pick);
			}

			var occupants = new List<string>();
			foreach (var creature in selected)
			{
				var minPop = Mathf.Max(1, creature.population_range[0]);
				var maxPop = Mathf.Max(minPop, creature.population_range[1]);
				var popMin = Mathf.Max(1, Mathf.RoundToInt(minPop * Mathf.Max(0.1f, creaturePopulationMultiplier)));
				var popMax = Mathf.Max(popMin, Mathf.RoundToInt(maxPop * Mathf.Max(0.1f, creaturePopulationMultiplier)));
				var pop = RandInt(popMin, popMax);
				occupants.Add($"{creature.name} x{pop}");
			}

			rooms.Add(new RoomData
			{
				id = node.id,
				label = node.label,
				biome = biome,
				zoneType = zoneType,
				occupants = occupants,
				size = size
			});
		}

		return new RoomAssignment { biome = biome, rooms = rooms };
	}

	private string RenderRooms(Graph graph, string biome, List<RoomData> rooms)
	{
		var lines = new List<string>
		{
			"Rooms and Connections:",
			$"Biome: {biome}"
		};

		foreach (var room in rooms)
		{
			var neighbors = new List<string>();
			lines.Add($"  Zone: {room.zoneType}");
			foreach (var neighborId in graph.adjacency[room.id])
			{
				neighbors.Add(rooms[neighborId].label);
			}

			neighbors.Sort();

			lines.Add(string.Empty);
			lines.Add(room.label);
			lines.Add($"  Creatures: {(room.occupants.Count > 0 ? string.Join(", ", room.occupants) : "None")}");
			lines.Add($"  Adjacent: {string.Join(", ", neighbors)}");
		}

		return string.Join("\n", lines);
	}

	private void SpawnRooms(List<RoomData> rooms, List<HashSet<int>> adjacency, Vector3[] positions, float[] scales)
	{
		if (rooms == null || rooms.Count == 0)
		{
			return;
		}

		var parent = roomsParent != null ? roomsParent : transform;
		for (var i = 0; i < rooms.Count; i++)
		{
			var position = positions != null && i < positions.Length ? positions[i] : Vector3.zero;

			var roomObj = roomPrefab != null
				? Instantiate(roomPrefab, position, Quaternion.identity, parent)
				: new GameObject(rooms[i].label);

			roomObj.transform.position = position;
			roomObj.name = rooms[i].label;
			var scaleValue = Mathf.Max(0.01f, uniformRoomScale);
			roomObj.transform.localScale = new Vector3(scaleValue, scaleValue, 1f);

			var info = roomObj.GetComponent<RoomInfo>();
			if (info == null)
			{
				info = roomObj.AddComponent<RoomInfo>();
			}

			info.id = rooms[i].id;
			info.label = rooms[i].label;
			info.biome = rooms[i].biome;
			info.zoneType = rooms[i].zoneType;
			info.size = rooms[i].size;
			info.occupants = rooms[i].occupants.ToArray();
			info.neighbors = GetNeighborLabels(rooms, adjacency, rooms[i].id);
			if (showRoomLabels)
			{
				EnsureLabel(roomObj.transform, rooms[i].label);
			}

			if (roomPrefab == null)
			{
				roomObj.transform.SetParent(parent);
			}

			spawnedRooms.Add(roomObj);
		}
	}

	private void SpawnEdges(List<RoomData> rooms, List<HashSet<int>> adjacency, Vector3[] positions)
	{
		if (rooms == null || rooms.Count == 0 || adjacency == null || positions == null)
		{
			return;
		}

		var parent = edgesParent != null ? edgesParent : transform;
		var created = new HashSet<string>();
		for (var i = 0; i < rooms.Count; i++)
		{
			foreach (var neighborId in adjacency[i])
			{
				var key = EdgeKey(i, neighborId);
				if (created.Contains(key))
				{
					continue;
				}

				created.Add(key);
				var lineObj = new GameObject($"Edge_{rooms[i].label}_{rooms[neighborId].label}");
				lineObj.transform.SetParent(parent, false);
				var line = lineObj.AddComponent<LineRenderer>();
				line.useWorldSpace = true;
				line.startWidth = edgeWidth;
				line.endWidth = edgeWidth;
				line.startColor = edgeColor;
				line.endColor = edgeColor;
				line.material = new Material(Shader.Find("Sprites/Default"));
				line.sortingOrder = -1;

				var startPos = positions[i];
				var endPos = positions[neighborId];
				line.positionCount = 2;
				line.SetPosition(0, startPos);
				line.SetPosition(1, endPos);
				spawnedEdges.Add(lineObj);
			}
		}
	}



	private float ComputeRoomScale(RoomData room, float maxDiameter)
	{
		if (room == null)
		{
			return 1f;
		}

		var range = GetRoomSizeRange(room.biome);
		var sizeValue = RandFloat(range.x, range.y) * Mathf.Max(0.01f, roomScaleMultiplier) * GetRoomSizeMultiplier(room.size);
		if (maxDiameter > 0.01f)
		{
			var maxScale = maxDiameter * 0.9f;
			sizeValue = Mathf.Min(sizeValue, maxScale);
		}
		return Mathf.Max(0.05f, sizeValue);
	}

	private float[] ComputeRoomScales(List<RoomData> rooms, float maxDiameter)
	{
		var count = rooms?.Count ?? 0;
		var scales = new float[count];
		for (var i = 0; i < count; i++)
		{
			scales[i] = ComputeRoomScale(rooms[i], maxDiameter);
		}
		return scales;
	}

	private void ResolveOverlaps(Vector3[] positions, float[] scales, Vector2 baseSize)
	{
		if (positions == null || scales == null || positions.Length != scales.Length)
		{
			return;
		}

		var count = positions.Length;
		if (count <= 1)
		{
			return;
		}

		var baseDiameter = Mathf.Max(0.1f, Mathf.Max(baseSize.x, baseSize.y));
		for (var iter = 0; iter < Mathf.Max(1, overlapIterations); iter++)
		{
			var moved = false;
			for (var i = 0; i < count; i++)
			{
				for (var j = i + 1; j < count; j++)
				{
					var delta = positions[j] - positions[i];
					var dist = delta.magnitude;
					var radiusA = scales[i] * baseDiameter * 0.5f;
					var radiusB = scales[j] * baseDiameter * 0.5f;
					var minDist = radiusA + radiusB + overlapPadding;
					if (dist < minDist)
					{
						var dir = dist > 0.001f
							? delta / dist
							: new Vector3(UnityEngine.Random.insideUnitCircle.normalized.x, UnityEngine.Random.insideUnitCircle.normalized.y, 0f);
						var push = (minDist - dist) * 0.5f;
						positions[i] -= (Vector3)(dir * push);
						positions[j] += (Vector3)(dir * push);
						moved = true;
					}
				}
			}

			if (!moved)
			{
				break;
			}
		}
	}

	private Vector2 GetPrefabBaseSize()
	{
		if (roomPrefab == null)
		{
			return Vector2.one;
		}

		var sprite = roomPrefab.GetComponentInChildren<SpriteRenderer>();
		if (sprite != null && sprite.sprite != null)
		{
			var size = sprite.sprite.bounds.size;
			return new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
		}

		var renderer = roomPrefab.GetComponentInChildren<Renderer>();
		if (renderer != null)
		{
			var worldSize = renderer.bounds.size;
			return new Vector2(Mathf.Max(0.001f, worldSize.x), Mathf.Max(0.001f, worldSize.y));
		}

		return Vector2.one;
	}

	private float ComputeMaxRoomDiameter(Vector3[] positions)
	{
		if (positions == null || positions.Length < 2)
		{
			return 0f;
		}

		var minSq = float.PositiveInfinity;
		for (var i = 0; i < positions.Length; i++)
		{
			for (var j = i + 1; j < positions.Length; j++)
			{
				var distSq = (positions[i] - positions[j]).sqrMagnitude;
				if (distSq < minSq)
				{
					minSq = distSq;
				}
			}
		}

		if (!float.IsFinite(minSq) || minSq <= 0f)
		{
			return 0f;
		}

		return Mathf.Sqrt(minSq);
	}


	private static float GetRoomSizeMultiplier(RoomSize size)
	{
		switch (size)
		{
			case RoomSize.Large:
				return 1.35f;
			case RoomSize.Small:
				return 0.85f;
			default:
				return 1f;
		}
	}

	private static RoomSize DetermineRoomSize(RoomNode node, List<HashSet<int>> adjacency)
	{
		if (node == null || adjacency == null || node.id < 0 || node.id >= adjacency.Count)
		{
			return RoomSize.Medium;
		}

		if (node.label == "START" || node.label == "GOAL")
		{
			return RoomSize.Large;
		}

		var degree = adjacency[node.id].Count;
		if (degree >= 3)
		{
			return RoomSize.Large;
		}
		if (degree == 2)
		{
			return RoomSize.Medium;
		}
		return RoomSize.Small;
	}

	private void EnsureLabel(Transform roomTransform, string label)
	{
		var labelTransform = roomTransform.Find("Label");
		TextMesh textMesh;
		if (labelTransform == null)
		{
			var labelObj = new GameObject("Label");
			labelObj.transform.SetParent(roomTransform, false);
			labelObj.transform.localPosition = new Vector3(0f, labelYOffset, 0f);
			textMesh = labelObj.AddComponent<TextMesh>();
			textMesh.anchor = TextAnchor.MiddleCenter;
			textMesh.alignment = TextAlignment.Center;
		}
		else
		{
			textMesh = labelTransform.GetComponent<TextMesh>();
			if (textMesh == null)
			{
				textMesh = labelTransform.gameObject.AddComponent<TextMesh>();
				textMesh.anchor = TextAnchor.MiddleCenter;
				textMesh.alignment = TextAlignment.Center;
			}
		}

		textMesh.text = label;
		textMesh.fontSize = labelFontSize;
		textMesh.color = labelColor;
	}

	private Vector2 GetRoomSizeRange(string biome)
	{
		if (biomeRoomSizes != null)
		{
			for (var i = 0; i < biomeRoomSizes.Length; i++)
			{
				if (string.IsNullOrWhiteSpace(biomeRoomSizes[i].biomeName))
				{
					continue;
				}

				if (string.Equals(biomeRoomSizes[i].biomeName, biome, StringComparison.OrdinalIgnoreCase))
				{
					return biomeRoomSizes[i].sizeRange;
				}
			}
		}

		return defaultRoomSizeRange;
	}

	private string[] GetNeighborLabels(List<RoomData> rooms, List<HashSet<int>> adjacency, int id)
	{
		var labels = new List<string>();
		foreach (var neighborId in adjacency[id])
		{
			labels.Add(rooms[neighborId].label);
		}
		labels.Sort();
		return labels.ToArray();
	}

	private void ClearSpawnedRooms()
	{
		for (var i = spawnedRooms.Count - 1; i >= 0; i--)
		{
			if (spawnedRooms[i] != null)
			{
				Destroy(spawnedRooms[i]);
			}
		}

		spawnedRooms.Clear();
	}

	private void ClearSpawnedEdges()
	{
		for (var i = spawnedEdges.Count - 1; i >= 0; i--)
		{
			if (spawnedEdges[i] != null)
			{
				Destroy(spawnedEdges[i]);
			}
		}

		spawnedEdges.Clear();
	}

	private void ShowRoomPreview(RoomInfo room)
	{
		if (!enableRoomPreview)
		{
			return;
		}

		if (room == null)
		{
			ClearRoomPreview();
			return;
		}

		var root = GetOrCreatePreviewRoot();
		ClearPreviewChildren(root.transform);

		var floor = new GameObject("PreviewFloor");
		floor.transform.SetParent(root.transform, false);
		floor.transform.localPosition = new Vector3(0f, 0f, previewZOffset);
		floor.transform.localScale = new Vector3(previewRoomSize.x, previewRoomSize.y, 1f);
		var floorRenderer = floor.AddComponent<SpriteRenderer>();
		floorRenderer.sprite = GetPreviewSprite(previewRoomSprite);
		floorRenderer.color = previewRoomColor;
		floorRenderer.sortingOrder = previewSortingOrder;

		var header = $"{room.label} ({room.biome})";
		CreatePreviewText(root.transform, "PreviewHeader", header,
			new Vector3(0f, previewRoomSize.y * 0.5f + previewLabelOffset, previewZOffset - 0.1f),
			previewTextColor, previewSortingOrder + 2, 0.18f);

		var occupants = room.occupants ?? Array.Empty<string>();
		if (occupants.Length == 0)
		{
			CreatePreviewText(root.transform, "PreviewEmpty", "Empty",
				new Vector3(0f, 0f, previewZOffset - 0.1f),
				previewTextColor, previewSortingOrder + 2, 0.16f);
			return;
		}

		var innerWidth = Mathf.Max(0.1f, previewRoomSize.x - previewPadding * 2f);
		var innerHeight = Mathf.Max(0.1f, previewRoomSize.y - previewPadding * 2f);
		var cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(occupants.Length)));
		var rows = Mathf.Max(1, Mathf.CeilToInt((float)occupants.Length / cols));
		var cellW = innerWidth / cols;
		var cellH = innerHeight / rows;
		var iconSize = Mathf.Min(cellW, cellH) * Mathf.Clamp01(previewCreatureScale) + 1.0f;

		for (var i = 0; i < occupants.Length; i++)
		{
			var col = i % cols;
			var row = i / cols;
			var x = -innerWidth * 0.5f + cellW * (col + 0.5f);
			var y = innerHeight * 0.5f - cellH * (row + 0.5f);

			var icon = new GameObject($"Creature_{occupants[i]}");
			icon.transform.SetParent(root.transform, false);
			icon.transform.localPosition = new Vector3(x, y, previewZOffset - 0.05f);
			var renderer = icon.AddComponent<SpriteRenderer>();
			var sprite = GetCreatureSprite(occupants[i]);
			renderer.sprite = sprite;
			var scale = iconSize;
			if (sprite != null)
			{
				var spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
				if (spriteSize > 0.0001f)
				{
					scale = iconSize / spriteSize;
				}
			}
			icon.transform.localScale = new Vector3(scale, scale, 1f);
			var baseName = GetOccupantBaseName(occupants[i]);
			renderer.color = HashToColor(baseName);
			renderer.sortingOrder = previewSortingOrder + 1;
		}
	}

	private void BuildCreatureSpriteLookup(EcosystemData ecosystem)
	{
		creatureSpritesByName.Clear();
		if (ecosystem?.creatures == null)
		{
			return;
		}

		foreach (var creature in ecosystem.creatures)
		{
			if (creature == null || string.IsNullOrWhiteSpace(creature.name))
			{
				continue;
			}

			if (string.IsNullOrWhiteSpace(creature.spritePath))
			{
				continue;
			}

			var sprite = Resources.Load<Sprite>(creature.spritePath);
			if (sprite != null)
			{
				creatureSpritesByName[creature.name] = sprite;
			}
		}
	}

	private GameObject GetOrCreatePreviewRoot()
	{
		if (roomPreviewRoot == null)
		{
			roomPreviewRoot = new GameObject("RoomPreview");
			roomPreviewRoot.transform.SetParent(transform, false);
		}

		roomPreviewRoot.transform.position = previewAnchor;
		return roomPreviewRoot;
	}

	private void ClearRoomPreview()
	{
		if (roomPreviewRoot == null)
		{
			return;
		}

		ClearPreviewChildren(roomPreviewRoot.transform);
	}

	private static void ClearPreviewChildren(Transform root)
	{
		if (root == null)
		{
			return;
		}

		for (var i = root.childCount - 1; i >= 0; i--)
		{
			var child = root.GetChild(i);
			if (child != null)
			{
				Destroy(child.gameObject);
			}
		}
	}

	private Sprite GetCreatureSprite(string creatureName)
	{
		var key = GetOccupantBaseName(creatureName);
		if (!string.IsNullOrWhiteSpace(key) && creatureSpritesByName.TryGetValue(key, out var mapped) && mapped != null)
		{
			return mapped;
		}

		if (!string.IsNullOrWhiteSpace(key) && creatureIcons != null)
		{
			for (var i = 0; i < creatureIcons.Count; i++)
			{
				if (creatureIcons[i].sprite == null)
				{
					continue;
				}
				if (string.Equals(creatureIcons[i].creatureName, key, StringComparison.OrdinalIgnoreCase))
				{
					return creatureIcons[i].sprite;
				}
			}
		}

		if (previewFallbackCreatureSprite != null)
		{
			return previewFallbackCreatureSprite;
		}

		return GetPreviewSprite(null);
	}

	private static string GetOccupantBaseName(string occupant)
	{
		if (string.IsNullOrWhiteSpace(occupant))
		{
			return string.Empty;
		}

		var trimmed = occupant.Trim();
		var idx = trimmed.LastIndexOf(" x", StringComparison.OrdinalIgnoreCase);
		if (idx > 0)
		{
			return trimmed.Substring(0, idx).Trim();
		}

		return trimmed;
	}

	private static Sprite GetPreviewSprite(Sprite overrideSprite)
	{
		if (overrideSprite != null)
		{
			return overrideSprite;
		}

		if (previewSprite != null)
		{
			return previewSprite;
		}

		var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
		tex.SetPixel(0, 0, Color.white);
		tex.Apply();
		previewSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
		previewSprite.name = "RoomPreviewSprite";
		return previewSprite;
	}

	private void CreatePreviewText(Transform parent, string name, string text, Vector3 localPos, Color color, int sortingOrder, float characterSize)
	{
		var textObj = new GameObject(name);
		textObj.transform.SetParent(parent, false);
		textObj.transform.localPosition = localPos;
		var mesh = textObj.AddComponent<TextMesh>();
		mesh.text = text;
		mesh.color = color;
		mesh.anchor = TextAnchor.MiddleCenter;
		mesh.alignment = TextAlignment.Center;
		mesh.characterSize = characterSize;
		mesh.fontSize = 48;
		var renderer = textObj.GetComponent<MeshRenderer>();
		renderer.sortingOrder = sortingOrder;
	}

	private static Color HashToColor(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return new Color(0.8f, 0.8f, 0.8f);
		}

		var hash = value.GetHashCode();
		var hue = Mathf.Abs(hash % 360) / 360f;
		return Color.HSVToRGB(hue, 0.6f, 0.95f);
	}

	[Serializable]
	private struct CreatureIcon
	{
		public string creatureName;
		public Sprite sprite;
	}



	private static int RandInt(int min, int max)
	{
		if (max < min)
		{
			var tmp = min;
			min = max;
			max = tmp;
		}

		return UnityEngine.Random.Range(min, max + 1);
	}

	private static float RandFloat(float min, float max)
	{
		if (max < min)
		{
			var tmp = min;
			min = max;
			max = tmp;
		}

		return UnityEngine.Random.Range(min, max);
	}

	private static T Pick<T>(T[] array)
	{
		if (array == null || array.Length == 0)
		{
			return default;
		}

		return array[RandInt(0, array.Length - 1)];
	}

	private static bool HasBiome(CreatureData creature, string biome)
	{
		if (creature == null || creature.biomes == null)
		{
			return false;
		}

		for (var i = 0; i < creature.biomes.Length; i++)
		{
			if (string.Equals(creature.biomes[i], biome, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private static string EdgeKey(int a, int b)
	{
		var x = Mathf.Min(a, b);
		var y = Mathf.Max(a, b);
		return $"{x}-{y}";
	}


	[Serializable]
	private class Graph
	{
		public List<RoomNode> nodes;
		public List<HashSet<int>> adjacency;
	}

	[Serializable]
	private class RoomNode
	{
		public int id;
		public string label;
	}

	[Serializable]
	private class RoomAssignment
	{
		public string biome;
		public List<RoomData> rooms;
	}

	[Serializable]
	public class RoomData
	{
		public int id;
		public string label;
		public string biome;
		public string zoneType;
		public List<string> occupants;
		public RoomSize size;
	}

	public enum RoomSize
	{
		Small,
		Medium,
		Large
	}

	[Serializable]
	private class EcosystemData
	{
		public CreatureData[] creatures;
		public BiomeData[] biomes;
	}

	[Serializable]
	private class CreatureData
	{
		public string name;
		public string[] biomes;
		public float cr;
		public string role;
		public int[] population_range;
		public string spritePath;
	}

	[Serializable]
	private class BiomeData
	{
		public string name;
		public string description;
		public string light_level;
		public string water_level;
		public string temperature;
		public string[] primary_resources;
		public string[] zone_types;
	}

	[Serializable]
	private struct BiomeRoomSize
	{
		public string biomeName;
		public Vector2 sizeRange;
	}
}

public class RoomInfo : MonoBehaviour
{
	public int id;
	public string label;
	public string biome;
	public string zoneType;
	public dungeonGenerator.RoomSize size;
	public string[] occupants;
	public string[] neighbors;
}
