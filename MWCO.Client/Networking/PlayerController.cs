using System;
using UnityEngine;
using MWCO.Shared;
using MWCO.Shared.Packets;

namespace MWCO.Client.Networking
{
    /// <summary>
    /// Manages local player state and sends updates to server
    /// Handles player position, rotation, animations, and interactions
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        private Transform playerTransform;
        private Animator playerAnimator;
        private NetworkManager networkManager;

        // Player state
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private float sendTimer = 0f;
        private int sendCount = 0;
        private const float SEND_INTERVAL = 0.02f; // 50Hz

        // Player identification
        private string playerName;

        public void Initialize(string name)
        {
            playerName = name;
            networkManager = NetworkManager.Instance;
            
            Debug.Log($"[MWCO] ========================================");
            Debug.Log($"[MWCO] PlayerController.Initialize() called!");
            Debug.Log($"[MWCO] Player name: {name}");
            Debug.Log($"[MWCO] NetworkManager.Instance: {(networkManager != null ? "OK" : "NULL")}");
            Debug.Log($"[MWCO] ========================================");

            // First, try to find a CharacterController - this is the actual player physics object
            var characterController = GameObject.FindObjectOfType<CharacterController>();
            if (characterController != null)
            {
                Debug.Log($"[MWCO] Found CharacterController on: {characterController.gameObject.name}");
                playerTransform = characterController.transform;
                lastPosition = playerTransform.position;
                lastRotation = playerTransform.rotation;
                Debug.Log($"[MWCO] Using CharacterController for player position: {lastPosition}");
                SendPlayerState();
                return;
            }

            // Try multiple common player object names
            string[] playerObjectNames = new string[]
            {
                "PLAYER",
                "Player",
                "FPSController",
                "FPSPlayer",
                "Character",
                "PlayerCharacter",
                "PlayerController"
            };

            GameObject playerObj = null;
            foreach (var objName in playerObjectNames)
            {
                playerObj = GameObject.Find(objName);
                if (playerObj != null)
                {
                    Debug.Log($"[MWCO] Found player object: {objName}");
                    break;
                }
            }

            // Fallback: use camera's parent or camera itself
            if (playerObj == null)
            {
                Debug.Log($"[MWCO] No named player object found. Camera.main = {(Camera.main != null ? Camera.main.name : "NULL")}");
                
                if (Camera.main != null)
                {
                    // Log the camera hierarchy
                    Debug.Log($"[MWCO] Camera hierarchy:");
                    var t = Camera.main.transform;
                    while (t != null)
                    {
                        Debug.Log($"[MWCO]   {t.name} at {t.position}");
                        t = t.parent;
                    }
                    
                    // Try camera's parent first (often the player)
                    if (Camera.main.transform.parent != null)
                    {
                        playerObj = Camera.main.transform.parent.gameObject;
                        Debug.Log($"[MWCO] Using camera parent as player: {playerObj.name}");
                    }
                    else
                    {
                        playerObj = Camera.main.gameObject;
                        Debug.Log($"[MWCO] Using main camera as player position source");
                    }
                }
                else
                {
                    // Camera.main is null - use THIS object as transform source
                    Debug.LogWarning("[MWCO] Camera.main is NULL! Using NetworkManager transform as fallback.");
                    playerObj = this.gameObject;
                }
            }

            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                playerAnimator = playerObj.GetComponent<Animator>();

                lastPosition = playerTransform.position;
                lastRotation = playerTransform.rotation;

                Debug.Log($"[MWCO] PlayerController initialized for {playerName} using {playerObj.name}");
                Debug.Log($"[MWCO] Initial player position: {lastPosition}");
                
                // Send first PlayerState immediately!
                Debug.Log($"[MWCO] Sending INITIAL PlayerState packet...");
                SendPlayerState();
            }
            else
            {
                Debug.LogError("[MWCO] Could not find player GameObject! Will retry in Update...");
            }
        }

        void Update()
        {
            if (networkManager == null || !networkManager.IsConnected)
            {
                return;
            }

            // Simple: just send camera position at 50Hz
            sendTimer += Time.deltaTime;

            if (sendTimer >= SEND_INTERVAL)
            {
                sendTimer = 0f;
                SendPlayerState();
            }
        }
        
        private int updateCount = 0;

        private void TryFindPlayer()
        {
            retryCount++;
            
            // Log every 50 attempts (1 second at 50Hz)
            bool shouldLog = retryCount % 50 == 0;
            
            // First, try to find a CharacterController
            var characterController = GameObject.FindObjectOfType<CharacterController>();
            if (characterController != null)
            {
                playerTransform = characterController.transform;
                lastPosition = playerTransform.position;
                lastRotation = playerTransform.rotation;
                Debug.Log($"[MWCO] Found CharacterController on: {characterController.gameObject.name} at {lastPosition}");
                return;
            }
            
            // Try camera as fallback
            if (Camera.main != null)
            {
                if (Camera.main.transform.parent != null)
                {
                    playerTransform = Camera.main.transform.parent;
                    Debug.Log($"[MWCO] Found player via camera parent: {playerTransform.name}");
                }
                else
                {
                    playerTransform = Camera.main.transform;
                    Debug.Log($"[MWCO] Using camera transform for player position");
                }
                lastPosition = playerTransform.position;
                lastRotation = playerTransform.rotation;
                Debug.Log($"[MWCO] Player position: {lastPosition}");
            }
            else if (shouldLog)
            {
                Debug.LogWarning($"[MWCO] Camera.main is NULL! Cannot find player. Attempt {retryCount}");
                
                // List all cameras as fallback
                var cameras = Camera.allCameras;
                Debug.Log($"[MWCO] Found {cameras.Length} cameras in scene");
                foreach (var cam in cameras)
                {
                    Debug.Log($"[MWCO] Camera: {cam.name}, tag: {cam.tag}, enabled: {cam.enabled}");
                }
            }
        }

        private int retryCount = 0;

        private void SendPlayerState()
        {
            // SIMPLE: Just use camera position directly
            if (Camera.main == null)
            {
                if (sendCount % 50 == 0)
                    Debug.LogWarning("[MWCO] PlayerController: Camera.main is NULL!");
                return;
            }

            try
            {
                PlayerStatePacket packet = new PlayerStatePacket(
                    networkManager.LocalPlayerId,
                    networkManager.CurrentTick
                );

                // Use camera position directly
                Vector3 camPos = Camera.main.transform.position;
                Quaternion camRot = Camera.main.transform.rotation;

                packet.PositionX = camPos.x;
                packet.PositionY = camPos.y;
                packet.PositionZ = camPos.z;

                packet.RotationX = camRot.x;
                packet.RotationY = camRot.y;
                packet.RotationZ = camRot.z;
                packet.RotationW = camRot.w;

                // Log BEFORE sending
                sendCount++;
                if (sendCount <= 10 || sendCount % 50 == 0)
                {
                    Debug.Log($"[MWCO] >>> SENDING PlayerState #{sendCount}: pos=({camPos.x:F1}, {camPos.y:F1}, {camPos.z:F1})");
                }

                // Use ToBytes() directly for reliable serialization
                networkManager.SendPacket(packet.ToBytes());

                lastPosition = camPos;
                lastRotation = camRot;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MWCO] Exception in SendPlayerState: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Handle player animations
        public void UpdateAnimation(string animationName, bool state)
        {
            if (playerAnimator != null)
            {
                playerAnimator.SetBool(animationName, state);
            }
        }

        // Handle player interactions
        public void OnPlayerInteract(GameObject interactedObject)
        {
            Debug.Log($"[MWCO] Player interacted with: {interactedObject.name}");

            // Send world object interaction packet
            WorldObjectPacket packet = new WorldObjectPacket(
                (uint)interactedObject.GetInstanceID(),
                interactedObject.name,
                1, // ObjectType: physics object
                PacketType.WorldObjectUpdate,
                networkManager.CurrentTick
            );

            packet.PosX = interactedObject.transform.position.x;
            packet.PosY = interactedObject.transform.position.y;
            packet.PosZ = interactedObject.transform.position.z;

            packet.RotX = interactedObject.transform.rotation.x;
            packet.RotY = interactedObject.transform.rotation.y;
            packet.RotZ = interactedObject.transform.rotation.z;
            packet.RotW = interactedObject.transform.rotation.w;

            Rigidbody rb = interactedObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                packet.VelX = rb.velocity.x;
                packet.VelY = rb.velocity.y;
                packet.VelZ = rb.velocity.z;
            }

            networkManager.SendPacket(packet.ToBytes());
        }
    }
}
