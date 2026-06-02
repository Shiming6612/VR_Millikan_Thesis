using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Interaction;
using Fusion.XR.Shared.Core.Interaction.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Fusion.XR.Shared.Locomotion
{
    public struct RayData : INetworkStruct
    {
        public bool isRayEnabled;
        public Vector3 origin;
        public Vector3 target;
        public Color color;
    }

    public interface IRayDescriptor
    {
        public RayData Ray { get; }
    }

    public interface IRayValidator
    {
        // Called just after a successful raycast, and ray status change, allowing to disable/change the status if needed (for instance if we try to telport to an invalid target, ...)
        public void ValidateOnBeamerHit(RayBeamer beamer, RaycastHit hit);
    }

    /**
     * 
     * Display a line renderer when action input is pressed, and raycast other the selected layer mask to find a destination point
     * 
     **/

    public class RayBeamer : MonoBehaviour, IRayDescriptor, IInteractionTip
    {
        #region IInteractionTip
        public float MaxStartInteractionDistance => float.MaxValue;
        public float MaxInteractionScanDistance => float.MaxValue;
        public float MaxMaintainInteractionDepth => 0;
        public IRigPart RigPart => rigPart;
        public bool CanInteract => ray.isRayEnabled;
        public Vector3 Origin => ray.origin;
        public Quaternion Rotation => origin.rotation;
        public virtual bool IsSelecting => rigPart is IController && triggerTracker.ReadValue<float>() is float t && t > 0.5f;
        public IInteractionDetailsProvider LastInteractionDetailProvider { get; set; } = null;

        public virtual Vector2 ScrollDelta
        {
            get
            {
                Vector2 value = Vector2.zero;
                if (rigPart is IController)
                {
                    if (scrollWithGreatestControllerJoystickMagnitude)
                    {
                        value = (joystickTracker.ReadMaxSideAxis() is Vector2 delta) ? delta : Vector2.zero;
                    }
                    else
                    {
                        value = (joystickTracker.ReadValue<Vector2>() is Vector2 delta) ? delta : Vector2.zero;
                    }
                }
                return value * scrollSpeed;
            }
        }
        #endregion

        public ILateralizedRigPart rigPart;
        protected LocalTriggerTracker triggerTracker;
        protected LocalJoystickTracker joystickTracker;

        public bool useRayActionInput = true;
#if ENABLE_INPUT_SYSTEM
        public InputActionProperty rayAction;
#endif
        public Transform origin;
        public LayerMask targetLayerMask = ~0;
        public float maxDistance = 100f;

        [Header("Representation")]
        public LineRenderer lineRenderer;
        public float width = 0.02f;
        public Material lineMaterial;

        public Color hitColor = Color.green;
        public Color noHitColor = Color.red;

        [Tooltip("Prevent line renderer to be automatically disabled by RigPartVisualizer")]
        public bool addToRigPartVisualizerIgnoreList = true;

        [Header("Event")]
        public UnityEvent<Collider, Vector3> onHitEnter = new UnityEvent<Collider, Vector3>();
        public UnityEvent<Collider, Vector3> onHitExit = new UnityEvent<Collider, Vector3>();
        public UnityEvent<Collider, Vector3> onRelease = new UnityEvent<Collider, Vector3>();

        [Header("UI interaction")]
        public bool scrollWithGreatestControllerJoystickMagnitude = true;
        public float scrollSpeed = 5;
        public bool stopRayByNonInteractableUI = true;

        // Define if the beamer ray is active this frame
        public bool isRayEnabled = false;

        public IRayValidator rayValidator;

        public enum Status
        {
            NoBeam,
            BeamNoHit,
            BeamHit
        }
        public Status status = Status.NoBeam;

        public RayData ray;
        [HideInInspector]
        public Vector3 lastHit;
        Collider lastHitCollider = null;

        public RayData Ray => ray;

        public virtual void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.material = lineMaterial;
                lineRenderer.numCapVertices = 4;
            }
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.useWorldSpace = true;
            lineRenderer.enabled = false;

            if (origin == null) origin = transform;

            DetectRigPart();
        }

        void DetectRigPart()
        {
            if (rigPart == null)
            {
                foreach (var r in GetComponentsInParent<ILateralizedRigPart>())
                {
                    // The subclass RayBeamerRigPart is a ILateralizedRigPart. Here, we want the related hand or controller only
                    if (this is not ILateralizedRigPart || r != (ILateralizedRigPart)this)
                    {
                        rigPart = r;
                        break;
                    }
                }
            }
            triggerTracker = new LocalTriggerTracker(rigPart);
            joystickTracker = new LocalJoystickTracker(rigPart);
            if (addToRigPartVisualizerIgnoreList)
            {
                // Ray beamer should not be automatically disabled by RigPartVisualizer
                foreach (var rigPartVisualizer in GetComponentsInParent<RigPartVisualizer>())
                {
                    rigPartVisualizer.renderersToIgnore.Add(lineRenderer);
                }
            }
        }

        public virtual void Start()
        {
            if (rigPart == null) DetectRigPart();
#if ENABLE_INPUT_SYSTEM
            rayAction.EnableWithDefaultXRBindings(rigPart.Side, new List<string> { "thumbstickClicked", "primaryButton", "secondaryButton" });
#else
            Debug.LogError("Missing com.unity.inputsystem package");
#endif
        }

        public bool BeamCast(out RaycastHit hitInfo, Vector3 origin, Vector3 direction)
        {
            Ray handRay = new Ray(origin, direction);
            var physicsRaycast = Physics.Raycast(handRay, out hitInfo, maxDistance, targetLayerMask);
            if (XSCInputModule.Instance)
            {
                var maxDistanceOverride = float.MaxValue;
                if (physicsRaycast)
                {
                    maxDistanceOverride = Vector3.Distance(origin, hitInfo.point);
                }
                IInteractionDetailsProvider result = XSCInputModule.Instance.ProcessInteractionTip(this, out var hits, maxDistanceOverride);
                if (result != null)
                {
                    // The UI Raycast prevent the physics raycast to execute
                    hitInfo = new RaycastHit();
                    hitInfo.point = result.LastInteractionWorldPosition;
                    return true;
                }
                if (stopRayByNonInteractableUI)
                {
                    if (hits.Count > 0 && hits[0].distance < maxDistance)
                    {
                        hitInfo = new RaycastHit();
                        hitInfo.point = hits[0].worldPosition;
                        return true;
                    }
                }
            }
            return physicsRaycast;
        }

        public bool BeamCast(out RaycastHit hitInfo)
        {
            return BeamCast(out hitInfo, ray.origin, origin.forward);
        }

        public virtual void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // If useRayActionInput is true, we read the rayAction to determine isRayEnabled for this frame
            //  Usefull for the mouse teleporter of the desktop mode, which disables the action reading to have its own logic to enable the beamer
            if (useRayActionInput && rayAction != null && rayAction.action != null)
            {
                isRayEnabled = rayAction.action.ReadValue<float>() == 1;
            }
#else
            Debug.LogError("Missing com.unity.inputsystem package");
#endif
            ray.isRayEnabled = isRayEnabled;
            if (ray.isRayEnabled)
            {
                ray.origin = origin.position;
                if (BeamCast(out RaycastHit hit))
                {
                    if (status == Status.BeamHit)
                    {
                        if (lastHitCollider != hit.collider)
                        {
                            OnHitExit(lastHitCollider, lastHit);
                            OnHitEnter(hit.collider, lastHit);
                        }
                    }
                    else
                    {
                        OnHitEnter(hit.collider, lastHit);
                    }
                    lastHitCollider = hit.collider;
                    ray.target = hit.point;
                    ray.color = hitColor;
                    lastHit = hit.point;
                    status = Status.BeamHit;
                    if (rayValidator != null) rayValidator.ValidateOnBeamerHit(this, hit);
                }
                else
                {
                    if (status == Status.BeamHit && lastHitCollider != null)
                    {
                        OnHitExit(lastHitCollider, lastHit);
                    }
                    lastHitCollider = null;
                    ray.target = ray.origin + origin.forward * maxDistance;
                    ray.color = noHitColor;
                    status = Status.BeamNoHit;
                }
            }
            else
            {
                if (status == Status.BeamHit && lastHitCollider != null)
                {
                    OnHitRelease(lastHitCollider, lastHit);
                }
                status = Status.NoBeam;
                lastHitCollider = null;
            }

            UpdateRay();
        }

        #region Callbacks
        void OnHitEnter(Collider hitCollider, Vector3 hitPosition)
        {
            if (onHitEnter != null) onHitEnter.Invoke(hitCollider, lastHit);
        }

        void OnHitExit(Collider lastHitCollider, Vector3 lastHitPosition)
        {
            if (onHitExit != null) onHitExit.Invoke(lastHitCollider, lastHit);
        }

        void OnHitRelease(Collider lastHitCollider, Vector3 lastHitPosition)
        {
            if (onRelease != null) onRelease.Invoke(lastHitCollider, lastHit);
        }
        #endregion

        public void CancelHit()
        {
            status = Status.NoBeam;
        }

        void UpdateRay()
        {
            lineRenderer.enabled = ray.isRayEnabled;
            if (ray.isRayEnabled)
            {
                lineRenderer.SetPositions(new Vector3[] { ray.origin, ray.target });
                lineRenderer.positionCount = 2;
                lineRenderer.startColor = ray.color;
                lineRenderer.endColor = ray.color;
            }
        }
    }
}
