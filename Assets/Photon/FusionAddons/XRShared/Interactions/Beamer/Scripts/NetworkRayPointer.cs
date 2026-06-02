using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Locomotion;
using UnityEngine;

namespace Fusion.XR.Shared
{
    [RequireComponent(typeof(NetworkTRSP))]
    public class NetworkRayPointer : NetworkRigPart, ILateralizedNetworkRigPart
    {
        public override RigPartKind Kind => RigPartKind.Pointer;

        public RigPartSide Side => ((ILateralizedNetworkRigPart)EmittingRigPart)?.Side ?? RigPartSide.Undefined;

        [Networked]
        public NetworkRigPart EmittingRigPart { get; set; }

        [Networked]
        public Vector3 EmittingRigPartPositionOffset { get; set; } = Vector3.zero;


        [Networked]
        public RayBeamer.Status BeamStatus { get; set; } = RayBeamer.Status.NoBeam;

        public RayPointer LocalHardwareRayPointer => LocalHardwareRigPart as RayPointer;

        [Header("Representation")]
        public LineRenderer lineRenderer;
        public float width = 0.02f;
        public Material lineMaterial;

        public Color hitColor = Color.green;
        public Color noHitColor = Color.red;

        bool offsetSet = false;

        public bool hideRayOnStateAuthority = true;
        protected override void Awake()
        {
            base.Awake(); 
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
        }

        public override void Spawned()
        {
            base.Spawned();
            if(EmittingRigPart == null && Object.HasStateAuthority)
            {
                foreach(var r in GetComponentsInParent<NetworkRigPart>())
                {
                    if (r is ILateralizedNetworkRigPart && r != this)
                    {
                        EmittingRigPart = r;
                        break;
                    }
                }
                if (EmittingRigPart == null)
                {
                    Debug.LogError("Unable to find emitting rig part");
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            var rayPointer = LocalHardwareRayPointer;
            if (offsetSet == false && rayPointer != null)
            {
                offsetSet = true;
                EmittingRigPartPositionOffset = rayPointer.transform.InverseTransformPoint(rayPointer.origin.position);
            }
            if (rayPointer)
            {
                BeamStatus = rayPointer.status;
            }
        }

        public override void Render()
        {
            base.Render();
            UpdateRay();
        }

        protected override void ExtrapolateWithLocalHardwareRigPart()
        {
            base.ExtrapolateWithLocalHardwareRigPart();
            UpdateRay();
        }

        void UpdateRay()
        {
            lineRenderer.enabled = TrackingStatus == RigPartTrackingstatus.Tracked;
            if (hideRayOnStateAuthority && Object.HasStateAuthority)
            {
                lineRenderer.enabled = false;
            }
            if (lineRenderer.enabled)
            {
                var color = BeamStatus == RayBeamer.Status.BeamHit ? hitColor : noHitColor;
                var originPosition = EmittingRigPart.transform.TransformPoint(EmittingRigPartPositionOffset);

                lineRenderer.SetPositions(new Vector3[] { originPosition, transform.position });
                lineRenderer.positionCount = 2;
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }
        }

        protected override bool IsMatchingHardwareRigPart(IHardwareRigPart rigPart)
        {
            if (rigPart is RayPointer rayPointer && EmittingRigPart != null && EmittingRigPart.LocalHardwareRigPart != null && EmittingRigPart.LocalHardwareRigPart == rayPointer.rigPart)
            {
                return true;
            }
            return false;
        }
    }

}
