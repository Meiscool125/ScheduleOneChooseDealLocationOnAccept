using MelonLoader;
using HarmonyLib;
using UnityEngine;
using ScheduleOne.Economy;
using ScheduleOne.Quests;
using System;
using System.Collections.Generic;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.DevUtilities;

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

    public static void Print(String s) => MelonLogger.Msg(s);

    public static String GetGuidFromDict(String locationName)
    {
        if (LocationGuids.TryGetValue(locationName, out string locationGuid))
            return locationGuid;
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
        ScheduleOne.Persistence.LoadManager.Instance.onLoadComplete.AddListener(MakeDeliveryLocationsDict);
    }
    //finds me some GameObjects so I can active/deactive them
    private static Transform FindObjectEndingWith(string pathEnd)
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform match = FindTransformEndingWith(root.transform, pathEnd);
            if (match != null)
                return match;
        }
        return null;
    }

    private static Transform FindTransformEndingWith(Transform current, string pathEnd)
    {
        string fullPath = current.name;
        Transform temp = current.parent;
        while (temp != null)
        {
            fullPath = temp.name + "/" + fullPath;
            temp = temp.parent;
        }

        if (fullPath.EndsWith(pathEnd))
            return current;

        foreach (Transform child in current)
        {
            Transform found = FindTransformEndingWith(child, pathEnd);
            if (found != null)
                return found;
        }

        return null;
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
            // turns on some shading GameObjects to make it look better. original method code handled in the OnGUI() method
            if (__instance.OfferedContractInfo == null)
            {
                MelonLogger.Warning("Offered contract is null!");
                return false;
            }

            string basePath = "/Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/AppsCanvas/Messages/Container/DealWindowSelector";

            Transform dealWindowSelector = FindObjectEndingWith(basePath);
            if (dealWindowSelector != null)
            {
                dealWindowSelector.gameObject.SetActive(true);

                Transform background = dealWindowSelector.Find("Background");
                Transform shade = dealWindowSelector.Find("Shade");
                Transform content = shade?.Find("Content");

                if (background != null) background.gameObject.SetActive(true);
                if (shade != null) shade.gameObject.SetActive(true);
                if (content != null) content.gameObject.SetActive(false);
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
            if (GUILayout.Button(pair.Key, squareButtonStyle))
            {
                currentSelectedDeliveryLocation = pair.Key;
                currentSelectedGUID = pair.Value;
                Print($"Clicked on location: {currentSelectedDeliveryLocation} with GUID {currentSelectedGUID}");
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
            windowUIRect = GUI.Window(0, windowUIRect, DrawWindow, "Choose Deal Locations", squareWindowStyle);
        }

        HandleDeferredContractAcceptance();
    }

    private void InitializeStyles()
    {
        Texture2D whiteTex = Texture2D.whiteTexture;
        Texture2D blackTex = Texture2D.blackTexture;
        Texture2D buttonColorTex = new Texture2D(1, 1);
        buttonColorTex.SetPixel(0, 0, new Color(74f / 255f, 175f / 255f, 224f / 255f));
        buttonColorTex.wrapMode = TextureWrapMode.Repeat;
        buttonColorTex.Apply();

        squareWindowStyle = new GUIStyle(GUI.skin.window)
        {
            normal = { background = buttonColorTex, textColor = Color.black },
            padding = new RectOffset(10, 10, 20, 10)
        };

        squareButtonStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { background = buttonColorTex, textColor = Color.black },
            border = new RectOffset(0, 0, 0, 0),
        };

        squareLabelStyle = new GUIStyle(GUI.skin.label)
        {
            normal = { textColor = Color.black },
            wordWrap = true
        };

        squareScrollStyle = new GUIStyle(GUI.skin.verticalScrollbar)
        {
            normal = { background = buttonColorTex },
            border = new RectOffset(0, 0, 0, 0),
            fixedWidth = 10
        };

        squareScrollThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb)
        {
            normal = { background = buttonColorTex },
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

        foreach (Transform child in deliveryLocations.transform)
        {
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
            Transform shadeTransform = FindObjectEndingWith("/Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/AppsCanvas/Messages/Container/DealWindowSelector/Shade");
            Transform contentTransform = FindObjectEndingWith("/Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/AppsCanvas/Messages/Container/DealWindowSelector/Shade/Content");
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
