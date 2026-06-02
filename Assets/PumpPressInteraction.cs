using Fusion;
using UnityEngine;

public class PumpPressInteraction : NetworkBehaviour
{
    [Header("Animators")]
    public Animator sprayAnimator;
    public Animator betweenPlatesAnimator;

    [Header("Animator Triggers (exact parameter names)")]
    public string sprayTrigger = "Spray";
    public string platesTrigger = "StartDroplets";

    [Header("Global Lock (seconds)")]
    [Tooltip("Lock duration for EVERYONE after firing. Set to your longest clip length.")]
    public float globalLockSeconds = 2.0f;

    [Header("Controller Grip")]
    public bool enableControllerGrip = true;

    // -------- Networked global lock --------
    [Networked]
    private TickTimer GlobalLockTimer { get; set; }

    // -------- Local proximity tracking --------
    int _pinchInsideCount = 0;
    int _controllerInsideCount = 0;
    bool _handFiredThisTouch = false;
    bool ControllerNear => _controllerInsideCount > 0;

    public override void FixedUpdateNetwork()
    {
        // IMPORTANT: Release lock when timer expires (only the authority should do this)
        if (HasStateAuthority && GlobalLockTimer.IsRunning && GlobalLockTimer.Expired(Runner))
        {
            GlobalLockTimer = default;
        }
    }

    void Update()
    {
        // Controller grip -> request when near
        if (enableControllerGrip && ControllerNear)
        {
            bool leftGripDown = OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger); // left grip
            bool rightGripDown = OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger); // right grip

            if (leftGripDown || rightGripDown)
            {
                RequestFirePump();
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsHandPinchCollider(other.name))
        {
            _pinchInsideCount++; // Hands: fire only once per touch session
            if (!_handFiredThisTouch)
            {
                _handFiredThisTouch = true;
                RequestFirePump();
            }
        }
        if (IsControllerCollider(other.name))
        {
            _controllerInsideCount++;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsHandPinchCollider(other.name))
        {
            _pinchInsideCount = Mathf.Max(0, _pinchInsideCount - 1);
            if (_pinchInsideCount == 0) _handFiredThisTouch = false;
        }
        if (IsControllerCollider(other.name))
        {
            _controllerInsideCount = Mathf.Max(0, _controllerInsideCount - 1);
        }
    }

    // ----------------- Networking -----------------
    void RequestFirePump()
    {
        if (HasStateAuthority) TryFire_AsAuthority();
        else RPC_RequestFirePump();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_RequestFirePump()
    {
        TryFire_AsAuthority();
    }

    void TryFire_AsAuthority()
    {
        // Reject while globally locked
        if (GlobalLockTimer.IsRunning && !GlobalLockTimer.Expired(Runner)) return;

        // Start / restart global lock
        GlobalLockTimer = TickTimer.CreateFromSeconds(Runner, globalLockSeconds);
        RPC_FirePump();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_FirePump()
    {
        if (sprayAnimator != null)
        {
            sprayAnimator.Play(0, 0, 0f);
            sprayAnimator.ResetTrigger(sprayTrigger);
            sprayAnimator.SetTrigger(sprayTrigger);
        }

        if (betweenPlatesAnimator != null)
        {
            betweenPlatesAnimator.Play(0, 0, 0f);
            betweenPlatesAnimator.ResetTrigger(platesTrigger);
            betweenPlatesAnimator.SetTrigger(platesTrigger);
        }
    }

    // ----------------- Name filters (based on your logs) -----------------
    bool IsHandPinchCollider(string n) => n.Contains("PinchArea") || n.Contains("PinchPointRange");
    bool IsControllerCollider(string n) => n.Contains("ControllerGrabLocation") || n.Contains("GrabbingCollider");
}
