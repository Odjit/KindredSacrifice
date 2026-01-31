using Stunlock.Core;

namespace KindredSacrifice.Data;

public static class Prefabs
{
	// Ritual Cage
	public static readonly PrefabGUID TM_SpecialStation_PrisonCell = new PrefabGUID(-1253061408);

	// Ritual element prefabs
	public static readonly PrefabGUID TM_Castle_ObjectDecor_Gothic_Brazier03_Orange = new(224878241);
    public static readonly PrefabGUID TM_Wilderness_Pyre = new(-961311808);
    public static readonly PrefabGUID TM_Castle_Module_Parent_RoundTable_3x3_Cabal01 = new(-1666587665);

    // Buff prefabs
    public static readonly PrefabGUID Buff_SacrificeFlames = new(451676082);
    public static readonly PrefabGUID Buff_PerfectSacrifice_Table = new(136816739); // AB_Vampire_Dracula_BloodBoltSwarm_ChannelBuff
    public static readonly PrefabGUID Buff_PerfectSacrifice_Cage = new(40754527);
    public static readonly PrefabGUID Buff_General_Ignite = new(1533067119); // Ignite debuff for sacrifices

    // Map icon prefabs
    public static readonly PrefabGUID MapIcon_ProxyObject_POI_Unknown = new(636813227);
    public static readonly PrefabGUID MapIcon_POI_Discover_Unknown = new(-1443504104);

}
