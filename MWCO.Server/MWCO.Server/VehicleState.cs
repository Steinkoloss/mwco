using System.Numerics;
using MWCO.Shared;
using MWCO.Shared.Packets;

namespace MWCO.Server;

public class VehicleState
{
    public ushort VehicleId { get; set; }
    public ushort PlayerId { get; set; }

    // Transform
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Velocity { get; set; }
    public Vector3 AngularVelocity { get; set; }

    // Engine
    public float RPM { get; set; }
    public sbyte Gear { get; set; }
    public float Throttle { get; set; }
    public float Brake { get; set; }
    public float Clutch { get; set; }
    public float Steering { get; set; }

    // Wheels
    public WheelData[] Wheels { get; set; } = new WheelData[4];

    // Config
    public float Fuel { get; set; }
    public float[] TirePressure { get; set; } = new float[4];
    public float Damage { get; set; }

    // Timing
    public uint LastUpdateTick { get; set; }
    public DateTime LastUpdateTime { get; set; }

    public VehicleState()
    {
        Position = Vector3.Zero;
        Rotation = Quaternion.Identity;
        Velocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        LastUpdateTime = DateTime.UtcNow;
    }

    public void ApplyInput(VehicleInputPacket input)
    {
        Steering = input.SteerInput;
        Throttle = input.ThrottleInput;
        Brake = input.BrakeInput;
        Clutch = input.ClutchInput;
    }

    public void UpdateFromPacket(VehicleStatePacket packet)
    {
        Position = new Vector3(packet.PositionX, packet.PositionY, packet.PositionZ);
        Rotation = new Quaternion(packet.RotationX, packet.RotationY, packet.RotationZ, packet.RotationW);
        Velocity = new Vector3(packet.VelocityX, packet.VelocityY, packet.VelocityZ);
        AngularVelocity = new Vector3(packet.AngularVelocityX, packet.AngularVelocityY, packet.AngularVelocityZ);
        RPM = packet.RPM;
        Gear = packet.Gear;
        Steering = packet.Steering;
        Throttle = packet.Throttle;
        Brake = packet.Brake;
        LastUpdateTime = DateTime.UtcNow;
    }

    public VehicleStatePacket ToPacket()
    {
        return new VehicleStatePacket(VehicleId, LastUpdateTick)
        {
            PositionX = Position.X,
            PositionY = Position.Y,
            PositionZ = Position.Z,
            RotationX = Rotation.X,
            RotationY = Rotation.Y,
            RotationZ = Rotation.Z,
            RotationW = Rotation.W,
            VelocityX = Velocity.X,
            VelocityY = Velocity.Y,
            VelocityZ = Velocity.Z,
            AngularVelocityX = AngularVelocity.X,
            AngularVelocityY = AngularVelocity.Y,
            AngularVelocityZ = AngularVelocity.Z,
            RPM = RPM,
            Gear = Gear,
            EngineRunning = (byte)(RPM > 0 ? 1 : 0),
            Steering = Steering,
            Throttle = Throttle,
            Brake = Brake
        };
    }
}

public struct WheelData
{
    public float AngularVelocity;
    public float Compression;
    public float SteerAngle;
}
