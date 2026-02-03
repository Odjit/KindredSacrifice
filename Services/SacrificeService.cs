using System;
using System.Collections;
using KindredSacrifice.Data;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Network;
using ProjectM.Shared;
using ProjectM.Tiles;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KindredSacrifice.Services;

internal class SacrificeService
{
    const float DropOffset = 2f;
    const string CageNamePrefix = "KindredSacrifice|";

    // Single sacrifice cage (identified via NameableInteractable for persistence)
    SacrificeCageData activeCage;
    RitualElements ritualElements = new();

    // Cached queries
    EntityQuery dayNightCycleQuery;

    public SacrificeService()
    {
        dayNightCycleQuery = Core.EntityManager.CreateEntityQuery(
            ComponentType.ReadWrite<DayNightCycle>(),
            ComponentType.ReadOnly<PrefabGUID>()
        );
    }

    static string EncodeCageName(float accumulatedPoints)
    {
        return $"{CageNamePrefix}{accumulatedPoints:F0}";
    }

    static bool TryDecodeCageName(string name, out float accumulatedPoints)
    {
        accumulatedPoints = 0f;

        if (!name.StartsWith(CageNamePrefix))
            return false;

        var data = name.Substring(CageNamePrefix.Length);
        return float.TryParse(data, out accumulatedPoints);
    }

    public void SaveWorldStateToCage()
    {
        if (activeCage == null) return;

        var cageEntity = activeCage.Entity;
        if (!Core.EntityManager.Exists(cageEntity)) return;

        var worldState = Core.ConfigService.WorldState;
        var encodedName = EncodeCageName(worldState.AccumulatedBloodPoints);

        if (!cageEntity.Has<NameableInteractable>())
        {
            cageEntity.Add<NameableInteractable>();
        }

        cageEntity.Write(new NameableInteractable
        {
            Name = new FixedString64Bytes(encodedName)
        });
    }

    static Entity TryDestroyAndClear(Entity entity)
    {
        if (entity.ExistsAndValid())
            DestroyUtility.Destroy(Core.EntityManager, entity);
        return Entity.Null;
    }

    public void LoadExistingSacrificeCage()
    {
        try
        {
            // Pass 1: Find the sacrifice cage (narrowed query with PrisonCell)
            FindSacrificeCage();

            // Pass 2: Find ritual elements (always run, even if no cage)
            // Use cage position for duplicate resolution, or float3.zero if no cage
            var referencePosition = activeCage?.Position ?? float3.zero;
            FindRitualElements(referencePosition);

            if (activeCage == null)
            {
                if (HasAnyRitualElements())
                {
                    Core.Log.LogWarning("Found ritual elements without a cage. Cleaning up orphaned elements.");
                    DestroyRitualElements();
                }
            }
            else
            {
                var basePosition = new float3(activeCage.Position.x, 0f, activeCage.Position.z);
                EnsureRitualElements(activeCage.Entity, basePosition);
                Core.Log.LogInfo($"Loaded sacrifice cage at {activeCage.Position}");
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(LoadExistingSacrificeCage));
        }
    }

    void FindSacrificeCage()
    {
        var eqb = new EntityQueryBuilder(Allocator.Temp)
            .AddAll(ComponentType.ReadOnly<NameableInteractable>())
            .AddAll(ComponentType.ReadOnly<PrefabGUID>())
            .AddAll(ComponentType.ReadOnly<PrisonCell>())
            .WithOptions(EntityQueryOptions.IncludeDisabledEntities);

        var query = Core.EntityManager.CreateEntityQuery(ref eqb);
        eqb.Dispose();

        var entities = query.ToEntityArray(Allocator.Temp);

        foreach (var entity in entities)
        {
            var prefabGuid = entity.Read<PrefabGUID>();
            if (prefabGuid != Prefabs.TM_SpecialStation_PrisonCell) continue;

            var nameable = entity.Read<NameableInteractable>();
            var name = nameable.Name.ToString();
            if (!name.StartsWith(CageNamePrefix)) continue;

            if (activeCage != null)
            {
                // Duplicate cage - destroy it
                Core.Log.LogError($"Multiple sacrifice cages found! Destroying duplicate: {entity}");
                if (Core.EntityManager.Exists(entity))
                {
                    DestroyUtility.Destroy(Core.EntityManager, entity);
                }
                continue;
            }

            var cagePosition = entity.Has<Translation>() ? entity.Read<Translation>().Value : float3.zero;
            var mapIconEntity = CreateMapIcon(cagePosition);

            activeCage = new SacrificeCageData
            {
                Entity = entity,
                Position = cagePosition,
                OwnerUserEntity = Entity.Null,
                CreatedTime = DateTime.UtcNow,
                MapIconEntity = mapIconEntity
            };

            // Restore world state from cage name
            if (TryDecodeCageName(name, out var points))
            {
                Core.ConfigService.WorldState.AccumulatedBloodPoints = points;
                Core.Log.LogInfo($"Restored world state from cage: {points:F0} points");
            }
        }

        entities.Dispose();
        query.Dispose();
    }

    void FindRitualElements(float3 referencePosition)
    {
        var eqb = new EntityQueryBuilder(Allocator.Temp)
            .AddAll(ComponentType.ReadOnly<NameableInteractable>())
            .AddAll(ComponentType.ReadOnly<PrefabGUID>())
			.AddAll(ComponentType.ReadOnly<Immortal>())
            .WithOptions(EntityQueryOptions.IncludeDisabledEntities);

        var query = Core.EntityManager.CreateEntityQuery(ref eqb);
        eqb.Dispose();

        var entities = query.ToEntityArray(Allocator.Temp);

        foreach (var entity in entities)
        {
            if (!entity.Has<Translation>()) continue;

            var nameable = entity.Read<NameableInteractable>();
            var name = nameable.Name.ToString();

            if (name == "KindredSacrifice_Pyre")
            {
                ritualElements.Pyre = HandleFoundElement(entity, ritualElements.Pyre, referencePosition, "Pyre", Prefabs.TM_Wilderness_Pyre);
            }
            else if (name == "KindredSacrifice_Brazier")
            {
                ritualElements.Brazier = HandleFoundElement(entity, ritualElements.Brazier, referencePosition, "Brazier", Prefabs.TM_Castle_ObjectDecor_Gothic_Brazier03_Orange);
            }
            else if (name == "KindredSacrifice_Table")
            {
                ritualElements.Table = HandleFoundElement(entity, ritualElements.Table, referencePosition, "Table", Prefabs.TM_Castle_Module_Parent_RoundTable_3x3_Cabal01);
            }
        }

        entities.Dispose();
        query.Dispose();
    }

    Entity HandleFoundElement(Entity entity, Entity currentElement, float3 referencePosition, string elementName, PrefabGUID expectedPrefab)
    {
        var prefabGuid = entity.Read<PrefabGUID>();
        if (prefabGuid.GuidHash != expectedPrefab.GuidHash)
        {
            Core.Log.LogError($"{elementName} has wrong PrefabGUID ({prefabGuid.GuidHash} vs expected {expectedPrefab.GuidHash}). Destroying invalid entity.");
            if (Core.EntityManager.Exists(entity))
            {
                DestroyUtility.Destroy(Core.EntityManager, entity);
            }
            return currentElement;
        }

        var entityPosition = entity.Read<Translation>().Value;

        if (currentElement == Entity.Null)
        {
            return entity;
        }

        // Duplicate found - destroy the one further away
        var existingPosition = currentElement.Has<Translation>() ? currentElement.Read<Translation>().Value : float3.zero;
        var existingDistance = math.distance(existingPosition, referencePosition);
        var newDistance = math.distance(entityPosition, referencePosition);

        Core.Log.LogError($"Multiple {elementName} elements found! Destroying the one further from cage.");

        if (newDistance < existingDistance)
        {
            // New one is closer - destroy existing, keep new
            if (Core.EntityManager.Exists(currentElement))
            {
                DestroyUtility.Destroy(Core.EntityManager, currentElement);
            }
            return entity;
        }
        else
        {
            // Existing is closer - destroy new one
            if (Core.EntityManager.Exists(entity))
            {
                DestroyUtility.Destroy(Core.EntityManager, entity);
            }
            return currentElement;
        }
    }

    bool HasAnyRitualElements()
    {
        return ritualElements.Pyre != Entity.Null ||
               ritualElements.Brazier != Entity.Null ||
               ritualElements.Table != Entity.Null;
    }

    void DestroyRitualElements()
    {
        ritualElements.Pyre = TryDestroyAndClear(ritualElements.Pyre);
        ritualElements.Brazier = TryDestroyAndClear(ritualElements.Brazier);
        ritualElements.Table = TryDestroyAndClear(ritualElements.Table);
	}

	Entity SpawnRitualElement(PrefabGUID prefab, float3 position, Rotation rotation, string markerName)
	{
		var prefabSystem = Core.PrefabCollectionSystem;
		if (!prefabSystem._PrefabLookupMap.GuidToEntityMap.TryGetValue(prefab, out var prefabEntity))
		{
			Core.Log.LogError($"Prefab not found! GUID: {prefab.GuidHash}");
			return Entity.Null;
		}

		var entity = Core.EntityManager.Instantiate(prefabEntity);
		entity.Write(new Translation { Value = position });
		entity.Write(rotation);
		MakeImmortalAndNonDismantleable(entity, markerName);
		return entity;
	}

	void EnsureRitualElements(Entity cageEntity, float3 basePosition)
	{
		var rotation = cageEntity.Has<Rotation>() ? cageEntity.Read<Rotation>() : new Rotation { Value = quaternion.identity };

		if (ritualElements.Pyre == Entity.Null)
			ritualElements.Pyre = SpawnRitualElement(Prefabs.TM_Wilderness_Pyre,
				new float3(basePosition.x, basePosition.y + 0.05f, basePosition.z), rotation, "KindredSacrifice_Pyre");

		if (ritualElements.Brazier == Entity.Null)
			ritualElements.Brazier = SpawnRitualElement(Prefabs.TM_Castle_ObjectDecor_Gothic_Brazier03_Orange,
				new float3(basePosition.x, basePosition.y - 0.7f, basePosition.z), rotation, "KindredSacrifice_Brazier");

		if (ritualElements.Table == Entity.Null)
			ritualElements.Table = SpawnRitualElement(Prefabs.TM_Castle_Module_Parent_RoundTable_3x3_Cabal01,
				new float3(basePosition.x, basePosition.y, basePosition.z), rotation, "KindredSacrifice_Table");
	}

	public Entity SpawnSacrificeCage(float3 cursorPosition, Entity ownerUserEntity, Entity charEntity)
    {
        try
        {
            if (activeCage != null)
            {
                throw new Exception("A sacrifice cage already exists! Only one cage can be active at a time. Use '.sac remove' to remove the existing cage first.");
            }

            var entityManager = Core.EntityManager;
            var prefabSystem = Core.PrefabCollectionSystem;

            if (!prefabSystem._PrefabLookupMap.GuidToEntityMap.TryGetValue(Prefabs.TM_SpecialStation_PrisonCell, out var prefabEntity))
            {
                throw new Exception("Prison cell prefab not found!");
            }

            var cageEntity = entityManager.Instantiate(prefabEntity);
            var cagePosition = new float3(cursorPosition.x, cursorPosition.y + 1.6f, cursorPosition.z);

            cageEntity.Write(new Translation { Value = cagePosition });

            // Determine which 90-degree tile rotation faces the cage's interact side toward the player
            var directionToPlayer = charEntity.Read<Translation>().Value - cursorPosition;
            directionToPlayer.y = 0;
            if (math.lengthsq(directionToPlayer) > 0.001f)
            {
                directionToPlayer = math.normalize(directionToPlayer);
                // atan2 gives angle from +Z axis, convert to 0-3 tile rotation index
                var angle = math.atan2(directionToPlayer.x, directionToPlayer.z);
                if (angle < 0) angle += 2f * math.PI;
                var tileRotIndex = (int)math.round(angle / (math.PI / 2f)) % 4;
                var tileRot = (TileRotation)tileRotIndex;

                if (cageEntity.Has<TilePosition>())
                {
                    var tilePos = cageEntity.Read<TilePosition>();
                    tilePos.TileRotation = tileRot;
                    cageEntity.Write(tilePos);
                }

                if (cageEntity.Has<StaticTransformCompatible>())
                {
                    var stc = cageEntity.Read<StaticTransformCompatible>();
                    stc.NonStaticTransform_Rotation = tileRot;
                    cageEntity.Write(stc);
                }

                cageEntity.Write(new Rotation { Value = quaternion.RotateY(math.radians(90 * (int)tileRot)) });
            }

            if (cageEntity.Has<Immortal>())
            {
                var immortal = cageEntity.Read<Immortal>();
                immortal.IsImmortal = true;
                cageEntity.Write(immortal);
            }
            else
            {
                cageEntity.Add<Immortal>();
                cageEntity.Write(new Immortal { IsImmortal = true });
            }

            if (cageEntity.Has<EditableTileModel>())
            {
                var editable = cageEntity.Read<EditableTileModel>();
                editable.CanDismantle = false;
                editable.CanMoveAfterBuild = false;
                editable.CanRotateAfterBuild = false;
                cageEntity.Write(editable);
            }

            // Add NameableInteractable component with encoded world state
            if (!cageEntity.Has<NameableInteractable>())
            {
                cageEntity.Add<NameableInteractable>();
            }

            cageEntity.Write(new NameableInteractable
            {
                Name = new FixedString64Bytes(EncodeCageName(Core.ConfigService.WorldState.AccumulatedBloodPoints))
            });


            var mapIconEntity = CreateMapIcon(cagePosition);

            activeCage = new SacrificeCageData
            {
                Entity = cageEntity,
                Position = cagePosition,
                OwnerUserEntity = ownerUserEntity,
                CreatedTime = DateTime.UtcNow,
                MapIconEntity = mapIconEntity
            };

            EnsureRitualElements(cageEntity, cursorPosition);

            return cageEntity;
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(SpawnSacrificeCage));
            return Entity.Null;
        }
    }

    public bool IsSacrificeCage(Entity entity)
    {
        return activeCage != null && activeCage.Entity == entity;
    }

    int GetCurrentDay()
    {
        try
        {
            var gameTimeModifiers = Core.ServerGameSettingsSystem._Settings.GameTimeModifiers;
            var dayDuration = gameTimeModifiers.DayDurationInSeconds;

            var entities = dayNightCycleQuery.ToEntityArray(Allocator.Temp);

            var currentDay = -1;
            foreach (var entity in entities)
            {
                if (entity.Has<DayNightCycle>())
                {
                    var cycle = entity.Read<DayNightCycle>();
                    var totalTime = cycle.Time;
                    currentDay = (int)Math.Floor(totalTime / dayDuration);
                    break;
                }
            }

            entities.Dispose();

            return currentDay;
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(GetCurrentDay));
            return -1;
        }
    }

    public bool IsBloodMoonActive()
    {
        try
        {
            var entities = dayNightCycleQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (entity.Has<DayNightCycle>())
                {
                    var cycle = entity.Read<DayNightCycle>();
                    var currentDay = GetCurrentDay();
                    entities.Dispose();
                    // Block on blood moon day and the following day (night runs past midnight)
                    return currentDay >= 0 &&
                           (currentDay == cycle.NextBloodMoonDay || currentDay == cycle.NextBloodMoonDay + 1);
                }
            }
            entities.Dispose();
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(IsBloodMoonActive));
        }
        return false;
    }

    public bool IsInLockout(out int daysRemaining)
    {
        daysRemaining = 0;

        // Block during active blood moon (covers the night past midnight)
        if (IsBloodMoonActive())
        {
            daysRemaining = 1;
            return true;
        }

        var settings = Core.ConfigService.Settings;
        var worldState = Core.ConfigService.WorldState;

        // No blood moon has been triggered this session
        if (worldState.LastBloodMoonDay < 0)
        {
            return false;
        }

        var currentDay = GetCurrentDay();
        if (currentDay < 0) return false;

        var daysSinceLastBloodMoon = currentDay - worldState.LastBloodMoonDay;
        daysRemaining = settings.BloodMoonLockoutNights - daysSinceLastBloodMoon;

        return daysSinceLastBloodMoon < settings.BloodMoonLockoutNights;
    }

    void ProcessSacrificeRewards(Entity prisonerEntity, Entity ownerUserEntity, float bloodQuality, int roundedQuality, PrefabGUID bloodType, Entity cageEntity)
    {
        var logger = Core.Log;
        var entityManager = Core.EntityManager;

        try
        {
            var sacrificerEntity = ownerUserEntity;
			var sacrificerName = sacrificerEntity != Entity.Null && entityManager.Exists(sacrificerEntity) && sacrificerEntity.Has<User>()
				? sacrificerEntity.Read<User>().CharacterName.ToString()
				: "Unknown";
			var bloodContribution = (bloodQuality / 100f) * (bloodQuality / 100f) * 10000f;

            // Add to global accumulator
            var previousTotal = Core.ConfigService.WorldState.AccumulatedBloodPoints;
            Core.ConfigService.WorldState.AccumulatedBloodPoints += bloodContribution;
            var newTotal = Core.ConfigService.WorldState.AccumulatedBloodPoints;
            logger.LogInfo($"Accumulated blood points: {previousTotal:F2} -> {newTotal:F2} (target: 10000)");

            Core.ConfigService.SaveWorldState();

            // Check if we've reached the bloodmoon threshold
            bool triggeredBloodmoon = false;
            if (newTotal >= 10000f)
            {
                SetNextBloodMoon();
                Core.ConfigService.WorldState.AccumulatedBloodPoints = 0f;
                Core.ConfigService.WorldState.LastBloodMoonDay = GetCurrentDay();
                Core.ConfigService.SaveWorldState();
                triggeredBloodmoon = true;
                logger.LogInfo($"Blood moon triggered. Lockout duration: {Core.ConfigService.Settings.BloodMoonLockoutNights} nights");
            }

            // Check for 100% quality blood type-specific reward
            var receivedBloodTypeReward = false;
            var bloodTypeName = BloodTypeMapping.GetName(bloodType.GuidHash);
            if (roundedQuality == 100 && bloodType.GuidHash != 0)
            {
                if (Core.ConfigService.Settings.BloodTypeRewards.TryGetValue(bloodTypeName, out var bloodTypeReward))
                {
                    ApplyReward(sacrificerEntity, bloodTypeReward, bloodQuality, bloodType, cageEntity);
                    receivedBloodTypeReward = bloodTypeReward.RewardType != RewardType.None;
                }
            }

            if (Core.ConfigService.Settings.EnableSacrificeMessages)
            {
                var progressPercent = (newTotal / 10000f) * 100f;

                if (triggeredBloodmoon)
                {
                    var timing = BloodMoonRisesTonight() ? "tonight" : "tomorrow night";
                    FixedString512Bytes globalMessage = $"<color=#b00><b>●</b> The bloodmoon <color=#d00>will rise {timing}. <color=#f00>{roundedQuality}% {bloodTypeName} <color=#d00>blood spilled upon <color=#b00>the altar fulfilled the ritual. <b>✽⃝";
                    ServerChatUtils.SendSystemMessageToAllClients(entityManager, ref globalMessage);
                }

                if (sacrificerEntity != Entity.Null && entityManager.Exists(sacrificerEntity) && sacrificerEntity.Has<User>())
                {
                    var user = sacrificerEntity.Read<User>();
                    FixedString512Bytes message;

                    if (receivedBloodTypeReward)
                    {
                        message = $"<color=#b00>The altar is <color=#d00>satisfied, <color=#f00>and your offering <color=#d00>grants you <color=#b00>a boon.";
                    }
                    else if (!triggeredBloodmoon)
                    {
                        message = $"<color=#b40><b>⽕⃝̒</b> The ritual concludes but <color=#d50>the altar remains hungry. <color=#f60>{roundedQuality}% blood offered. <color=#d50>(+{bloodContribution:F0} pts) <color=#b40>Progress: {progressPercent:F1}%";
                    }
                    else
                    {
                        message = $"<color=#b00>The ritual is <color=#d00>complete. <color=#f00>{roundedQuality}% blood <color=#d00>offered. <color=#b00>(+{bloodContribution:F0} pts)";
                    }

                    ServerChatUtils.SendSystemMessageToClient(entityManager, user, ref message);
                }

                logger.LogInfo($"Sacrifice: {sacrificerName} - {roundedQuality}% {bloodTypeName} (+{bloodContribution:F0} pts)");
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(ProcessSacrificeRewards));
        }
    }

    public bool ProcessSacrifice(Entity cageEntity, Entity userEntity)
    {
        try
        {
            if (activeCage?.Entity != cageEntity)
            {
                return false;
            }

            var entityManager = Core.EntityManager;

            // Check if there's a prisoner in the cage
            if (!cageEntity.Has<PrisonCell>())
            {
                return false;
            }

            var prisonCell = cageEntity.Read<PrisonCell>();
            var prisonerEntity = prisonCell.ImprisonedEntity.GetEntityOnServer();

            if (prisonerEntity == Entity.Null)
            {
                return false;
            }

			if (!prisonerEntity.Has<Minion>() && !prisonerEntity.Has<EntityOwner>())
			{
				return false;
			}

			// Skip Bloodcraft familiars
			if (prisonerEntity.Has<BlockFeedBuff>())
			{
				return false;
			}

			var bloodQuality = GetPrisonerBloodQuality(prisonerEntity);
			var roundedQuality = (int)Math.Round(bloodQuality);
			var bloodType = GetPrisonerBloodType(prisonerEntity);

			Core.StartCoroutine(PerformSacrificeSequence(cageEntity, prisonerEntity, userEntity, bloodQuality, roundedQuality, bloodType));
			return true;
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(ProcessSacrifice));
            return false;
        }
    }

    float GetPrisonerBloodQuality(Entity prisonerEntity)
    {
        try
        {
            if (prisonerEntity.Has<BloodConsumeSource>())
            {
                var bloodSource = prisonerEntity.Read<BloodConsumeSource>();
                return bloodSource.BloodQuality; // Already 0-100
            }

            return 50f; // Default
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(GetPrisonerBloodQuality));
            return 50f;
        }
    }

    PrefabGUID GetPrisonerBloodType(Entity prisonerEntity)
    {
        try
        {
            if (prisonerEntity.Has<BloodConsumeSource>())
            {
                var bloodSource = prisonerEntity.Read<BloodConsumeSource>();
                return bloodSource.UnitBloodType._Value;
            }

            return new PrefabGUID(0);
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(GetPrisonerBloodType));
            return new PrefabGUID(0);
        }
    }

    float3 GetDropPosition(Entity cageEntity)
    {
        var cagePos = cageEntity.Read<Translation>().Value;
        var rotation = cageEntity.Has<Rotation>() ? cageEntity.Read<Rotation>().Value : quaternion.identity;
        var forward = math.mul(rotation, new float3(0, 0, 1));
        return cagePos + forward * DropOffset;
    }

    void ApplyReward(Entity userEntity, SacrificeReward reward, float bloodQuality, PrefabGUID prisonerBloodType, Entity cageEntity)
    {
        try
        {
            switch (reward.RewardType)
            {
                case RewardType.BloodMoon:
                    SetNextBloodMoon();
                    break;

                case RewardType.Buff:
                    if (reward.BuffPrefabGuid != 0 && userEntity != Entity.Null)
                    {
                        var charEntity = userEntity.GetCharacter();
                        var buffGuid = new PrefabGUID(reward.BuffPrefabGuid);
                        Core.BuffService.ApplyBuff(userEntity, charEntity, buffGuid, reward.BuffDuration);

                        var buffName = buffGuid.LookupName();
                        var durationText = reward.BuffDuration < 0 ? "permanent" : $"{reward.BuffDuration}s";
                        Core.Log.LogInfo($"Buff reward: {buffName} ({durationText})");
                    }
                    break;

                case RewardType.DropItems:
                    if (reward.ItemDrops != null && reward.ItemDrops.Count > 0)
                    {
                        var dropPos = GetDropPosition(cageEntity);
                        Core.ItemService.DropItems(reward.ItemDrops, dropPos);
                    }
                    break;

                case RewardType.DropBloodMerlots:
                    {
                        var merlotBloodType = reward.UsePrisonerBloodType
                            ? prisonerBloodType
                            : new PrefabGUID(BloodTypeMapping.GetGuid(reward.MerlotBloodType));

                        var merlotQuality = reward.UsePrisonerQuality
                            ? bloodQuality
                            : reward.MerlotQuality;

                        var quantity = UnityEngine.Random.Range(reward.MerlotMinQuantity, reward.MerlotMaxQuantity + 1);
                        var dropPos = GetDropPosition(cageEntity);

                        Core.ItemService.DropBloodMerlots(merlotBloodType, merlotQuality, quantity, dropPos);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(ApplyReward));
        }
    }

    bool BloodMoonRisesTonight()
    {
        try
        {
            var gameTimeModifiers = Core.ServerGameSettingsSystem._Settings.GameTimeModifiers;
            var entities = dayNightCycleQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (entity.Has<DayNightCycle>())
                {
                    var cycle = entity.Read<DayNightCycle>();
                    var now = cycle.GameDateTimeNow;

                    var dayEndHour = gameTimeModifiers.DayEndHour;
                    var dayEndMinute = gameTimeModifiers.DayEndMinute;
                    var currentTimeInMinutes = now.Hour * 60 + now.Minute;
                    var dayEndInMinutes = dayEndHour * 60 + dayEndMinute;

                    entities.Dispose();
                    // Before evening (PreDawn or DayTime) = tonight, Evening = tomorrow night
                    return currentTimeInMinutes < dayEndInMinutes;
                }
            }
            entities.Dispose();
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(BloodMoonRisesTonight));
        }
        return true;
    }

    void SetNextBloodMoon()
    {
        try
        {
            var gameTimeModifiers = Core.ServerGameSettingsSystem._Settings.GameTimeModifiers;
            var dayDuration = gameTimeModifiers.DayDurationInSeconds;

            var entities = dayNightCycleQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (entity.Has<DayNightCycle>())
                {
                    var cycle = entity.Read<DayNightCycle>();

                    var totalTime = cycle.Time;
                    var currentDay = (int)System.Math.Floor(totalTime / dayDuration);
                    var now = cycle.GameDateTimeNow;

                    var dayStartHour = gameTimeModifiers.DayStartHour;
                    var dayStartMinute = gameTimeModifiers.DayStartMinute;
                    var dayEndHour = gameTimeModifiers.DayEndHour;
                    var dayEndMinute = gameTimeModifiers.DayEndMinute;
                    var currentTimeInMinutes = now.Hour * 60 + now.Minute;
                    var dayStartInMinutes = dayStartHour * 60 + dayStartMinute;
                    var dayEndInMinutes = dayEndHour * 60 + dayEndMinute;
                    var isDay = currentTimeInMinutes >= dayStartInMinutes && currentTimeInMinutes < dayEndInMinutes;

                    var targetDay = isDay ? currentDay : currentDay + 1;

                    cycle.NextBloodMoonDay = targetDay;
                    entity.Write(cycle);

                    Core.Log.LogInfo($"Blood moon scheduled for day {targetDay} ({(isDay ? "tonight" : "tomorrow night")})");
                    break;
                }
            }
            entities.Dispose();
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(SetNextBloodMoon));
        }
    }


    RitualElements GetRitualElements(Entity cageEntity)
    {
        return ritualElements;
    }

    IEnumerator PerformSacrificeSequence(Entity cageEntity, Entity prisonerEntity, Entity ownerUserEntity, float bloodQuality, int roundedQuality, PrefabGUID bloodType)
    {
        var entityManager = Core.EntityManager;

        var ritualElements = GetRitualElements(cageEntity);

        if (roundedQuality == 100)
        {
            // 100 sacrifice
            if (ritualElements.Table != Entity.Null && entityManager.Exists(ritualElements.Table))
            {
                Core.BuffService.ApplyBuff(Entity.Null, ritualElements.Table, Prefabs.Buff_PerfectSacrifice_Table, -1);
            }

            if (cageEntity != Entity.Null && entityManager.Exists(cageEntity))
            {
                Core.BuffService.ApplyBuff(Entity.Null, cageEntity, Prefabs.Buff_PerfectSacrifice_Cage, -1);
                Core.BuffService.ApplyBuff(Entity.Null, cageEntity, Prefabs.Buff_PerfectSacrifice_BloodRain, -1);
            }

            // Apply buff to sacrificer
            if (ownerUserEntity != Entity.Null && entityManager.Exists(ownerUserEntity))
            {
                var charEntity = ownerUserEntity.GetCharacter();
                if (charEntity != Entity.Null && entityManager.Exists(charEntity))
                {
                    Core.BuffService.ApplyBuff(ownerUserEntity, charEntity, Prefabs.Buff_PerfectSacrifice_Players, -1);
                }
            }
        }
        else
        {
            // Regular sacrifice
            if (ritualElements.Brazier != Entity.Null && entityManager.Exists(ritualElements.Brazier))
            {
                Core.BuffService.ApplyBuff(Entity.Null, ritualElements.Brazier, Prefabs.Buff_SacrificeFlames, -1);
            }
        }

        Core.BuffService.ApplyBuff(Entity.Null, prisonerEntity, Prefabs.Buff_General_Ignite, 6f);

        if (!prisonerEntity.Has<Health>())
        {
            yield break;
        }

        var prisonerHealth = prisonerEntity.Read<Health>();
        var maxHealth = prisonerHealth.MaxHealth;
        var numStrikes = 5;
        var damagePerStrike = maxHealth / numStrikes;

        for (int i = 0; i < numStrikes; i++)
        {
            if (!entityManager.Exists(prisonerEntity))
            {
                break;
            }

            prisonerHealth = prisonerEntity.Read<Health>();
            prisonerHealth.Value = Math.Max(0, prisonerHealth.Value - damagePerStrike);
            prisonerEntity.Write(prisonerHealth);

            var waitTime = 1f;
            var elapsed = 0f;
            while (elapsed < waitTime)
            {
                elapsed += 0.016f; // ~60fps
                yield return null;
            }
        }

        if (entityManager.Exists(prisonerEntity))
        {
            prisonerHealth = prisonerEntity.Read<Health>();
            prisonerHealth.Value = 0;
            prisonerEntity.Write(prisonerHealth);

            // Wait a moment before destruction
            yield return null;
            yield return null;

            DestroyUtility.Destroy(entityManager, prisonerEntity, DestroyDebugReason.TryRemoveBuff);
        }

        var cooldownTime = 2f;
        var cooldownElapsed = 0f;
        while (cooldownElapsed < cooldownTime)
        {
            cooldownElapsed += 0.016f;
            yield return null;
        }

        if (roundedQuality == 100)
        {
            if (ritualElements.Table != Entity.Null && entityManager.Exists(ritualElements.Table))
            {
                Core.BuffService.RemoveBuff(ritualElements.Table, Prefabs.Buff_PerfectSacrifice_Table);
            }
            if (cageEntity != Entity.Null && entityManager.Exists(cageEntity))
            {
                Core.BuffService.RemoveBuff(cageEntity, Prefabs.Buff_PerfectSacrifice_Cage);
                Core.BuffService.RemoveBuff(cageEntity, Prefabs.Buff_PerfectSacrifice_BloodRain);
            }

            // Remove buff from sacrificer
            if (ownerUserEntity != Entity.Null && entityManager.Exists(ownerUserEntity))
            {
                var charEntity = ownerUserEntity.GetCharacter();
                if (charEntity != Entity.Null && entityManager.Exists(charEntity))
                {
                    Core.BuffService.RemoveBuff(charEntity, Prefabs.Buff_PerfectSacrifice_Players);
                }
            }
        }
        else
        {
            if (ritualElements.Brazier != Entity.Null && entityManager.Exists(ritualElements.Brazier))
            {
                Core.BuffService.RemoveBuff(ritualElements.Brazier, Prefabs.Buff_SacrificeFlames);
            }
        }

        ProcessSacrificeRewards(prisonerEntity, ownerUserEntity, bloodQuality, roundedQuality, bloodType, cageEntity);
    }

    void MakeImmortalAndNonDismantleable(Entity entity, string markerName = null)
    {
        if (entity.Has<Immortal>())
        {
            var immortal = entity.Read<Immortal>();
            immortal.IsImmortal = true;
            entity.Write(immortal);
        }
        else
        {
            entity.Add<Immortal>();
            entity.Write(new Immortal { IsImmortal = true });
        }

        if (entity.Has<EditableTileModel>())
        {
            var editable = entity.Read<EditableTileModel>();
            editable.CanDismantle = false;
            editable.CanMoveAfterBuild = false;
            editable.CanRotateAfterBuild = false;
            entity.Write(editable);
        }

        if (!string.IsNullOrEmpty(markerName))
        {
            if (entity.Has<NameableInteractable>())
            {
                var nameable = entity.Read<NameableInteractable>();
                nameable.Name = new FixedString64Bytes(markerName);
                entity.Write(nameable);
            }
            else
            {
                entity.Add<NameableInteractable>();
                entity.Write(new NameableInteractable { Name = new FixedString64Bytes(markerName) });
            }
        }
    }

    Entity CreateMapIcon(float3 position)
    {
        try
        {
            var entityManager = Core.EntityManager;
            var prefabSystem = Core.PrefabCollectionSystem;

            if (!prefabSystem._PrefabLookupMap.GuidToEntityMap.TryGetValue(Prefabs.MapIcon_ProxyObject_POI_Unknown, out var proxyPrefab))
            {
                Core.Log.LogError("Map icon proxy prefab not found!");
                return Entity.Null;
            }

            var proxyEntity = entityManager.Instantiate(proxyPrefab);
            proxyEntity.Write(new Translation { Value = position });

            if (proxyEntity.Has<AttachMapIconsToEntity>())
            {
                var iconBuffer = entityManager.GetBuffer<AttachMapIconsToEntity>(proxyEntity);
                if (iconBuffer.Length > 0)
                {
                    var iconData = iconBuffer[0];
                    iconData.Prefab = Prefabs.MapIcon_POI_Discover_Unknown;
                    iconBuffer[0] = iconData;
                }
            }

            return proxyEntity;
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(CreateMapIcon));
            return Entity.Null;
        }
    }

    void DestroyMapIcon(Entity mapIconEntity)
    {
        try
        {
            if (mapIconEntity == Entity.Null || !Core.EntityManager.Exists(mapIconEntity))
            {
                return;
            }

            if (mapIconEntity.Has<AttachedBuffer>())
            {
                var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(mapIconEntity);
                foreach (var attached in attachedBuffer)
                {
                    if (Core.EntityManager.Exists(attached.Entity))
                    {
                        DestroyUtility.Destroy(Core.EntityManager, attached.Entity);
                    }
                }
            }

            DestroyUtility.Destroy(Core.EntityManager, mapIconEntity);
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(DestroyMapIcon));
        }
    }

    public void RemoveSacrificeCage()
    {
        try
        {
            if (activeCage == null)
            {
                return;
            }

            if (activeCage.MapIconEntity != Entity.Null)
            {
                DestroyMapIcon(activeCage.MapIconEntity);
            }

            DestroyRitualElements();
            activeCage = null;
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(RemoveSacrificeCage));
        }
    }

    public Entity? GetSacrificeCage()
    {
        return activeCage?.Entity;
    }
}

internal class SacrificeCageData
{
    public Entity Entity { get; set; }
    public float3 Position { get; set; }
    public Entity OwnerUserEntity { get; set; }
    public DateTime CreatedTime { get; set; }
    public Entity MapIconEntity { get; set; } = Entity.Null;
}

internal class RitualElements
{
    public Entity Pyre { get; set; } = Entity.Null;
    public Entity Brazier { get; set; } = Entity.Null;
    public Entity Table { get; set; } = Entity.Null;
}

