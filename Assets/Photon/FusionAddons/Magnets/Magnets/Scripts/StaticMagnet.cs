using System;
using UnityEngine;

namespace Fusion.XRShared.GrabbableMagnet
{
    [Obsolete("Replace by AttractorMagnet")]
    public class StaticMagnet : MonoBehaviour, IMagnet
    {
        public enum AttractedMagnetMove
        {
            AttracktOnlyOnAlignmentAxis,
            MatchAttracktingMagnetPosition
        };
        
        [Header("Attractor options")]
        public AlignmentAxisAsAttractor alignmentAxisAsAttractor = AlignmentAxisAsAttractor.MinusY;
        [Tooltip("AttracktOnlyOnAlignmentAxis: The attracted magnet will only move to project itself on the plane defined by the attractor magnet alignment axis" +
            "\nMatchAttracktingMagnetPosition : the attracted magnet will match the attractork magnet position")]
        public AttractedMagnetMove attractedMagnetMove = AttractedMagnetMove.MatchAttracktingMagnetPosition;
        [Tooltip("MatchAlignmentAxis: The attracted object will rotate only to align the attracted axis and the attractor axis\nMatchAlignmentAxisWithOrthogonalRotation: The attracted object will also rotate to only have 90 angles between other axis")]
        public AttractedMagnetRotation attractedMagnetRotation = AttractedMagnetRotation.MatchAlignmentAxisWithOrthogonalRotation;

        public Vector3 localOffset = Vector3.zero;
        public bool ignoreOffsetSign = true;

        [Header("Automatic layer setup")]
        public string magnetLayer = "Magnets";
        public bool applyLayerToChildren = true;
        
        // Can be optionnally used in TryFindClosestMagnetInRange to filter same group magnets (alternative to coordinators)
        public IMagnetConfigurator MagnetConfigurator { get; set; } = null;


        MagnetCoordinator _magnetCoordinator;
        public MagnetCoordinator MagnetCoordinator => _magnetCoordinator;

        private void Awake()
        {
            Debug.LogError("StaticMagnet is deprecated. Replace by AttractorMagnet");
        }
    }
}
