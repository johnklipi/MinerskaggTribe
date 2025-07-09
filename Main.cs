using AsmResolver.PE.Win32Resources.Builder;
using BepInEx.Logging;
using EnumsNET;
using HarmonyLib;
using Il2CppSystem.Runtime.Remoting.Messaging;
using Polytopia.Data;
using UnityEngine;

namespace PolytopiaMinerskaggTribe;

public static class Main
{
    private static string stateMarker = "Minerskagg";
    private static ManualLogSource? modLogger;
    // private static Dictionary<int, string> shards = new();
    private static Dictionary<int, ShardsInfo> shards = new();
    private record ShardsInfo(int yellowShardCount, int purpleShardCount, int blueShardCount);
    private static Transform? yellowShardContainer = null;
    private static Transform? purpleShardContainer = null;
    private static Transform? blueShardContainer = null; private static bool currentlyWagoning = false;

    public static void Load(ManualLogSource logger)
    {
        PolyMod.Loader.AddPatchDataType("cityReward", typeof(CityReward));
        int value = (int)Enum.GetValues(typeof(TileData.EffectType)).Cast<TileData.EffectType>().Last();
        value++;
        EnumCache<TileData.EffectType>.AddMapping("railed", (TileData.EffectType)value);
        EnumCache<TileData.EffectType>.AddMapping("railed", (TileData.EffectType)value);
        Harmony.CreateAndPatchAll(typeof(Main));
        modLogger = logger;

    }

    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(GameState), nameof(GameState.Serialize))]
    // public static void GameState_Serialize(Il2CppSystem.IO.BinaryWriter writer, int version)
    // {
    //     writer.Write(stateMarker);
    //     writer.Write((ushort)shards.Count);

    //     foreach (var kvp in shards)
    //     {
    //         writer.Write(kvp.Key);
    //         writer.Write(kvp.Value);
    //     }
    // }

    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(GameState), nameof(GameState.Deserialize))]
    // public static void GameState_Deserialize(Il2CppSystem.IO.BinaryReader reader, int version)
    // {
    //     var stream = reader.BaseStream;
    //     long oldPos = stream.Position;

    //     try
    //     {
    //         if (stream.Length - stream.Position < 8)
    //             return;
    //         string marker = reader.ReadString();
    //         if (marker != stateMarker)
    //         {
    //             stream.Position = oldPos;
    //             return;
    //         }
    //         if (stream.Length - stream.Position < 2)
    //             return;

    //         ushort count = reader.ReadUInt16();
    //         shards = new Dictionary<int, string>(count);

    //         for (int i = 0; i < count; i++)
    //         {
    //             if (stream.Position >= stream.Length)
    //                 throw new EndOfStreamException();

    //             int key = reader.ReadInt32();

    //             if (stream.Position >= stream.Length)
    //                 throw new EndOfStreamException();

    //             string value = reader.ReadString();

    //             shards[key] = value;
    //         }
    //     }
    //     catch (System.IO.EndOfStreamException)
    //     {
    //         stream.Position = oldPos;
    //         shards?.Clear();
    //     }
    //     catch
    //     {
    //         stream.Position = oldPos;
    //     }
    // }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ResourceBar), nameof(ResourceBar.OnEnable))]
    private static void OnEnable(ResourceBar __instance)
    {
        Transform? topRowContainer = null;
        for (int i = 0; i < __instance.transform.childCount; i++)
        {
            Transform resourceBarChild = __instance.transform.GetChild(i);
            if (resourceBarChild.gameObject.name == "Top Row Container")
            {
                topRowContainer = resourceBarChild;
            }
        }
        Transform? topRow = null;
        if (topRowContainer != null)
        {
            for (int i = 0; i < topRowContainer.childCount; i++)
            {
                Transform topRowContainerChild = topRowContainer.GetChild(i);
                if (topRowContainerChild.gameObject.name == "Top Row")
                {
                    topRow = topRowContainerChild;
                }
            }
        }
        if (topRow != null)
        {
            for (int i = 0; i < topRow.childCount; i++)
            {
                Transform topRowChild = topRow.GetChild(i);
                if (topRowChild.gameObject.name == "CurrencyContainer")
                {
                    yellowShardContainer = GameObject.Instantiate(topRowChild, topRow);
                    yellowShardContainer.gameObject.name = "Yellow Shards";
                    UpdateShardIncome(yellowShardContainer, 0);
                    purpleShardContainer = GameObject.Instantiate(topRowChild, topRow);
                    purpleShardContainer.gameObject.name = "Purple Shards";
                    UpdateShardIncome(purpleShardContainer, 0);
                    blueShardContainer = GameObject.Instantiate(topRowChild, topRow);
                    blueShardContainer.gameObject.name = "Blue Shards";
                    UpdateShardIncome(blueShardContainer, 0);
                }
            }
        }
    }

    private static CurrencyContainer GetCurrencyContainer(Transform container)
    {
        return container.GetComponent<CurrencyContainer>();
    }

    private static void UpdateShardIncome(Transform container, int income)
    {
        CurrencyContainer currencyContainer = container.GetComponent<CurrencyContainer>();
        currencyContainer.headerLabel.text = $"{container.gameObject.name} (+{income})";
    }

    private static void UpdateShardCount(Transform container, int count)
    {
        CurrencyContainer currencyContainer = container.GetComponent<CurrencyContainer>();
        currencyContainer.resourceWidget.label.text = count.ToString();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ResourceBar), nameof(ResourceBar.OnDisable))]
    private static void OnDisable(ResourceBar __instance)
    {
        if (yellowShardContainer != null)
            GameObject.Destroy(yellowShardContainer);
        if (purpleShardContainer != null)
            GameObject.Destroy(purpleShardContainer);
        if (blueShardContainer != null)
            GameObject.Destroy(blueShardContainer);
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
                        // Console.Write(resource);
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
    [HarmonyPatch(typeof(StartTurnAction), nameof(StartTurnAction.ExecuteDefault))]
    private static void StartTurnAction_ExecuteDefault(StartTurnAction __instance, GameState gameState)
    {
        // modLogger.LogInfo(__instance.PlayerId);
        foreach (TileData tileData in gameState.Map.tiles)
        {
            if (tileData.owner == __instance.PlayerId)
            {
                if (tileData.improvement != null)
                {
                    if (gameState.GameLogicData.TryGetData(tileData.improvement.type, out ImprovementData improvementData))
                    {
                        if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("dig")) && tileData.resource != null)
                        {
                            string resourceName = EnumCache<ResourceData.Type>.GetName(tileData.resource.type);
                            TileData rulingTile = gameState.Map.GetTile(tileData.rulingCityCoordinates);
                            if (rulingTile.improvement != null)
                            {
                                if (EnumCache<CityReward>.TryGetType(resourceName, out CityReward reward))
                                {
                                    rulingTile.improvement.AddReward(reward);
                                    // Console.Write("Adding reward OF TYPE " + resourceName);
                                    // Console.Write("For " + rulingTile.improvement.type);
                                    // Console.Write("For " + rulingTile.coordinates);
                                }
                            }
                        }
                    }
                }
            }
        }
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
            foreach (TileData tileData in GameManager.GameState.Map.tiles)
            {
                if (tileData.owner == playerId && tileData.improvement != null)
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
        }
        shards[playerId] = new ShardsInfo(yellowShardCount, purpleShardCount, blueShardCount);
        UpdateShardCount(yellowShardContainer, yellowShardCount);
        UpdateShardCount(purpleShardContainer, purpleShardCount);
        UpdateShardCount(blueShardContainer, blueShardCount);
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
                if (tileData.owner == playerId && tileData.improvement != null)
                {
                    if (GameManager.GameState.GameLogicData.TryGetData(tileData.improvement.type, out ImprovementData improvementData))
                    {
                        if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("dig")) && tileData.resource != null)
                        {
                            if (tileData.resource.type == EnumCache<ResourceData.Type>.GetType("yellowshard"))
                            {
                                yellowShardCount++;
                            }
                            if (tileData.resource.type == EnumCache<ResourceData.Type>.GetType("purpleshard"))
                            {
                                purpleShardCount++;
                            }
                            if (tileData.resource.type == EnumCache<ResourceData.Type>.GetType("blueshard"))
                            {
                                blueShardCount++;
                            }
                        }
                    }
                }
            }
        }
        UpdateShardIncome(yellowShardContainer, yellowShardCount);
        UpdateShardIncome(purpleShardContainer, purpleShardCount);
        UpdateShardIncome(blueShardContainer, blueShardCount);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveAction), nameof(MoveAction.ExecuteDefault))]
    private static void MoveAction_ExecuteDefault(MoveAction __instance, GameState gameState)
    {
        Console.Write("MoveAction_ExecuteDefault");
        UnitState unitState;
        PlayerState playerState;
        UnitData unitData;
        if (gameState.TryGetUnit(__instance.UnitId, out unitState) && gameState.TryGetPlayer(__instance.PlayerId, out playerState) && gameState.GameLogicData.TryGetData(unitState.type, out unitData))
        {
            TileData unitTile = gameState.Map.GetTile(unitState.coordinates);
            if (unitTile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")) && !unitState.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")))
            {
                gameState.ActionStack.Add(new EmbarkAction(__instance.PlayerId, unitTile.coordinates));
                currentlyWagoning = true;
            }
            else if (!unitTile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")) && unitState.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")) && !unitTile.HasImprovement(ImprovementData.Type.City))
            {
                gameState.ActionStack.Add(new DisembarkAction(__instance.PlayerId, unitTile.coordinates));
            }
            // if (unitTile.improvement != null && gameState.GameLogicData.TryGetData(unitTile.improvement.type, out ImprovementData data) && data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("depot")) && !unitState.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")))
            // {
            //     gameState.GameLogicData.TryGetData(EnumCache<UnitData.Type>.GetType("minecart"), out UnitData wagonData);
            //     gameState.ActionStack.Add(new EmbarkAction(__instance.PlayerId, unitTile.coordinates));
            //     currentlyWagoning = true;
            // }
            // else
            // {
            //     gameState.ActionStack.Add(new DisembarkAction(__instance.PlayerId, unitTile.coordinates));
            // }
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
    [HarmonyPatch(typeof(TileData), nameof(TileData.GetMovementCost))]
    public static void GetMovementCost(ref int __result, TileData __instance, MapData map, TileData fromTile, PathFinderSettings settings)
    {
        if (settings.unit != null)
        {
            if (settings.unit.UnitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")))
            {
                __result = 1000;
                if (__instance.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")) || __instance.HasImprovement(ImprovementData.Type.City))
                {
                    __result = 0;
                    return;
                }
                // List<TileData> neighbours = map.GetTileNeighbors(__instance.coordinates).ToArray().ToList();
                // neighbours.Add(__instance);
                // foreach (var tileData in neighbours)
                // {
                //     if (tileData.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")) || tileData.HasImprovement(ImprovementData.Type.City))
                //     {
                //         __result = 0;
                //         return;
                //     }
                // }
            }
        }
    }

    //[HarmonyPostfix]
    //[HarmonyPatch(typeof(PathFinder), nameof(PathFinder.GetMoveOptions))]
    private static void PathFinder_GetMoveOptions(ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result, GameState gameState, WorldCoordinates start, int maxCost, UnitState unit)
    {
        List<WorldCoordinates> toRemove = new();
        foreach (var cordinates in __result)
        {
            PathFinderSettings pathFinderSettings = PathFinderSettings.CreateForUnit(unit, gameState);
            TileData tile = gameState.Map.GetTile(cordinates);
            int cost = tile.GetMovementCost(gameState.Map, gameState.Map.GetTile(start), pathFinderSettings);
            if (cost == 1000)
            {
                toRemove.Add(cordinates);
            }
        }
        foreach (var item in toRemove)
        {
            __result.Remove(item);
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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClientInteraction), nameof(ClientInteraction.SelectTile))]
    public static void ClientInteraction_SelectTile(ClientInteraction __instance, Tile tile)
    {
        if (tile.IsHidden)
        {
            __instance.SelectTileInternal(tile);
            // CameraController.Instance.RevealTile(__instance.selectedTile, true, 1f, true, false, null);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.OnTileSelected))]
    private static bool InteractionBar_OnTileSelected(InteractionBar __instance, Tile tile)
    {
        Console.Write("InteractionBar_OnTileSelected");
        if (tile == null || tile.IsHidden)
        {
            __instance.SetMode(InteractionBar.Mode.None);
            __instance.SetTile(tile);
            __instance.building = null;
            __instance.unit = null;
            __instance.Refresh();
            Console.Write("false");
            return false;
        }
        Console.Write("true");
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SelectionIndicator), nameof(SelectionIndicator.OnTileSelected))]
    private static void SelectionIndicator_OnTileSelected(SelectionIndicator __instance, Tile tile)
    {
        Console.Write("SelectionIndicator_OnTileSelected");
        if (tile == null || tile.IsHidden)
        {
            __instance.cityAreaSelection.Hide();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapDataExtensions), nameof(MapDataExtensions.UpdateRoutes))]
    private static bool MapDataExtensions_UpdateRoutes(GameState gameState, Il2CppSystem.Collections.Generic.List<TileData> changedTiles)
    {
        //UpdateRoutes(gameState, changedTiles);
        //return false;
        //UpdateRoutesV2(gameState, changedTiles);
        UpdateRoutesV2(gameState, changedTiles); // FUCKING MIDJIWAN CODE ISTG I JUST CANT COMPREHEND HALF OF THIS SHIT PLEASE SOMEONE SEND AMBULANCE IM SERIOUSLY GOING SLOWLY INSANE MY MENTAL HEALTH IS DECLINING AND WITH SUCH PACE IM GONNA GET INTO ASYLUM
        return false;
        // Console.Write("MapDataExtensions_UpdateRoutes");
        // foreach (TileData tileData in gameState.Map.Tiles)
        // {
        //     if (tileData.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")))
        //     {
        //         changedTiles.Add(tileData);
        //         foreach (TileData neighbour in gameState.Map.GetTileNeighbors(tileData.coordinates))
        //         {
        //             if (neighbour.HasImprovement(ImprovementData.Type.City))
        //             {
        //                 neighbour.hasRoute = true;
        //                 changedTiles.Add(neighbour);
        //             }
        //         }
        //         tileData.hasRoute = true;
        //     }
        // }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.ExecuteDefault))]
    public static bool BuildAction_ExecuteDefault(BuildAction __instance, GameState gameState)
    {
        TileData tile = gameState.Map.GetTile(__instance.Coordinates);
        if (tile != null && gameState.GameLogicData.TryGetData(__instance.Type, out ImprovementData improvementData) && gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState))
        {
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
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void CanBuild(ref bool __result, GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement)
    {
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("rail")) && __result)
        {
            if (tile.HasEffect(EnumCache<TileData.EffectType>.GetType("railed")))
            {
                __result = false;
            }
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

    public static void UpdateRoutes(GameState gameState, Il2CppSystem.Collections.Generic.List<TileData> changedTiles)
    {
        if (gameState == null || gameState.Map == null)
            return;
        var map = gameState.Map;

        Il2CppSystem.Collections.Generic.List<TileData> list = new Il2CppSystem.Collections.Generic.List<TileData>();
        map.ResetRoutes(list);

        var empireTiles = new Il2CppSystem.Collections.Generic.List<TileData>();
        var playerRouteOpeners = new Il2CppSystem.Collections.Generic.List<TileData>();
        var routeOpeners = new Il2CppSystem.Collections.Generic.List<TileData>();

        foreach (PlayerState player in gameState.PlayerStates)
        {
            if (player == null || player.Id == 255)
                continue;
            map.GetPlayerEmpireTiles(player.Id, empireTiles);
            playerRouteOpeners.Clear();
            routeOpeners.Clear();
            Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> unlockedMovements = gameState.GameLogicData.GetUnlockedMovements(player);
            foreach (TileData tile in empireTiles)
            {
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
            for (int i = 0; i < playerRouteOpeners.Count; i++)
            {
                TileData sourceTile = playerRouteOpeners[i];
                ImprovementData improvementData2;
                if (gameState.GameLogicData.TryGetData(sourceTile.improvement.type, out improvementData2))
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

    public static bool FindPathBetweenRouters(
        TileData origin,
        TileData destination,
        Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> allowedTerrain,
        PlayerState player,
        GameState gameState,
        Il2CppSystem.Collections.Generic.List<TileData> changedTiles
    )
    {
        Console.Write("/////////////////////////////////////////////////");
        Console.Write(origin.coordinates);
        Console.Write(destination.coordinates);
        Console.Write("0");
        if (origin == null || destination == null || gameState == null)
            return false;
        Console.Write("1");
        var logicData = gameState.GameLogicData;
        if (logicData == null || origin.improvement == null || destination.improvement == null)
            return false;
        Console.Write("2");
        if (!logicData.TryGetData(origin.improvement.type, out var originImp) ||
            !logicData.TryGetData(destination.improvement.type, out var destImp))
            return false;
        Console.Write("3");
        int distance = MapDataExtensions.ChebyshevDistance(origin.coordinates, destination.coordinates);
        Console.Write("Distance: " + distance);
        Console.Write("Range: " + originImp.range);
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
        Console.Write("5");
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
        Console.Write("6");
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
        Console.Write("7");
        Console.Write("/////////////////////////////////////////////////");
        return true;
    }
}