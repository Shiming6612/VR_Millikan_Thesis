using Fusion.Addons.AudioRoomAddon;
using TMPro;
using UnityEngine;

namespace Fusion.Addons.AudioChatBubble
{
    public class ChatBubbleDisplayMembersCount : MonoBehaviour, IAudioRoomListener
    {
        TextMeshPro numberOfUserTMP;
        ChatBubble chatBubble;

        private void Awake()
        {
            if (chatBubble == null)
                chatBubble = GetComponent<ChatBubble>();
            if (chatBubble == null)
                Debug.LogError("ChaBubble not found");


            if (numberOfUserTMP == null)
                numberOfUserTMP = GetComponentInChildren<TextMeshPro>();
            if (numberOfUserTMP == null)
                Debug.LogError("numberOfUserTMP not found");
        }

        private void Start()
        {
            UpdateMembersDisplayInChatBubble();
        }

        public void OnIsInRoom(IAudioRoomMember member, IAudioRoom room)
        {
            UpdateMembersDisplayInChatBubble();
        }

        // Update the number of players on the display
        private void UpdateMembersDisplayInChatBubble()
        {
            if (numberOfUserTMP) numberOfUserTMP.text = chatBubble.members.Count + " / " + chatBubble.capacity;
        }

    }
}