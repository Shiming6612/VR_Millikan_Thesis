using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.AudioRoomAddon
{
    public interface IAudioRoomMember
    {
        public Vector3 HearPosition { get; }
        public void OnCurrentRoomIsolationChange(IAudioRoom room);
    }

    public interface IAudioRoom
    {
        public bool IsInRoom(Vector3 position);
        public int RoomId { get; }
        public bool IsIsolated { get; }
    }

    public interface IAudioRoomAccessValidation
    {
        public bool AcceptMoreMembers { get; }
    }

    public interface IAudioRoomListener
    {
        public void OnIsInRoom(IAudioRoomMember member, IAudioRoom room);
    }

    /***
     * 
     * AudioRoomManager manages the list of rooms, doors & players
     * MatchingRooms returns the list of rooms matching with a specific position
     * 
     ***/
    public class AudioRoomManager : MonoBehaviour
    {
#if PHOTON_VOICE_AVAILABLE
        public List<IAudioRoomMember> audioRoomMembers = new List<IAudioRoomMember>();
        public List<IAudioRoom> audioRooms = new List<IAudioRoom>();

#if UNITY_EDITOR
        public int roomsCount = 0;

        private void Update()
        {
            roomsCount = audioRooms.Count;
        }
#endif

        // return the list of rooms matching with a specific position
        public List<IAudioRoom> MatchingRooms(Vector3 position)
        {
            var rooms = new List<IAudioRoom>();
            foreach (var room in audioRooms)
            {
                if (room.IsInRoom(position))
                {
                    rooms.Add(room);
                }
            }
            return rooms;
        }

        public void RegisterAudioRoom(IAudioRoom audioRoom)
        {
            if (audioRooms.Contains(audioRoom)) return;
            audioRooms.Add(audioRoom);
        }

        public void UnregisterAudioRoom(IAudioRoom audioRoom)
        {
            if (audioRooms.Contains(audioRoom) == false) return;
            audioRooms.Remove(audioRoom);
        }

        internal void RegisterAudioRoomMember(AudioRoomMember audioRoomMember)
        {
            if (audioRoomMembers.Contains(audioRoomMember)) return;
            audioRoomMembers.Add(audioRoomMember);
        }

        internal void UnregisterAudioRoomMember(AudioRoomMember audioRoomMember)
        {
            if (audioRoomMembers.Contains(audioRoomMember) == false) return;
            audioRoomMembers.Remove(audioRoomMember);
        }
#endif
    }
}
