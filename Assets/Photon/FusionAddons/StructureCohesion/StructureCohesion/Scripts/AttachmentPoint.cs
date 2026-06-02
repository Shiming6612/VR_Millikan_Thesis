using Fusion.Addons.Containment;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.XR.Shared.Utils;
using UnityEngine.Events;

namespace Fusion.Addons.StructureCohesion
{
    [System.Serializable]
    public struct AttachmentDetails : INetworkStruct
    {
        public uint attachementOrder;
        public Vector3 offset;
        public Quaternion rotationOffset;
    }

    public interface IAttachmentListener
    {
        void OnRegisterAttachment(AttachmentPoint attachedTo, AttachmentPoint attachedPoint, bool changeDetectionDuringRender);
        void OnRegisterReverseAttachment(AttachmentPoint attachedTo, AttachmentPoint attachedPoint, bool changeDetectionDuringRender);
        void OnUnregisterAttachment(AttachmentPoint attachedTo, AttachmentPoint attachedPoint, bool changeDetectionDuringRender);
        void OnUnregisterReverseAttachment(AttachmentPoint attachedTo, AttachmentPoint attachedPoint, bool changeDetectionDuringRender);
    }

    /**
     * Abstract attachment point. Can be used to memorize 2 points are attached together.
     * Note that it does not deal with maintening this attachment positions in place. Snap has to be called for that with dedicated logic and timing (to order a chain of attachment properly)
     * 
     * Implementation needs to define :
     * - how to find closest attachment point (TryFindClosestAttachmentPoint)
     * - how to match position with another attachment point (Snap)
     */
    public abstract class AttachmentPoint : NetworkBehaviour
    {
        [Networked]
        public AttachmentPoint AttachedPoint { get; set; }

        public AttachmentPoint preconfiguredAttachedPoint;

        [Networked]
        public AttachmentDetails Details { get; set; }

        // Reverse link: not synched on the networked, but computed based on the point on which this point is attached (attachedToPoint.AttachedPoint = this)
        public AttachmentPoint attachedToPoint = null;

        public AttachmentPoint RelatedPoint => (Object && AttachedPoint != null) ? AttachedPoint : attachedToPoint;

        public List<string> attachmentPointTags = new List<string>();
        public List<string> compatibleAttachmentPointTags = new List<string>();

        public bool IsAttachementSource => AttachedPoint != null;
        public bool IsAttachementTarget => attachedToPoint != null;
        
        public List<IAttachmentListener> attachmentListeners = new List<IAttachmentListener>();

        public AttachmentPoint IncomingAttachedPoint => requestedNewAttachedPoint;

        protected NetworkTRSP rootNTRSP;
        protected Rigidbody rb;

        [Header("Visual feedback")]
        public bool displayConnection = true;
        // For visual feedback: the current candidate attachment point
        public AttachmentPoint targetAttachmentPoint;
        public float connectionWidth = 0.02f;
        public Material connectionMaterial;
        public Color connectionColor = Color.red;
        protected LineRenderer connectionLineRenderer;
        ChangeDetector changeDetector;
        // Attached request, to align them with FUN
        [HideInInspector] public AttachmentPoint requestedNewAttachedPoint = null;
        protected AttachmentPoint requestedDeleteAttachmentTarget = null;
        AttachmentDetails requestedNewAttachmentDetails = default;

        [Header("Callbacks")]
        public UnityEvent onRegisterAttachment;
        public UnityEvent onUnregisterAttachment;
        public UnityEvent onRegisterReverseAttachment;
        public UnityEvent onUnregisterReverseAttachment;

        #region Preview attachment
        // Add a check to be sure a delete request is not planned
        public bool IsPendingAttachementSource(bool shouldInterpolate = false, Tick interpolationTo = default) {
            if (shouldInterpolate && interpolationTo < attachedDetectionTick)
            {
                //Debug.LogError($"[Debug] Skipping attachment due to interpolation interpolationTo:{interpolationTo} < attachmentDetectionTick:{attachedDetectionTick}");
                return false;
            }
            return (IsAttachementSource && AttachedPoint != requestedDeleteAttachmentTarget) || requestedNewAttachedPoint != null;
        }

        // Add a check to be sure a delete request is not planned
        public bool IsPendingAttachementTarget(bool shouldInterpolate = false, Tick interpolationTo = default)
        {
            if (shouldInterpolate && interpolationTo < attachedToDetectionTick)
            {
                //Debug.LogError($"[Debug] Skipping attachment to another point due to interpolation interpolationTo:{interpolationTo} < attachedToDetectionTick:{attachedToDetectionTick}");
                return false;
            }
            return (IsAttachementTarget && attachedToPoint.AttachedPoint == this && attachedToPoint.requestedDeleteAttachmentTarget != this) || requestedNewAttachedToPoint != null;
        }
        public AttachmentPoint PendingAttachedPoint => AttachedPoint != null ? AttachedPoint : requestedNewAttachedPoint;
        public AttachmentPoint PendingAttachedToPoint => attachedToPoint != null ? attachedToPoint : requestedNewAttachedToPoint;
        // Memorize that another object is going to attach us, to preview attachment
        [HideInInspector] public AttachmentPoint requestedNewAttachedToPoint = null;
        Tick attachedToDetectionTick;
        Tick attachedDetectionTick;
        #endregion

        #region Abstract methods to implement
        public abstract bool TryFindClosestAttachmentPoint(out AttachmentPoint closestPoint, out float minDistance, bool excludeSameGroupId = true);

        // Immediatly move this point magnet to match current other point
        public abstract void Snap(AttachmentPoint other);
        #endregion

        protected virtual void Awake()
        {
            attachmentListeners = new List<IAttachmentListener>(GetComponentsInParent<IAttachmentListener>());
            rootNTRSP = GetComponentInParent<NetworkTRSP>();
            rb = GetComponentInParent<Rigidbody>();
        }

        public virtual void RepositionToMatchRelatedPoint(AttachmentPoint a = null)
        {
            if (AttachedPoint == a)
            {
                RepositionToMatchAttachedPoint(a, Details);
            } 
            else if (a.AttachedPoint == this) {
                RepositionToMatchAttachedToPoint(a, a.Details);
            }
            else if (requestedNewAttachedPoint == a)
            {
                RepositionToMatchAttachedPoint(a, requestedNewAttachmentDetails);
            }
            else if (a.requestedNewAttachedPoint == this)
            {
                RepositionToMatchAttachedPoint(a, a.requestedNewAttachmentDetails);
            }
        }

        public virtual void RepositiontoMatchIncomingAttachedPoint()
        {
            if (requestedNewAttachedPoint)
            {
                RepositionToMatchAttachedPoint(requestedNewAttachedPoint, requestedNewAttachmentDetails);
            }
        }

        public virtual void RepositionToMatchAttachedPoint(AttachmentPoint a, AttachmentDetails details)
        {
            //Debug.LogError($"[Debug] [{this.Id}] RepositionToMatchAttachedPoint {a?.Id} | details.offset:{details.offset} | a.Details.offset: {a.Details.offset} | Details.offset: {Details.offset}");
            // We can use Details offset to reposition transform with its expected offset to a.transform
            (Vector3 targetPosition, Quaternion targetRotation) = TransformManipulations.ApplyUnscaledOffset(a.transform, details.offset, details.rotationOffset);
            RepositionToMatchPose(targetPosition, targetRotation);            
        }

        public virtual void RepositionToMatchAttachedToPoint(AttachmentPoint a, AttachmentDetails details)
        {
            //Debug.LogError($"[Debug] [{this.Id}] RepositionToMatchAttachedToPoint {a?.Id} | details.offset:{details.offset} | a.Details.offset: {a.Details.offset} | Details.offset: {Details.offset}");
            // We can use a's Details offset to reposition, but transform needs to be moved, based on a.transform position, and its expected offset to transform
            (Vector3 targetPosition, Quaternion targetRotation) = TransformManipulations.ReferentialPositionToRespectOffsetsOfPositionedObjectWithUnscaledOffsets(
                transform, 
                a.transform.position, a.transform.rotation, 
                details.offset, details.rotationOffset);

            RepositionToMatchPose(targetPosition, targetRotation);           
        }

        protected virtual void RepositionToMatchPose(Vector3 targetPosition, Quaternion targetRotation)
        {
            RepositionToMatchPose(rootToMove: rootNTRSP, targetPosition, targetRotation);
        }

        protected virtual void RepositionToMatchPose(NetworkTRSP rootToMove, Vector3 targetPosition, Quaternion targetRotation)
        {
            var localRotationToRoot = Quaternion.Inverse(rootToMove.transform.rotation) * transform.rotation;
            var rootRotation = targetRotation * Quaternion.Inverse(localRotationToRoot);
            if (rb)
            {
                rb.rotation = rootRotation;
            }
            rootToMove.transform.rotation = rootRotation;

            var rootPosition = targetPosition - transform.position + rootToMove.transform.position;
            if (rb)
            {
                rb.position = rootPosition;
            }
            rootToMove.transform.position = rootPosition;
        }

        #region NetworkBehaviour
        public override void Spawned()
        {
            base.Spawned();
            changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

            CheckAttachmentOrder(Details.attachementOrder);

            if(Object.HasStateAuthority && preconfiguredAttachedPoint)
            {
                // Add attachedPoint that has been set in the editor
                RequestAttachmentStorage(preconfiguredAttachedPoint);
            }

            if (AttachedPoint)
            {
                RegisterAttachment(AttachedPoint, true);
            }
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();

            ApplyPendingAttachmentRequest();
        }

        public override void Render()
        {
            base.Render();
            DetectAttachmentChanges();
            // An attachment is planned: we preview the position this point will take on the attachment
            if (requestedNewAttachedPoint)
            {
                RepositionToMatchAttachedPoint(requestedNewAttachedPoint, requestedNewAttachmentDetails);
            }
        }
        #endregion

        #region Attachment
        public void RegisterAttachment(AttachmentPoint atttachedPoint, bool changeDetection = false)
        {
            // Create reverse attachment
            atttachedPoint.AddReverseAttachment(this, changeDetection);

            OnRegisterAttachment(attachedToPoint: this, atttachedPoint, changeDetection);
            if (onRegisterAttachment != null) onRegisterAttachment.Invoke();
            attachedDetectionTick = Runner.Tick;
            atttachedPoint.attachedToDetectionTick = attachedDetectionTick;
        }

        public void AddReverseAttachment(AttachmentPoint attachedToPoint, bool changeDetection = false)
        {
            this.attachedToPoint = attachedToPoint;
            OnRegisterReverseAttachment(attachedToPoint, attachedPoint: this, changeDetection);
            if (onRegisterReverseAttachment != null) onRegisterReverseAttachment.Invoke();
        }


        public void UnregisterAttachment(AttachmentPoint previouslyAttachedPoint, bool changeDetection = false)
        {
            // Create reverse attachment
            previouslyAttachedPoint.RemoveReverseAttachment(this, changeDetection);

            OnUnregisterAttachment(this, previouslyAttachedPoint, changeDetection);
            if (onUnregisterAttachment != null) onUnregisterAttachment.Invoke();
        }

        public void RemoveReverseAttachment(AttachmentPoint previouslyAttachedToPoint, bool changeDetection = false)
        {
            // Check if the attachment was not alreay removed (for instance if both point are under the auth of the same player, the reverse attachment would be called during FUN, then during Render change detection)
            if (attachedToPoint == previouslyAttachedToPoint)
            {
                attachedToPoint = null;
            }
            else if (attachedToPoint != null)
            {
                Debug.LogError($"[DEBUG] [{name}] possible incoherent   attachedToPoint={attachedToPoint.transform.parent.name}     previouslyAttachedToPoint={previouslyAttachedToPoint.transform.parent.name}");
            }
            OnUnregisterReverseAttachment(previouslyAttachedToPoint, this, changeDetection);
            if (onUnregisterReverseAttachment != null) onUnregisterReverseAttachment.Invoke();
        }

        // Request an attachment: will be done during the next FUN
        // We first snap this object to match the other one position, then store the offset
        public void RequestAttachmentStorage(AttachmentPoint attachedPoint)
        {
            bool resettingAttachment = false;

            if (requestedDeleteAttachmentTarget == attachedPoint)
            {
                requestedDeleteAttachmentTarget = null;
                resettingAttachment = true;
            }

            if (resettingAttachment == false)
            {
                if (attachedPoint == AttachedPoint)
                {
                    // Already known
                    Debug.LogError("---- Attachment already stored");
                }
                if (requestedNewAttachedPoint == attachedPoint)
                {
                    Debug.LogError("---Attachment already requested");
                    return;
                }
            }
            var attachmentOrder = ConsumeNextAttachmentOrder();
            // Compute what would be the snap position
            var initialPos = rootNTRSP.transform.position;
            var initialRot = rootNTRSP.transform.rotation;
            Snap(attachedPoint);

            // Store the offset in attachment details. We used unscaled offset (worldspace, ignoring trnasforms scale)
            (var offset, var rotationOffset) = TransformManipulations.UnscaledOffset(referenceTransform: attachedPoint.transform, transformToOffset: transform);
            rootNTRSP.transform.position = initialPos;
            rootNTRSP.transform.rotation = initialRot;
            var details = new AttachmentDetails { attachementOrder = attachmentOrder, offset = offset, rotationOffset = rotationOffset };

            requestedNewAttachedPoint = attachedPoint;
            attachedPoint.requestedNewAttachedToPoint = this;
            requestedNewAttachmentDetails = details;
            // Request the state authority to be able to store the attachment
            if (Object.HasStateAuthority == false)
            {
                Object.RequestStateAuthority();
            }
        }

        // Request an unattachment: will be done during the next FUN
        public void RequestAttachmentDeletion(AttachmentPoint attachedPoint)
        {

            if (requestedNewAttachedPoint == attachedPoint)
            {
                if(requestedNewAttachedPoint.requestedNewAttachedToPoint == this)
                {
                    requestedNewAttachedPoint.requestedNewAttachedToPoint = null;
                }
                requestedNewAttachedPoint = null;
            }

            requestedDeleteAttachmentTarget = attachedPoint;

            if (Object.HasStateAuthority == false)
            {
                Object.RequestStateAuthority();
            }

        }

        [EditorButton("RequestAttachmentDeletion")]
        public void RequestAttachmentDeletion()
        {
            if (AttachedPoint)
            {
                RequestAttachmentDeletion(AttachedPoint);
            }
        }

        // Apply pending attachment (attachment triggered out of FUN, that needed to be aligned with it). Should be called during FUN to align attachment 
        void ApplyPendingAttachmentRequest()
        {
            if (requestedDeleteAttachmentTarget)
            {
                DeleteAttachment(requestedDeleteAttachmentTarget);
            }

            if (requestedNewAttachedPoint)
            {
                // Check if points are not already attached (an attachment point can only be used once, or if there is not already a reverse attachment


                bool noExistingAttachedPoint = AttachedPoint == null;
                bool newAttachedPointIsNotAttached = requestedNewAttachedPoint.attachedToPoint == null;
                // if new attachment point will be released from attachement, then it is still a valid target
                newAttachedPointIsNotAttached = newAttachedPointIsNotAttached || requestedNewAttachedPoint == requestedNewAttachedPoint.attachedToPoint.requestedDeleteAttachmentTarget;
                bool newAttachedPointIsNotAttachingUs = requestedNewAttachedPoint.AttachedPoint != this;


                if (noExistingAttachedPoint && newAttachedPointIsNotAttached && newAttachedPointIsNotAttachingUs) {
                    RepositionToMatchAttachedPoint(requestedNewAttachedPoint, requestedNewAttachmentDetails);
                    var nt = GetComponentInParent<NetworkTransform>();
                    nt.Teleport(nt.transform.position, nt.transform.rotation);
                    StoreAttachment(requestedNewAttachedPoint, requestedNewAttachmentDetails);
                }
            }


            if (requestedNewAttachedPoint && requestedNewAttachedPoint.requestedNewAttachedToPoint == this)
            {
                requestedNewAttachedPoint.requestedNewAttachedToPoint = null;
            }
            requestedNewAttachedPoint = null;
            requestedDeleteAttachmentTarget = null;
        }

        // Actualy store the attachment, and trigger effets (with RegisterAttachment). Can only be used by the state authority
        public void StoreAttachment(AttachmentPoint attachedPoint, AttachmentDetails details)
        {
            if (Object.HasStateAuthority == false)
            {
                throw new System.Exception("[Structure error] Only able to attach if state auth");
            }
            if (attachedPoint == AttachedPoint)
            {
                // Already known
                Debug.LogError("---- Attachment already stored");
                return;
            }
            AttachedPoint = attachedPoint;
            Details = details;
            RegisterAttachment(attachedPoint);
        }

        public void DeleteAttachment()
        {
            if (AttachedPoint)
            {
                DeleteAttachment(AttachedPoint);
            }
        }

        // Actualy remove the attachment, and trigger effets (with UnregisterAttachment). Can only be used by the state authority
        public void DeleteAttachment(AttachmentPoint attachedPoint)
        {
            if (Object.HasStateAuthority == false)
            {
                throw new System.Exception("[Structure error] Only able to unattach if state auth");
            }
            if(AttachedPoint == attachedPoint)
            {
                AttachedPoint = null;
            }
            else
            {
                Debug.LogError("[Error] Trying to unattach an unattached point");
            }
            UnregisterAttachment(attachedPoint);
        }

        // Edit attachment based on detected changes during Render. Apply effects with RegisterAttachment/UnregisterAttachment
        public void DetectAttachmentChanges()
        {
            foreach (var propertyname in changeDetector.DetectChanges(this, out var previousBuffer, out var currentBuffer))
            {
                switch (propertyname)
                {
                    case nameof(AttachedPoint):
                        {
                            var reader = GetBehaviourReader<AttachmentPoint>(propertyname);
                            AttachmentPoint previous = reader.Read(previousBuffer);
                            AttachmentPoint current = reader.Read(currentBuffer);
                            var detailsReader = GetPropertyReader<AttachmentDetails>(nameof(Details));
                            AttachmentDetails currentDetails = detailsReader.Read(currentBuffer);

                            if (previous != null)
                            {
                                UnregisterAttachment(previouslyAttachedPoint: previous, changeDetection: true);
                            }

                            if (current != null)
                            {
                                CheckAttachmentOrder(currentDetails.attachementOrder);
                                RegisterAttachment(current, changeDetection: true);

                            }
                        }
                        break;
                }
            }
        }
        #endregion

        #region Attachment listeners
        void OnRegisterAttachment(AttachmentPoint attachedToPoint, AttachmentPoint attachedPoint, bool changeDetection)
        {
            foreach (var l in attachmentListeners) l.OnRegisterAttachment(attachedToPoint, attachedPoint, changeDetection);
        }
        void OnRegisterReverseAttachment(AttachmentPoint attachedToPoint, AttachmentPoint attachedPoint, bool changeDetection)
        {
            foreach (var l in attachmentListeners) l.OnRegisterReverseAttachment(attachedToPoint, attachedPoint, changeDetection);
        }
        void OnUnregisterAttachment(AttachmentPoint attachedToPoint, AttachmentPoint attachedPoint, bool changeDetection)
        {
            foreach (var l in attachmentListeners) l.OnUnregisterAttachment(attachedToPoint, attachedPoint, changeDetection);
        }
        void OnUnregisterReverseAttachment(AttachmentPoint attachedToPoint, AttachmentPoint attachedPoint, bool changeDetection)
        {
            foreach (var l in attachmentListeners) l.OnUnregisterReverseAttachment(attachedToPoint, attachedPoint, changeDetection);
        }
        #endregion

        private void LateUpdate()
        {
            UpdateConnectionVisual();
        }

        #region Visual connection to target point
        protected virtual void DisplayConnectionVisual()
        {
            if (connectionLineRenderer == null)
            {
                var lrObj = new GameObject("ConnectionVisual");
                lrObj.transform.parent = transform;
                lrObj.transform.localPosition = Vector3.zero;
                lrObj.transform.localRotation = Quaternion.identity;
                connectionLineRenderer = lrObj.AddComponent<LineRenderer>();
                connectionLineRenderer.useWorldSpace = true;
                connectionLineRenderer.positionCount = 2;
                connectionLineRenderer.startWidth = connectionWidth;
                connectionLineRenderer.endWidth = connectionWidth;
                connectionLineRenderer.widthMultiplier = 1;
                if (connectionMaterial != null) connectionLineRenderer.sharedMaterial = connectionMaterial;
                connectionLineRenderer.transform.position = transform.position;
                connectionLineRenderer.transform.rotation = transform.rotation;
                connectionLineRenderer.startColor = connectionColor;
                connectionLineRenderer.endColor = connectionColor;
                connectionLineRenderer.receiveShadows = false;
                connectionLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (connectionLineRenderer.enabled == false)
            {
                connectionLineRenderer.enabled = true;
            }
        }

        protected virtual void HideConnectionVisual()
        {
            if (connectionLineRenderer != null && connectionLineRenderer.enabled)
            {
                connectionLineRenderer.enabled = false;
            }
        }

        protected virtual void PositionConnectionVisual()
        {
            if (connectionLineRenderer == null) return;
            connectionLineRenderer.SetPosition(0, transform.position);
            connectionLineRenderer.SetPosition(1, targetAttachmentPoint.transform.position);

        }

        // Display a link to the targetAttachmentPoint if any, then set it to null to force to reset it 
        protected virtual void UpdateConnectionVisual()
        {
            if (targetAttachmentPoint && displayConnection)
            {
                DisplayConnectionVisual();
                PositionConnectionVisual();
            }
            else
            {
                HideConnectionVisual();
            }
            targetAttachmentPoint = null;
        }
        #endregion

        #region AttachmentOrder
        // Store a global index for ordering attachment: it can be used to determine globally which one is the most recent in a structure, to break along it, if it is between 2 movings parts
        public static uint MaxAttachmentOrder = 1;

        // Return the current max attachment order, and increase it for next usage
        public static uint ConsumeNextAttachmentOrder()
        {
            MaxAttachmentOrder++;
            return MaxAttachmentOrder;
        }

        // Check if a new attachment as a greater order than the local one, to be sure to still use the largest order possible for future attachments created locally
        public void CheckAttachmentOrder(uint attachementOrder)
        {
            if (attachementOrder > MaxAttachmentOrder)
            {
                MaxAttachmentOrder = attachementOrder;
            }
        }
        #endregion
    }
}
