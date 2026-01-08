using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using MWCO.Client.Networking;
using MWCO.Shared;
using MWCO.Shared.Packets;

namespace MWCO.Client.Patches
{
    /// <summary>
    /// Patches PlayMaker FSM system to intercept game events
    /// This allows us to sync part assembly, interactions, and world changes
    /// </summary>
    [HarmonyPatch]
    public static class PlayMakerPatches
    {
        // Track FSM state changes
        [HarmonyPatch(typeof(Fsm), nameof(Fsm.Event), new[] { typeof(FsmEvent) })]
        [HarmonyPrefix]
        public static void Event_Prefix(Fsm __instance, FsmEvent fsmEvent)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
                return;

            // Check if this is a relevant event we need to sync
            string eventName = fsmEvent?.Name ?? "Unknown";
            string fsmName = __instance?.Name ?? "Unknown";
            GameObject owner = __instance?.GameObject;

            // Log significant events for debugging
            if (IsSignificantEvent(eventName, fsmName))
            {
                Debug.Log($"[MWCO] FSM Event: {fsmName}.{eventName} on {owner?.name}");

                // Handle specific event types
                HandleFSMEvent(fsmName, eventName, owner);
            }
        }

        // Track variable changes (for detecting part attachment, bolts tightened, etc.)
        [HarmonyPatch(typeof(FsmBool), nameof(FsmBool.Value), MethodType.Setter)]
        [HarmonyPrefix]
        public static void FsmBool_Set_Prefix(FsmBool __instance, bool value)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
                return;

            // Track boolean variables that indicate part states
            if (IsPartStateVariable(__instance.Name))
            {
                Debug.Log($"[MWCO] FSM Bool changed: {__instance.Name} = {value}");

                // Get owner FSM from variable's owner
                PlayMakerFSM[] fsms = UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>();
                foreach (var fsm in fsms)
                {
                    if (fsm.FsmVariables != null && fsm.gameObject != null)
                    {
                        PartSyncPacket packet = new PartSyncPacket(
                            0, // VehicleId - need to determine from context
                            (ushort)fsm.gameObject.GetInstanceID(),
                            fsm.gameObject.name,
                            value,
                            NetworkManager.Instance.CurrentTick
                        );

                        packet.LocalPosX = fsm.transform.localPosition.x;
                        packet.LocalPosY = fsm.transform.localPosition.y;
                        packet.LocalPosZ = fsm.transform.localPosition.z;

                        packet.LocalRotX = fsm.transform.localRotation.x;
                        packet.LocalRotY = fsm.transform.localRotation.y;
                        packet.LocalRotZ = fsm.transform.localRotation.z;
                        packet.LocalRotW = fsm.transform.localRotation.w;

                        NetworkManager.Instance.SendPacket(packet.ToBytes());
                        break; // Send only once
                    }
                }
            }
        }

        [HarmonyPatch(typeof(FsmFloat), nameof(FsmFloat.Value), MethodType.Setter)]
        [HarmonyPrefix]
        public static void FsmFloat_Set_Prefix(FsmFloat __instance, float value)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
                return;

            // Track float variables (damage, wear, fluid levels, etc.)
            if (IsDamageOrWearVariable(__instance.Name))
            {
                Debug.Log($"[MWCO] FSM Float changed: {__instance.Name} = {value}");

                // Send damage update using VehicleConfig packet
                VehicleConfigPacket packet = new VehicleConfigPacket(0, NetworkManager.Instance.CurrentTick);
                if (__instance.Name.ToLower().Contains("engine"))
                    packet.EngineDamage = value;
                else
                    packet.BodyDamage = value;
                NetworkManager.Instance.SendPacket(packet.ToBytes());
            }
        }

        private static bool IsSignificantEvent(string eventName, string fsmName)
        {
            // Events related to parts
            if (eventName.Contains("Bolt") || eventName.Contains("Screw"))
                return true;

            if (eventName.Contains("Attach") || eventName.Contains("Detach"))
                return true;

            if (eventName.Contains("Install") || eventName.Contains("Remove"))
                return true;

            // Events related to vehicle systems
            if (eventName.Contains("Engine") || eventName.Contains("Start") || eventName.Contains("Stop"))
                return true;

            if (eventName.Contains("Gear") || eventName.Contains("Shift"))
                return true;

            // Events related to interactions
            if (eventName.Contains("Use") || eventName.Contains("Interact"))
                return true;

            if (eventName.Contains("Pickup") || eventName.Contains("Drop"))
                return true;

            return false;
        }

        private static bool IsPartStateVariable(string varName)
        {
            if (string.IsNullOrEmpty(varName))
                return false;

            string lower = varName.ToLower();

            return lower.Contains("bolt") ||
                   lower.Contains("screw") ||
                   lower.Contains("attached") ||
                   lower.Contains("installed") ||
                   lower.Contains("assembled");
        }

        private static bool IsDamageOrWearVariable(string varName)
        {
            if (string.IsNullOrEmpty(varName))
                return false;

            string lower = varName.ToLower();

            return lower.Contains("damage") ||
                   lower.Contains("wear") ||
                   lower.Contains("health") ||
                   lower.Contains("condition") ||
                   lower.Contains("fluid") ||
                   lower.Contains("oil") ||
                   lower.Contains("fuel");
        }

        private static void HandleFSMEvent(string fsmName, string eventName, GameObject owner)
        {
            // Handle part assembly events
            if (eventName.Contains("Bolt") || eventName.Contains("Screw"))
            {
                HandlePartAssemblyEvent(fsmName, eventName, owner);
            }

            // Handle engine events
            else if (eventName.Contains("Engine") || eventName.Contains("Start"))
            {
                HandleEngineEvent(fsmName, eventName, owner);
            }

            // Handle gear shift events
            else if (eventName.Contains("Gear") || eventName.Contains("Shift"))
            {
                HandleGearEvent(fsmName, eventName, owner);
            }

            // Handle interaction events
            else if (eventName.Contains("Pickup") || eventName.Contains("Drop"))
            {
                HandlePickupDropEvent(fsmName, eventName, owner);
            }
        }

        private static void HandlePartAssemblyEvent(string fsmName, string eventName, GameObject owner)
        {
            Debug.Log($"[MWCO] Part assembly event: {eventName} on {owner?.name}");

            if (owner == null) return;

            // Detect if this is attach/detach based on event name
            bool isAttach = eventName.Contains("Attach") || eventName.Contains("Install") || eventName.Contains("Bolt");

            PartSyncPacket packet = new PartSyncPacket(
                0, // VehicleId
                (ushort)owner.GetInstanceID(),
                owner.name,
                isAttach,
                NetworkManager.Instance.CurrentTick
            );

            packet.LocalPosX = owner.transform.localPosition.x;
            packet.LocalPosY = owner.transform.localPosition.y;
            packet.LocalPosZ = owner.transform.localPosition.z;

            packet.LocalRotX = owner.transform.localRotation.x;
            packet.LocalRotY = owner.transform.localRotation.y;
            packet.LocalRotZ = owner.transform.localRotation.z;
            packet.LocalRotW = owner.transform.localRotation.w;

            // Try to count bolts from FSM variables
            PlayMakerFSM[] fsms = owner.GetComponents<PlayMakerFSM>();
            foreach (var fsm in fsms)
            {
                var boltVar = fsm.FsmVariables.FindFsmInt("Bolts");
                if (boltVar != null)
                {
                    packet.BoltCount = (byte)boltVar.Value;
                    break;
                }
            }

            NetworkManager.Instance.SendPacket(packet.ToBytes());
        }

        private static void HandleEngineEvent(string fsmName, string eventName, GameObject owner)
        {
            Debug.Log($"[MWCO] Engine event: {eventName}");
            // This should already be handled by DrivetrainPatches
        }

        private static void HandleGearEvent(string fsmName, string eventName, GameObject owner)
        {
            Debug.Log($"[MWCO] Gear event: {eventName}");
            // This should already be handled by DrivetrainPatches
        }

        private static void HandlePickupDropEvent(string fsmName, string eventName, GameObject owner)
        {
            Debug.Log($"[MWCO] Pickup/Drop event: {eventName} on {owner?.name}");

            if (owner == null) return;

            WorldObjectPacket packet = new WorldObjectPacket(
                (uint)owner.GetInstanceID(),
                owner.name,
                1, // ObjectType: physics object
                PacketType.WorldObjectUpdate,
                NetworkManager.Instance.CurrentTick
            );

            packet.PosX = owner.transform.position.x;
            packet.PosY = owner.transform.position.y;
            packet.PosZ = owner.transform.position.z;

            packet.RotX = owner.transform.rotation.x;
            packet.RotY = owner.transform.rotation.y;
            packet.RotZ = owner.transform.rotation.z;
            packet.RotW = owner.transform.rotation.w;

            Rigidbody rb = owner.GetComponent<Rigidbody>();
            if (rb != null)
            {
                packet.VelX = rb.velocity.x;
                packet.VelY = rb.velocity.y;
                packet.VelZ = rb.velocity.z;
            }

            NetworkManager.Instance.SendPacket(packet.ToBytes());
        }
    }

    /// <summary>
    /// Patches for specific PlayMaker actions used in MWC
    /// </summary>
    [HarmonyPatch]
    public static class PlayMakerActionPatches
    {
        // Hook into SetBoolValue action (used for bolts, screws, part states)
        // Note: We can't patch the action directly without knowing exact type names,
        // so we use FSM-level hooks instead (see above)
    }
}
