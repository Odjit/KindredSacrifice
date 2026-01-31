using System;
using System.Collections;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using KindredSacrifice.Services;
using ProjectM;
using ProjectM.Network;
using ProjectM.Physics;
using ProjectM.Scripting;
using Unity.Entities;
using UnityEngine;

namespace KindredSacrifice;

internal static class Core
{
    static World server;
    public static World Server => server ??= GetServerWorld() ?? throw new Exception("There is no Server world (yet). Did you install a server mod on the client?");
    public static EntityManager EntityManager => Server.EntityManager;
    public static bool IsServer => Application.productName == "VRisingServer";
    public static PrefabCollectionSystem PrefabCollectionSystem { get; internal set; }
    public static DebugEventsSystem DebugEventsSystem { get; internal set; }
    public static ServerGameManager ServerGameManager { get; internal set; }
    public static ServerGameSettingsSystem ServerGameSettingsSystem { get; internal set; }

    public static SacrificeConfigService ConfigService { get; internal set; }
    public static SacrificeService SacrificeService { get; internal set; }
    public static BuffService BuffService { get; } = new();
    public static ItemService ItemService { get; } = new();

    static bool _hasInitialized = false;
    public static bool HasInitialized => _hasInitialized;
    static MonoBehaviour monoBehaviour;

    static EntityQuery networkIdSystemQuery;

    public static ManualLogSource Log { get; } = Plugin.PluginLog;

    static World GetServerWorld()
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world.Name == "Server")
            {
                return world;
            }
        }
        return null;
    }

    public static void LogException(Exception e, string caller = "")
    {
        Log.LogError($"Exception in {caller}: {e.Message}");
        Log.LogError($"Stack trace: {e.StackTrace}");
    }

    public static bool TryGetEntityFromNetworkId(NetworkId networkId, out Entity entity)
    {
        entity = Entity.Null;
        if (!_hasInitialized) return false;

        var networkIdSingleton = networkIdSystemQuery.GetSingleton<NetworkIdSystem.Singleton>();
        var networkIdToEntityMap = networkIdSingleton.GetNetworkIdLookupRO();
        return networkIdToEntityMap.TryGetValue(networkId, out entity);
    }

    public static void InitializeAfterLoaded()
    {
        if (_hasInitialized) return;

        PrefabCollectionSystem = Server.GetExistingSystemManaged<PrefabCollectionSystem>();
        DebugEventsSystem = Server.GetExistingSystemManaged<DebugEventsSystem>();
        ServerGameSettingsSystem = Server.GetExistingSystemManaged<ServerGameSettingsSystem>();
        var serverScriptMapper = Server.GetExistingSystemManaged<ServerScriptMapper>();
        ServerGameManager = serverScriptMapper.GetServerGameManager();

        networkIdSystemQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { ComponentType.ReadOnly<NetworkIdSystem.Singleton>() },
            Options = EntityQueryOptions.IncludeSystems
        });

        ConfigService = new SacrificeConfigService();
        ConfigService.LoadConfig();

        SacrificeService = new SacrificeService();

        _hasInitialized = true;
        Log.LogInfo($"{nameof(InitializeAfterLoaded)} completed");
    }

    public static Coroutine StartCoroutine(IEnumerator routine)
    {
        if (monoBehaviour == null)
        {
            var go = new GameObject("KindredSacrifice");
            monoBehaviour = go.AddComponent<IgnorePhysicsDebugSystem>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        return monoBehaviour.StartCoroutine(routine.WrapToIl2Cpp());
    }
}
