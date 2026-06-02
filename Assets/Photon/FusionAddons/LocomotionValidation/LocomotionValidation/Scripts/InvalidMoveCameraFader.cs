using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Interaction;
using Fusion.XR.Shared.Locomotion;
using UnityEngine;

namespace Fusion.Addons.LocomotionValidation
{
    /*
     * Handles view fading related to movement validation
     * If checkHeadMovements and fadeIfMovingHeadInInvalidZone are true, when the user moves its head in an invalid zone (according to any ILocomotionValidator)
     *  the view will fade until they go out of the invalid zone.
     */
    public class InvalidMoveCameraFader : MonoBehaviour, ILocomotionObserver
    {
        [Header("Headset movement analysis (for zones handling mainly)")]
        public bool fadeIfMovingHeadInInvalidZone = true;

        bool isInInvalidZone = false;
        IHardwareHeadset hardwareHeadset;
        HardwareLocomotionValidation hardwareLocomotionValidation;

        private void Awake()
        {
            hardwareHeadset = GetComponentInChildren<IHardwareHeadset>();
            hardwareLocomotionValidation = GetComponent<HardwareLocomotionValidation>();
        }

        #region ILocomotionObserver
        // Check if the head enters a forbidden Zone, and fade the view if it occurs (if checkHeadMovements is set to ture)
        public void OnDidMove()
        {
            if (fadeIfMovingHeadInInvalidZone && hardwareHeadset is IFadeable fadeable && fadeable.Fader != null)
            {
                if (hardwareLocomotionValidation.CanMoveHeadset(hardwareHeadset.transform.position) == false)
                {
                    if (isInInvalidZone == false)
                    {
                        isInInvalidZone = true;
                        StartCoroutine(fadeable.Fader.FadeIn());
                    }
                }
                else
                {
                    if (isInInvalidZone)
                    {
                        isInInvalidZone = false;
                        StartCoroutine(fadeable.Fader.FadeOut());
                    }
                }
            }
        }

        public void OnDidMoveFadeFinished()
        {
        }
        #endregion
    }
}


