using System.Collections;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;

namespace KindredSacrifice.Services;

internal class BuffService
{
    public void ApplyBuff(Entity userEntity, Entity charEntity, PrefabGUID buffGuid, float duration = -1)
    {
        var logger = Core.Log;
        var entityManager = Core.Server.EntityManager;

        if (BuffUtility.HasBuff(entityManager, charEntity, buffGuid))
        {
            Core.StartCoroutine(ApplyBuffCoroutine(userEntity, charEntity, buffGuid, duration));
            return;
        }

        ApplyBuffInternal(userEntity, charEntity, buffGuid, duration);
    }

    void ApplyBuffInternal(Entity userEntity, Entity charEntity, PrefabGUID buffGuid, float duration)
    {
        var logger = Core.Log;
        var entityManager = Core.Server.EntityManager;

        var des = Core.Server.GetExistingSystemManaged<DebugEventsSystem>();
        var buffEvent = new ApplyBuffDebugEvent
        {
            BuffPrefabGUID = buffGuid
        };
        var fromCharacter = new FromCharacter
        {
            User = userEntity,
            Character = charEntity
        };

        des.ApplyBuff(fromCharacter, buffEvent);

        if (!BuffUtility.TryGetBuff(entityManager, charEntity, buffGuid, out Entity buffEntity))
        {
            return;
        }

        // Remove gameplay event components to prevent unwanted behavior
        if (buffEntity.Has<CreateGameplayEventsOnSpawn>())
        {
            buffEntity.Remove<CreateGameplayEventsOnSpawn>();
        }
        if (buffEntity.Has<GameplayEventListeners>())
        {
            buffEntity.Remove<GameplayEventListeners>();
        }

        if (!buffEntity.Has<Buff_Persists_Through_Death>())
        {
            buffEntity.Add<Buff_Persists_Through_Death>();
        }

        if (duration > -1 && duration != 0)
        {
            if (!buffEntity.Has<LifeTime>())
            {
                buffEntity.Add<LifeTime>();
            }

            var lifetime = buffEntity.Read<LifeTime>();
            lifetime.Duration = duration;
            lifetime.EndAction = LifeTimeEndAction.Destroy;
            buffEntity.Write(lifetime);
        }

        else if (duration == -1)
        {
            if (buffEntity.Has<LifeTime>())
            {
                buffEntity.Write(new LifeTime
                {
                    Duration = 0f,
                    EndAction = LifeTimeEndAction.None
                });
            }
            if (buffEntity.Has<RemoveBuffOnGameplayEvent>())
            {
                buffEntity.Remove<RemoveBuffOnGameplayEvent>();
            }
            if (buffEntity.Has<RemoveBuffOnGameplayEventEntry>())
            {
                buffEntity.Remove<RemoveBuffOnGameplayEventEntry>();
            }
        }
    }

    IEnumerator ApplyBuffCoroutine(Entity userEntity, Entity charEntity, PrefabGUID buffGuid, float duration)
    {
        var logger = Core.Log;
        var entityManager = Core.Server.EntityManager;

        // Remove existing buff
        if (BuffUtility.TryGetBuff(entityManager, charEntity, buffGuid, out Entity oldBuffEntity))
        {
            DestroyUtility.Destroy(entityManager, oldBuffEntity, DestroyDebugReason.TryRemoveBuff);
        }

        // Wait until buff is completely removed
        while (BuffUtility.HasBuff(entityManager, charEntity, buffGuid))
        {
            yield return null;
        }

        // Apply fresh buff
        ApplyBuffInternal(userEntity, charEntity, buffGuid, duration);
    }

    public void RemoveBuff(Entity charEntity, PrefabGUID buffGuid)
    {
        if (BuffUtility.TryGetBuff(Core.EntityManager, charEntity, buffGuid, out var buffEntity))
        {
            DestroyUtility.Destroy(Core.EntityManager, buffEntity, DestroyDebugReason.TryRemoveBuff);
        }
    }
}
