using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KindredSacrifice;

internal static class ECSExtensions
{
	public unsafe static void Write<T>(this Entity entity, T componentData) where T : struct
	{
		var ct = new ComponentType(Il2CppType.Of<T>());
		byte[] byteArray = StructureToByteArray(componentData);
		int size = Marshal.SizeOf<T>();

		fixed (byte* p = byteArray)
		{
			Core.EntityManager.SetComponentDataRaw(entity, ct.TypeIndex, p, size);
		}
	}

	public static byte[] StructureToByteArray<T>(T structure) where T : struct
	{
		int size = Marshal.SizeOf(structure);
		byte[] byteArray = new byte[size];
		IntPtr ptr = Marshal.AllocHGlobal(size);

		Marshal.StructureToPtr(structure, ptr, true);
		Marshal.Copy(ptr, byteArray, 0, size);
		Marshal.FreeHGlobal(ptr);

		return byteArray;
	}

	public unsafe static T Read<T>(this Entity entity) where T : struct
	{
		var ct = new ComponentType(Il2CppType.Of<T>());
		void* rawPointer = Core.EntityManager.GetComponentDataRawRO(entity, ct.TypeIndex);
		T componentData = Marshal.PtrToStructure<T>(new IntPtr(rawPointer));

		return componentData;
	}
	public static DynamicBuffer<T> ReadBuffer<T>(this Entity entity) where T : struct
	{
		return Core.Server.EntityManager.GetBuffer<T>(entity);
	}
	public static bool Has<T>(this Entity entity)
	{
		var ct = new ComponentType(Il2CppType.Of<T>());
		return Core.EntityManager.HasComponent(entity, ct);
	}

	public static string LookupName(this PrefabGUID prefabGuid)
	{
		var prefabCollectionSystem = Core.Server.GetExistingSystemManaged<PrefabCollectionSystem>();
		return (prefabCollectionSystem._PrefabLookupMap.GuidToEntityMap.ContainsKey(prefabGuid)
			? prefabCollectionSystem._PrefabLookupMap.GetName(prefabGuid) + " PrefabGuid(" + prefabGuid.GuidHash + ")" : "GUID Not Found");
	}

	public static void Add<T>(this Entity entity)
	{
		var ct = new ComponentType(Il2CppType.Of<T>());
		Core.EntityManager.AddComponent(entity, ct);
	}

	public static void Remove<T>(this Entity entity)
	{
		var ct = new ComponentType(Il2CppType.Of<T>());
		Core.EntityManager.RemoveComponent(entity, ct);
	}

	public static float3 GetPosition(this Entity entity)
	{
		if (entity.Has<LocalToWorld>())
		{
			return entity.Read<LocalToWorld>().Position;
		}
		return float3.zero;
	}

	public static Entity GetCharacter(this Entity userEntity)
	{
		var user = userEntity.Read<User>();
		return user.LocalCharacter._Entity;
	}

	public static bool ExistsAndValid(this Entity entity)
	{
		return entity != Entity.Null && Core.EntityManager.Exists(entity);
	}
}
