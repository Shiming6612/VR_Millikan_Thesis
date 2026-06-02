using UnityEngine;

public class DropletVoltageAnimator : MonoBehaviour
{
    [Header("References")]
    public Animator dropletsAnimator;
    public VoltageKnobRotator voltageKnob;

    [Header("Speed Mapping")]
    [Tooltip("Animator speed when knob is at MIN angle (0°).")]
    public float normalSpeed = 1.0f;

    [Tooltip("Animator speed when knob is at MAX angle (180°).")]
    public float minSpeed = 0.1f;

    private void Update()
    {
        if (dropletsAnimator == null || voltageKnob == null) return;

        // 0 at minAngle, 1 at maxAngle (so with 0..180 this maps perfectly)
        float t = voltageKnob.NormalizedValue;

        // Higher rotation => slower animation
        dropletsAnimator.speed = Mathf.Lerp(normalSpeed, minSpeed, t);
    }
}
