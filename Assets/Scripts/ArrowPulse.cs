using UnityEngine;

public class ArrowPulse : MonoBehaviour
{
    [Header("Pulse")]
    public Vector3 baseScale = Vector3.one;
    public float pulseMultiplier = 1.15f;
    public float pulseSpeed = 2.5f;
    public bool useUnscaledTime = false;

    private void OnEnable()
    {
        transform.localScale = baseScale;
    }

    private void Update()
    {
        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        float wave = (Mathf.Sin(t * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float scaleFactor = Mathf.Lerp(1f, pulseMultiplier, wave);
        transform.localScale = baseScale * scaleFactor;
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
    }
}