using Fusion.XR.Shared;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.StructureCohesion
{
    /*
     * Stores a reference to all the structures
     * Creates the Structure containers based on referenced prefab
     * Centralize structure edition based an attachment and parts changes
     */
    public class StructureManager : MonoBehaviour
    {                
        #region Shared instance
        static StructureManager _sharedInstance;

        public List<Structure> structures = new List<Structure>();

        public bool deepDebug = false;

        [Tooltip("If true, a structure won't be repositioned when all its part are not moving (disabled if containment is used). Limite repositioning calls")]
        public bool allowStabilisationDueToStillness = true;

        public static StructureManager SharedInstance
        {
            get
            {
                if(_sharedInstance == null)
                {
                    Debug.LogError("Missing StructureManager");
                }
                return _sharedInstance;
            }
        }

        private void Awake()
        {
            if (_sharedInstance != null)
            {
                Debug.LogError("Multiple StructureManager is not authorized");
            }
            _sharedInstance = this;
        }

        private void OnDestroy()
        {
            if (_sharedInstance == this) _sharedInstance = null;
        }
        #endregion

        #region Structure update API
        public void UpdateStructureForNewAttachment(StructurePart from, StructurePart to, AttachmentDetails attachmentDetails)
        {
            if(from.CurrentStructure == to.CurrentStructure && from.CurrentStructure != null)
            {
                return;
            }
            Structure structure = null;
            if (to.CurrentStructure == null)
            {
                if (from.CurrentStructure == null)
                {
                    // New structure
                    CreateStructure(partCausingStructureCreation: from, causedByDeletedAttachment: false, out structure);
                }
                else
                {
                    structure = from.CurrentStructure;
                }
            }
            else
            {
                structure = to.CurrentStructure;
                if(from.CurrentStructure != null && from.CurrentStructure != to.CurrentStructure)
                {
                    if (deepDebug) Debug.LogError("[Structure logic] Merging");
                    // Merge the 2 structures
                    var fromStructure = from.CurrentStructure;
                    to.CurrentStructure.MergeStructure(from.CurrentStructure);
                    structure = to.CurrentStructure;
                    Destroy(fromStructure);
                }
            }
            structure.UpdateStructureForNewAttachment(from, to, attachmentDetails);
        }

        public void UpdateStructureForDeletedAttachment(StructurePart from, StructurePart to)
        {
            if(from.CurrentStructure != to.CurrentStructure)
            {
                return;
            }
            List<StructurePart> fromStructureParts = from.ComputeWholeStructure();
            List<StructurePart> toStructureParts = to.ComputeWholeStructure();

            var newStructParts = fromStructureParts;
            var remaingingStructParts = toStructureParts;
                        
            MoveToNewStructure(newStructParts, from);

            if (deepDebug) Debug.LogError($"Splitting structure in {newStructParts.Count} / {remaingingStructParts.Count}");
            
            if (remaingingStructParts.Count == 1)
            {
                if (deepDebug) Debug.LogError("[remaingingStructParts] Structure with just one part: destroying it");
                var lastPart = remaingingStructParts[0];
                var structure = lastPart.CurrentStructure;
                if (structure != null)
                {
                    RemovePart(structure, lastPart);
                }
                else
                {
                    if (deepDebug) Debug.LogError($"!!! No structure for: {lastPart} (parent: {lastPart.transform.parent}");
                }
            }
        }

        void RemovePart(Structure structure, StructurePart part)
        {
            structure.RemovePart(part);
            if(structure.structureParts.Count == 0)
            {
                Destroy(structure);
            }
        }
        void Destroy(Structure structure)
        {
            structure.WillBeDestroyed();
            structures.Remove(structure);

        }

        public void UpdateStructureForDestroyedStructurePart(StructurePart part)
        {
            if (part.CurrentStructure != null)
            {
                var structure = part.CurrentStructure;
                RemovePart(part.CurrentStructure, part);
                if (structure.structureParts.Count == 1)
                {
                    if (deepDebug) Debug.LogError("[UpdateStructureForDestroyedStructurePart] Structure with just one part: destroying it");
                    var lastPart = structure.structureParts[0];
                    RemovePart(structure, lastPart);
                }
            }
        }
        #endregion

        #region Internal logic
        void CreateStructure(StructurePart partCausingStructureCreation, bool causedByDeletedAttachment, out Structure structure)
        {
            structure = new Structure();
            structure.deepDebug = deepDebug;
            structures.Add(structure);
            if (deepDebug) Debug.LogError("[Structure logic] New Structure (" + structures.Count + " structures): " + structure);
        }

        void MoveToNewStructure(List<StructurePart> movingParts, StructurePart partCausingMove)
        {
            Structure structure = null;
            if (movingParts.Count == 1)
            {
                if (deepDebug) Debug.LogError("[New structure] Structure with just one part: not creating it");
            }
            else
            {
                CreateStructure(partCausingStructureCreation: partCausingMove, causedByDeletedAttachment: true, out structure);
            }

            foreach (var part in movingParts)
            {
                if (part.CurrentStructure != null)
                {
                    if (deepDebug) Debug.LogError("Removing from ancient struct " + part);
                    RemovePart(part.CurrentStructure, part);
                }
                if (structure != null)
                {
                    if (structure == null)
                        if (deepDebug) Debug.LogError($"Moving alone " + part);
                        else
                        if (deepDebug) Debug.LogError($"Moving from ancient struct ({structures.IndexOf(structure)}) " + part);
                    structure.AddStructurePart(part);
                }
            }
        }
        #endregion
    }

}
