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
    private Texture2D blackTex;
    private Texture2D buttonColorTex;
    private Texture2D buttonHoverColorTex;
    private Texture2D scrollBarTex;
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
        blackTex.SetPixel(0, 0, new Color(190f / 255f, 190f / 255f, 190f / 255f));
        blackTex.wrapMode = TextureWrapMode.Repeat;
        blackTex.Apply();

        scrollBarTex = new Texture2D(1, 1);
        scrollBarTex.SetPixel(0, 0, new Color(190f / 255f, 190f / 255f, 190f / 255f));
        scrollBarTex.wrapMode = TextureWrapMode.Repeat;
        scrollBarTex.Apply();
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

    private void DrawUI()
    {
        float buttonWidth = windowUIRect.width - 20;

        if (GUILayout.Button("Let the customer choose", squareButtonStyle, GUILayout.Width(buttonWidth)))
        {
            showUI = false;
            selectedDeliveryLocation = true;
            useRandomDeliveryLocation = true;
        }

        scrollUIPosition = GUILayout.BeginScrollView(
            scrollUIPosition,
            GUIStyle.none,
            squareVerticalScrollStyle,
            GUILayout.Height(windowUIRect.height - 80)
        );

        foreach (KeyValuePair<string, string> pair in LocationGuids)
        {
            if (!buttonLabels.ContainsKey(pair.Key))
                buttonLabels[pair.Key] = new GUIContent(pair.Key);

            if (GUILayout.Button(buttonLabels[pair.Key], squareButtonStyle, GUILayout.Width(buttonWidth)))
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
            GUILayout.BeginArea(windowUIRect, squareWindowStyle);

            GUILayout.Label("Choose where to meet the customer:", squareWindowStyle);

            DrawUI();

            GUILayout.EndArea();
        }

        if (pendingCustomer != null && selectedDeliveryLocation)
        {
            HandleDeferredContractAcceptance();
        }
    }

    private void InitializeStyles()
    {
        Texture2D whiteTex = Texture2D.whiteTexture;
        Texture2D blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f));
        blackTex.wrapMode = TextureWrapMode.Repeat;
        blackTex.Apply();

        scrollUIPosition = Vector2.zero;
        windowUIRect = new Rect(837, 354, 245, 335);

        squareWindowStyle = new GUIStyle()
        {
            padding = new RectOffset(10, 10, 15, 10),
            normal = new GUIStyleState
            {
                background = whiteTex,
                textColor = new Color(0.1f, 0.1f, 0.1f),
            },
            onNormal = new GUIStyleState
            {
                background = whiteTex,
                textColor = new Color(0.1f, 0.1f, 0.1f),
            },
            fontSize = 12,
            fontStyle = FontStyle.Bold,
        };

        squareButtonStyle = new GUIStyle()
        {
            normal = new GUIStyleState
            {
                background = buttonColorTex,
                textColor = Color.white,
            },
            hover = new GUIStyleState
            {
                background = buttonHoverColorTex,
                textColor = Color.white,
            },
            active = new GUIStyleState
            {
                background = buttonHoverColorTex,
                textColor = Color.white,
            },
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(8, 8, 6, 6),
            margin = new RectOffset(3, 3, 3, 3),
            wordWrap = true,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
        };

        squareLabelStyle = new GUIStyle()
        {
            normal = new GUIStyleState
            {
                textColor = new Color(0.2f, 0.2f, 0.2f),
            },
            wordWrap = true,
            fontSize = 12,
            padding = new RectOffset(10, 10, 3, 3),
        };

        squareVerticalScrollStyle = new GUIStyle(GUI.skin.verticalScrollbar)
        {
            normal = new GUIStyleState
            {
                background = whiteTex,
            },
            border = new RectOffset(1, 1, 1, 1),
            fixedWidth = 10,
        };

        squareVerticalScrollThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb)
        {
            normal = new GUIStyleState
            {
                background = scrollBarTex,
            },
            onNormal = new GUIStyleState
            {
                background = scrollBarTex,
            },
            onFocused = new GUIStyleState
            {
                background = scrollBarTex,
            },
            onActive = new GUIStyleState
            {
                background = scrollBarTex,
            },
            onHover = new GUIStyleState
            {
                background = scrollBarTex,
            },
            hover = new GUIStyleState
            {
                background = scrollBarTex,
            },
            focused = new GUIStyleState
            {
                background = scrollBarTex,
            },
            active = new GUIStyleState
            {
                background = scrollBarTex,
            },
            fixedWidth = 10,
            fixedHeight = 16,
        };

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
