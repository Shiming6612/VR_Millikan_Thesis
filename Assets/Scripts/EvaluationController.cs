using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// Replacement for Assets/Scripts/EvaluationController.cs.
// Manual timing and verbal answers: no response timer or automatic scoring.
public class EvaluationController : MonoBehaviour
{
    public enum VisualizationOrder
    {
        O1_Arrows_Paths_Cloud, O2_Arrows_Cloud_Paths,
        O3_Paths_Arrows_Cloud, O4_Paths_Cloud_Arrows,
        O5_Cloud_Arrows_Paths, O6_Cloud_Paths_Arrows
    }
    public enum Condition { Weaker, Balanced, Stronger }
    private enum Method { Arrows, Paths, Cloud }
    private enum Stage { Preparing, Ready, Observing, Record, Exploring, Review, Invalid, Finished, Error }

    [Header("Participant - Set Before Play")]
    public string participantId = "P01";
    public string conditionPlanId = "PILOT";
    public VisualizationOrder order = VisualizationOrder.O1_Arrows_Paths_Cloud;

    [Header("References - Keep Existing Assignments")]
    public Transform trialDropStart;
    public SpraySpawner spraySpawner;
    public ElectricFieldVolume electricField;
    public DropSelectionManager selectionManager;
    public VisualizationModeController visualizationController;
    public ExperimentController experimentController;

    [Header("Droplets - Same Parameter Set For All Methods")]
    public float radiusMicrometer = 0.75f;
    public int chargeMultiple = 3;
    public float[] radiusMultipliers = { 0.9f, 1f, 1.1f };

    [Header("PILOT Conditions - Three Entries Per Method")]
    public Condition[] arrowsConditions = { Condition.Weaker, Condition.Balanced, Condition.Stronger };
    public Condition[] pathsConditions = { Condition.Balanced, Condition.Stronger, Condition.Weaker };
    public Condition[] cloudConditions = { Condition.Stronger, Condition.Weaker, Condition.Balanced };
    [Range(0.05f, 0.9f)]
    public float relativeDeviation = 0.2f;

    [Header("Researcher Keyboard Controls")]
    public bool enableKeyboardControls = true;

    [Header("Researcher Only - Never Show To Participant")]
    [SerializeField] private Stage stage = Stage.Preparing;
    [SerializeField] private string status;
    [SerializeField] private string currentVisualization;
    [SerializeField] private string questionnaireCode;
    [SerializeField] private string expectedAnswer;
    [SerializeField] private float balanceVoltage;
    [SerializeField] private float targetVoltage;
    [SerializeField] private string conditionLogPath;

    private OilDrop trialDrop;
    private DropProperties properties;
    private SelectableDrop selectable;
    private Rigidbody body;
    private VoltageKnobInput knob;
    private Method[] methods;
    private Condition[][] conditions;
    private float[] radii;
    private float deviation;
    private int sessionCharge;
    private string sessionId, planId;
    private int methodIndex, taskIndex, attempt = 1;
    private bool exploring;
    private readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
    private readonly CultureInfo culture = CultureInfo.InvariantCulture;

    private Method CurrentMethod => methods[methodIndex];
    private Condition CurrentCondition => conditions[(int)CurrentMethod][taskIndex];

    private IEnumerator Start()
    {
        yield return null; // Let knob.Start finish first.
        if (trialDropStart == null || spraySpawner == null || spraySpawner.dropPrefab == null ||
            electricField == null || !electricField.isActiveAndEnabled ||
            electricField.voltageSource == null || selectionManager == null ||
            visualizationController == null)
        {
            Fail("Assign all references and enable ElectricFieldVolume.");
            yield break;
        }
        if (!ValidThree(arrowsConditions) || !ValidThree(pathsConditions) ||
            !ValidThree(cloudConditions) || radiusMultipliers == null || radiusMultipliers.Length != 3 ||
            !Positive(radiusMicrometer) || chargeMultiple <= 0 ||
            !Positive(relativeDeviation) || relativeDeviation >= 1f ||
            string.IsNullOrWhiteSpace(participantId))
        {
            Fail("Check participant ID, three-entry arrays, radius, charge and deviation.");
            yield break;
        }

        sessionId = participantId;
        planId = conditionPlanId;
        sessionCharge = chargeMultiple;
        deviation = relativeDeviation;
        methods = BuildOrder(order);
        conditions = new[] {
            (Condition[])arrowsConditions.Clone(),
            (Condition[])pathsConditions.Clone(),
            (Condition[])cloudConditions.Clone()
        };
        radii = new float[3];
        for (int i = 0; i < 3; i++)
        {
            radii[i] = radiusMicrometer * radiusMultipliers[i];
            if (!Positive(radii[i])) { Fail("Invalid radius multiplier."); yield break; }
        }

        knob = electricField.voltageSource;
        if (experimentController != null) experimentController.enabled = false;
        spraySpawner.ResetAllDrops();
        spraySpawner.maxTotalDrops = 0;
        selectionManager.SetSelectionEnabled(false);
        selectionManager.ClearSelectionAndHover();
        visualizationController.HideAllVisualizations();
        knob.SetInteractionEnabled(false);
        knob.snapToStep = false;

        trialDrop = Instantiate(spraySpawner.dropPrefab);
        trialDrop.name = "EvaluationDrop";
        trialDrop.gameObject.SetActive(true);
        trialDrop.gameObject.SetActive(false);
        properties = trialDrop.GetComponentInChildren<DropProperties>(true);
        selectable = trialDrop.GetComponentInChildren<SelectableDrop>(true);
        body = trialDrop.GetComponent<Rigidbody>();
        if (properties == null || selectable == null || body == null)
        {
            Fail("Droplet requires DropProperties, SelectableDrop and Rigidbody.");
            yield break;
        }
        properties.randomizeOnSpawn = false;
        if (deviation <= Mathf.Clamp(trialDrop.hoverDeadZone, 0f, 0.99f) + 0.01f)
        {
            Fail("Deviation must exceed the droplet balance tolerance by more than 0.01.");
            yield break;
        }
        // Validate all three parameter sets before beginning any observations.
        for (int i = 0; i < 3; i++)
        {
            properties.ApplyRadiusAndCharge(radii[i], sessionCharge);
            float voltage = CalculateBalanceVoltage();
            if (!Positive(voltage) || voltage * (1f - deviation) < knob.minVoltage ||
                voltage * (1f + deviation) > knob.maxVoltage)
            {
                Fail("A task voltage is outside the knob range. Check all radii and charge.");
                yield break;
            }
        }
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "EvaluationLogs");
            Directory.CreateDirectory(folder);
            conditionLogPath = Path.Combine(folder,
                "conditions_" + Guid.NewGuid().ToString("N") + ".csv");
            File.WriteAllText(conditionLogPath,
                "participant,plan,order,method,task,attempt,event,condition,expected,radius_um,charge_n,balance_V,target_V\n");
        }
        catch (Exception e) { Fail("Cannot create condition log: " + e.Message); yield break; }

        Debug.Log("[Evaluation] Condition log: " + conditionLogPath, this);
        yield return Prepare(false);
    }

    private IEnumerator Prepare(bool manual)
    {
        stage = Stage.Preparing;
        exploring = manual;
        status = "Preparing - do not start the stopwatch yet";
        knob.SetInteractionEnabled(false);
        visualizationController.HideAllVisualizations();
        selectionManager.ClearSelectionAndHover();
        trialDrop.ResetDrop();
        yield return new WaitForFixedUpdate();

        // Manual exploration always uses the middle-sized droplet.
        properties.ApplyRadiusAndCharge(radii[manual ? 1 : taskIndex], sessionCharge);
        balanceVoltage = CalculateBalanceVoltage();
        float factor = manual || CurrentCondition == Condition.Balanced ? 1f :
            CurrentCondition == Condition.Weaker ? 1f - deviation : 1f + deviation;
        targetVoltage = balanceVoltage * factor;
        currentVisualization = CurrentMethod.ToString();
        questionnaireCode = Prefix() + (manual ? "RATE / " + Prefix() + "COM" : "T" + (taskIndex + 1));
        expectedAnswer = manual ? "-" : CurrentCondition == Condition.Balanced ? "Yes" : "No";

        knob.SetVoltageFromExternal(balanceVoltage);
        trialDrop.launchPhaseDuration = 30f;
        trialDrop.Launch(trialDropStart.position, Vector3.zero);
        float deadline = Time.realtimeSinceStartup + 10f;
        bool balanced = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            if (Inside(out float ratio, out float tolerance) &&
                Mathf.Abs(ratio - 1f) <= Mathf.Min(0.001f, Mathf.Max(0.00001f, tolerance)))
            {
                balanced = true;
                break;
            }
        }
        if (!balanced)
        {
            Fail("Preparation failed: check start position, field trigger and OilDrop tag.");
            yield break;
        }
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        trialDrop.launchPhaseDuration = 0f;
        selectionManager.SetSelected(selectable);
        // Selection callbacks may show visuals, so explicitly hide them again.
        visualizationController.HideAllVisualizations();
        if (manual)
        {
            ShowMethod();
            knob.SetInteractionEnabled(true);
            stage = Stage.Exploring;
            status = "Hands-on: knob unlocked. 2 = reset; 5 = end and fill Part C.";
        }
        else
        {
            stage = Stage.Ready;
            status = "Ready: 1 = start observation AND your manual stopwatch.";
            LogEvent("prepared");
        }
    }

    private void Update()
    {
        if (stage == Stage.Observing && !Inside(out _, out _))
        {
            LogEvent("invalid_outside_field");
            HideAndRemove();
            stage = Stage.Invalid;
            status = "Invalid: droplet left field. Discard timing; press 2 to repeat.";
        }
    }

    public void StartTask()
    {
        if (stage != Stage.Ready) return;
        if (!Inside(out _, out _))
        {
            stage = Stage.Invalid;
            status = "Drop outside field. Press 2.";
            return;
        }
        knob.SetInteractionEnabled(false);
        knob.SetVoltageFromExternal(targetVoltage);
        ShowMethod();
        stage = Stage.Observing;
        status = "Observing: stop manual stopwatch at verbal answer; then press 5.";
        LogEvent("started");
    }

    public void FinishObservationOrExploration()
    {
        if (stage == Stage.Observing)
        {
            LogEvent("observation_closed"); // NOT a response-time measurement.
            if (stage == Stage.Error) return;
            HideAndRemove();
            stage = Stage.Record;
            status = "Record answer, confidence and manual time. Then press 3.";
        }
        else if (stage == Stage.Exploring)
        {
            HideAndRemove();
            stage = Stage.Review;
            status = "Fill this method's Part C. After completion press 4.";
        }
    }

    public void NextTask()
    {
        if (stage != Stage.Record) return;
        if (taskIndex < 2)
        {
            taskIndex++;
            attempt = 1;
            StartCoroutine(Prepare(false));
        }
        else StartCoroutine(Prepare(true));
    }

    public void PrepareAgain()
    {
        if (stage == Stage.Preparing || stage == Stage.Error || stage == Stage.Finished) return;
        if (stage == Stage.Review) return;
        if (!exploring)
        {
            LogEvent("retry_previous_attempt_excluded");
            if (stage == Stage.Error) return;
            attempt++;
        }
        StartCoroutine(Prepare(exploring));
    }

    public void NextVisualization()
    {
        if (stage != Stage.Review) return;
        if (methodIndex == methods.Length - 1)
        {
            stage = Stage.Finished;
            status = "VR complete. Remove headset and fill Part D.";
            return;
        }
        methodIndex++;
        taskIndex = 0;
        attempt = 1;
        StartCoroutine(Prepare(false));
    }

    private void HideAndRemove()
    {
        knob.SetInteractionEnabled(false);
        visualizationController.HideAllVisualizations();
        selectionManager.ClearSelectionAndHover();
        trialDrop.ResetDrop();
    }

    private void ShowMethod()
    {
        visualizationController.HideAllVisualizations();
        switch (CurrentMethod)
        {
            case Method.Arrows: visualizationController.SetForceArrowsVisible(true); break;
            case Method.Paths: visualizationController.SetTrailPathVisible(true); break;
            case Method.Cloud: visualizationController.SetFieldCloudVisible(true); break;
        }
    }

    private bool Inside(out float ratio, out float tolerance)
    {
        ratio = tolerance = 0f;
        return trialDrop != null && trialDrop.gameObject.activeInHierarchy &&
            electricField.TryGetBalanceState(selectable, out ratio, out tolerance);
    }

    private float CalculateBalanceVoltage()
    {
        Vector3 direction = electricField.fieldDirection.sqrMagnitude > 0.000001f ?
            electricField.fieldDirection.normalized : Vector3.up;
        float gravity = Mathf.Abs(Vector3.Dot(trialDrop.customGravity, direction));
        float charge = Mathf.Abs(properties.ChargeC);
        return properties.MassKg * gravity * electricField.GetPlateSpacingMeters() /
            (charge * Mathf.Max(0.000001f, electricField.fieldScale));
    }

    private string Prefix()
    {
        return CurrentMethod == Method.Arrows ? "FA" : CurrentMethod == Method.Paths ? "IP" : "PC";
    }
    private bool Positive(float x) { return !float.IsNaN(x) && !float.IsInfinity(x) && x > 0f; }
    private bool ValidThree(Condition[] values)
    {
        if (values == null || values.Length != 3) return false;
        foreach (Condition value in values)
            if (!Enum.IsDefined(typeof(Condition), value)) return false;
        return true;
    }
    private string Csv(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }
    private void LogEvent(string eventName)
    {
        if (string.IsNullOrEmpty(conditionLogPath)) return;
        try
        {
            string[] fields = {
                sessionId, planId, string.Join("-", methods), CurrentMethod.ToString(),
                Prefix() + "T" + (taskIndex + 1), attempt.ToString(), eventName,
                CurrentCondition.ToString(), expectedAnswer,
                properties.RadiusMicrometer.ToString("R", culture), sessionCharge.ToString(),
                balanceVoltage.ToString("R", culture), targetVoltage.ToString("R", culture)
            };
            File.AppendAllText(conditionLogPath, string.Join(",", Array.ConvertAll(fields, Csv)) + "\n");
        }
        catch (Exception e) { Fail("Condition log write failed: " + e.Message); }
    }

    private Method[] BuildOrder(VisualizationOrder value)
    {
        switch (value)
        {
            case VisualizationOrder.O2_Arrows_Cloud_Paths: return new[] { Method.Arrows, Method.Cloud, Method.Paths };
            case VisualizationOrder.O3_Paths_Arrows_Cloud: return new[] { Method.Paths, Method.Arrows, Method.Cloud };
            case VisualizationOrder.O4_Paths_Cloud_Arrows: return new[] { Method.Paths, Method.Cloud, Method.Arrows };
            case VisualizationOrder.O5_Cloud_Arrows_Paths: return new[] { Method.Cloud, Method.Arrows, Method.Paths };
            case VisualizationOrder.O6_Cloud_Paths_Arrows: return new[] { Method.Cloud, Method.Paths, Method.Arrows };
            default: return new[] { Method.Arrows, Method.Paths, Method.Cloud };
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !enableKeyboardControls) return;
        Event e = Event.current;
        if (e == null) return;
        int key = 0;
        switch (e.keyCode)
        {
            case KeyCode.Alpha1: case KeyCode.Keypad1: key = 1; break;
            case KeyCode.Alpha2: case KeyCode.Keypad2: key = 2; break;
            case KeyCode.Alpha3: case KeyCode.Keypad3: key = 3; break;
            case KeyCode.Alpha4: case KeyCode.Keypad4: key = 4; break;
            case KeyCode.Alpha5: case KeyCode.Keypad5: key = 5; break;
        }
        if (key == 0) return;
        if (e.type == EventType.KeyUp) { heldKeys.Remove(e.keyCode); e.Use(); return; }
        if (e.type != EventType.KeyDown || e.control || e.alt || e.command || e.shift) return;
        bool first = heldKeys.Add(e.keyCode);
        e.Use();
        if (!first) return;
        switch (key)
        {
            case 1: StartTask(); break;
            case 2: PrepareAgain(); break;
            case 3: NextTask(); break;
            case 4: NextVisualization(); break;
            case 5: FinishObservationOrExploration(); break;
        }
    }
    private void OnApplicationFocus(bool focus) { if (!focus) heldKeys.Clear(); }
    private void OnDisable() { heldKeys.Clear(); }
    private void OnDestroy() { if (trialDrop != null) Destroy(trialDrop.gameObject); }
    private void Fail(string message)
    {
        stage = Stage.Error;
        status = message;
        if (knob != null) knob.SetInteractionEnabled(false);
        if (visualizationController != null) visualizationController.HideAllVisualizations();
        if (trialDrop != null) trialDrop.ResetDrop();
        Debug.LogError("[Evaluation] " + message, this);
    }
}