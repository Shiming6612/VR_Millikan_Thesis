using UnityEngine;

[DisallowMultipleComponent]
public class DropProperties : MonoBehaviour
{
    [Header("Radius / Mass")]
    public float oilDensityKgPerM3 = 875.3f;
    public float minRadiusMicrometer = 0.5f;
    public float maxRadiusMicrometer = 1.0f;

    [Header("Charge")]
    public int minChargeMultiple = 1;
    public int maxChargeMultiple = 12;

    [Header("Options")]
    public bool randomizeOnSpawn = false;
    public bool applyMassToRigidbody = true;

    [Header("Visual Radius")]
    public bool applyVisualScale = true;
    public Transform visualRoot;
    public float visualReferenceRadiusMicrometer = 1.0f;
    public float visualScaleStrength = 1.0f;

    public float RadiusMicrometer { get; private set; }
    public float MassKg { get; private set; }
    public float ChargeC { get; private set; }
    public int ChargeMultiple { get; private set; }

    private Rigidbody rb;
    private Vector3 initialVisualScale;
    private bool visualScaleCached;

    private const double ElementaryCharge = 1.602176634e-19;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (visualRoot == null)
            visualRoot = transform;

        CacheVisualScale();
    }

    private void OnEnable()
    {
        if (randomizeOnSpawn)
            RandomizeAndApply();
    }

    public void RandomizeAndApply()
    {
        float radius = Random.Range(
            Mathf.Min(minRadiusMicrometer, maxRadiusMicrometer),
            Mathf.Max(minRadiusMicrometer, maxRadiusMicrometer)
        );

        int charge = Random.Range(
            Mathf.Min(minChargeMultiple, maxChargeMultiple),
            Mathf.Max(minChargeMultiple, maxChargeMultiple) + 1
        );

        ApplyRadiusAndCharge(radius, charge);
    }

    public void ApplyRadiusAndCharge(float radiusMicrometer, int chargeMultiple)
    {
        RadiusMicrometer = Mathf.Max(0.01f, radiusMicrometer);

        ChargeMultiple = Mathf.Max(1, chargeMultiple);
        ChargeC = (float)(ChargeMultiple * ElementaryCharge);

        MassKg = CalculateMassFromRadius(RadiusMicrometer);

        if (applyMassToRigidbody && rb != null)
            rb.mass = MassKg;

        ApplyVisualRadius();
    }

    public void ApplyRadiusAndAutoCharge(
        float radiusMicrometer,
        float targetHoverVoltage,
        float plateSpacingMeters,
        float gravity = 9.81f)
    {
        float radius = Mathf.Max(0.01f, radiusMicrometer);
        float mass = CalculateMassFromRadius(radius);

        float targetVoltage = Mathf.Max(1f, targetHoverVoltage);
        float d = Mathf.Max(0.0001f, plateSpacingMeters);

        float requiredChargeC = mass * gravity * d / targetVoltage;
        int chargeMultiple = Mathf.RoundToInt(requiredChargeC / (float)ElementaryCharge);

        chargeMultiple = Mathf.Clamp(
            chargeMultiple,
            Mathf.Min(minChargeMultiple, maxChargeMultiple),
            Mathf.Max(minChargeMultiple, maxChargeMultiple)
        );

        ApplyRadiusAndCharge(radius, chargeMultiple);
    }

    private float CalculateMassFromRadius(float radiusMicrometer)
    {
        float r = radiusMicrometer * 1e-6f;
        float volume = (4f / 3f) * Mathf.PI * r * r * r;
        return oilDensityKgPerM3 * volume;
    }

    private void CacheVisualScale()
    {
        if (visualRoot == null || visualScaleCached)
            return;

        initialVisualScale = visualRoot.localScale;
        visualScaleCached = true;
    }

    private void ApplyVisualRadius()
    {
        if (!applyVisualScale || visualRoot == null)
            return;

        CacheVisualScale();

        float reference = Mathf.Max(0.01f, visualReferenceRadiusMicrometer);
        float radiusRatio = RadiusMicrometer / reference;
        float scaleRatio = Mathf.Lerp(1f, radiusRatio, Mathf.Clamp01(visualScaleStrength));

        visualRoot.localScale = initialVisualScale * scaleRatio;
    }
}