using System.Collections;
using UnityEngine;

public class EvaluationController : MonoBehaviour
{
    [Header("References")]
    public Transform trialDropStart;
    public SpraySpawner spraySpawner;
    public ElectricFieldVolume electricField;
    public DropSelectionManager selectionManager;
    public VisualizationModeController visualizationController;
    public ExperimentController experimentController;

    [Header("Test Droplet")]
    public float radiusMicrometer = 0.75f;
    public int chargeMultiple = 3;

    private OilDrop trialDrop;

    private IEnumerator Start()
    {
        // Wait until the existing components finish their Start methods.
        yield return null;

        if (trialDropStart == null ||
            spraySpawner == null ||
            spraySpawner.dropPrefab == null ||
            electricField == null ||
            electricField.voltageSource == null ||
            selectionManager == null ||
            visualizationController == null)
        {
            Debug.LogError(
                "[Evaluation] Please assign all required references.", this);
            yield break;
        }

        if (!electricField.isActiveAndEnabled)
        {
            Debug.LogError("[Evaluation] ElectricFieldVolume is disabled.", this);
            yield break;
        }

        // Stop the original experiment controller from changing selection.
        // Only this scene's component is disabled at runtime.
        if (experimentController != null)
            experimentController.enabled = false;

        // Prevent manual spraying during this test.
        spraySpawner.ResetAllDrops();
        spraySpawner.maxTotalDrops = 0;

        selectionManager.SetSelectionEnabled(false);
        selectionManager.ClearSelectionAndHover();
        visualizationController.HideAllVisualizations();

        VoltageKnobInput knob = electricField.voltageSource;
        knob.SetInteractionEnabled(false);

        // Use the same prefab as the original application.
        trialDrop = Instantiate(spraySpawner.dropPrefab);
        trialDrop.name = "EvaluationDrop";

        // Ensure Awake has run even if the prefab was inactive.
        trialDrop.gameObject.SetActive(true);
        trialDrop.gameObject.SetActive(false);

        DropProperties properties =
            trialDrop.GetComponentInChildren<DropProperties>(true);
        SelectableDrop selectable =
            trialDrop.GetComponentInChildren<SelectableDrop>(true);
        Rigidbody body = trialDrop.GetComponent<Rigidbody>();

        if (properties == null || selectable == null || body == null)
        {
            Fail("The droplet prefab is missing a required component.");
            yield break;
        }

        properties.randomizeOnSpawn = false;
        properties.ApplyRadiusAndCharge(radiusMicrometer, chargeMultiple);

        // Same balance calculation as ElectricFieldVolume.
        Vector3 direction = electricField.fieldDirection.sqrMagnitude > 0.000001f
            ? electricField.fieldDirection.normalized
            : Vector3.up;

        float gravity = Mathf.Abs(
            Vector3.Dot(trialDrop.customGravity, direction));
        float spacing = electricField.GetPlateSpacingMeters();
        float scale = Mathf.Max(0.000001f, electricField.fieldScale);
        float charge = Mathf.Abs(properties.ChargeC);

        if (gravity <= 0f || spacing <= 0f ||
            charge <= 0f || properties.MassKg <= 0f)
        {
            Fail("Invalid droplet or electric field parameters.");
            yield break;
        }

        float hoverVoltage =
            properties.MassKg * gravity * spacing / (charge * scale);

        if (float.IsNaN(hoverVoltage) || float.IsInfinity(hoverVoltage) ||
            hoverVoltage < knob.minVoltage ||
            hoverVoltage > knob.maxVoltage)
        {
            Fail("The balance voltage is outside the knob's voltage range.");
            yield break;
        }

        // Avoid rounding the starting voltage away from exact balance.
        knob.snapToStep = false;
        knob.SetVoltageFromExternal(hoverVoltage);

        // With zero launch speed, this preparation phase keeps the
        // droplet still while the real electric field settles.
        trialDrop.launchPhaseDuration = 30f;
        trialDrop.Launch(trialDropStart.position, Vector3.zero);

        float deadline = Time.realtimeSinceStartup + 10f;
        bool ready = false;

        while (Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();

            if (trialDrop == null || !trialDrop.gameObject.activeInHierarchy)
                break;

            if (electricField.TryGetBalanceState(
                    selectable, out float ratio, out float tolerance) &&
                Mathf.Abs(ratio - 1f) <= Mathf.Min(0.001f, tolerance))
            {
                ready = true;
                break;
            }
        }

        if (!ready)
        {
            Fail("Preparation failed. Check that TrialDropStart is inside " +
                 "PlateGapVolume and the droplet has the correct OilDrop tag.");
            yield break;
        }

        // Resume the original motion calculation from rest.
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        trialDrop.launchPhaseDuration = 0f;

        selectionManager.SetSelected(selectable);
        visualizationController.SetForceArrowsVisible(true);

        Debug.Log(
            "[Evaluation] Ready. Radius = " +
            properties.RadiusMicrometer.ToString("0.00") +
            " um, charge = " + properties.ChargeMultiple +
            " e, voltage = " + knob.CurrentVoltage.ToString("0.00") +
            " V.", this);
    }

    private void Fail(string message)
    {
        Debug.LogError("[Evaluation] " + message, this);

        if (trialDrop != null)
            Destroy(trialDrop.gameObject);
    }
}