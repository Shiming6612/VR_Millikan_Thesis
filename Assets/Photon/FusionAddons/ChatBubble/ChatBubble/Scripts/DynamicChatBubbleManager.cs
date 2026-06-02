using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.AudioChatBubble
{
    public interface IDynamicChatBubbleSpawner
    {
        public Vector3 HearPosition { get; }
        public Vector3 HeadGroundProjectionPosition { get; }
        public bool IsCurrentlyInroom { get; }
        public PlayerRef Player { get; }
        public void RequestCheckRoomPresence();
    }

    public interface IDynamicChatBubbleListener
    {
        void Register(DynamicChatBubble bubble);
        void Unregister(DynamicChatBubble bubble);
        void CancelingRemovingBubble(DynamicChatBubble bubble);
        void EvaluatingRemovingBubble(DynamicChatBubble bubble, float emptyBubbleConservationRemainingTime);
        void RemoveBubble(DynamicChatBubble bubble);
    }

    public class DynamicChatBubbleManager : MonoBehaviour
    {
        public List<IDynamicChatBubbleSpawner> chatBubbleSpawners = new List<IDynamicChatBubbleSpawner>();
        public List<DynamicChatBubble> dynamicChatBubbles = new List<DynamicChatBubble>();
        [Header("Empty criteria")]
        public float emptyBubbleConservationDuration = 0.5f;
        public int maxUserCountForEmptyDynamicChatBubble = 1;
        [Header("Dynamic chat bubble group id")]
        public int baseId = 1_000;
        public int maxRandomOffset = 20;

        public int FreeChatBubbleId()
        {
            int randomOffset = Random.Range(1, maxRandomOffset + 1);
            int maxChatbubbleId = baseId;
            foreach (var bubble in dynamicChatBubbles)
            {
                if (bubble.RoomId > maxChatbubbleId) maxChatbubbleId = bubble.RoomId;
            }
            return maxChatbubbleId + randomOffset;
        }

        #region Registration
        public void RegisterSpawner(IDynamicChatBubbleSpawner spawner)
        {
            if (chatBubbleSpawners.Contains(spawner)) return;
            chatBubbleSpawners.Add(spawner);
        }

        public void UnregisterSpawner(IDynamicChatBubbleSpawner spawner)
        {
            if (chatBubbleSpawners.Contains(spawner) == false) return;
            chatBubbleSpawners.Remove(spawner);
        }

        public void RegisterDynamicChatbubble(DynamicChatBubble chatBubble)
        {
            if (dynamicChatBubbles.Contains(chatBubble)) return;
            if (chatBubble.TryGetComponent<IDynamicChatBubbleListener>(out var remover))
            {
                remover.Register(chatBubble);
            }
            dynamicChatBubbles.Add(chatBubble);
        }

        public void UnregisterDynamicChatbubble(DynamicChatBubble chatBubble)
        {
            if (chatBubblesEmptyStarts.ContainsKey(chatBubble)) chatBubblesEmptyStarts.Remove(chatBubble);
            if (dynamicChatBubbles.Contains(chatBubble) == false) return;
            if (chatBubble.TryGetComponent<IDynamicChatBubbleListener>(out var remover))
            {
                remover.Unregister(chatBubble);
            }
            dynamicChatBubbles.Remove(chatBubble);
        }
        #endregion

        Dictionary<DynamicChatBubble, float> chatBubblesEmptyStarts = new Dictionary<DynamicChatBubble, float>();
        private void Update()
        {
            // Check dynamic bubble emptyness
            foreach (var bubble in dynamicChatBubbles)
            {
                if (bubble.enabled == false)
                {
                    // Bubble being destroyed
                    continue;
                }
                if (bubble.members.Count > maxUserCountForEmptyDynamicChatBubble)
                {
                    if (chatBubblesEmptyStarts.ContainsKey(bubble))
                    {
                        // Not empty anymore
                        chatBubblesEmptyStarts.Remove(bubble);
                        if (bubble.TryGetComponent<IDynamicChatBubbleListener>(out var remover))
                        {
                            remover.CancelingRemovingBubble(bubble);
                        }
                    }
                    continue;
                }
                // Here, the bubble is considered empty
                if (chatBubblesEmptyStarts.ContainsKey(bubble) == false)
                {
                    // First time we see it empty: we record the time to give it a chance to be filled again
                    //Debug.LogError("Detected empty bubble: " + bubble.RoomId);
                    chatBubblesEmptyStarts[bubble] = Time.time;
                    if (bubble.TryGetComponent<IDynamicChatBubbleListener>(out var remover))
                    {
                        remover.EvaluatingRemovingBubble(bubble, emptyBubbleConservationRemainingTime: emptyBubbleConservationDuration);
                    }
                }
                else if ((Time.time - chatBubblesEmptyStarts[bubble]) > emptyBubbleConservationDuration)
                {
                    // The bubble has been empty for long enough: we destroy it (or ask a listener to do so)
                    // if(bubble.Object) Debug.LogError($"Destroying empty bubble: {bubble.RoomId}[{bubble.Object.Id}] ({(Time.time - chatBubblesEmptyStarts[bubble])}s)");
                    chatBubblesEmptyStarts.Remove(bubble);

                    var dynamicChatBubbleListener = bubble.GetComponent<IDynamicChatBubbleListener>();
                    if (dynamicChatBubbleListener != null)
                    {
                        bubble.enabled = false;
                    }

                    // Only the state authority should despawn the bubble if empty
                    if (bubble.HasStateAuthority)
                    {
                        if (dynamicChatBubbleListener != null)
                        {
                            dynamicChatBubbleListener.RemoveBubble(bubble);
                        }
                        else
                        {
                            bubble.Object.Runner.Despawn(bubble.Object);
                        }
                    }

                    break;
                }
            }
        }
    }
}