using System;
using BepInEx;
using UnityEngine;
using MWCO.Client.Networking;
using MWCO.Shared;

namespace MWCO.Client
{
    /// <summary>
    /// BepInEx entry point. BepInEx will instantiate this automatically.
    /// </summary>
    [BepInPlugin("com.mwco.multiplayer", "MWCO - My Winter Car Online", "0.1.1")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        
        private bool _showUI = false;
        private string _serverAddress = "127.0.0.1";
        private string _serverPort = NetworkConfig.DefaultPort.ToString();
        private string _statusMessage = "Press F10 to open connection menu";
        private float _statusMessageTime = 0f;
        
        // Connection state tracking
        private bool _connectionPending = false;
        private float _connectionTimeout = 0f;
        
        private NetworkManager _networkManager;
        private GameObject _debugCube;
        
        private void Awake()
        {
            try
            {
                Instance = this;
                Logger.LogInfo("[MWCO] ============================================");
                Logger.LogInfo("[MWCO] MWCO Plugin.Awake() called");
                Logger.LogInfo("[MWCO] ============================================");
                Logger.LogInfo($"[MWCO] Time: {DateTime.Now}");
                Logger.LogInfo($"[MWCO] Protocol Version: {NetworkConfig.ProtocolVersion}");
                Logger.LogInfo($"[MWCO] Default Port: {NetworkConfig.DefaultPort}");
                Logger.LogInfo($"[MWCO] GameObject: {gameObject.name}");
                
                // Add NetworkManager to this GameObject
                _networkManager = gameObject.AddComponent<NetworkManager>();
                NetworkManager.Logger = Logger;
                Logger.LogInfo("[MWCO] ✓ NetworkManager component added");
                
                DontDestroyOnLoad(gameObject);
                Logger.LogInfo("[MWCO] ✓ DontDestroyOnLoad set");
                Logger.LogInfo("[MWCO] Mod initialized! Press F10 to open connection menu.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MWCO] ✗ FAILED TO INITIALIZE: {ex.GetType().Name}");
                Logger.LogError($"[MWCO] Error: {ex.Message}");
                Logger.LogError($"[MWCO] Stack: {ex.StackTrace}");
            }
        }
        
        private void Update()
        {
            // F10 toggles UI
            if (Input.GetKeyDown(KeyCode.F10))
            {
                _showUI = !_showUI;
                Logger.LogInfo($"[MWCO] F10 pressed, showUI = {_showUI}");
                
                // Debug: Log current state
                if (_networkManager != null)
                {
                    Logger.LogInfo($"[MWCO] NetworkManager exists, IsConnected={_networkManager.IsConnected}");
                    Logger.LogInfo($"[MWCO] Packets Sent={_networkManager.PacketsSent}, Recv={_networkManager.PacketsReceived}");
                }
                else
                {
                    Logger.LogError("[MWCO] NetworkManager is NULL!");
                }
            }
            
            // F9 spawns a test cube (network sync test)
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (_networkManager != null && _networkManager.IsConnected)
                {
                    _networkManager.SpawnTestCube();
                    SetStatus("Spawned test cube! (should appear on other clients)", 3f);
                }
                else
                {
                    SetStatus("Connect to server first (F10)", 3f);
                }
            }
            
            // Update status message timeout
            if (_statusMessageTime > 0)
            {
                _statusMessageTime -= Time.deltaTime;
            }
            
            // Check connection timeout
            if (_connectionPending)
            {
                _connectionTimeout -= Time.deltaTime;
                if (_connectionTimeout <= 0)
                {
                    _connectionPending = false;
                    SetStatus("Connection timed out! Is the server running?", 5f);
                    Logger.LogWarning("[MWCO] Connection attempt timed out");
                }
                else if (_networkManager != null && _networkManager.IsConnected)
                {
                    _connectionPending = false;
                    SetStatus($"Connected! Player ID: {_networkManager.LocalPlayerId}, Vehicle ID: {_networkManager.LocalVehicleId}", 5f);
                    Logger.LogInfo($"[MWCO] Connection successful! Player ID: {_networkManager.LocalPlayerId}");
                }
            }
        }
        
        private void SetStatus(string message, float duration = 3f)
        {
            _statusMessage = message;
            _statusMessageTime = duration;
            Logger.LogInfo($"[MWCO] Status: {message}");
        }
        
        private void OnGUI()
        {
            // Always draw status indicator in top-left (even if menu closed)
            string connectionStatus;
            if (_networkManager != null && _networkManager.IsConnected)
            {
                connectionStatus = $"[MWCO: Connected - Player {_networkManager.LocalPlayerId}]";
                GUI.color = Color.green;
            }
            else if (_connectionPending)
            {
                connectionStatus = "[MWCO: Connecting...]";
                GUI.color = Color.yellow;
            }
            else
            {
                connectionStatus = "[MWCO: Disconnected - Press F10]";
                GUI.color = Color.white;
            }
            
            GUI.Label(new Rect(10, 10, 350, 25), connectionStatus);
            GUI.color = Color.white;
            
            if (!_showUI)
            {
                // Draw a small indicator that the mod is loaded
                GUI.Label(new Rect(10, 35, 200, 20), "MWCO v0.1.1 Loaded");
                return;
            }
            
            // Debug: Log that we're rendering the menu
            if (Time.frameCount % 60 == 0)
            {
                Logger.LogInfo("[MWCO] OnGUI rendering menu (called every 60 frames)");
            }
            
            // Center window
            float windowWidth = 400;
            float windowHeight = 350;
            float x = (Screen.width - windowWidth) / 2;
            float y = (Screen.height - windowHeight) / 2;
            
            // Background box
            GUI.Box(new Rect(x, y, windowWidth, windowHeight), "MWCO - My Winter Car Online v0.1.1");
            
            float contentX = x + 20;
            float currentY = y + 40;
            float contentWidth = windowWidth - 40;
            
            bool isConnected = _networkManager != null && _networkManager.IsConnected;
            
            if (!isConnected)
            {
                GUI.Label(new Rect(contentX, currentY, contentWidth, 25), "Server Address:");
                currentY += 25;
                _serverAddress = GUI.TextField(new Rect(contentX, currentY, contentWidth, 25), _serverAddress);
                currentY += 35;
                
                GUI.Label(new Rect(contentX, currentY, contentWidth, 25), "Port:");
                currentY += 25;
                _serverPort = GUI.TextField(new Rect(contentX, currentY, contentWidth, 25), _serverPort);
                currentY += 45;
                
                // Debug info
                if (_networkManager != null)
                {
                    GUI.Label(new Rect(contentX, currentY, contentWidth, 25), $"Packets: Sent={_networkManager.PacketsSent} Recv={_networkManager.PacketsReceived}");
                    currentY += 25;
                    if (!string.IsNullOrEmpty(_networkManager.LastError))
                    {
                        GUI.color = Color.red;
                        GUI.Label(new Rect(contentX, currentY, contentWidth, 25), $"Error: {_networkManager.LastError}");
                        GUI.color = Color.white;
                        currentY += 25;
                    }
                }
                currentY += 10;
                
                // Buttons side by side
                float buttonWidth = (contentWidth - 10) / 2;
                
                GUI.enabled = !_connectionPending;
                bool connectClicked = GUI.Button(new Rect(contentX, currentY, buttonWidth, 40), _connectionPending ? "Connecting..." : "Connect");
                GUI.enabled = true;
                
                bool closeClicked = GUI.Button(new Rect(contentX + buttonWidth + 10, currentY, buttonWidth, 40), "Close");
                
                currentY += 50;
                
                // Process button clicks AFTER all GUI rendering
                if (connectClicked && !_connectionPending)
                {
                    Logger.LogInfo("[MWCO] Connect button clicked!");
                    try
                    {
                        Logger.LogInfo("[MWCO] About to call Connect()...");
                        Connect();
                        Logger.LogInfo("[MWCO] Connect() returned successfully");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[MWCO] EXCEPTION in Connect(): {ex.GetType().Name}: {ex.Message}");
                        Logger.LogError($"[MWCO] Stack trace: {ex.StackTrace}");
                    }
                }
                if (closeClicked)
                {
                    Logger.LogInfo("[MWCO] Close button clicked!");
                    _showUI = false;
                }
            }
            else
            {
                GUI.Label(new Rect(contentX, currentY, contentWidth, 25), $"Player ID: {_networkManager.LocalPlayerId}");
                currentY += 25;
                GUI.Label(new Rect(contentX, currentY, contentWidth, 25), $"Vehicle ID: {_networkManager.LocalVehicleId}");
                currentY += 25;
                GUI.Label(new Rect(contentX, currentY, contentWidth, 25), $"Server Tick: {_networkManager.ServerTick}");
                currentY += 25;
                GUI.Label(new Rect(contentX, currentY, contentWidth, 25), $"Packets: Sent={_networkManager.PacketsSent} Recv={_networkManager.PacketsReceived}");
                currentY += 35;
                
                if (!string.IsNullOrEmpty(_networkManager.LastError))
                {
                    GUI.Label(new Rect(contentX, currentY, contentWidth, 25), $"Error: {_networkManager.LastError}");
                    currentY += 30;
                }
                
                bool disconnectClicked = GUI.Button(new Rect(contentX, currentY, contentWidth, 40), "Disconnect");
                currentY += 50;
                
                if (disconnectClicked)
                {
                    Logger.LogInfo("[MWCO] Disconnect button clicked!");
                    Disconnect();
                }
                
                // Debug cube button
                string cubeButtonText = _debugCube == null ? "Spawn Debug Cube (10m ahead)" : "Remove Debug Cube";
                bool cubeClicked = GUI.Button(new Rect(contentX, currentY, contentWidth, 35), cubeButtonText);
                currentY += 45;
                
                if (cubeClicked)
                {
                    if (_debugCube == null)
                    {
                        SpawnDebugCube();
                    }
                    else
                    {
                        Logger.LogInfo("[MWCO] Removing debug cube");
                        Destroy(_debugCube);
                        _debugCube = null;
                    }
                }
            }
            
            if (_statusMessageTime > 0)
            {
                GUI.Label(new Rect(contentX, currentY, contentWidth, 25), _statusMessage);
            }
        }
        
        private void Connect()
        {
            Logger.LogInfo("[MWCO] Connect() method called!");
            Logger.LogInfo($"[MWCO] _networkManager is null? {_networkManager == null}");
            Logger.LogInfo($"[MWCO] _serverAddress = '{_serverAddress}'");
            Logger.LogInfo($"[MWCO] _serverPort = '{_serverPort}'");
            
            if (_networkManager == null)
            {
                SetStatus("ERROR: NetworkManager not found!", 5f);
                Logger.LogError("[MWCO] NetworkManager is null - cannot connect");
                return;
            }
            
            Logger.LogInfo($"[MWCO] IsConnected = {_networkManager.IsConnected}");
            
            if (_networkManager.IsConnected)
            {
                SetStatus("Already connected!", 3f);
                Logger.LogInfo("[MWCO] Already connected, ignoring");
                return;
            }
            
            // Parse port
            Logger.LogInfo($"[MWCO] Parsing port: '{_serverPort}'");
            if (!int.TryParse(_serverPort, out int port) || port <= 0 || port > 65535)
            {
                SetStatus("Invalid port number! Must be 1-65535", 3f);
                Logger.LogWarning($"[MWCO] Invalid port: {_serverPort}");
                return;
            }
            Logger.LogInfo($"[MWCO] Parsed port = {port}");
            
            // Validate IP
            if (string.IsNullOrEmpty(_serverAddress) || _serverAddress.Trim().Length == 0)
            {
                SetStatus("Server address cannot be empty!", 3f);
                Logger.LogWarning("[MWCO] Server address is empty");
                return;
            }
            Logger.LogInfo("[MWCO] IP validated");
            
            try
            {
                Logger.LogInfo($"[MWCO] Attempting connection to {_serverAddress}:{port}");
                SetStatus($"Connecting to {_serverAddress}:{port}...", 10f);
                
                _connectionPending = true;
                _connectionTimeout = NetworkConfig.ConnectionTimeoutSeconds;
                
                Logger.LogInfo("[MWCO] Calling _networkManager.Connect()...");
                _networkManager.Connect(_serverAddress, port);
                Logger.LogInfo("[MWCO] _networkManager.Connect() returned");
                
                Logger.LogInfo("[MWCO] Connection request sent - waiting for server response");
            }
            catch (Exception ex)
            {
                _connectionPending = false;
                SetStatus($"Connection error: {ex.Message}", 5f);
                Logger.LogError($"[MWCO] Connection failed: {ex.Message}");
                Logger.LogError($"[MWCO] Stack trace: {ex.StackTrace}");
            }
        }
        
        private void Disconnect()
        {
            if (_networkManager == null) return;
            
            try
            {
                Logger.LogInfo("[MWCO] Disconnecting from server...");
                _networkManager.Disconnect();
                _connectionPending = false;
                SetStatus("Disconnected from server", 3f);
            }
            catch (Exception ex)
            {
                SetStatus($"Disconnect error: {ex.Message}", 5f);
                Logger.LogError($"[MWCO] Disconnect error: {ex.Message}");
            }
        }
        
        private void SpawnDebugCube()
        {
            try
            {
                Logger.LogInfo("[MWCO] SpawnDebugCube called");
                
                // MASSIVE cube at world origin - impossible to miss
                Vector3 cubePos = new Vector3(500f, 250f, 0f); // 500m to the side, 250m up
                
                Logger.LogInfo($"[MWCO] Creating MASSIVE cube at {cubePos}");
                _debugCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _debugCube.name = "MWCO_DebugCube_MASSIVE";
                _debugCube.transform.position = cubePos;
                _debugCube.transform.localScale = new Vector3(500f, 500f, 500f); // 500 METERS!
                
                // Remove collider
                var collider = _debugCube.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                    Logger.LogInfo("[MWCO] Removed collider");
                }
                
                // Set color to bright magenta
                var renderer = _debugCube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // Use a simple unlit shader for visibility
                    var shader = Shader.Find("Unlit/Color");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader != null)
                    {
                        renderer.material = new Material(shader);
                        renderer.material.color = new Color(1f, 0f, 1f, 1f); // Magenta
                        Logger.LogInfo("[MWCO] Set material color to magenta");
                    }
                    else
                    {
                        Logger.LogWarning("[MWCO] Could not find shader!");
                    }
                }
                
                // Also log all found shaders for debugging
                Logger.LogInfo("[MWCO] Attempting to list available shaders...");
                
                Logger.LogInfo($"[MWCO] MASSIVE debug cube spawned at {cubePos} with scale 500m");
                Logger.LogInfo($"[MWCO] Cube GameObject exists: {_debugCube != null}");
                Logger.LogInfo($"[MWCO] Cube active: {_debugCube.activeSelf}");
                Logger.LogInfo($"[MWCO] Cube position: {_debugCube.transform.position}");
                Logger.LogInfo($"[MWCO] Cube scale: {_debugCube.transform.localScale}");
                SetStatus($"MASSIVE Cube at {cubePos} (500m)", 10f);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MWCO] SpawnDebugCube failed: {ex.GetType().Name}: {ex.Message}");
                Logger.LogError($"[MWCO] Stack: {ex.StackTrace}");
                SetStatus($"Cube spawn failed: {ex.Message}", 5f);
            }
        }
        
        private void OnDestroy()
        {
            Logger.LogInfo("[MWCO] Plugin.OnDestroy() - cleaning up");
            if (_debugCube != null) Destroy(_debugCube);
            Disconnect();
        }
    }
}
