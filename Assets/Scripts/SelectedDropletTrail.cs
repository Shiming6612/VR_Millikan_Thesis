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

    [Header("Color")]
    public Color lowSpeedColor =
        new Color(0.914f, 0.667f, 0.053f, 0.714f);

    public Color highSpeedColor =
        new Color(0.784f, 0.105f, 0.493f, 0.761f);

    [Header("Runtime")]
    public bool isSelected = false;

    private TrailRenderer trail;
    private Vector3 lastPosition;
    private bool initialized;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();

        if (visualizationModeController == null)
        {
            visualizationModeController =
                FindFirstObjectByType<VisualizationModeController>();
        }

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

        UpdateTrailBySpeed(speed);
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
        Color color = Color.Lerp(lowSpeedColor, highSpeedColor, t);

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
