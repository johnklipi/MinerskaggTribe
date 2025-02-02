using BepInEx.Logging;
using HarmonyLib;
using Polytopia.Data;

namespace PolytopiaMinerskaggTribe;
public static class Main
{
    private static ManualLogSource? modLogger;
    private static uint berserkTurn = 0;
    private static uint teleportTurn = 0;
    private static Dictionary<uint, int> unitToAttacksAmount = new ();
    private static Dictionary<uint, bool> hasTeleportedThisTurn = new ();
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(Main));
        modLogger = logger;
        modLogger.LogMessage("Created mapping for UnitAbility.Type with id throw and index " + PolyMod.Managers.ModManager.autoidx);
        EnumCache<UnitAbility.Type>.AddMapping("throw", (UnitAbility.Type)PolyMod.Managers.ModManager.autoidx);
        EnumCache<UnitAbility.Type>.AddMapping("throw", (UnitAbility.Type)PolyMod.Managers.ModManager.autoidx);
        PolyMod.Managers.ModManager.autoidx++;
        modLogger.LogMessage("Created mapping for UnitAbility.Type with id berserk and index " + PolyMod.Managers.ModManager.autoidx);
        EnumCache<UnitAbility.Type>.AddMapping("berserk", (UnitAbility.Type)PolyMod.Managers.ModManager.autoidx);
        EnumCache<UnitAbility.Type>.AddMapping("berserk", (UnitAbility.Type)PolyMod.Managers.ModManager.autoidx);
        PolyMod.Managers.ModManager.autoidx++;
        modLogger.LogMessage("Created mapping for UnitAbility.Type with id battledestroy and index " + PolyMod.Managers.ModManager.autoidx);
        EnumCache<UnitAbility.Type>.AddMapping("battledestroy", (UnitAbility.Type)PolyMod.Managers.ModManager.autoidx);
        EnumCache<UnitAbility.Type>.AddMapping("battledestroy", (UnitAbility.Type)PolyMod.Managers.ModManager.autoidx);
        PolyMod.Managers.ModManager.autoidx++;
        modLogger.LogMessage("Created mapping for ImprovementAbility.Type with id teleport and index " + PolyMod.Managers.ModManager.autoidx);
        EnumCache<ImprovementAbility.Type>.AddMapping("teleport", (ImprovementAbility.Type)PolyMod.Managers.ModManager.autoidx);
        EnumCache<ImprovementAbility.Type>.AddMapping("teleport", (ImprovementAbility.Type)PolyMod.Managers.ModManager.autoidx);
        PolyMod.Managers.ModManager.autoidx++;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AttackReaction), nameof(AttackReaction.Execute))]
	private static void AttackReaction_Execute(AttackReaction __instance, Il2CppSystem.Action onComplete)
	{
		GameState gameState = GameManager.GameState;
        TileData tile = gameState.Map.GetTile(__instance.action.Origin);
        UnitState unitState = tile.unit;
        if (unitState != null)
        {
            if(unitState.UnitData.unitAbilities.Contains(EnumCache<UnitAbility.Type>.GetType("throw")))
            {
                gameState.ActionStack.Add(new UpgradeAction(unitState.owner, UnitData.Type.Warrior, unitState.coordinates, 0));
            }
        }
	}

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.ExecuteDefault))]
	private static void AttackCommand_ExecuteDefault(AttackCommand __instance, GameState gameState)
	{
        if(berserkTurn != gameState.CurrentTurn)
        {
            unitToAttacksAmount = new Dictionary<uint, int>();
            berserkTurn = gameState.CurrentTurn;
        }
        UnitState unitState;
        gameState.TryGetUnit(__instance.UnitId, out unitState);
        TileData tile = gameState.Map.GetTile(__instance.Target);
        UnitState unit = tile.unit;
		gameState.GameLogicData.TryGetData(unitState.type, out UnitData unitData);
		BattleResults battleResults = BattleHelpers.GetBattleResults(gameState, unitState, unit);
    	if (unitState.HasAbility(EnumCache<UnitAbility.Type>.GetType("berserk"), gameState))
		{
            if (!unitToAttacksAmount.ContainsKey(__instance.UnitId))
            {
                unitToAttacksAmount[__instance.UnitId] = 3;
            }
            unitToAttacksAmount[__instance.UnitId]--;

			WorldCoordinates worldCoordinates = battleResults.shouldMoveToDefeatedEnemyTile ? __instance.Target : __instance.Origin;
			int range = unitData.GetRange();
			bool flag = unitData.GetSightRange() >= range;
			List<WorldCoordinates> attackOptionsAtPosition = UnitDataExtensions.GetAttackOptionsAtPosition(gameState, __instance.PlayerId, worldCoordinates, range, flag, unitState, false).ToArray().ToList();
			if (attackOptionsAtPosition != null && attackOptionsAtPosition.Count > 0 && unitToAttacksAmount[__instance.UnitId] > 0)
			{
				using (List<WorldCoordinates>.Enumerator enumerator = attackOptionsAtPosition.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
                        unitState.attacked = false;
                        break;
					}
				}
			}
		}
	}

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.IsValid))]
	private static void IsValid(ref bool __result, GameState state, ref string validationError)
	{
        __result = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveAction), nameof(MoveAction.ExecuteDefault))]
	private static void MoveAction_ExecuteDefault(MoveAction __instance, GameState gameState)
	{
        if(teleportTurn != gameState.CurrentTurn)
        {
            hasTeleportedThisTurn = new Dictionary<uint, bool>();
            teleportTurn = gameState.CurrentTurn;
        }

        TileData tileFrom = gameState.Map.GetTile(__instance.Path[0]);
;
        if(tileFrom.improvement == null)
            return;

        if(gameState.GameLogicData.TryGetData(tileFrom.improvement.type, out ImprovementData improvementToData))
        {
            if(improvementToData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("teleport")))
            {
                List<TileData> neighbors = gameState.Map.GetArea(tileFrom.coordinates, improvementToData.range, true, false).ToArray().ToList();
                if(!hasTeleportedThisTurn.ContainsKey(__instance.UnitId))
                {
                    hasTeleportedThisTurn[__instance.UnitId] = false;
                }
                foreach (TileData neighbor in neighbors)
                {
                    Il2CppSystem.Collections.Generic.List<WorldCoordinates> moveCoordinates = new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();
                    moveCoordinates.Add(neighbor.coordinates);
                    moveCoordinates.Add(tileFrom.coordinates);

                    if(neighbor.improvement != null)
                    {
                        TileData tileTo = gameState.Map.GetTile(neighbor.coordinates);
                        if(neighbor.improvement.type == improvementToData.type && !hasTeleportedThisTurn[__instance.UnitId])
                        {
                            if(tileTo.unit == null)
                            {
                            if(gameState.TryGetUnit(__instance.UnitId, out UnitState unit))
                            {
                                hasTeleportedThisTurn[__instance.UnitId] = true;
                                gameState.ActionStack.Add(new MoveAction(__instance.PlayerId, __instance.UnitId, moveCoordinates, MoveAction.MoveReason.Command));
                                break;
                            }
                            }
                        }
                    }
                }
            }
        }
    }
}