using Fusion;
using Fusion.XR.Shared.Core;
using UnityEngine;

/**
 * Anchor detected in a room by an user
 * The room id is based on the room id (NetworkIRLRoomMember.RoomId) of the user detecting it
 * 
 * Requires an IRLRoomManager in the scene
 * 
 * Need to persist after state authority disconnection, and to allow state auth changes
 */
[RequireComponent(typeof(NetworkTransform))]
public class NetworkIRLRoomAnchor : NetworkBehaviour, IStateAuthorityChanged
{
    const int MAX_ANCHORID_LENGTH = 64;
    [Networked]
    public NetworkString<_64> AnchorId { get; set; }
    [Networked, OnChangedRender(nameof(OnRoomIdChange))]
    public NetworkString<_32> RoomId { get; set; }

    public Pose AnchorPose => new Pose(transform.position, transform.rotation);

    [Networked]
    public bool IsChangingRoomForbidden { get; set; }  = false;

    public bool shouldUpdateGameObjectName = true;

    [Networked]
    public NetworkBool ShouldNotBeFollowed { get; set; }  = false;


    IRLRoomManager roomManager;

    NetworkTransform nt;

    private void Awake()
    {
        roomManager = FindAnyObjectByType<IRLRoomManager>();
        if (GetComponentInParent<NetworkTransform>() == null)
        {
            Debug.LogError("Missing network transform");
        }
        nt = GetComponent<NetworkTransform>();
        // Anchor might be moved during Render (due to cascading room merges)
#if FUSION_2_1_OR_NEWER
        nt.ConfigFlags = NetworkTransform.NetworkTransformFlags.DisableSharedModeInterpolation;
#else
        nt.DisableSharedModeInterpolation = true;
#endif
    }

    public override void Spawned()
    {
        base.Spawned();
        roomManager.RegisterNetworkIRLRoomAnchor(this);
        roomManager.OnNetworkIRLRoomAnchorRoomChange(this, "");
    }

    public override void Render()
    {
        base.Render();
        if (shouldUpdateGameObjectName)
        {
            UpdateName();
        }
        Object.AffectStateAuthorityIfNone();
    }

    public void UpdateName()
    {
        name = $"IRLRoomAnchor-{AnchorId} ({RoomId})";
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        roomManager?.UnregisterNetworkIRLRoomAnchor(this);
    }

    public void SetAnchorId(string anchorId)
    {
        if (string.IsNullOrEmpty(anchorId)) return;

        if (Object.HasStateAuthority == false)
        {
            Debug.LogError("Cannot set anchorId on anchors not owned");
            return;
        }

        if (anchorId.Length > MAX_ANCHORID_LENGTH)
        {
            anchorId = anchorId.Substring(0, MAX_ANCHORID_LENGTH);
        }
        AnchorId = anchorId;
    }

    public void ChangeRoomId(string roomId)
    {
        var previousRoomId = RoomId.ToString();
        if (string.IsNullOrEmpty(roomId)) return;

        if (Object.HasStateAuthority == false)
        {
            Debug.LogError("Cannot set RoomId on anchors not owned");
            return;
        }

        if (roomId.Length > NetworkIRLRoomMember.MAX_ROOMID_LENGTH)
        {
            roomId = roomId.Substring(0, NetworkIRLRoomMember.MAX_ROOMID_LENGTH);
        }
        RoomId = roomId;

        roomManager?.OnNetworkIRLRoomAnchorRoomChange(this, previousRoomId);
    }

    public void ChangeRoomId(NetworkString<_32> roomId)
    {
        var previousRoomId = RoomId.ToString();
        if (Object.HasStateAuthority == false)
        {
            Debug.LogError("Cannot set RoomId on anchors not owned");
            return;
        }

        RoomId = roomId;
        roomManager?.OnNetworkIRLRoomAnchorRoomChange(this, previousRoomId);
    }

    void OnRoomIdChange(NetworkBehaviourBuffer previous)
    {
        string previousRoomId = GetPropertyReader<NetworkString<_32>>(nameof(RoomId)).Read(previous).ToString();
        Debug.Log($"Anchor room changed: {RoomId}, prev: {previousRoomId}");

        roomManager?.OnNetworkIRLRoomAnchorRoomChange(this, previousRoomId);
    }

    #region IStateAuthorityChanged
    public void StateAuthorityChanged()
    {
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
