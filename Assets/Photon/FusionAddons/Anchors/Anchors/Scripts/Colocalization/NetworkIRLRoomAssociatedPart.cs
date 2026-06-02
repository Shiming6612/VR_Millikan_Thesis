using Fusion;
using Fusion.XR.Shared.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Allow to preview a room move
/// This class does not handle the room move application, just the preview:
/// - members should be moved by IRLRoomManager colocalization or followed anchors
/// - anchors should be moved by IRMRoomManager
/// - IRLRoom furniture should be despawned and respawned on colocalization
/// 
/// TODO The class could handle move for furnitures if we don't want to despawn them (but room merge would still require specific logic, so probably not relevant)
/// 
/// </summary>
[DefaultExecutionOrder(NetworkIRLRoomAssociatedPart.EXECUTION_ORDER)]
public class NetworkIRLRoomAssociatedPart : NetworkBehaviour
{
    const int EXECUTION_ORDER = 10_000;

    public bool attachedToLocalUser = false;
    public bool previewRoomRequesterMoves = true;
    public bool applyLayerIfNotStateAuthority = false;
    public string layerToApplyName = "";

    [Networked]
    public NetworkIRLRoomMember ReferenceRoomMember { get; set; }
    IRLRoomManager roomManager;

    [Header("Visualisation")]
    public bool adaptRendersToRoomManagerMode = false;
    public List<Renderer> renderers = new List<Renderer>();

    bool lastDisplayState = true;

    private void Awake()
    {
        roomManager = FindAnyObjectByType<IRLRoomManager>();
        if (renderers == null || renderers.Count == 0)
        {
            renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
        }
    }

    NetworkIRLRoomMember localMember = null;
    Vector3 offsetPositionToLocalMember;
    Quaternion offsetRotationToLocalMember;


    public override void Spawned()
    {
        base.Spawned();
        if (Object.HasStateAuthority == false && applyLayerIfNotStateAuthority)
        {
            LayerUtils.ApplyLayer(gameObject, layerToApplyName, true);
        }
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        bool localUserDetectedThisFrame = false;
        if (Object.HasStateAuthority)
        {
            if (localMember == null)
            {
                localMember = roomManager.localNetworkIRLRoomMember;
                if (localMember != null)
                {
                    localUserDetectedThisFrame = true;
                }
            }
        }


        if (Object.HasStateAuthority)
        {
            if (localUserDetectedThisFrame)
            {
                ReferenceRoomMember = localMember;
            }
        }

        if (Object.HasStateAuthority && attachedToLocalUser)
        {
            if (localUserDetectedThisFrame)
            {
                offsetPositionToLocalMember = localMember.transform.InverseTransformPoint(transform.position);
                offsetRotationToLocalMember = Quaternion.Inverse(localMember.transform.rotation) * transform.rotation;
            }
            if (localMember)
            {
                transform.position = localMember.transform.TransformPoint(offsetPositionToLocalMember);
                transform.rotation = localMember.transform.rotation * offsetRotationToLocalMember;
            }
        }
    }

    public enum PreviewState
    {
        NoPreview,
        Previewing,
        PreviewCancelledAsMoveAlreadyOccured,
    }

    public PreviewState previewState = PreviewState.NoPreview;
    Vector3 positionAtPreviewStart;
    Quaternion rotationAtPreviewStart;

    public override void Render()
    {
        base.Render();
        if (ReferenceRoomMember == null) return;

        var roomId = ReferenceRoomMember.RoomId.ToString();
        if (roomManager.knowRoomByRoomIds.ContainsKey(roomId) && roomManager.knowRoomByRoomIds[roomId].moveRequester != null)
        {
            var moveRequester = roomManager.knowRoomByRoomIds[roomId].moveRequester;
            if (previewRoomRequesterMoves && moveRequester.ShouldPreview)
            {
                if (previewState == PreviewState.NoPreview)
                {
                    previewState = PreviewState.Previewing;
                    positionAtPreviewStart = transform.position;
                    rotationAtPreviewStart = transform.rotation;
                }
                else if (previewState == PreviewState.Previewing)
                {
                    // Maybe the request has already been applied: no need to apply an addition preview anymore then
                    if (positionAtPreviewStart != transform.position || rotationAtPreviewStart != transform.rotation)
                    {
                        previewState = PreviewState.PreviewCancelledAsMoveAlreadyOccured;
                    }
                }

                if (previewState == PreviewState.Previewing)
                {
                    // Preview running
                    StartCoroutine(RestoreState(transform.position, transform.rotation));
                    roomManager.MoveTransformToFollowSameRoomReferenceElementMove(moveRequester, transform);
                }
            }
            else
            {
                previewState = PreviewState.NoPreview;

                // No preview of a pending move. If the element is attached to the local user, we can extrapolate its move normally
                if (Object.HasStateAuthority && attachedToLocalUser && localMember)
                {
                    transform.position = localMember.transform.TransformPoint(offsetPositionToLocalMember);
                    transform.rotation = localMember.transform.rotation * offsetRotationToLocalMember;
                }
            }
        }
        else
        {
            previewState = PreviewState.NoPreview;
        }

        if (adaptRendersToRoomManagerMode)
        {
            AdaptDisplay();
        }
    }

    void AdaptDisplay()
    {
        bool shoulDisplay = true;
        if (ReferenceRoomMember == null)
        {
            shoulDisplay = false;
        }
        else
        {
            var referenceRoomMemberRoomId = ReferenceRoomMember.RoomId.ToString();
            var localUserRoomId = roomManager.localNetworkIRLRoomMember?.RoomId.ToString() ?? "";
            var isRemoteRoom = referenceRoomMemberRoomId != localUserRoomId;

            if (roomManager.roomAssociatedPartDisplayMode == IRLRoomManager.NetworkIRLRoomAssociatedPartDisplayMode.Never)
            {
                shoulDisplay = false;
            }
            else if (roomManager.roomAssociatedPartDisplayMode != IRLRoomManager.NetworkIRLRoomAssociatedPartDisplayMode.Always && isRemoteRoom == false)
            {
                shoulDisplay = false;
            }
            else if (roomManager.roomAssociatedPartDisplayMode == IRLRoomManager.NetworkIRLRoomAssociatedPartDisplayMode.RemoteRoomOnly && isRemoteRoom == false)
            {
                shoulDisplay = false;

            }
            else if (roomManager.roomAssociatedPartDisplayMode == IRLRoomManager.NetworkIRLRoomAssociatedPartDisplayMode.MainPlayerInRemoteRoomOnly && isRemoteRoom)
            {
                if (ReferenceRoomMember != roomManager.RoomMainMember(referenceRoomMemberRoomId))
                {
                    shoulDisplay = false;
                }
            }
        }

        if (shoulDisplay != lastDisplayState)
        {
            foreach (var r in renderers) r.enabled = shoulDisplay;
            lastDisplayState = shoulDisplay;
        }
    }

    IEnumerator RestoreState(Vector3 p, Quaternion r)
    {
        yield return new WaitForEndOfFrame();
        transform.rotation = r;
        transform.position = p;
    }
}
