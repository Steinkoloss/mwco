using System;
using HarmonyLib;
using UnityEngine;
using MWCO.Client.Networking;

namespace MWCO.Client
{
    /// <summary>
    /// Main entry point for MWCO mod
    /// This gets called when the DLL is injected
    /// </summary>
    public class MWCOMod
    {
        public static string ModVersion = "0.1.1";
        public static string HarmonyId = "com.mwco.multiplayer";

        private static GameObject networkManagerObject;
        private static Harmony harmony;
        private static bool initialized = false;

        /// <summary>
        /// Called when mod is loaded
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
            {
                Debug.Log("[MWCO] Already initialized, skipping");
                return;
            }
            initialized = true;

            try
            {
                Debug.Log($"[MWCO] Initializing My Winter Car Online v{ModVersion}");

                // Apply Harmony patches
                harmony = new Harmony(HarmonyId);
                harmony.PatchAll();
                Debug.Log("[MWCO] Harmony patches applied successfully");

                // Create NetworkManager GameObject
                networkManagerObject = new GameObject("MWCO_NetworkManager");
                Debug.Log("[MWCO] GameObject created");
                
                networkManagerObject.AddComponent<NetworkManager>();
                Debug.Log("[MWCO] NetworkManager component added");
                
                networkManagerObject.AddComponent<UI.ConnectionUI>();
                Debug.Log("[MWCO] ConnectionUI component added");
                
                UnityEngine.Object.DontDestroyOnLoad(networkManagerObject);

                Debug.Log("[MWCO] NetworkManager created");
                Debug.Log("[MWCO] MWCO initialized successfully!");
                Debug.Log("[MWCO] Press F10 to open connection menu");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MWCO] Failed to initialize: {ex.Message}");
                Debug.LogError($"[MWCO] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Called when mod is unloaded
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                Debug.Log("[MWCO] Shutting down...");

                // Unpatch everything
                if (harmony != null)
                {
                    harmony.UnpatchAll(HarmonyId);
                    Debug.Log("[MWCO] Harmony patches removed");
                }

                // Destroy network manager
                if (networkManagerObject != null)
                {
                    UnityEngine.Object.Destroy(networkManagerObject);
                    Debug.Log("[MWCO] NetworkManager destroyed");
                }

                Debug.Log("[MWCO] Shutdown complete");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MWCO] Error during shutdown: {ex.Message}");
            }
        }
    }
}
