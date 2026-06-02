using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Touch;
using Fusion.XR.Shared.Locomotion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fusion.Addons.Touch
{
    /***
     * 
     * BeamToucher simulates a touch when the player uses the beam and presses the trigger button
     * It is used to interact with Touchable objects
     * 
     ***/
    public class BeamToucher : MonoBehaviour
    {
        [System.Flags]
        public enum TouchableComponents
        {
            TouchableButton = 1,
        }

        public TouchableComponents touchableComponents = TouchableComponents.TouchableButton;
        public bool ShouldTouchTouchable => (touchableComponents & TouchableComponents.TouchableButton) != 0;

        public enum TouchMode
        {
            WasPressedThisFrame,
            WasReleasedThisFrame
        }
        public TouchMode touchMode = TouchMode.WasPressedThisFrame;

        RayBeamer beamer;
        Collider latestHitCollider;

        // Cache attributes, to limit the number of GetComponent calls
        ITouchable touchable;
        bool noTouchableButtonFound = false;

        public InputActionProperty useAction;

        private void Awake()
        {
            beamer = GetComponentInChildren<RayBeamer>();
            beamer.onHitEnter.AddListener(OnHitEnter);
            beamer.onHitExit.AddListener(OnHitExit);
            beamer.onRelease.AddListener(OnRelease);
        }

        private void Start()
        {
            useAction.EnableWithDefaultXRBindings(side: beamer.rigPart.Side, new List<string> { "trigger" });
        }

        #region RayBeamer callbacks
        private void OnRelease(Collider collider, Vector3 hitPoint)
        {
            ResetColliderInfo();
        }

        private void OnHitExit(Collider collider, Vector3 hitPoint)
        {
            ResetColliderInfo();
        }

        private void OnHitEnter(Collider collider, Vector3 hitPoint)
        {
            if (latestHitCollider != collider)
            {
                ResetColliderInfo();
            }
            latestHitCollider = collider;
        }
        #endregion

        void ResetColliderInfo()
        {
            latestHitCollider = null;
            touchable = null;
            noTouchableButtonFound = false;
        }

        private void Update()
        {
            if (latestHitCollider == false) return;
            bool used = touchMode == TouchMode.WasPressedThisFrame ? useAction.action.WasPressedThisFrame() : useAction.action.WasReleasedThisFrame();

            if (used)
            {
                if (ShouldTouchTouchable && noTouchableButtonFound == false)
                {

                    if (touchable == null)
                        touchable = latestHitCollider.GetComponentInParent<ITouchable>();

                    if (touchable != null)
                    {
                        touchable.OnToucherContactStart(null);
                        if(beamer.rigPart is IHapticFeedbackProviderRigPart hapticFeedbackProviderRigPart && touchable is IHapticConsumer consumer)
                        {
                            consumer.HapticFeedback(hapticFeedbackProviderRigPart);
                        }
                            
                        touchable.OnToucherContactEnd(null);
                    }
                    else
                    {
                        noTouchableButtonFound = true;
                    }
                        
                }
            }
        }
    }
}