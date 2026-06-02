using Fusion;
using UnityEngine;

namespace Fusion.XR.Shared
{
    public class NPCSpawner : MonoBehaviour
    {
        [Header("NPC Prefab (must be registered in Fusion Prefab Table)")]
        public NetworkPrefabRef npcPrefab;

        [Header("Spawn Settings")]
        public Transform spawnPoint;

        private NetworkObject _spawnedNpc;

        public void TrySpawnNPC(NetworkRunner runner)
        {
            Debug.Log("[NPCSpawner] TrySpawnNPC CALLED");
            if (_spawnedNpc != null) return;
            if (!npcPrefab.IsValid)
            {
                Debug.LogError("[NPCSpawner] npcPrefab is not assigned/valid.");
                return;
            }

            // Spawn only on server in ClientServer topology.
            // In Shared topology, you usually still pick a single authority.
            if (runner.Topology == Topologies.ClientServer)
            {
                //if (!runner.IsServer) return;
            }
            else
            {
                // Shared: allow only StateAuthority to spawn (safe choice)
                // If this never spawns in your setup, we can switch to runner.IsSharedModeMasterClient if you use that pattern.
                if (runner.IsServer == false && runner.GameMode != GameMode.Single)
                {
                    // In many shared setups runner.IsServer is false for clients, so only one instance should call this.
                    // If you run into "no NPC in shared", tell me your Fusion mode and I’ll adjust.
                }
            }

            Vector3 pos = spawnPoint ? spawnPoint.position : Vector3.zero;
            Quaternion rot = spawnPoint ? spawnPoint.rotation : Quaternion.identity;

            _spawnedNpc = runner.Spawn(npcPrefab, pos, rot, inputAuthority: null);
            Debug.Log("[NPCSpawner] NPC spawned (networked).");
            Debug.Log("[NPCSpawner] SPAWNED OK");
        }
    }
}
