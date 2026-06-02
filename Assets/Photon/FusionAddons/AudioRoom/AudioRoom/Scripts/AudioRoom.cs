using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.AudioRoomAddon
{
    /***
     * 
     * Audio rooms are defined by the `AudioRoom` component.
     * Each `AudioRoom` has a specific `Id` and registers itself on the `AudioRoomManager` during the `Awake()`.
     * The `IsIsolated` bool is used to define whether players located into the room are isolated from others.
     * When the door move, the `Isolate()` methods is called (by `AudioDoor`) and checks if a player is located into the room to ask him to update the audio group filtering thanks to `OnCurrentRoomIsolationChange()` method.
     * 
     ***/
#if PHOTON_VOICE_AVAILABLE
    public class AudioRoom : MonoBehaviour, IAudioRoom
    {

        public List<IAudioRoomMember> allMembers;
        public AudioRoomManager audioRoomManager;
        public int roomId;
        public bool isIsolated = false;
        public Transform roomReference;
        public Vector3 roomSize = new Vector3(2, 2, 2);

        public int RoomId => roomId;
        public bool IsIsolated => isIsolated;

        void OnDrawGizmosSelected()
        {
            var r = roomReference;
            if (roomReference == null) r = transform;
            Gizmos.color = new Color(1, 0, 0, 0.2f);
            Gizmos.DrawCube(r.position, roomSize);
        }

        private void Awake()
        {
            if (audioRoomManager == null) audioRoomManager = FindAnyObjectByType<AudioRoomManager>(FindObjectsInactive.Include);
            audioRoomManager.RegisterAudioRoom(this);
            if (roomReference == null) roomReference = transform;
        }

        private void OnDestroy()
        {
            if (audioRoomManager)
                audioRoomManager.UnregisterAudioRoom(this);
        }

        // Inform players located into the room about the isolation status
        public void Isolate(bool isIsolated)
        {
            this.isIsolated = isIsolated;
            if (!audioRoomManager) return;
            foreach (var member in audioRoomManager.audioRoomMembers)
            {
                if (IsInRoom(member))
                {
                    member.OnCurrentRoomIsolationChange(this);
                }
            }
        }

        // Check if a position (player's head) is located into the room box
        public bool IsInRoom(Vector3 position)
        {
            var localPos = roomReference.InverseTransformPoint(position);
            if (Mathf.Abs(localPos.x) > roomSize.x / 2f) return false;
            if (Mathf.Abs(localPos.y) > roomSize.y / 2f) return false;
            if (Mathf.Abs(localPos.z) > roomSize.z / 2f) return false;
            return true;
        }

        // Check if a audio room member is in the room
        bool IsInRoom(IAudioRoomMember member)
        {
            return IsInRoom(member.HearPosition);
        }
    }
#else
    public class AudioRoom : MonoBehaviour { }
#endif
}
