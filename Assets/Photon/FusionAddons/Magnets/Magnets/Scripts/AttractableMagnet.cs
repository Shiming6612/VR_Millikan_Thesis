using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Fusion.XR.Shared.Core;

namespace Fusion.XRShared.GrabbableMagnet
{
    [DefaultExecutionOrder(AttractableMagnet.EXECUTION_ORDER)]
    public class AttractableMagnet : NetworkBehaviour, IAttractableMagnet
    {
        public const int EXECUTION_ORDER = INetworkGrabbable.EXECUTION_ORDER + 5;
        [HideInInspector]
        public NetworkTRSP rootNTRSP;
        NetworkTRSP initialRootNTRSP;
        INetworkGrabbable networkGrabbable;
        Rigidbody rb;

        [Header("Snap options as an attracted")]
        public AlignmentAxisAsAttracted alignmentAxisAsAttracted = AlignmentAxisAsAttracted.Y;
        public float magnetRadius = 0.1f;
        public LayerMask compatibleLayers;
        public string additionalCompatibleLayer = "";
        public bool addObjectLayerToCompatibleLayers = true;
        [HideInInspector] public bool didDetectProximityMagnetThisFrame = false;
        public enum AttractorMagnetTagRequirement
        {
            NoTagRequired,
            AnyTag,
            AllTags,
        }
        public AttractorMagnetTagRequirement attractorMagnetTagRequirement = AttractorMagnetTagRequirement.NoTagRequired;
        [DrawIf(nameof(attractorMagnetTagRequirement), (long)AttractorMagnetTagRequirement.NoTagRequired, CompareOperator.NotEqual, Hide = true)]
        [Tooltip("Can be attracted by an attractor with any of this tags (one tag matching is required if the list is not empty)")]
        public List<string> requiredTagsInAttractor = new List<string>();

        [Header("Snap animation")]
        public bool instantSnap = true;
        public float snapDuration = 1;

        [Header("Automatic layer setup")]
        [Tooltip("If set, this object and its children collider will be set to this layer")]
        public string magnetLayer = "Magnets";

        public bool CheckOnUngrab { get; set; } = true;

        public MagnetCoordinator magnetCoordinator;
        public MagnetCoordinator MagnetCoordinator => magnetCoordinator;


        public UnityEvent<IMagnet> onSnapToMagnet = null;

        [Header("Proximity detection while grabbing")]
        public bool enableProximityDetectionWhileGrabbed = false;
        public UnityEvent<IMagnet, IMagnet, float> onMagnetDetectedInProximity = null;
        public UnityEvent<IMagnet, IMagnet, float> onMagnetProximity = null;
        public UnityEvent<IMagnet, IMagnet> onMagnetLeavingProximity = null;
        public IMagnet proximityMagnet = null;

        protected bool IsGrabbed => networkGrabbable != null && networkGrabbable.IsGrabbed;

        [Header("Feedback")]
        [SerializeField] IFeedbackHandler feedback;
        [SerializeField] string audioType;

        IAttractorMagnet snapRequest = null;
        float snapStart = -1;


        #region IAttractableMagnet
        // Can be optionnally used in TryFindClosestMagnetInRange to filter magnets (alternative to coordinators)
        public IMagnetConfigurator MagnetConfigurator { get; set; } = null;

        public AlignmentAxisAsAttracted AlignmentAxisAsAttracted
        {
            get => alignmentAxisAsAttracted;
            set => alignmentAxisAsAttracted = value;
        }

        public float MagnetRadius
        {
            get => magnetRadius;
            set => magnetRadius = value;
        }
        #endregion


        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            if (snapRequest != null && IsGrabbed)
            {
                // Cancel snap
                snapRequest = null;
            }
            if (snapRequest != null)
            {
                DoSnapToMagnet(snapRequest);
            }
        }

        private void Awake()
        {
            magnetCoordinator = GetComponentInParent<MagnetCoordinator>();

            rootNTRSP = GetComponentInParent<NetworkTRSP>();
            initialRootNTRSP = rootNTRSP;
            networkGrabbable = GetComponentInParent<INetworkGrabbable>();
            rb = GetComponentInParent<Rigidbody>();
            if (networkGrabbable != null) networkGrabbable.OnUngrab.AddListener(OnDidUngrab);
            if (string.IsNullOrEmpty(magnetLayer) == false)
            {
                int layer = LayerMask.NameToLayer(magnetLayer);
                if (layer == -1)
                {
                    Debug.LogError($"Please add a {magnetLayer} layer (it will be automatically be set to this object)");
                }
                else
                {
                    gameObject.layer = layer;
                    foreach (var collider in GetComponentsInChildren<Collider>())
                    {
                        collider.gameObject.layer = layer;
                    }
                }
            }
            if (string.IsNullOrEmpty(additionalCompatibleLayer) == false)
            {
                int layer = LayerMask.NameToLayer(additionalCompatibleLayer);
                if (layer == -1)
                {
                    Debug.LogError($"Please add a {magnetLayer} layer (it will be automatically be set to this object magnet mask)");
                }
                else
                {
                    compatibleLayers |= (1 << layer);
                }
            }

            if (feedback == null)
                feedback = GetComponent<IFeedbackHandler>();
        }

        public void ChangeMovingRoot(NetworkTRSP root)
        {
            rootNTRSP = root;
        }

        public void RestoreInitialMovingRoot()
        {
            rootNTRSP = initialRootNTRSP;
        }


        private void OnDidUngrab()
        {
            if (CheckOnUngrab)
            {
                CheckMagnetProximity();
            }
        }

        public bool HasAttractorMatchingTags(IAttractorMagnet candidateAttractor)
        {
            if (attractorMagnetTagRequirement == AttractorMagnetTagRequirement.NoTagRequired || requiredTagsInAttractor.Count == 0)
            {
                return true;
            }
            foreach (var tag in requiredTagsInAttractor)
            {
                if (candidateAttractor.Tags.Contains(tag))
                {
                    if (attractorMagnetTagRequirement == AttractorMagnetTagRequirement.AnyTag)
                    {
                        return true;
                    }
                } 
                else
                {
                    if (attractorMagnetTagRequirement == AttractorMagnetTagRequirement.AllTags)
                    {
                        return false;
                    }
                }
            }
            return attractorMagnetTagRequirement == AttractorMagnetTagRequirement.AllTags;
        }

        public bool TryFindClosestMagnetInRange(out IAttractorMagnet closestMagnet, out float minDistance)
        {
            return TryFindClosestMagnetInRange(out closestMagnet, out minDistance, ignoreSameGroupMagnet: false);
        }


        public Collider[] CollidersInProximity() {
            if (MagnetConfigurator != null && MagnetConfigurator.IsMagnetActive() == false)
            {
                return new Collider[0];
            }
            var layerMask = compatibleLayers;
            if (addObjectLayerToCompatibleLayers)
            {
                layerMask = layerMask | (1 << gameObject.layer);
            }
            var colliders = Physics.OverlapSphere(transform.position, magnetRadius, layerMask: layerMask);
            return colliders;
        }

        public bool IsCompatibleAttractorMagnet(IAttractorMagnet magnet, bool ignoreSameGroupMagnet)
        {
            if ((Object)magnet == null)
            {
                //Debug.LogError($"No attractor magnet ({collider})");
                return false;
            }
            if ((Object)(magnet.MagnetConfigurator) != null && magnet.MagnetConfigurator.IsMagnetActive() == false)
            {
                return false;
            }
            if ((Object)magnet == this)
            {
                return false;
            }
            if (ignoreSameGroupMagnet && MagnetConfigurator != null && magnet.MagnetConfigurator != null && MagnetConfigurator.IsInSameGroup(magnet.MagnetConfigurator))
            {
                return false;
            }
            if (MagnetCoordinator != null && magnet.MagnetCoordinator == MagnetCoordinator)
            {
                return false;
            }
            if (HasAttractorMatchingTags(magnet) == false)
            {
                return false;
            }
            return true;
        }

        public IAttractorMagnet AttractorMagnetForCollider(Collider collider)
        {
            IAttractorMagnet magnet = collider.GetComponentInParent<IAttractorMagnet>();

            if (magnet is AttractorMagnetProxy magnetProxy)
            {
                magnet = magnetProxy.target;
            }
            return magnet;
        }

        public bool TryFindClosestMagnetInRange(out IAttractorMagnet closestMagnet, out float minDistance, bool ignoreSameGroupMagnet)
        {
            closestMagnet = null;
            minDistance = magnetRadius;

            var colliders = CollidersInProximity();

            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                IAttractorMagnet magnet = AttractorMagnetForCollider(collider);

                if (IsCompatibleAttractorMagnet(magnet, ignoreSameGroupMagnet) == false)
                {
                    continue;
                }

                var distance = Vector3.Distance(transform.position, magnet.SnapTargetPosition(transform.position));
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestMagnet = magnet;
                }
            }
            return closestMagnet != null;
        }

        public bool IsAttractorMagnetInRange(IAttractorMagnet attractorMagnet, bool ignoreSameGroupMagnet)
        {
            var colliders = CollidersInProximity();

            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                IAttractorMagnet magnet = AttractorMagnetForCollider(collider);

                if (IsCompatibleAttractorMagnet(magnet, ignoreSameGroupMagnet) == false)
                {
                    if ((Object)attractorMagnet == (Object)magnet)
                    {
                        return false;
                    }
                    continue;
                }

                if ((Object)attractorMagnet == (Object)magnet)
                {
                    return true;
                }
            }
            return false;
        }

        [ContextMenu("CheckMagnetProximity")]
        public void CheckMagnetProximity()
        {
            if (Object && Object.HasStateAuthority && IsGrabbed == false)
            {

                if (TryFindClosestMagnetInRange(out var closestMagnet, out _))
                {
                    SnapToMagnet(closestMagnet);
                }
            }
        }

        public void SnapToMagnet(IAttractorMagnet magnet)
        {
            snapRequest = magnet;
            snapStart = Time.time;
        }

        public void DoSnapToMagnet(IAttractorMagnet magnet)
        {
            float progress = 1;
            if (instantSnap)
            {
                snapRequest = null;
            }
            else
            {
                progress = (Time.time - snapStart) / snapDuration;
                if (progress >= 1)
                {
                    progress = 1;
                    snapRequest = null;
                }
            }

            InstantSnap(magnet, progress);

            // Send event
            if (onSnapToMagnet != null)
            {
                onSnapToMagnet.Invoke(magnet);

                if (feedback != null)
                {
                    feedback.PlayAudioFeedback(audioType);
                }
            }
        }

        // Immediatly move attractable magnet to match current attractor position
        public void InstantSnap(IAttractorMagnet magnet, float progress = 1)
        {
            if (rb && rb.isKinematic == false)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
                rb.angularVelocity = Vector3.zero;
            }

            // Rotate the parent NT to match the magnet positions
            Quaternion targetRotation;
            if (magnet.AttractedMagnetRotation == AttractedMagnetRotation.MatchAlignmentAxis)
            {
                targetRotation = AdaptedRotationOnAlignAxis(magnet.transform, magnet.AlignmentAxisAsAttractor);
            }
            else
            {
                targetRotation = AdaptedRotationOnAllAxis(magnet.transform, magnet.AlignmentAxisAsAttractor);
            }
            ApplyRotation(targetRotation, progress);

            // Move the parent NT to match the magnet positions
            var targetPosition = magnet.SnapTargetPosition(transform.position);
            ApplyPosition(targetPosition, progress);
        }

        protected virtual Quaternion AdaptedRotationOnAlignAxis(Transform targetTransform, AlignmentAxisAsAttractor targetAlignAxisAsAttractor)
        {
            return AdaptedRotation(targetTransform, targetAlignAxisAsAttractor, useTargetPlaneAxisOrthogonalGuide: false);
        }

        // Find the most appropriate axis to adapt on the align axis while aligning other axis too
        protected virtual Quaternion AdaptedRotationOnAllAxis(Transform targetTransform, AlignmentAxisAsAttractor targetAlignAxisAsAttractor)
        {
            return AdaptedRotation(targetTransform, targetAlignAxisAsAttractor, useTargetPlaneAxisOrthogonalGuide: true);
        }

        protected virtual Quaternion AdaptedRotation(Transform targetTransform, AlignmentAxisAsAttractor targetAlignAxisAsAttractor, bool useTargetPlaneAxisOrthogonalGuide = false)
        {
            // The attractable oriented-axis than needs to be aligned
            var sourceAlignementAxis = Vector3.zero;
            // The attractable oriented-axis on the plane normal to the attractable alignment axis, that will be projected on its target plane post orientation
            var sourceAlignementPlaneAxis = Vector3.zero;

            switch (alignmentAxisAsAttracted)
            {
                case AlignmentAxisAsAttracted.Y:
                    sourceAlignementAxis = transform.up;
                    sourceAlignementPlaneAxis = transform.forward;
                    break;
                case AlignmentAxisAsAttracted.MinusY:
                    sourceAlignementAxis = -transform.up;
                    sourceAlignementPlaneAxis = transform.forward;
                    break;
                case AlignmentAxisAsAttracted.Z:
                    sourceAlignementAxis = transform.forward;
                    sourceAlignementPlaneAxis = transform.up;
                    break;
                case AlignmentAxisAsAttracted.MinusZ:
                    sourceAlignementAxis = -transform.forward;
                    sourceAlignementPlaneAxis = transform.up;
                    break;
                case AlignmentAxisAsAttracted.X:
                    sourceAlignementAxis = transform.right;
                    sourceAlignementPlaneAxis = transform.up;
                    break;
                case AlignmentAxisAsAttracted.MinusX:
                    sourceAlignementAxis = -transform.right;
                    sourceAlignementPlaneAxis = transform.up;
                    break;

            }

            // The attractor oriented-axis to align to
            var targetAlignmentAxis = Vector3.zero;
            //The projection of sourceAlignementPlaneAxis on the plane normal to the attractor alignment axis
            var targetAlignmentPlaneVector = Vector3.zero;
            switch (targetAlignAxisAsAttractor)
            {
                case AlignmentAxisAsAttractor.Y:
                    targetAlignmentAxis = targetTransform.up;
                    targetAlignmentPlaneVector = Vector3.ProjectOnPlane(sourceAlignementPlaneAxis, targetTransform.up);
                    break;
                case AlignmentAxisAsAttractor.MinusY:
                    targetAlignmentAxis = -targetTransform.up;
                    targetAlignmentPlaneVector = Vector3.ProjectOnPlane(sourceAlignementPlaneAxis, targetTransform.up);
                    break;
                case AlignmentAxisAsAttractor.AnyY:
                    targetAlignmentAxis = Vector3.Project(sourceAlignementAxis, targetTransform.up);
                    targetAlignmentPlaneVector = Vector3.ProjectOnPlane(sourceAlignementPlaneAxis, targetTransform.up);
                    break;
                case AlignmentAxisAsAttractor.Z:
                    targetAlignmentAxis = targetTransform.forward;
                    targetAlignmentPlaneVector = Vector3.ProjectOnPlane(sourceAlignementPlaneAxis, targetTransform.forward);
                    break;
                case AlignmentAxisAsAttractor.MinusZ:
                    targetAlignmentAxis = -targetTransform.forward;
                    targetAlignmentPlaneVector = Vector3.ProjectOnPlane(sourceAlignementPlaneAxis, targetTransform.forward);
                    break;
                case AlignmentAxisAsAttractor.AnyZ:
                    targetAlignmentAxis = Vector3.Project(sourceAlignementAxis, targetTransform.forward);
                    targetAlignmentPlaneVector = Vector3.ProjectOnPlane(sourceAlignementPlaneAxis, targetTransform.forward);
                    break;
                case AlignmentAxisAsAttractor.X:
                    targetAlignmentAxis = targetTransform.right;
                    targetAlignmentPlaneVector = Vector3.ProjectOnPlane(sourceAlignementPlaneAxis, targetTransform.right);
                    break;
                case AlignmentAxisAsAttractor.MinusX:
                    targetAlignmentAxis = -targetTransform.right;
                    targetAlignmentPlaneVector = Vector3.ProjectOnPlane(sourceAlignementPlaneAxis, targetTransform.right);
                    break;
                case AlignmentAxisAsAttractor.AnyX:
                    targetAlignmentAxis = Vector3.Project(sourceAlignementAxis, targetTransform.right);
                    targetAlignmentPlaneVector = Vector3.ProjectOnPlane(sourceAlignementPlaneAxis, targetTransform.right);
                    break;
            }

            // If we need to force an orthogonal angle adaptation, we find the targetTransform oriented axis that will be the closest to the actual targetAlignmentPlaneVector, as the new targetAlignmentPlaneVector
            if (useTargetPlaneAxisOrthogonalGuide)
            {
                var candidates = new Vector3[] { targetTransform.up, -targetTransform.up, targetTransform.right, -targetTransform.right, targetTransform.forward, -targetTransform.forward };
                var minAngle = float.PositiveInfinity;
                var bestCandidate = targetAlignmentPlaneVector;
                for (int i = 0; i < candidates.Length; i++)
                {
                    var candidate = candidates[i];
                    var angle = Vector3.Angle(targetAlignmentPlaneVector, candidate);
                    if (angle < minAngle)
                    {
                        minAngle = angle;
                        bestCandidate = candidate;
                    }
                }
                targetAlignmentPlaneVector = bestCandidate;
            }

            Vector3 newUp = Vector3.zero;
            Vector3 newForward = Vector3.zero;
            Vector3 newRight = Vector3.zero;

            switch (alignmentAxisAsAttracted)
            {
                case AlignmentAxisAsAttracted.Y:
                    newForward = targetAlignmentPlaneVector;
                    newUp = targetAlignmentAxis;
                    break;
                case AlignmentAxisAsAttracted.MinusY:
                    newForward = targetAlignmentPlaneVector;
                    newUp = -targetAlignmentAxis;
                    break;
                case AlignmentAxisAsAttracted.Z:
                    newForward = targetAlignmentAxis;
                    newUp = targetAlignmentPlaneVector;
                    break;
                case AlignmentAxisAsAttracted.MinusZ:
                    newForward = -targetAlignmentAxis;
                    newUp = targetAlignmentPlaneVector;
                    break;
                case AlignmentAxisAsAttracted.X:
                    newRight = targetAlignmentAxis;
                    newUp = targetAlignmentPlaneVector;
                    newForward = Vector3.Cross(newRight, newUp);
                    break;
                case AlignmentAxisAsAttracted.MinusX:
                    newRight = targetAlignmentAxis;
                    newUp = targetAlignmentPlaneVector;
                    newForward = -Vector3.Cross(newRight, newUp);
                    break;
            }
            var targetRotation = Quaternion.LookRotation(newForward, newUp);

            return targetRotation;
        }

        void ApplyRotation(Quaternion targetRotation, float progress)
        {
            var localMagnetRotation = Quaternion.Inverse(rootNTRSP.transform.rotation) * transform.rotation;
            var rotation = targetRotation * Quaternion.Inverse(localMagnetRotation);

            if (progress < 1) rotation = Quaternion.Slerp(rootNTRSP.transform.rotation, rotation, progress);

            if (rb)
            {
                rb.rotation = rotation;
            }
            rootNTRSP.transform.rotation = rotation;
        }

        void ApplyPosition(Vector3 targetPosition, float progress)
        {
            var position = targetPosition - transform.position + rootNTRSP.transform.position;
            if (progress < 1) position = Vector3.Lerp(rootNTRSP.transform.position, position, progress);
            if (rb)
            {
                rb.position = position;
            }
            rootNTRSP.transform.position = position;
        }

        private void Update()
        {
            didDetectProximityMagnetThisFrame = false;
            if (enableProximityDetectionWhileGrabbed && IsGrabbed && Object && Object.HasStateAuthority)
            {
                DetectProximityMagnet();
            }
        }

        public void DetectProximityMagnet()
        {
            didDetectProximityMagnetThisFrame = true;
            if (TryFindClosestMagnetInRange(out var remoteMagnet, out var distance))
            {
                if (remoteMagnet != proximityMagnet)
                {
                    if (proximityMagnet != null)
                    {
                        if (onMagnetLeavingProximity != null) onMagnetLeavingProximity.Invoke(this, proximityMagnet);
                    }
                    proximityMagnet = remoteMagnet;
                    if (onMagnetDetectedInProximity != null) onMagnetDetectedInProximity.Invoke(this, remoteMagnet, distance);
                }
                if (onMagnetProximity != null) onMagnetProximity.Invoke(this, remoteMagnet, distance);
            }
            else if (proximityMagnet != null)
            {
                if (onMagnetLeavingProximity != null) 
                    onMagnetLeavingProximity.Invoke(this, proximityMagnet);
                proximityMagnet = null;
            }
        }


        [Header("Debug")]
        public bool debug = true;
        public Color debugColor = new Color(0, 1, 0, 0.2f);
        void OnDrawGizmos()
        {
            float debugRadius;
            if (debug)
            {
                if (magnetCoordinator != null && magnetCoordinator.overrideMagnetRadius == true)
                    debugRadius = magnetCoordinator.magnetRadius;
                else
                    debugRadius = magnetRadius;

                Gizmos.color = debugColor;
                Gizmos.DrawWireSphere(transform.position, debugRadius);
                //Gizmos.DrawSphere(transform.position, debugRadius);
            }
        }

    }
}


