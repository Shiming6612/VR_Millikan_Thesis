using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XRShared.GrabbableMagnet
{
    /**
     * AttractorMagnet components will atttract IMagnets to them, but won't be attracted themselves by other magnets
     * They require a collider, that will be the zone in which the attractable magnet will start evaluating the distance to them
     * (it will reduce the number of checks and improve performances). The detection is based on a SphreCast on the compatible layers of the attractable magnet
     */
    public class AttractorMagnet : MonoBehaviour, IAttractorMagnet
    {
        [Header("Attractor options")]
        public AlignmentAxisAsAttractor alignmentAxisAsAttractor = AlignmentAxisAsAttractor.MinusY;
        [Tooltip("AttractOnlyOnAlignmentAxis: The attracted magnet will only move to project itself on the plane defined by the attractor magnet alignment axis" +
            "\nMatchAttractingMagnetPosition : the attracted magnet will match the attractork magnet position")]
        public AttractedMagnetMove attractedMagnetMove = AttractedMagnetMove.MatchAttractingMagnetPosition;
        [Tooltip("MatchAlignmentAxis: The attracted object will rotate only to align the attracted axis and the attractor axis\nMatchAlignmentAxisWithOrthogonalRotation: The attracted object will also rotate to only have 90 angles between other axis")]
        public AttractedMagnetRotation attractedMagnetRotation = AttractedMagnetRotation.MatchAlignmentAxisWithOrthogonalRotation;


        [Header("Attractor options - optional offset")]
        [Tooltip("Set to true to apply an offset to the projected position")]
        public bool applyOffsetToSnapPosition = false;
        [Tooltip("Local space offset (along transform axis), described with world space value (ignoring transform scale)")]
        public Vector3 localOffset = Vector3.zero;
        [Tooltip("If true, both localOffset and -localOffset will be tested, and the closest resulting snapping point will be used")]
        public bool ignoreOffsetSign = true;

        [Header("Automatic layer setup")]
        [Tooltip("If set, this object and its children collider will be set to this layer")]
        public string magnetLayer = "Magnets";
        public bool applyLayerToChildren = true;

        [Header("Filtering tags")]
        public List<string> tags = new List<string>();

        MagnetCoordinator _magnetCoordinator;
        public MagnetCoordinator MagnetCoordinator => _magnetCoordinator;

        private void Awake()
        {
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
                    if (applyLayerToChildren) {
                        foreach (var collider in GetComponentsInChildren<Collider>())
                        {
                            collider.gameObject.layer = layer;
                        }
                    }
                }
            }

            _magnetCoordinator = GetComponentInParent<MagnetCoordinator>();
        }

        #region IAttractorMagnet
        public AlignmentAxisAsAttractor AlignmentAxisAsAttractor
        {
            get => alignmentAxisAsAttractor;
            set => alignmentAxisAsAttractor = value;
        }

        public AttractedMagnetMove AttractedMagnetMove
        {
            get => attractedMagnetMove;
            set => attractedMagnetMove = value;
        }
        public AttractedMagnetRotation AttractedMagnetRotation
        {
            get => attractedMagnetRotation;
            set => attractedMagnetRotation = value;
        }

        public Vector3 SnapTargetPosition(Vector3 position)
        {
            Vector3 offset = Vector3.zero;
            Vector3 reverseOffset = Vector3.zero;
            Vector3 snapPosition = Vector3.zero;
            Vector3 reverseSnapPosition = Vector3.zero;
            if (applyOffsetToSnapPosition)
            {
                // localOffset discribes an offset, along the transform axis, but using world space lengths
                // Compute a world space offset but ignore current transform scale
                // This allows to use world space length for offset, no matter the actual magnet scale
                Matrix4x4 trsMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                // Find the offset position in world space (ignoring scale)
                var offsetetPosition = trsMatrix.MultiplyPoint3x4(localOffset);
                // Find a offset, with world space values/scale
                offset = offsetetPosition - transform.position;
                if (ignoreOffsetSign)
                {
                    // Determine the world space offset when applying -localOffset instead of localOffset as the local offset
                    var reverseOffsetetPosition = trsMatrix.MultiplyPoint3x4(-localOffset);
                    reverseOffset = reverseOffsetetPosition - transform.position;
                }
            }

            if (attractedMagnetMove == AttractedMagnetMove.AttractOnlyOnAlignmentAxis)
            {
                var planeDirection = Vector3.zero;

                switch (alignmentAxisAsAttractor)
                {
                    case AlignmentAxisAsAttractor.Y:
                    case AlignmentAxisAsAttractor.MinusY:
                    case AlignmentAxisAsAttractor.AnyY:
                        planeDirection = transform.up;
                        break;
                    case AlignmentAxisAsAttractor.Z:
                    case AlignmentAxisAsAttractor.MinusZ:
                    case AlignmentAxisAsAttractor.AnyZ:
                        planeDirection = transform.forward;
                        break;
                    case AlignmentAxisAsAttractor.X:
                    case AlignmentAxisAsAttractor.MinusX:
                    case AlignmentAxisAsAttractor.AnyX:
                        planeDirection = transform.right;
                        break;
                }

                var projectionPlane = new Plane(planeDirection, transform.position);
                // Project position on plane
                var projection = projectionPlane.ClosestPointOnPlane(position);

                if (applyOffsetToSnapPosition)
                {
                    // Apply the offset vector to the projection
                    var offsetedProjection = projection + offset;
                    if (ignoreOffsetSign)
                    {
                        // Apply the world space offset to the projection
                        reverseSnapPosition = projection + reverseOffset;
                    }
                    snapPosition = offsetedProjection;
                } 
                else
                {
                    snapPosition = projection;
                }
            }
            else
            {
                snapPosition = transform.position;
                if (applyOffsetToSnapPosition)
                {
                    snapPosition = transform.position + offset;
                    if (ignoreOffsetSign)
                    {
                        reverseSnapPosition = transform.position + reverseOffset;
                    }
                }
            }

            if (applyOffsetToSnapPosition && ignoreOffsetSign)
            {
                // Select the offseted snap position that is the closest to the position we are trying to snap
                var reverseDistance = Vector3.Distance(position, reverseSnapPosition);
                var distance = Vector3.Distance(position, snapPosition);
                if (reverseDistance < distance)
                {
                    snapPosition = reverseSnapPosition;
                }
            }
            return snapPosition;
        }

        public List<string> Tags => tags;

        // Can be optionnally used in TryFindClosestMagnetInRange to filter same group magnets (alternative to coordinators)
        public IMagnetConfigurator MagnetConfigurator { get; set; } = null;
        #endregion
    }
}
