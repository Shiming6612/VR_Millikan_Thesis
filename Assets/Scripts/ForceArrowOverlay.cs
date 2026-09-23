using UnityEngine;

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
    [Tooltip("World-space Z offset shared by both arrows. Individual arrow offset Z values are ignored.")]
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
    private float fallbackWidth = 0.1f;

    private void Awake()
    {
        if (electricArrow != null)
            fallbackWidth = electricArrow.localScale.y;

        if (fieldVolume != null)
            fieldBox = fieldVolume.GetComponent<BoxCollider>();

        HideAll();
    }

    private void Update()
    {
        SelectableDrop selected = selectionManager != null ? selectionManager.CurrentSelected : null;

        if (selected == null)
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
        UpdateBuoyancyArrow();
        UpdateElectricArrow(selected);
    }

    private void UpdateGravityArrow()
    {
        if (gravityArrow == null) return;

        gravityArrow.position = GetArrowPosition(gravityOffset);
        SetArrow(gravityArrow, gravityLength, Vector3.down);
        gravityArrow.gameObject.SetActive(true);
    }

    private void UpdateBuoyancyArrow()
    {
        if (buoyancyArrow != null)
            buoyancyArrow.gameObject.SetActive(false);
    }

    private void UpdateElectricArrow(SelectableDrop selected)
    {
        if (electricArrow == null) return;

        if (fieldVolume == null ||
            !fieldVolume.TryGetBalanceState(selected, out float ratio, out float tolerance))
        {
            electricArrow.gameObject.SetActive(false);
            return;
        }

        if (hideElectricWhenVoltageZero &&
            fieldVolume.SmoothedVoltageMagnitude <= minVoltageToShowElectric)
        {
            electricArrow.gameObject.SetActive(false);
            return;
        }

        float length = gravityLength * ratio;
        length = Mathf.Clamp(length, electricMinLength, electricMaxLength);

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
        electricArrow.gameObject.SetActive(true);
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
        return fieldBox != null && fieldBox.bounds.Contains(position);
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

    private void OnDisable()
    {
        HideAll();
    }

    private void HideAll()
    {
        if (gravityArrow != null) gravityArrow.gameObject.SetActive(false);
        if (buoyancyArrow != null) buoyancyArrow.gameObject.SetActive(false);
        if (electricArrow != null) electricArrow.gameObject.SetActive(false);
    }
}