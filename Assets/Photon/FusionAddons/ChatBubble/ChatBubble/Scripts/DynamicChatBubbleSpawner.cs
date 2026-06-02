using Fusion.Addons.AudioRoomAddon;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.AudioChatBubble
{
    [RequireComponent(typeof(AudioRoomMember))]
#if PHOTON_VOICE_AVAILABLE
    public class DynamicChatBubbleSpawner : NetworkBehaviour, IDynamicChatBubbleSpawner
    {
       public DynamicChatBubbleManager manager;
        AudioRoomMember audioRoomMember;

        public float proximityDistance = 2;
        public float proximityDelayBeforeSpawn = 0.3f;
        public GameObject dynamicChatBubblePrefab;

        public Vector3 HearPosition => audioRoomMember.HearPosition;
        public Vector3 HeadGroundProjectionPosition
        {
            get
            {
                var projection = HearPosition;
                projection.y = audioRoomMember.audioGroupMember.rig.transform.position.y;
                return projection;
            }
        }

        public bool IsCurrentlyInroom => audioRoomMember.IsCurrentlyInroom;

        public PlayerRef Player => Object != null ? audioRoomMember.Object.StateAuthority : PlayerRef.None;
        Dictionary<IDynamicChatBubbleSpawner, float> spawnersProximityStarts = new Dictionary<IDynamicChatBubbleSpawner, float>();
        float recentDynamicChatbubblePotentialCreation = 0;

        private void Awake()
        {
            if (TryGetComponent<ChatBubbleLocomotionValidator>(out var chatBubbleValidator) && chatBubbleValidator.allowedToMoveToChatBubbles == false)
            {
                enabled = false;
                return;
            }
            if (manager == null) manager = FindAnyObjectByType<DynamicChatBubbleManager>();
            audioRoomMember = GetComponent<AudioRoomMember>();
            manager.RegisterSpawner(this);
            if (dynamicChatBubblePrefab.TryGetComponent<ChatBubble>(out var chatbubble) && chatbubble.roomShape == ChatBubble.RoomShape.Circle)
            {
                // We read the proximityDistance in the prefab radius (we set it a bit lower to avoid creating a bubble and missing people entering it: we create it when we are sure people are clearly in it)
                proximityDistance = chatbubble.radius * 0.9f;
            }
        }

        private void OnDestroy()
        {
            if (manager) manager.UnregisterSpawner(this);
        }

        public override void Render()
        {
            base.Render();
            if (enabled == false) return;
            CheckSpawnersInProximityValidity();
            CheckProximity();
            TriggerProximity();
            CheckRecentDynamicChatbubblePotentialCreation();
        }

        void CheckSpawnersInProximityValidity()
        {
            List<IDynamicChatBubbleSpawner> destroyedSpawners = new List<IDynamicChatBubbleSpawner>();
            foreach (var spawner in spawnersProximityStarts.Keys)
            {
                if (manager.chatBubbleSpawners.Contains(spawner) == false)
                {
                    destroyedSpawners.Add(spawner);
                }
            }
            foreach (var spawner in destroyedSpawners) spawnersProximityStarts.Remove(spawner);
        }

        void CheckRecentDynamicChatbubblePotentialCreation()
        {
            if (recentDynamicChatbubblePotentialCreation != 0)
            {
                if ((Time.time - recentDynamicChatbubblePotentialCreation) < 2)
                {
                    ForceRoomJoincheck();
                }
                else
                {
                    recentDynamicChatbubblePotentialCreation = 0;
                }
            }
        }

        void ForceRoomJoincheck()
        {
            RequestCheckRoomPresence();
            foreach (var spawn in spawnersProximityStarts.Keys)
            {
                spawn.RequestCheckRoomPresence();
            }
        }

        void TriggerProximity()
        {
            // We check as the local user
            if (Object.HasStateAuthority == false) return;

            if (spawnersProximityStarts.Count == 0) return;
            if (IsCurrentlyInroom)
            {
                Debug.LogError($"[{audioRoomMember.Object.Id}] In room, ignore prox chatbubble ");
                return;
            }
            // 1 - choose who is the spawner (and if we waited long enough to spawn)
            bool waitTimeElapsed = false;
            IDynamicChatBubbleSpawner choosenSpawner = this;
            foreach (var spawnerInfo in spawnersProximityStarts)
            {
                var spawner = spawnerInfo.Key;
                if (spawner == null) continue; // Destroyed player
                var proximityStart = spawnerInfo.Value;
                if ((Time.time - proximityStart) > proximityDelayBeforeSpawn)
                {
                    waitTimeElapsed = true;
                }
                if (spawner.Player.PlayerId < Player.PlayerId)
                {
                    // Only the lowest id player should spawn the bubble
                    choosenSpawner = spawner;
                }
            }

            if (waitTimeElapsed == false)
            {
                // Waiting time not finished
                return;
            }
            if (choosenSpawner != (IDynamicChatBubbleSpawner)this)
            {
                // Someone else will handle the dynamic bubble spawn
                recentDynamicChatbubblePotentialCreation = Time.time;
                return;
            }

            // 2 - spawn the chat bubble
            var spawnPosition = HeadGroundProjectionPosition;
            var roomId = manager.FreeChatBubbleId();
            //Debug.LogError($"[{audioRoomMember.Object.Id}] Spawning chatbubble " + roomId);

            var bubbleObj = Runner.Spawn(dynamicChatBubblePrefab, spawnPosition);
            var chatBubble = bubbleObj.GetComponent<DynamicChatBubble>();
            // 3 - set the chatbubble id (note network it)
            chatBubble.RoomId = roomId;
            // 4 - In case the player do not move (triggering proximity check), we force a check for a short duration
            recentDynamicChatbubblePotentialCreation = Time.time;
            // 5 - in chat bubble manager, check for how long a room has been empty)
            spawnersProximityStarts.Clear();
        }

        public void RequestCheckRoomPresence()
        {
            audioRoomMember.RequestCheckRoomPresence();
        }

        static int DistanceCount = 0;
        public int latestDistanceCount;
        float DistanceToSpawner(IDynamicChatBubbleSpawner spawner)
        {
            DistanceCount++;
            var d = Vector3.Distance(HearPosition, spawner.HearPosition);
            return d;
        }

        private void LateUpdate()
        {
            latestDistanceCount = DistanceCount;
            DistanceCount = 0;
        }

        void CheckProximity()
        {
            if (IsCurrentlyInroom)
            {
                if (spawnersProximityStarts.Count > 0)
                {
                    //Debug.LogError($"[{audioRoomMember.Object.Id}] In room: cleaning proximity");

                }
                spawnersProximityStarts.Clear();
                return;
            }

            foreach (var spawner in manager.chatBubbleSpawners)
            {
                if (spawner == (IDynamicChatBubbleSpawner)this) continue;
                if (spawner.IsCurrentlyInroom == false && DistanceToSpawner(spawner) < proximityDistance)
                {
                    if (spawnersProximityStarts.ContainsKey(spawner) == false)
                    {
                        //Debug.LogError($"[{audioRoomMember.Object.Id}] Proximity detected with {((DynamicChatBubbleSpawner)spawner).audioRoomMember.Object.Id}");
                        spawnersProximityStarts.Add(spawner, Time.time);
                    }
                    // These spawner are close to each other: we have to monitor if they should enter a room, even if they do not move anymore
                    spawner.RequestCheckRoomPresence();
                }
                else if (spawnersProximityStarts.ContainsKey(spawner))
                {
                    if (spawner.IsCurrentlyInroom)
                    {
                        // We check if the other player has just joined our new common bubble
                        audioRoomMember.CheckRoomPresence();
                    }
                    //Debug.LogError($"[{audioRoomMember.Object.Id}] Proximity stopped with {((DynamicChatBubbleSpawner)spawner).audioRoomMember.Object.Id}");
                    spawnersProximityStarts.Remove(spawner);
                }
            }
        }
    }
#else
    public class DynamicChatBubbleSpawner : NetworkBehaviour { }

#endif
}