using Fusion;
using Fusion.XR.Shared.Core;
using System;
using UnityEngine;

namespace Fusion.Addons.AnchorsAddon.Colocalization
{
    public class NetworkIRLRoomMoveRequester : NetworkBehaviour, IRLRoomMovingReferenceElement, IStateAuthorityChanged
    {
        [Networked, OnChangedRender(nameof(OnRoomIdChange))]
        public NetworkString<_32> RoomId { get; set; }

        [Networked]
        public Vector3 PositionBeforeMoveToPropagate { get; set; }

        [Networked]
        public Quaternion RotationBeforeMoveToPropagate { get; set; }
        [Networked]
        public Vector3 PositionAfterMoveToPropagate { get; set; }

        [Networked]
        public Quaternion RotationAfterMoveToPropagate { get; set; }

        [Networked, OnChangedRender(nameof(OnMoveCounterChange))]
        public int MoveCounter { get; set; } = 0;

        [Tooltip("If an axis value is not zero, the requested position won't change on this axis")]
        public Vector3 positionLockAxis = new Vector3(0, 1, 0);
        [Tooltip("If an axis value is not zero, the requested position won't change on this axis")]
        public Vector3 rotationLockAxis = new Vector3(1, 0, 1);

        public enum RequestStatus { 
            RequestAllowed,                 // Normal state: watches for target moves to potential trigger a request    
            RequestPostponed,               // Expected request position are saved, but the actual move request is not sent to move the room (useful for previews)
            RequestPlanned,               // A request should be send to move the room, we are first waiting a bit (delayBeforeStabilizingIncomingPosition) to be sure that the requester is not currently moving
            RequestSent,                    // The move request has just been sent
            RequestsBlocked                 // This component should not allow moves (relevant if an user in the room could move it, and it is not desired)
        }

        [Networked]
        public NetworkBool ShouldPreview { get; set; } = false;

        [Networked]
        public RequestStatus Status { get; set; } = RequestStatus.RequestAllowed;

        [Tooltip("Transform to easily pass the target position and rotaiton (optional, will be automatically set)")]
        public Transform target;

        public bool despawnWhenRoomAlreadycontainsARequester = true;
        public bool despawnWhenRoomIsEmpty = true;


        IRLRoomManager roomManager;

        float delayBeforeStabilizingIncomingPosition = 1;
        Vector3 lastSentTargetPosition;
        Quaternion lastSentTargetRotation;
        float incomingPositionStabilisationTimeout = -1;

        string baseName = "";

        private void Awake()
        {
            baseName = name;
            roomManager = FindAnyObjectByType<IRLRoomManager>();
            if (target == null) target = transform;
        }

        /// <summary>
        /// Plan a room move, so that this member rig will have a new position/rotation
        /// All members and anchors in the room will then be moved by the IRLRoomManager (first the anchors, then the members will follow the anchors)
        /// </summary>
        public void InitializingRoomMove(Vector3 positionBeforeMoveToPropagate, Quaternion rotationBeforeMoveToPropagate, Vector3 positionAfterMoveToPropagate, Quaternion rotationAfterMoveToPropagate)
        {
            PositionBeforeMoveToPropagate = positionBeforeMoveToPropagate;
            RotationBeforeMoveToPropagate = rotationBeforeMoveToPropagate;
            PositionAfterMoveToPropagate = positionAfterMoveToPropagate;
            RotationAfterMoveToPropagate = rotationAfterMoveToPropagate;
            MoveCounter = roomManager.MaxMoveCounterForRoomId(RoomId.ToString()) + 1;
        }

        void OnMoveCounterChange()
        {
            roomManager?.NetworkIRLRoomMoveRequesterMoveCounterChange(this);
        }

        public override void Spawned()
        {
            base.Spawned();
            if (Object.HasStateAuthority)
            {
                DidMoveWithoutRequest();
            }
            name = $"{baseName}-{RoomId}";

            roomManager.RegisterNetworkIRLMoveRequester(this);
            roomManager.OnNetworkIRLRoomMoveRequesterRoomChange(this, "");

            // Not neet to call roomManager.NetworkIRLRoomMoveRequesterMoveCounterChange(this),
            // as a new requester should not trigger and move, and as for late joining users, there have no move to propagate
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            roomManager?.UnregisterNetworkIRLMoveRequester(this);
        }

        // Prevent triggering moves
        public void DidMoveWithoutRequest()
        {
            lastSentTargetPosition = target.position;
            lastSentTargetRotation = target.rotation;
            PositionBeforeMoveToPropagate = lastSentTargetPosition;
            RotationBeforeMoveToPropagate = lastSentTargetRotation;
            PositionAfterMoveToPropagate = lastSentTargetPosition;
            RotationAfterMoveToPropagate = lastSentTargetRotation;
        }

        public Vector3 ApplyFilter(Vector3 originalValue, Vector3 targetValue, Vector3 filter)
        {
            var result = targetValue;
            if (filter.x > 0) result.x = originalValue.x;
            if (filter.y > 0) result.y = originalValue.y;
            if (filter.z > 0) result.z = originalValue.z;
            return result;
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            if (Status == RequestStatus.RequestsBlocked) return;

            if (target != null && string.IsNullOrEmpty(RoomId.ToString()) == false)
            {
                if (lastSentTargetPosition != target.position || lastSentTargetRotation != target.rotation)
                {
                    var filteredTargetPosition = ApplyFilter(lastSentTargetPosition, target.position, positionLockAxis);
                    var filteredTargetRotation = Quaternion.Euler(ApplyFilter(lastSentTargetRotation.eulerAngles, target.rotation.eulerAngles, rotationLockAxis));

                    if (incomingPositionStabilisationTimeout == -1 || PositionAfterMoveToPropagate != filteredTargetPosition || RotationAfterMoveToPropagate != filteredTargetRotation)
                    {
                        PositionBeforeMoveToPropagate = lastSentTargetPosition;
                        RotationBeforeMoveToPropagate = lastSentTargetRotation;
                        PositionAfterMoveToPropagate = filteredTargetPosition;
                        RotationAfterMoveToPropagate = filteredTargetRotation;

                        incomingPositionStabilisationTimeout = Time.time + delayBeforeStabilizingIncomingPosition;
                    }

                    if (Status != RequestStatus.RequestPostponed)
                    {
                        Status = RequestStatus.RequestPlanned;
                        if (delayBeforeStabilizingIncomingPosition == 0 || incomingPositionStabilisationTimeout < Time.time)
                        {
                            roomManager?.ConsoleLog($"InitializingRoomMove: {lastSentTargetPosition} -> {PositionAfterMoveToPropagate}");
                            InitializingRoomMove(lastSentTargetPosition, lastSentTargetRotation, PositionAfterMoveToPropagate, RotationAfterMoveToPropagate);
                            incomingPositionStabilisationTimeout = -1;
                            lastSentTargetPosition = PositionAfterMoveToPropagate;
                            lastSentTargetRotation = RotationAfterMoveToPropagate;
                            target.rotation = lastSentTargetRotation;
                            target.position = lastSentTargetPosition;
                            Status = RequestStatus.RequestSent;
                        }
                    }
                }
            }

            if (Status == RequestStatus.RequestSent && Object.HasStateAuthority && roomManager.MaxMoveCounterForRoomId(RoomId.ToString()) >= MoveCounter)
            {
                Status = RequestStatus.RequestAllowed;
            }
        }

        public override void Render()
        {
            base.Render();
            Object.AffectStateAuthorityIfNone();
        }

        public void ChangeRoomId(string roomId)
        {
            var previousRoomId = RoomId.ToString();
            if (string.IsNullOrEmpty(roomId)) return;

            if (Object.HasStateAuthority == false)
            {
                Debug.LogError("Cannot set RoomId on move requester not owned");
                return;
            }

            if (roomId.Length > NetworkIRLRoomMember.MAX_ROOMID_LENGTH)
            {
                roomId = roomId.Substring(0, NetworkIRLRoomMember.MAX_ROOMID_LENGTH);
            }
            RoomId = roomId;

            roomManager?.OnNetworkIRLRoomMoveRequesterRoomChange(this, previousRoomId);
        }

        public virtual void OnRoomAlreadyContainsARequester(NetworkIRLRoomMoveRequester existingRequester) {
            if(despawnWhenRoomAlreadycontainsARequester && Object.HasStateAuthority)
            {
                Debug.Log($"Only one move requester should be associated to a given room: {existingRequester} ({existingRequester.RoomId}=");
                Runner.Despawn(Object);
            }
                
        }

        public virtual void OnRoomEmpty()
        {
            if (despawnWhenRoomIsEmpty && Object.HasStateAuthority)
            {
                Debug.Log($"Room {RoomId} empty, destroying move requester");
                Runner.Despawn(Object);
            } 
        }

        void OnRoomIdChange(NetworkBehaviourBuffer previous)
        {
            name = $"{baseName}-{RoomId}";

            string previousRoomId = GetPropertyReader<NetworkString<_32>>(nameof(RoomId)).Read(previous).ToString();
            Debug.Log($"Move requester room changed: {RoomId}, prev: {previousRoomId}");

            roomManager?.OnNetworkIRLRoomMoveRequesterRoomChange(this, previousRoomId);
        }

        #region IStateAuthorityChanged
        public void StateAuthorityChanged()
        {
            if (Object.HasStateAuthority)
            {
                // Make sure to store the initial position on state auth change, to prevent triggering a move based on previously known position
                DidMoveWithoutRequest();
            }

            var roomId = RoomId.ToString();
            if (Object.HasStateAuthority && roomManager.knowRoomByRoomIds.ContainsKey(roomId))
            {
                // Our room exists, and we are state auth on this component
                if (roomManager.knowRoomByRoomIds[roomId].members.Count == 0)
                {
                    Runner.Despawn(Object);
                }
            }
            if (Object.HasStateAuthority && roomManager.knowRoomByRoomIds.ContainsKey(roomId) == false)
            {
                // Our room does not exist anymore
                Runner.Despawn(Object);
            }
        }
        #endregion
    }
}
