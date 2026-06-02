using Fusion.XRShared.GrabbableMagnet;

namespace Fusion.Addons.StructureCohesion
{
    public class MagnetStructureAttachmentPoint : MagnetAttachmentPoint, IStructurePartPoint, IMagnetConfigurator
    {
        #region Structure
        public Structure CurrentStructure => StructurePart == null ? null : StructurePart.CurrentStructure;
        public StructurePart StructurePart { get; set; } = null;
        #endregion

        #region IMagnetGroupIdentificator
        public bool IsInSameGroup(IMagnetConfigurator otherIdentificator)
        {
            if (otherIdentificator is MagnetStructureAttachmentPoint otherAttachmentPoint)
            {
                if (CurrentStructure == null && otherAttachmentPoint == this)
                {
                    return true;
                }
                if (CurrentStructure != null && otherAttachmentPoint.CurrentStructure == CurrentStructure)
                {
                    return true;
                }
            }
            return false;
        }
        
        public bool IsMagnetActive()
        {
            if (AttachedPoint != null || attachedToPoint != null || requestedNewAttachedPoint != null)
            {
                return false;
            }
            return true;
        }
        #endregion
        protected override bool ApplyAutomaticMagnetAttachment => false;

        protected override void Awake()
        {
            base.Awake();
            StructurePart = GetComponentInParent<StructurePart>();
            if (attractableMagnet != null)
            {
                attractableMagnet.CheckOnUngrab = false;
                attractableMagnet.MagnetConfigurator = this;
            }

            if (attractorMagnet != null)
            {
                attractorMagnet.MagnetConfigurator = this;
            }
        }

        public override bool IsValidAttractingMagnet(IAttractorMagnet otherMagnet, out AttachmentPoint attachmentPoint)
        {
            var structurePoint = otherMagnet.MagnetConfigurator as MagnetStructureAttachmentPoint;
            attachmentPoint = structurePoint;
            return structurePoint != null;
        }
    }
}

