using System;
using UnityEngine;
using MWCO.Shared.Packets;
using BepInEx.Logging;

namespace MWCO.Client.Networking
{
    /// <summary>
    /// Controls the local player's vehicle
    /// Captures input and physics state to send to server
    /// </summary>
    public class LocalVehicleController : MonoBehaviour
    {
        public static ManualLogSource Logger { get; set; }
        
        public ushort VehicleId { get; private set; }
        public bool IsReady { get; private set; }

        // References to game components
        private CarController carController;
        private CarDynamics carDynamics;
        private Drivetrain drivetrain;
        private Axles axles;
        private Rigidbody rigidbody;
        private Transform vehicleTransform;

        private const string VERSION = "v4-BEPINEX-LOG";
        
        private void Log(string msg)
        {
            Debug.Log(msg);
            Logger?.LogInfo(msg);
        }

        public void Initialize(ushort vehicleId)
        {
            VehicleId = vehicleId;
            Log($"[MWCO] *** LocalVehicleController {VERSION} *** initialized for vehicle {vehicleId}");

            // Find the player's car
            FindPlayerVehicle();
        }

        void Update()
        {
            // Check if we lost our vehicle reference (scene change)
            if (IsReady && (vehicleTransform == null || carController == null))
            {
                Log("[MWCO] Lost vehicle reference (scene change?), searching again...");
                IsReady = false;
            }
            
            // Re-check periodically if player might have changed cars
            if (IsReady && Time.frameCount % 60 == 0) // Check every ~1 second
            {
                // If the attached car's controller is disabled, player got out - search again
                var axisController = carController as AxisCarController;
                if (axisController != null && !axisController.enabled)
                {
                    Log("[MWCO] Player exited vehicle, searching for new one...");
                    IsReady = false;
                }
            }
            
            if (!IsReady)
            {
                // Keep trying to find the vehicle - more frequently
                if (Time.frameCount % 30 == 0) // Check every 30 frames (~0.5 sec)
                {
                    FindPlayerVehicle();
                }
            }
        }

        private void FindPlayerVehicle()
        {
            Log("[MWCO] Searching for player vehicle...");
            
            // Best method: Find AxisCarController that's enabled (player is driving)
            var axisControllers = FindObjectsOfType<AxisCarController>();
            Log($"[MWCO] Found {axisControllers.Length} AxisCarController objects");
            
            foreach (var controller in axisControllers)
            {
                // Check if this controller is enabled (player is in this car)
                if (controller.enabled)
                {
                    Log($"[MWCO] Found ENABLED AxisCarController: {controller.gameObject.name}");
                    AttachToVehicle(controller.gameObject);
                    return;
                }
            }
            
            // Second method: Check if camera is parented to a car
            if (Camera.main != null)
            {
                Transform t = Camera.main.transform;
                while (t != null)
                {
                    var carDyn = t.GetComponent<CarDynamics>();
                    if (carDyn != null)
                    {
                        Log($"[MWCO] Found car via camera parent: {t.gameObject.name}");
                        AttachToVehicle(t.gameObject);
                        return;
                    }
                    t = t.parent;
                }
            }
            
            // Third method: Find any car with input being applied
            var carDynamicsObjects = FindObjectsOfType<CarDynamics>();
            Log($"[MWCO] Found {carDynamicsObjects.Length} CarDynamics objects");
            
            foreach (var cd in carDynamicsObjects)
            {
                var controller = cd.GetComponent<CarController>();
                if (controller != null)
                {
                    // Check if this car has throttle/steering input (player is driving it)
                    if (Mathf.Abs(controller.throttle) > 0.01f || Mathf.Abs(controller.steering) > 0.01f || controller.brake > 0.01f)
                    {
                        Log($"[MWCO] Found car with active input: {cd.gameObject.name} (throttle={controller.throttle}, steer={controller.steering})");
                        AttachToVehicle(cd.gameObject);
                        return;
                    }
                }
            }

            Log("[MWCO] Player vehicle not found yet - player may not be in a car");
        }

        private void AttachToVehicle(GameObject vehicle)
        {
            vehicleTransform = vehicle.transform;
            rigidbody = vehicle.GetComponent<Rigidbody>();
            carController = vehicle.GetComponent<CarController>();
            carDynamics = vehicle.GetComponent<CarDynamics>();
            drivetrain = vehicle.GetComponent<Drivetrain>();
            axles = vehicle.GetComponent<Axles>();

            if (carController == null || carDynamics == null || drivetrain == null)
            {
                Log("[MWCO] Failed to get required components from vehicle!");
                return;
            }

            IsReady = true;
            Log($"[MWCO] Attached to player vehicle: {vehicle.name}");
        }

        public VehicleInputPacket GetInputPacket(uint tick)
        {
            if (!IsReady)
                return new VehicleInputPacket(VehicleId, tick);

            var packet = new VehicleInputPacket(VehicleId, tick)
            {
                SteerInput = carController.steerInput,
                ThrottleInput = carController.throttleInput,
                BrakeInput = carController.brakeInput,
                HandbrakeInput = carController.handbrakeInput,
                ClutchInput = carController.clutchInput,
                TargetGear = (sbyte)drivetrain.gear
            };

            // Set input flags
            packet.SetFlag(VehicleInputPacket.FLAG_START_ENGINE, carController.startEngineInput);

            return packet;
        }

        public VehicleStatePacket GetStatePacket(uint tick)
        {
            var packet = new VehicleStatePacket(VehicleId, tick);

            // SIMPLE APPROACH: Just use camera position - it follows the player everywhere
            if (Camera.main != null)
            {
                Vector3 camPos = Camera.main.transform.position;
                Quaternion camRot = Camera.main.transform.rotation;
                
                packet.PositionX = camPos.x;
                packet.PositionY = camPos.y;
                packet.PositionZ = camPos.z;

                packet.RotationX = camRot.x;
                packet.RotationY = camRot.y;
                packet.RotationZ = camRot.z;
                packet.RotationW = camRot.w;
                
                // Log every ~2 seconds
                if (tick % 100 == 0)
                {
                    Log($"[MWCO] Sending camera pos: ({camPos.x:F1}, {camPos.y:F1}, {camPos.z:F1})");
                }
            }
            else
            {
                if (tick % 100 == 0)
                {
                    Log("[MWCO] Camera.main is NULL!");
                }
            }

            if (rigidbody != null)
            {
                packet.VelocityX = rigidbody.velocity.x;
                packet.VelocityY = rigidbody.velocity.y;
                packet.VelocityZ = rigidbody.velocity.z;

                packet.AngularVelocityX = rigidbody.angularVelocity.x;
                packet.AngularVelocityY = rigidbody.angularVelocity.y;
                packet.AngularVelocityZ = rigidbody.angularVelocity.z;
            }

            // Engine state
            packet.RPM = drivetrain.rpm;
            packet.Gear = (sbyte)drivetrain.gear;
            packet.EngineRunning = (byte)(drivetrain.rpm > drivetrain.minRPM ? 1 : 0);

            // Current processed inputs
            packet.Steering = carController.steering;
            packet.Throttle = carController.throttle;
            packet.Brake = carController.brake;

            return packet;
        }

        public WheelStatePacket GetWheelStatePacket(uint tick)
        {
            if (!IsReady || axles == null)
                return new WheelStatePacket(VehicleId, tick);

            var packet = new WheelStatePacket(VehicleId, tick);

            // Front left
            if (axles.frontAxle.leftWheel != null)
            {
                packet.FrontLeft = GetWheelData(axles.frontAxle.leftWheel);
            }

            // Front right
            if (axles.frontAxle.rightWheel != null)
            {
                packet.FrontRight = GetWheelData(axles.frontAxle.rightWheel);
            }

            // Rear left
            if (axles.rearAxle.leftWheel != null)
            {
                packet.RearLeft = GetWheelData(axles.rearAxle.leftWheel);
            }

            // Rear right
            if (axles.rearAxle.rightWheel != null)
            {
                packet.RearRight = GetWheelData(axles.rearAxle.rightWheel);
            }

            return packet;
        }

        private WheelData GetWheelData(Wheel wheel)
        {
            return new WheelData
            {
                AngularVelocity = wheel.angularVelocity,
                Compression = wheel.compression,
                SteeringAngle = wheel.steering,
                OnGround = (byte)(wheel.onGroundDown ? 1 : 0),
                TirePuncture = (byte)(wheel.tirePuncture ? 1 : 0)
            };
        }

        public VehicleConfigPacket GetConfigPacket(uint tick)
        {
            if (!IsReady)
                return new VehicleConfigPacket(VehicleId, tick);

            var packet = new VehicleConfigPacket(VehicleId, tick);

            // Fuel
            if (drivetrain.fuelTanks != null && drivetrain.fuelTanks.Length > 0)
            {
                packet.FuelLevel = drivetrain.fuelTanks[0].currentFuel;
            }
            packet.FuelConsumption = drivetrain.currentConsumption;

            // Tire pressures
            if (axles != null)
            {
                if (axles.frontAxle.leftWheel != null)
                    packet.TirePressureFL = axles.frontAxle.leftWheel.pressure;
                if (axles.frontAxle.rightWheel != null)
                    packet.TirePressureFR = axles.frontAxle.rightWheel.pressure;
                if (axles.rearAxle.leftWheel != null)
                    packet.TirePressureRL = axles.rearAxle.leftWheel.pressure;
                if (axles.rearAxle.rightWheel != null)
                    packet.TirePressureRR = axles.rearAxle.rightWheel.pressure;
            }

            // TODO: Get actual damage values
            packet.EngineDamage = 0f;
            packet.BodyDamage = 0f;

            return packet;
        }
        
        // HACK: Get player state from camera position since PlayerController isn't working
        public PlayerStatePacket GetPlayerStatePacket(ushort playerId, uint tick)
        {
            var packet = new PlayerStatePacket(playerId, tick);
            
            // Use camera position as player position
            if (Camera.main != null)
            {
                Transform playerT = Camera.main.transform;
                if (Camera.main.transform.parent != null)
                {
                    playerT = Camera.main.transform.parent;
                }
                
                packet.PositionX = playerT.position.x;
                packet.PositionY = playerT.position.y;
                packet.PositionZ = playerT.position.z;
                
                packet.RotationX = playerT.rotation.x;
                packet.RotationY = playerT.rotation.y;
                packet.RotationZ = playerT.rotation.z;
                packet.RotationW = playerT.rotation.w;
            }
            else
            {
                // No camera - use vehicle position
                if (vehicleTransform != null)
                {
                    packet.PositionX = vehicleTransform.position.x;
                    packet.PositionY = vehicleTransform.position.y + 1.5f; // Offset up
                    packet.PositionZ = vehicleTransform.position.z;
                }
            }
            
            return packet;
        }
    }
}
