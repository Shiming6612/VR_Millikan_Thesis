using Fusion.XR.Shared.Core.Interaction;
using UnityEngine;

namespace Fusion.XR.Shared.Core.Tools
{
    public class IndexTipMarker : MonoBehaviour, IInteractionTip, IRigPartPositionModifier
    {
        [Header("UI Interaction option (with XSCInputModule)")]
        public float _maxStartInteractionDistance = 0.01f;
        public float _maxMaintainInteractionDepth = 0.2f;
        public float _maxInteractionScanDistance = 0.2f;
        public bool modifyRigPartPositionOnMaintenedInteraction = true;

        public IRigPart rigPart;

        public Transform directionTransform;

        #region IInteractionTip
        public float MaxStartInteractionDistance => _maxStartInteractionDistance;
        public float MaxMaintainInteractionDepth => _maxMaintainInteractionDepth;
        public float MaxInteractionScanDistance => _maxInteractionScanDistance;

        public IRigPart RigPart => rigPart;

        public bool CanInteract => enabled && ((rigPart is IHardwareRigPart hrp) ? hrp.TrackingStatus == RigPartTrackingstatus.Tracked:true);

        public bool IsSelecting => true;

        public Vector3 Origin => transform.position;

        public Quaternion Rotation => directionTransform.rotation;
        public virtual Vector2 ScrollDelta => Vector2.zero;
        public IInteractionDetailsProvider LastInteractionDetailProvider { get; set; } = null;
        #endregion

        #region IRigPartPositionModifier
        public Vector3 PositionModification {
            get
            {
                if (modifyRigPartPositionOnMaintenedInteraction && LastInteractionDetailProvider != null && LastInteractionDetailProvider.IsMaintainedInteraction && LastInteractionDetailProvider.Target != null)
                {
                    return -LastInteractionDetailProvider.MaintainDepth * LastInteractionDetailProvider.Target.transform.forward;
                }
                return Vector3.zero;
            }
        }
        #endregion


        private void Awake()
        {
            if (directionTransform == null) directionTransform = transform;
        }

        private void Update()
        {
            if (rigPart == null) rigPart = GetComponentInParent<IRigPart>();
        }
    }
}
