using UnityEngine;
#if XRIT_ENABLED
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#endif

namespace Fusion.XR.Shared.XRIT
{
#if XRIT_ENABLED
    public class DebugXRITGrabInteractable : XRGrabInteractable
    {
        [SerializeField] bool debugProcessInteractablePosition = false;
        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            if (debugProcessInteractablePosition) Debug.LogError($"[ProcessInteractable] Start {updatePhase}: {transform.position}  (parent:{transform.parent})");
            base.ProcessInteractable(updatePhase);
            if (debugProcessInteractablePosition) Debug.LogError($"[ProcessInteractable] End {updatePhase}: {transform.position}  (parent:{transform.parent})");
        }
    }
#else
    public class DebugXRITGrabInteractable : MonoBehaviour {}
#endif


}


