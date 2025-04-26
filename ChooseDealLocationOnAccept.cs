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
    private static Rect windowUIRect = new Rect(837, 354, 245, 335);
    private Vector2 scrollUIPosition = Vector2.zero;
    private GUIStyle squareWindowStyle;
    private GUIStyle squareButtonStyle;
    private GUIStyle squareLabelStyle; // don't delete this one! error without
    private GUIStyle squareScrollStyle;
    private GUIStyle squareScrollThumbStyle;
    Texture2D buttonColorTex;
    Texture2D buttonHoverColorTex;
    private static Dictionary<string, GUIContent> buttonLabels = new Dictionary<string, GUIContent>();

    public static void Print(String s) => MelonLogger.Msg(s);

    public static String GetGuidFromDict(String locationName)
    {
        if (LocationGuids.TryGetValue(locationName, out string locationGuid))
        {
            return locationGuid;
        }
        Print("No GUID found for location: " + locationName + ". Using Next to Bud's bar GUID instead.");
        return "7549f5e4-3702-4890-aabf-a9a170cdf15b";
    }

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
            windowUIRect = GUI.Window(0, windowUIRect, (GUI.WindowFunction)DrawWindow, "Choose Deal Locations", squareWindowStyle);
        }

        if (pendingCustomer != null && selectedDeliveryLocation)
        {
            HandleDeferredContractAcceptance();
        }
    }

    private void InitializeStyles()
    {
        Texture2D whiteTex = Texture2D.whiteTexture;
        Texture2D blackTex = Texture2D.blackTexture;
        squareWindowStyle = new GUIStyle(GUI.skin.window)
        {
            normal = { background = whiteTex, textColor = Color.black },
            padding = new RectOffset(10, 10, 20, 10)
        };

        squareButtonStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { background = buttonColorTex, textColor = Color.white },
            hover = { background = buttonHoverColorTex, textColor = Color.white },
            border = new RectOffset(0, 0, 0, 0),
        };

        squareLabelStyle = new GUIStyle(GUI.skin.label)
        {
            normal = { textColor = Color.black },
            wordWrap = true
        };

        squareScrollStyle = new GUIStyle(GUI.skin.verticalScrollbar)
        {
            normal = { background = whiteTex },
            border = new RectOffset(0, 0, 0, 0),
            fixedWidth = 10
        };

        squareScrollThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb)
        {
            normal = { background = whiteTex },
            border = new RectOffset(0, 0, 0, 0)
        };

        GUI.skin.verticalScrollbar = squareScrollStyle;
        GUI.skin.verticalScrollbarThumb = squareScrollThumbStyle;
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
