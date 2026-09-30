using UnityEngine;

[DefaultExecutionOrder(100)]
public class SimpleForceArrowOverlay : MonoBehaviour
{
    [Header("Refs")]
    public DropSelectionManager selectionManager;
    public ElectricFieldVolume fieldVolume;
    public VoltageKnobInput voltageSource;

    [Header("Arrows")]
    public Transform gravityArrow;
    public Transform buoyancyArrow;
    public Transform electricArrow;

    [Header("Offsets")]
    public Vector3 overlayOffset = Vector3.zero;
    public Vector3 gravityOffset = Vector3.zero;
    public Vector3 buoyancyOffset = Vector3.zero;
    public Vector3 electricOffset = Vector3.zero;

    [Header("Shared Arrow Plane")]
    public float sharedPlaneDepth = 0f;

    [Header("Lengths")]
    public float gravityLength = 0.03f;
    public float buoyancyLength = 0.004f;
    public float electricMinLength = 0.002f;
    public float electricMaxLength = 0.06f;

    [Header("Electric Width")]
    [Range(0.1f, 1f)]
    public float electricMinWidthMultiplier = 0.6f;
    [Min(1f)]
    public float electricMaxWidthMultiplier = 1.4f;

    [Header("Visibility")]
    public bool showBuoyancyArrow = false;
    public bool hideElectricWhenVoltageZero = true;
    public float minVoltageToShowElectric = 0.01f;

    private BoxCollider fieldBox;
    private VisualizationModeController visualizationController;
    private Renderer[] gravityRenderers;
    private Renderer[] buoyancyRenderers;
    private Renderer[] electricRenderers;
    private float fallbackWidth = 0.1f;

    private void Awake()
    {
        if (electricArrow != null)
            fallbackWidth = electricArrow.localScale.y;

        CacheRenderers();
        ResolveReferences();
        HideAll();
    }

    private void OnEnable()
    {
        HideAll();
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void CacheRenderers()
    {
        gravityRenderers = gravityArrow != null
            ? gravityArrow.GetComponentsInChildren<Renderer>(true) : null;
        buoyancyRenderers = buoyancyArrow != null
            ? buoyancyArrow.GetComponentsInChildren<Renderer>(true) : null;
        electricRenderers = electricArrow != null
            ? electricArrow.GetComponentsInChildren<Renderer>(true) : null;
    }

    private void ResolveReferences()
    {
        if (fieldVolume != null)
        {
            fieldBox = fieldVolume.GetComponent<BoxCollider>();
            if (voltageSource == null)
                voltageSource = fieldVolume.voltageSource;
        }

        if (visualizationController != null)
            return;

        VisualizationModeController[] controllers =
            FindObjectsByType<VisualizationModeController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (VisualizationModeController controller in controllers)
        {
            if (controller.gameObject.scene != gameObject.scene)
                continue;

            if (selectionManager != null && controller.selectionManager != null &&
                controller.selectionManager != selectionManager)
                continue;

            visualizationController = controller;
            break;
        }
    }

    private void LateUpdate()
    {
        if (visualizationController != null &&
            (!visualizationController.isActiveAndEnabled ||
             !visualizationController.ForceArrowsEnabled))
        {
            HideAll();
            return;
        }

        SelectableDrop selected = selectionManager != null &&
            selectionManager.isActiveAndEnabled
            ? selectionManager.CurrentSelected : null;

        if (selected == null || !selected.gameObject.activeInHierarchy)
        {
            HideAll();
            return;
        }

        Transform target = GetSelectedTargetTransform(selected);

        if (target == null || !IsInsideField(target.position))
        {
            HideAll();
            return;
        }

        transform.position = target.position + overlayOffset;
        UpdateGravityArrow();
        SetVisible(buoyancyArrow, buoyancyRenderers, false);
        UpdateElectricArrow(selected);
    }

    private void UpdateGravityArrow()
    {
        if (gravityArrow == null)
            return;

        gravityArrow.position = GetArrowPosition(gravityOffset);
        SetArrow(gravityArrow, gravityLength, Vector3.down);
        SetVisible(gravityArrow, gravityRenderers, true);
    }

    private void UpdateElectricArrow(SelectableDrop selected)
    {
        if (electricArrow == null)
            return;

        VoltageKnobInput source = fieldVolume != null && fieldVolume.voltageSource != null
            ? fieldVolume.voltageSource : voltageSource;

        float threshold = hideElectricWhenVoltageZero
            ? Mathf.Max(0.000001f, minVoltageToShowElectric) : 0.000001f;

        if (fieldVolume == null ||
            (source != null && Mathf.Abs(source.CurrentVoltage) <= threshold) ||
            fieldVolume.SmoothedVoltageMagnitude <= threshold ||
            !fieldVolume.TryGetBalanceState(selected, out float ratio, out float tolerance) ||
            float.IsNaN(ratio) || float.IsInfinity(ratio) || ratio <= 0f)
        {
            SetVisible(electricArrow, electricRenderers, false);
            return;
        }

        float length = Mathf.Clamp(gravityLength * ratio, electricMinLength, electricMaxLength);
        electricArrow.position = GetArrowPosition(electricOffset);
        SetArrow(electricArrow, length, Vector3.up);

        float baseWidth = gravityArrow != null ? gravityArrow.localScale.y : fallbackWidth;
        float minWidth = Mathf.Clamp(electricMinWidthMultiplier, 0.1f, 1f);
        float maxWidth = Mathf.Max(1f, electricMaxWidthMultiplier);
        float widthMultiplier = ratio <= 1f
            ? Mathf.Lerp(minWidth, 1f, Mathf.Clamp01(ratio))
            : Mathf.Lerp(1f, maxWidth, Mathf.Clamp01(ratio - 1f));

        Vector3 scale = electricArrow.localScale;
        scale.y = baseWidth * widthMultiplier;
        electricArrow.localScale = scale;
        SetVisible(electricArrow, electricRenderers, true);
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
        if (fieldVolume == null || !fieldVolume.isActiveAndEnabled ||
            fieldBox == null || !fieldBox.enabled || !fieldBox.gameObject.activeInHierarchy)
            return false;

        Vector3 local = fieldBox.transform.InverseTransformPoint(position) - fieldBox.center;
        Vector3 half = fieldBox.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x &&
               Mathf.Abs(local.y) <= half.y &&
               Mathf.Abs(local.z) <= half.z;
    }

    private Vector3 GetArrowPosition(Vector3 offset)
    {
        return transform.position + new Vector3(offset.x, offset.y, sharedPlaneDepth);
    }

    private void SetArrow(Transform arrow, float length, Vector3 direction)
    {
        float angle = Mathf.Atan2(-direction.y, -direction.x) * Mathf.Rad2Deg;
        arrow.rotation = Quaternion.Euler(0f, 0f, angle);
        Vector3 scale = arrow.localScale;
        scale.x = length;
        arrow.localScale = scale;
    }

    private static void SetVisible(Transform arrow, Renderer[] renderers, bool visible)
    {
        if (arrow == null)
            return;

        if (renderers != null)
            foreach (Renderer renderer in renderers)
                if (renderer != null)
                    renderer.enabled = visible;

        if (arrow.gameObject.activeSelf != visible)
            arrow.gameObject.SetActive(visible);
    }

    private void OnDisable()
    {
        HideAll();
    }

    private void HideAll()
    {
        SetVisible(gravityArrow, gravityRenderers, false);
        SetVisible(buoyancyArrow, buoyancyRenderers, false);
        SetVisible(electricArrow, electricRenderers, false);
    }
}
