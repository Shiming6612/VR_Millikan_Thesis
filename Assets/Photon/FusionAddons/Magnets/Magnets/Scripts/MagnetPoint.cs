using UnityEngine;
using UnityEngine.Events;
using System;
using Fusion.XR.Shared.Core;

namespace Fusion.XRShared.GrabbableMagnet.Legacy
{
    [Obsolete("Replace by AttractorMagnet and AttractableMagnet")]
    public class MagnetPoint : NetworkBehaviour
    {
        public enum AttractedMagnetMove
        {
            AttracktOnlyOnAlignmentAxis,
            MatchAttracktingMagnetPosition
        };

        [Header("Snap options as an attracted")]
        public AlignmentAxisAsAttracted alignmentAxisAsAttracted = AlignmentAxisAsAttracted.Y;
        public float magnetRadius = 0.1f;
        public LayerMask compatibleLayers;
        public string additionalCompatibleLayer = "";
        public bool addObjectLayerToCompatibleLayers = true;

        [Header("Attractor options")]
        public AlignmentAxisAsAttractor alignmentAxisAsAttractor = AlignmentAxisAsAttractor.MinusY;
        [Tooltip("AttracktOnlyOnAlignmentAxis: The attracted magnet will only move to project itself on the plane defined by the attractor magnet alignment axis" +
            "\nMatchAttracktingMagnetPosition : the attracted magnet will match the attractork magnet position")]
        public AttractedMagnetMove attractedMagnetMove = AttractedMagnetMove.MatchAttracktingMagnetPosition;
        [Tooltip("MatchAlignmentAxis: The attracted object will rotate only to align the attracted axis and the attractor axis\nMatchAlignmentAxisWithOrthogonalRotation: The attracted object will also rotate to only have 90 angles between other axis")]
        public AttractedMagnetRotation attractedMagnetRotation = AttractedMagnetRotation.MatchAlignmentAxisWithOrthogonalRotation;

        [Header("Snap animation")]
        public bool instantSnap = true;
        public float snapDuration = 1;

        [Header("Automatic layer setup")]
        [Tooltip("If set, this object and its children collider will be set to this layer")]
        public string magnetLayer = "Magnets";

        public bool CheckOnUngrab { get; set; } = true;

        public MagnetCoordinator magnetCoordinator;
        public MagnetCoordinator MagnetCoordinator => magnetCoordinator;


        public UnityEvent onSnapToMagnet;

        [Header("Proximity detection while grabbing")]
        public bool enableProximityDetectionWhileGrabbed = false;
        public UnityEvent<IMagnet, IMagnet, float> onMagnetDetectedInProximity = null;
        public UnityEvent<IMagnet, IMagnet, float> onMagnetProximity = null;
        public UnityEvent<IMagnet, IMagnet> onMagnetLeavingProximity = null;

        [Header("Feedback")]
        [SerializeField] IFeedbackHandler feedback;
        [SerializeField] string audioType;

        private void Awake()
        {
            Debug.LogError("MagnetPoint is deprecated. Replace by AttractorMagnet and AtractedMagnet");
        }
    }
}


