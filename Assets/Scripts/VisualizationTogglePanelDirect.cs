using UnityEngine;
using UnityEngine.UI;

public class VisualizationTogglePanelDirect : MonoBehaviour
{
    [Header("Visualization Controller")]
    public VisualizationModeController visualizationController;

    [Header("UI Toggles")]
    public Toggle forceArrowsToggle;
    public Toggle forceImbalancePathsToggle;
    public Toggle fieldCloudToggle;

    [Header("Controller Input")]
    [Tooltip("Use exactly the same Ray Origin as RadiusSliderController.")]
    public Transform rayOrigin;

    public OVRInput.Button controlButton = OVRInput.Button.SecondaryIndexTrigger;
    public bool invertRayDirection = false;

    [Tooltip("Maximum controller-to-panel selection distance in metres.")]
    public float maxRayDistance = 2f;

    [Tooltip("Additional clickable area around each Toggle RectTransform in canvas units.")]
    public float hitPadding = 15f;

    private void Awake()
    {
        if (visualizationController == null)
            visualizationController = FindFirstObjectByType<VisualizationModeController>();

        AddListeners();
        ResetAllToggles();
    }

    private void OnDestroy()
    {
        RemoveListeners();
    }

    private void Update()
    {
        if (rayOrigin == null)
            return;

        if (!OVRInput.GetDown(controlButton))
            return;

        Toggle hitToggle = FindToggleUnderControllerRay();

        if (hitToggle != null)
            hitToggle.isOn = !hitToggle.isOn;
    }

    private Toggle FindToggleUnderControllerRay()
    {
        Vector3 direction = invertRayDirection
            ? -rayOrigin.forward
            : rayOrigin.forward;

        Ray ray = new Ray(rayOrigin.position, direction);
        Toggle closestToggle = null;
        float closestDistance = float.MaxValue;

        CheckToggleHit(forceArrowsToggle, ray, ref closestToggle, ref closestDistance);
        CheckToggleHit(forceImbalancePathsToggle, ray, ref closestToggle, ref closestDistance);
        CheckToggleHit(fieldCloudToggle, ray, ref closestToggle, ref closestDistance);

        return closestToggle;
    }

    private void CheckToggleHit(
        Toggle candidate,
        Ray ray,
        ref Toggle closestToggle,
        ref float closestDistance)
    {
        if (candidate == null ||
            !candidate.isActiveAndEnabled ||
            !candidate.interactable)
        {
            return;
        }

        RectTransform rectTransform = candidate.GetComponent<RectTransform>();

        if (rectTransform == null)
            return;

        Plane plane = new Plane(rectTransform.forward, rectTransform.position);

        if (!plane.Raycast(ray, out float distance))
            return;

        if (distance < 0f ||
            distance > Mathf.Max(0.01f, maxRayDistance) ||
            distance >= closestDistance)
        {
            return;
        }

        Vector3 hitWorld = ray.GetPoint(distance);
        Vector3 localPoint = rectTransform.InverseTransformPoint(hitWorld);
        Rect rect = rectTransform.rect;

        Rect clickableRect = new Rect(
            rect.xMin - hitPadding,
            rect.yMin - hitPadding,
            rect.width + hitPadding * 2f,
            rect.height + hitPadding * 2f);

        if (!clickableRect.Contains(new Vector2(localPoint.x, localPoint.y)))
            return;

        closestToggle = candidate;
        closestDistance = distance;
    }

    private void AddListeners()
    {
        if (forceArrowsToggle != null)
            forceArrowsToggle.onValueChanged.AddListener(SetForceArrows);

        if (forceImbalancePathsToggle != null)
            forceImbalancePathsToggle.onValueChanged.AddListener(SetForceImbalancePaths);

        if (fieldCloudToggle != null)
            fieldCloudToggle.onValueChanged.AddListener(SetFieldCloud);
    }

    private void RemoveListeners()
    {
        if (forceArrowsToggle != null)
            forceArrowsToggle.onValueChanged.RemoveListener(SetForceArrows);

        if (forceImbalancePathsToggle != null)
            forceImbalancePathsToggle.onValueChanged.RemoveListener(SetForceImbalancePaths);

        if (fieldCloudToggle != null)
            fieldCloudToggle.onValueChanged.RemoveListener(SetFieldCloud);
    }

    private void SetForceArrows(bool visible)
    {
        if (visualizationController != null)
            visualizationController.SetForceArrowsVisible(visible);
    }

    private void SetForceImbalancePaths(bool visible)
    {
        if (visualizationController != null)
            visualizationController.SetTrailPathVisible(visible);
    }

    private void SetFieldCloud(bool visible)
    {
        if (visualizationController != null)
            visualizationController.SetFieldCloudVisible(visible);
    }

    public void ResetAllToggles()
    {
        if (forceArrowsToggle != null)
            forceArrowsToggle.SetIsOnWithoutNotify(false);

        if (forceImbalancePathsToggle != null)
            forceImbalancePathsToggle.SetIsOnWithoutNotify(false);

        if (fieldCloudToggle != null)
            fieldCloudToggle.SetIsOnWithoutNotify(false);

        if (visualizationController != null)
            visualizationController.HideAllVisualizations();
    }
}
