using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Fusion.Addons.Containment;
using Fusion.XR.Shared.Core;

namespace Fusion.Addons.StructureCohesion
{
    public enum StructuralCohesionMode
    {
        // Objects of a structure always move together
        SystematicCohesion,
        // Grabbing an object attached to / attaching strictly more "heavy" parts detaches from the heaviest part
        WeightBasedCohesion
    }

    public interface IStructurePartPoint
    {
        StructurePart StructurePart { get; }
    }

    public abstract class StructurePart : NetworkBehaviour, IAfterTick, IBeforeTick, IContainable, IStructureListener, IAttachmentListener
    {
        [Networked]
        public NetworkBool IsMoving { get; set; }

        public Structure CurrentStructure { get; set; } = null;

        public int partWeight = 100;
        public StructuralCohesionMode structuralCohesionMode = StructuralCohesionMode.WeightBasedCohesion;

        public List<AttachmentPoint> attachmentPoints = new List<AttachmentPoint>();

        [System.Serializable]
        public class AttachementEvents : UnityEvent<StructurePart, AttachmentPoint, Vector3, bool> { };

        [Header("Callbacks")]
        public AttachementEvents onRegisterAttachmentEvent;
        public AttachementEvents onUnregisterAttachmentEvent;

        [Header("Debug")]
        public bool deepDebug = false;
        [SerializeField] GameObject structureReferenceIndicator;

        [SerializeField] TMPro.TextMeshProUGUI debugLine1;
        [SerializeField] TMPro.TextMeshProUGUI debugLine2;
        [SerializeField] TMPro.TextMeshProUGUI debugLine3;

        // We store the simulation phase, as we want to align attachment creation and parenting to FUN
        [HideInInspector] 
        public StructurePhase currentPhase = StructurePhase.AfterTick;

        // Track if a repositioning has been done for this object during this phase (stored to avoid looping between attachments when browing the strcuture graph)
        bool repositionedInStructureDuringThisPhase = false;

        [HideInInspector]
        public bool isStill = false;
        float lastIsMovingTime = -1;

        StructurePartsManager structurePartsManager;
        NetworkTRSP networkTRSP;

        IContainmentHandler containmentHandler;
        public void ChangeStructure(Structure s)
        {
            if (deepDebug || CurrentStructure?.deepDebug == true) Debug.LogError($"[Part {this}] ChangeStructure {CurrentStructure} -> {s}");

            CurrentStructure = s;
        }

        // When interpolating attachment on proxies, we want to ignore cases where an object has been still for a long time (in number of ticks)
        const int STILL_OBJECT_DETECTION_DURATION_THRESHOLD = 20;


        #region Phase end transfer to structure
        public void PrepareNextPhase(StructurePhase phase)
        {
            currentPhase = phase;
        }

        public void OnPhaseStart(StructurePhase phase)
        {
            //if(deepDebug || CurrentStructure?.deepDebug == true) Debug.LogError($"[Part {this}] OnPhaseChange {phase} (parent: {transform.parent}, structure: {CurrentStructure})");
            currentPhase = phase;
            CurrentStructure?.OnPhaseStart(phase);
            // RepositionAttachedParts will prapagate the reposition to siblings (and so on in the structure), and duplicates call are avoided with repositionedInStructureDuringThisPhase.
            // Now that the phase is ended, and so potential repositionings, reset it
            repositionedInStructureDuringThisPhase = false;
        }
        #endregion

        #region MonoBehaviour
        protected virtual void Awake()
        {
            // Detect child attachment point, and register ourselves as their structure provider (tells them in which strcture they are)
            attachmentPoints = new List<AttachmentPoint>();

            foreach (var a in GetComponentsInChildren<AttachmentPoint>())
            {
                if (a is IStructurePartPoint)
                {
                    attachmentPoints.Add(a);
                }
            }

            if (structurePartsManager == null) structurePartsManager = FindAnyObjectByType<StructurePartsManager>(FindObjectsInactive.Include);
            structurePartsManager?.RegisterStructurePart(this);

            containmentHandler = GetComponent<IContainmentHandler>();
            networkTRSP = GetComponentInParent<NetworkTRSP>();
        }


        protected virtual void InterpolationPhase()
        { 
            OnPhaseStart(StructurePhase.OnBeforeRender);
            PrepareNextPhase(StructurePhase.BeforeTick);
            if (structureReferenceIndicator)
            {
                var shouldBeEnabled = CurrentStructure != null && CurrentStructure.lastReferencePart == this;
                if (shouldBeEnabled != structureReferenceIndicator.gameObject.activeSelf)
                {
                    structureReferenceIndicator.gameObject.SetActive(shouldBeEnabled);
                }
            }
        }
        #endregion
        void UpdateDebugPanel()
        {
            string t = null;
            if (debugLine1)
            {
                t = $"Structure: {(CurrentStructure == null ? "" : "#" + StructureManager.SharedInstance.structures.IndexOf(CurrentStructure))}/P:{Object.Id}/IsMoving: {IsMoving}";
                debugLine1?.SetText(t);
            }

            if (debugLine2)
            {
                t = "Out:";
                foreach (var a in attachmentPoints)
                {
                    if (a.IsAttachementSource)  t += $"[{a.Details.attachementOrder}]{a.AttachedPoint.Object.Id}|";
                }
                debugLine2.text = t;
            }

            if (debugLine3)
            {
                t = $"In: ";
                foreach (var a in attachmentPoints)
                {
                    if (a.IsAttachementTarget) t += $"[{a.attachedToPoint.Details.attachementOrder}]{a.attachedToPoint.Object.Id}|";
                }
                debugLine3.text = t;
            }
        }

        #region IAttachmentListener
        public void OnRegisterAttachment(AttachmentPoint attachedTo, AttachmentPoint attachedPoint, bool changeDetection)
        {
            if (attachmentPoints.Contains(attachedTo) == false && attachmentPoints.Contains(attachedPoint) == false)
            {
                // We skip child attachement points which are not among our structure points
                return;
            }


            StructurePart attachedToPart = null;
            StructurePart attachedPart = null;
            if (attachedTo is IStructurePartPoint attachedToStructurePoint && attachedPoint is IStructurePartPoint attachedStructurePoint)
            {
                attachedToPart = attachedToStructurePoint.StructurePart;
                attachedPart = attachedStructurePoint.StructurePart;
            }
            if (attachedToPart == null || attachedPart == null) throw new System.Exception("[Error] Missing part in attachment");

            // Update structure
            StructureManager.SharedInstance.UpdateStructureForNewAttachment(from: attachedToPart, to: attachedPart, attachedTo.Details);

            // We only send the event on change detection to avoid duplicated event
            if (onRegisterAttachmentEvent != null && changeDetection)
            {
                var position = attachedTo.transform.position;
                onRegisterAttachmentEvent.Invoke(this, attachedTo, position, false);
            }
        }

        public void OnRegisterReverseAttachment(AttachmentPoint attachedTo, AttachmentPoint attachedPoint, bool changeDetection)
        {
            // We only send the event on change detection to avoid duplicated event
            if (onRegisterAttachmentEvent != null && changeDetection)
            {
                var position = attachedTo.transform.position;
                onRegisterAttachmentEvent.Invoke(this, attachedPoint, position, true);
            }
        }

        public void OnUnregisterAttachment(AttachmentPoint attachedTo, AttachmentPoint attachedPoint, bool changeDetection) {

            if (attachmentPoints.Contains(attachedTo) == false && attachmentPoints.Contains(attachedPoint) == false)
            {
                // We skip child attachement points which are not among our structure points
                return;
            }

            StructurePart attachedToPart = null;
            StructurePart attachedPart = null;
            if (attachedTo is IStructurePartPoint attachedToStructurePoint && attachedPoint is IStructurePartPoint attachedStructurePoint)
            {
                attachedToPart = attachedToStructurePoint.StructurePart;
                attachedPart = attachedStructurePoint.StructurePart;
            }
            if (attachedToPart == null || attachedPart == null) throw new System.Exception("[Error] Missing part in attachment");

            // Update structure
            StructureManager.SharedInstance.UpdateStructureForDeletedAttachment(attachedToPart, attachedPart);

            // We only send the event on change detection to avoid duplicated event
            if (onUnregisterAttachmentEvent != null && changeDetection)
            {
                var position = attachedTo.transform.position;
                onUnregisterAttachmentEvent.Invoke(this, attachedTo, position, false);
            }
        }

        public void OnUnregisterReverseAttachment(AttachmentPoint attachedTo, AttachmentPoint attachedPoint, bool changeDetection) {

            // We only send the event on change detection to avoid duplicated event
            if (onRegisterAttachmentEvent != null && changeDetection)
            {
                onUnregisterAttachmentEvent.Invoke(this, attachedPoint, attachedPoint.transform.position, true);
            }
        }
        #endregion

        #region NetworkBehaviour

        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        [BeforeRenderOrder(7000)]
        protected virtual void OnBeforeRender()
        {
            InterpolationPhase();
        }

        public override void Render()
        {
            base.Render();
            OnPhaseStart(StructurePhase.Render);
            UpdateIsMoving();
            if (IsMoving)
            {
                isStill = false;
                lastIsMovingTime = Time.time;
                if (TryFindStructureClosestAttachmentPoint(out StructurePart currentStructurepart, out AttachmentPoint localPoint, out var closestPoint, out var distance, excludeSameGroupId: true))
                {
                    // Used to display a link between the attachment point that would be attached if we'd release now
                    localPoint.targetAttachmentPoint = closestPoint;
                }
            } 
            else if(isStill == false)
            {
                if (lastIsMovingTime == -1)
                {
                    // Startup: we cannot be sure this object was still on remote, so we start the time counter before considering it is still
                    lastIsMovingTime = Time.time;
                } 
                else if ((Time.time - lastIsMovingTime) > Structure.PostStructureChangeRepositioningPropagationDuration)
                {
                    isStill = true;
                }
            }

            UpdateDebugPanel();

            foreach (var a in attachmentPoints)
            {
                if (a.requestedNewAttachedPoint)
                {
                    // A new attached point will be added in the next FUN. At this moment, this part will moved to match the attached point position
                    // In advance, we need to place the object (and its structure) at the proper position
                    a.RepositiontoMatchIncomingAttachedPoint();
                    if (CurrentStructure != null)
                    {
                        RepositionAttachedParts(useInitialRoot: true);
                    }
                }
            }

            // If the player having the state authority is not here anymore, someone should take the authority to ensure the part position keeps being updated
            Object.AffectStateAuthorityIfNone();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            StructureManager.SharedInstance?.UpdateStructureForDestroyedStructurePart(this);

            if (structurePartsManager)
                structurePartsManager.UnegisterStructurePart(this);
        }
        #endregion

        #region IAfterTick
        public void AfterTick()
        {
            OnPhaseStart(StructurePhase.AfterTick);
            PrepareNextPhase(StructurePhase.Render);
        }
        #endregion

        #region IBeforeTick
        public void BeforeTick()
        {
            PrepareNextPhase(StructurePhase.FUN);
        }
        #endregion

        #region IsMoving update
        // Subclasses can update IsMoving here
        protected abstract void UpdateIsMoving();
        #endregion

        #region Proximity lookup
        // Closest attachment point for this structure only
        public bool TryFindClosestAttachmentPoint(out AttachmentPoint localAttachmentPoint, out AttachmentPoint closestPoint, out float minDistance, bool excludeSameGroupId = true)
        {
            minDistance = float.PositiveInfinity;
            closestPoint = null;
            localAttachmentPoint = null;
            bool pointFound = false;
            foreach (var point in attachmentPoints)
            {
                if (point.TryFindClosestAttachmentPoint(out var closestPointFromAttachmentPoint, out var distanceFromAttachmentPoint, excludeSameGroupId))
                {
                    if(distanceFromAttachmentPoint < minDistance)
                    {
                        closestPoint = closestPointFromAttachmentPoint;
                        localAttachmentPoint = point;
                        minDistance = distanceFromAttachmentPoint;
                        pointFound = true;
                    }
                }
            }
            return pointFound;
        }

        // Closest attachment point for the whole structure
        public bool TryFindStructureClosestAttachmentPoint(out StructurePart currentStructurePart, out AttachmentPoint currentStructureAttachmentPoint, out AttachmentPoint closestPoint, out float minDistance, bool excludeSameGroupId = true)
        {
            currentStructurePart = null;
            var pointFound = TryFindClosestAttachmentPoint(out currentStructureAttachmentPoint, out closestPoint, out minDistance, excludeSameGroupId);
            if (pointFound)
            {
                currentStructurePart = this;
            }
            if (CurrentStructure != null)
            {
                foreach(var part in CurrentStructure.structureParts)
                {
                    if (part == this) continue;
                    if(part.TryFindClosestAttachmentPoint(out var partLocalAttachmentPoint, out var partClosestPoint, out var partMinDistance, excludeSameGroupId))
                    {
                        closestPoint = partClosestPoint;
                        currentStructureAttachmentPoint = partLocalAttachmentPoint;
                        minDistance = partMinDistance;
                        currentStructurePart = part;
                        pointFound = true;
                    }
                }
            }
            return pointFound;
        }
        #endregion

        #region Attachment
        // Plan an attachment request if an attachment point is close enough.
        // This call be called at any time, but the actual attachment will be aligned with FUN (and delayed to when we'll have the state authority on the structure part point that will store the attachment)
        protected void AttachClosestPartInProximity()
        {
            if (Object.HasStateAuthority == false)
            {
                return;
            }
            if (TryFindStructureClosestAttachmentPoint(out StructurePart currentStructureClosestPart, out AttachmentPoint currentStructureClosestPoint, out AttachmentPoint closestExternalPoint, out var distance, excludeSameGroupId: true))
            {
                // Create attachment (will be applied during FUN)
                if (closestExternalPoint is IStructurePartPoint closestExternalStructurePoint)
                {
                    if (currentStructureClosestPart.CurrentStructure == closestExternalStructurePoint.StructurePart.CurrentStructure && currentStructureClosestPart.CurrentStructure != null) throw new System.Exception("Unable to attach: same group");
                    if (closestExternalPoint.IncomingAttachedPoint == currentStructureClosestPoint)
                    {
                        // The 2 parts have been released at the same time, by the same user, so an attachment is already planned, on the other side
                        return;
                    }
                    currentStructureClosestPoint.RequestAttachmentStorage(closestExternalPoint);
                }
            }
        }
        #endregion

        #region Compute structure
        public List<StructurePart> ComputeWholeStructure()
        {
            List<StructurePart> structureParts = new List<StructurePart>();
            ComputeWholeStructureFrom(this, ref structureParts);
            return structureParts;
        }

        public List<StructurePart> ComputeWholeStrucutreExcludingSibling(StructurePart sibling)
        {
            // By preloading the structure with the sibling, its path won't be explored by ComputeWholeStructureFrom (it will be skipped)
            List<StructurePart> structureParts = new List<StructurePart> { sibling };
            ComputeWholeStructureFrom(this, ref structureParts);
            structureParts.Remove(sibling);
            return structureParts;
        }

        static void ComputeWholeStructureFrom(StructurePart part, ref List<StructurePart> structureParts)
        {
            if (structureParts.Contains(part))
            {
                return;
            }
            structureParts.Add(part);
            foreach (var sibling in part.ComputeSiblings())
            {
                ComputeWholeStructureFrom(sibling, ref structureParts);
            }
        }

        public List<StructurePart> ComputeSiblings(bool includeAttachedSiblings = true, bool includeAttachedToSiblings = true)
        {
            List<StructurePart> parts = new List<StructurePart>();

            foreach(var a in attachmentPoints)
            {
                if (includeAttachedSiblings && a.IsAttachementSource && a.AttachedPoint is IStructurePartPoint attachedStructurePoint && parts.Contains(attachedStructurePoint.StructurePart) == false)
                {
                    parts.Add(attachedStructurePoint.StructurePart);
                }
                if (includeAttachedToSiblings && a.IsAttachementTarget && a.attachedToPoint is IStructurePartPoint attachedToStructurePoint && parts.Contains(attachedToStructurePoint.StructurePart) == false)
                {
                    parts.Add(attachedToStructurePoint.StructurePart);
                }
            }
            return parts;
        }

        // Look for the highest order attachment between part1 and part2
        public static bool FindHighestAttachmentOrderBetween(StructurePart part1, StructurePart part2, out StructurePart highestFromPart, out AttachmentPoint highestFromPoint, bool requireStateAuthority)
        {
            var found = part1.FindHighestAttachmentOrderInPathToPart(part2, out highestFromPart, out highestFromPoint, requireStateAuthority);
            return found;
        }

        public bool FindHighestAttachmentOrderInPathToPart(StructurePart searchedPart, out StructurePart highestFromPart, out AttachmentPoint highestFromPoint, bool requireStateAuthority)
        {
            List<StructurePart> analysedParts = new List<StructurePart>();
            return FindHighestAttachmentOrderInPathToPart(searchedPart, out highestFromPart, out highestFromPoint, ref analysedParts, requireStateAuthority);
        }

        // Look for other part amongs related part recursively, to find the attachment with highest order
        public bool FindHighestAttachmentOrderInPathToPart(StructurePart searchedPart, out StructurePart highestFromPart, out AttachmentPoint highestFromPoint, ref List<StructurePart> analysedParts, bool requireStateAuthority)
        {
            highestFromPart = null;
            highestFromPoint = null;
            if (this == searchedPart || analysedParts.Contains(this))
            {
                return false;
            }
            analysedParts.Add(this);
            bool found = false;

            foreach (var a in attachmentPoints)
            {
                if (a.IsAttachementSource && a.AttachedPoint is IStructurePartPoint attachedStructurePoint && analysedParts.Contains(attachedStructurePoint.StructurePart) == false)
                {
                    var attachedPart = attachedStructurePoint.StructurePart;
                    if (searchedPart == attachedPart && (requireStateAuthority == false || attachedPart.HasStateAuthority))
                    {
                        // The other part is an attached point: stop looking for it
                        highestFromPart = this;
                        highestFromPoint = a;
                        found = true;
                        break;
                    }
                    else if (attachedPart.FindHighestAttachmentOrderInPathToPart(searchedPart, out var attachedHighestFrom, out var attachedHighestOrderFromPoint, ref analysedParts, requireStateAuthority))
                    {
                        // otherPart has been recursively found in attachement hierarchy from this sibling
                        var currentMaxOrder = highestFromPoint == null ? 0 : highestFromPoint.Details.attachementOrder;
                        var localOrder = a.Details.attachementOrder;
                        var foundInHierachyOrder = attachedHighestOrderFromPoint.Details.attachementOrder;
                        if (localOrder >= currentMaxOrder && localOrder > foundInHierachyOrder)
                        {
                            // The link to this sibling was in fact the highest order attachment
                            highestFromPart = this;
                            highestFromPoint = a;
                            found = true;
                        }
                        else if (foundInHierachyOrder >= currentMaxOrder)
                        {
                            highestFromPart = attachedHighestFrom;
                            highestFromPoint = attachedHighestOrderFromPoint;
                            found = true;
                        } 
                    }
                }

                if (a.IsAttachementTarget && a.attachedToPoint is IStructurePartPoint attachedToStructurePoint && analysedParts.Contains(attachedToStructurePoint.StructurePart) == false)
                {
                    var attachedToPart = attachedToStructurePoint.StructurePart;
                    var localOrder = a.attachedToPoint.Details.attachementOrder;
                    if (searchedPart == attachedToPart && (requireStateAuthority == false || attachedToPart.HasStateAuthority))
                    {
                        // The other part is the point to which we are attached: stop looking for it
                        if (a.attachedToPoint.AttachedPoint != a) 
                        { 
                            if(deepDebug) Debug.LogError($"[{name}] Incoherent reverse attachment. Due to proxy not knowing an attachment is removed already ? a.HasStateAuth {a.HasStateAuthority}");
                            return false;
                        }
                        highestFromPart = attachedToPart;
                        highestFromPoint = a.attachedToPoint;
                        found = true;
                        break;
                    }
                    else if (attachedToPart.FindHighestAttachmentOrderInPathToPart(searchedPart, out var attachedHighestFrom, out var attachedHighestOrderPoint, ref analysedParts, requireStateAuthority))
                    {
                        // otherPart has been recursively found in attachement hierarchy from this sibling
                        var currentMaxOrder = highestFromPoint == null ? 0 : highestFromPoint.Details.attachementOrder;
                        var foundInHierachyOrder = attachedHighestOrderPoint.Details.attachementOrder;
                        if (localOrder >= currentMaxOrder && localOrder > foundInHierachyOrder)
                        {
                            // The link to this sibling was in fact the highest order attachment
                            if (a.attachedToPoint.AttachedPoint != a)
                            {
                                Debug.LogError($"[{name}] Incoherent reverse attachment. Due to proxy not knowing an attachment is removed already ? a.HasStateAuth {a.HasStateAuthority}");
                                return false;
                            }
                            highestFromPart = attachedToPart;
                            highestFromPoint = a.attachedToPoint;
                            found = true;
                        }
                        else if (foundInHierachyOrder >= currentMaxOrder)
                        {
                            highestFromPart = attachedHighestFrom;
                            highestFromPoint = attachedHighestOrderPoint;
                            found = true;
                        }
                    }
                }
            }
            return found;
        }
        #endregion

        #region Repositioning
        // Rebuild a structure parts position to ensure all siblings reposition according to this part position
        // This method should reposition all sibling to match its position, out of the eventual source sibling having asked for the repositioning
        //
        // If useInitialRoot is true, the snap logic will deal with parts as autonomous objects (won't move parent / siblings with it, with is relevant for repositioning)
        public void RepositionAttachedParts(StructurePart source = null, bool useInitialRoot = true, AttachmentPoint incomingSeparationPoint = null)
        {
            // Make sure we don't reposition twice a same object in a structure in the same call
            if (repositionedInStructureDuringThisPhase) return;
            repositionedInStructureDuringThisPhase = true;

            // We had interpolation on proxies, to be sure the attachment is only applied once the related parts have moved (to avoid an attachment before an ungrabbed part snaps to its new attached part)
            bool shouldInterpolate = Object.HasStateAuthority == false && currentPhase == StructurePhase.OnBeforeRender;
            Tick interpolationTo = default;
            if (shouldInterpolate)
            {
                if (networkTRSP.TryGetSnapshotsBuffers(out _, out var to, out _))
                {
                    interpolationTo = to.Tick;
                    if (interpolationTo < (Runner.Tick - STILL_OBJECT_DETECTION_DURATION_THRESHOLD))
                    {
                        // The networkTRSP has been still for a long time (its buffer has not evolved): we can ignore interpolation of attachment
                        shouldInterpolate = false;
                    }
                }
                else
                {
                    shouldInterpolate = false;
                }
            }
            
            foreach (var a in attachmentPoints)
            {
                if (a.IsPendingAttachementSource(shouldInterpolate, interpolationTo))
                {
                    var relatedPoint = a.PendingAttachedPoint;
                    RepositionRelatedPoint(localPoint: a, relatedPoint: relatedPoint, repositioningSource: source, incomingSeparationPoint: incomingSeparationPoint);
                }
                if (a.IsPendingAttachementTarget(shouldInterpolate, interpolationTo))
                {
                    var relatedPoint = a.PendingAttachedToPoint;
                    RepositionRelatedPoint(localPoint: a, relatedPoint: relatedPoint, repositioningSource: source, incomingSeparationPoint: incomingSeparationPoint);
                }
            }
        }

        // Reposition a structure part containing relatedPoint, so that it matches our position. This relatedPoint is linked to our localPoint
        void RepositionRelatedPoint(AttachmentPoint localPoint, AttachmentPoint relatedPoint, StructurePart repositioningSource, AttachmentPoint incomingSeparationPoint)
        {
            if (localPoint == incomingSeparationPoint || relatedPoint == incomingSeparationPoint)
            {
                // This attachment will soon be broken: we don't walk through it while repositioning
                if(deepDebug) Debug.Log($"Stop propagation to to incoming breaking attachment {localPoint}<->{relatedPoint} (incomingSeparationPoint: {incomingSeparationPoint})");
                return;
            }
            if (relatedPoint is IStructurePartPoint structurePoint)
            {
                if (repositioningSource && structurePoint.StructurePart == repositioningSource)
                {
                    // We skip the link going back (reverse or direct, reverse here) to source, as we "come" from it
                    return;
                }
                if (relatedPoint == incomingSeparationPoint)
                {
                    return;
                }
                relatedPoint.RepositionToMatchRelatedPoint(localPoint);
                structurePoint.StructurePart.RepositionAttachedParts(this);
            }
        }
        #endregion

        #region Debug

        // Editor only method, for debug purposes
        [EditorButton("RepositionStructure")]
        public void RepositionStructure(bool useInitialRoot = true)
        {
            if (CurrentStructure != null)
            {
                CurrentStructure.RepositionStructure();
            }
        }
        #endregion

        #region IContainable
        public bool ShouldBecontained => CurrentStructure != null && CurrentStructure.structureParts.Count >= 2;

        public IEnumerable<IContainable> ContainmentPairs => CurrentStructure.structureParts;

        public int ContainmentPairsCount => CurrentStructure.structureParts.Count;

        public void OnContainmentChange(IContainer container) {
            // To refresh the timer of repositioning post containment change
            CurrentStructure?.OnContainmentChange();
        }

        public IContainmentHandler ContainmentHandler => containmentHandler;

        public bool IsContainmentLeader => IsMoving;

        public object PairsReference => CurrentStructure;
        #endregion

        #region IStructureListener
        public void OnStructureWillBeDestroyed(Structure structure) {
        
        }

        public void OnStructureChange(Structure structure) {
            containmentHandler?.OnContainablePairsChanged();
        }
        #endregion
    }
}
