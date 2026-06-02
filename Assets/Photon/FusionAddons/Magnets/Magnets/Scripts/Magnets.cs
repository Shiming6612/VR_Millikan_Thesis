using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XRShared.GrabbableMagnet
{
    public enum AlignmentAxisAsAttracted
    {
        X, Y, Z,
        MinusX, MinusY, MinusZ
    }
    public enum AlignmentAxisAsAttractor
    {
        X, Y, Z,
        MinusX, MinusY, MinusZ,
        AnyX, AnyY, AnyZ
    }

    public enum AttractedMagnetMove
    {
        // The attracted magnet will only move to project itself on the plane defined by the attractor magnet alignment axis
        AttractOnlyOnAlignmentAxis,
        // the attracted magnet will match the attractork magnet position
        MatchAttractingMagnetPosition
    };

    public enum AttractedMagnetRotation
    {
        // The attracted object will rotate only to align the attracted axis and the attractor axis
        MatchAlignmentAxis,
        // The attracted object will also rotate to only have 90 angles between other axis
        MatchAlignmentAxisWithOrthogonalRotation
    };

    // An IMagnetConfigurator can be optionnally used in TryFindClosestMagnetInRange to customize magnet detection
    //  (alternative to coordinators)
    public interface IMagnetConfigurator
    {
        // If true, the 2 magnets are in the same group, and should not attract each other
        public bool IsInSameGroup(IMagnetConfigurator otherIdentificator);
        public bool IsMagnetActive();
    }

    public interface IMagnet
    {
#pragma warning disable IDE1006 // Naming Styles
        public Transform transform { get; }
#pragma warning restore IDE1006 // Naming Styles
        public MagnetCoordinator MagnetCoordinator { get; }
        // Can be optionnally used in TryFindClosestMagnetInRange to filter same group magnets (alternative to coordinators)
        public IMagnetConfigurator MagnetConfigurator { get; set; }
    }

    public interface IAttractorMagnet : IMagnet
    {
        public Vector3 SnapTargetPosition(Vector3 position);
        public AlignmentAxisAsAttractor AlignmentAxisAsAttractor { get; }
        public AttractedMagnetMove AttractedMagnetMove { get; set; }
        public AttractedMagnetRotation AttractedMagnetRotation { get; set; }
        public List<string> Tags {get;}
    }

    public interface IAttractableMagnet : IMagnet
    {
        public AlignmentAxisAsAttracted AlignmentAxisAsAttracted { get; }
        public bool CheckOnUngrab { get; set; }
        public float MagnetRadius { get; set; }
        public bool TryFindClosestMagnetInRange(out IAttractorMagnet closestMagnet, out float distance);
        public void SnapToMagnet(IAttractorMagnet magnet);
    }

}