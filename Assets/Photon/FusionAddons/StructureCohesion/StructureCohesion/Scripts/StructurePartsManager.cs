using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.StructureCohesion
{
    /// <summary>
    /// This class is used to maintain a list of StructureParts and associated attachment point tags.
    /// Theses caches allow to display a visual guide between compatible attachment points when a user is grabbing a part with a minimal impact on performance.
    /// </summary>
    public class StructurePartsManager : MonoBehaviour
    {
        static StructurePartsManager _sharedInstance;

        StructureManager structureManager;
        public List<StructurePart> structureParts = new List<StructurePart>();
        public Dictionary<string, List<AttachmentPoint>> attachmentPointTags = new Dictionary<string, List<AttachmentPoint>>();

        [System.Serializable]
        public class TagCacheEntry
        {
            public string tag;
            public List<AttachmentPoint> attachmentPoints;
        }

        [SerializeField]
        private List<TagCacheEntry> tagCacheList = new List<TagCacheEntry>();
        public static StructurePartsManager SharedInstance
        {
            get
            {
                if (_sharedInstance == null) Debug.LogError("Missing StructurePartsManager");
                return _sharedInstance;
            }
        }

        private void Awake()
        {
            if (_sharedInstance != null)
            {
                Debug.LogError("Multiple StructurePartsManager is not authorized");
            }
            _sharedInstance = this;

            if (structureManager == null)
            {
                structureManager = GetComponent<StructureManager>();
            }
            if (structureManager == null)
                Debug.LogError("Structure Manager not found");
        }


        public void RegisterStructurePart(StructurePart structurePart)
        {
            // check if the structurePart should be added in the list
            if (structureParts.Contains(structurePart)) return;
            structureParts.Add(structurePart);

            // Add the structurePart attachmentPoints tags in the tag cache
            foreach (var attachementPoint in structurePart.attachmentPoints)
            {
                foreach (var tag in attachementPoint.attachmentPointTags)
                {
                    // If the tag already exists in the dictionary, add the attachmentPoint to its list
                    if (attachmentPointTags.ContainsKey(tag))
                    {
                        if (!attachmentPointTags[tag].Contains(attachementPoint))
                        {
                            attachmentPointTags[tag].Add(attachementPoint);
                        }
                    }
                    else
                    {
                        // If the tag does not exist, create a new list and add the attachmentPoint
                        attachmentPointTags[tag] = new List<AttachmentPoint> { attachementPoint };
                    }
                }
            }

            UpdateTagCacheList();
        }

        public void UnegisterStructurePart(StructurePart structurePart)
        {
            if (structureParts.Contains(structurePart) == false) return;
            structureParts.Remove(structurePart);

            foreach (var attachementPoint in structurePart.attachmentPoints)
            {
                foreach (var tag in attachementPoint.attachmentPointTags)
                {
                    if (attachmentPointTags.ContainsKey(tag))
                    {
                        if (attachmentPointTags[tag].Contains(attachementPoint))
                        {
                            attachmentPointTags[tag].Remove(attachementPoint);
                            if (attachmentPointTags[tag].Count == 0)
                            {
                                attachmentPointTags.Remove(tag);
                            }
                        }
                    }
                }
            }
        }

        public void UpdateTagCacheList()
        {
            tagCacheList.Clear();
            foreach (var attachmentPointTag in attachmentPointTags)
            {
                tagCacheList.Add(new TagCacheEntry
                {
                    tag = attachmentPointTag.Key,
                    attachmentPoints = attachmentPointTag.Value
                });
            }
        }

        private void OnDestroy()
        {
            if (_sharedInstance == this) _sharedInstance = null;
        }

    }

}