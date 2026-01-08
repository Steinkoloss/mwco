# MWCO - My Winter Car Online AI Coding Guidelines

## Project Architecture

MWCO is a multiplayer mod for My Winter Car using client-server UDP architecture:

- **MWCO.Shared**: Network protocol, packet structures, constants
- **MWCO.Server**: Dedicated server handling connections and packet routing
- **MWCO.Client**: Unity mod DLL injected via BepInEx with Harmony patches
- **decompiled/**: Reference copy of game's Assembly-CSharp.dll for understanding game internals

## Key Components

### Networking
- **NetworkManager** (Client): Singleton managing UDP connection, packet routing, update timers
- **UdpServer** (Server): Handles client connections, broadcasts state updates at 50Hz physics ticks
- **LocalVehicleController**: Captures player car state from game objects (position, inputs, physics)
- **RemoteVehicle**: Interpolates and renders other players' vehicles with 100ms delay buffer

### Packet System
15+ packet types with prioritized rates:
- **50Hz**: VehicleStatePacket (84B), VehicleInputPacket (34B) - transforms, inputs, engine state
- **20Hz**: WheelStatePacket (72B) - wheel physics, suspension
- **5Hz**: VehicleConfigPacket (42B) - fuel, damage, tire pressure

Packets use `[StructLayout(LayoutKind.Sequential, Pack = 1)]` for binary serialization.

## Critical Workflows

### Building Distribution
Use dotnet to build server and client projects
relevant paths: 
C:\Program Files (x86)\Steam\steamapps\common\My Winter Car
C:\Program Files (x86)\Steam\steamapps\common\My Winter Car 2

### Running Server
```bash
dotnet run --project MWCO.Server/MWCO.Server/MWCO.Server.csproj
# Or use built executable: ./dist/mwco-server
```

### Installing Mod
Extract `mwco-mod.zip` to `My Winter Car/BepInEx/plugins/` or run `install-mod.bat`

### Connecting In-Game
Press F10 → Enter server IP:port (default 127.0.0.1:1999) → Connect

## Development Patterns

### Packet Creation
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VehicleStatePacket
{
    public PacketHeader Header;
    public ushort VehicleId;
    // ... fields
    
    public VehicleStatePacket(ushort vehicleId, uint tick)
    {
        Header = new PacketHeader(PacketType.VehicleStateUpdate, tick);
        VehicleId = vehicleId;
        // ... initialize
    }
}
```

### Client State Capture
Hook into game objects via Harmony patches:
```csharp
[HarmonyPatch(typeof(CarController), "Update")]
[HarmonyPostfix]
public static void CarController_Update_Postfix(CarController __instance)
{
    // Capture steering, throttle, brake inputs
}
```

### Server Broadcasting
```csharp
private async Task ProcessTick()
{
    // Broadcast vehicle states to all clients
    foreach (var client in _clients.Values)
    {
        await SendPacketAsync(client.EndPoint, vehicleStatePacket);
    }
}
```

## Integration Points

### Game Hooks
- **CarController**: Input capture (steering, throttle, brake)
- **CarDynamics**: Physics state (velocity, angular velocity)
- **Drivetrain**: Engine state (RPM, gear, running)
- **Axles/Wheels**: Wheel physics (rotation, compression, steering)

### Unity Integration
- NetworkManager runs as DontDestroyOnLoad MonoBehaviour
- Update loops use Time.deltaTime for rate limiting
- Remote vehicles use interpolation between state snapshots

## Conventions

### Naming
- Packet structs: `{Type}Packet` (VehicleStatePacket, ConnectionRequestPacket)
- Components: `{Purpose}{Type}` (LocalVehicleController, RemoteVehicle)
- Enums: PascalCase (PacketType.VehicleStateUpdate)

### Error Handling
- UDP is connectionless - handle packet loss gracefully
- Use try/catch in network code for robustness
- Log errors with `[MWCO]` prefix for filtering

### Performance
- Minimize allocations in hot paths (packet serialization)
- Use struct packets for zero-allocation serialization
- Rate-limit updates to match physics tick (50Hz)

## Common Pitfalls

### Physics Synchronization
Game uses complex Pacejka tire physics - server currently relays packets but should implement authoritative physics simulation.

### Input Smoothing
CarController applies input smoothing - send raw inputs to server, apply smoothing locally only.

### Tick Synchronization
Match 50Hz physics rate exactly - use TimeSpan for precise timing, not float deltas.

## Key Files
- `MWCO.Shared/Packets/VehicleStatePacket.cs` - Core vehicle sync packet
- `MWCO.Client/Networking/NetworkManager.cs` - Client network management
- `MWCO.Server/UdpServer.cs` - Server implementation
- `decompiled/CarController.cs` - Game input handling reference
- `build.py` - Distribution build script</content>
<parameter name="filePath">/workspaces/mwco/.github/copilot-instructions.md