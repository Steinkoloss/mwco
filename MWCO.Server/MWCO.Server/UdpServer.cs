using System.Net;
using System.Net.Sockets;
using System.Linq;
using MWCO.Shared;
using MWCO.Shared.Packets;

namespace MWCO.Server;

/// <summary>
/// Basic UDP server for MWCO multiplayer
/// Handles client connections and packet routing
/// </summary>
public class UdpServer
{
    private readonly UdpClient _udpClient;
    private readonly Dictionary<IPEndPoint, ConnectedClient> _clients;
    private readonly int _port;
    private uint _currentTick;
    private bool _running;
    private ushort _nextPlayerId = 1;
    private ushort _nextVehicleId = 1;

    public UdpServer(int port = NetworkConfig.DefaultPort)
    {
        _port = port;
        _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        _clients = new Dictionary<IPEndPoint, ConnectedClient>();
        _currentTick = 0;
        Console.WriteLine($"[MWCO Server] UDP Server initialized - listening on 0.0.0.0:{port}");
    }

    public async Task StartAsync()
    {
        _running = true;
        Console.WriteLine($"[MWCO Server] Starting on port {_port}...");
        Console.WriteLine($"[MWCO Server] Protocol version: {NetworkConfig.ProtocolVersion}");
        Console.WriteLine($"[MWCO Server] Physics tick rate: {NetworkConfig.PhysicsTickRate}Hz");

        // Start server loop - both must run forever
        var receiveTask = ReceiveLoopAsync();
        var tickTask = TickLoopAsync();

        try
        {
            // This will never complete unless we call Stop()
            await Task.WhenAll(receiveTask, tickTask);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[MWCO Server] Server was cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MWCO Server] FATAL: Unhandled exception in main loop!");
            Console.WriteLine($"[MWCO Server] Error: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[MWCO Server] Stack: {ex.StackTrace}");
        }
    }

    public void Stop()
    {
        _running = false;
        _udpClient.Close();
        Console.WriteLine("[MWCO Server] Server stopped.");
    }

    private async Task ReceiveLoopAsync()
    {
        Console.WriteLine("[MWCO Server] Receive loop started.");

        while (_running)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync();
                // Verbose logging removed - enable for debugging
                // Console.WriteLine($"[MWCO Server] Received {result.Buffer.Length} bytes from {result.RemoteEndPoint}");
                _ = Task.Run(() => 
                {
                    try
                    {
                        ProcessPacket(result.Buffer, result.RemoteEndPoint);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MWCO Server] CRITICAL: Exception in ProcessPacket task!");
                        Console.WriteLine($"[MWCO Server] Error: {ex.GetType().Name}: {ex.Message}");
                        Console.WriteLine($"[MWCO Server] Stack: {ex.StackTrace}");
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                // Server is shutting down
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MWCO Server] Receive error: {ex.Message}");
                Console.WriteLine($"[MWCO Server] Stack trace: {ex.StackTrace}");
            }
        }

        Console.WriteLine("[MWCO Server] Receive loop stopped.");
    }

    private async Task TickLoopAsync()
    {
        Console.WriteLine("[MWCO Server] Tick loop started.");
        var tickInterval = TimeSpan.FromSeconds(1.0 / NetworkConfig.PhysicsTickRate);

        try
        {
            uint tickCount = 0;
            while (_running)
            {
                tickCount++;
                var tickStart = DateTime.UtcNow;

                try
                {
                    // Process game tick
                    ProcessTick();
                    _currentTick++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MWCO Server] ERROR in ProcessTick: {ex.Message}");
                    throw;
                }

                // Sleep until next tick
                var elapsed = DateTime.UtcNow - tickStart;
                var remaining = tickInterval - elapsed;

                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining);
                }
                else if (tickCount % 250 == 0)  // Log occasional overruns
                {
                    Console.WriteLine($"[MWCO Server] Warning: Tick {tickCount} took {elapsed.TotalMilliseconds:F2}ms (target: {tickInterval.TotalMilliseconds:F2}ms)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MWCO Server] CRITICAL: Exception in TickLoop!");
            Console.WriteLine($"[MWCO Server] Error: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[MWCO Server] Stack: {ex.StackTrace}");
            throw;
        }

        Console.WriteLine("[MWCO Server] Tick loop stopped.");
    }

    private void ProcessTick()
    {
        // Log status every 5 seconds (250 ticks at 50Hz)
        if (_currentTick % 250 == 0)
        {
            if (_clients.Count > 0)
            {
                Console.WriteLine($"[SERVER] Tick {_currentTick}, {_clients.Count} client(s)");
                // Show packet stats
                Console.WriteLine($"[SERVER] Packet counts received:");
                foreach (var kv in _packetCounts.OrderByDescending(x => x.Value))
                {
                    Console.WriteLine($"[SERVER]   {kv.Key}: {kv.Value}");
                }
            }
            else if (_currentTick == 0 || _currentTick == 250)
            {
                Console.WriteLine($"[SERVER] Tick {_currentTick}, waiting for clients...");
            }
        }

        // Broadcast vehicle states to all clients
        BroadcastVehicleStates();

        // Broadcast player states to all clients
        BroadcastPlayerStates();

        // Check for timeouts
        CheckTimeouts();
    }

    // Packet counters for debugging
    private Dictionary<PacketType, int> _packetCounts = new Dictionary<PacketType, int>();

    private void ProcessPacket(byte[] data, IPEndPoint remoteEndPoint)
    {
        if (data.Length < PacketHeader.Size)
        {
            Console.WriteLine($"[SERVER] Received packet too small from {remoteEndPoint}");
            return;
        }

        try
        {
            var header = PacketHeader.FromBytes(data);
            
            // Count packets by type
            if (!_packetCounts.ContainsKey(header.PacketType))
                _packetCounts[header.PacketType] = 0;
            _packetCounts[header.PacketType]++;

            // Verify protocol version
            if (header.ProtocolVersion != NetworkConfig.ProtocolVersion)
            {
                Console.WriteLine($"[SERVER] Protocol version mismatch from {remoteEndPoint}: {header.ProtocolVersion} vs {NetworkConfig.ProtocolVersion}");
                SendConnectionDenied(remoteEndPoint, "Protocol version mismatch");
                return;
            }

            switch (header.PacketType)
            {
                case PacketType.ConnectionRequest:
                    Console.WriteLine($"[SERVER] Handling ConnectionRequest...");
                    HandleConnectionRequest(data, remoteEndPoint);
                    break;

                case PacketType.VehicleStateUpdate:
                    HandleVehicleStateUpdate(data, remoteEndPoint);
                    break;

                case PacketType.VehicleInputUpdate:
                    HandleVehicleInput(data, remoteEndPoint);
                    break;

                case PacketType.PlayerState:
                    HandlePlayerState(data, remoteEndPoint);
                    break;

                case PacketType.Heartbeat:
                    HandleHeartbeat(remoteEndPoint);
                    break;

                case PacketType.Disconnect:
                    HandleDisconnect(remoteEndPoint);
                    break;

                case PacketType.WorldObjectSpawn:
                    HandleWorldObjectSpawn(data, remoteEndPoint);
                    break;

                default:
                    Console.WriteLine($"[SERVER] Unknown packet type {header.PacketType} from {remoteEndPoint}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SERVER] Error processing packet from {remoteEndPoint}: {ex.Message}");
            Console.WriteLine($"[SERVER] Stack trace: {ex.StackTrace}");
        }
    }

    private void HandleConnectionRequest(byte[] data, IPEndPoint remoteEndPoint)
    {
        var packet = ConnectionRequestPacket.FromBytes(data);
        Console.WriteLine($"[<<< CLIENT] ConnectionRequest from {remoteEndPoint}, player: {packet.PlayerName}");

        // Check if already connected
        if (_clients.ContainsKey(remoteEndPoint))
        {
            Console.WriteLine($"[<<< CLIENT] Client {remoteEndPoint} already connected");
            return;
        }

        // Assign IDs
        ushort playerId = _nextPlayerId++;
        ushort vehicleId = _nextVehicleId++;

        // Create client with initialized vehicle state
        var client = new ConnectedClient
        {
            EndPoint = remoteEndPoint,
            PlayerId = playerId,
            VehicleId = vehicleId,
            PlayerName = packet.PlayerName,
            ConnectedTick = _currentTick,
            LastSeenTick = _currentTick,
            VehicleState = new VehicleState
            {
                VehicleId = vehicleId,
                PlayerId = playerId,
                LastUpdateTick = _currentTick
            }
        };

        _clients[remoteEndPoint] = client;

        // Send acceptance
        var response = new ConnectionResponsePacket(
            accepted: true,
            playerId: playerId,
            vehicleId: vehicleId,
            message: $"Welcome to MWCO Server! You are player {playerId}",
            tick: _currentTick
        );

        SendPacket(response.ToBytes(), remoteEndPoint);
        Console.WriteLine($"[>>> CLIENT] Sent ConnectionAccepted to {packet.PlayerName} (Player ID: {playerId}, Vehicle ID: {vehicleId})");
    }

    private void SendConnectionDenied(IPEndPoint remoteEndPoint, string reason)
    {
        var response = new ConnectionResponsePacket(
            accepted: false,
            playerId: 0,
            vehicleId: 0,
            message: reason,
            tick: _currentTick
        );

        SendPacket(response.ToBytes(), remoteEndPoint);
    }

    private void HandleVehicleStateUpdate(byte[] data, IPEndPoint remoteEndPoint)
    {
        if (!_clients.TryGetValue(remoteEndPoint, out var client))
        {
            Console.WriteLine($"[<<< CLIENT] VehicleState from unknown client {remoteEndPoint}");
            return;
        }

        var packet = VehicleStatePacket.FromBytes(data);

        // Validate position is plausible (basic sanity checks)
        // Reject positions with NaN or infinity
        if (float.IsNaN(packet.PositionX) || float.IsNaN(packet.PositionY) || float.IsNaN(packet.PositionZ) ||
            float.IsInfinity(packet.PositionX) || float.IsInfinity(packet.PositionY) || float.IsInfinity(packet.PositionZ))
        {
            Console.WriteLine($"[<<< CLIENT] Vehicle {packet.VehicleId} invalid position (NaN/Infinity)");
            return;
        }

        // Reject positions that are unreasonably far away (e.g., >10km in any axis)
        const float MAX_POSITION = 10000f;
        if (System.Math.Abs(packet.PositionX) > MAX_POSITION || 
            System.Math.Abs(packet.PositionY) > MAX_POSITION || 
            System.Math.Abs(packet.PositionZ) > MAX_POSITION)
        {
            Console.WriteLine($"[<<< CLIENT] Vehicle {packet.VehicleId} out-of-bounds ({packet.PositionX}, {packet.PositionY}, {packet.PositionZ})");
            return;
        }

        // Reject unreasonable velocity (>500 m/s = way too fast)
        float velocityMagnitude = System.MathF.Sqrt(packet.VelocityX * packet.VelocityX + 
                                                      packet.VelocityY * packet.VelocityY + 
                                                      packet.VelocityZ * packet.VelocityZ);
        if (velocityMagnitude > 500f)
        {
            Console.WriteLine($"[<<< CLIENT] Vehicle {packet.VehicleId} unrealistic velocity ({velocityMagnitude:F1} m/s)");
            return;
        }

        // Reject invalid RPM
        if (packet.RPM < 0 || packet.RPM > 10000)
        {
            Console.WriteLine($"[<<< CLIENT] Vehicle {packet.VehicleId} invalid RPM ({packet.RPM})");
            return;
        }

        // Update client's vehicle state from received packet
        client.VehicleState.UpdateFromPacket(packet);
        client.VehicleState.LastUpdateTick = _currentTick;
        client.LastSeenTick = _currentTick;
        
        // Log position every 50 ticks (1 second at 50Hz) for easier debugging
        if (_currentTick % 50 == 0)
        {
            Console.WriteLine($"[<<< CLIENT] VehicleState {client.PlayerName} at ({packet.PositionX:F1}, {packet.PositionY:F1}, {packet.PositionZ:F1})");
        }
    }

    private void HandleVehicleInput(byte[] data, IPEndPoint remoteEndPoint)
    {
        if (!_clients.TryGetValue(remoteEndPoint, out var client))
        {
            Console.WriteLine($"[<<< CLIENT] VehicleInput from unknown client {remoteEndPoint}");
            return;
        }

        var packet = VehicleInputPacket.FromBytes(data);

        // Store input for reference (not used in client-authoritative mode)
        client.LastInput = packet;
        client.LastSeenTick = _currentTick;
    }

    private int playerStateReceiveCount = 0;
    
    private void HandlePlayerState(byte[] data, IPEndPoint remoteEndPoint)
    {
        playerStateReceiveCount++;
        
        if (!_clients.TryGetValue(remoteEndPoint, out var client))
        {
            Console.WriteLine($"[<<< CLIENT] PlayerState from unknown client {remoteEndPoint}");
            return;
        }

        var packet = PlayerStatePacket.FromBytes(data);
        
        // Log first few receives
        if (playerStateReceiveCount <= 5)
        {
            Console.WriteLine($"[<<< CLIENT] PlayerState #{playerStateReceiveCount} from {client.PlayerName} at ({packet.PositionX:F1}, {packet.PositionY:F1}, {packet.PositionZ:F1})");
        }

        // Validate position is plausible
        if (float.IsNaN(packet.PositionX) || float.IsNaN(packet.PositionY) || float.IsNaN(packet.PositionZ) ||
            float.IsInfinity(packet.PositionX) || float.IsInfinity(packet.PositionY) || float.IsInfinity(packet.PositionZ))
        {
            Console.WriteLine($"[<<< CLIENT] Player {packet.PlayerId} invalid position (NaN/Infinity)");
            return;
        }

        // Reject positions that are unreasonably far away
        const float MAX_POSITION = 10000f;
        if (System.Math.Abs(packet.PositionX) > MAX_POSITION || 
            System.Math.Abs(packet.PositionY) > MAX_POSITION || 
            System.Math.Abs(packet.PositionZ) > MAX_POSITION)
        {
            Console.WriteLine($"[<<< CLIENT] Player {packet.PlayerId} out-of-bounds ({packet.PositionX}, {packet.PositionY}, {packet.PositionZ})");
            return;
        }

        // Store player state for broadcasting
        client.PlayerState = packet;
        client.LastSeenTick = _currentTick;

        // Log position every second
        if (_currentTick % 50 == 0)
        {
            Console.WriteLine($"[<<< CLIENT] PlayerState {client.PlayerName} at ({packet.PositionX:F1}, {packet.PositionY:F1}, {packet.PositionZ:F1})");
        }
    }

    private void HandleHeartbeat(IPEndPoint remoteEndPoint)
    {
        if (_clients.TryGetValue(remoteEndPoint, out var client))
        {
            client.LastSeenTick = _currentTick;
            // Log heartbeat periodically (every ~5 seconds = 250 ticks at 50Hz)
            if (_currentTick % 250 == 0)
            {
                Console.WriteLine($"[<<< CLIENT] Heartbeat from {client.PlayerName}");
            }
        }
        else
        {
            Console.WriteLine($"[<<< CLIENT] Heartbeat from unknown {remoteEndPoint}");
        }
    }

    private void HandleDisconnect(IPEndPoint remoteEndPoint)
    {
        if (_clients.TryGetValue(remoteEndPoint, out var client))
        {
            Console.WriteLine($"[<<< CLIENT] Disconnect from {client.PlayerName}");
            _clients.Remove(remoteEndPoint);
        }
    }

    private void BroadcastVehicleStates()
    {
        // Take a snapshot of clients to avoid collection modified exception
        var clients = _clients.Values.ToList();
        
        // Log broadcast stats every 5 seconds
        if (_currentTick % 250 == 0 && clients.Count > 0)
        {
            Console.WriteLine($"[SERVER] Status: {clients.Count} clients connected");
            foreach (var c in clients)
            {
                Console.WriteLine($"[SERVER]   - {c.PlayerName} @ ({c.VehicleState.Position.X:F1}, {c.VehicleState.Position.Y:F1}, {c.VehicleState.Position.Z:F1})");
            }
        }
        
        // For each connected vehicle, broadcast its server-authoritative state to all other clients
        foreach (var client in clients)
        {
            // Get server-side authoritative state
            var statePacket = client.VehicleState.ToPacket();
            statePacket.Header.Tick = _currentTick;

            // Broadcast to all OTHER clients
            int broadcastCount = 0;
            foreach (var otherClient in clients)
            {
                if (otherClient.PlayerId != client.PlayerId)
                {
                    SendPacket(statePacket.ToBytes(), otherClient.EndPoint);
                    broadcastCount++;
                }
            }
            
            // Verbose broadcast logging removed
        }
    }

    private void BroadcastPlayerStates()
    {
        // Take a snapshot of clients to avoid collection modified exception
        var clients = _clients.Values.ToList();
        
        int playerStatesWithData = 0;
        int broadcastsSent = 0;
        
        // For each connected player, broadcast their state to all other clients
        foreach (var client in clients)
        {
            // Skip if no player state received yet
            if (client.PlayerState == null)
                continue;

            playerStatesWithData++;
            var statePacket = client.PlayerState.Value;
            statePacket.Header.Tick = _currentTick;

            // Broadcast to all OTHER clients
            foreach (var otherClient in clients)
            {
                if (otherClient.PlayerId != client.PlayerId)
                {
                    SendPacket(statePacket.ToBytes(), otherClient.EndPoint);
                    broadcastsSent++;
                }
            }
        }
        
        // Log every 5 seconds
        if (_currentTick % 250 == 0 && clients.Count > 0)
        {
            Console.WriteLine($"[>>> CLIENT] Broadcasting PlayerState: {playerStatesWithData}/{clients.Count} synced");
        }
    }

    private void CheckTimeouts()
    {
        var timeoutTicks = (uint)(NetworkConfig.ConnectionTimeoutSeconds * NetworkConfig.PhysicsTickRate);
        var toRemove = new List<IPEndPoint>();

        foreach (var (endPoint, client) in _clients)
        {
            if (_currentTick - client.LastSeenTick > timeoutTicks)
            {
                Console.WriteLine($"[SERVER] Client {client.PlayerName} timed out");
                toRemove.Add(endPoint);
            }
        }

        foreach (var endPoint in toRemove)
        {
            _clients.Remove(endPoint);
        }
    }

    private void HandleWorldObjectSpawn(byte[] data, IPEndPoint senderEndPoint)
    {
        // Get sender client
        if (!_clients.TryGetValue(senderEndPoint, out var sender))
        {
            Console.WriteLine($"[<<< CLIENT] WorldObjectSpawn from unknown {senderEndPoint}");
            return;
        }

        var packet = WorldObjectPacket.FromBytes(data);
        Console.WriteLine($"[<<< CLIENT] WorldObjectSpawn from {sender.PlayerName}: {packet.ObjectName} at ({packet.PosX:F1}, {packet.PosY:F1}, {packet.PosZ:F1})");

        // Broadcast to ALL clients (including sender, they'll filter themselves)
        foreach (var client in _clients.Values.ToList())
        {
            SendPacket(data, client.EndPoint);
        }

        Console.WriteLine($"[>>> CLIENT] Broadcast WorldObjectSpawn to {_clients.Count} clients");
    }

    private void SendPacket(byte[] data, IPEndPoint destination)
    {
        try
        {
            _udpClient.Send(data, data.Length, destination);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MWCO Server] Error sending to {destination}: {ex.Message}");
        }
    }
}

/// <summary>
/// Represents a connected client
/// </summary>
public class ConnectedClient
{
    public required IPEndPoint EndPoint { get; set; }
    public required ushort PlayerId { get; set; }
    public required ushort VehicleId { get; set; }
    public required string PlayerName { get; set; }
    public required uint ConnectedTick { get; set; }
    public required uint LastSeenTick { get; set; }

    public VehicleInputPacket? LastInput { get; set; }
    public VehicleState VehicleState { get; set; } = new VehicleState();
    
    // Player state (position, rotation, animation)
    public PlayerStatePacket? PlayerState { get; set; }
}
