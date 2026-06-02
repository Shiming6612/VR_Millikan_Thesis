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
    public Vector3 gravityOffset = new Vector3(-0.035f, 0f, 0f);
    public Vector3 buoyancyOffset = Vector3.zero;
    public Vector3 electricOffset = new Vector3(0.035f, 0f, 0f);

    [Header("Lengths")]
    public float gravityLength = 0.03f;
    public float buoyancyLength = 0.004f;
    public float electricMinLength = 0.002f;
    public float electricMaxLength = 0.06f;

    [Header("Visibility")]
    public bool showBuoyancyArrow = true;
    public bool hideElectricWhenVoltageZero = true;
    public float minVoltageToShowElectric = 0.01f;

    private BoxCollider fieldBox;

    private void Awake()
    {
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

        gravityArrow.localPosition = gravityOffset;
        SetArrow(gravityArrow, gravityLength, Vector3.down);
        gravityArrow.gameObject.SetActive(true);
    }

    private void UpdateBuoyancyArrow()
    {
        if (buoyancyArrow == null) return;

        if (!showBuoyancyArrow)
        {
            buoyancyArrow.gameObject.SetActive(false);
            return;
        }

        buoyancyArrow.localPosition = buoyancyOffset;
        SetArrow(buoyancyArrow, buoyancyLength, Vector3.up);
        buoyancyArrow.gameObject.SetActive(true);
    }

    private void UpdateElectricArrow(SelectableDrop selected)
    {
        if (electricArrow == null) return;

        float voltage = voltageSource != null ? Mathf.Abs(voltageSource.CurrentVoltage) : 0f;

        if (hideElectricWhenVoltageZero && voltage <= minVoltageToShowElectric)
        {
            electricArrow.gameObject.SetActive(false);
            return;
        }

        float hoverVoltage = GetHoverVoltage(selected);

        if (hoverVoltage <= 1e-6f)
        {
            electricArrow.gameObject.SetActive(false);
            return;
        }

        float ratio = voltage / hoverVoltage;
        float length = gravityLength * ratio;
        length = Mathf.Clamp(length, electricMinLength, electricMaxLength);

        electricArrow.localPosition = electricOffset;
        SetArrow(electricArrow, length, Vector3.up);
        electricArrow.gameObject.SetActive(true);
    }

    private float GetHoverVoltage(SelectableDrop selected)
    {
        if (selected == null || fieldVolume == null)
            return 0f;

        DropProperties dp = FindDropProperties(selected);
        if (dp == null) return 0f;

        float mass = Mathf.Max(1e-18f, dp.MassKg);
        float charge = Mathf.Abs(dp.ChargeC);
        if (charge < 1e-20f) return 0f;

        float d = fieldVolume.GetPlateSpacingMeters();
        if (d <= 1e-6f) return 0f;

        Vector3 dir = fieldVolume.fieldDirection.sqrMagnitude > 1e-6f
            ? fieldVolume.fieldDirection.normalized
            : Vector3.up;

        Vector3 gravity = GetGravityVector(selected);
        float g = Mathf.Abs(Vector3.Dot(gravity, dir));

        float scale = Mathf.Max(1e-6f, fieldVolume.fieldScale);

        return (mass * g * d) / (charge * scale);
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

    private void SetArrow(Transform arrow, float length, Vector3 direction)
    {
        arrow.right = -direction.normalized;

        Vector3 scale = arrow.localScale;
        scale.x = length;
        arrow.localScale = scale;
    }

    private void HideAll()
    {
        if (gravityArrow != null) gravityArrow.gameObject.SetActive(false);
        if (buoyancyArrow != null) buoyancyArrow.gameObject.SetActive(false);
        if (electricArrow != null) electricArrow.gameObject.SetActive(false);
    }
}