using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using static PolytopiaMinerskaggTribe.Main;

namespace PolytopiaMinerskaggTribe;

public static class UI
{
    internal static Transform? yellowShardContainer = null;
    internal static Transform? purpleShardContainer = null;
    internal static Transform? blueShardContainer = null;
    internal static UIRoundButton? tileRevealButton = null;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ResourceBar), nameof(ResourceBar.OnEnable))]
    internal static void OnEnable(ResourceBar __instance)
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

    internal static CurrencyContainer GetCurrencyContainer(Transform container)
    {
        return container.GetComponent<CurrencyContainer>();
    }

    internal static void UpdateShardIncome(Transform container, int income)
    {
        CurrencyContainer currencyContainer = container.GetComponent<CurrencyContainer>();
        currencyContainer.headerLabel.text = $"{container.gameObject.name} (+{income})";
    }

    internal static void UpdateShardCount(Transform container, int count)
    {
        CurrencyContainer currencyContainer = container.GetComponent<CurrencyContainer>();
        currencyContainer.resourceWidget.label.text = count.ToString();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ResourceBar), nameof(ResourceBar.OnDisable))]
    internal static void OnDisable(ResourceBar __instance)
    {
        if (yellowShardContainer != null)
            GameObject.Destroy(yellowShardContainer);
        if (purpleShardContainer != null)
            GameObject.Destroy(purpleShardContainer);
        if (blueShardContainer != null)
            GameObject.Destroy(blueShardContainer);
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClientInteraction), nameof(ClientInteraction.SelectTile))]
    public static void ClientInteraction_SelectTile(ClientInteraction __instance, Tile tile)
    {
        if (tile.IsHidden)
        {
            __instance.SelectTileInternal(tile);
            Main.chosenTileCoordinates = tile.Coordinates;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClientInteraction), nameof(ClientInteraction.DeselectTile))]
    public static void ClientInteraction_DeselectTile(ClientInteraction __instance)
    {
        Main.chosenTileCoordinates = null;
    }

    private static UIRoundButton AddUiButtonToArray(UIRoundButton prefabButton, HudScreen hudScreen, UIButtonBase.ButtonAction action, UIRoundButton[] buttonArray, string? description = null, string? name = null)
    {
        UIRoundButton button = UnityEngine.GameObject.Instantiate(prefabButton, prefabButton.transform);
        if (name != null)
            button.gameObject.name = name;
        button.transform.parent = hudScreen.buttonBar.transform;
        button.OnClicked += action;
        List<UIRoundButton> list = buttonArray.ToList();
        list.Add(button);
        list.ToArray();

        if (description != null)
        {
            Transform child = button.gameObject.transform.Find("DescriptionText");

            if (child != null)
            {
                TMPLocalizer localizer = child.gameObject.GetComponent<TMPLocalizer>();
                localizer.Text = description;
            }
        }
        return button;
    }

    private static bool IsUiButtonInArray(string name, UIRoundButton[] buttonArray)
    {
        foreach (UIRoundButton button in buttonArray)
        {
            Console.Write(button.gameObject.name);
            if (button.gameObject.name == name)
            {
                return true;
            }
        }
        return false;
    }

    private static void RemoveUiButtonFromArray(string name, UIRoundButton[] buttonArray)
    {
        int pos = -1;
        for (int i = 0; i < buttonArray.Length; i++)
        {
            UIRoundButton button = buttonArray[i];
            if (button.gameObject.name == name)
            {
                pos = i;
                break;
            }
        }
        if (pos != -1)
        {
            List<UIRoundButton> list = buttonArray.ToList();
            UIRoundButton button = list[pos];
            UnityEngine.GameObject.Destroy(button);
            list.RemoveAt(pos);
            list.ToArray();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HudButtonBar), nameof(HudButtonBar.Init))]
    private static void HudButtonBar_Init(HudButtonBar __instance, HudScreen hudScreen)
    {
        string name = "TileRevealButton";
        tileRevealButton = AddUiButtonToArray(__instance.menuButton, __instance.hudScreen, (UIButtonBase.ButtonAction)TileRevealButton_OnClicked, __instance.buttonArray, "Reveal Tile", name);
        __instance.Show();
		__instance.Update();

        void TileRevealButton_OnClicked(int id, BaseEventData eventdata)
        {
            ShardsInfo shardInfo = shards[GameManager.LocalPlayer.Id];
            if (Main.chosenTileCoordinates != null && shardInfo.purpleShardCount >= 5)
            {
                DeductShards(GameManager.LocalPlayer.Id, EnumCache<CityReward>.GetType("purpleshard"), 5);
                GameManager.GameState.ActionStack.Add(new ExploreAction(GameManager.LocalPlayer.Id, (WorldCoordinates)Main.chosenTileCoordinates));
                GameManager.Client.ActionManager.Update();
                //GameManager.GameState.CommandStack.Add(new BuildCommand(GameManager.LocalPlayer.Id, EnumCache<Polytopia.Data.ImprovementData.Type>.GetType("exploretile"), (WorldCoordinates)Main.chosenTileCoordinates));
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HudButtonBar), nameof(HudButtonBar.Update))]
    private static bool HudButtonBar_Update(HudButtonBar __instance)
    {
        if (tileRevealButton != null)
        {
            ShardsInfo shardInfo = shards[GameManager.LocalPlayer.Id];
            if (Main.chosenTileCoordinates != null && shardInfo.purpleShardCount >= 5)
            {
                tileRevealButton.buttonActive = true;
                tileRevealButton.CanRegisterHover = true;
            }
            else
            {
                tileRevealButton.buttonActive = false;
                tileRevealButton.CanRegisterHover = false;
            }
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.OnTileSelected))]
    private static bool InteractionBar_OnTileSelected(InteractionBar __instance, Tile tile)
    {
        if (tile == null || tile.IsHidden)
        {
            __instance.SetMode(InteractionBar.Mode.None);
            __instance.SetTile(tile);
            __instance.building = null;
            __instance.unit = null;
            __instance.Refresh();
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SelectionIndicator), nameof(SelectionIndicator.OnTileSelected))]
    private static void SelectionIndicator_OnTileSelected(SelectionIndicator __instance, Tile tile)
    {
        if (tile == null || tile.IsHidden)
        {
            __instance.cityAreaSelection.Hide();
        }
    }
}