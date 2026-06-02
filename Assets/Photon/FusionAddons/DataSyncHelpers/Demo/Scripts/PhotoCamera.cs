using Fusion.XR.Shared;
using Fusion.XR.Shared.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PhotoCamera : PhotoRecorder
{
    public InputActionProperty leftUseAction;
    public InputActionProperty rightUseAction;
    public float minAction = 0.05f;
    public float delayBetweenShot = 1.5f;
    protected INetworkGrabbable grabbable;
    InputActionProperty UseAction => IsGrabbed && grabbable.CurrentGrabberSide() == RigPartSide.Left ? leftUseAction : rightUseAction;
    public virtual bool IsGrabbed => grabbable != null && grabbable.IsGrabbed;
    public virtual bool IsGrabbedByLocalPLayer => IsGrabbed && grabbable != null && grabbable.IsGrabbedByLocalPlayer();
    public virtual bool IsUsed => UseAction.action.ReadValue<float>() > minAction;

    float lastShot = 0;

    [SerializeField] Renderer cameraOffScreen;

    protected override void Awake()
    {
        base.Awake();
        grabbable = GetComponent<INetworkGrabbable>();

        leftUseAction.EnableWithDefaultXRBindings(new List<string> { "<XRController>{LeftHand}/trigger", "<Keyboard>/space" });
        rightUseAction.EnableWithDefaultXRBindings(new List<string> { "<XRController>{RightHand}/trigger", "<Keyboard>/space" });
        ShutDownScreen();

        grabbable.OnGrab.AddListener(EnableScreen);
        grabbable.OnUngrab.AddListener(ShutDownScreen);
    }

    private void EnableScreen()
    {
        cameraOffScreen.enabled = false;
        foreach (var mirrorRenderer in mirrorRenderers)
        {
            mirrorRenderer.enabled = true;
        }
        captureCamera.enabled = true;
    }
    private void ShutDownScreen()
    {
        cameraOffScreen.enabled = true;
        foreach (var mirrorRenderer in mirrorRenderers)
        {
            mirrorRenderer.enabled = false;
        }
        captureCamera.enabled = false;
    }

    private void Update()
    {
        if (IsUsed && IsGrabbedByLocalPLayer && (Time.time - lastShot) > delayBetweenShot)
        {
            lastShot = Time.time;
            var picture = CreatePicture();
        }
    }
}
