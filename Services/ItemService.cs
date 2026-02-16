using System;
using System.Collections.Generic;
using ProjectM;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KindredSacrifice.Services;

internal class ItemService
{
	static readonly PrefabGUID BloodMerlotPrefab = new(1223264867);

	Entity CreateDropProxy(float3 position)
	{
		var entity = Core.EntityManager.CreateEntity(ComponentType.ReadWrite<Translation>());
		entity.Write(new Translation { Value = position });
		return entity;
	}

	void WithDropProxy(float3 position, Action<Entity> action)
	{
		var proxy = CreateDropProxy(position);
		try { action(proxy); }
		finally { DestroyUtility.Destroy(Core.EntityManager, proxy); }
	}

	public void DropItemWithModification(PrefabGUID itemPrefab, float3 dropPosition, int quantity = 1, Action<Entity> modifyItem = null)
	{
		var entityManager = Core.EntityManager;

		try
		{
			var gameDataSystem = Core.Server.GetExistingSystemManaged<GameDataSystem>();
			var itemDataLookup = gameDataSystem.ItemHashLookupMap;

			if (!itemDataLookup.TryGetValue(itemPrefab, out var itemData))
			{
				Core.Log.LogWarning($"Could not find ItemData for prefab {itemPrefab.GuidHash}");
				return;
			}

			var itemEntity = itemData.Entity;
			modifyItem?.Invoke(itemEntity);

			WithDropProxy(dropPosition, proxy =>
			{
				InventoryUtilitiesServer.CreateDropItem(entityManager, proxy, itemPrefab, quantity, new Entity());
			});
		}
		catch (Exception ex)
		{
			Core.LogException(ex, nameof(DropItemWithModification));
		}
	}

	public void DropItems(List<ItemDrop> itemDrops, float3 dropPosition, float qualityMultiplier = 1.0f)
	{
		var logger = Core.Log;
		var entityManager = Core.EntityManager;

		try
		{
			WithDropProxy(dropPosition, proxy =>
			{
				foreach (var itemDrop in itemDrops)
				{
					var quantity = UnityEngine.Random.Range(itemDrop.MinQuantity, itemDrop.MaxQuantity + 1);

					if (qualityMultiplier != 1.0f)
					{
						quantity = (int)(quantity * qualityMultiplier);
					}

					if (quantity <= 0) continue;

					var itemGuid = new PrefabGUID(itemDrop.ItemPrefabGuid);

					InventoryUtilitiesServer.CreateDropItem(entityManager, proxy, itemGuid, quantity, new Entity());

					logger.LogInfo($"Item drop reward: {quantity}x {itemGuid.LookupName()}");
				}
			});
		}
		catch (Exception ex)
		{
			Core.LogException(ex, nameof(DropItems));
		}
	}

	public void DropBloodMerlots(PrefabGUID bloodType, float bloodQuality, int quantity, float3 dropPosition)
	{
		var logger = Core.Log;
		var entityManager = Core.EntityManager;

		try
		{
			var gameDataSystem = Core.Server.GetExistingSystemManaged<GameDataSystem>();
			var itemDataLookup = gameDataSystem.ItemHashLookupMap;

			if (!itemDataLookup.TryGetValue(BloodMerlotPrefab, out var itemData))
			{
				Core.Log.LogWarning($"Could not find ItemData for BloodMerlot prefab {BloodMerlotPrefab.GuidHash}");
				return;
			}

			var itemEntity = itemData.Entity;
			if (!itemEntity.Has<StoredBlood>())
			{
				itemEntity.Add<StoredBlood>();
			}

			var storedBlood = new StoredBlood
			{
				BloodQuality = bloodQuality,
				PrimaryBloodType = bloodType
			};
			entityManager.SetComponentData(itemEntity, storedBlood);

			WithDropProxy(dropPosition, proxy =>
			{
				for (int i = 0; i < quantity; i++)
				{
					InventoryUtilitiesServer.CreateDropItem(entityManager, proxy, BloodMerlotPrefab, 1, new Entity());
				}
			});

			logger.LogInfo($"Reward dropped: {quantity} blood merlots of {bloodType.GuidHash} @ {bloodQuality}%");
		}
		catch (Exception ex)
		{
			Core.LogException(ex, nameof(DropBloodMerlots));
		}
	}
}
