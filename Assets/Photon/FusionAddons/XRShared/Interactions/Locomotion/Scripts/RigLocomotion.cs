using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion.XR.Shared.Core.Interaction;


#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Fusion.XR.Shared.Core;

namespace Fusion.XR.Shared.Locomotion
{
    /**
     * 
     * Simple locomotion system
     * - trigger a fade and rotation when input horizontal axis is above a threshold
     * - trigger a fade and teleport when Teleport method called (usually from a RayBeamer)
     * 
     * Look for child RayBeamer, to trigger Teleport on the onRelease event of the beamers
     * 
     * Ensure that there is no bounce effect, with several movements called too quickly when the user keeps pressing the associated input  
     * 
     **/

    public class RigLocomotion : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM
        [Header("Snap turn")]
        public InputActionProperty leftControllerTurnAction;
        public InputActionProperty rightControllerTurnAction;
#endif

        public float debounceTime = 0.5f;
        public float snapDegree = 45f;
        public float rotationInputThreshold = 0.5f;

        public bool disableLeftHandRotation = false;
        public bool disableRightHandRotation = false;

        [Header("Teleportation")]
        [Tooltip("Automatically found if not set")]
        public List<RayBeamer> teleportBeamers;
        public bool addBeamersToControllers = false;
        public RayBeamer beamerPrefab;
        const string beamerPrefabName = "Beamer";
        public bool addFaderToHeadset = false;

        bool rotating = false;
        float timeStarted = 0;

        IMovableHardwareRig rig;

        [Header("Locomotion target layers")]

        public LayerMask locomotionLayerMask = 0;
        public string additionnalCompatibleLayer = "Locomotion";

        // If locomotion constraints are needed, a ILocomotionValidationHandler can restrict them
        ILocomotionValidationHandler locomotionValidationHandler;

        private void Awake()
        {
            rig = GetComponentInParent<IMovableHardwareRig>();
            if (rig == null)
            {
                Debug.LogError("Should be placed next to an IHardwareRig component");
            }
            locomotionValidationHandler = GetComponentInParent<ILocomotionValidationHandler>();

            var bindings = new List<string> { "joystick" };
#if ENABLE_INPUT_SYSTEM
            leftControllerTurnAction.EnableWithDefaultXRBindings(leftBindings: bindings);
            rightControllerTurnAction.EnableWithDefaultXRBindings(rightBindings: bindings);
#else
            Debug.LogError("Missing com.unity.inputsystem package");
#endif

            if (string.IsNullOrEmpty(additionnalCompatibleLayer) == false)
            {
                int layerToAdd = LayerMask.NameToLayer(additionnalCompatibleLayer);
                if (layerToAdd != -1)
                {
                    locomotionLayerMask = locomotionLayerMask | (1 << layerToAdd);
                }
            }

        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if(addBeamersToControllers && beamerPrefab == null)
            {
                if (Fusion.XR.Shared.Automatization.AutomatisationTools.TryFindAsset(beamerPrefabName, out beamerPrefab, extension: "prefab", requiredPathElements: new string[] { "XRShared", "Locomotion" }))
                {                    
                    Debug.Log($"Set <b>beamerBrefab</b> to {beamerPrefab.name} prefab");                    
                }
            }
#endif
        }

        private void Start()
        {
            if (locomotionLayerMask == 0)
            {
                Debug.LogError("[RigLocomotion] For locomotion to be possible, at least one layer has to be added to locomotionLayerMask, and used on locomotion surface colliders");
                Debug.LogError("[RigLocomotion] Enable locomotion on all surfaces as a fallback");
                locomotionLayerMask = ~0;
            }

            if (addBeamersToControllers)
            {
                var controllers = GetComponentsInChildren<IHardwareController>(true);
                foreach (var controller in controllers)
                {
                    if (controller.gameObject.GetComponentInChildren<RayBeamer>(true) == null)
                    {
                        var beamerGO = GameObject.Instantiate(beamerPrefab);
                        Debug.Log($"Add <b>RayBeamer</b> {beamerGO.name} under {controller.gameObject.name} to display a locomotion beam");
                        beamerGO.transform.parent = controller.transform;
                        beamerGO.transform.localPosition = Vector3.zero;
                        beamerGO.transform.localRotation = Quaternion.identity;
                    }
                }
            }
            if (addFaderToHeadset)
            {
                var headset = GetComponentInChildren<IHardwareHeadset>(true);
                if (headset != null && headset.gameObject.GetComponentInChildren<Fader>() == null)
                {
                    var fader = headset.gameObject.AddComponent<Fader>();
                    if(headset is IFadeable fadable)
                    {
                        fadable.Fader = fader;
                    }
                }
            }

            if (teleportBeamers.Count == 0) teleportBeamers = new List<RayBeamer>(GetComponentsInChildren<RayBeamer>(true));
            foreach (var beamer in teleportBeamers)
            {
                beamer.onRelease.AddListener(OnBeamRelease);
            }
        }

        protected virtual void Update()
        {
            CheckSnapTurn();
        }

        protected virtual void CheckSnapTurn()
        {
#if ENABLE_INPUT_SYSTEM
            if (rotating) return;
            if (timeStarted > 0f)
            {
                // Wait for a certain amount of time before allowing another turn.
                if (timeStarted + debounceTime < Time.time)
                {
                    timeStarted = 0f;
                }
                return;
            }

            var leftStickTurn = disableLeftHandRotation ? 0 :  leftControllerTurnAction.action.ReadValue<Vector2>().x;
            var rightStickTurn = disableRightHandRotation ? 0 : rightControllerTurnAction.action.ReadValue<Vector2>().x;

            if (Mathf.Abs(leftStickTurn) > rotationInputThreshold)
            {
                timeStarted = Time.time;
                StartCoroutine(Rotate(Mathf.Sign(leftStickTurn) * snapDegree));
            }
            else if (Mathf.Abs(rightStickTurn) > rotationInputThreshold)
            {
                timeStarted = Time.time;
                StartCoroutine(Rotate(Mathf.Sign(rightStickTurn) * snapDegree));
            }
#else
            Debug.LogError("Missing com.unity.inputsystem package");
#endif
        }

        IEnumerator Rotate(float angle)
        {
            timeStarted = Time.time;
            rotating = true;
            rig.Rotate(angle, addSnapMovementVisualProtection: true);
            while (rig.SnapMovementInProgress)
            {
                yield return null;
            }
            rotating = false;
        }

        public virtual bool ValidLocomotionSurface(Collider surfaceCollider)
        {
            // We check if the hit collider is in the locomoation layer mask
            bool colliderInLocomotionLayerMask = locomotionLayerMask == (locomotionLayerMask | (1 << surfaceCollider.gameObject.layer));
            return colliderInLocomotionLayerMask;
        }

        protected virtual void OnBeamRelease(Collider lastHitCollider, Vector3 position)
        {
            if (!enabled) return;
            // Checking potential validation handler
            if (locomotionValidationHandler != null)
            {
                var headsetPositionRelativeToRig = rig.transform.InverseTransformPoint(rig.Headset.transform.position);
                Vector3 newHeadsetPosition = position + headsetPositionRelativeToRig.y * rig.transform.up;
                if (!locomotionValidationHandler.CanMoveHeadset(newHeadsetPosition)) return;
            }

            // Checking target surface layer
            if (ValidLocomotionSurface(lastHitCollider))
            {
                StartCoroutine(FadedTeleport(position));
            }
        }

        IEnumerator FadedTeleport(Vector3 position)
        {
            rig.Teleport(position, addSnapMovementVisualProtection: true);

            while (rig.SnapMovementInProgress)
            {
                yield return null;
            }
        }
    }
}
