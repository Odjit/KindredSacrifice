using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KindredSacrifice.Services;

internal static class BloodTypeMapping
{
    static readonly Dictionary<int, string> GuidToName = new()
    {
        { -516976528, "Warrior" },
        { -1620185637, "Rogue" },
        { 804798592, "Brute" },
        { 1476452791, "Scholar" },
        { -1776904174, "Worker" },
        { 1821108694, "Mutant" },
        { 524822543, "Creature" },
        { -1382693416, "Corrupted" },
        { 1328126535, "Draculin" }
    };

    public static int GetGuid(string bloodTypeName)
    {
        // Linear search is fine - only called once per perfect sacrifice (rare event)
        foreach (var kvp in GuidToName)
        {
            if (kvp.Value.Equals(bloodTypeName, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Key;
            }
        }
        return 0;
    }

    public static string GetName(int guid)
    {
        return GuidToName.TryGetValue(guid, out var name) ? name : $"Unknown_{guid}";
    }
}

internal class SacrificeConfigService
{
    static readonly string ConfigDirectory = Path.Combine(BepInEx.Paths.ConfigPath, "KindredSacrifice");
    static readonly string SettingsPath = Path.Combine(ConfigDirectory, "Settings.json");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SacrificeSettings Settings { get; private set; }
    public SacrificeWorldState WorldState { get; internal set; }

    void EnsureConfigDirectory()
    {
        if (!Directory.Exists(ConfigDirectory))
            Directory.CreateDirectory(ConfigDirectory);
    }

    public SacrificeConfigService()
    {
        Settings = new SacrificeSettings();
        WorldState = new SacrificeWorldState();
    }

    public void LoadConfig()
    {
        LoadSettings();
    }

    void LoadSettings()
    {
        try
        {
            EnsureConfigDirectory();

            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<SacrificeSettings>(json, JsonOptions) ?? new SacrificeSettings();

                // Validate settings
                if (Settings.BloodMoonLockoutNights < 2)
                {
                    Core.Log.LogWarning($"BloodMoonLockoutNights was {Settings.BloodMoonLockoutNights}, clamping to minimum of 2");
                    Settings.BloodMoonLockoutNights = 2;
                    SaveSettings();
                }
            }
            else
            {
                Settings = new SacrificeSettings();
                SaveSettings();
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(LoadSettings));
            Settings = new SacrificeSettings();
        }
    }

    public void SaveSettings()
    {
        try
        {
            EnsureConfigDirectory();

            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(SaveSettings));
        }
    }

    public void SaveWorldState()
    {
        Core.SacrificeService?.SaveWorldStateToCage();
    }

    public void ResetWorldState()
    {
        WorldState = new SacrificeWorldState();
        SaveWorldState();
    }
}

internal class SacrificeSettings
{
    public bool EnableSacrificeMessages { get; set; } = true;

    public bool EnableBloodmoonAccumulation { get; set; } = true;

    public int BloodMoonLockoutNights { get; set; } = 3;

    public Dictionary<string, SacrificeReward> BloodTypeRewards { get; set; } = new()
    {
        { "Warrior", new SacrificeReward
        {
            RewardType = RewardType.DropBloodMerlots,
            MerlotMinQuantity = 1,
            MerlotMaxQuantity = 3,
            UsePrisonerBloodType = true,
            MerlotBloodType = "",
            UsePrisonerQuality = true,
            MerlotQuality = 100f
        } },
        { "Rogue", new SacrificeReward
        {
            RewardType = RewardType.DropBloodMerlots,
            MerlotMinQuantity = 1,
            MerlotMaxQuantity = 3,
            UsePrisonerBloodType = true,
            MerlotBloodType = "",
            UsePrisonerQuality = true,
            MerlotQuality = 100f
        } },
        { "Brute", new SacrificeReward
        {
            RewardType = RewardType.Buff,
            BuffPrefabGuid = 1068709119, // AB_Interact_UseRelic_Monster_Buff
            BuffDuration = 3600,
        } },
        { "Scholar", new SacrificeReward
        {
            RewardType = RewardType.Buff,
            BuffPrefabGuid = -238197495, // AB_Interact_UseRelic_Manticore_Buff
            BuffDuration = 3600,
        } },
        { "Worker", new SacrificeReward
        {
            RewardType = RewardType.Buff,
            BuffPrefabGuid = -1703886455, // AB_Interact_UseRelic_Behemoth_Buff
            BuffDuration = 3600,
        } },
        { "Mutant", new SacrificeReward
        {
            RewardType = RewardType.DropBloodMerlots,
            MerlotMinQuantity = 1,
            MerlotMaxQuantity = 3,
            UsePrisonerBloodType = true,
            MerlotBloodType = "",
            UsePrisonerQuality = true,
            MerlotQuality = 100f
        } },
        { "Creature", new SacrificeReward
        {
            RewardType = RewardType.DropBloodMerlots,
            MerlotMinQuantity = 1,
            MerlotMaxQuantity = 3,
            UsePrisonerBloodType = true,
            MerlotBloodType = "",
            UsePrisonerQuality = true,
            MerlotQuality = 100f
        } },
        { "Corrupted", new SacrificeReward
        {
            RewardType = RewardType.DropBloodMerlots,
            MerlotMinQuantity = 1,
            MerlotMaxQuantity = 3,
            UsePrisonerBloodType = true,
            MerlotBloodType = "",
            UsePrisonerQuality = true,
            MerlotQuality = 100f
        } },
        { "Draculin", new SacrificeReward
        {
            RewardType = RewardType.DropBloodMerlots,
            MerlotMinQuantity = 1,
            MerlotMaxQuantity = 3,
            UsePrisonerBloodType = true,
            MerlotBloodType = "",
            UsePrisonerQuality = true,
            MerlotQuality = 100f
        } }
    };
}

internal class SacrificeWorldState
{
    public float AccumulatedBloodPoints { get; set; } = 0f;

    public int LastBloodMoonDay { get; set; } = -1;
}

internal class ItemDrop
{
    public int ItemPrefabGuid { get; set; }
    public int MinQuantity { get; set; } = 1;
    public int MaxQuantity { get; set; } = 1;
}

internal class SacrificeReward
{
    public RewardType RewardType { get; set; }

    public int BuffPrefabGuid { get; set; }
    public float BuffDuration { get; set; }

    public List<ItemDrop> ItemDrops { get; set; } = new();

    public int MerlotMinQuantity { get; set; } = 1;
    public int MerlotMaxQuantity { get; set; } = 1;
    public bool UsePrisonerBloodType { get; set; } = true;
    public string MerlotBloodType { get; set; } = "Warrior"; 
    public bool UsePrisonerQuality { get; set; } = true; 
    public float MerlotQuality { get; set; } = 100f; 
}

internal enum RewardType
{
    None,
    BloodMoon,
    Buff,
    DropItems,
    DropBloodMerlots
}
