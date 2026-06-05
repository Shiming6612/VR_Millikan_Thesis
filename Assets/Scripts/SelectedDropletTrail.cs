using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class SelectedDropletTrail : MonoBehaviour
{
    [Header("References")]
    public VisualizationModeController visualizationModeController;

    [Header("Speed Mapping")]
    public float minSpeedToShow = 0.005f;
    public float maxSpeedForFullTrail = 0.08f;

    [Header("Trail Width")]
    public float minWidth = 0.002f;
    public float maxWidth = 0.025f;

    [Header("Trail Time")]
    public float minTime = 0.2f;
    public float maxTime = 2.0f;

    [Header("Color")]
    public Color lowSpeedColor = new Color(0.176f, 0.612f, 0.859f, 0.0f);
    public Color highSpeedColor = new Color(0.176f, 0.612f, 0.859f, 0.65f);

    [Header("Runtime")]
    public bool isSelected = false;

    private TrailRenderer trail;
    private Vector3 lastPosition;
    private bool initialized;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
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

        float speed = Vector3.Distance(transform.position, lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = transform.position;

        UpdateTrailBySpeed(speed);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (!isSelected)
            HideTrail();
        else
            ClearTrail();
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

        float t = Mathf.InverseLerp(minSpeedToShow, maxSpeedForFullTrail, speed);

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

        Color transparentStart = mainColor;
        transparentStart.a = 0f;

        Color visibleMiddle = mainColor;

        Color transparentEnd = mainColor;
        transparentEnd.a = 0f;

        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(mainColor, 0f),
                new GradientColorKey(mainColor, 0.5f),
                new GradientColorKey(mainColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(visibleMiddle.a, 0.35f),
                new GradientAlphaKey(visibleMiddle.a, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        return gradient;
    }
}