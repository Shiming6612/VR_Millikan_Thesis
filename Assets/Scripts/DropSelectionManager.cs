using System;
using UnityEngine;
using UnityEngine.XR;

public class DropSelectionManager : MonoBehaviour
{
    [Header("Ray Setup")]
    public Transform rayOrigin;
    public float rayLength = 10f;
    public LayerMask oilDropLayerMask = ~0;
    public float sphereCastRadius = 0.03f;
    public bool enableHoverHighlight = true;

    [Header("Ray Visual")]
    public bool showLine = true;
    public LineRenderer line;
    public Color rayNormalColor = Color.white;
    public Color rayHitColor = Color.yellow;

    [Header("Input")]
    public bool useXRTriggerInput = true;
    public bool allowPrimaryButtonFallback = false;
    public XRNode triggerHand = XRNode.RightHand;

    [Header("Ray Transform")]
    public Vector3 rayLocalOffset = Vector3.zero;
    public Vector3 rayLocalDirection = Vector3.forward;

    [Header("Runtime State")]
    public bool selectionEnabled = false;

    [Header("Debug")]
    public bool logHits = false;
    public bool logSelection = true;
    public bool logInput = false;

    public SelectableDrop CurrentSelected => selected;
    public event Action<SelectableDrop> OnSelectionChanged;

    private SelectableDrop selected;
    private SelectableDrop hovered;

    private bool prevTriggerPressed;
    private bool prevPrimaryPressed;

    private void Start()
    {
        SetSelectionEnabled(selectionEnabled);
    }

    private void Update()
    {
        if (!selectionEnabled)
        {
            ClearHover();
            RefreshLineVisibility(false);
            ResetInputState();
            return;
        }

        if (rayOrigin == null)
        {
            ClearHover();
            RefreshLineVisibility(false);
            return;
        }

        Vector3 origin = rayOrigin.TransformPoint(rayLocalOffset);
        Vector3 direction = rayOrigin.TransformDirection(rayLocalDirection.normalized);
        Ray ray = new Ray(origin, direction);

        bool hitSomething = Physics.SphereCast(
            ray,
            sphereCastRadius,
            out RaycastHit hit,
            rayLength,
            oilDropLayerMask,
            QueryTriggerInteraction.Ignore
        );

        SelectableDrop hitDrop = null;

        if (hitSomething && hit.collider != null)
            hitDrop = hit.collider.GetComponentInParent<SelectableDrop>();

        if (logHits)
            Debug.Log(hitDrop != null ? "[DropSelection] Hit: " + hitDrop.name : "[DropSelection] No hit");

        UpdateHover(hitDrop);
        UpdateLine(ray, hitSomething, hit);

        if (GetSelectDown() && hitDrop != null)
            SetSelected(hitDrop);
    }

    public void SetSelectionEnabled(bool enabled)
    {
        if (selectionEnabled == enabled)
        {
            RefreshLineVisibility(enabled);
            return;
        }

        selectionEnabled = enabled;
        ResetInputState();

        if (!selectionEnabled)
        {
            ClearHover();
            RefreshLineVisibility(false);
        }
        else
        {
            RefreshLineVisibility(showLine);
        }

        if (logSelection)
            Debug.Log(selectionEnabled ? "[DropSelection] Enabled." : "[DropSelection] Disabled.");
    }

    public void ClearSelection()
    {
        SetSelected(null);
    }

    public void ClearSelectionAndHover()
    {
        ClearHover();
        ClearSelection();
        RefreshLineVisibility(false);
        ResetInputState();
    }

    private void ClearHover()
    {
        if (hovered != null)
        {
            hovered.SetHovered(false);
            hovered = null;
        }
    }

    private void UpdateHover(SelectableDrop hitDrop)
    {
        if (!enableHoverHighlight)
        {
            ClearHover();
            return;
        }

        if (hovered == hitDrop)
            return;

        if (hovered != null)
            hovered.SetHovered(false);

        hovered = hitDrop;

        if (hovered != null)
            hovered.SetHovered(true);
    }

    public void SetSelected(SelectableDrop newSelected)
    {
        if (selected == newSelected)
            return;

        if (selected != null)
            selected.SetSelected(false);

        selected = newSelected;

        if (selected != null)
            selected.SetSelected(true);

        if (logSelection)
            Debug.Log(selected != null ? "[DropSelection] Selected: " + selected.name : "[DropSelection] Selected: None");

        OnSelectionChanged?.Invoke(selected);

        if (selected != null)
        {
            BottomTutorialController tutorial = FindFirstObjectByType<BottomTutorialController>();

            if (tutorial != null)
                tutorial.NotifyDropSelected();
        }
    }

    private bool GetSelectDown()
    {
        if (!useXRTriggerInput)
            return false;

        InputDevice device = InputDevices.GetDeviceAtXRNode(triggerHand);

        if (!device.isValid)
        {
            if (logInput)
                Debug.LogWarning("[DropSelection] XR device invalid: " + triggerHand);

            return false;
        }

        bool triggerPressed = false;
        bool primaryPressed = false;

        device.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed);
        device.TryGetFeatureValue(CommonUsages.primaryButton, out primaryPressed);

        bool triggerDown = triggerPressed && !prevTriggerPressed;
        bool primaryDown = allowPrimaryButtonFallback && primaryPressed && !prevPrimaryPressed;

        prevTriggerPressed = triggerPressed;
        prevPrimaryPressed = primaryPressed;

        if (logInput && (triggerDown || primaryDown))
        {
            Debug.Log(
                "[DropSelection] SelectDown. Hand=" + triggerHand +
                ", TriggerDown=" + triggerDown +
                ", PrimaryDown=" + primaryDown
            );
        }

        return triggerDown || primaryDown;
    }

    private void ResetInputState()
    {
        prevTriggerPressed = false;
        prevPrimaryPressed = false;
    }

    private void UpdateLine(Ray ray, bool hitSomething, RaycastHit hit)
    {
        if (line == null)
            return;

        if (!showLine || !selectionEnabled)
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;
        line.useWorldSpace = true;
        line.positionCount = 2;

        Vector3 end = ray.origin + ray.direction * rayLength;

        if (hitSomething)
            end = ray.origin + ray.direction * hit.distance;

        line.SetPosition(0, ray.origin);
        line.SetPosition(1, end);

        Color color = hitSomething ? rayHitColor : rayNormalColor;
        line.startColor = color;
        line.endColor = color;
    }

    private void RefreshLineVisibility(bool visible)
    {
        if (line != null)
            line.enabled = visible && selectionEnabled && showLine;
    }

    private void OnDrawGizmosSelected()
    {
        if (rayOrigin == null)
            return;

        Vector3 origin = rayOrigin.TransformPoint(rayLocalOffset);
        Vector3 direction = rayOrigin.TransformDirection(rayLocalDirection.normalized);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(origin, 0.01f);
        Gizmos.DrawLine(origin, origin + direction * 0.2f);
    }
}