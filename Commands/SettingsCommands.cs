using System;
using KindredSacrifice.Services;
using Stunlock.Core;
using VampireCommandFramework;

namespace KindredSacrifice.Commands;

[CommandGroup("sacrifice", "sac")]
internal class SettingsCommands
{
	[Command("settings", description: "Show global settings and reward type summary", adminOnly: true)]
	public static void ShowSettings(ChatCommandContext ctx)
	{
		var settings = Core.ConfigService.Settings;

		ctx.Reply("<color=#b00> ̷͠ ͠ ⸸⃝✽⃝ <color=#d00>Blood Sacrifice <color=#f00>Settings <color=#d00>✽⃝  ̷͠⸸⃝̷͠  <color=#b00>");
		ctx.Reply($"<color=#f44>Messages:</color> <color=#c24>{settings.EnableSacrificeMessages}</color> | <color=#f44>Accumulation:</color> <color=#c24>{settings.EnableBloodmoonAccumulation}</color> | <color=#f44>Lockout:</color> <color=#c24>{settings.BloodMoonLockoutNights} nights</color> | <color=#f44>Minimum Blood Quality for Reward:</color> <color=#c24>{settings.MinimumRewardBloodQuality}</color>");

		var buffTypes = new System.Collections.Generic.List<string>();
		var merlotTypes = new System.Collections.Generic.List<string>();
		var itemTypes = new System.Collections.Generic.List<string>();
		var noneTypes = new System.Collections.Generic.List<string>();
		var bloodMoonTypes = new System.Collections.Generic.List<string>();

		foreach (var kvp in settings.BloodTypeRewards)
		{
			switch (kvp.Value.RewardType)
			{
				case RewardType.Buff:
					buffTypes.Add(kvp.Key);
					break;
				case RewardType.DropBloodMerlots:
					merlotTypes.Add(kvp.Key);
					break;
				case RewardType.DropItems:
					itemTypes.Add(kvp.Key);
					break;
				case RewardType.BloodMoon:
					bloodMoonTypes.Add(kvp.Key);
					break;
				default:
					noneTypes.Add(kvp.Key);
					break;
			}
		}

		if (buffTypes.Count > 0)
			ctx.Reply($"<color=#f44>Buff:</color> <color=#c24>{string.Join(", ", buffTypes)}</color>");
		if (merlotTypes.Count > 0)
			ctx.Reply($"<color=#f44>Merlot:</color> <color=#c24>{string.Join(", ", merlotTypes)}</color>");
		if (itemTypes.Count > 0)
			ctx.Reply($"<color=#f44>Items:</color> <color=#c24>{string.Join(", ", itemTypes)}</color>");
		if (bloodMoonTypes.Count > 0)
			ctx.Reply($"<color=#f44>BloodMoon:</color> <color=#c24>{string.Join(", ", bloodMoonTypes)}</color>");
		if (noneTypes.Count > 0)
			ctx.Reply($"<color=#f44>None:</color> <color=#c24>{string.Join(", ", noneTypes)}</color>");
	}

	[Command("reward", "r", description: "Show full reward details for a blood type (read-only)", adminOnly: true)]
	public static void ShowReward(ChatCommandContext ctx, string bloodType)
	{
		if (!TryGetExistingReward(ctx, bloodType, out var name, out var reward)) return;

		ctx.Reply($"<color=#f44>{name}</color> <color=#c24>- Type: {reward.RewardType}</color>");

		switch (reward.RewardType)
		{
			case RewardType.Buff:
				var prefabGuid = new PrefabGUID(reward.BuffPrefabGuid);
				ctx.Reply($"  <color=#f44>Buff:</color> <color=#c24>{prefabGuid.LookupName()} ({reward.BuffPrefabGuid})</color>");
				ctx.Reply($"  <color=#f44>Duration:</color> <color=#c24>{reward.BuffDuration}s</color>");
				break;

			case RewardType.DropBloodMerlots:
				var typeStr = reward.UsePrisonerBloodType ? "Prisoner" : reward.MerlotBloodType;
				var qualStr = reward.UsePrisonerQuality ? "Prisoner" : $"{reward.MerlotQuality}%";
				ctx.Reply($"  <color=#f44>Quantity:</color> <color=#c24>{reward.MerlotMinQuantity}-{reward.MerlotMaxQuantity}</color>");
				ctx.Reply($"  <color=#f44>Type:</color> <color=#c24>{typeStr}</color> | <color=#f44>Quality:</color> <color=#c24>{qualStr}</color>");
				break;

			case RewardType.DropItems:
				for (int i = 0; i < reward.ItemDrops.Count; i++)
				{
					var item = reward.ItemDrops[i];
					var itemPrefab = new PrefabGUID(item.ItemPrefabGuid);
					ctx.Reply($"  <color=#f44>{i + 1}.</color> <color=#c24>{itemPrefab.LookupName()} ({item.MinQuantity}-{item.MaxQuantity})</color>");
				}
				break;
		}
	}

	[Command("setmessages", "sm", description: "Toggle sacrifice messages on/off", adminOnly: true)]
	public static void SetMessages(ChatCommandContext ctx, bool enabled)
	{
		Core.ConfigService.Settings.EnableSacrificeMessages = enabled;
		Core.ConfigService.SaveSettings();
		ctx.Reply($"<color=#f44>Sacrifice messages</color> <color=#c24>{(enabled ? "enabled" : "disabled")}.</color>");
	}

	[Command("setaccumulation", "sa", description: "Toggle bloodmoon accumulation on/off", adminOnly: true)]
	public static void SetAccumulation(ChatCommandContext ctx, bool enabled)
	{
		Core.ConfigService.Settings.EnableBloodmoonAccumulation = enabled;
		Core.ConfigService.SaveSettings();
		ctx.Reply($"<color=#f44>Bloodmoon accumulation</color> <color=#c24>{(enabled ? "enabled" : "disabled")}.</color>");
	}

	[Command("setlockout", "sl", description: "Set blood moon lockout nights (min 2)", adminOnly: true)]
	public static void SetLockout(ChatCommandContext ctx, int nights)
	{
		if (nights < 2)
		{
			ctx.Reply("<color=#f44>Error:</color> <color=#c24>Lockout nights must be at least 2.</color>");
			return;
		}
		Core.ConfigService.Settings.BloodMoonLockoutNights = nights;
		Core.ConfigService.SaveSettings();
		ctx.Reply($"<color=#f44>Blood moon lockout</color> <color=#c24>set to {nights} nights.</color>");
	}

	[Command("setrewardtype", "srt", description: "Set reward type for a blood type", adminOnly: true)]
	public static void SetRewardType(ChatCommandContext ctx, string bloodType, RewardType rewardType)
	{
		var reward = GetOrCreateReward(ctx, bloodType);
		if (reward == null) return;

		reward.RewardType = rewardType;
		Core.ConfigService.SaveSettings();
		ctx.Reply($"<color=#f44>{bloodType} reward type</color> <color=#c24>set to {rewardType}.</color>");
	}

	[Command("setbuff", "sb", description: "Set buff reward for a blood type (prefabGuid, duration)", adminOnly: true)]
	public static void SetBuff(ChatCommandContext ctx, string bloodType, int prefabGuid, float duration = 3600f)
	{
		var reward = GetOrCreateReward(ctx, bloodType);
		if (reward == null) return;

		reward.BuffPrefabGuid = prefabGuid;
		reward.BuffDuration = duration;
		Core.ConfigService.SaveSettings();
		ctx.Reply($"<color=#f44>{bloodType} buff</color> <color=#c24>set to {prefabGuid} for {duration}s.</color>");
	}

	[Command("setmerlot", "smer", description: "Set merlot config: <bloodType> <quality|prisoner> <min> <max> [type|prisoner]", adminOnly: true)]
	public static void SetMerlot(ChatCommandContext ctx, string bloodType, string qualityOrPrisoner, int min, int max, string typeOrPrisoner = "prisoner")
	{
		var reward = GetOrCreateReward(ctx, bloodType);
		if (reward == null) return;

		if (min < 0 || max < min)
		{
			ctx.Reply("<color=#f44>Error:</color> <color=#c24>Min must be >= 0 and max must be >= min.</color>");
			return;
		}

		if (qualityOrPrisoner.Equals("prisoner", StringComparison.OrdinalIgnoreCase))
		{
			reward.UsePrisonerQuality = true;
		}
		else if (float.TryParse(qualityOrPrisoner, out var qualityValue))
		{
			if (qualityValue < 0 || qualityValue > 100)
			{
				ctx.Reply("<color=#f44>Error:</color> <color=#c24>Quality must be between 0 and 100, or 'prisoner'.</color>");
				return;
			}
			reward.UsePrisonerQuality = false;
			reward.MerlotQuality = qualityValue;
		}
		else
		{
			ctx.Reply("<color=#f44>Error:</color> <color=#c24>Quality must be a number (0-100) or 'prisoner'.</color>");
			return;
		}

		if (typeOrPrisoner.Equals("prisoner", StringComparison.OrdinalIgnoreCase))
		{
			reward.UsePrisonerBloodType = true;
			reward.MerlotBloodType = "";
		}
		else
		{
			var typeGuid = BloodTypeMapping.GetGuid(typeOrPrisoner);
			if (typeGuid == 0)
			{
				ctx.Reply("<color=#f44>Error:</color> <color=#c24>Unknown merlot blood type. Valid: Warrior, Rogue, Brute, Scholar, Worker, Mutant, Creature, Corrupted, Draculin, prisoner</color>");
				return;
			}
			reward.UsePrisonerBloodType = false;
			reward.MerlotBloodType = typeOrPrisoner;
		}

		reward.MerlotMinQuantity = min;
		reward.MerlotMaxQuantity = max;
		Core.ConfigService.SaveSettings();

		var qualStr = reward.UsePrisonerQuality ? "Prisoner" : $"{reward.MerlotQuality}%";
		var typeStr = reward.UsePrisonerBloodType ? "Prisoner" : reward.MerlotBloodType;
		ctx.Reply($"<color=#f44>{bloodType} merlot:</color> <color=#c24>{min}-{max}, Quality:{qualStr}, Type:{typeStr}</color>");
	}

	[Command("adddrop", "ad", description: "Add an item drop to a blood type", adminOnly: true)]
	public static void AddDrop(ChatCommandContext ctx, string bloodType, int prefabGuid, int min = 1, int max = 1)
	{
		if (min < 0 || max < min)
		{
			ctx.Reply("<color=#f44>Error:</color> <color=#c24>Min must be >= 0 and max must be >= min.</color>");
			return;
		}

		var reward = GetOrCreateReward(ctx, bloodType);
		if (reward == null) return;

		reward.ItemDrops.Add(new ItemDrop
		{
			ItemPrefabGuid = prefabGuid,
			MinQuantity = min,
			MaxQuantity = max
		});
		Core.ConfigService.SaveSettings();

		var itemPrefab = new PrefabGUID(prefabGuid);
		ctx.Reply($"<color=#f44>Added drop #{reward.ItemDrops.Count}:</color> <color=#c24>{itemPrefab.LookupName()} ({min}-{max})</color>");
	}

	[Command("removedrop", "rd", description: "Remove an item drop by index (1-based)", adminOnly: true)]
	public static void RemoveDrop(ChatCommandContext ctx, string bloodType, int index)
	{
		if (!TryGetExistingReward(ctx, bloodType, out var name, out var reward)) return;

		if (index < 1 || index > reward.ItemDrops.Count)
		{
			ctx.Reply($"<color=#f44>Error:</color> <color=#c24>Invalid index. Must be 1-{reward.ItemDrops.Count}.</color>");
			return;
		}

		var removed = reward.ItemDrops[index - 1];
		var removedPrefab = new PrefabGUID(removed.ItemPrefabGuid);
		reward.ItemDrops.RemoveAt(index - 1);
		Core.ConfigService.SaveSettings();

		ctx.Reply($"<color=#f44>Removed drop #{index}:</color> <color=#c24>{removedPrefab.LookupName()}</color>");
	}

	[Command("cleardrops", "cd", description: "Clear all item drops from a blood type", adminOnly: true)]
	public static void ClearDrops(ChatCommandContext ctx, string bloodType)
	{
		if (!TryGetExistingReward(ctx, bloodType, out var name, out var reward)) return;

		var count = reward.ItemDrops.Count;
		reward.ItemDrops.Clear();
		Core.ConfigService.SaveSettings();
		ctx.Reply($"<color=#f44>Cleared</color> <color=#c24>{count} item drops from {name}.</color>");
	}

	[Command("setqualityrewardmin", "sqm", description: "Set the minimum blood quality at which rewards can be obtained", adminOnly: true)]
	public static void SetQualityRewardMin(ChatCommandContext ctx, int quality)
	{
		if (quality < 1)
		{
			ctx.Reply($"<color=#f44>Quality {quality} too low, must be a whole number greater than zero</color>");
			return;
		}
		if (quality > 100)
		{
			ctx.Reply($"<color=#f44>Quality {quality} too high, must be a whole number less than or equal to 100</color>");
			return;
		}
		if (quality == Core.ConfigService.Settings.MinimumRewardBloodQuality)
		{
			ctx.Reply($"<color=#f44>Quality {quality} is the current setting</color>");
			return;
		}
		var currentQuality = Core.ConfigService.Settings.MinimumRewardBloodQuality;
		Core.ConfigService.Settings.MinimumRewardBloodQuality = quality;
		Core.ConfigService.SaveSettings();
		ctx.Reply($"<color=#360>Successfully set min quality for rewards from {currentQuality} to {quality}</color>");
	}

	static bool TryResolveBloodType(ChatCommandContext ctx, string bloodType, out string name)
	{
		var guid = BloodTypeMapping.GetGuid(bloodType);
		if (guid == 0)
		{
			name = null;
			ctx.Reply("<color=#f44>Unknown blood type.</color> <color=#c24>Valid: Warrior, Rogue, Brute, Scholar, Worker, Mutant, Creature, Corrupted, Draculin</color>");
			return false;
		}
		name = BloodTypeMapping.GetName(guid);
		return true;
	}

	static bool TryGetExistingReward(ChatCommandContext ctx, string bloodType, out string name, out SacrificeReward reward)
	{
		reward = null;
		if (!TryResolveBloodType(ctx, bloodType, out name)) return false;

		var rewards = Core.ConfigService.Settings.BloodTypeRewards;
		if (!rewards.TryGetValue(name, out reward))
		{
			ctx.Reply($"<color=#f44>{name}:</color> <color=#c24>No reward configured.</color>");
			return false;
		}
		return true;
	}

	static SacrificeReward GetOrCreateReward(ChatCommandContext ctx, string bloodType)
	{
		if (!TryResolveBloodType(ctx, bloodType, out var name)) return null;
		var rewards = Core.ConfigService.Settings.BloodTypeRewards;
		if (!rewards.TryGetValue(name, out var reward))
		{
			reward = new SacrificeReward();
			rewards[name] = reward;
		}
		return reward;
	}
}
