using UnityEngine;
using System.Collections.Generic;
using System;
using MelonLoader;
using HarmonyLib;


#if MELONLOADER_IL2CPP
using ScheduleOneGame = Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Phone.Messages;
#else
using ScheduleOneGame = ScheduleOne;
using ScheduleOne.Economy;
using ScheduleOne.Quests;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.DevUtilities;
#endif

public class ChooseDealLocationOnAccept : MelonMod
{
    // stores LocationName, LocationGUID
    public static Dictionary<string, string> LocationGuids = new Dictionary<string, string>();
    // the users current location and its corresponding GUID
    private static string currentSelectedDeliveryLocation = "none";
    private static string currentSelectedGUID = "none";
    // more vars
    private static bool useRandomDeliveryLocation = true;
    private static bool selectedDeliveryLocation = false;
    // used for deferred popup
    private static Customer pendingCustomer = null;
    //UI
    private static bool showUI = false;
    private static Rect windowUIRect;
    private Vector2 scrollUIPosition;
    private GUIStyle squareWindowStyle;
    private GUIStyle squareButtonStyle;
    private GUIStyle squareLabelStyle; // don't delete this one! error without
    private GUIStyle squareVerticalScrollStyle;
    private GUIStyle squareVerticalScrollThumbStyle;
    private GUIStyle squareHorizontalScrollStyle;
    private GUIStyle squareHorizontalScrollThumbStyle;
    private Texture2D blackTex;
    private Texture2D buttonColorTex;
    private Texture2D buttonHoverColorTex;
    private static Dictionary<string, GUIContent> buttonLabels = new Dictionary<string, GUIContent>();

    public static void Print(String s) => MelonLogger.Msg(s);

    public override void OnInitializeMelon()
    {
        Print("Initialized");
    }

    public override void OnLateInitializeMelon()
    {
        // wait till the game loads, then make the LocationGuids dict
        ScheduleOneGame.Persistence.LoadManager.Instance.onLoadComplete.AddListener((UnityEngine.Events.UnityAction)MakeDeliveryLocationsDict);
        // make textures/colors
        buttonColorTex = new Texture2D(1, 1);
        buttonColorTex.SetPixel(0, 0, new Color(74f / 255f, 175f / 255f, 224f / 255f));
        buttonColorTex.wrapMode = TextureWrapMode.Repeat;
        buttonColorTex.Apply();

        buttonHoverColorTex = new Texture2D(1, 1);
        buttonHoverColorTex.SetPixel(0, 0, new Color(117f / 255f, 194f / 255f, 230f / 255f));
        buttonHoverColorTex.wrapMode = TextureWrapMode.Repeat;
        buttonHoverColorTex.Apply();

        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, new Color(190f/255f, 190f / 255f, 190f / 255f));
        blackTex.wrapMode = TextureWrapMode.Repeat;
        blackTex.Apply();
    }

    [HarmonyPatch(typeof(Customer), "PlayerAcceptedContract")]
    public static class Customer_PlayerAcceptedContract_Patch
    {
        public static bool Prefix(Customer __instance, ref EDealWindow window)
        {
            // changes the delivery location the user selected
            ContractInfo contractInfo = __instance.OfferedContractInfo;
            if (contractInfo != null && currentSelectedDeliveryLocation != "none" && useRandomDeliveryLocation == false)
            {
                contractInfo.DeliveryLocationGUID = currentSelectedGUID;
            }
            return true;
        }
    }


    [HarmonyPatch(typeof(Customer), "AcceptContractClicked")]
    public class Customer_AcceptContractClicked_Patch
    {
        public static bool Prefix(Customer __instance)
        {

            if (__instance.OfferedContractInfo == null)
            {
                MelonLogger.Warning("Offered contract is null!");
                return false;
            }

            // turns on some shading GameObjects to make it look better. original method code handled in the OnGUI() method
            Transform dealWindowSelector = GameObject.Find("Messages")?.transform.Find("Container")?.transform.Find("DealWindowSelector")?.transform;
            if (dealWindowSelector != null)
            {
                dealWindowSelector.gameObject.SetActive(true);

                Transform background = dealWindowSelector.Find("Background");
                Transform shade = dealWindowSelector.Find("Shade");
                Transform content = shade?.Find("Content");

                if (background != null) background.gameObject.SetActive(true);
                if (shade != null) shade.gameObject.SetActive(true);
                if (content != null) content.gameObject.SetActive(false);
                Print("Should've disabled!");
            }
            else
            {
                MelonLogger.Warning("Could not find DealWindowSelector hierarchy to modify.");
            }

            pendingCustomer = __instance;
            showUI = true;
            selectedDeliveryLocation = false;

            return false;
        }
    }

    private void DrawWindow(int windowID)
    {
        if (GUILayout.Button("Let the customer choose", squareButtonStyle))
        {
            showUI = false;
            selectedDeliveryLocation = true;
            useRandomDeliveryLocation = true;
        }

        scrollUIPosition = GUILayout.BeginScrollView(scrollUIPosition, GUILayout.Height(windowUIRect.height - 60));
        foreach (KeyValuePair<string, string> pair in LocationGuids)
        {
            if (!buttonLabels.ContainsKey(pair.Key))
                buttonLabels[pair.Key] = new GUIContent(pair.Key);

            if (GUILayout.Button(buttonLabels[pair.Key], squareButtonStyle))
            {
                currentSelectedDeliveryLocation = pair.Key;
                currentSelectedGUID = pair.Value;
                showUI = false;
                selectedDeliveryLocation = true;
                useRandomDeliveryLocation = false;
            }
        }
        GUILayout.EndScrollView();
    }

    public override void OnGUI()
    {
        if (squareWindowStyle == null)
            InitializeStyles();

        if (showUI)
        {
            windowUIRect = GUI.Window(0, windowUIRect, (GUI.WindowFunction)DrawWindow, "Choose where to meet the customer:", squareWindowStyle);
        }

        if (pendingCustomer != null && selectedDeliveryLocation)
        {
            HandleDeferredContractAcceptance();
        }
    }

    private void InitializeStyles()
    {
        // a big mess. probably need to make some helper methods for this stuff soon
        Texture2D whiteTex = Texture2D.whiteTexture;
        scrollUIPosition = Vector2.zero;
        windowUIRect = new Rect(837, 354, 245, 335);

        /*  steps:
            1. make the GUIStyle
            2. make the GUIStyleState
            3. modify the GUIStyleState
            4. apply the GUIStyleState to the GUIStyle
        */

        //background everything rests on
        squareWindowStyle = new GUIStyle(GUI.skin.window);

        GUIStyleState normalSqaureWindowStyleState = new GUIStyleState();
        normalSqaureWindowStyleState.background = whiteTex;
        normalSqaureWindowStyleState.textColor = Color.black;

        GUIStyleState onNormalSqaureWindowStyleState = new GUIStyleState();
        onNormalSqaureWindowStyleState.background = whiteTex;
        onNormalSqaureWindowStyleState.textColor = Color.black;

        squareWindowStyle.padding = new RectOffset(10, 10, 20, 10);
        squareWindowStyle.normal = normalSqaureWindowStyleState;
        squareWindowStyle.onNormal = onNormalSqaureWindowStyleState;

        // buttons

        squareButtonStyle = new GUIStyle(GUI.skin.button);

        GUIStyleState squareButtonNormalState = new GUIStyleState();
        squareButtonNormalState.background = buttonColorTex;
        squareButtonNormalState.textColor = Color.white;

        GUIStyleState squareButtonHoverState = new GUIStyleState();
        squareButtonHoverState.background = buttonHoverColorTex;
        squareButtonHoverState.textColor = Color.white;

        GUIStyleState squareButtonActiveState = new GUIStyleState();
        squareButtonActiveState.background = buttonHoverColorTex;
        squareButtonActiveState.textColor = Color.white;

        squareButtonStyle.normal = squareButtonNormalState;
        squareButtonStyle.hover = squareButtonHoverState;
        squareButtonStyle.active = squareButtonActiveState;
        squareButtonStyle.border = new RectOffset(0, 0, 0, 0);

        // regular text

        squareLabelStyle = new GUIStyle(GUI.skin.label);

        GUIStyleState squareLabelNormalState = new GUIStyleState();
        squareLabelNormalState.textColor = Color.black;

        squareLabelStyle.normal = squareLabelNormalState;
        squareLabelStyle.wordWrap = true;

        // vertical scrollbar 
        
        squareVerticalScrollThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);

        GUIStyleState squareScrollThumbNormalState = new GUIStyleState();
        squareScrollThumbNormalState.background = blackTex;

        squareVerticalScrollThumbStyle.normal = squareScrollThumbNormalState;
        squareVerticalScrollThumbStyle.border = new RectOffset(0, 0, 0, 0);

        // horizontal scrollbar

        squareHorizontalScrollThumbStyle = new GUIStyle(GUI.skin.horizontalScrollbarThumb);

        GUIStyleState squareHorizontalScrollThumbNormalState = new GUIStyleState();
        squareHorizontalScrollThumbNormalState.background = blackTex;

        squareHorizontalScrollThumbStyle.normal = squareHorizontalScrollThumbNormalState;
        squareHorizontalScrollThumbStyle.border = new RectOffset(0, 0, 0, 0);
        
        // Apply to GUI.skin

        GUI.skin.horizontalScrollbar = squareHorizontalScrollStyle;
        GUI.skin.horizontalScrollbarThumb = squareHorizontalScrollThumbStyle;
        GUI.skin.verticalScrollbar = squareVerticalScrollStyle;
        GUI.skin.verticalScrollbarThumb = squareVerticalScrollThumbStyle;

        
    }

    public static void MakeDeliveryLocationsDict()
    {
        GameObject deliveryLocations = null;
        try
        {
            deliveryLocations = GameObject.Find("Delivery Locations");
            Print("Got deliveryLocations");
        }
        catch (Exception exception)
        {
            Print("Could not get deliveryLocations: " + exception);
            return;
        }

        for (int i = 0; i < deliveryLocations.transform.childCount; i++)
        {
            Transform child = deliveryLocations.transform.GetChild(i);
            DeliveryLocation location = child.GetComponent<DeliveryLocation>();
            if (location != null)
            {
                string name = location.LocationName;
                string guid = location.GUID.ToString();

                if (!LocationGuids.ContainsKey(name))
                {
                    LocationGuids.Add(name, guid);
                }
                else
                {
                    MelonLogger.Warning($"Duplicate location \"{name}\" skipped building DeliveryLocation dict.");
                }
            }
        }
    }

    private void HandleDeferredContractAcceptance()
    {
        if (pendingCustomer != null && selectedDeliveryLocation)
        {
            // Reactivate Shade/Content
            Transform dealWindowSelector = GameObject.Find("Messages")?.transform.Find("Container")?.transform.Find("DealWindowSelector")?.transform;
            Transform shadeTransform = dealWindowSelector?.transform.Find("Shade")?.transform;
            Transform contentTransform = shadeTransform?.Find("Content")?.transform;
            if (shadeTransform != null && contentTransform != null)
            {
                shadeTransform.gameObject.SetActive(true);  // Reactivate Shade
                contentTransform.gameObject.SetActive(true);  // Reactivate Content
                Print("Re-enabled Shade/Content GameObjects.");
            }
            else
            {
                MelonLogger.Warning("Could not find Shade/Content GameObjects to enable.");
            }

            // Execute the deferred contract acceptance logic
            var method = AccessTools.Method(typeof(Customer), "PlayerAcceptedContract");
            if (method != null)
            {
                Action<EDealWindow> callback = (Action<EDealWindow>)Delegate.CreateDelegate(typeof(Action<EDealWindow>), pendingCustomer, method);
                PlayerSingleton<MessagesApp>.Instance.DealWindowSelector.SetIsOpen(true, pendingCustomer.NPC.MSGConversation, callback);
                Print("Executed deferred PlayerAcceptedContract logic");
            }
            else
            {
                MelonLogger.Warning("Could not find PlayerAcceptedContract method!");
            }



            // Reset state
            pendingCustomer = null;
            selectedDeliveryLocation = false;
        }
    }

}
