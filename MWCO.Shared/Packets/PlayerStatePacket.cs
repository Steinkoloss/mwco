using System;
using System.Runtime.InteropServices;

namespace MWCO.Shared.Packets;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PlayerStatePacket
{
    public PacketHeader Header;
    public ushort PlayerId;
    public uint Tick;

    // Transform
    public float PositionX;
    public float PositionY;
    public float PositionZ;
    public float RotationX;
    public float RotationY;
    public float RotationZ;
    public float RotationW;

    // Animation states
    public byte IsWalking;
    public byte IsRunning;
    public byte IsCrouching;
    public byte IsInVehicle;

    public PlayerStatePacket(ushort playerId, uint tick)
    {
        Header = new PacketHeader(PacketType.PlayerState, tick);
        PlayerId = playerId;
        Tick = tick;
        PositionX = PositionY = PositionZ = 0f;
        RotationX = RotationY = RotationZ = 0f;
        RotationW = 1f;
        IsWalking = IsRunning = IsCrouching = IsInVehicle = 0;
    }

    public static PlayerStatePacket FromBytes(byte[] bytes, int startOffset = 0)
    {
        // Expected layout: Header (8) | PlayerId (2) | Tick (4) | Position (12) | Rotation (16) | Anim (4) = 46 bytes
        const int expectedSize = PacketHeader.Size + 2 + 4 + 12 + 16 + 4;
        if (bytes.Length - startOffset < expectedSize)
            throw new ArgumentException($"Buffer too small. Need at least {expectedSize} bytes.");

        var packet = new PlayerStatePacket();
        int offset = startOffset;

        packet.Header = PacketHeader.FromBytes(bytes, offset);
        offset += PacketHeader.Size;

        packet.PlayerId = BitConverter.ToUInt16(bytes, offset);
        offset += 2;

        packet.Tick = BitConverter.ToUInt32(bytes, offset);
        offset += 4;

        packet.PositionX = BitConverter.ToSingle(bytes, offset);
        offset += 4;
        packet.PositionY = BitConverter.ToSingle(bytes, offset);
        offset += 4;
        packet.PositionZ = BitConverter.ToSingle(bytes, offset);
        offset += 4;

        packet.RotationX = BitConverter.ToSingle(bytes, offset);
        offset += 4;
        packet.RotationY = BitConverter.ToSingle(bytes, offset);
        offset += 4;
        packet.RotationZ = BitConverter.ToSingle(bytes, offset);
        offset += 4;
        packet.RotationW = BitConverter.ToSingle(bytes, offset);
        offset += 4;

        packet.IsWalking = bytes[offset++];
        packet.IsRunning = bytes[offset++];
        packet.IsCrouching = bytes[offset++];
        packet.IsInVehicle = bytes[offset++];

        return packet;
    }

    public byte[] ToBytes()
    {
        byte[] bytes = new byte[PacketHeader.Size + 2 + 4 + 12 + 16 + 4];
        int offset = 0;

        // Header
        byte[] headerBytes = Header.ToBytes();
        Buffer.BlockCopy(headerBytes, 0, bytes, offset, PacketHeader.Size);
        offset += PacketHeader.Size;

        // PlayerId
        BitConverter.GetBytes(PlayerId).CopyTo(bytes, offset);
        offset += 2;

        // Tick
        BitConverter.GetBytes(Tick).CopyTo(bytes, offset);
        offset += 4;

        // Position
        BitConverter.GetBytes(PositionX).CopyTo(bytes, offset); offset += 4;
        BitConverter.GetBytes(PositionY).CopyTo(bytes, offset); offset += 4;
        BitConverter.GetBytes(PositionZ).CopyTo(bytes, offset); offset += 4;

        // Rotation
        BitConverter.GetBytes(RotationX).CopyTo(bytes, offset); offset += 4;
        BitConverter.GetBytes(RotationY).CopyTo(bytes, offset); offset += 4;
        BitConverter.GetBytes(RotationZ).CopyTo(bytes, offset); offset += 4;
        BitConverter.GetBytes(RotationW).CopyTo(bytes, offset); offset += 4;

        // Animation bytes
        bytes[offset++] = IsWalking;
        bytes[offset++] = IsRunning;
        bytes[offset++] = IsCrouching;
        bytes[offset++] = IsInVehicle;

        return bytes;
    }
}
