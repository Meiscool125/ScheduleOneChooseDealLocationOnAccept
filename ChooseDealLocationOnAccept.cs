using MelonLoader;
using HarmonyLib;
using UnityEngine;
using ScheduleOne.Economy;
using ScheduleOne.Quests;
using System;
using System.Collections.Generic;

public class ChooseDealLocationOnAccept : MelonMod
{
    //variables
    public static Dictionary<string, string> LocationGuids = new Dictionary<string, string>();
    private static string currentSelectedGUID = "none";
    private static string currentSelectedDeliveryLocation = "none";

    private static bool showUI = false;
    private static Rect windowUIRect = new Rect(100, 100, 350, 250); // Start position/size
    private static string currentUIMode = "Random";
    private Vector2 scrollUIPosition = Vector2.zero;
    

    //helper methods
    public static void Print(String s)
    {
        MelonLogger.Msg(s);
    }

    public static String GetGuidFromDict(String locationName)
    {

        if (LocationGuids.TryGetValue(locationName, out string locationGuid))
        {
            return locationGuid;
        }
        else
        {
            Print("No GUID found for location: " + locationName + " in LocationGuids. Using GUID for Next to Bud's Bar instead.");
            return "7549f5e4-3702-4890-aabf-a9a170cdf15b";
        }

    }

    //melon methods
    public override void OnInitializeMelon()
    {
        Print("Initialized");
        MelonLogger.Warning("Test warning");
    }

    public override void OnLateInitializeMelon()
    {
        // OnLateInitializeMelon waits for unity to be loaded, sending this line before unity loads will throw an exception
        ScheduleOne.Persistence.LoadManager.Instance.onLoadComplete.AddListener(ChooseDealLocationOnAccept.MakeDeliveryLocationsDict);
    }
    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            showUI = !showUI;           
        }
    }

    //UI
    private void DrawWindow(int windowID)
    {
        
        if (GUILayout.Button(("Current mode: " + currentUIMode)))
        {
            if(currentUIMode == "Choose")
            {
                currentUIMode = "Random";
            }
            else
            {
                currentUIMode = "Choose";
            }
        }

        if (currentUIMode == "Choose")
        {
            GUILayout.Label($"Current selected location: {currentSelectedDeliveryLocation}");
            scrollUIPosition = GUILayout.BeginScrollView(scrollUIPosition, GUILayout.Height(windowUIRect.height-20));
            foreach (KeyValuePair<string, string> pair in LocationGuids)
            {
                if (GUILayout.Button(pair.Key))
                {
                    currentSelectedDeliveryLocation = pair.Key;
                    currentSelectedGUID = pair.Value;
                    Print($"Clicked on location: {currentSelectedDeliveryLocation} with GUID {currentSelectedGUID}");
                }
            }
            GUILayout.EndScrollView();
        }

        GUI.DragWindow();
    }
    public override void OnGUI()
    {
        if (showUI)
        {
            windowUIRect = GUI.Window(0, windowUIRect, DrawWindow, "Choose Deal Locations");
        }
    }

    // Thanks to overweightunicorn to writing the original version of this Customer_PlayerAcceptedContract_Patch method.
    // I changed it quite a bit but their original version helped me a lot 
    [HarmonyPatch(typeof(Customer), "PlayerAcceptedContract")]
    public static class Customer_PlayerAcceptedContract_Patch
    {
        public static bool Prefix(Customer __instance, ref EDealWindow window)
        {
            ContractInfo contractInfo = __instance.OfferedContractInfo;
            if (contractInfo != null && currentSelectedDeliveryLocation != "none" && currentUIMode == "Choose")
            {
                contractInfo.DeliveryLocationGUID = currentSelectedGUID;
            }
            return true;
        }
    }

    // other methods
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
}

