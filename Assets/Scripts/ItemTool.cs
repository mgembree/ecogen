using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GeneratedItem
{
    public string Name;
    public string Rarity;
    public List<string> PrefixModifiers = new List<string>();
    public List<string> SuffixModifiers = new List<string>();
    public List<string> PrefixModifierRarities = new List<string>();
    public List<string> SuffixModifierRarities = new List<string>();
}

[System.Serializable]
public class ModifierRange
{
    public int min;
    public int max;
}

[System.Serializable]
public class ModifierDefinition
{
    public string template;
    public string namePrefix;
    public string nameSuffix;
    public ModifierRange rare;
    public ModifierRange epic;
    public ModifierRange legendary;
    public List<string> allowedBaseNames;
}

[System.Serializable]
public class BaseNameCategory
{
    public string name;
    public List<string> baseNames;
}

[System.Serializable]
public class ItemNameData
{
    public List<string> baseNames;
    public List<BaseNameCategory> categories;
    public ModifierDefinition[] prefix;
    public ModifierDefinition[] suffix;
}

public class ItemTool : MonoBehaviour
{
    public enum ExclusiveGenerationMode
    {
        Any = 0,
        BaseName = 1,
        Category = 2
    }
    #region Rarity Constants

    public const string RarityCommon = "Common";
    public const string RarityRare = "Rare";
    public const string RarityEpic = "Epic";
    public const string RarityLegendary = "Legendary";

    public static string TierToRarity(int tier)
    {
        if (tier <= 1)
        {
            return RarityCommon;
        }

        if (tier == 2)
        {
            return RarityRare;
        }

        if (tier == 3)
        {
            return RarityEpic;
        }

        return RarityLegendary;
    }

    public static int RarityToTier(string rarity)
    {
        if (string.Equals(rarity, RarityLegendary, System.StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (string.Equals(rarity, RarityEpic, System.StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (string.Equals(rarity, RarityRare, System.StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }

    #endregion

    #region Nested Types

    [System.Serializable]
    public struct ItemGenerationOptions
    {
        [Tooltip("Override the base name (e.g., Sword). Leave empty for random.")]
        public string BaseNameOverride;

        [Tooltip("Override the base name category (e.g., Weapons). Leave empty for random.")]
        public string BaseNameCategoryOverride;

        [Tooltip("Override the loot luck. Use a negative value to use the tool's lootLuck.")]
        public int LootLuckOverride;

        [Tooltip("Override the rarity (Common/Rare/Epic/Legendary). Leave empty for roll.")]
        public string RarityOverride;
    }

    [System.Serializable]
    public class LuckMultipliers
    {
        [Tooltip("How much more likely Common items drop at max luck.")]
        public float common = 1f;

        [Tooltip("How much more likely Rare items drop at max luck.")]
        public float rare = 10f;

        [Tooltip("How much more likely Epic items drop at max luck.")]
        public float epic = 70f;

        [Tooltip("How much more likely Legendary items drop at max luck.")]
        public float legendary = 250f;
    }

    [System.Serializable]
    private class LegacyItemNameData
    {
        public List<string> baseNames;
        public ModifierDefinition[] prefixModifiers;
        public ModifierDefinition[] suffixModifiers;
    }

    #endregion

    #region Inspector Fields

    [Header("Generation")]
    public int itemsToGenerate = 10;
    
    [Header("Data Source")]
    [Tooltip("Path to the JSON file (relative to Assets folder)")]
    public string jsonFilePath = "Scripts/ItemNames.json";
    
    [Header("Rarity Weights")]
    [Tooltip("Base weight for Common rarity.")]
    public float commonWeight = 60f;

    [Tooltip("Base weight for Rare rarity.")]
    public float rareWeight = 25f;

    [Tooltip("Base weight for Epic rarity.")]
    public float epicWeight = 10f;

    [Tooltip("Base weight for Legendary rarity.")]
    public float legendaryWeight = 5f;

    [Header("Luck Bias")]
    [Range(0, 2500)]
    [Tooltip("Loot luck value (higher = better rarity chances).")]
    public int lootLuck = 0;

    [SerializeField]
    public LuckMultipliers luckMultipliers = new LuckMultipliers();

    [Header("Modifier Rarity Weights")]
    [Tooltip("Base weight for Rare modifier rarity.")]
    public float modifierRareWeight = 60f;

    [Tooltip("Base weight for Epic modifier rarity.")]
    public float modifierEpicWeight = 30f;

    [Tooltip("Base weight for Legendary modifier rarity.")]
    public float modifierLegendaryWeight = 10f;


    [Header("Item Type Filter")]
    [SerializeField]
    private ExclusiveGenerationMode exclusiveGenerationMode = ExclusiveGenerationMode.Any;

    [SerializeField]
    private bool useSpecificBaseName = false;

    [SerializeField, HideInInspector]
    private string specificBaseName = string.Empty;

    [SerializeField]
    private bool useSpecificBaseCategory = false;

    [SerializeField, HideInInspector]
    private string specificBaseCategory = string.Empty;

    #endregion

    #region Private Fields

    private ItemNameData nameData;
    public List<GeneratedItem> generatedHistory;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        LoadNameData();
    }

    #endregion

    #region Public Menu Commands

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (nameData == null)
        {
            LoadNameData();
        }
        
        if (nameData == null)
        {
            Debug.LogError("ItemNameData could not be loaded! Check the JSON file path.");
            return;
        }

        if (!ValidateNameData())
        {
            return;
        }
        
        generatedHistory = new List<GeneratedItem>(); // Clear old list
        
        for(int i = 0; i < itemsToGenerate; i++)
        {
            GeneratedItem newItem = GenerateItem();
            generatedHistory.Add(newItem);
        }
    }

    [ContextMenu("Show Rarity Percentages")]
    public void ShowRarityPercentages()
    {
        GetAdjustedRarityWeights(lootLuck, out float adjustedCommon, out float adjustedRare, out float adjustedEpic, out float adjustedLegendary);
        
        float totalWeight = adjustedCommon + adjustedRare + adjustedEpic + adjustedLegendary;
        
        float commonPercent = (adjustedCommon / totalWeight) * 100f;
        float rarePercent = (adjustedRare / totalWeight) * 100f;
        float epicPercent = (adjustedEpic / totalWeight) * 100f;
        float legendaryPercent = (adjustedLegendary / totalWeight) * 100f;
        Debug.Log($"Rarity Chances (Loot Luck {lootLuck}): " +
                  $"{RarityCommon} {commonPercent:F2}%, " +
                  $"{RarityRare} {rarePercent:F2}%, " +
                  $"{RarityEpic} {epicPercent:F2}%, " +
                  $"{RarityLegendary} {legendaryPercent:F2}%");
    }

    #endregion

    #region Public API

    public GeneratedItem GenerateSingleItem()
    {
        return GenerateItem();
    }

    public GeneratedItem GenerateSingleItem(ItemGenerationOptions options)
    {
        return GenerateItem(options);
    }

    public string RollRarityForLootLuck(int lootLuckOverride = -1)
    {
        int resolvedLootLuck = lootLuckOverride >= 0 ? lootLuckOverride : lootLuck;
        return DetermineRarity(resolvedLootLuck);
    }

    public List<string> GetAvailableBaseNames()
    {
        if (nameData == null)
        {
            LoadNameData();
        }

        return nameData != null ? nameData.baseNames : null;
    }

    public List<string> GetAvailableBaseCategories()
    {
        if (nameData == null)
        {
            LoadNameData();
        }

        if (nameData == null || nameData.categories == null || nameData.categories.Count == 0)
        {
            return new List<string>();
        }

        List<string> categories = new List<string>();
        for (int i = 0; i < nameData.categories.Count; i++)
        {
            BaseNameCategory category = nameData.categories[i];
            if (category != null && !string.IsNullOrWhiteSpace(category.name))
            {
                categories.Add(category.name);
            }
        }

        return categories;
    }

    public void ExportItemToJson(GeneratedItem item, string folderPath = "ExportedItems")
    {
        if (item == null)
        {
            Debug.LogError("Cannot export null item.");
            return;
        }

        string json = JsonUtility.ToJson(item, true);
        
        // Create folder if it doesn't exist
        string fullFolderPath = System.IO.Path.Combine(Application.dataPath, folderPath);
        if (!System.IO.Directory.Exists(fullFolderPath))
        {
            System.IO.Directory.CreateDirectory(fullFolderPath);
        }

        // Generate filename from item name (sanitized)
        string sanitizedName = SanitizeFileName(item.Name);
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{sanitizedName}_{timestamp}.json";
        string fullPath = System.IO.Path.Combine(fullFolderPath, fileName);

        // Write to file
        System.IO.File.WriteAllText(fullPath, json);
        
        Debug.Log($"Item exported to: {fullPath}");
        
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    private string SanitizeFileName(string fileName)
    {
        char[] invalids = System.IO.Path.GetInvalidFileNameChars();
        string sanitized = string.Join("_", fileName.Split(invalids, System.StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        return sanitized;
    }

    #endregion

    #region Data Loading

    private void LoadNameData()
    {
        // Try loading directly from Assets folder first
        string fullPath = System.IO.Path.Combine(Application.dataPath, jsonFilePath);
        if (System.IO.File.Exists(fullPath))
        {
            string jsonContent = System.IO.File.ReadAllText(fullPath);
            nameData = ParseNameData(jsonContent);
        }
        else
        {
            // Fallback: try Resources if placed under Assets/Resources
            string resourcesPath = jsonFilePath.Replace(".json", "").Replace("Resources/", "");
            TextAsset jsonFile = Resources.Load<TextAsset>(resourcesPath);
            if (jsonFile != null)
            {
                nameData = ParseNameData(jsonFile.text);
            }
            else
            {
                Debug.LogError("Could not find JSON file at: " + fullPath + " or in Resources at: " + resourcesPath);
            }
        }

        ValidateNameData();
    }

    private ItemNameData ParseNameData(string jsonContent)
    {
        ItemNameData parsed = JsonUtility.FromJson<ItemNameData>(jsonContent);
        if (IsNameDataValidShape(parsed))
        {
            return parsed;
        }

        LegacyItemNameData legacyParsed = JsonUtility.FromJson<LegacyItemNameData>(jsonContent);
        if (IsLegacyNameDataValidShape(legacyParsed))
        {
            return new ItemNameData
            {
                baseNames = legacyParsed.baseNames,
                prefix = legacyParsed.prefixModifiers,
                suffix = legacyParsed.suffixModifiers
            };
        }

        return parsed;
    }

    #endregion

    #region Data Validation

    private bool ValidateNameData()
    {
        if (nameData == null)
        {
            Debug.LogError("ItemNameData is null. JSON may not have loaded.");
            return false;
        }

        if (nameData.baseNames == null || nameData.baseNames.Count == 0)
        {
            Debug.LogError("ItemNameData.baseNames is empty. Check ItemNames.json.");
            return false;
        }

        if (nameData.prefix == null || nameData.prefix.Length == 0)
        {
            Debug.LogError("ItemNameData.prefix is empty. Check ItemNames.json.");
            return false;
        }

        if (nameData.suffix == null || nameData.suffix.Length == 0)
        {
            Debug.LogError("ItemNameData.suffix is empty. Check ItemNames.json.");
            return false;
        }

        if (!ValidateModifierDefinitions(nameData.prefix, "prefix", requirePrefixName: true, requireSuffixName: false))
        {
            return false;
        }

        if (!ValidateModifierDefinitions(nameData.suffix, "suffix", requirePrefixName: false, requireSuffixName: true))
        {
            return false;
        }

        ValidateCategories();

        return true;
    }

    private void ValidateCategories()
    {
        if (nameData == null || nameData.categories == null || nameData.categories.Count == 0)
        {
            return;
        }

        for (int i = 0; i < nameData.categories.Count; i++)
        {
            BaseNameCategory category = nameData.categories[i];
            if (category == null || string.IsNullOrWhiteSpace(category.name) || category.baseNames == null)
            {
                continue;
            }

            for (int j = 0; j < category.baseNames.Count; j++)
            {
                string baseName = category.baseNames[j];
                if (!string.IsNullOrWhiteSpace(baseName) && !nameData.baseNames.Contains(baseName))
                {
                    Debug.LogWarning($"Category '{category.name}' references unknown base name '{baseName}'.");
                }
            }
        }
    }

    private bool ValidateModifierDefinitions(ModifierDefinition[] modifiers, string label, bool requirePrefixName, bool requireSuffixName)
    {
        for (int i = 0; i < modifiers.Length; i++)
        {
            ModifierDefinition modifier = modifiers[i];
            if (modifier == null)
            {
                Debug.LogError($"ItemNameData.{label}[{i}] is null. Check ItemNames.json.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(modifier.template))
            {
                Debug.LogError($"ItemNameData.{label}[{i}].template is empty. Check ItemNames.json.");
                return false;
            }

            if (requirePrefixName && string.IsNullOrWhiteSpace(modifier.namePrefix))
            {
                Debug.LogError($"ItemNameData.{label}[{i}].namePrefix is empty. Check ItemNames.json.");
                return false;
            }

            if (requireSuffixName && string.IsNullOrWhiteSpace(modifier.nameSuffix))
            {
                Debug.LogError($"ItemNameData.{label}[{i}].nameSuffix is empty. Check ItemNames.json.");
                return false;
            }

            if (!ValidateModifierRange(modifier.rare, $"{label}[{i}].rare"))
            {
                return false;
            }

            if (!ValidateModifierRange(modifier.epic, $"{label}[{i}].epic"))
            {
                return false;
            }

            if (!ValidateModifierRange(modifier.legendary, $"{label}[{i}].legendary"))
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidateModifierRange(ModifierRange range, string label)
    {
        if (range == null)
        {
            Debug.LogError($"ItemNameData.{label} is missing. Check ItemNames.json.");
            return false;
        }

        if (range.max < range.min)
        {
            Debug.LogError($"ItemNameData.{label} has max < min ({range.max} < {range.min}).");
            return false;
        }

        return true;
    }

    private bool IsNameDataValidShape(ItemNameData data)
    {
        if (data == null)
        {
            return false;
        }

        if (data.baseNames == null || data.baseNames.Count == 0)
        {
            return false;
        }

        if (data.prefix == null || data.prefix.Length == 0)
        {
            return false;
        }

        if (data.suffix == null || data.suffix.Length == 0)
        {
            return false;
        }

        return true;
    }

    private bool IsLegacyNameDataValidShape(LegacyItemNameData data)
    {
        if (data == null)
        {
            return false;
        }

        if (data.baseNames == null || data.baseNames.Count == 0)
        {
            return false;
        }

        if (data.prefixModifiers == null || data.prefixModifiers.Length == 0)
        {
            return false;
        }

        if (data.suffixModifiers == null || data.suffixModifiers.Length == 0)
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Item Generation

    private GeneratedItem GenerateItem()
    {
        return GenerateItem(new ItemGenerationOptions
        {
            BaseNameOverride = exclusiveGenerationMode == ExclusiveGenerationMode.BaseName ? specificBaseName : string.Empty,
            BaseNameCategoryOverride = exclusiveGenerationMode == ExclusiveGenerationMode.Category ? specificBaseCategory : string.Empty,
            LootLuckOverride = -1,
            RarityOverride = string.Empty
        });
    }

    private GeneratedItem GenerateItem(ItemGenerationOptions options)
    {
        if (nameData == null)
        {
            LoadNameData();
        }

        GeneratedItem item = new GeneratedItem();

        int resolvedLootLuck = options.LootLuckOverride >= 0 ? options.LootLuckOverride : lootLuck;

        string resolvedRarity = !string.IsNullOrWhiteSpace(options.RarityOverride)
            ? options.RarityOverride
            : DetermineRarity(resolvedLootLuck);
        item.Rarity = resolvedRarity;

        // Generate name and modifiers based on rarity
        GenerateItemContent(item, options.BaseNameOverride, options.BaseNameCategoryOverride);

        return item;
    }

    private void GenerateItemContent(GeneratedItem item)
    {
        GenerateItemContent(item, string.Empty, string.Empty);
    }

    private void GenerateItemContent(GeneratedItem item, string baseNameOverride, string baseCategoryOverride)
    {
        if (nameData == null)
        {
            LoadNameData();
        }

        if (!ValidateNameData())
        {
            item.Name = "Invalid Item Data";
            return;
        }

        string baseName = SelectBaseName(baseNameOverride, baseCategoryOverride);
        
        int prefixModCount = 0;
        int suffixModCount = 0;
        
        switch (item.Rarity)
        {
            case RarityCommon:
                // Just the base name, no modifiers
                item.Name = baseName;
                return;
                
            case RarityRare:
                // Prefix + Name + Suffix, 1 prefix mod + 1 suffix mod
                prefixModCount = 1;
                suffixModCount = 1;
                break;
                
            case RarityEpic:
                // Prefix + Name + Suffix, 2 prefix mods + 2 suffix mods
                prefixModCount = 2;
                suffixModCount = 2;
                break;
                
            case RarityLegendary:
                // Prefix + Name + Suffix, 3 prefix mods + 3 suffix mods
                prefixModCount = 3;
                suffixModCount = 3;
                break;
        }

            List<ModifierDefinition> prefixPool = FilterModifiersByBaseName(nameData.prefix, baseName, "prefix");
            List<ModifierDefinition> suffixPool = FilterModifiersByBaseName(nameData.suffix, baseName, "suffix");
        
        // Build name (Rare and above get Prefix + Name + Suffix)
        ModifierDefinition bestPrefixNameModifier = null;
        float bestPrefixAmplitude = -1f;
        ModifierDefinition bestSuffixNameModifier = null;
        float bestSuffixAmplitude = -1f;

        List<ModifierDefinition> prefixSelections = GetUniqueModifiers(prefixPool, prefixModCount, "prefix");
        List<ModifierDefinition> suffixSelections = GetUniqueModifiers(suffixPool, suffixModCount, "suffix");

        // Generate prefix modifiers
        for (int i = 0; i < prefixSelections.Count; i++)
        {
            ModifierDefinition modifierDef = prefixSelections[i];
            string modifierRarity = DetermineModifierRarity();
            int value = GenerateModifierValue(modifierDef, modifierRarity);
            string modifier = string.Format(modifierDef.template, value);
            item.PrefixModifiers.Add(modifier);
            item.PrefixModifierRarities.Add(modifierRarity);

            float amplitude = GetModifierAmplitude(modifierDef, modifierRarity, value);
            if (amplitude > bestPrefixAmplitude)
            {
                bestPrefixAmplitude = amplitude;
                bestPrefixNameModifier = modifierDef;
            }
        }
        
        // Generate suffix modifiers
        for (int i = 0; i < suffixSelections.Count; i++)
        {
            ModifierDefinition modifierDef = suffixSelections[i];
            string modifierRarity = DetermineModifierRarity();
            int value = GenerateModifierValue(modifierDef, modifierRarity);
            string modifier = string.Format(modifierDef.template, value);
            item.SuffixModifiers.Add(modifier);
            item.SuffixModifierRarities.Add(modifierRarity);

            float amplitude = GetModifierAmplitude(modifierDef, modifierRarity, value);
            if (amplitude > bestSuffixAmplitude)
            {
                bestSuffixAmplitude = amplitude;
                bestSuffixNameModifier = modifierDef;
            }
        }

        string namePrefix = GetModifierNamePrefix(bestPrefixNameModifier);
        string nameSuffix = GetModifierNameSuffix(bestSuffixNameModifier);
        item.Name = $"{namePrefix} {baseName} {nameSuffix}";
    }

    #endregion

    #region Rarity Determination

    private string DetermineRarity(int lootLuck)
    {
        // Calculate weights for each rarity based on loot luck
        GetAdjustedRarityWeights(lootLuck, out float adjustedCommon, out float adjustedRare, out float adjustedEpic, out float adjustedLegendary);
        
        // Calculate total weight
        float totalWeight = adjustedCommon + adjustedRare + adjustedEpic + adjustedLegendary;
        if (totalWeight <= 0f)
        {
            adjustedCommon = adjustedRare = adjustedEpic = adjustedLegendary = 1f;
            totalWeight = 4f;
        }
        
        // Roll a random number between 0 and totalWeight
        float roll = Random.Range(0f, totalWeight);
        
        // Determine which rarity was rolled
        if (roll < adjustedCommon)
            return "Common";
        else if (roll < adjustedCommon + adjustedRare)
            return "Rare";
        else if (roll < adjustedCommon + adjustedRare + adjustedEpic)
            return "Epic";
        else
            return "Legendary";
    }

    private string DetermineModifierRarity()
    {
        float adjustedRare = Mathf.Max(0f, modifierRareWeight);
        float adjustedEpic = Mathf.Max(0f, modifierEpicWeight);
        float adjustedLegendary = Mathf.Max(0f, modifierLegendaryWeight);

        float totalWeight = adjustedRare + adjustedEpic + adjustedLegendary;
        if (totalWeight <= 0f)
        {
            adjustedRare = adjustedEpic = adjustedLegendary = 1f;
            totalWeight = 3f;
        }

        float roll = Random.Range(0f, totalWeight);

        if (roll < adjustedRare)
            return RarityRare;
        else if (roll < adjustedRare + adjustedEpic)
            return RarityEpic;
        else
            return RarityLegendary;
    }

    private void GetAdjustedRarityWeights(int lootLuck, out float adjustedCommon, out float adjustedRare, out float adjustedEpic, out float adjustedLegendary)
    {
        float baseCommon = Mathf.Max(0f, commonWeight);
        float baseRare = Mathf.Max(0f, rareWeight);
        float baseEpic = Mathf.Max(0f, epicWeight);
        float baseLegendary = Mathf.Max(0f, legendaryWeight);

        const float maxLuck = 2500f;
        float luckT = Mathf.Clamp01(lootLuck / maxLuck);
        float biasT = luckT;

        float commonMultiplier = Mathf.Lerp(1f, luckMultipliers.common, biasT);
        float rareMultiplier = Mathf.Lerp(1f, luckMultipliers.rare, biasT);
        float epicMultiplier = Mathf.Lerp(1f, luckMultipliers.epic, biasT);
        float legendaryMultiplier = Mathf.Lerp(1f, luckMultipliers.legendary, biasT);

        adjustedCommon = baseCommon * commonMultiplier;
        adjustedRare = baseRare * rareMultiplier;
        adjustedEpic = baseEpic * epicMultiplier;
        adjustedLegendary = baseLegendary * legendaryMultiplier;
    }


    #endregion

    #region Modifier Utilities

    private List<ModifierDefinition> FilterModifiersByBaseName(ModifierDefinition[] modifiers, string baseName, string label)
    {
        List<ModifierDefinition> results = new List<ModifierDefinition>();
        if (modifiers == null || modifiers.Length == 0)
        {
            return results;
        }

        for (int i = 0; i < modifiers.Length; i++)
        {
            ModifierDefinition modifier = modifiers[i];
            if (modifier == null)
            {
                continue;
            }

            if (modifier.allowedBaseNames == null || modifier.allowedBaseNames.Count == 0)
            {
                results.Add(modifier);
                continue;
            }

            if (IsModifierAllowedForBaseName(modifier.allowedBaseNames, baseName))
            {
                results.Add(modifier);
            }
        }

        if (results.Count == 0)
        {
            Debug.LogWarning($"No {label} modifiers allowed for base name '{baseName}'. Falling back to full pool.");
            for (int i = 0; i < modifiers.Length; i++)
            {
                ModifierDefinition modifier = modifiers[i];
                if (modifier != null)
                {
                    results.Add(modifier);
                }
            }
        }

        return results;
    }

    private bool IsModifierAllowedForBaseName(List<string> allowedEntries, string baseName)
    {
        if (allowedEntries == null || allowedEntries.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < allowedEntries.Count; i++)
        {
            string allowed = allowedEntries[i];
            if (string.IsNullOrWhiteSpace(allowed))
            {
                continue;
            }

            if (string.Equals(allowed, baseName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (TryGetCategoryBaseNames(allowed, out List<string> categoryBaseNames))
            {
                for (int j = 0; j < categoryBaseNames.Count; j++)
                {
                    if (string.Equals(categoryBaseNames[j], baseName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private List<ModifierDefinition> GetUniqueModifiers(List<ModifierDefinition> pool, int count, string label)
    {
        List<ModifierDefinition> selections = new List<ModifierDefinition>();
        if (pool == null || pool.Count == 0 || count <= 0)
        {
            return selections;
        }

        if (pool.Count < count)
        {
            Debug.LogWarning($"Not enough unique {label} modifiers for this item. Requested {count}, available {pool.Count}." );
            count = pool.Count;
        }

        List<ModifierDefinition> tempPool = new List<ModifierDefinition>(pool);
        for (int i = tempPool.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            ModifierDefinition temp = tempPool[i];
            tempPool[i] = tempPool[swapIndex];
            tempPool[swapIndex] = temp;
        }

        for (int i = 0; i < count; i++)
        {
            selections.Add(tempPool[i]);
        }

        return selections;
    }

    private int GenerateModifierValue(ModifierDefinition modifierDef, string rarity)
    {
        switch (rarity)
        {
            case RarityRare: return GetModifierValue(modifierDef.rare, 5, 15);
            case RarityEpic: return GetModifierValue(modifierDef.epic, 10, 25);
            case RarityLegendary: return GetModifierValue(modifierDef.legendary, 20, 50);
            default: return 0;
        }
    }

    private int GetModifierValue(ModifierRange range, int fallbackMin, int fallbackMax)
    {
        int min = range != null ? range.min : fallbackMin;
        int max = range != null ? range.max : fallbackMax;

        if (max < min)
        {
            int temp = min;
            min = max;
            max = temp;
        }

        return Random.Range(min, max + 1);
    }

    private float GetModifierAmplitude(ModifierDefinition modifierDef, string rarity, int value)
    {
        int max = GetModifierMax(modifierDef, rarity);
        if (max <= 0)
        {
            return 0f;
        }

        return (float)value / max;
    }

    private int GetModifierMax(ModifierDefinition modifierDef, string rarity)
    {
        switch (rarity)
        {
            case RarityRare: return modifierDef?.rare?.max ?? 15;
            case RarityEpic: return modifierDef?.epic?.max ?? 25;
            case RarityLegendary: return modifierDef?.legendary?.max ?? 50;
            default: return 0;
        }
    }

    private string GetModifierNamePrefix(ModifierDefinition modifierDef)
    {
        if (modifierDef != null && !string.IsNullOrWhiteSpace(modifierDef.namePrefix))
        {
            return modifierDef.namePrefix;
        }

        return "Unnamed";
    }

    private string GetModifierNameSuffix(ModifierDefinition modifierDef)
    {
        if (modifierDef != null && !string.IsNullOrWhiteSpace(modifierDef.nameSuffix))
        {
            return modifierDef.nameSuffix;
        }

        return "of the Unknown";
    }

    #endregion

    #region Base Name Selection

    private string SelectBaseName()
    {
        return SelectBaseName(string.Empty, string.Empty);
    }

    private string SelectBaseName(string baseNameOverride, string baseCategoryOverride)
    {
        if (nameData == null || nameData.baseNames == null || nameData.baseNames.Count == 0)
        {
            return "Unknown";
        }

        string resolvedOverride = baseNameOverride;
        string resolvedCategory = baseCategoryOverride;

        if (string.IsNullOrWhiteSpace(resolvedOverride) && string.IsNullOrWhiteSpace(resolvedCategory))
        {
            if (exclusiveGenerationMode == ExclusiveGenerationMode.BaseName)
            {
                resolvedOverride = specificBaseName;
            }
            else if (exclusiveGenerationMode == ExclusiveGenerationMode.Category)
            {
                resolvedCategory = specificBaseCategory;
            }
            else
            {
                if (useSpecificBaseName)
                {
                    resolvedOverride = specificBaseName;
                }

                if (string.IsNullOrWhiteSpace(resolvedCategory) && useSpecificBaseCategory)
                {
                    resolvedCategory = specificBaseCategory;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(resolvedOverride))
        {
            if (nameData.baseNames.Contains(resolvedOverride))
            {
                return resolvedOverride;
            }

            Debug.LogWarning($"Specific base name '{resolvedOverride}' was not found. Falling back to random selection.");
        }

        if (!string.IsNullOrWhiteSpace(resolvedCategory))
        {
            if (TryGetCategoryBaseNames(resolvedCategory, out List<string> categoryBaseNames) && categoryBaseNames.Count > 0)
            {
                return categoryBaseNames[Random.Range(0, categoryBaseNames.Count)];
            }

            Debug.LogWarning($"Specific category '{resolvedCategory}' was not found or empty. Falling back to random selection.");
        }

        return nameData.baseNames[Random.Range(0, nameData.baseNames.Count)];
    }

    private bool TryGetCategoryBaseNames(string categoryName, out List<string> baseNames)
    {
        baseNames = new List<string>();
        if (string.IsNullOrWhiteSpace(categoryName) || nameData == null || nameData.categories == null)
        {
            return false;
        }

        for (int i = 0; i < nameData.categories.Count; i++)
        {
            BaseNameCategory category = nameData.categories[i];
            if (category == null || string.IsNullOrWhiteSpace(category.name))
            {
                continue;
            }

            if (string.Equals(category.name, categoryName, System.StringComparison.OrdinalIgnoreCase))
            {
                if (category.baseNames != null)
                {
                    baseNames = category.baseNames;
                }

                return true;
            }
        }

        return false;
    }

    #endregion
}