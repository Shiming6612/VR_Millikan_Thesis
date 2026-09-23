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

    [Header("Relative Force Imbalance")]
    [Tooltip("Absolute difference between electric/gravity ratio and 1 at maximum density. 0.5 means ratios 0.5 and 1.5 reach maximum density.")]
    [Min(0.01f)] public float imbalanceAtMaximumDensity = 0.5f;

    [Tooltip("1 = linear density response; larger values emphasize larger differences.")]
    public float densityResponsePower = 1.15f;

    // Legacy serialized fields retained, but no longer used. Balance and
    // zero voltage must remain visible when a valid droplet is selected.
    [HideInInspector] public float minNormalizedForceToShow = 0.01f;
    [HideInInspector] public float hoverProximityFalloff = 1f;
    [HideInInspector] public bool hideWhenVoltageZero = false;
    [HideInInspector] public float minVoltageToShowCloud = 0.01f;

    [Header("Cloud Shape")]
    public float cloudRadius = 0.6f;

    [Header("Particle Density")]
    [HideInInspector] public float minEmissionRate = 5f;
    [HideInInspector] public float maxEmissionRate = 320f;

    public int minCloudParticles = 25;
    public int maxCloudParticles = 200;

    // Legacy emission controls: particle count is now maintained directly.
    [HideInInspector] public bool useImmediateBurstFill = true;
    [HideInInspector] public int maxBurstParticlesPerStep = 80;
    [HideInInspector] public float burstInterval = 0.08f;

    [Header("Visual Style")]
    [Range(0f, 1f)]
    public float minCloudAlpha = 0.22f;

    [Range(0f, 1f)]
    public float maxCloudAlpha = 0.85f;

    public float particleSize = 0.018f;
    [HideInInspector] public Color lowDensityColor = new Color(0.55f, 0.82f, 1f, 1f);
    [HideInInspector] public Color highDensityColor = new Color(0.04f, 0.22f, 0.70f, 1f);

    [Header("Imbalance Colors")]
    public Color gravityDominantColor = new Color(0.05f, 0.12f, 0.80f, 1f);
    public Color balanceTint = new Color(0.55f, 0.90f, 0.90f, 1f);
    public Color electricDominantColor = new Color(0.08f, 0.65f, 0.12f, 1f);

    [HideInInspector] public Color weakerForceColor;
    [HideInInspector] public Color balancedForceColor;
    [HideInInspector] public Color strongerForceColor;

    // Legacy field retained for compatibility. A valid force reading is now
    // required so a manual object cannot falsely indicate balance.
    [HideInInspector] public bool fullCloudWhenManuallyAssignedWithoutDropData = true;

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
    private ParticleSystem.Particle[] particles;
    private bool hasForceState;
    private Color currentForceColor;

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
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = 0f;
        // Neutral base color prevents the old blue Start Color tinting the palette.
        main.startColor = Color.white;
        main.startSize = particleSize;
        main.maxParticles = Mathf.Max(1, maxCloudParticles);

        ApplyCloudShape();
        SetEmissionRate(0f);
        emission.enabled = false;
        var colorBySpeed = ps.colorBySpeed;
        colorBySpeed.enabled = false;
        ApplyColorGradient(balanceTint, 0f);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        if (ps != null) HideCloud();
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

    // Compatibility API: automatic force readings take precedence in LateUpdate.
    public void SetNormalizedForce(float normalizedForce)
    {
        currentNormalizedForce = Mathf.Clamp01(normalizedForce);
    }

    private void UpdateSelectedDropletAndForce()
    {
        hasForceState = false;
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

        if (float.IsNaN(ratio) || float.IsInfinity(ratio) ||
            float.IsNaN(tolerance) || float.IsInfinity(tolerance)) return 0f;

        hasForceState = true;
        float deviation = Mathf.Abs(ratio - 1f);
        tolerance = Mathf.Max(0f, tolerance);
        float limit = Mathf.Max(tolerance + 0.01f, imbalanceAtMaximumDensity);
        float strength = Mathf.Clamp01((deviation - tolerance) / (limit - tolerance));
        Color extreme = ratio < 1f ? gravityDominantColor : electricDominantColor;
        currentForceColor = Color.Lerp(balanceTint, extreme, strength);
        // Within OilDrop's balance tolerance: a sparse, pale cyan cloud.
        return strength;
    }

    private float CalculateNormalizedElectricForceFromTransform(Transform target)
    {
        if (target == null || !IsInsideField(target.position))
            return 0f;

        SelectableDrop selected = target.GetComponent<SelectableDrop>();

        if (selected == null)
            selected = target.GetComponentInParent<SelectableDrop>();

        if (selected == null)
            selected = target.GetComponentInChildren<SelectableDrop>();

        if (selected == null)
            return 0f;

        return CalculateNormalizedForceValue(selected);
    }

    private void UpdateCloud()
    {
        if (ps == null)
            return;

        ApplyCloudShape();

        bool shouldShow =
            visualizationEnabled &&
            hasForceState &&
            selectedDroplet != null;

        if (!shouldShow)
        {
            HideCloud();
            return;
        }

        if (followSelectedDroplet)
            transform.position = selectedDroplet.position + positionOffset;

        float t = Mathf.Clamp01(currentNormalizedForce);
        float densityT = Mathf.Pow(t, Mathf.Max(0.01f, densityResponsePower));

        int minimum = Mathf.Max(1, minCloudParticles);
        int maximum = Mathf.Max(minimum, maxCloudParticles);
        targetParticleCount = Mathf.RoundToInt(Mathf.Lerp(minimum, maximum, densityT));
        float currentAlpha = Mathf.Lerp(minCloudAlpha, maxCloudAlpha, densityT);
        // Hue identifies the dominant force. Density and opacity encode
        // relative imbalance; all living particles update together.
        Color currentColor = currentForceColor;
        currentAlpha *= currentColor.a;

        main.startSize = particleSize;
        main.maxParticles = maximum;

        SetEmissionRate(0f);
        emission.enabled = false;
        ApplyColorGradient(currentColor, currentAlpha);

        if (!ps.isPlaying)
            ps.Play();

        SynchronizeParticleCount();
    }

    private void SynchronizeParticleCount()
    {
        // Direct count control works in BOTH directions. Emission alone would
        // leave a dense cloud lingering after returning to balance.
        int capacity = main.maxParticles;
        if (particles == null || particles.Length < capacity)
            particles = new ParticleSystem.Particle[capacity];

        int count = ps.GetParticles(particles);
        if (count > targetParticleCount)
        {
            count = targetParticleCount;
            ps.SetParticles(particles, count);
        }
        if (count < targetParticleCount)
            ps.Emit(targetParticleCount - count);

        liveParticleCount = ps.particleCount;
    }

    [ContextMenu("Apply Imbalance Cloud Defaults")]
    private void ApplyImbalanceCloudDefaults()
    {
        imbalanceAtMaximumDensity = 0.5f;
        densityResponsePower = 1.15f;
        minCloudParticles = 25;
        maxCloudParticles = 200;
        minCloudAlpha = 0.22f;
        maxCloudAlpha = 0.85f;
        gravityDominantColor = new Color(0.05f, 0.12f, 0.80f, 1f);
        balanceTint = new Color(0.55f, 0.90f, 0.90f, 1f);
        electricDominantColor = new Color(0.08f, 0.65f, 0.12f, 1f);
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
        if (ps == null) return;

        SetEmissionRate(0f);
        ApplyColorGradient(lowDensityColor, 0f);

        if (ps != null)
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
                new GradientAlphaKey(alpha, 0f),
                new GradientAlphaKey(alpha, 0.2f),
                new GradientAlphaKey(alpha, 0.8f),
                new GradientAlphaKey(alpha, 1f)
            }
        );

        colorOverLifetime.color = gradient;
    }
}
