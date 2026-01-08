using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEngine;
using MWCO.Shared;
using MWCO.Shared.Packets;
using BepInEx.Logging;

namespace MWCO.Client.Networking
{
    /// <summary>
    /// Main network manager - handles connection and packet routing
    /// Runs as a MonoBehaviour on a persistent GameObject
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static ManualLogSource Logger { get; set; }
        public static NetworkManager Instance { get; private set; }

        // Connection state
        public bool IsConnected { get; private set; }
        public ushort LocalPlayerId { get; private set; }
        public ushort LocalVehicleId { get; private set; }
        public uint CurrentTick => currentTick;
        public uint ServerTick { get; private set; }
        public int PacketsReceived { get; private set; }
        public int PacketsSent { get; private set; }
        public string LastError { get; private set; } = "";

        // Networking
        private UdpClient udpClient;
        private IPEndPoint serverEndPoint;
        private uint currentTick;

        // Vehicle tracking
        private Dictionary<ushort, RemoteVehicle> remoteVehicles = new Dictionary<ushort, RemoteVehicle>();
        private LocalVehicleController localVehicleController;

        // Player tracking
        private Dictionary<ushort, RemotePlayer> remotePlayers = new Dictionary<ushort, RemotePlayer>();
        private PlayerController localPlayerController;

        // Config
        private string serverAddress = "127.0.0.1";
        private int serverPort = NetworkConfig.DefaultPort;
        private string playerName = "Player";

        // Update timers
        private float highPriorityTimer = 0f;
        private float mediumPriorityTimer = 0f;
        private float lowPriorityTimer = 0f;
        private float heartbeatTimer = 0f;

        private const float HEARTBEAT_INTERVAL = 1.0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LogInfo("[MWCO] NetworkManager initialized");
        }

        // PlayerState timer
        private float playerStateTimer = 0f;
        private int playerStateSendCount = 0;

        void Start()
        {
            LoadConfig();
        }

        void Update()
        {
            ReceivePackets();

            if (!IsConnected)
                return;

            float deltaTime = Time.deltaTime;
            currentTick++;

            highPriorityTimer += deltaTime;
            mediumPriorityTimer += deltaTime;
            lowPriorityTimer += deltaTime;
            heartbeatTimer += deltaTime;
            playerStateTimer += deltaTime;

            if (highPriorityTimer >= 1.0f / NetworkConfig.HighPriorityUpdateRate)
            {
                SendHighPriorityUpdates();
                highPriorityTimer = 0f;
            }

            if (mediumPriorityTimer >= 1.0f / NetworkConfig.MediumPriorityUpdateRate)
            {
                SendMediumPriorityUpdates();
                mediumPriorityTimer = 0f;
            }

            if (lowPriorityTimer >= 1.0f / NetworkConfig.LowPriorityUpdateRate)
            {
                SendLowPriorityUpdates();
                lowPriorityTimer = 0f;
            }

            if (heartbeatTimer >= HEARTBEAT_INTERVAL)
            {
                SendHeartbeat();
                heartbeatTimer = 0f;
            }

            // Send PlayerState directly from NetworkManager at 50Hz
            if (playerStateTimer >= 0.02f)
            {
                SendPlayerStateDirectly();
                playerStateTimer = 0f;
            }
        }

        private void SendPlayerStateDirectly()
        {
            if (Camera.main == null)
                return;

            var packet = new PlayerStatePacket(LocalPlayerId, currentTick);
            
            Vector3 camPos = Camera.main.transform.position;
            Quaternion camRot = Camera.main.transform.rotation;

            packet.PositionX = camPos.x;
            packet.PositionY = camPos.y;
            packet.PositionZ = camPos.z;
            packet.RotationX = camRot.x;
            packet.RotationY = camRot.y;
            packet.RotationZ = camRot.z;
            packet.RotationW = camRot.w;

            playerStateSendCount++;
            if (playerStateSendCount <= 5 || playerStateSendCount % 250 == 0)
            {
                LogInfo($"[MWCO] Sending PlayerState #{playerStateSendCount} at ({camPos.x:F1}, {camPos.y:F1}, {camPos.z:F1})");
            }

            SendPacket(packet.ToBytes());
        }

        void OnDestroy()
        {
            Disconnect();
        }

        private void LoadConfig()
        {
            string configPath = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "MWCO", "config.txt"
            );

            try
            {
                if (System.IO.File.Exists(configPath))
                {
                    var configLines = System.IO.File.ReadAllLines(configPath);
                    foreach (var line in configLines)
                    {
                        if (line.StartsWith("player_name="))
                        {
                            string loadedName = line.Substring("player_name=".Length).Trim();
                            if (!string.IsNullOrEmpty(loadedName) && loadedName.Length <= ConnectionRequestPacket.MaxNameLength)
                            {
                                playerName = loadedName;
                                LogInfo($"[MWCO] Loaded player name: {playerName}");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[MWCO] Failed to load config: {ex.Message}");
            }

            playerName = SystemInfo.deviceName;
            LogInfo($"[MWCO] Using device name: {playerName}");
        }

        public void Connect(string address = null, int port = 0)
        {
            if (IsConnected)
            {
                LogWarning("[MWCO] Already connected!");
                return;
            }

            try
            {
                if (address != null) serverAddress = address;
                if (port > 0) serverPort = port;

                LogInfo($"[MWCO] Connecting to {serverAddress}:{serverPort}");
                
                serverEndPoint = new IPEndPoint(IPAddress.Parse(serverAddress), serverPort);
                udpClient = new UdpClient();
                udpClient.Client.Blocking = false;
                udpClient.Connect(serverEndPoint);

                var request = new ConnectionRequestPacket(playerName, currentTick);
                byte[] data = request.ToBytes();
                
                int sent = udpClient.Send(data, data.Length);
                PacketsSent++;
                LogInfo($"[MWCO] Connection request sent ({sent} bytes)");
            }
            catch (Exception ex)
            {
                LastError = $"Connection failed: {ex.Message}";
                LogError($"[MWCO] {LastError}");
            }
        }

        public void Disconnect()
        {
            if (!IsConnected)
                return;

            try
            {
                var header = new PacketHeader(PacketType.Disconnect, currentTick);
                SendPacket(header.ToBytes());
                udpClient?.Close();
                IsConnected = false;
                LogInfo("[MWCO] Disconnected from server");
            }
            catch (Exception ex)
            {
                LogError($"[MWCO] Error during disconnect: {ex.Message}");
            }
        }

        public void SendPacket(byte[] data)
        {
            try
            {
                if (udpClient == null)
                {
                    LastError = "UDP client is null";
                    return;
                }
                
                udpClient.Send(data, data.Length);
                PacketsSent++;
            }
            catch (Exception ex)
            {
                LastError = $"Send error: {ex.Message}";
            }
        }

        public void SendPacket<T>(T packet) where T : struct
        {
            try
            {
                int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
                byte[] data = new byte[size];
                IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
                System.Runtime.InteropServices.Marshal.StructureToPtr(packet, ptr, false);
                System.Runtime.InteropServices.Marshal.Copy(ptr, data, 0, size);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                SendPacket(data);
            }
            catch (Exception ex)
            {
                LogError($"[MWCO] Failed to send packet: {ex.Message}");
            }
        }

        private void ReceivePackets()
        {
            var client = udpClient;
            if (client == null) return;

            try
            {
                while (client.Available > 0)
                {
                    IPEndPoint remoteEndPoint = null;
                    byte[] data = client.Receive(ref remoteEndPoint);
                    PacketsReceived++;

                    if (data.Length >= PacketHeader.Size)
                    {
                        ProcessPacket(data);
                    }
                }
            }
            catch (SocketException se)
            {
                if (se.SocketErrorCode != SocketError.WouldBlock &&
                    se.SocketErrorCode != SocketError.ConnectionReset)
                {
                    LastError = $"Socket error: {se.Message}";
                }
            }
            catch (Exception ex)
            {
                LastError = $"Receive error: {ex.Message}";
            }
        }

        private void ProcessPacket(byte[] data)
        {
            try
            {
                var header = PacketHeader.FromBytes(data);

                switch (header.PacketType)
                {
                    case PacketType.ConnectionAccepted:
                        HandleConnectionAccepted(data);
                        break;
                    case PacketType.ConnectionDenied:
                        HandleConnectionDenied(data);
                        break;
                    case PacketType.VehicleStateUpdate:
                        HandleVehicleState(data);
                        break;
                    case PacketType.VehicleSpawn:
                        HandleVehicleSpawn(data);
                        break;
                    case PacketType.VehicleDespawn:
                        HandleVehicleDespawn(data);
                        break;
                    case PacketType.PlayerState:
                        HandlePlayerState(data);
                        break;
                    case PacketType.PlayerSpawn:
                        HandlePlayerSpawn(data);
                        break;
                    case PacketType.PlayerDespawn:
                        HandlePlayerDespawn(data);
                        break;
                    case PacketType.WheelStateUpdate:
                        HandleWheelState(data);
                        break;
                    case PacketType.GearChange:
                    case PacketType.EngineStart:
                    case PacketType.EngineStop:
                    case PacketType.LightToggle:
                    case PacketType.HornTrigger:
                        HandleVehicleEvent(data);
                        break;
                    case PacketType.FuelUpdate:
                        HandleVehicleConfig(data);
                        break;
                    case PacketType.PartAttach:
                    case PacketType.PartDetach:
                        HandlePartSync(data);
                        break;
                    case PacketType.TimeWeatherSync:
                        HandleTimeWeather(data);
                        break;
                    case PacketType.WorldObjectSpawn:
                        HandleWorldObjectSpawn(data);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogError($"[MWCO] Error processing packet: {ex.Message}");
            }
        }

        private void HandleConnectionAccepted(byte[] data)
        {
            var response = ConnectionResponsePacket.FromBytes(data);
            LocalPlayerId = response.AssignedPlayerId;
            LocalVehicleId = response.AssignedVehicleId;
            ServerTick = response.Header.Tick;
            IsConnected = true;

            LogInfo($"[MWCO] Connected! Player ID: {LocalPlayerId}, Vehicle ID: {LocalVehicleId}");

            LocalVehicleController.Logger = Logger;
            localVehicleController = gameObject.AddComponent<LocalVehicleController>();
            localVehicleController.Initialize(LocalVehicleId);

            localPlayerController = gameObject.AddComponent<PlayerController>();
            localPlayerController.Initialize(playerName);
        }

        private void HandleConnectionDenied(byte[] data)
        {
            var response = ConnectionResponsePacket.FromBytes(data);
            LastError = $"Connection denied: {response.Message}";
            LogError($"[MWCO] {LastError}");
        }

        private void HandleVehicleState(byte[] data)
        {
            var packet = VehicleStatePacket.FromBytes(data);

            if (packet.VehicleId == LocalVehicleId)
                return;

            if (!remoteVehicles.TryGetValue(packet.VehicleId, out var vehicle))
            {
                SpawnRemoteVehicleFromState(packet);
                return;
            }

            vehicle.UpdateState(packet);
        }

        private void SpawnRemoteVehicleFromState(VehicleStatePacket packet)
        {
            try
            {
                var parent = GameObject.Find("RemoteVehicles") ?? new GameObject("RemoteVehicles");

                var vehicleObj = new GameObject($"RemoteVehicle_{packet.VehicleId}");
                vehicleObj.transform.parent = parent.transform;
                vehicleObj.transform.position = new Vector3(packet.PositionX, packet.PositionY, packet.PositionZ);
                vehicleObj.transform.rotation = new Quaternion(packet.RotationX, packet.RotationY, packet.RotationZ, packet.RotationW);
                
                var remoteVehicle = vehicleObj.AddComponent<RemoteVehicle>();
                remoteVehicle.InitializeFromState(packet);

                remoteVehicles[packet.VehicleId] = remoteVehicle;
                LogInfo($"[MWCO] Remote vehicle {packet.VehicleId} spawned");
            }
            catch (Exception ex)
            {
                LogError($"[MWCO] Failed to spawn vehicle {packet.VehicleId}: {ex.Message}");
            }
        }

        private void HandleVehicleSpawn(byte[] data)
        {
            var packet = VehicleSpawnPacket.FromBytes(data);

            if (packet.VehicleId == LocalVehicleId)
                return;

            if (remoteVehicles.ContainsKey(packet.VehicleId))
                return;

            try
            {
                var parent = GameObject.Find("RemoteVehicles") ?? new GameObject("RemoteVehicles");

                var vehicleObj = new GameObject($"RemoteVehicle_{packet.VehicleId}");
                vehicleObj.transform.parent = parent.transform;
                
                var remoteVehicle = vehicleObj.AddComponent<RemoteVehicle>();
                remoteVehicle.Initialize(packet);

                remoteVehicles[packet.VehicleId] = remoteVehicle;
                LogInfo($"[MWCO] Remote vehicle {packet.VehicleId} spawned");
            }
            catch (Exception ex)
            {
                LogError($"[MWCO] Failed to spawn vehicle {packet.VehicleId}: {ex.Message}");
            }
        }

        private void HandleVehicleDespawn(byte[] data)
        {
            var packet = VehicleDespawnPacket.FromBytes(data);

            if (remoteVehicles.TryGetValue(packet.VehicleId, out var vehicle))
            {
                remoteVehicles.Remove(packet.VehicleId);
                if (vehicle != null && vehicle.gameObject != null)
                    Destroy(vehicle.gameObject);
            }
        }

        private void HandleWheelState(byte[] data)
        {
            var packet = WheelStatePacket.FromBytes(data);

            if (remoteVehicles.TryGetValue(packet.VehicleId, out var vehicle))
                vehicle.UpdateWheelState(packet);
        }

        private void HandleVehicleEvent(byte[] data)
        {
            var packet = VehicleEventPacket.FromBytes(data);

            if (remoteVehicles.TryGetValue(packet.VehicleId, out var vehicle))
                vehicle.HandleEvent(packet);
        }

        private void HandleVehicleConfig(byte[] data)
        {
            var packet = VehicleConfigPacket.FromBytes(data);

            if (remoteVehicles.TryGetValue(packet.VehicleId, out var vehicle))
                vehicle.UpdateConfig(packet);
        }

        private void HandlePartSync(byte[] data)
        {
            var packet = PartSyncPacket.FromBytes(data);

            if (remoteVehicles.TryGetValue(packet.VehicleId, out var vehicle))
                vehicle.UpdatePart(packet);
        }

        private void HandleTimeWeather(byte[] data)
        {
            var packet = TimeWeatherPacket.FromBytes(data);
            // TODO: Sync game time and weather
        }

        private void SendHighPriorityUpdates()
        {
            if (localVehicleController != null && localVehicleController.IsReady)
            {
                var statePacket = localVehicleController.GetStatePacket(currentTick);
                SendPacket(statePacket.ToBytes());

                var inputPacket = localVehicleController.GetInputPacket(currentTick);
                SendPacket(inputPacket.ToBytes());
                
                var playerPacket = localVehicleController.GetPlayerStatePacket(LocalPlayerId, currentTick);
                SendPacket(playerPacket.ToBytes());
            }
        }

        private void SendMediumPriorityUpdates()
        {
            if (localVehicleController != null && localVehicleController.IsReady)
            {
                var wheelPacket = localVehicleController.GetWheelStatePacket(currentTick);
                SendPacket(wheelPacket.ToBytes());
            }
        }

        private void SendLowPriorityUpdates()
        {
            if (localVehicleController != null && localVehicleController.IsReady)
            {
                var configPacket = localVehicleController.GetConfigPacket(currentTick);
                SendPacket(configPacket.ToBytes());
            }
        }

        private void SendHeartbeat()
        {
            var header = new PacketHeader(PacketType.Heartbeat, currentTick);
            SendPacket(header.ToBytes());
        }

        private void HandlePlayerState(byte[] data)
        {
            var packet = PlayerStatePacket.FromBytes(data);

            if (packet.PlayerId == LocalPlayerId)
                return;

            if (!remotePlayers.TryGetValue(packet.PlayerId, out var player))
            {
                SpawnRemotePlayerFromState(packet);
                return;
            }

            Vector3 position = new Vector3(packet.PositionX, packet.PositionY, packet.PositionZ);
            Quaternion rotation = new Quaternion(packet.RotationX, packet.RotationY, packet.RotationZ, packet.RotationW);
            player.UpdateState(position, rotation);
        }

        private void SpawnRemotePlayerFromState(PlayerStatePacket packet)
        {
            try
            {
                Vector3 pos = new Vector3(packet.PositionX, packet.PositionY, packet.PositionZ);
                LogInfo($"[MWCO] *** SPAWNING REMOTE PLAYER {packet.PlayerId} at {pos} ***");
                
                var parent = GameObject.Find("RemotePlayers") ?? new GameObject("RemotePlayers");

                var playerObj = new GameObject($"RemotePlayer_{packet.PlayerId}");
                playerObj.transform.parent = parent.transform;
                playerObj.transform.position = pos;
                playerObj.transform.rotation = new Quaternion(packet.RotationX, packet.RotationY, packet.RotationZ, packet.RotationW);
                
                var remotePlayer = playerObj.AddComponent<RemotePlayer>();
                remotePlayer.Initialize(packet.PlayerId, $"Player{packet.PlayerId}");

                remotePlayers[packet.PlayerId] = remotePlayer;
                LogInfo($"[MWCO] Remote player {packet.PlayerId} spawned successfully at {pos}");
            }
            catch (Exception ex)
            {
                LogError($"[MWCO] Failed to spawn player {packet.PlayerId}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void HandlePlayerSpawn(byte[] data)
        {
            var packet = PlayerSpawnPacket.FromBytes(data);

            if (packet.PlayerId == LocalPlayerId)
                return;

            var playerObj = new GameObject($"RemotePlayer_{packet.PlayerId}");
            var remotePlayer = playerObj.AddComponent<RemotePlayer>();
            remotePlayer.Initialize(packet.PlayerId, packet.GetPlayerName());

            playerObj.transform.position = new Vector3(packet.SpawnPositionX, packet.SpawnPositionY, packet.SpawnPositionZ);
            remotePlayers[packet.PlayerId] = remotePlayer;
        }

        private void HandlePlayerDespawn(byte[] data)
        {
            var packet = PlayerDespawnPacket.FromBytes(data);

            if (remotePlayers.TryGetValue(packet.PlayerId, out var player))
            {
                Destroy(player.gameObject);
                remotePlayers.Remove(packet.PlayerId);
            }
        }
        
        private void LogInfo(string message)
        {
            Debug.Log(message);
            Logger?.LogInfo(message);
        }
        
        private void LogError(string message)
        {
            Debug.LogError(message);
            Logger?.LogError(message);
        }
        
        private void LogWarning(string message)
        {
            Debug.LogWarning(message);
            Logger?.LogWarning(message);
        }

        // ===== TEST CUBE NETWORKING =====
        private static uint nextTestCubeId = 1;
        private Dictionary<uint, GameObject> testCubes = new Dictionary<uint, GameObject>();

        public void SpawnTestCube()
        {
            try
            {
                // Spawn 1km away from origin, 1km in the sky - impossible to miss!
                Vector3 spawnPos = new Vector3(1000f, 1000f, 0f);
                Quaternion spawnRot = Quaternion.identity;

                // Spawn locally first
                uint cubeId = nextTestCubeId++;
                SpawnTestCubeLocal(cubeId, spawnPos, spawnRot);

                // Create packet using the constructor properly
                string name = $"TestCube_{LocalPlayerId}_{cubeId}";
                var packet = new WorldObjectPacket(cubeId, name, 255, PacketType.WorldObjectSpawn, currentTick)
                {
                    PosX = spawnPos.x,
                    PosY = spawnPos.y,
                    PosZ = spawnPos.z,
                    RotX = spawnRot.x,
                    RotY = spawnRot.y,
                    RotZ = spawnRot.z,
                    RotW = spawnRot.w
                };

                SendPacket(packet.ToBytes());
                LogInfo($"[MWCO] Sent test cube spawn: {name} at {spawnPos}");
            }
            catch (Exception ex)
            {
                LogError($"[MWCO] Failed to spawn test cube: {ex.Message}");
            }
        }

        private void SpawnTestCubeLocal(uint cubeId, Vector3 position, Quaternion rotation)
        {
            // Create primitive cube - 1km x 1km x 1km, impossible to miss!
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"TestCube_{cubeId}";
            cube.transform.position = position;
            cube.transform.rotation = rotation;
            cube.transform.localScale = new Vector3(1000f, 1000f, 1000f);

            // Make it magenta
            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = Color.magenta;
            }

            // Store reference
            testCubes[cubeId] = cube;
            LogInfo($"[MWCO] Spawned test cube {cubeId} at {position}");
        }

        private void HandleWorldObjectSpawn(byte[] data)
        {
            try
            {
                var packet = WorldObjectPacket.FromBytes(data);
                
                // Only handle test cubes (type 255)
                if (packet.ObjectType != 255)
                    return;

                // Don't spawn our own cubes again
                if (packet.ObjectName.Contains($"_{LocalPlayerId}_"))
                {
                    LogInfo($"[MWCO] Ignoring own test cube: {packet.ObjectName}");
                    return;
                }

                Vector3 position = new Vector3(packet.PosX, packet.PosY, packet.PosZ);
                Quaternion rotation = new Quaternion(packet.RotX, packet.RotY, packet.RotZ, packet.RotW);

                SpawnTestCubeLocal(packet.ObjectId, position, rotation);
                LogInfo($"[MWCO] Received remote test cube: {packet.ObjectName}");
            }
            catch (Exception ex)
            {
                LogError($"[MWCO] Failed to handle world object spawn: {ex.Message}");
            }
        }
    }
}
