using Fusion.Addons.Containment;
using Fusion.XRShared.GrabbableMagnet;
using UnityEngine;

namespace Fusion.Addons.StructureCohesion
{
    public class MagnetAttachmentPoint : AttachmentPoint
    {
        AttractableMagnet.AttractorMagnetTagRequirement attractorMagnetTagRequirement = AttractableMagnet.AttractorMagnetTagRequirement.AnyTag;

        protected AttractableMagnet attractableMagnet = null;
        protected AttractorMagnet attractorMagnet = null;
        public IContainable containable;

        // If true, the attachment persistence will be checked based on the unattach criteria every frame
        protected virtual bool ApplyAutomaticMagnetAttachment => true;

        public enum UnattachCriteria { 
            MagnetProximity,
            DistanceChange
        }

        [DrawIf(nameof(ApplyAutomaticMagnetAttachment), Hide = true)]
        public UnattachCriteria unattachCriteria = UnattachCriteria.DistanceChange;

        [DrawIf(nameof(ApplyAutomaticMagnetAttachment), Hide = true)]
        [DrawIf(nameof(unattachCriteria), (long)UnattachCriteria.DistanceChange, CompareOperator.Equal, Hide = true)]
        public float unattachDistance = 0.03f;


        float attachDistance = -1;

        protected override void Awake()
        {
            base.Awake();
            attractableMagnet = GetComponent<AttractableMagnet>();
            attractorMagnet = GetComponent<AttractorMagnet>();
            containable = GetComponentInParent<IContainable>();

            UpdateMagnetsTags();
            if (attractableMagnet && ApplyAutomaticMagnetAttachment)
            {
                attractableMagnet.onSnapToMagnet.AddListener(OnSnapToMagnet);
            }
        }

        public void UpdateMagnetsTags()
        {
            if (attractableMagnet != null)
            {
                attractableMagnet.requiredTagsInAttractor = compatibleAttachmentPointTags;
                attractableMagnet.attractorMagnetTagRequirement = attractorMagnetTagRequirement;
            }

            if (attractorMagnet != null)
            {
                attractorMagnet.tags = attachmentPointTags;
            }
        }

        public override bool TryFindClosestAttachmentPoint(out AttachmentPoint closestPoint, out float minDistance, bool excludeSameGroupId = true)
        {
            closestPoint = null;
            minDistance = float.PositiveInfinity;
            if (attractableMagnet && attractableMagnet.TryFindClosestMagnetInRange(out var otherMagnet, out minDistance, ignoreSameGroupMagnet: excludeSameGroupId))
            {
                return IsValidAttractingMagnet(otherMagnet, out closestPoint);
            }
            return false;
        }

        public virtual bool IsValidAttractingMagnet(IAttractorMagnet otherMagnet, out AttachmentPoint attachmentPoint)
        {
            attachmentPoint = otherMagnet as AttachmentPoint;
            return attachmentPoint != null;
        }

        public override void Snap(AttachmentPoint other)
        {
            if (attractableMagnet == null)
                return;// Attractor only attachment point

            if (other is MagnetStructureAttachmentPoint otherAttachmentPoint)
            {
                attractableMagnet.InstantSnap(otherAttachmentPoint.attractorMagnet);
            }
        }

        #region Automatic attachment creation

        protected virtual void OnSnapToMagnet(IMagnet magnet)
        {
            if (ApplyAutomaticMagnetAttachment)
            {
                var otherMagnetAttachmentPoint = magnet.transform.gameObject.GetComponent<MagnetAttachmentPoint>();
                if ((Object)otherMagnetAttachmentPoint != null && AttachedPoint != otherMagnetAttachmentPoint)
                {
                    RequestAttachmentStorage(otherMagnetAttachmentPoint);
                }
            }
        }

        void CheckAutomaticAttachment()
        {
            if (Object && Object.HasStateAuthority && AttachedPoint != null && AttachedPoint is MagnetAttachmentPoint magnetPoint && (Object)attractableMagnet != null && requestedDeleteAttachmentTarget != AttachedPoint)
            {
                if (unattachCriteria == UnattachCriteria.MagnetProximity)
                {
                    // Note We don't consider same group target to be an invalid link, as we can guess that some subclass logic could decide to move magnets in the same group, and we are not checking if we should snap them, but if the snap is still valid)
                    if (attractableMagnet.IsAttractorMagnetInRange(magnetPoint.attractorMagnet, ignoreSameGroupMagnet: false) == false)
                    {
                        RequestAttachmentDeletion(AttachedPoint);
                        attachDistance = -1;
                    }
                }
                else
                {
                    var distance = Vector3.Distance(AttachedPoint.transform.position, transform.position);
                    if (attachDistance == -1)
                    {
                        // We check if we are still in range, as we could just have had a state auth change
                        if (attractableMagnet.IsAttractorMagnetInRange(magnetPoint.attractorMagnet, ignoreSameGroupMagnet: false))
                        {
                            attachDistance = distance;
                        }
                        else
                        {
                            RequestAttachmentDeletion(AttachedPoint);
                            attachDistance = -1;
                        }
                    }
                    else if (Mathf.Abs(distance - attachDistance) >= unattachDistance)
                    {
                        RequestAttachmentDeletion(AttachedPoint);
                        attachDistance = -1;
                    }
                }
            }
        }
        #endregion

        protected virtual void LateUpdate()
        {
            if (ApplyAutomaticMagnetAttachment)
            {
                CheckAutomaticAttachment();
            }
        }
    }
}