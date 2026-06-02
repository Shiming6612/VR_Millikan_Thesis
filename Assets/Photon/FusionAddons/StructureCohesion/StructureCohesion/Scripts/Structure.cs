using Fusion.Addons.Containment;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.StructureCohesion
{
    public enum StructurePhase { 
        Render,
        FUN,
        AfterTick,
        OnBeforeRender,
        BeforeTick
    }

    public interface IStructureListener
    {
        void OnStructureWillBeDestroyed(Structure structure);
        void OnStructureChange(Structure structure);
    }

    [System.Serializable]
    public class Structure
    {
        public bool deepDebug = false;
        public List<StructurePart> structureParts = new List<StructurePart>();

        public List<StructurePart> movingPartsThisPhase = new List<StructurePart>();

        public float lastStructureChangeTime = -1;

        // During a RepositionStructure, a reference structure part is choosen, to align on part on this one; priority to the moving part, then to the one with the lowest id
        //  But when a part stop moving, the reference part could then change, leading to teleport (as the structure would be computed on a new base).
        // To avoid that, we try as long as possible (last reference in structure, no other moving part) to keep the last reference part as the reference part.
        public StructurePart lastReferencePart = null;

        // Stores the detected (in CheckStructureChanges) attachment that should break due to one (for WeightBasedCohesion break cases) or two moving parts
        AttachmentPoint incomingSeparationPoint = null;

        public float lastReferenceExtendedValidityStart = -1;

        float lastReferencePartMemorizationDuration = 2;

        StructurePhase currentPhase;
        int structureIndex;
        public static int CreatedStructures = 0;
        public int structureHightestWeight = 0;

        List<IStructureListener> listeners = new List<IStructureListener>();

        public const float PostStructureChangeRepositioningPropagationDuration = 2;
        bool wasStable = false;

        // The structure part to use a a starting point to rebuild structure positions
        // If a strcture part is moving, it should be this one. Otherwise, the first one

        public Structure()
        {
            structureIndex = CreatedStructures;
            CreatedStructures++;
        }

        public override string ToString()
        {
            return $"[S.{structureIndex}/parts:{structureParts.Count}]";
        }

        // Return the part with the lowest id (among the moving if possible, or among all otherwise), to ensure determinism among users, so that we all align on the same object
        //
        // If possible (no other moving part, reference still present in the structure), we try to keep the same reference, to avoid repositioning changes when a part stops moving.
        // Note: this last point breaks the determinism if players do not end up electing the same reference. Might need consolidation for edge cases.
        public StructurePart ReferenceStructurePart()
        {
            // If possible, we try to keep the same reference. First, we check if it is still in the structure
            if(lastReferencePart != null && structureParts.Contains(lastReferencePart) == false)
            {
                lastReferencePart = null;
            }

            // To ensure determinism in the end, prioritizing the lastReferencePart should end at some point (in case on non agreement)
            if (lastReferencePart != null && lastReferenceExtendedValidityStart != -1 && (Time.time - lastReferenceExtendedValidityStart) > lastReferencePartMemorizationDuration)
            {
                //Debug.LogError("End of extended validity of memorized last reference");
                lastReferencePart = null;
            }

            StructurePart reference = lastReferencePart;
            uint lowestObjectId = uint.MaxValue;
            bool extendedLastReferenceValidity = false;
            foreach(var p in structureParts)
            {
                // If a candidate reference exists, is moving, and not this one, we skip it
                if (reference && reference.IsMoving && p.IsMoving == false)
                {
                    continue;
                }
                // If no reference is choosen yet, this one becomes the candidate
                bool isNewreference = reference == null;
                // If the previous reference part is not moving, and this part moves, this one becomes the candidate
                if (isNewreference == false && reference != null && reference.IsMoving == false && p.IsMoving == true)
                {
                    isNewreference = true;
                }
                // If neither the current candidate part or this one move, we select the one with the lowest id ...
                if (isNewreference == false && reference.Object.Id.Raw > p.Object.Id.Raw)
                {
                    // ... unless the reference is the last selected reference: we keep it then
                    isNewreference = reference != lastReferencePart;
                    if (isNewreference == false)
                    {
                        // We ignored the determinist reference to reuse the lastReference. We will do it for a short duration only, to ensure determinism in the end
                        extendedLastReferenceValidity = true;
                    }
                }

                if (isNewreference)
                {
                    reference = p;
                    lowestObjectId = p.Object.Id.Raw;
                }
            }

            if (extendedLastReferenceValidity)
            {
                // We should have changed the reference, but kept the last reference: we start counting the extended duration of the lastReference validity
                if (lastReferenceExtendedValidityStart == -1)
                {
                    //Debug.LogError("Start of extended validity of memorized last reference");
                    lastReferenceExtendedValidityStart = Time.time;
                }
            }
            else
            {
                lastReferenceExtendedValidityStart = -1;
            }

            lastReferencePart = reference;
            return reference;
        }

        #region Listener
        public void RegisterListener(IStructureListener listener)
        {
            if (listeners.Contains(listener)) return;
            listeners.Add(listener);
        }
        public void UnregisterListener(IStructureListener listener)
        {
            if (listeners.Contains(listener) == false) return;
            listeners.Remove(listener);
        }
        #endregion

        public void OnStructureChange()
        {
            lastStructureChangeTime = Time.time;
            foreach (var listener in listeners) listener.OnStructureChange(this);
        }

        public void OnContainmentChange()
        {
            lastStructureChangeTime = Time.time;
        }

        // Rebuild the structure position
        public void RepositionStructure(AttachmentPoint incomingSeparationPoint = null)
        {
            var reference = ReferenceStructurePart();
            reference?.RepositionAttachedParts(useInitialRoot: true, incomingSeparationPoint: incomingSeparationPoint);
        }

        public void WillBeDestroyed()
        {
            foreach (var listener in listeners) listener.OnStructureWillBeDestroyed(this);
        }

        public void AddStructurePart(StructurePart part)
        {
            if (deepDebug) Debug.LogError("AddStructurePart " + part);
            if (structureParts.Contains(part) == false)
            {
                structureParts.Add(part);
            }       
            if(part is IStructureListener listener)
            {
                RegisterListener(listener);
            }
            part.ChangeStructure(this);
            if(part.partWeight > structureHightestWeight)
            {
                structureHightestWeight = part.partWeight;
            }
            OnStructureChange();
        }

        public void RemovePart(StructurePart part)
        {
            if (deepDebug) Debug.LogError("RemovePart "+ part);
            structureParts.Remove(part);
            if (part is IStructureListener listener)
            {
                UnregisterListener(listener);
            }
            if (part.CurrentStructure == this)
            {
                part.ChangeStructure(null);
            } else
            {
                if (deepDebug) Debug.LogError($"!!! Did not changed structure, not matching: {part} / {part.CurrentStructure} / {this}");
            }
            movingPartsThisPhase.Remove(part);

            if (part.partWeight == structureHightestWeight)
            {
                structureHightestWeight = 0;
                foreach (var p in structureParts)
                {
                    if (p.partWeight > structureHightestWeight) 
                        structureHightestWeight = p.partWeight;
                }
            }
            OnStructureChange();
        }

        public void UpdateStructureForNewAttachment(StructurePart from, StructurePart to, AttachmentDetails details)
        {
            if(structureParts.Contains(from) == false)
            {
                AddStructurePart(from);
            }
            if (structureParts.Contains(to) == false)
            {
                AddStructurePart(to);
            }
            OnStructureChange();
        }

        public void MergeStructure(Structure mergingStructure)
        {
            foreach(var part in mergingStructure.structureParts)
            {
                AddStructurePart(part);
            }
            while (mergingStructure.structureParts.Count > 0)
            {
                var part = mergingStructure.structureParts[mergingStructure.structureParts.Count - 1];
                mergingStructure.RemovePart(part);
            }
            OnStructureChange();
        }

        AttachmentPoint FindLocalSeparationPoint(List<StructurePart> parts)
        {
            if (parts.Count < 2) return null;
            if(StructurePart.FindHighestAttachmentOrderBetween(parts[0], parts[1], out StructurePart highestFromPart, out AttachmentPoint highestFromPoint, requireStateAuthority: false))
            {
                return highestFromPoint;
            }

            return null;
        }

        // Determine how many parts moved during this phase and updates movingPartsThisPhase
        public void CheckMovingParts()
        {
            foreach (var part in structureParts)
            {
                if (part.IsMoving && movingPartsThisPhase.Contains(part) == false)
                {
                    movingPartsThisPhase.Add(part);
                }
            }
        }

        // Detect which attachment should break due to one (for WeightBasedCohesion break cases) or two moving parts
        public void CheckStructureChanges()
        {
            // Split part if needed
            if (movingPartsThisPhase.Count == 2)
            {
                incomingSeparationPoint = FindLocalSeparationPoint(movingPartsThisPhase);
            }             
            else if(movingPartsThisPhase.Count == 1)
            {
                // Check weight based split
                var movingPart = movingPartsThisPhase[0];
                if (movingPart.structuralCohesionMode == StructuralCohesionMode.WeightBasedCohesion && movingPart.partWeight < structureHightestWeight)
                {
                    StructurePart simulatedMovingSibling = null;
                    uint highestOrder = 0;
                    int siblingsHighestWeight = -10_000;
                    // We split first on the highest weight, then in case of equality, on the highest order (most recent attachment)
                    foreach(var a in movingPart.attachmentPoints)
                    {
                        StructurePart sibling = null;
                        uint siblingOrder = 0;
                        if (a.AttachedPoint != null &&  a.AttachedPoint is IStructurePartPoint structurePoint1)
                        {
                            sibling = structurePoint1.StructurePart;
                            siblingOrder = a.Details.attachementOrder;

                        }
                        if (a.attachedToPoint != null && a.attachedToPoint is IStructurePartPoint structurePoint2)
                        {
                            sibling = structurePoint2.StructurePart;
                            siblingOrder = a.attachedToPoint.Details.attachementOrder;
                        }

                        if (sibling)
                        {
                            var siblingWeight = sibling.partWeight;
                            // We consider the weight of this sibling as the weight of the heaviest structure part on its "side" of the structure if we would cut it between movingPart and sibling
                            var siblingHalfStructureParts = sibling.ComputeWholeStrucutreExcludingSibling(movingPart);
                            foreach (var siblingHalfStructurePart in siblingHalfStructureParts)
                            {
                                if(siblingHalfStructurePart.partWeight > siblingWeight)
                                {
                                    siblingWeight = siblingHalfStructurePart.partWeight;
                                }
                            }
                            if (siblingWeight > siblingsHighestWeight)
                            {
                                siblingsHighestWeight = siblingWeight;
                                highestOrder = siblingOrder;
                                simulatedMovingSibling = sibling;
                            }
                            else if (siblingWeight == siblingsHighestWeight && siblingOrder > highestOrder)
                            {
                                siblingsHighestWeight = siblingWeight;
                                highestOrder = siblingOrder;
                                simulatedMovingSibling = sibling;
                            }
                        }
                    }
                    if (simulatedMovingSibling)
                    {
                        incomingSeparationPoint = FindLocalSeparationPoint(new List<StructurePart> { movingPart, simulatedMovingSibling });
                    }
                }
            }
        }

        void ApplySeparationPoint()
        {
            if(incomingSeparationPoint != null)
            {
                // should we split, expect someone else to do it, or take authority on the splitting point ?
                if (incomingSeparationPoint.HasStateAuthority)
                {
                    if (deepDebug && incomingSeparationPoint is IStructurePartPoint splitStructurePoint) Debug.LogError($"Separating {splitStructurePoint.StructurePart}-{incomingSeparationPoint}|{incomingSeparationPoint.Id} -({incomingSeparationPoint.Details.attachementOrder})> {incomingSeparationPoint.AttachedPoint.Id}");
                    // We do not need to use a RequestAttachmentDeletion call to align with FUN, as ApplySeparationPoint should only be called during FUN (AfterTick in fact), so deletying right now is fine
                    incomingSeparationPoint.DeleteAttachment(incomingSeparationPoint.AttachedPoint);
                }
                OnStructureChange();
                incomingSeparationPoint = null;
            }
        }

        public void OnPhaseStart(StructurePhase phase)
        {
            if (currentPhase == phase)
            {
                // Phase already handled
                return;
            }
            currentPhase = phase;
            if (phase == StructurePhase.FUN || phase == StructurePhase.Render || phase == StructurePhase.BeforeTick)
            {
                // Nothing to do at the start of FUN, nor at the beginnning of Render
                return;
            }

            // Find moving parts
            CheckMovingParts();
            // Check if the structure should break in 2
            CheckStructureChanges();

            if (phase == StructurePhase.AfterTick)
            {
                // Split the structure in 2 if needed (only during FUN aligned phase)
                ApplySeparationPoint();
            }

            bool stableStructure = AnalyseStructureStability();

            if (stableStructure == false)
            {
                // Reposition parts to keep the structure together, if needed
                RepositionStructure(incomingSeparationPoint);
            }

            movingPartsThisPhase.Clear();
        }

        // Check if the structure is stable (no need to reposition its part).
        // If containment is not used, it will never bee stable, unless no part is moving for a while
        // Handle updating lastStructureChangeTime
        bool AnalyseStructureStability()
        {
            bool stableStructure = false;
            bool structureContained = true;
            // We do not allow stillness stability when containement is enabled
            bool stillnessStabilityAllowed = (StructureManager.SharedInstance && StructureManager.SharedInstance.allowStabilisationDueToStillness) && (ContainmentManager.SharedInstanceAvailable == false || ContainmentManager.SharedInstance.disableContainerParenting);
            bool structureIsStill = stillnessStabilityAllowed;

            foreach (var part in structureParts)
            {
                if ((UnityEngine.Object)part.ContainmentHandler?.ConfirmedContainer == null)
                {
                    structureContained = false;
                }
                if (stillnessStabilityAllowed && structureIsStill && part.isStill == false)
                {
                    structureIsStill = false;
                }
            }

            if (structureIsStill || structureContained)
            {
                stableStructure = true;
            }

            // Check if we should reposition part relatively to each other, taking a reference part as the starting point
            if (stableStructure)
            {
                bool recentStructureChange = false;
                if (lastStructureChangeTime != -1)
                {
                    if ((Time.time - lastStructureChangeTime) < PostStructureChangeRepositioningPropagationDuration)
                    {
                        recentStructureChange = true;
                    }
                    else
                    {
                        lastStructureChangeTime = -1;
                    }
                }
                stableStructure = recentStructureChange == false;
            }

            if (stillnessStabilityAllowed && structureIsStill && stableStructure && wasStable == false && structureContained == false)
            {
                if(deepDebug) Debug.LogError("Structure is becoming stable due to its parts being still");
            }

            wasStable = stableStructure;
            return stableStructure;
        }
    }
}
