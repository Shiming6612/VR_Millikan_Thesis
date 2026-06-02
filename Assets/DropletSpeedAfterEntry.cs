using UnityEngine;

public class DropletSpeedAfterEntry : MonoBehaviour
{
    [Header("References")]
    public Animator dropletsAnimator;
    public VoltageKnobRotator knob;

    [Header("Timing")]
    [Tooltip("When the droplets enter the plates (seconds from the beginning of the clip).")]
    public float entryTimeSeconds = 2.0f;

    [Tooltip("Total clip length in seconds.")]
    public float clipLengthSeconds = 4.125f;

    [Header("Speed Mapping")]
    public float normalSpeed = 1.0f;   // before entry
    public float minSpeed = 0.0f;      // at max voltage (freeze)

    [Header("Optional")]
    [Tooltip("If set, only applies when this state is playing (layer 0). Leave empty to always apply.")]
    public string stateName = "";

    [Tooltip("If true, never set speed to EXACT 0 (uses tiny epsilon). Turn OFF if you want true freeze.")]
    public bool useEpsilonInsteadOfZero = false;

    [Tooltip("Only used when useEpsilonInsteadOfZero = true")]
    public float epsilonSpeed = 0.0001f;

    private float _entryNorm;

    void Awake()
    {
        if (dropletsAnimator == null) dropletsAnimator = GetComponent<Animator>();
        _entryNorm = Mathf.Clamp01(entryTimeSeconds / clipLengthSeconds);
    }

    void Update()
    {
        if (dropletsAnimator == null || knob == null) return;

        var st = dropletsAnimator.GetCurrentAnimatorStateInfo(0);

        // If you want to restrict to one specific state, set stateName in Inspector.
        if (!string.IsNullOrEmpty(stateName) && !st.IsName(stateName))
            return;

        // normalizedTime: 0..1 for a single play (if not looping)
        float t = st.normalizedTime;

        if (t < _entryNorm)
        {
            // Before droplets enter the plates: always normal speed
            dropletsAnimator.speed = normalSpeed;
            return;
        }

        // After entry: knob controls speed
        float p = Mathf.Clamp01(knob.NormalizedValue); // 0..1 from your knob

        float target = Mathf.Lerp(normalSpeed, minSpeed, p);

        // Optional safeguard (some animator setups behave weird at exact speed 0)
        if (useEpsilonInsteadOfZero && target <= 0f)
            target = epsilonSpeed;

        dropletsAnimator.speed = target;
    }
}
