using Fusion;
using UnityEngine;
using Oculus.Interaction;

public class VoltageKnobRotator : NetworkBehaviour
{
    [Header("References")]
    public Transform knobTransform;
    public Transform pivot;
    public GrabInteractable grabInteractable;
    public Transform leftHandTransform;
    public Transform rightHandTransform;

    [Header("Rotation Settings")]
    public Vector3 localAxis = Vector3.forward;
    public float minAngle = 0f;
    public float maxAngle = 180f;
    public float rotationSpeed = 1.0f;

    [Header("Networking")]
    [Tooltip("How often to send rotation updates while grabbed (seconds). 0.05–0.12 is good for Quest).")]
    public float sendInterval = 0.08f;

    [Tooltip("Min change (degrees) before sending an update.")]
    public float sendThresholdDeg = 0.25f;

    [Header("Debug")]
    public bool logDebug = false;

    // Local state
    private bool _isGrabbed;
    private Transform _grabberTf;
    private float _currentAngle;
    private Vector3 _prevVectorOnPlane;
    private bool _hasPrevVector;

    private float _nextSendTime;
    private float _lastSentAngle;

    // --- Networked state (StateAuthority owns these) ---
    [Networked] private bool IsKnobLocked { get; set; }

    // Who is allowed to rotate right now
    [Networked] private PlayerRef LockedBy { get; set; }

    // The angle everyone should render
    [Networked] private float NetworkedRotation { get; set; }

    public float NormalizedValue => Mathf.InverseLerp(minAngle, maxAngle, _currentAngle);

    private void Awake()
    {
        if (knobTransform == null) knobTransform = transform;
        if (pivot == null) pivot = knobTransform;
    }

    public override void Spawned()
    {
        // Initialize visuals from networked state
        _currentAngle = Mathf.Clamp(NetworkedRotation, minAngle, maxAngle);
        ApplyAngle(_currentAngle);
        _lastSentAngle = _currentAngle;
    }

    // -------------------------
    // Grab callbacks (Controller)
    // -------------------------
    public void OnGrab()
    {
        var grabber = FindSelectingInteractorTransformFromGrab();
        RequestBeginGrab(grabber);
    }

    public void OnRelease()
    {
        RequestEndGrab();
    }

    // -------------------------
    // Grab callbacks (Hands)
    // -------------------------
    public void OnGrabHand()
    {
        var grabber = FindClosestHandTransform();
        RequestBeginGrab(grabber);
    }

    public void OnReleaseHand()
    {
        RequestEndGrab();
    }

    // -------------------------
    // Networking: Begin/End grab
    // -------------------------
    void RequestBeginGrab(Transform grabberTf)
    {
        _grabberTf = grabberTf;

        // If already locked by someone else, ignore
        if (IsKnobLocked && LockedBy != Runner.LocalPlayer)
            return;

        // Ask StateAuthority to lock it for me
        if (HasStateAuthority)
            TryBeginGrab_AsAuthority(Runner.LocalPlayer);
        else
            Rpc_RequestBeginGrab(Runner.LocalPlayer);

        // Start local interaction immediately (feels responsive)
        _isGrabbed = true;
        BeginDrag();
        _nextSendTime = Time.time;
        _lastSentAngle = _currentAngle;

        if (logDebug) Debug.Log($"[Knob] BeginGrab request by {Runner.LocalPlayer}");
    }

    void RequestEndGrab()
    {
        _isGrabbed = false;
        _grabberTf = null;
        _hasPrevVector = false;

        if (HasStateAuthority)
            TryEndGrab_AsAuthority(Runner.LocalPlayer);
        else
            Rpc_RequestEndGrab(Runner.LocalPlayer);

        if (logDebug) Debug.Log($"[Knob] EndGrab request by {Runner.LocalPlayer}");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void Rpc_RequestBeginGrab(PlayerRef who)
    {
        TryBeginGrab_AsAuthority(who);
    }

    void TryBeginGrab_AsAuthority(PlayerRef who)
    {
        // Already locked by another player
        if (IsKnobLocked && LockedBy != who)
            return;

        IsKnobLocked = true;
        LockedBy = who;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void Rpc_RequestEndGrab(PlayerRef who)
    {
        TryEndGrab_AsAuthority(who);
    }

    void TryEndGrab_AsAuthority(PlayerRef who)
    {
        if (!IsKnobLocked) return;
        if (LockedBy != who) return;

        IsKnobLocked = false;
        LockedBy = default;
    }

    // -------------------------
    // Rotation logic + send to StateAuthority
    // -------------------------
    void Update()
    {
        // Everyone (including non-grabbers) should always render the networked angle:
        // If I'm NOT the one rotating, I just follow NetworkedRotation.
        if (!_isGrabbed || LockedBy != Runner.LocalPlayer)
        {
            float target = Mathf.Clamp(NetworkedRotation, minAngle, maxAngle);
            if (Mathf.Abs(target - _currentAngle) > 0.001f)
            {
                _currentAngle = target;
                ApplyAngle(_currentAngle);
            }
            return;
        }

        // I'm the active rotator (locally)
        if (_grabberTf == null || knobTransform == null) return;

        Vector3 axisWorld = knobTransform.TransformDirection(localAxis).normalized;

        Vector3 currentVec = Vector3.ProjectOnPlane(_grabberTf.position - pivot.position, axisWorld);
        if (currentVec.sqrMagnitude < 1e-6f) return;
        currentVec.Normalize();

        if (!_hasPrevVector)
        {
            _prevVectorOnPlane = currentVec;
            _hasPrevVector = true;
            return;
        }

        float delta = Vector3.SignedAngle(_prevVectorOnPlane, currentVec, axisWorld);
        _currentAngle = Mathf.Clamp(_currentAngle + delta * rotationSpeed, minAngle, maxAngle);
        ApplyAngle(_currentAngle);
        _prevVectorOnPlane = currentVec;

        // Throttle network sends
        if (Time.time >= _nextSendTime && Mathf.Abs(_currentAngle - _lastSentAngle) >= sendThresholdDeg)
        {
            _nextSendTime = Time.time + sendInterval;
            _lastSentAngle = _currentAngle;

            if (HasStateAuthority)
                SetRotation_AsAuthority(_currentAngle, Runner.LocalPlayer);
            else
                Rpc_SendRotation(_currentAngle, Runner.LocalPlayer);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void Rpc_SendRotation(float angle, PlayerRef who)
    {
        SetRotation_AsAuthority(angle, who);
    }

    void SetRotation_AsAuthority(float angle, PlayerRef who)
    {
        // Only accept updates from the current lock owner
        if (!IsKnobLocked) return;
        if (LockedBy != who) return;

        NetworkedRotation = Mathf.Clamp(angle, minAngle, maxAngle);
    }

    // -------------------------
    // Drag start
    // -------------------------
    void BeginDrag()
    {
        _hasPrevVector = false;

        if (_grabberTf == null) return;

        Vector3 axisWorld = knobTransform.TransformDirection(localAxis).normalized;
        Vector3 v = Vector3.ProjectOnPlane(_grabberTf.position - pivot.position, axisWorld);
        if (v.sqrMagnitude < 1e-6f) v = Vector3.ProjectOnPlane(_grabberTf.forward, axisWorld);
        if (v.sqrMagnitude < 1e-6f) return;

        _prevVectorOnPlane = v.normalized;
        _hasPrevVector = true;
    }

    void ApplyAngle(float angle)
    {
        knobTransform.localRotation = Quaternion.AngleAxis(angle, localAxis);
    }

    // -------------------------
    // Helpers
    // -------------------------
    Transform FindSelectingInteractorTransformFromGrab()
    {
        if (grabInteractable == null) return null;
        var views = grabInteractable.SelectingInteractorViews;
        if (views == null) return null;

        foreach (var v in views)
            if (v is Component c) return c.transform;

        return null;
    }

    Transform FindClosestHandTransform()
    {
        if (leftHandTransform == null && rightHandTransform == null) return null;
        if (leftHandTransform != null && rightHandTransform == null) return leftHandTransform;
        if (rightHandTransform != null && leftHandTransform == null) return rightHandTransform;

        float dl = Vector3.Distance(leftHandTransform.position, pivot.position);
        float dr = Vector3.Distance(rightHandTransform.position, pivot.position);
        return (dl <= dr) ? leftHandTransform : rightHandTransform;
    }
}
