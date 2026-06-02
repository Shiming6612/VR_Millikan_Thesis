using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RadiusSliderController : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;
    public Slider radiusSlider;
    public TMP_Text radiusText;

    [Header("Target")]
    public SpraySpawner spraySpawner;

    [Header("Ray Control")]
    public Transform rayOrigin;
    public OVRInput.Button controlButton = OVRInput.Button.SecondaryIndexTrigger;
    public bool allowRayControlOnlyDuringTask = true;
    public bool invertRayDirection = false;
    public float maxRayDistance = 5f;
    public float hitPadding = 35f;

    [Header("Radius Settings")]
    public float minRadiusMicrometer = 0.3f;
    public float maxRadiusMicrometer = 2.0f;
    public float defaultRadiusMicrometer = 0.3f;

    [Header("Behaviour")]
    public bool clearDropsWhenRadiusChanges = true;
    public bool enableTutorialRadiusModeOnStart = false;
    public bool hidePanelOnStart = false;

    private bool isActiveForTask;
    private RectTransform sliderRect;
    private float lastAppliedRadius = -999f;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (radiusSlider == null)
            radiusSlider = GetComponentInChildren<Slider>(true);

        if (radiusSlider != null)
            sliderRect = radiusSlider.GetComponent<RectTransform>();

        SetupSlider();
    }

    private void Start()
    {
        if (hidePanelOnStart && panelRoot != null)
            panelRoot.SetActive(false);

        if (enableTutorialRadiusModeOnStart)
            StartRadiusTask();
        else
            isActiveForTask = false;
    }

    private void Update()
    {
        UpdateRayControl();
    }

    private void SetupSlider()
    {
        if (radiusSlider == null)
            return;

        radiusSlider.minValue = minRadiusMicrometer;
        radiusSlider.maxValue = maxRadiusMicrometer;
        radiusSlider.wholeNumbers = false;
        radiusSlider.value = defaultRadiusMicrometer;

        radiusSlider.onValueChanged.RemoveAllListeners();
        radiusSlider.onValueChanged.AddListener(OnSliderValueChanged);

        UpdateRadiusText(radiusSlider.value);
    }

    public void StartRadiusTask()
    {
        isActiveForTask = true;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (radiusSlider != null)
            radiusSlider.interactable = true;

        SetRadius(defaultRadiusMicrometer, true);
    }

    public void EndRadiusTask()
    {
        isActiveForTask = false;

        if (radiusSlider != null)
            radiusSlider.interactable = false;

        if (spraySpawner != null)
            spraySpawner.ReturnToRandomModeAndClearDrops();

        if (panelRoot != null && hidePanelOnStart)
            panelRoot.SetActive(false);
    }

    public float GetCurrentRadiusMicrometer()
    {
        return radiusSlider != null ? radiusSlider.value : defaultRadiusMicrometer;
    }

    private void UpdateRayControl()
    {
        if (allowRayControlOnlyDuringTask && !isActiveForTask)
            return;

        if (rayOrigin == null || radiusSlider == null)
            return;

        if (!OVRInput.Get(controlButton))
            return;

        if (sliderRect == null)
            sliderRect = radiusSlider.GetComponent<RectTransform>();

        Vector3 direction = invertRayDirection ? -rayOrigin.forward : rayOrigin.forward;
        Ray ray = new Ray(rayOrigin.position, direction);
        Plane plane = new Plane(sliderRect.forward, sliderRect.position);

        if (!plane.Raycast(ray, out float distance))
            return;

        if (distance < 0f || distance > maxRayDistance)
            return;

        Vector3 hitWorld = ray.GetPoint(distance);
        Vector2 localPoint = sliderRect.InverseTransformPoint(hitWorld);
        Rect rect = sliderRect.rect;

        Rect paddedRect = new Rect(
            rect.xMin - hitPadding,
            rect.yMin - hitPadding,
            rect.width + hitPadding * 2f,
            rect.height + hitPadding * 2f
        );

        if (!paddedRect.Contains(localPoint))
            return;

        float normalized = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        normalized = Mathf.Clamp01(normalized);

        float radius = Mathf.Lerp(radiusSlider.minValue, radiusSlider.maxValue, normalized);
        SetRadius(radius, clearDropsWhenRadiusChanges);
    }

    private void OnSliderValueChanged(float value)
    {
        UpdateRadiusText(value);

        if (allowRayControlOnlyDuringTask && !isActiveForTask)
            return;

        ApplyRadiusToSprayer(value, clearDropsWhenRadiusChanges);
    }

    public void SetRadius(float radiusMicrometer, bool clearExistingDrops)
    {
        float radius = Mathf.Clamp(radiusMicrometer, minRadiusMicrometer, maxRadiusMicrometer);

        if (radiusSlider != null)
            radiusSlider.value = radius;

        UpdateRadiusText(radius);
        ApplyRadiusToSprayer(radius, clearExistingDrops);
    }

    private void ApplyRadiusToSprayer(float radius, bool clearExistingDrops)
    {
        if (Mathf.Abs(radius - lastAppliedRadius) < 0.005f)
            return;

        lastAppliedRadius = radius;

        if (spraySpawner != null)
        {
            spraySpawner.EnableTutorialRadiusMode();
            spraySpawner.SetTutorialRadiusMicrometer(radius, clearExistingDrops);
        }
    }

    private void UpdateRadiusText(float radius)
    {
        if (radiusText != null)
            radiusText.text = "Radius: " + radius.ToString("0.00") + " \u00B5m";
    }
}