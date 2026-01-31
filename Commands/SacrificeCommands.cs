using System;
using ProjectM;
using ProjectM.Shared;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;

namespace KindredSacrifice.Commands;

[CommandGroup("sacrifice", "sac")]
internal class SacrificeCommands
{
    static bool TryGetValidCage(ChatCommandContext ctx, out Unity.Entities.Entity cageEntity)
    {
        cageEntity = Unity.Entities.Entity.Null;
        var cage = Core.SacrificeService.GetSacrificeCage();
        if (cage == null)
        {
            ctx.Reply("<color=#c24>No sacrificial site found.");
            return false;
        }
        cageEntity = cage.Value;
        if (!Core.EntityManager.Exists(cageEntity))
        {
            ctx.Reply("<color=#c24>Sacrificial site no longer exists.");
            return false;
        }
        return true;
    }

    [Command("place", description: "Place a sacrifice cage at your cursor position", adminOnly: true)]
    public static void PlaceSacrificeCage(ChatCommandContext ctx)
    {
        try
        {
            var userEntity = ctx.Event.SenderUserEntity;
            var charEntity = ctx.Event.SenderCharacterEntity;

            if (charEntity == Unity.Entities.Entity.Null)
            {
                ctx.Reply("<color=#c24>Unable to find your character.");
                return;
            }

            var cursorPosition = charEntity.Read<EntityAimData>().AimPositionPlane;

            var existingCage = Core.SacrificeService.GetSacrificeCage();
            if (existingCage != null)
            {
                ctx.Reply("<color=#c24>A sacrificial site already exists! Use '.sac remove' to remove it first.");
                return;
            }

            ctx.Reply("<color=#c24>Placing sacrificial site at cursor position.");

            var cageEntity = Core.SacrificeService.SpawnSacrificeCage(cursorPosition, userEntity, charEntity);

            if (cageEntity != Unity.Entities.Entity.Null)
            {
                ctx.Reply("<color=#c24>Sacrificial site placed successfully.");
            }
            else
            {
                ctx.Reply("<color=#c24>Failed to place sacrificial site.");
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(PlaceSacrificeCage));
            ctx.Reply($"Error: {ex.Message}");
        }
    }

    [Command("remove", description: "Remove the sacrificial site", adminOnly: true)]
    public static void RemoveSacrificeCage(ChatCommandContext ctx)
    {
        try
        {
            if (!TryGetValidCage(ctx, out var cageEntity))
                return;

            Core.SacrificeService.RemoveSacrificeCage();
            DestroyUtility.Destroy(Core.EntityManager, cageEntity, DestroyDebugReason.TryRemoveBuff);

            ctx.Reply("<color=#c24>Sacrificial site removed.");
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(RemoveSacrificeCage));
            ctx.Reply($"Error: {ex.Message}");
        }
    }

    [Command("goto", "g", description: "Teleport to the sacrificial site", adminOnly: true)]
    public static void GotoSacrificeCage(ChatCommandContext ctx)
    {
        try
        {
            var charEntity = ctx.Event.SenderCharacterEntity;

            if (charEntity == Unity.Entities.Entity.Null)
            {
                ctx.Reply("<color=#c24>Unable to find your character.");
                return;
            }

            if (!TryGetValidCage(ctx, out var cageEntity))
                return;

            var cagePos = cageEntity.GetPosition();
            var cageRotation = cageEntity.Has<Rotation>() ? cageEntity.Read<Rotation>().Value : quaternion.identity;
            var forward = math.forward(cageRotation);
            var teleportPos = cagePos + (forward * 5f);

            charEntity.Write(new Translation { Value = teleportPos });
            charEntity.Write(new LastTranslation { Value = teleportPos });

            ctx.Reply($"<color=#c24>Teleported to sacrificial site at ({cagePos.x:F1}, {cagePos.y:F1}, {cagePos.z:F1})");
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(GotoSacrificeCage));
            ctx.Reply($"Error: {ex.Message}");
        }
    }

    [Command("reload", description: "Reload the sacrifice configuration", adminOnly: true)]
    public static void ReloadConfig(ChatCommandContext ctx)
    {
        try
        {
            Core.ConfigService.LoadConfig();
            ctx.Reply("<color=#c24>Sacrifice configuration reloaded.");
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(ReloadConfig));
            ctx.Reply($"Error: {ex.Message}");
        }
    }

    [Command("status", description: "Check blood moon progress and lockout status", adminOnly: false)]
    public static void CheckStatus(ChatCommandContext ctx)
    {
        try
        {
            var worldState = Core.ConfigService.WorldState;
            var progress = (worldState.AccumulatedBloodPoints / 10000f) * 100f;

            ctx.Reply("<color=#b00> ̷͠ ͠ ⸸⃝✽⃝ <color=#d00>Blood Sacrifice <color=#f00>Status <color=#d00>✽⃝  ̷͠⸸⃝̷͠  <color=#b00>");
			ctx.Reply($"<color=#b00>Blood Moon Progress: <color=#d00>{progress:F1}% <color=#f00>({worldState.AccumulatedBloodPoints:F0}/10000 <color=#d00>points)");

			var existingCage = Core.SacrificeService.GetSacrificeCage();
			if (existingCage == null)
			{
				ctx.Reply("<color=#b00>Cage Status: <color=#d00>No <color=#e00>sacrifice <color=#f00>altar <color=#e00>has <color=#d00>been <color=#b00>placed ");
				return;
			} 

			if (Core.SacrificeService.IsInLockout(out int daysRemaining))
            {
                ctx.Reply($"<color=#b00>Cage Status: <color=#d00>LOCKED <color=#f00>for {daysRemaining} <color=#d00>more night{(daysRemaining != 1 ? "s" : "")}");
            }
            else
            {
                ctx.Reply("<color=#b00>Cage Status: <color=#d00>ACTIVE <color=#f00>- Ready to accept sacrifices");
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex, nameof(CheckStatus));
            ctx.Reply($"Error: {ex.Message}");
        }
    }

}
