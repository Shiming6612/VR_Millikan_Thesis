using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

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
    private enum Stage { Preparing, Ready, Observing, Exploring, Invalid, Finished, Error }

    [Header("Participant - Set Before Play")]
    public string participantId = "P01";
    public string conditionPlanId = "REPLAY_PILOT";
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

    [Header("Part B Replay")]
    [Min(0f)] public float resetDelayAfterPlateContact = 1f;
    public Transform upperPlateRoot;
    public Transform lowerPlateRoot;
    [Min(0.05f)] public float answerDelayAfterBoost = 0.75f;
    [Min(1f)] public float voltageIncrease = 100f;
    [Min(0.00001f)] public float stationarySpeedThreshold = 0.0005f;
    [Range(0.04f, 0.9f)] public float closeToBalanceLimit = 0.1f;
    [Min(0.001f)] public float observationSpeedLimit = 0.03f;
    public bool useSharedTaskConditions = true;

    [Header("Replay Readout")]
    [SerializeField] private int replayNumber;
    [SerializeField] private bool waitingForPlateReset;
    [SerializeField] private string contactedPlate;
    [SerializeField] private string initialBalanceAnswer;
    [SerializeField] private string initialForceAnswer;
    [SerializeField] private string sampledMotionAnswer;
    [SerializeField] private float sampledVerticalSpeed;
    [SerializeField] private float sampledForceRatio;
    [SerializeField] private float appliedVoltageIncrease;

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

    [SerializeField] private int stepIndex = -1;
    private readonly int[] attempts = new int[12];
    private bool initialized;
    private bool boosted;
    private bool answerSampled;
    private bool baselineSampled;
    private float cycleStart;
    private float boostTime;
    private float plateContactTime;
    private bool originalDestroyOnCollision;
    private float originalMaxSpeed;
    private EvaluationReplayCollision collisionMonitor;
    private readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
    private readonly CultureInfo culture = CultureInfo.InvariantCulture;

    private Method CurrentMethod => methods[methodIndex];
    private Condition CurrentCondition => conditions[(int)CurrentMethod][taskIndex];

    private IEnumerator Start()
    {
        yield return null;
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

        if (float.IsNaN(resetDelayAfterPlateContact) || float.IsInfinity(resetDelayAfterPlateContact) ||
            resetDelayAfterPlateContact < 0f || !Positive(answerDelayAfterBoost) ||
            !Positive(voltageIncrease) || !Positive(observationSpeedLimit) ||
            !Positive(stationarySpeedThreshold) || !Positive(closeToBalanceLimit))
        {
            Fail("Check contact reset delay, voltage increase, speed and answer settings.");
            yield break;
        }
        if (upperPlateRoot == null) upperPlateRoot = electricField.upperPlate;
        if (lowerPlateRoot == null) lowerPlateRoot = electricField.lowerPlate;
        if (!HasActiveCollider(upperPlateRoot) || !HasActiveCollider(lowerPlateRoot))
        {
            Fail("Assign upper and lower plate roots with enabled Colliders on them or their children.");
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
        if (useSharedTaskConditions)
        {
            conditions[1] = (Condition[])conditions[0].Clone();
            conditions[2] = (Condition[])conditions[0].Clone();
        }
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
        originalMaxSpeed = trialDrop.maxVerticalSpeed;
        originalDestroyOnCollision = trialDrop.destroyOnCollision;
        collisionMonitor = trialDrop.gameObject.AddComponent<EvaluationReplayCollision>();
        collisionMonitor.UpperPlate = upperPlateRoot;
        collisionMonitor.LowerPlate = lowerPlateRoot;
        properties.randomizeOnSpawn = false;
        if (closeToBalanceLimit <= trialDrop.hoverDeadZone)
        {
            Fail("Close-to-balance limit must exceed the droplet balance tolerance.");
            yield break;
        }
        if (deviation <= Mathf.Clamp(trialDrop.hoverDeadZone, 0f, 0.99f) + 0.01f)
        {
            Fail("Deviation must exceed the droplet balance tolerance by more than 0.01.");
            yield break;
        }

        for (int i = 0; i < 3; i++)
        {
            properties.ApplyRadiusAndCharge(radii[i], sessionCharge);
            float voltage = CalculateBalanceVoltage();
            if (!Positive(voltage) || voltage * (1f - deviation) < knob.minVoltage ||
                voltage * (1f + deviation) + voltageIncrease > knob.maxVoltage)
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
                "participant,plan,order,method,task,attempt,event,condition,expected,radius_um,charge_n,balance_V,target_V,replay,time_s,waiting_plate_reset,boosted,command_V,smoothed_V,ratio,velocity_y,initial_balance,initial_force,sampled_motion,sampled_velocity_y,sampled_ratio,boost_delta_V\n");
        }
        catch (Exception e) { Fail("Cannot create condition log: " + e.Message); yield break; }

        Debug.Log("[Evaluation] Condition log: " + conditionLogPath, this);
        initialized = true;
        stage = Stage.Ready;
        status = "Ready. 3 = begin Part B; 1 = previous step. Manual timing only.";
        Debug.Log("[Evaluation] " + status, this);
    }

    private IEnumerator Prepare(bool manual)
    {
        stage = Stage.Preparing;
        exploring = manual;
        status = "Preparing replay";
        RestoreDropMotion();
        trialDrop.destroyOnCollision = manual ? originalDestroyOnCollision : false;
        waitingForPlateReset = false;
        contactedPlate = "";
        trialDrop.maxVerticalSpeed = manual ? originalMaxSpeed : Mathf.Min(originalMaxSpeed, observationSpeedLimit);
        boosted = answerSampled = baselineSampled = false;
        sampledMotionAnswer = "Not sampled";
        initialBalanceAnswer = initialForceAnswer = "Not sampled";
        expectedAnswer = "Not sampled";
        sampledVerticalSpeed = sampledForceRatio = 0f;
        appliedVoltageIncrease = 0f;
        collisionMonitor.ClearContact();
        knob.SetInteractionEnabled(false);
        visualizationController.HideAllVisualizations();
        selectionManager.ClearSelectionAndHover();
        trialDrop.ResetDrop();
        yield return new WaitForFixedUpdate();

        properties.ApplyRadiusAndCharge(radii[manual ? 1 : taskIndex], sessionCharge);
        balanceVoltage = CalculateBalanceVoltage();
        float factor = manual || CurrentCondition == Condition.Balanced ? 1f :
            CurrentCondition == Condition.Weaker ? 1f - deviation : 1f + deviation;
        targetVoltage = balanceVoltage * factor;
        currentVisualization = CurrentMethod.ToString();
        questionnaireCode = Prefix() + (manual ? "RATE / " + Prefix() + "COM" : "T" + (taskIndex + 1));
        expectedAnswer = manual ? "-" : "Not sampled";

        knob.SetVoltageFromExternal(targetVoltage);
        trialDrop.launchPhaseDuration = 30f;
        trialDrop.Launch(trialDropStart.position, Vector3.zero);
        float deadline = Time.realtimeSinceStartup + 10f;
        bool balanced = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            if (Inside(out float ratio, out float tolerance) &&
                Mathf.Abs(ratio - factor) <= Mathf.Min(0.001f, Mathf.Max(0.00001f, tolerance)))
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

        visualizationController.HideAllVisualizations();
        if (manual)
        {
            ShowMethod();
            knob.SetInteractionEnabled(true);
            stage = Stage.Exploring;
            status = "Part C: knob unlocked. Complete exploration and ratings, then 3. 1 = previous step.";
            LogEvent("exploration_started");
            Debug.Log("[Evaluation] " + questionnaireCode + " - " + status, this);
        }
        else
        {
            LogEvent("prepared");
            if (stage == Stage.Error) yield break;
            StartObservation();
        }
    }

    private void Update()
    {
        if (stage != Stage.Observing) return;
        if (waitingForPlateReset)
        {
            if (Time.time - plateContactTime >= resetDelayAfterPlateContact)
                RestartReplay();
            return;
        }
        if (collisionMonitor.HasPlateContact)
        {
            waitingForPlateReset = true;
            contactedPlate = collisionMonitor.PlateName;
            plateContactTime = Time.time;
            if (boosted && !answerSampled)
            {
                sampledMotionAnswer = "Not sampled: plate reached before observation cue";
                expectedAnswer = "Invalid motion observation";
            }
            LogEvent("plate_contact");
            if (stage == Stage.Error) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            trialDrop.enabled = false;
            body.isKinematic = true;
            status = "Plate contact: " + contactedPlate + ". Reset to initial voltage after delay. 3 = next; 1 = previous.";
            Debug.Log("[Evaluation] " + status, this);
            return;
        }
        if (!Inside(out _, out _))
        {
            LogEvent("invalid_outside_field_without_plate_contact");
            if (stage == Stage.Error) return;
            HideAndRemove();
            stage = Stage.Invalid;
            status = "Drop left field without plate contact. Check plate colliders and collision layers. 3 = retry; 1 = previous.";
            Debug.LogWarning(status, this);
            return;
        }
        float elapsed = Time.time - cycleStart;
        if (!baselineSampled && elapsed >= 0.1f)
            CaptureBaseline();
        if (stage == Stage.Error) return;
        if (boosted && !answerSampled && Time.time - boostTime >= answerDelayAfterBoost)
        {
            answerSampled = true;
            sampledVerticalSpeed = body.linearVelocity.y;
            Inside(out sampledForceRatio, out _);
            sampledMotionAnswer = Mathf.Abs(sampledVerticalSpeed) <= stationarySpeedThreshold
                ? "Stationary" : sampledVerticalSpeed > 0f ? "Moving upward" : "Moving downward";
            expectedAnswer = sampledMotionAnswer;
            status = "OBSERVE NOW. Researcher-only answer: " + sampledMotionAnswer;
            LogEvent("motion_sample");
            Debug.Log("[Evaluation OBSERVE NOW] " + questionnaireCode + " replay " + replayNumber + " - " + sampledMotionAnswer, this);
        }
        if (stage == Stage.Error) return;
    }

    private void CaptureBaseline()
    {
        if (!Inside(out float ratio, out float tolerance)) return;
        baselineSampled = true;
        float difference = Mathf.Abs(ratio - 1f);
        initialBalanceAnswer = difference <= tolerance ? "Balanced" :
            difference <= closeToBalanceLimit ? "Close to balanced" : "Far from balanced";
        initialForceAnswer = difference <= tolerance ? "Approximately equal" :
            ratio < 1f ? "Smaller" : "Greater";
        LogEvent("baseline_sample");
    }

    public void IncreaseVoltage()
    {
        if (!initialized || exploring || stage != Stage.Observing ||
            waitingForPlateReset || collisionMonitor.HasPlateContact || boosted) return;
        ApplyBoost();
    }

    private void ApplyBoost()
    {
        if (boosted) return;
        if (!baselineSampled) CaptureBaseline();
        if (stage == Stage.Error) return;
        float command = targetVoltage + voltageIncrease;
        if (command > knob.maxVoltage || command < knob.minVoltage)
        {
            Fail("Requested boost is outside the voltage range.");
            return;
        }
        boosted = true;
        boostTime = Time.time;
        appliedVoltageIncrease = voltageIncrease;
        knob.SetVoltageFromExternal(command);
        status = "Voltage increased. Wait for OBSERVE NOW.";
        LogEvent("voltage_increased");
    }

    private void RestartReplay()
    {
        HideAndRemove();
        replayNumber++;
        StartCoroutine(Prepare(false));
    }

    private void StartObservation()
    {
        knob.SetInteractionEnabled(false);
        knob.SetVoltageFromExternal(targetVoltage);
        ShowMethod();
        stage = Stage.Observing;
        cycleStart = Time.time;
        collisionMonitor.ClearContact();
        status = "Initial voltage. 5 = +100 V once this replay. Plate contact resets this task. 3 = next; 1 = previous.";
        LogEvent("observation_started");
        if (stage != Stage.Error)
            Debug.Log("[Evaluation START] " + questionnaireCode + " - " + status, this);
    }

    public void NextStep()
    {
        if (!initialized || stage == Stage.Error || stage == Stage.Finished) return;
        if (stage == Stage.Invalid)
        {
            EnterStep(stepIndex, "retry_invalid");
            return;
        }
        EnterStep(stepIndex + 1, "forward");
    }

    public void PreviousStep()
    {
        if (!initialized || stage == Stage.Error || stepIndex < 0) return;

        EnterStep(Mathf.Max(0, stepIndex - 1), "backward");
    }

    private void EnterStep(int destination, string navigation)
    {
        StopAllCoroutines();
        if (stepIndex >= 0 && stepIndex < 12)
        {

            LogEvent("leave_" + navigation);
            if (stage == Stage.Error) return;
        }
        HideAndRemove();
        stepIndex = destination;
        replayNumber = 1;
        waitingForPlateReset = false;
        boosted = false;
        if (stepIndex >= 12)
        {
            stage = Stage.Finished;
            questionnaireCode = "Part D";
            expectedAnswer = "-";
            status = "VR complete. Remove headset and fill Part D. 1 = return to final Part C block.";
            Debug.Log("[Evaluation] " + status, this);
            return;
        }

        exploring = stepIndex >= 9;
        methodIndex = exploring ? stepIndex - 9 : stepIndex / 3;
        taskIndex = exploring ? 0 : stepIndex % 3;
        attempt = ++attempts[stepIndex];
        StartCoroutine(Prepare(exploring));
    }

    private void HideAndRemove()
    {
        RestoreDropMotion();
        knob.SetInteractionEnabled(false);
        visualizationController.HideAllVisualizations();
        selectionManager.ClearSelectionAndHover();
        trialDrop.ResetDrop();
    }

    private void RestoreDropMotion()
    {
        if (body != null) body.isKinematic = false;
        if (trialDrop != null) trialDrop.enabled = true;
    }

    private bool HasActiveCollider(Transform root)
    {
        if (root == null) return false;
        foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            if (collider.enabled && collider.gameObject.activeInHierarchy) return true;
        return false;
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
                questionnaireCode, attempt.ToString(), eventName,
                exploring ? "Exploration" : CurrentCondition.ToString(), expectedAnswer,
                properties.RadiusMicrometer.ToString("R", culture), sessionCharge.ToString(),
                balanceVoltage.ToString("R", culture), targetVoltage.ToString("R", culture),
                replayNumber.ToString(), Time.time.ToString("R", culture), waitingForPlateReset.ToString(), boosted.ToString(),
                (targetVoltage + (boosted ? appliedVoltageIncrease : 0f)).ToString("R", culture),
                electricField.SmoothedVoltageMagnitude.ToString("R", culture),
                trialDrop.CurrentElectricFieldRatio.ToString("R", culture), body.linearVelocity.y.ToString("R", culture),
                initialBalanceAnswer, initialForceAnswer, sampledMotionAnswer,
                sampledVerticalSpeed.ToString("R", culture), sampledForceRatio.ToString("R", culture),
                appliedVoltageIncrease.ToString("R", culture)
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
            case KeyCode.Alpha3: case KeyCode.Keypad3: key = 3; break;
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
            case 1: PreviousStep(); break;
            case 3: NextStep(); break;
            case 5: IncreaseVoltage(); break;
        }
    }
    private void OnApplicationFocus(bool focus) { if (!focus) heldKeys.Clear(); }
    private void OnDisable()
    {
        heldKeys.Clear();
        StopAllCoroutines();
        if (initialized) HideAndRemove();
    }
    private void OnDestroy() { if (trialDrop != null) Destroy(trialDrop.gameObject); }
    private void Fail(string message)
    {
        stage = Stage.Error;
        status = message;
        if (knob != null) knob.SetInteractionEnabled(false);
        if (visualizationController != null) visualizationController.HideAllVisualizations();
        RestoreDropMotion();
        if (trialDrop != null) trialDrop.ResetDrop();
        Debug.LogError("[Evaluation] " + message, this);
    }
}

public class EvaluationReplayCollision : MonoBehaviour
{
    public Transform UpperPlate { get; set; }
    public Transform LowerPlate { get; set; }
    public bool HasPlateContact { get; private set; }
    public string PlateName { get; private set; }

    public void ClearContact()
    {
        HasPlateContact = false;
        PlateName = "";
    }

    private void CheckContact(Collider other)
    {
        if (HasPlateContact || other == null) return;
        Transform target = other.transform;
        if (UpperPlate != null && (target == UpperPlate || target.IsChildOf(UpperPlate)))
        {
            HasPlateContact = true;
            PlateName = "Upper plate";
        }
        else if (LowerPlate != null && (target == LowerPlate || target.IsChildOf(LowerPlate)))
        {
            HasPlateContact = true;
            PlateName = "Lower plate";
        }
    }

    private void OnCollisionEnter(Collision collision) { CheckContact(collision.collider); }
    private void OnCollisionStay(Collision collision) { CheckContact(collision.collider); }
    private void OnTriggerEnter(Collider other) { CheckContact(other); }
    private void OnTriggerStay(Collider other) { CheckContact(other); }
}
