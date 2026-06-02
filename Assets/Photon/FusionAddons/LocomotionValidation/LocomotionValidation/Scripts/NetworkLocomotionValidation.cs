using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Locomotion;
using UnityEngine;

namespace Fusion.Addons.LocomotionValidation
{
    /**
     * Used by HardwareLocomotionValidation if some network component (on the local user prefab) are validator or observers, to forward the call to them
     * Detect if the rig has actually moved to trigger OnDidMove
     */
    [DefaultExecutionOrder(NetworkLocomotionValidation.EXECUTION_ORDER)]

    public class NetworkLocomotionValidation : NetworkBehaviour, ILocomotionValidationHandler
    {
        ChildrenBasedLocomotionValidation childrenBasedLocomotionValidation;
        public const int EXECUTION_ORDER = NetworkRig.EXECUTION_ORDER + 10;
        public float minimalDetectedMove = 0.5f;
        float minimalDetectedMoveSqr = 0.025f;
        Vector3 lastCheckedPosition;

        INetworkRig rig;

        protected void Awake()
        {
            childrenBasedLocomotionValidation = new ChildrenBasedLocomotionValidation(this);
            rig = GetComponent<INetworkRig>();
            if (rig == null)
                Debug.LogError("An NetworkRig is required");

            minimalDetectedMoveSqr = minimalDetectedMove * minimalDetectedMove;
        }

        public override void Render()
        {
            base.Render();
            bool didMove = false;

            // Detect minimal movements, to trigger OnDidMove
            var move = rig.transform.position - lastCheckedPosition;
            if (move.sqrMagnitude >= minimalDetectedMoveSqr)
            {
                didMove = true;
                lastCheckedPosition = rig.transform.position;
            }


            if (didMove)
            {
                OnDidMove();
            }
        }

        #region ILocomotionObserver
        // Forward the ILocomotionValidator.OnDidMove callbacks to any child ILocomotionValidator
        //  Used by the locomotion systems to warn that an actual move did occur
        public void OnDidMove()
        {
            childrenBasedLocomotionValidation.OnDidMove();
        }

        public void OnDidMoveFadeFinished()
        {
            childrenBasedLocomotionValidation.OnDidMove();
        }
        #endregion

        #region ILocomotionValidator
        // Forward the ILocomotionValidator.CanMove request to any child ILocomotionValidator
        //  Used by the locomotion systems to validate if an incoming move is valid
        public bool CanMoveHeadset(Vector3 position)
        {
            if (!childrenBasedLocomotionValidation.CanMoveHeadset(position)) return false;
            return true;
        }
        #endregion
    }
}
