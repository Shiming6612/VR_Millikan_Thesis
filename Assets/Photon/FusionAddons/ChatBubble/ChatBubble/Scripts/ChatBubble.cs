using Fusion.Addons.AudioRoomAddon;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Fusion.Addons.AudioChatBubble
{
    /**
     * Require the local user to have an AudioRoomMember on its user networked rig,
     *  and some system triggering OnDidMove() on it (like a pair of HardwarelocomotionValidation/NetworkLocomotionValidation components on the hardware rig/ network rig)
     */
    public class ChatBubble : NetworkBehaviour, IAudioRoom, IAudioRoomListener, IAudioRoomAccessValidation
    {
        public List<IAudioRoomMember> allMembers;
        public AudioRoomManager audioRoomManager;
        public Transform roomReference;
        public int capacity = 4;
        public List<IAudioRoomMember> members = new List<IAudioRoomMember>();
        public bool unlockIfEmpty = true;

        [Networked]
        public NetworkBool IsLocked { get; set; }

        public enum RoomShape
        {
            Sphere = 0,
            Circle = 1,
            AABB = 2
        }
        public RoomShape roomShape = RoomShape.Sphere;
        // Radius of the sphere or circle / half size of the box
        [DrawIf(nameof(roomShape), (int)RoomShape.AABB, mode: DrawIfMode.Hide)]
        public Vector3 roomSize = new Vector3(4, 4, 4);

        [DrawIf(nameof(roomShape), (int)RoomShape.AABB, CompareOperator.NotEqual, mode: DrawIfMode.Hide)]
        public float radius = 2;

        // If true, the chatbubble works, aka an user in this chatbubble is active will receive an audio filter (the bubble is "soundproof")
        public bool isIsolated = true;

        #region IAudioRoomAccessValidation
        public bool AcceptMoreMembers => members.Count < capacity && IsLocked == false;
        #endregion

        #region IAudioRoom
        public bool IsIsolated => isIsolated;

        [Networked]
        public int RoomId { get; set; }
        public virtual bool IsInRoom(Vector3 position)
        {
            if (enabled == false) return false;
            bool inRoom = false;
            switch (roomShape)
            {
                case RoomShape.AABB:
                    inRoom = true;
                    var globalAxisOffset = position - roomReference.position;
                    if (Mathf.Abs(globalAxisOffset.x) > roomSize.x / 2) inRoom = false;
                    if (inRoom && Mathf.Abs(globalAxisOffset.y) > roomSize.y / 2) inRoom = false;
                    if (inRoom && Mathf.Abs(globalAxisOffset.z) > roomSize.z / 2) inRoom = false;
                    break;
                case RoomShape.Sphere:
                    var delta = roomReference.position - position;
                    inRoom = delta.magnitude < radius;
                    break;
                case RoomShape.Circle:
                    var groundPosition = roomReference.InverseTransformPoint(position);
                    var groundDelta = new Vector3(groundPosition.x, 0, groundPosition.z);
                    inRoom = groundDelta.magnitude < radius;
                    break;
            }
            return inRoom;
        }
        #endregion

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var r = roomReference;
            if (roomReference == null) r = transform;
            switch (roomShape)
            {
                case RoomShape.AABB:
                    Gizmos.color = new Color(1, 0, 0, 0.2f);
                    Gizmos.DrawCube(r.position, roomSize);
                    break;
                case RoomShape.Sphere:
                    Gizmos.color = new Color(1, 0, 0, 0.2f);
                    Gizmos.DrawSphere(r.position, radius);
                    break;
                case RoomShape.Circle:
                    Handles.color = new Color(1, 0, 0, 0.2f);
                    Handles.DrawWireDisc(center: r.position, normal: r.up, radius);
                    break;
            }
        }

        public int membersCount = 0;

        private void Update()
        {
            membersCount = members.Count;
        }
#endif

        protected virtual void Awake()
        {
            if (audioRoomManager == null) audioRoomManager = FindAnyObjectByType<AudioRoomManager>(FindObjectsInactive.Include);

#if PHOTON_VOICE_AVAILABLE
            if (audioRoomManager == null)
                Debug.LogError("AudioRoomManager not found");
            else
                audioRoomManager.RegisterAudioRoom(this);
#endif

            if (roomReference == null) roomReference = transform;
        }

        protected virtual void OnDestroy()
        {
#if PHOTON_VOICE_AVAILABLE

            if (audioRoomManager)
                audioRoomManager.UnregisterAudioRoom(this);
#endif
        }

        public bool IsAlreadyInRoom(IAudioRoomMember member)
        {
            return members.Contains(member);
        }

        #region Lock
        public void ToggleIsLock()
        {
            ChangeIsLock(!IsLocked);
        }

        public void ChangeIsLock(bool isLocked)
        {
            RpcChangeIsLock(isLocked);
        }

        [Rpc(sources: RpcSources.All, targets: RpcTargets.StateAuthority)]
        public void RpcChangeIsLock(bool isLocked)
        {
            IsLocked = isLocked;
        }
        #endregion

        public override void Spawned()
        {
            base.Spawned();
            CheckLock();
        }

        void CheckLock()
        {
            if (Object && Object.HasStateAuthority && IsLocked && members.Count == 0 && unlockIfEmpty)
            {
                IsLocked = false;
            }
        }
        #region IAudioRoomListener
        public void OnIsInRoom(IAudioRoomMember member, IAudioRoom room)
        {
            if (members.Contains(member) && room != (IAudioRoom)this)
            {
                members.Remove(member);
                CheckLock();

            }
            else if (members.Contains(member) == false && room == (IAudioRoom)this)
            {
                members.Add(member);
            }
        }
        #endregion
    }
}