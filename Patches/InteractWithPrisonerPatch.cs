using System;
using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace KindredSacrifice.Patches;

[HarmonyPatch(typeof(InteractWithPrisonerSystem), nameof(InteractWithPrisonerSystem.OnUpdate))]
public static class InteractWithPrisonerPatch
{
	static readonly Dictionary<Entity, DateTime> lastLockoutMessageTime = new();
	const double LockoutMessageCooldownSeconds = 10.0;

	[HarmonyPrefix]
	public static void Prefix(InteractWithPrisonerSystem __instance)
	{
		if (!Core.HasInitialized)
		{
			return;
		}

		try
		{
			var query = __instance._EventQuery;
			var entities = query.ToEntityArray(Allocator.Temp);
			var fromCharacters = query.ToComponentDataArray<FromCharacter>(Allocator.Temp);
			var prisonerEvents = query.ToComponentDataArray<InteractWithPrisonerEvent>(Allocator.Temp);

			for (var i = 0; i < entities.Length; i++)
			{
				var ev = prisonerEvents[i];
				var character = fromCharacters[i].Character;

				if (!Core.TryGetEntityFromNetworkId(ev.Prison, out var prison))
				{
					continue;
				}

				// Check if this is a sacrifice cage
				if (Core.SacrificeService.IsSacrificeCage(prison))
				{
					Entity userEntity = Entity.Null;
					if (character.Has<PlayerCharacter>())
					{
						userEntity = character.Read<PlayerCharacter>().UserEntity;
					}

					switch (ev.PrisonInteraction)
					{
						case EventHelper.PrisonInteraction.Imprison:
							ManageImprison(entities[i], character, prison);
							break;

						case EventHelper.PrisonInteraction.Charm:
						case EventHelper.PrisonInteraction.Kill:
							break;
					}
				}
			}

			entities.Dispose();
			fromCharacters.Dispose();
			prisonerEvents.Dispose();
		}
		catch (Exception ex)
		{
			Core.LogException(ex, nameof(InteractWithPrisonerPatch));
		}
	}

	static void ManageImprison(Entity eventEntity, Entity character, Entity prison)
	{
		try
		{
			Entity userEntity = Entity.Null;
			if (character.Has<PlayerCharacter>())
			{
				userEntity = character.Read<PlayerCharacter>().UserEntity;
			}

			// Check if we're in lockout
			if (Core.SacrificeService.IsInLockout(out int daysRemaining))
			{
				Core.EntityManager.DestroyEntity(eventEntity);

				// Send lockout message to player (with cooldown to prevent spam)
				if (userEntity != Entity.Null && userEntity.Has<User>())
				{
					var now = DateTime.UtcNow;
					bool shouldSendMessage = true;

					if (lastLockoutMessageTime.TryGetValue(userEntity, out var lastTime))
					{
						var timeSince = (now - lastTime).TotalSeconds;
						if (timeSince < LockoutMessageCooldownSeconds)
						{
							shouldSendMessage = false;
						}
					}

					if (shouldSendMessage)
					{
						lastLockoutMessageTime[userEntity] = now;
						CleanupStaleEntries(now);

						var user = userEntity.Read<User>();
						FixedString512Bytes message = $"<color=#b00>Blood moon lockout <color=#d00>active! <color=#f00>Cannot sacrifice for <color=#d00>{daysRemaining} more <color=#b00>night{(daysRemaining != 1 ? "s" : "")}.";
						ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, user, ref message);
					}
				}
			}
			else
			{
				// Not in lockout - trigger sacrifice on next frame
				Core.StartCoroutine(TriggerSacrificeAfterImprisonment(prison, userEntity));
			}
		}
		catch (Exception ex)
		{
			Core.LogException(ex, nameof(ManageImprison));
		}
	}

	static void CleanupStaleEntries(DateTime now)
	{
		if (lastLockoutMessageTime.Count <= 1)
			return;

		var keysToRemove = new List<Entity>();
		foreach (var kvp in lastLockoutMessageTime)
		{
			if ((now - kvp.Value).TotalSeconds > LockoutMessageCooldownSeconds)
			{
				keysToRemove.Add(kvp.Key);
			}
		}

		foreach (var key in keysToRemove)
		{
			lastLockoutMessageTime.Remove(key);
		}
	}

	static System.Collections.IEnumerator TriggerSacrificeAfterImprisonment(Entity prison, Entity userEntity)
	{
		yield return null;

		if (prison.Has<PrisonCell>())
		{
			var prisonCell = prison.Read<PrisonCell>();
			var prisonerEntity = prisonCell.ImprisonedEntity.GetEntityOnServer();

			if (prisonerEntity != Entity.Null && Core.EntityManager.Exists(prisonerEntity))
			{
				Core.SacrificeService.ProcessSacrifice(prison, userEntity);
			}
		}
	}
}
