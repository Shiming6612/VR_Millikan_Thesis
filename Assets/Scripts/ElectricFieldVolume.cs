using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ElectricFieldVolume : MonoBehaviour
{
    [Header("Refs")]
    public VoltageKnobInput voltageSource;
    public bool invertVoltage;
    public Transform upperPlate;
    public Transform lowerPlate;

    [Header("Field")]
    public float plateSpacingMetersOverride = 0.006f;
    public float fieldScale = 1f;
    public Vector3 fieldDirection = new Vector3(0f, 1f, 0f);
    public float voltageSmoothing = 8f;

    [Header("Detection")]
    public string oilDropTag = "OilDrop";
    public bool logEnterExit = false;

    [Header("Debug")]
    public bool logRatioDebug = false;

    public bool HasBodiesInside => bodies.Count > 0;
    public event Action<bool> OnOccupiedStateChanged;

    private readonly HashSet<Rigidbody> bodies = new HashSet<Rigidbody>();
    private float voltageSmooth;
    private bool lastOccupiedState;

    private void Awake()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;

        float voltage = voltageSource != null ? voltageSource.CurrentVoltage : 0f;
        voltageSmooth = invertVoltage ? -voltage : voltage;
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = GetValidOilDropBody(other);

        if (rb == null)
            return;

        bool added = bodies.Add(rb);

        if (added)
        {
            BottomTutorialController tutorial = FindFirstObjectByType<BottomTutorialController>();

            if (tutorial != null)
                tutorial.NotifyDropEnteredField();

            if (logEnterExit)
                Debug.Log("[ElectricFieldVolume] Enter: " + rb.name, this);
        }

        CheckOccupiedStateChanged();
    }

    private void OnTriggerStay(Collider other)
    {
        Rigidbody rb = GetValidOilDropBody(other);

        if (rb != null)
            bodies.Add(rb);
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody rb = GetValidOilDropBody(other);

        if (rb == null)
            return;

        bool removed = bodies.Remove(rb);

        OilDrop oilDrop = rb.GetComponent<OilDrop>();

        if (oilDrop != null)
            oilDrop.SetElectricFieldRatio(0f);

        if (removed && logEnterExit)
            Debug.Log("[ElectricFieldVolume] Exit: " + rb.name, this);

        CheckOccupiedStateChanged();
    }

    private void FixedUpdate()
    {
        bodies.RemoveWhere(rb => rb == null);

        SmoothVoltage();

        foreach (Rigidbody rb in bodies)
        {
            if (rb == null)
                continue;

            OilDrop oilDrop = rb.GetComponent<OilDrop>();
            DropProperties dropProperties = rb.GetComponent<DropProperties>();

            if (dropProperties == null)
                dropProperties = rb.GetComponentInChildren<DropProperties>();

            if (oilDrop == null || dropProperties == null)
                continue;

            float hoverVoltage = CalculateHoverVoltage(rb, dropProperties);

            if (hoverVoltage <= 1e-6f)
            {
                oilDrop.SetElectricFieldRatio(0f);
                continue;
            }

            float ratio = Mathf.Abs(voltageSmooth) / hoverVoltage;
            oilDrop.SetElectricFieldRatio(ratio);

            if (logRatioDebug)
            {
                Debug.Log(
                    "[ElectricFieldVolume] U=" + Mathf.Abs(voltageSmooth).ToString("0.0") +
                    " V, Hover=" + hoverVoltage.ToString("0.0") +
                    " V, Ratio=" + ratio.ToString("0.00"),
                    this
                );
            }
        }

        CheckOccupiedStateChanged();
    }

    private void SmoothVoltage()
    {
        float voltage = voltageSource != null ? voltageSource.CurrentVoltage : 0f;

        if (invertVoltage)
            voltage = -voltage;

        float alpha = 1f - Mathf.Exp(Mathf.Max(0.01f, voltageSmoothing) * -Time.fixedDeltaTime);
        voltageSmooth = Mathf.Lerp(voltageSmooth, voltage, alpha);
    }

    private float CalculateHoverVoltage(Rigidbody rb, DropProperties dropProperties)
    {
        float mass = Mathf.Max(1e-18f, dropProperties.MassKg);
        float charge = Mathf.Abs(dropProperties.ChargeC);

        if (charge < 1e-20f)
            return 0f;

        float d = GetPlateSpacingMeters();

        if (d <= 1e-6f)
            return 0f;

        Vector3 dir = fieldDirection.sqrMagnitude > 1e-6f
            ? fieldDirection.normalized
            : Vector3.up;

        Vector3 gravity = Physics.gravity;

        OilDrop oilDrop = rb.GetComponent<OilDrop>();

        if (oilDrop != null)
            gravity = oilDrop.customGravity;

        float g = Mathf.Abs(Vector3.Dot(gravity, dir));

        if (g <= 1e-6f)
            return 0f;

        float scale = Mathf.Max(1e-6f, fieldScale);

        // PDF formula:
        // F_el = F_G
        // q * U / d = m * g
        // U = m * g * d / q
        return (mass * g * d) / (charge * scale);
    }

    private Rigidbody GetValidOilDropBody(Collider other)
    {
        if (other == null)
            return null;

        Rigidbody rb = other.attachedRigidbody;

        if (rb == null)
            return null;

        if (!rb.CompareTag(oilDropTag) && !other.CompareTag(oilDropTag))
            return null;

        return rb;
    }

    private void CheckOccupiedStateChanged()
    {
        bool occupied = bodies.Count > 0;

        if (occupied == lastOccupiedState)
            return;

        lastOccupiedState = occupied;
        OnOccupiedStateChanged?.Invoke(occupied);
    }

    public float GetPlateSpacingMeters()
    {
        if (plateSpacingMetersOverride > 0f)
            return plateSpacingMetersOverride;

        Vector3 dir = fieldDirection.sqrMagnitude > 1e-6f
            ? fieldDirection.normalized
            : Vector3.up;

        if (upperPlate != null && lowerPlate != null)
            return Mathf.Abs(Vector3.Dot(upperPlate.position - lowerPlate.position, dir));

        BoxCollider box = GetComponent<BoxCollider>();

        if (box != null)
        {
            Vector3 size = box.bounds.size;

            return Mathf.Abs(dir.x) * size.x +
                   Mathf.Abs(dir.y) * size.y +
                   Mathf.Abs(dir.z) * size.z;
        }

        return 0f;
    }
}