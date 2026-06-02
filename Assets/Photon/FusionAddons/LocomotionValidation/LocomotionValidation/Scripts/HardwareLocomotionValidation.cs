using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Locomotion;
using Fusion.XR.Shared.Rig;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.LocomotionValidation
{

    /**
      * Handle locomotion limitations by forwarding the ILocomotionValidator and ILocomotionObserver calls to its children implementing the interface 
      */
    public class ChildrenBasedLocomotionValidation : ILocomotionValidationHandler
    {

        List<ILocomotionValidator> locomotionValidators = new List<ILocomotionValidator>();
        List<ILocomotionObserver> locomotionObservers = new List<ILocomotionObserver>();

        public ChildrenBasedLocomotionValidation(MonoBehaviour behaviour) 
        { 
            foreach (var v in behaviour.GetComponentsInChildren<ILocomotionValidator>())
            {
                if ((object)v != behaviour) locomotionValidators.Add(v);
            }
            foreach (var v in behaviour.GetComponentsInChildren<ILocomotionObserver>())
            {
                if ((object)v != behaviour) locomotionObservers.Add(v);
            }
        }

        #region ILocomotionObserver
        // Forward the ILocomotionValidator.OnDidMove callbacks to any child ILocomotionValidator
        // Used by the locomotion systems to warn that an actual move did occur
        public virtual void OnDidMove()
        {
            ChildrenOnDidMove();
        }

        protected void ChildrenOnDidMove()
        {
            foreach (var validator in locomotionObservers)
            {
                validator.OnDidMove();
            }
        }

        public virtual void OnDidMoveFadeFinished()
        {
            ChildrenOnDidMoveFadeFinished();
        }

        protected void ChildrenOnDidMoveFadeFinished()
        { 
            foreach (var validator in locomotionObservers)
            {
                validator.OnDidMoveFadeFinished();
            }
        }
        #endregion

        #region ILocomotionValidator
        // Forward the ILocomotionValidator.CanMove request to any child ILocomotionValidator and return false if any of them returns false
        //  Used by the locomotion systems to validate if an incoming move is valid
        public virtual bool CanMoveHeadset(Vector3 position)
        {
            if (!ChildrenCanMoveHeadset(position)) return false;
            return true;
        }

        public bool ChildrenCanMoveHeadset(Vector3 position)
        {
            foreach (var validator in locomotionValidators)
            {
                if (!validator.CanMoveHeadset(position))
                {
                    return false;
                }
            }
            return true;
        }
        #endregion
    }


    /**
     * Handle locomotion limitations for the HardwareRig: check on HardwareRig children, then on NetworkRig children if it has a NetworkLocomotionValidation component
     */
    public class HardwareLocomotionValidation : MonoBehaviour, ILocomotionValidationHandler
    {
        ChildrenBasedLocomotionValidation childrenBasedLocomotionValidation;

        [Header("Headset movement analysis")]
        public bool checkHeadMovements = true;

        Vector3 lastHeadMovement;
        float minHeadMovementDetected = 0.05f;
        float minHeadMovementDetectedSqr;

        IHardwareRig hardwareRig;
        RigInfo rigInfo;
        bool didSearchNetworkLocomotionValidation = false;
        NetworkLocomotionValidation _networkLocomotionValidation;
        NetworkLocomotionValidation NetworkLocomotionValidation
        {
            get
            {
                if (rigInfo == null)
                {
                    if (hardwareRig != null) rigInfo = RigInfo.FindRigInfo(allowSceneSearch: true);
                }
                if (rigInfo && rigInfo.localNetworkedRig?.gameObject && !didSearchNetworkLocomotionValidation)
                {
                    _networkLocomotionValidation = rigInfo.localNetworkedRig.gameObject.GetComponentInChildren<NetworkLocomotionValidation>();
                    didSearchNetworkLocomotionValidation = true;
                }
                return _networkLocomotionValidation;
            }
        }

        protected void Awake()
        {
            childrenBasedLocomotionValidation = new ChildrenBasedLocomotionValidation(this);
            hardwareRig = GetComponent<IHardwareRig>();
            if (hardwareRig == null)
                Debug.LogError("An hardwareRig is required");

            minHeadMovementDetectedSqr = minHeadMovementDetected * minHeadMovementDetected;
        }


        #region ILocomotionObserver
        // Forward the ILocomotionValidator.OnDidMove callbacks to any child ILocomotionValidator, and to the NetworkRig ILocomotionValidator childs
        //  Used by the locomotion systems to warn that an actual move did occur
        public void OnDidMove()
        {
            // We use both hardware rig and local user networked rig as a source of locomotion validation
            if(childrenBasedLocomotionValidation != null) childrenBasedLocomotionValidation.OnDidMove();
            if (NetworkLocomotionValidation) NetworkLocomotionValidation.OnDidMove();
        }

        public void OnDidMoveFadeFinished()
        {
            childrenBasedLocomotionValidation.OnDidMove();
            if (NetworkLocomotionValidation) NetworkLocomotionValidation.OnDidMoveFadeFinished();
        }
        #endregion

        #region ILocomotionValidator
        // Forward the ILocomotionValidator.CanMove request to any child ILocomotionValidator, and to the NetworkRig ILocomotionValidator childs, and return false if any of them returns false
        //  Used by the locomotion systems to validate if an incoming move is valid
        public bool CanMoveHeadset(Vector3 position)
        {
            // We use both hardware rig and local user networked rig as a source of locomotion validation
            if (childrenBasedLocomotionValidation.CanMoveHeadset(position) == false) return false;
            if (NetworkLocomotionValidation && !NetworkLocomotionValidation.CanMoveHeadset(position)) return false;
            return true;
        }
        #endregion

        private void Update()
        {
            if (checkHeadMovements)
            {
                if ((hardwareRig.Headset.transform.position - lastHeadMovement).sqrMagnitude > minHeadMovementDetectedSqr)
                {
                    OnDidMove();
                    lastHeadMovement = hardwareRig.Headset.transform.position;
                }
            }
        }
    }
}
