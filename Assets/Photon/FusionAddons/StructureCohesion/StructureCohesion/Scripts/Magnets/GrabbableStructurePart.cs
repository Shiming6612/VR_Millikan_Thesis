using Fusion.XR.Shared.Core;
using System.Threading.Tasks;
using UnityEngine;


namespace Fusion.Addons.StructureCohesion
{
    [DefaultExecutionOrder(GrabbableStructurePart.EXECUTION_ORDER)]
    public class GrabbableStructurePart : StructurePart
    {
        const int EXECUTION_ORDER = INetworkGrabbable.EXECUTION_ORDER + 10;
        [Header("Debug")]
        public bool fakeGrab = false;
        bool lastFakeGrab = false;

        INetworkGrabbable networkGrabbable;

        Pose? requestedPose;

        protected override void Awake()
        {
            base.Awake();
            networkGrabbable = GetComponentInParent<INetworkGrabbable>();
            networkGrabbable.OnUngrab.AddListener(OnDidUngrab);
        }

        private void OnDidUngrab()
        {
            AttachClosestPartInProximity();
        }

        protected override void UpdateIsMoving()
        {
            if (Object.HasStateAuthority)
            {
                IsMoving = networkGrabbable.IsGrabbed;
                IsMoving = IsMoving || fakeGrab;
            }
        }

        public override void Render()
        {
            base.Render();

            if (fakeGrab == false && lastFakeGrab)
            {
                AttachClosestPartInProximity();
            }
            lastFakeGrab = fakeGrab;
            if (networkGrabbable.IsReceivingAuthority)
            {
                isStill = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            if (requestedPose is Pose pose)
            {
                MoveToPosition(pose);
                requestedPose = null;
            }
        }

        public void RequestMoveToPosition(Pose pose)
        {
            requestedPose = pose;
            if (Object.HasStateAuthority == false)
            {
                Object.RequestStateAuthority();
            }
        }

        #region Parenting
        // We want to move in FUN to be sure of aligment
        public bool CanChangeNetworkTransformImmediatly()
        {
            return (currentPhase == StructurePhase.FUN || currentPhase == StructurePhase.AfterTick);
        }
        #endregion

        public async Task WaitMoveToPosition(Pose pose)
        {
            if (CanChangeNetworkTransformImmediatly())
            {
                MoveToPosition(pose);
            } 
            else
            {
                RequestMoveToPosition(pose);
                while(requestedPose != null)
                {
                    await AsyncTask.Delay(10);
                }
            }
        }

        void MoveToPosition(Pose pose)
        {
            if(CanChangeNetworkTransformImmediatly() == false)
            {
                throw new System.Exception("Cannot move directly");
            }
            transform.position = pose.position;
            transform.rotation = pose.rotation;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            networkGrabbable?.OnUngrab?.RemoveListener(OnDidUngrab);
        }
    }

}
