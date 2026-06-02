using Fusion.Addons.AudioRoomAddon;
using Fusion.XR.Shared.Locomotion;
using UnityEngine;


namespace Fusion.Addons.AudioChatBubble
{
    [RequireComponent(typeof(AudioRoomMember))]
    public class ChatBubbleLocomotionValidator : MonoBehaviour, ILocomotionValidator
    {
        AudioRoomMember audioRoomMember;
        public bool allowedToMoveToChatBubbles = true;

        private void Awake()
        {
            audioRoomMember = GetComponent<AudioRoomMember>();
        }

        #region ILocomotionValidator
        public bool CanMoveHeadset(Vector3 headserNewPosition)
        {
#if PHOTON_VOICE_AVAILABLE
            if (audioRoomMember.currentRoom != null && audioRoomMember.currentRoom.IsInRoom(headserNewPosition))
            {
                // the point is in a room where we already are: we can move in it
                return true;
            }
            foreach (var room in audioRoomMember.audioRoomManager.MatchingRooms(headserNewPosition))
            {
                if (room is ChatBubble c)
                {
                    if (allowedToMoveToChatBubbles == false)
                    {
                        return false;
                    }
                    if (c.AcceptMoreMembers == false)
                    {
                        // We can't move if the chatbubble is full, unless if we are already registered in it
                        return c.IsAlreadyInRoom(audioRoomMember);
                    }
                }
            }
#endif
            return true;
        }
#endregion

    }
}
