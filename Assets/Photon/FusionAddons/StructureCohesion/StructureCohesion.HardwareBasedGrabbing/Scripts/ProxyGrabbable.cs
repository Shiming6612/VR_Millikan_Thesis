using Fusion.XR.Shared.Core.HardwareBasedGrabbing;
using UnityEngine;

public class ProxyGrabbable : Grabbable
{
    public Grabbable targetGrabbable;

    Transform previousParent = null;
    Grabbable previousTargetGrabbable = null;
    public bool useParentAsTarget = true;

    public bool moveIfTargetFollowsAnotherGrabbable = false;
    public bool grabNewTarget = true;
    bool targetFollowingAnotherGrabble = false;

    protected override void Update()
    {
        CheckParent();
        bool targetChange = targetGrabbable != previousTargetGrabbable;
        bool targetStoppedFollowingAnotherGrabble = targetGrabbable == previousTargetGrabbable && targetFollowingAnotherGrabble && targetGrabbable.currentGrabber == null;
        if (targetChange || targetStoppedFollowingAnotherGrabble)
        {
            // Target change
            if (targetChange && previousTargetGrabbable && currentGrabber != null && previousTargetGrabbable.currentGrabber == currentGrabber)
            {
                // Still grabbing, and previous target grabbing was proxified: ungrabbing it
                previousTargetGrabbable.Ungrab();
            }
            if (targetGrabbable && currentGrabber != null && targetGrabbable.currentGrabber != currentGrabber)
            {
                // Still grabbing, and new target grabbing is not proxified: grabbing it
                if (targetGrabbable.currentGrabber != null)
                {
                    targetGrabbable.Ungrab();
                }
                targetGrabbable.Grab(currentGrabber);
            }
            targetFollowingAnotherGrabble = false;
        }
        previousTargetGrabbable = targetGrabbable;
        base.Update();        
    }

    void CheckParent()
    {
        if (useParentAsTarget && transform.parent != previousParent)
        {
            if (transform.parent)
            {
                targetGrabbable = transform.parent.GetComponent<Grabbable>();
            }
            else
            {
                targetGrabbable = null;
            }
        }
        previousParent = transform.parent;
    }
    public override void Grab(Grabber newGrabber, Transform grabPointTransform = null)
    {
        base.Grab(newGrabber, grabPointTransform);
        CheckParent();
        if (targetGrabbable)
        {
            targetGrabbable.Grab(newGrabber, grabPointTransform);
        }
    }

    public override void Ungrab()
    {
        base.Ungrab();
        CheckParent();
        if (targetGrabbable)
        {
            targetGrabbable.Ungrab();
        }
    }

    public override void Follow(Vector3 followedTransformPosition, Quaternion followedTransformRotation, Vector3 localPositionOffsetToFollowed, Quaternion localRotationOffsetTofollowed)
    {
        if (targetGrabbable)
        {
            if (targetGrabbable.currentGrabber != currentGrabber)
            {
                targetFollowingAnotherGrabble = true;
            }
            if (moveIfTargetFollowsAnotherGrabbable == false || targetFollowingAnotherGrabble == false)
            {
                // The target will be moved instead
                return;
            }
        }
        base.Follow(followedTransformPosition, followedTransformRotation, localPositionOffsetToFollowed, localRotationOffsetTofollowed);
    }

}
