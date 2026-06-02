using Fusion.XR.Shared.Core;
using UnityEngine;
using UnityEngine.InputSystem.UI;

public class XSCTrackedDeviceRaycaster : TrackedDeviceRaycaster
{
    protected Camera headsetCamera;
    protected IHardwareRig rig;
    Canvas raycasterCanvas = null;
    protected override void Awake()
    {
        base.Awake();
        raycasterCanvas = GetComponent<Canvas>();
    }

    protected virtual void DetectRig()
    {
        if (rig == null)
        {
            foreach (var r in HardwareRigsRegistry.GetAvailableHardwareRigs())
            {
                if (r.Headset != null)
                {
                    rig = r;
                    headsetCamera = rig.Headset.gameObject.GetComponentInChildren<Camera>();
                }
            }
        }
    }
    protected void Update()
    {
        DetectRig();
        if (raycasterCanvas && headsetCamera != null)
        {
            raycasterCanvas.worldCamera = headsetCamera;
        }
    }
}
