using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class SelectedDropletTrail : MonoBehaviour
{
    [Header("References")]
    public VisualizationModeController visualizationModeController;

    [Header("Speed Mapping")]
    public float minSpeedToShow = 0.002f;
    public float maxSpeedForFullTrail = 0.05f;

    [Header("Trail Width")]
    public float minWidth = 0.002f;
    public float maxWidth = 0.035f;

    [Header("Trail Time")]
    public float minTime = 0.2f;
    public float maxTime = 4.0f;

    // Retained for serialized compatibility; color now represents force state.
    [HideInInspector]
    public Color lowSpeedColor =
        new Color(0.914f, 0.667f, 0.053f, 0.714f);

    [HideInInspector]
    public Color highSpeedColor =
        new Color(0.784f, 0.105f, 0.493f, 0.761f);

    [Header("Force State Colors - Same In Both Visualizations")]
    public Color weakerForceColor = new Color(0.00f, 0.45f, 0.70f, 1f);
    public Color balancedForceColor = new Color(0.00f, 0.70f, 0.32f, 1f);
    public Color strongerForceColor = new Color(0.90f, 0.40f, 0.05f, 1f);

    [Header("Force Reference - Auto Find If Empty")]
    public ElectricFieldVolume fieldVolume;
    [Range(0f, 1f)] public float trailOpacity = 0.75f;

    [Header("Runtime")]
    public bool isSelected = false;

    private TrailRenderer trail;
    private Vector3 lastPosition;
    private bool initialized;
    private SelectableDrop selectable;
    private Color currentForceColor;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();

        if (visualizationModeController == null)
        {
            visualizationModeController =
                FindFirstObjectByType<VisualizationModeController>();
        }

        if (fieldVolume == null)
            fieldVolume = FindFirstObjectByType<ElectricFieldVolume>();
        selectable = GetComponent<SelectableDrop>();
        if (selectable == null) selectable = GetComponentInParent<SelectableDrop>();
        if (selectable == null) selectable = GetComponentInChildren<SelectableDrop>();

        trail.numCornerVertices = 6;
        trail.numCapVertices = 4;

        lastPosition = transform.position;
        initialized = true;

        HideTrail();
    }

    private void Update()
    {
        if (!initialized)
        {
            lastPosition = transform.position;
            initialized = true;
            return;
        }

        bool trailSwitchEnabled =
            visualizationModeController == null ||
            visualizationModeController.TrailEnabled;

        if (!isSelected || !trailSwitchEnabled)
        {
            HideTrail();
            lastPosition = transform.position;
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed =
            Vector3.Distance(transform.position, lastPosition) / deltaTime;

        lastPosition = transform.position;

        // Use the same smoothed force ratio and balance band as droplet motion.
        // No force data means no state color, rather than a guessed state.
        if (fieldVolume == null || selectable == null ||
            !fieldVolume.TryGetBalanceState(selectable, out float ratio, out float tolerance))
        {
            HideTrail();
            return;
        }
        currentForceColor = Mathf.Abs(ratio - 1f) <= tolerance
            ? balancedForceColor
            : ratio < 1f ? weakerForceColor : strongerForceColor;
        currentForceColor.a *= Mathf.Clamp01(trailOpacity);
        UpdateTrailBySpeed(speed);
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        initialized = true;
        HideTrail();
    }

    private void OnDisable()
    {
        HideTrail();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (isSelected)
            ClearTrail();
        else
            HideTrail();
    }

    public void ShowTrail()
    {
        isSelected = true;
        ClearTrail();
    }

    public void HideTrail()
    {
        if (trail == null)
            return;

        trail.emitting = false;
        trail.Clear();
    }

    public void ClearTrail()
    {
        if (trail == null)
            return;

        trail.Clear();
        lastPosition = transform.position;
    }

    private void UpdateTrailBySpeed(float speed)
    {
        if (trail == null)
            return;

        if (speed < minSpeedToShow)
        {
            trail.emitting = false;
            trail.Clear();
            return;
        }

        float t = Mathf.InverseLerp(
            minSpeedToShow,
            maxSpeedForFullTrail,
            speed
        );

        float width = Mathf.Lerp(minWidth, maxWidth, t);
        float time = Mathf.Lerp(minTime, maxTime, t);
        // Width/lifetime still encode speed. The WHOLE visible trail uses
        // current force-state color; it is not a history of past force colors.
        Color color = currentForceColor;

        trail.emitting = true;
        trail.time = time;
        trail.startWidth = width;
        trail.endWidth = width * 0.2f;
        trail.colorGradient = CreateTrailGradient(color);
    }

    private Gradient CreateTrailGradient(Color mainColor)
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(mainColor, 0f),
                new GradientColorKey(mainColor, 0.5f),
                new GradientColorKey(mainColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(mainColor.a, 0.35f),
                new GradientAlphaKey(mainColor.a, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        return gradient;
    }
}

