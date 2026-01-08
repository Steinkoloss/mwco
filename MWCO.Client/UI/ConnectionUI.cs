using UnityEngine;
using MWCO.Client.Networking;

namespace MWCO.Client.UI
{
    /// <summary>
    /// In-game UI for connecting to MWCO servers
    /// Press F10 to toggle the connection menu
    /// </summary>
    public class ConnectionUI : MonoBehaviour
    {
        private bool showConnectionMenu = false;
        private string serverAddress = "127.0.0.1:1999";
        private string statusMessage = "";
        private Rect windowRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 100, 400, 200);

        private NetworkManager networkManager;

        private void Start()
        {
            networkManager = GetComponent<NetworkManager>();
            Debug.Log("[MWCO] ConnectionUI initialized");
        }

        private void Update()
        {
            // Toggle connection menu with F10
            if (Input.GetKeyDown(KeyCode.F10))
            {
                showConnectionMenu = !showConnectionMenu;
                Debug.Log($"[MWCO] Connection menu {(showConnectionMenu ? "opened" : "closed")}");
            }
        }

        private void OnGUI()
        {
            if (!showConnectionMenu) return;

            // Draw connection window
            windowRect = GUI.Window(12345, windowRect, DrawConnectionWindow, "MWCO Connection");
        }

        private void DrawConnectionWindow(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("My Winter Car Online", GUI.skin.GetStyle("label"));
            GUILayout.Label($"Version {MWCOMod.ModVersion}", GUI.skin.GetStyle("label"));
            GUILayout.Space(10);

            // Connection status
            if (networkManager != null && networkManager.IsConnected)
            {
                GUILayout.Label("Status: Connected", GUI.skin.GetStyle("label"));
                GUILayout.Label($"Player ID: {networkManager.LocalPlayerId}", GUI.skin.GetStyle("label"));
                GUILayout.Label($"Vehicle ID: {networkManager.LocalVehicleId}", GUI.skin.GetStyle("label"));
                GUILayout.Space(5);
                GUILayout.Label($"Packets Sent: {networkManager.PacketsSent}", GUI.skin.GetStyle("label"));
                GUILayout.Label($"Packets Received: {networkManager.PacketsReceived}", GUI.skin.GetStyle("label"));
                
                if (GUILayout.Button("Disconnect"))
                {
                    networkManager.Disconnect();
                    statusMessage = "Disconnected from server";
                }
            }
            else
            {
                GUILayout.Label("Status: Disconnected", GUI.skin.GetStyle("label"));

                if (networkManager != null)
                {
                    GUILayout.Label($"Packets Sent: {networkManager.PacketsSent}", GUI.skin.GetStyle("label"));
                    GUILayout.Label($"Packets Received: {networkManager.PacketsReceived}", GUI.skin.GetStyle("label"));
                    
                    if (!string.IsNullOrEmpty(networkManager.LastError))
                    {
                        GUILayout.Label($"Last Error: {networkManager.LastError}", GUI.skin.GetStyle("label"));
                    }
                }

                GUILayout.Space(5);
                GUILayout.Label("Server Address:", GUI.skin.GetStyle("label"));
                serverAddress = GUILayout.TextField(serverAddress, 100);

                if (GUILayout.Button("Connect"))
                {
                    ConnectToServer();
                }
            }

            GUILayout.Space(10);

            // Status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Label(statusMessage, GUI.skin.GetStyle("label"));
            }

            GUILayout.EndVertical();

            // Make window draggable
            GUI.DragWindow();
        }

        private void ConnectToServer()
        {
            try
            {
                string[] parts = serverAddress.Split(':');
                string ip = parts[0];
                int port = parts.Length > 1 ? int.Parse(parts[1]) : 1999;

                Debug.Log($"[MWCO] Attempting to connect to {ip}:{port}");
                networkManager.Connect(ip, port);
                statusMessage = $"Connecting to {ip}:{port}...";
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MWCO] Failed to parse server address: {ex.Message}");
                statusMessage = "Invalid server address format (use IP:PORT)";
            }
        }
    }
}
