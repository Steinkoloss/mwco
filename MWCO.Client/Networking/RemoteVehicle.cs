using System;
using System.Collections.Generic;
using UnityEngine;
using MWCO.Shared.Packets;

namespace MWCO.Client.Networking
{
    /// <summary>
    /// Represents a remote player's vehicle
    /// Handles interpolation and visual representation
    /// </summary>
    public class RemoteVehicle : MonoBehaviour
    {
        public ushort VehicleId { get; private set; }
        public ushort OwnerId { get; private set; }

        // Visual components
        private GameObject vehicleModel;
        private GameObject[] wheelModels = new GameObject[4];

        // Interpolation
        private class StateSnapshot
        {
            public float timestamp;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angularVelocity;
            public float rpm;
            public sbyte gear;
        }

        private Queue<StateSnapshot> stateBuffer = new Queue<StateSnapshot>();
        private const int MAX_BUFFER_SIZE = 10;
        private const float INTERPOLATION_DELAY = 0.1f; // 100ms

        // Current state
        private Vector3 currentPosition;
        private Quaternion currentRotation;
        private Vector3 currentVelocity;
        private float currentRPM;
        private sbyte currentGear;

        // Wheel state
        private WheelData[] wheelData = new WheelData[4];

        // Config
        private float fuelLevel;
        private float[] tirePressures = new float[4];

        public void Initialize(VehicleSpawnPacket spawnPacket)
        {
            VehicleId = spawnPacket.VehicleId;
            OwnerId = spawnPacket.OwnedByPlayerId;

            // Set initial position
            currentPosition = new Vector3(spawnPacket.PosX, spawnPacket.PosY, spawnPacket.PosZ);
            currentRotation = new Quaternion(spawnPacket.RotX, spawnPacket.RotY, spawnPacket.RotZ, spawnPacket.RotW);
            transform.position = currentPosition;
            transform.rotation = currentRotation;

            currentRPM = spawnPacket.RPM;
            currentGear = spawnPacket.Gear;
            fuelLevel = spawnPacket.FuelLevel;

            // Create visual representation
            CreateVisuals(spawnPacket.VehicleModel);

            Debug.Log($"[MWCO] Remote vehicle {VehicleId} initialized");
        }
        
        public void InitializeFromState(VehicleStatePacket statePacket)
        {
            VehicleId = statePacket.VehicleId;
            OwnerId = 0; // Unknown owner when auto-spawning

            // Set initial position
            currentPosition = new Vector3(statePacket.PositionX, statePacket.PositionY, statePacket.PositionZ);
            currentRotation = new Quaternion(statePacket.RotationX, statePacket.RotationY, statePacket.RotationZ, statePacket.RotationW);
            transform.position = currentPosition;
            transform.rotation = currentRotation;

            currentRPM = statePacket.RPM;
            currentGear = statePacket.Gear;

            // Create visual representation - use a LARGE BRIGHT visual for testing
            CreateTestVisuals();

            Debug.Log($"[MWCO] Remote vehicle {VehicleId} auto-initialized from state at {currentPosition}");
        }
        
        private void CreateTestVisuals()
        {
            // Create a LARGE, BRIGHT cube so it's impossible to miss
            vehicleModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vehicleModel.transform.parent = transform;
            vehicleModel.transform.localPosition = Vector3.up * 2f; // 2m above ground
            vehicleModel.transform.localScale = new Vector3(4f, 4f, 4f); // 4m cube

            // Remove collider
            var collider = vehicleModel.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Set bright RED color - unmissable
            var renderer = vehicleModel.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null)
                {
                    renderer.material = new Material(shader);
                    renderer.material.color = Color.red; // BRIGHT RED
                }
            }

            Debug.Log($"[MWCO] Created TEST visuals (RED 4m cube) for remote vehicle {VehicleId}");
        }

        private void CreateVisuals(string modelName)
        {
            // Create a simple cube as placeholder
            // TODO: Load actual car model or clone from game
            vehicleModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vehicleModel.transform.parent = transform;
            vehicleModel.transform.localPosition = Vector3.zero;
            vehicleModel.transform.localScale = new Vector3(2f, 1f, 4f);

            // Set color to distinguish from local vehicle
            var renderer = vehicleModel.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.5f, 0.7f, 1.0f); // Light blue
            }

            // Create wheel placeholders
            CreateWheelVisuals();

            Debug.Log($"[MWCO] Created visuals for remote vehicle {VehicleId}");
        }

        private void CreateWheelVisuals()
        {
            // Create 4 wheel cylinders
            Vector3[] wheelPositions = new Vector3[]
            {
                new Vector3(-0.8f, -0.5f, 1.5f),   // Front left
                new Vector3(0.8f, -0.5f, 1.5f),    // Front right
                new Vector3(-0.8f, -0.5f, -1.5f),  // Rear left
                new Vector3(0.8f, -0.5f, -1.5f)    // Rear right
            };

            for (int i = 0; i < 4; i++)
            {
                wheelModels[i] = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelModels[i].transform.parent = transform;
                wheelModels[i].transform.localPosition = wheelPositions[i];
                wheelModels[i].transform.localRotation = Quaternion.Euler(0, 0, 90);
                wheelModels[i].transform.localScale = new Vector3(0.6f, 0.2f, 0.6f);

                var renderer = wheelModels[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.black;
                }
            }
        }

        void Update()
        {
            // Perform interpolation
            InterpolateState();

            // Update visual elements
            UpdateVisuals();
        }

        public void UpdateState(VehicleStatePacket packet)
        {
            // Verbose logging removed - enable for debugging
            // if (Time.frameCount % 300 == 0)
            // {
            //     Debug.Log($"[MWCO] RemoteVehicle {VehicleId} received state: pos=({packet.PositionX:F1}, {packet.PositionY:F1}, {packet.PositionZ:F1})");
            // }
            
            // Add state to buffer
            var snapshot = new StateSnapshot
            {
                timestamp = Time.time,
                position = new Vector3(packet.PositionX, packet.PositionY, packet.PositionZ),
                rotation = new Quaternion(packet.RotationX, packet.RotationY, packet.RotationZ, packet.RotationW),
                velocity = new Vector3(packet.VelocityX, packet.VelocityY, packet.VelocityZ),
                angularVelocity = new Vector3(packet.AngularVelocityX, packet.AngularVelocityY, packet.AngularVelocityZ),
                rpm = packet.RPM,
                gear = packet.Gear
            };

            stateBuffer.Enqueue(snapshot);

            // Limit buffer size
            while (stateBuffer.Count > MAX_BUFFER_SIZE)
            {
                stateBuffer.Dequeue();
            }
        }

        public void UpdateWheelState(WheelStatePacket packet)
        {
            wheelData[0] = packet.FrontLeft;
            wheelData[1] = packet.FrontRight;
            wheelData[2] = packet.RearLeft;
            wheelData[3] = packet.RearRight;
        }

        public void UpdateConfig(VehicleConfigPacket packet)
        {
            fuelLevel = packet.FuelLevel;
            tirePressures[0] = packet.TirePressureFL;
            tirePressures[1] = packet.TirePressureFR;
            tirePressures[2] = packet.TirePressureRL;
            tirePressures[3] = packet.TirePressureRR;
        }

        public void HandleEvent(VehicleEventPacket packet)
        {
            switch (packet.Header.PacketType)
            {
                case MWCO.Shared.PacketType.GearChange:
                    currentGear = GearChangeEvent.GetGear(packet);
                    Debug.Log($"[MWCO] Vehicle {VehicleId} changed to gear {currentGear}");
                    break;

                case MWCO.Shared.PacketType.EngineStart:
                    Debug.Log($"[MWCO] Vehicle {VehicleId} started engine");
                    // TODO: Play engine start sound
                    break;

                case MWCO.Shared.PacketType.EngineStop:
                    Debug.Log($"[MWCO] Vehicle {VehicleId} stopped engine");
                    break;

                case MWCO.Shared.PacketType.HornTrigger:
                    if (packet.EventData == 1)
                    {
                        // TODO: Play horn sound
                        Debug.Log($"[MWCO] Vehicle {VehicleId} honked");
                    }
                    break;
            }
        }

        public void UpdatePart(PartSyncPacket packet)
        {
            Debug.Log($"[MWCO] Vehicle {VehicleId} part update: {packet.PartName} - {(packet.Attached == 1 ? "attached" : "detached")}");
            // TODO: Actually sync parts
        }

        private void InterpolateState()
        {
            if (stateBuffer.Count < 2)
            {
                // Not enough data for interpolation, use extrapolation or last known state
                if (stateBuffer.Count == 1)
                {
                    var state = stateBuffer.Peek();
                    currentPosition = state.position;
                    currentRotation = state.rotation;
                    currentRPM = state.rpm;
                    currentGear = state.gear;
                }
                return;
            }

            // Calculate render time (current time - interpolation delay)
            float renderTime = Time.time - INTERPOLATION_DELAY;

            // Find two states to interpolate between
            StateSnapshot from = null;
            StateSnapshot to = null;

            foreach (var state in stateBuffer)
            {
                if (state.timestamp <= renderTime)
                {
                    from = state;
                }
                else if (to == null)
                {
                    to = state;
                    break;
                }
            }

            if (from != null && to != null)
            {
                // Interpolate
                float t = (renderTime - from.timestamp) / (to.timestamp - from.timestamp);
                t = Mathf.Clamp01(t);

                currentPosition = Vector3.Lerp(from.position, to.position, t);
                currentRotation = Quaternion.Slerp(from.rotation, to.rotation, t);
                currentVelocity = Vector3.Lerp(from.velocity, to.velocity, t);
                currentRPM = Mathf.Lerp(from.rpm, to.rpm, t);

                // Remove old states
                while (stateBuffer.Count > 0 && stateBuffer.Peek().timestamp < renderTime - 1.0f)
                {
                    stateBuffer.Dequeue();
                }
            }
            else if (from != null)
            {
                // Extrapolate using last known velocity
                float dt = renderTime - from.timestamp;
                currentPosition = from.position + from.velocity * dt;
                currentRotation = from.rotation;
                currentRPM = from.rpm;
            }
        }

        private void UpdateVisuals()
        {
            // Update transform
            transform.position = currentPosition;
            transform.rotation = currentRotation;

            // Update wheel rotations based on wheel data
            for (int i = 0; i < 4; i++)
            {
                if (wheelModels[i] != null)
                {
                    // Rotate wheel based on angular velocity
                    float rotation = wheelData[i].AngularVelocity * Time.deltaTime * Mathf.Rad2Deg;
                    wheelModels[i].transform.Rotate(Vector3.forward, rotation);

                    // Apply steering to front wheels
                    if (i < 2) // Front wheels
                    {
                        wheelModels[i].transform.localRotation =
                            Quaternion.Euler(0, wheelData[i].SteeringAngle * Mathf.Rad2Deg, 90);
                    }
                }
            }
        }

        void OnDestroy()
        {
            Debug.Log($"[MWCO] Remote vehicle {VehicleId} destroyed");
        }
    }
}
