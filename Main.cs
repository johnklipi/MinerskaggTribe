using BepInEx.Logging;
using HarmonyLib;
using Polytopia.Data;

namespace PolytopiaMinerskaggTribe;

public static class Main // probably the WORST FUCKING CODE I'VE EVER WRITTEN.
{
    internal static string stateMarker = "Minerskagg";
    internal static ManualLogSource? modLogger;
    // private static Dictionary<int, string> shards = new();
    internal static Dictionary<int, ShardsInfo> shards = new();
    internal record ShardsInfo(int yellowShardCount, int purpleShardCount, int blueShardCount);
    internal static bool currentlyWagoning = false;
    internal static WorldCoordinates? chosenTileCoordinates = null;

    public static void Load(ManualLogSource logger)
    {
        PolyMod.Loader.AddPatchDataType("cityReward", typeof(CityReward));
        int value = (int)Enum.GetValues(typeof(TileData.EffectType)).Cast<TileData.EffectType>().Last();
        value++;
        EnumCache<TileData.EffectType>.AddMapping("railed", (TileData.EffectType)value);
        EnumCache<TileData.EffectType>.AddMapping("railed", (TileData.EffectType)value);
        value++;
        EnumCache<TileData.EffectType>.AddMapping("doubled", (TileData.EffectType)value);
        EnumCache<TileData.EffectType>.AddMapping("doubled", (TileData.EffectType)value);
        Harmony.CreateAndPatchAll(typeof(Main));
        Harmony.CreateAndPatchAll(typeof(UI));
        modLogger = logger;

    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateInternal))]
    private static void MapGenerator_GenerateInternal(ref MapData __result, MapGenerator __instance, int seed, GameState gameState, MapGeneratorSettings settings)
    {
        System.Random random = new System.Random(seed);
        Dictionary<int, TribeData> cachedClimates = new();
        foreach (TileData tileData in __result.tiles)
        {
            if (tileData.resource != null)
            {
                if (tileData.resource.type == ResourceData.Type.Metal)
                {
                    TribeData tribeData;
                    if (!cachedClimates.ContainsKey(tileData.climate))
                    {
                        TribeData.Type type = gameState.GameLogicData.GetTribeTypeFromStyle(tileData.climate);
                        if (gameState.GameLogicData.TryGetData(type, out tribeData))
                        {
                            cachedClimates[tileData.climate] = tribeData;
                        }
                    }
                    else
                    {
                        tribeData = cachedClimates[tileData.climate];
                    }
                    if (tribeData.HasAbility(EnumCache<TribeAbility.Type>.GetType("geology")))
                    {
                        long resource = random.NextInt64(3);
                        ResourceData.Type resourceType = ResourceData.Type.None;
                        switch (resource)
                        {
                            case 0:
                                resourceType = EnumCache<ResourceData.Type>.GetType("yellowshard");
                                break;
                            case 1:
                                resourceType = EnumCache<ResourceData.Type>.GetType("purpleshard");
                                break;
                            case 2:
                                resourceType = EnumCache<ResourceData.Type>.GetType("blueshard");
                                break;
                        }
                        tileData.resource.type = resourceType;
                    }
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TileDataExtensions), nameof(TileDataExtensions.CalculateWork), typeof(TileData), typeof(GameState), typeof(int))]
    public static void TileDataExtensions_CalculateWork(ref int __result, TileData tile, GameState gameState, int improvementLevel)
    {
        if (tile.HasEffect(EnumCache<TileData.EffectType>.GetType("doubled")))
        {
            __result *= 2;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartTurnAction), nameof(StartTurnAction.ExecuteDefault))]
    private static void StartTurnAction_ExecuteDefault(StartTurnAction __instance, GameState gameState)
    {
        // modLogger.LogInfo(__instance.PlayerId);
        foreach (TileData tileData in gameState.Map.tiles)
        {
            if (tileData.owner == __instance.PlayerId && gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState))
            {
                if (tileData.improvement != null)
                {
                    if (gameState.GameLogicData.TryGetData(tileData.improvement.type, out ImprovementData improvementData))
                    {
                        if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("dig")) && tileData.resource != null)
                        {
                            string resourceName = EnumCache<ResourceData.Type>.GetName(tileData.resource.type);
                            TileData capitalTile = gameState.Map.GetTile(playerState.startTile);
                            if (capitalTile.improvement != null)
                            {
                                if (EnumCache<CityReward>.TryGetType(resourceName, out CityReward reward))
                                {
                                    capitalTile.improvement.AddReward(reward);
                                    if (tileData.HasEffect(EnumCache<TileData.EffectType>.GetType("doubled")))
                                    {
                                        capitalTile.improvement.AddReward(reward);
                                    }
                                    // Console.Write("Adding reward OF TYPE " + resourceName);
                                    // Console.Write("For " + rulingTile.improvement.type);
                                    // Console.Write("For " + rulingTile.coordinates);
                                }
                            }
                        }
                        if (tileData.HasEffect(EnumCache<TileData.EffectType>.GetType("doubled")))
                        {
                            tileData.RemoveEffect(EnumCache<TileData.EffectType>.GetType("doubled"));
                        }
                    }
                }
            }
        }
    }

    internal static void DeductShards(byte playerId, CityReward shardType, int cost)
    {
        ShardsInfo info = shards[playerId];
        int yellowShardCount = info.yellowShardCount;
        int purpleShardCount = info.purpleShardCount;
        int blueShardCount = info.blueShardCount;
        if (GameManager.GameState.TryGetPlayer(playerId, out PlayerState playerState))
        {
            TileData tileData = GameManager.GameState.Map.GetTile(playerState.startTile);
            if (tileData.improvement != null)
            {
                if (tileData.improvement.type == ImprovementData.Type.City)
                {
                    if (tileData.improvement.rewards != null)
                    {
                        for (int i = 0; i < cost; i++)
                        {
                            tileData.improvement.rewards.Remove(shardType);
                            if (shardType == EnumCache<CityReward>.GetType("yellowshard"))
                            {
                                yellowShardCount--;
                            }
                            if (shardType == EnumCache<CityReward>.GetType("purpleshard"))
                            {
                                purpleShardCount--;
                            }
                            if (shardType == EnumCache<CityReward>.GetType("blueshard"))
                            {
                                blueShardCount--;
                            }
                        }
                    }
                }
            }
        }
        shards[playerId] = new ShardsInfo(yellowShardCount, purpleShardCount, blueShardCount);
        UI.UpdateShardCount(UI.yellowShardContainer!, yellowShardCount);
        UI.UpdateShardCount(UI.purpleShardContainer!, purpleShardCount);
        UI.UpdateShardCount(UI.blueShardContainer!, blueShardCount);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ResourceManager), nameof(ResourceManager.OnRefreshWallets))]
    private static void OnRefreshWallets(byte playerId)
    {
        // ResourceManager.GetWallet(playerId).SyncWithState();
        // ResourceEvents.ResourceChanged(playerId);
        // Console.Write("OnRefreshWallets");
        int yellowShardCount = 0;
        int purpleShardCount = 0;
        int blueShardCount = 0;
        if (GameManager.GameState.TryGetPlayer(playerId, out PlayerState playerState))
        {
            TileData tileData = GameManager.GameState.Map.GetTile(playerState.startTile);
            if (tileData.improvement != null)
            {
                if (tileData.improvement.type == ImprovementData.Type.City)
                {
                    if (tileData.improvement.rewards != null)
                    {
                        foreach (var reward in tileData.improvement.rewards)
                        {
                            // Console.Write(reward);
                            if (reward == EnumCache<CityReward>.GetType("yellowshard"))
                            {
                                yellowShardCount++;
                            }
                            if (reward == EnumCache<CityReward>.GetType("purpleshard"))
                            {
                                purpleShardCount++;
                            }
                            if (reward == EnumCache<CityReward>.GetType("blueshard"))
                            {
                                blueShardCount++;
                            }
                        }
                    }
                }
            }
        }
        shards[playerId] = new ShardsInfo(yellowShardCount, purpleShardCount, blueShardCount);
        UI.UpdateShardCount(UI.yellowShardContainer!, yellowShardCount);
        UI.UpdateShardCount(UI.purpleShardContainer!, purpleShardCount);
        UI.UpdateShardCount(UI.blueShardContainer!, blueShardCount);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ResourceBar), nameof(ResourceBar.RefreshIncome))]
    private static void RefreshIncome(byte playerId)
    {
        // Console.Write("ResourceBar.RefreshIncome");
        // if (shards.ContainsKey(playerId))
        // {
        //     var info = shards[playerId];
        //     UpdateShardCount(yellowShardContainer, info.yellowShardCount);
        //     UpdateShardCount(purpleShardContainer, info.purpleShardCount);
        //     UpdateShardCount(blueShardContainer, info.blueShardCount);
        //     //yellowShardContainer.
        // }
        int yellowShardCount = 0;
        int purpleShardCount = 0;
        int blueShardCount = 0;
        if (GameManager.GameState.TryGetPlayer(playerId, out PlayerState playerState))
        {
            foreach (TileData tileData in GameManager.GameState.Map.tiles)
            {
                int shardsToAdd = 1;
                if (tileData.HasEffect(EnumCache<TileData.EffectType>.GetType("doubled")))
                {
                    shardsToAdd *= 2;
                }
                if (tileData.owner == playerId && tileData.improvement != null)
                    {
                        if (GameManager.GameState.GameLogicData.TryGetData(tileData.improvement.type, out ImprovementData improvementData))
                        {
                            if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("dig")) && tileData.resource != null)
                            {
                                if (tileData.resource.type == EnumCache<ResourceData.Type>.GetType("yellowshard"))
                                {
                                    yellowShardCount += shardsToAdd;
                                }
                                if (tileData.resource.type == EnumCache<ResourceData.Type>.GetType("purpleshard"))
                                {
                                    purpleShardCount += shardsToAdd;
                                }
                                if (tileData.resource.type == EnumCache<ResourceData.Type>.GetType("blueshard"))
                                {
                                    blueShardCount += shardsToAdd;
                                }
                            }
                        }
                    }
            }
        }
        UI.UpdateShardIncome(UI.yellowShardContainer!, yellowShardCount);
        UI.UpdateShardIncome(UI.purpleShardContainer!, purpleShardCount);
        UI.UpdateShardIncome(UI.blueShardContainer!, blueShardCount);
    }


    [HarmonyPrefix]
    [HarmonyPatch(typeof(MoveCommand), nameof(MoveCommand.Execute))]
    public static bool MoveCommand_Execute(MoveCommand __instance, GameState gameState)
    {
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveAction), nameof(MoveAction.ExecuteDefault))]
    private static void MoveAction_ExecuteDefault(MoveAction __instance, GameState gameState)
    {
        UnitState unitState;
        PlayerState playerState;
        UnitData unitData;
        if (gameState.TryGetUnit(__instance.UnitId, out unitState) && gameState.TryGetPlayer(__instance.PlayerId, out playerState) && gameState.GameLogicData.TryGetData(unitState.type, out unitData))
        {
            TileData unitTile = gameState.Map.GetTile(unitState.coordinates);
            if (!unitTile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")) && unitState.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")) && !unitTile.HasImprovement(ImprovementData.Type.City))
            {
                gameState.ActionStack.Add(new DisembarkAction(__instance.PlayerId, unitTile.coordinates));
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.TrainUnit))]
    private static bool ActionUtils_TrainUnit(ref UnitState __result, GameState gameState, PlayerState playerState, TileData tile, ref UnitData unitData)
    {
        if (tile == null)
        {
            return true;
        }
        if (tile.unit == null)
        {
            return true;
        }
        if (currentlyWagoning)
        {
            currentlyWagoning = false;
            gameState.GameLogicData.TryGetData(EnumCache<UnitData.Type>.GetType("minecart"), out unitData);
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.IsVehicle))]
    public static void UnitDataExtensions_IsVehicle(ref bool __result, UnitData unitData)
    {
        if (__result && unitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")))
        {
            __result = false;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.GetMoveOptions))]
    private static void PathFinder_GetMoveOptions(ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result, GameState gameState, WorldCoordinates start, int maxCost, UnitState unit)
    {
        if (unit == null)
            return;

        if (unit.UnitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")))
        {
            HashSet<WorldCoordinates> visited = new();
            Queue<WorldCoordinates> toExplore = new();

            toExplore.Enqueue(start);
            visited.Add(start);

            while (toExplore.Count > 0)
            {
                WorldCoordinates current = toExplore.Dequeue();
                List<TileData> neighbors = gameState.Map.GetTileNeighbors(current).ToArray().ToList();

                foreach (TileData tile in neighbors)
                {
                    if (!visited.Contains(tile.coordinates) &&
                        tile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")))
                    {
                        visited.Add(tile.coordinates);
                        toExplore.Enqueue(tile.coordinates);
                    }
                }
            }

            Il2CppSystem.Collections.Generic.List<WorldCoordinates> newResult = new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();
            foreach (var visitedTile in visited)
            {
                TileData tileData = gameState.Map.GetTile(visitedTile);
                if (tileData.GetExplored(unit.owner))
                {
                    newResult.Add(visitedTile);
                    List<TileData> neighbors = gameState.Map.GetTileNeighbors(visitedTile).ToArray().ToList();
                    foreach (TileData neighbor in neighbors)
                    {
                        if (__result.Contains(neighbor.coordinates) && !neighbor.HasEmbarkImprovement(gameState) && tileData.GetExplored(unit.owner))
                        {
                            newResult.Add(neighbor.coordinates);
                        }
                    }
                }
            }
            __result = newResult;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetUnitActions))]
    public static void CommandUtils_GetUnitActions(ref Il2CppSystem.Collections.Generic.List<CommandBase> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable = false)
    {
        UnitState unit = tile.unit;
        if (unit == null)
        {
            return;
        }
        if (unit.owner != player.Id)
        {
            return;
        }
        foreach (ImprovementData improvementData in gameState.GameLogicData.GetUnlockedImprovements(player))
        {
            if (improvementData.HasAbility(ImprovementAbility.Type.Manual) && gameState.GameLogicData.CanBuild(gameState, tile, player, improvementData) && improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("actionless")))
            {
                if (unit.CanBuild())
                {
                    for (int i = __result.Count - 1; i >= 0; i--)
                    {
                        CommandBase command = __result[i];
                        if (command.GetCommandType() == CommandType.Build)
                        {
                            BuildCommand buildCommand = command.Cast<BuildCommand>();
                            if (
                                buildCommand.PlayerId == player.Id &&
                                buildCommand.Type == improvementData.type &&
                                buildCommand.Coordinates == tile.coordinates)
                            {
                                __result.RemoveAt(i);
                            }
                        }
                    }
                }
                else
                {
                    CommandUtils.AddCommand(gameState, __result, new BuildCommand(player.Id, improvementData.type, tile.coordinates), includeUnavailable);
                }
            }
        }
        return;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapDataExtensions), nameof(MapDataExtensions.UpdateRoutes))]
    private static bool MapDataExtensions_UpdateRoutes(GameState gameState, Il2CppSystem.Collections.Generic.List<TileData> changedTiles)
    {
        UpdateRoutesV2(gameState, changedTiles); // FUCKING MIDJIWAN CODE ISTG I JUST CANT COMPREHEND HALF OF THIS SHIT PLEASE SOMEONE SEND AMBULANCE IM SERIOUSLY GOING SLOWLY INSANE MY MENTAL HEALTH IS DECLINING AND WITH SUCH PACE IM GONNA GET INTO ASYLUM
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.ExecuteDefault))]
    public static bool BuildAction_ExecuteDefault(BuildAction __instance, GameState gameState)
    {
        TileData tile = gameState.Map.GetTile(__instance.Coordinates);
        if (tile != null && gameState.GameLogicData.TryGetData(__instance.Type, out ImprovementData improvementData) && gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState))
        {
            if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("costspurple")))
            {
                DeductShards(__instance.PlayerId, EnumCache<CityReward>.GetType("purpleshard"), 5);
            }
            if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("costsblue")))
            {
                DeductShards(__instance.PlayerId, EnumCache<CityReward>.GetType("blueshard"), 5);
            }
            if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("costsyellow")))
            {
                DeductShards(__instance.PlayerId, EnumCache<CityReward>.GetType("yellowshard"), 5);
            }
            if (__instance.Type == EnumCache<ImprovementData.Type>.GetType("exploretile"))
            {
                gameState.ActionStack.Add(new ExploreAction(__instance.PlayerId, tile.coordinates));
                return false;
            }
            if (__instance.Type == EnumCache<ImprovementData.Type>.GetType("doublework"))
            {
                tile.AddEffect(EnumCache<TileData.EffectType>.GetType("doubled"));
                return false;
            }
            if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("rail")))
                {
                    tile.AddEffect(EnumCache<TileData.EffectType>.GetType("railed"));
                    Il2CppSystem.Collections.Generic.List<TileData> neighbours = gameState.Map.GetTileNeighbors(tile.coordinates);
                    foreach (TileData tileData in neighbours)
                    {
                        if (tileData.HasImprovement(ImprovementData.Type.City))
                        {
                            tileData.AddEffect(EnumCache<TileData.EffectType>.GetType("railed"));
                        }
                    }

                    Il2CppSystem.Collections.Generic.List<byte> list = new Il2CppSystem.Collections.Generic.List<byte>();
                    list.Add(__instance.PlayerId);
                    gameState.ActionStack.Add(new UpdateCityConnectionsAction(__instance.PlayerId, list));
                    gameState.ActionStack.Add(new UpdateRoutesAction(__instance.PlayerId));
                    return false;
                }
            UnitState unit = tile.unit;
            if (unit != null)
            {
                if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("depot")))
                {
                    if (tile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")) && !unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")))
                    {
                        gameState.ActionStack.Add(new EmbarkAction(__instance.PlayerId, tile.coordinates));
                        currentlyWagoning = true;
                        return false;
                    }
                }
                else if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("dedepot")))
                {
                    if (unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")))
                    {
                        gameState.ActionStack.Add(new DisembarkAction(__instance.PlayerId, tile.coordinates));
                    }
                    return false;
                }
                if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("restoreactions")))
                {
                    if (unit.attacked)
                    {
                        unit.attacked = false;
                    }
                    else if (unit.moved)
                    {
                        unit.moved = false;
                    }
                    return false;
                }
            }
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.ExecuteDefault))]
    public static void BuildAction_ExecuteDefault_Postfix(BuildAction __instance, GameState gameState)
    {
        TileData tile = gameState.Map.GetTile(__instance.Coordinates);
        ImprovementData improvementData;
        PlayerState playerState;
        if (tile != null && gameState.GameLogicData.TryGetData(__instance.Type, out improvementData)
            && gameState.TryGetPlayer(__instance.PlayerId, out playerState)
            && __instance.DeductCost && UsesShards(improvementData))
        {
            playerState.Currency += improvementData.GetCurrencyCost();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildCommand), nameof(BuildCommand.IsValid))]
    public static void BuildCommand_IsValid(ref bool __result, BuildCommand __instance, GameState state, ref string validationError)
    {
        if (!__result && __instance.Type == EnumCache<ImprovementData.Type>.GetType("exploretile"))
        {
            Console.Write(validationError);
            __result = true;
        }
        if (__result && state.GameLogicData.TryGetData(__instance.Type, out ImprovementData improvementData) && UsesShards(improvementData))
            {
                ShardsInfo shardInfo = shards[__instance.PlayerId];
                if (
                    (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("costspurple")) && shardInfo.purpleShardCount < 5)
                    || (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("costsblue")) && shardInfo.blueShardCount < 5)
                    || (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("costsyellow")) && shardInfo.yellowShardCount < 5))
                {
                    validationError = "Not enough shards";
                    __result = false;
                }
            }
    }

    public static bool UsesShards(ImprovementData improvementData)
    {
        if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("costspurple"))
            || improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("costsblue"))
            || improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("costsyellow")))
        {
            return true;
        }
        return false;
    }

    public static bool IsWorking(ImprovementData improvementData)
    {
        return improvementData.work > 0 || improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("dig"));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void CanBuild(ref bool __result, GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement)
    {
        if (improvement.type == EnumCache<ImprovementData.Type>.GetType("doublework"))
        {
            if (!(tile.improvement != null && gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData data) && IsWorking(data)))
            {
                __result = false;
            }
            return;
        }
        if (!tile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")) && __result && improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("depot")))
        {
            __result = false;
        }
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("depot")) && __result && tile.unit != null && tile.unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")))
        {
            __result = false;
        }
        if (tile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")))
        {
            if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("rail")) && __result)
            {
                __result = false;
            }
        }
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("dedepot")) && __result)
        {
            __result = !(tile.unit == null || !tile.unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")) || !tile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")));
        }
    }

    public static void UpdateRoutesV2(GameState gameState, Il2CppSystem.Collections.Generic.List<TileData> changedTiles)
    {
        if (gameState == null || gameState.Map == null)
            return;
        var map = gameState.Map;

        Il2CppSystem.Collections.Generic.List<TileData> list = new Il2CppSystem.Collections.Generic.List<TileData>();
        map.ResetRoutes(list);

        var empireTiles = new Il2CppSystem.Collections.Generic.List<TileData>();
        var rails = new Il2CppSystem.Collections.Generic.List<TileData>();
        var playerRouteOpeners = new Il2CppSystem.Collections.Generic.List<TileData>();
        var routeOpeners = new Il2CppSystem.Collections.Generic.List<TileData>();

        foreach (PlayerState player in gameState.PlayerStates)
        {
            if (player == null || player.Id == 255)
                continue;
            map.GetPlayerEmpireTiles(player.Id, empireTiles);
            rails.Clear();
            playerRouteOpeners.Clear();
            routeOpeners.Clear();
            Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> unlockedMovements = gameState.GameLogicData.GetUnlockedMovements(player);
            
            foreach (var tileData in map.Tiles)
            {
                if (tileData.owner == 0 && tileData.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")))
                {
                    rails.Add(tileData);
                }
            }
            foreach (TileData tile in empireTiles)
            {
                if (tile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")))
                {
                    rails.Add(tile);
                }
                if (tile.improvement != null && gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData improvementData))
                {
                    if (improvementData.IsRouteOpener())
                    {
                        playerRouteOpeners.Add(tile);
                    }
                    if (improvementData.type == ImprovementData.Type.City)
                    {
                        routeOpeners.Add(tile);
                    }
                }
            }
            for (int i = 0; i < rails.Count; i++)
            {
                TileData sourceTile = rails[i];
                if (sourceTile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")))
                {
                    for (int j = i + 1; j < rails.Count; j++)
                    {
                        TileData targetTile = rails[j];
                        FindPathBetweenRoutersV2(sourceTile, targetTile, unlockedMovements, player, gameState, changedTiles);
                    }
                }
            }
            for (int i = 0; i < playerRouteOpeners.Count; i++)
                {
                    TileData sourceTile = playerRouteOpeners[i];
                    if (gameState.GameLogicData.TryGetData(sourceTile.improvement.type, out ImprovementData improvementData2))
                    {
                        for (int j = i + 1; j < playerRouteOpeners.Count; j++)
                        {
                            TileData targetTile = playerRouteOpeners[j];
                            FindPathBetweenRouters(sourceTile, targetTile, unlockedMovements, player, gameState, changedTiles);
                        }
                        if (improvementData2.HasAbility(ImprovementAbility.Type.Network))
                        {
                            ushort num = 0;
                            for (int k = 0; k < routeOpeners.Count; k++)
                            {
                                TileData tile = routeOpeners[k];
                                if (FindPathBetweenRouters(sourceTile, tile, unlockedMovements, player, gameState, changedTiles))
                                {
                                    num += 1;
                                }
                            }
                        }
                    }
                }
        }

        foreach (var tile in list)
        {
            if (!tile.hasRoute && !changedTiles.Contains(tile))
            {
                changedTiles.Add(tile);
            }

            tile.hadRoute = false;
        }
    }

    public static bool FindPathBetweenRoutersV2(
        TileData origin,
        TileData destination,
        Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> allowedTerrain,
        PlayerState player,
        GameState gameState,
        Il2CppSystem.Collections.Generic.List<TileData> changedTiles
    )
    {
        if (origin == null || destination == null || gameState == null)
            return false;
        var logicData = gameState.GameLogicData;
        if (logicData == null)
            return false;
        int distance = MapDataExtensions.ChebyshevDistance(origin.coordinates, destination.coordinates);
        if (1 < distance)
        {
            return false;
        }
        var settings = PathFinderSettings.CreateRouterSettings(player, allowedTerrain, gameState.Version, gameState);
        Il2CppSystem.Collections.Generic.List<WorldCoordinates> path = PathFinder.GetPath(
            gameState.Map,
            origin.coordinates,
            destination.coordinates,
            1,
            settings
        );
        if (path == null || path.Count < 1)
            return false;
        foreach (var coord in path)
        {
            int idx = MapDataExtensions.GetTileIndex(gameState.Map, coord);
            if (idx < 0)
                continue;

            var tile = gameState.Map.Tiles[idx];
            if (tile == null)
                continue;

            if (!tile.hasRoute)
            {
                tile.hasRoute  = true;
                if (!changedTiles.Contains(tile))
                    changedTiles.Add(tile);
            }
        }
        return true;
    }

    public static bool FindPathBetweenRouters(
        TileData origin,
        TileData destination,
        Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> allowedTerrain,
        PlayerState player,
        GameState gameState,
        Il2CppSystem.Collections.Generic.List<TileData> changedTiles
    )
    {
        if (origin == null || destination == null || gameState == null)
            return false;

        var logicData = gameState.GameLogicData;
        if (logicData == null || origin.improvement == null || destination.improvement == null)
            return false;

        if (!logicData.TryGetData(origin.improvement.type, out var originImp) ||
            !logicData.TryGetData(destination.improvement.type, out var destImp))
            return false;

        int distance = MapDataExtensions.ChebyshevDistance(origin.coordinates, destination.coordinates);

        if (originImp.range < distance)
            return false;
        var usableTerrain = new Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData>();
        if (destImp.type != ImprovementData.Type.City)
        {
            foreach (var terrain in originImp.routes)
            {
                if (destImp.routes.Contains(terrain))
                    usableTerrain.Add(terrain);
            }

            if (usableTerrain.Count == 0)
                return false;
        }
        else
        {
            usableTerrain = originImp.routes;
        }

        var settings = PathFinderSettings.CreateRouterSettings(player, usableTerrain, gameState.Version, gameState);

        Il2CppSystem.Collections.Generic.List<WorldCoordinates> path = PathFinder.GetPath(
            gameState.Map,
            origin.coordinates,
            destination.coordinates,
            originImp.range,
            settings
        );

        if (path == null || path.Count < 1)
            return false;

        foreach (var coord in path)
        {
            int idx = MapDataExtensions.GetTileIndex(gameState.Map, coord);
            if (idx < 0)
                continue;

            var tile = gameState.Map.Tiles[idx];
            if (tile == null)
                continue;

            if (!tile.hasRoute)
            {
                tile.hasRoute  = true;
                if (!changedTiles.Contains(tile))
                    changedTiles.Add(tile);
            }
        }
        return true;
    }
}