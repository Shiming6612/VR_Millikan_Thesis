using Fusion.Addons.DynamicAudioGroup;
using Fusion.XR.Shared.Locomotion;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.AudioRoomAddon
{
    /***
     * 
     * AudioRoomMember register each user's network rig on the AudioRoomManager
     * At each move, thanks to the `DidMove()` callback, `AudioRoomMember` checks whether the user has moved to another room.
     * Then, it set the audio group according the room status (isolated or not).
     * OnCurrentRoomIsolationChange() method is used by the AudioRoom to inform the player to update the audio group filtering (DynamicAudioGroupMember)
     * 
     * When leaving a room, the group filtering is either reset to the default value (we can listen to anybody) if set to OutOfRoomFilterMode.NoFilter
     *  or to OutOfRoomFilterMode.NeverMatchingGroupFilter, making the user unable to listen to anybody.
     *  
     * If the member or the room contains child implementing IAudioRoomListener, they will receive the OnIsInRoom callback when a player enter or leave the room.
     ***/
    [RequireComponent(typeof(DynamicAudioGroupMember))]
#if PHOTON_VOICE_AVAILABLE
    public class AudioRoomMember : NetworkBehaviour, IAudioRoomMember, ILocomotionObserver
    {
        public DynamicAudioGroupMember audioGroupMember;
        public AudioRoomManager audioRoomManager;
        public Vector3 HearPosition => audioGroupMember.rig.Headset.transform.position;
        public IAudioRoom currentRoom;
        bool currentIsolation = false;
        bool isInRoom = false;

        bool checkPresenceRequested = false;

        public bool IsCurrentlyInroom => currentRoom != null;


        public enum OutOfRoomFilterMode
        {
            NoFilter,
            NeverMatchingGroupFilter
        }
        [Header("Default out of room group filter")]
        public OutOfRoomFilterMode outOfRoomFilterMode = OutOfRoomFilterMode.NoFilter;

        List<IAudioRoomListener> memberListeners = new List<IAudioRoomListener>();
        List<IAudioRoomListener> roomListeners = new List<IAudioRoomListener>();

        private void Awake()
        {
            audioGroupMember = GetComponent<DynamicAudioGroupMember>();
            if (audioRoomManager == null) audioRoomManager = FindAnyObjectByType<AudioRoomManager>(FindObjectsInactive.Include);
            if (audioRoomManager == null) Debug.LogError("Audio room manager missing");
            else
                audioRoomManager.RegisterAudioRoomMember(this);
            memberListeners = new List<IAudioRoomListener>(GetComponentsInChildren<IAudioRoomListener>());
            ApplyOutOfRoomGroupFilter();
        }

        private void OnDestroy()
        {
            TryRoomChange(null);
            if (audioRoomManager)
                audioRoomManager.UnregisterAudioRoomMember(this); 
        }

        public void OnCurrentRoomIsolationChange(IAudioRoom room)
        {
            // Inform the player that the room isolation changed
            TryRoomChange(room);
        }

        bool TryRoomChange(IAudioRoom room)
        {
            // Do nothing if the player is already in the room and if the room status has not change
            if (room == currentRoom && (room == null || room.IsIsolated == currentIsolation)) return true;

            if (room is IAudioRoomAccessValidation validator && validator.AcceptMoreMembers == false)
            {
                return false;
            }

            // save the new room & status
            RoomChange(room);
            return true;
        }

        protected virtual void RoomChange(IAudioRoom room)
        {
            if (room == currentRoom && (room == null || room.IsIsolated == currentIsolation)) return;
            currentRoom = room;
            List<IAudioRoomListener> previousRoomListeners = new List<IAudioRoomListener>(roomListeners);

            if (room != null)
            {
                currentIsolation = room.IsIsolated;
                isInRoom = true;
                if (room is MonoBehaviour m)
                {
                    roomListeners = new List<IAudioRoomListener>(m.GetComponentsInChildren<IAudioRoomListener>());
                }
            }
            else
            {
                roomListeners.Clear();
            }

            // Update the DynamicAudioGroupMember according to the new room status
            if (room != null && room.IsIsolated)
            {
                AdditionalGroupFilterChange(room.RoomId);
            }
            else
            {
                ApplyOutOfRoomGroupFilter();
            }

            foreach (var l in memberListeners) l.OnIsInRoom(member: this, room: room);
            foreach (var l in roomListeners) l.OnIsInRoom(member: this, room: room);
            foreach (var l in previousRoomListeners) l.OnIsInRoom(member: this, room: room);
        }

        void ApplyOutOfRoomGroupFilter()
        {
            if (outOfRoomFilterMode == OutOfRoomFilterMode.NeverMatchingGroupFilter)
            {
                AdditionalGroupFilterChange(DynamicAudioGroupMember.NEVER_MATCHING_GROUP_FILTER);
            }
            else
            {
                AdditionalGroupFilterChange(default);
            }
        }

        void AdditionalGroupFilterChange(int additionalGroupFilter)
        {
            audioGroupMember.additionalFilter = additionalGroupFilter;
        }

        public void OnDidMove()
        {
            CheckRoomPresence();
        }

        public void RequestCheckRoomPresence()
        {
            checkPresenceRequested = true;
        }

        public void CheckRoomPresence() 
        { 
            if (!audioRoomManager) return;

            bool roomFound = false;

            if (currentRoom != null && currentRoom.IsInRoom(HearPosition) && currentIsolation == currentRoom.IsIsolated)
            {
                // Still present in the current room (and its isolation mode did not changed)
                roomFound = true;
            }
            else
            {
                // Check if the player is in a room
                foreach (var room in audioRoomManager.MatchingRooms(HearPosition))
                {
                    if (TryRoomChange(room))
                    {
                        roomFound = true;
                        break;

                    }
                }
            }

            if(roomFound == false && currentRoom != null)
            {
                TryRoomChange(null);
            }
            checkPresenceRequested = false;
        }

        public override void Spawned()
        {
            base.Spawned();
            // Check initial state
            OnDidMove();
        }

        public override void Render()
        {
            base.Render();
            if (isInRoom && (currentRoom == null || audioRoomManager.audioRooms.Contains(currentRoom) == false))
            {
                // Room destroyed
                TryRoomChange(null);
            }
            if (currentRoom != null && currentRoom.IsIsolated != currentIsolation)
            {
                // room isolation changed
                TryRoomChange(currentRoom);
            }

            if (checkPresenceRequested)
            {
                CheckRoomPresence();
            }
        }

        public void OnDidMoveFadeFinished() { }
    }
#else
    public class AudioRoomMember : NetworkBehaviour  { }
#endif
    }
