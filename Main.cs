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
    private static Transform? blueShardContainer = null;
    public static void Load(ManualLogSource logger)
    {
        PolyMod.Loader.AddPatchDataType("cityReward", typeof(CityReward));
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
        UnitState unitState;
        PlayerState playerState;
        UnitData unitData;
        if (gameState.TryGetUnit(__instance.UnitId, out unitState) && gameState.TryGetPlayer(__instance.PlayerId, out playerState) && gameState.GameLogicData.TryGetData(unitState.type, out unitData))
        {
            WorldCoordinates worldCoordinates = __instance.Path[0];
            WorldCoordinates worldCoordinates2 = __instance.Path[__instance.Path.Count - 1];
            TileData tile2 = gameState.Map.GetTile(worldCoordinates2);
            if (!unitState.HasAbility(UnitAbility.Type.Fly, gameState) && tile2.improvement != null)
            {
                if (gameState.GameLogicData.TryGetData(tile2.improvement.type, out ImprovementData improvementData))
                {
                    if (improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("depot")))
                    {
                        UnitData.Type type = EnumCache<UnitData.Type>.GetType("minecart");
                        UnitData wagonData;
                        gameState.GameLogicData.TryGetData(type, out wagonData);
                        UnitState newUnitState = ActionUtils.TrainUnit(gameState, playerState, tile2, wagonData);
                        if (!unitState.HasAbility(UnitAbility.Type.Protect, gameState))
                        {
                            newUnitState.health = unitState.health;
                        }
                        newUnitState.home = unitState.home;
                        newUnitState.direction = unitState.direction;
                        newUnitState.flipped = unitState.flipped;
                        newUnitState.passengerUnit = unitState;
                        newUnitState.effects = unitState.effects;
                        newUnitState.attacked = true;
                        newUnitState.moved = true;
                    }
                }
            }
            else if (unitData.IsVehicle() && unitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("wagon")))
            {
                UnitState unit = tile2.unit;
                UnitState passengerUnit = unit.passengerUnit;
                if (passengerUnit != null)
                {
                    if (!unit.HasAbility(UnitAbility.Type.Protect, gameState))
                    {
                        passengerUnit.health = unit.health;
                    }
                    passengerUnit.flipped = unit.flipped;
                    passengerUnit.direction = unit.direction;
                    passengerUnit.moved = true;
                    passengerUnit.attacked = true;
                    tile2.SetUnit(passengerUnit);
                    passengerUnit.coordinates = tile2.coordinates;
                    UnitData newUnitData;
                    if (gameState.GameLogicData.TryGetData(passengerUnit.type, out newUnitData) && passengerUnit.HasAbility(UnitAbility.Type.Grow, gameState))
                    {
                        Il2CppSystem.Collections.Generic.List<UnitData> unlockedUpgradesForUnit = gameState.GameLogicData.GetUnlockedUpgradesForUnit(playerState, gameState, newUnitData);
                        if (unlockedUpgradesForUnit.Count > 0 && tile2.unit.GetAge(gameState) >= 3U)
                        {
                            gameState.ActionStack.Add(new UpgradeAction(__instance.PlayerId, unlockedUpgradesForUnit[0].type, tile2.coordinates, 0));
                        }
                    }
                }
            }
        }
    }
}