using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class FieldParticleCloudVisualizer : MonoBehaviour
{
    [Header("Refs")]
    public DropSelectionManager selectionManager;
    public ElectricFieldVolume fieldVolume;
    public VoltageKnobInput voltageSource;

    [Header("Selected Droplet")]
    public Transform selectedDroplet;

    [Header("Follow Settings")]
    public bool followSelectedDroplet = true;
    public Vector3 positionOffset = Vector3.zero;

    [Header("Hover Proximity Mapping")]
    [Range(0f, 1f)]
    public float minNormalizedForceToShow = 0.01f;

    [Tooltip("Electric-force ratio distance from the hover point over which the cloud fades from full to empty. A value of 1 means ratios 0 and 2 are fully faded, while ratio 1 is fully visible.")]
    public float hoverProximityFalloff = 1f;

    [Tooltip("Higher values make weak forces less visible and strong forces more dominant.")]
    public float densityResponsePower = 1.15f;

    [Header("Voltage Visibility")]
    public bool hideWhenVoltageZero = true;
    public float minVoltageToShowCloud = 0.01f;

    [Header("Cloud Shape")]
    public float cloudRadius = 0.6f;

    [Header("Particle Density")]
    public float minEmissionRate = 5f;
    public float maxEmissionRate = 320f;

    public int minCloudParticles = 40;
    public int maxCloudParticles = 650;

    [Header("Immediate Density Fill")]
    public bool useImmediateBurstFill = true;
    public int maxBurstParticlesPerStep = 80;
    public float burstInterval = 0.08f;

    [Header("Visual Style")]
    [Range(0f, 1f)]
    public float minCloudAlpha = 0.08f;

    [Range(0f, 1f)]
    public float maxCloudAlpha = 0.75f;

    public float particleSize = 0.018f;
    public Color lowDensityColor = new Color(0.55f, 0.82f, 1f, 1f);
    public Color highDensityColor = new Color(0.04f, 0.22f, 0.70f, 1f);

    [Header("Manual Test Fallback")]
    public bool fullCloudWhenManuallyAssignedWithoutDropData = true;

    [Header("Runtime")]
    public bool visualizationEnabled = false;

    [SerializeField, Range(0f, 1f)]
    private float currentNormalizedForce = 0f;

    [SerializeField]
    private int targetParticleCount = 0;

    [SerializeField]
    private int liveParticleCount = 0;

    private ParticleSystem ps;
    private ParticleSystem.MainModule main;
    private ParticleSystem.EmissionModule emission;
    private ParticleSystem.ShapeModule shape;
    private ParticleSystem.ColorOverLifetimeModule colorOverLifetime;

    private BoxCollider fieldBox;
    private Transform currentTarget;
    private float nextBurstTime;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();

        main = ps.main;
        emission = ps.emission;
        shape = ps.shape;
        colorOverLifetime = ps.colorOverLifetime;

        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<DropSelectionManager>();

        if (fieldVolume == null)
            fieldVolume = FindFirstObjectByType<ElectricFieldVolume>();

        if (voltageSource == null)
            voltageSource = FindFirstObjectByType<VoltageKnobInput>();

        if (fieldVolume != null)
            fieldBox = fieldVolume.GetComponent<BoxCollider>();

        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = 0f;
        main.startSize = particleSize;
        main.maxParticles = Mathf.Max(1, maxCloudParticles);

        ApplyCloudShape();
        SetEmissionRate(0f);
        ApplyColorGradient(lowDensityColor, 0f);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void LateUpdate()
    {
        UpdateSelectedDropletAndForce();
        UpdateCloud();
    }

    public void SetVisualizationEnabled(bool enabled)
    {
        visualizationEnabled = enabled;

        if (!visualizationEnabled)
            HideCloud();
    }

    public void SetSelectedDroplet(Transform droplet)
    {
        if (selectedDroplet == droplet)
            return;

        selectedDroplet = droplet;
        currentTarget = droplet;
        nextBurstTime = 0f;

        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void ClearSelectedDroplet()
    {
        selectedDroplet = null;
        currentTarget = null;
        currentNormalizedForce = 0f;
        targetParticleCount = 0;
        liveParticleCount = 0;

        HideCloud();
    }

    public void SetNormalizedForce(float normalizedForce)
    {
        currentNormalizedForce = Mathf.Clamp01(normalizedForce);
    }

    private void UpdateSelectedDropletAndForce()
    {
        SelectableDrop selected = selectionManager != null ? selectionManager.CurrentSelected : null;

        if (selected == null)
        {
            if (selectedDroplet == null)
            {
                currentTarget = null;
                currentNormalizedForce = 0f;
                return;
            }

            currentTarget = selectedDroplet;
            currentNormalizedForce = CalculateNormalizedElectricForceFromTransform(selectedDroplet);
            return;
        }

        Transform target = GetSelectedTargetTransform(selected);

        if (target == null || !IsInsideField(target.position))
        {
            selectedDroplet = null;
            currentTarget = null;
            currentNormalizedForce = 0f;
            return;
        }

        if (currentTarget != target)
        {
            currentTarget = target;
            selectedDroplet = target;
            nextBurstTime = 0f;

            if (ps != null)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        currentNormalizedForce = CalculateNormalizedForceValue(selected);
    }

    private float CalculateNormalizedForceValue(SelectableDrop selected)
    {
        if (fieldVolume == null ||
            !fieldVolume.TryGetBalanceState(selected, out float ratio, out float tolerance))
            return 0f;

        if (hideWhenVoltageZero &&
            fieldVolume.SmoothedVoltageMagnitude <= minVoltageToShowCloud)
            return 0f;

        // The cloud reaches its maximum throughout the same balance band
        // used by OilDrop. This indicates force balance, not zero velocity.
        float deviation = Mathf.Abs(ratio - 1f);
        if (deviation <= tolerance) return 1f;
        float outerLimit = Mathf.Max(tolerance + 0.01f, hoverProximityFalloff);
        return Mathf.Clamp01(1f - (deviation - tolerance) / (outerLimit - tolerance));
    }

    private float CalculateNormalizedElectricForceFromTransform(Transform target)
    {
        if (target == null)
            return 0f;

        SelectableDrop selected = target.GetComponent<SelectableDrop>();

        if (selected == null)
            selected = target.GetComponentInParent<SelectableDrop>();

        if (selected == null)
            selected = target.GetComponentInChildren<SelectableDrop>();

        if (selected == null)
            return fullCloudWhenManuallyAssignedWithoutDropData ? 1f : 0f;

        return CalculateNormalizedForceValue(selected);
    }

    private void UpdateCloud()
    {
        if (ps == null)
            return;

        ApplyCloudShape();

        bool shouldShow =
            visualizationEnabled &&
            selectedDroplet != null &&
            currentNormalizedForce > minNormalizedForceToShow;

        if (!shouldShow)
        {
            HideCloud();
            return;
        }

        if (followSelectedDroplet)
            transform.position = selectedDroplet.position + positionOffset;

        float t = Mathf.Clamp01(currentNormalizedForce);
        float densityT = Mathf.Pow(t, Mathf.Max(0.01f, densityResponsePower));

        float emissionRate = Mathf.Lerp(minEmissionRate, maxEmissionRate, densityT);
        targetParticleCount = Mathf.RoundToInt(Mathf.Lerp(minCloudParticles, maxCloudParticles, densityT));
        float currentAlpha = Mathf.Lerp(minCloudAlpha, maxCloudAlpha, densityT);
        Color currentColor = Color.Lerp(lowDensityColor, highDensityColor, densityT);

        main.startSize = particleSize;
        main.maxParticles = Mathf.Max(1, maxCloudParticles);

        SetEmissionRate(emissionRate);
        ApplyColorGradient(currentColor, currentAlpha);

        if (!ps.isPlaying)
            ps.Play();

        liveParticleCount = ps.particleCount;

        if (useImmediateBurstFill)
            FillDensityWithBursts();
    }

    private void FillDensityWithBursts()
    {
        if (Time.time < nextBurstTime)
            return;

        int missing = targetParticleCount - ps.particleCount;

        if (missing <= 0)
            return;

        int emitCount = Mathf.Min(missing, Mathf.Max(1, maxBurstParticlesPerStep));

        ps.Emit(emitCount);

        nextBurstTime = Time.time + Mathf.Max(0.01f, burstInterval);
    }

    private float GetHoverVoltage(SelectableDrop selected)
    {
        if (selected == null || fieldVolume == null)
            return 0f;

        DropProperties dp = FindDropProperties(selected);

        if (dp == null)
            return 0f;

        float charge = Mathf.Abs(dp.ChargeC);

        if (charge < 1e-20f)
            return 0f;

        float d = fieldVolume.GetPlateSpacingMeters();

        if (d <= 1e-6f)
            return 0f;

        Vector3 dir = fieldVolume.fieldDirection.sqrMagnitude > 1e-6f
            ? fieldVolume.fieldDirection.normalized
            : Vector3.up;

        Vector3 gravity = GetGravityVector(selected);
        float g = Mathf.Abs(Vector3.Dot(gravity, dir));

        float scale = Mathf.Max(1e-6f, fieldVolume.fieldScale);
        float effectiveWeight = dp.MassKg * g;

        return (effectiveWeight * d) / (charge * scale);
    }

    private Transform GetSelectedTargetTransform(SelectableDrop selected)
    {
        Rigidbody rb = selected.GetComponent<Rigidbody>();

        if (rb == null)
            rb = selected.GetComponentInParent<Rigidbody>();

        if (rb == null)
            rb = selected.GetComponentInChildren<Rigidbody>();

        return rb != null ? rb.transform : selected.transform;
    }

    private bool IsInsideField(Vector3 position)
    {
        if (fieldBox == null)
            return true;

        return fieldBox.bounds.Contains(position);
    }

    private Vector3 GetGravityVector(SelectableDrop selected)
    {
        Vector3 gravity = Physics.gravity;

        Rigidbody rb = selected.GetComponent<Rigidbody>();

        if (rb == null)
            rb = selected.GetComponentInParent<Rigidbody>();

        if (rb == null)
            rb = selected.GetComponentInChildren<Rigidbody>();

        if (rb != null)
        {
            OilDrop oilDrop = rb.GetComponent<OilDrop>();

            if (oilDrop != null)
                gravity = oilDrop.customGravity;
        }

        return gravity;
    }

    private DropProperties FindDropProperties(SelectableDrop selected)
    {
        DropProperties dp = selected.GetComponent<DropProperties>();

        if (dp == null)
            dp = selected.GetComponentInParent<DropProperties>();

        if (dp == null)
            dp = selected.GetComponentInChildren<DropProperties>();

        return dp;
    }

    private void ApplyCloudShape()
    {
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.001f, cloudRadius);
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = Vector3.one;
    }

    private void HideCloud()
    {
        targetParticleCount = 0;
        liveParticleCount = 0;

        SetEmissionRate(0f);
        ApplyColorGradient(lowDensityColor, 0f);

        if (ps != null && ps.isPlaying)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void SetEmissionRate(float rateValue)
    {
        ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
        rate.constant = rateValue;
        emission.rateOverTime = rate;
    }

    private void ApplyColorGradient(Color color, float alpha)
    {
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 0.2f),
                new GradientColorKey(color, 0.8f),
                new GradientColorKey(color, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(alpha, 0.2f),
                new GradientAlphaKey(alpha, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        colorOverLifetime.color = gradient;
    }
}


