using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Locomotion;
using UnityEngine;

namespace Fusion.Addons.Hover
{
    public interface IBeamHoverListener
    {
        void OnHoverStart(BeamHoverer beamToucher);
        void OnHoverEnd(BeamHoverer beamToucher);
        void OnHoverRelease(BeamHoverer beamToucher);
    }

    public class BeamHoverer : MonoBehaviour
    {
        RayBeamer beamer;
        IBeamHoverListener hoverListener;
        Collider latestHitCollider;
        public IHapticFeedbackProviderRigPart hapticFeedbackProvider;

        private void Awake()
        {
            hapticFeedbackProvider = GetComponentInParent<IHapticFeedbackProviderRigPart>();
            beamer = GetComponentInChildren<RayBeamer>();
            beamer.onHitEnter.AddListener(OnHitEnter);
            beamer.onHitExit.AddListener(OnHitExit);
            beamer.onRelease.AddListener(OnRelease);
        }

        #region RayBeamer callbacks
        private void OnRelease(Collider collider, Vector3 hitPoint)
        {
            if (hoverListener != null)
            {
                hoverListener.OnHoverRelease(this);
            }
            ResetHoverInfo();
        }

        private void OnHitExit(Collider collider, Vector3 hitPoint)
        {
            ResetHoverInfo();
        }

        private void OnHitEnter(Collider collider, Vector3 hitPoint)
        {
            if (latestHitCollider != collider)
            {
                ResetHoverInfo();
            }
            latestHitCollider = collider;
            hoverListener = latestHitCollider.GetComponentInChildren<IBeamHoverListener>();
            if (hoverListener != null)
            {
                hoverListener.OnHoverStart(this);
            }
        }
        #endregion
        void ResetHoverInfo()
        {
            latestHitCollider = null;
            if (hoverListener != null)
            {
                hoverListener.OnHoverEnd(this);
                hoverListener = null;
            }
        }
    }
}
