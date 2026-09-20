using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EvaluationController : MonoBehaviour
{
    public enum VisualizationOrder
    {
        O1_Arrows_Paths_Cloud,
        O2_Arrows_Cloud_Paths,
        O3_Paths_Arrows_Cloud,
        O4_Paths_Cloud_Arrows,
        O5_Cloud_Arrows_Paths,
        O6_Cloud_Paths_Arrows
    }

    private enum Method
    {
        Arrows,
        Paths,
        Cloud
    }

    private enum Stage
    {
        Preparing,
        Ready,
        Running,
        Answer,
        Finished,
        Error
    }

    [Header("Participant")]
    public string participantId = "P01";

    [Header("Visualization Order - Set Before Play")]
    public VisualizationOrder order =
        VisualizationOrder.O1_Arrows_Paths_Cloud;

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

    [Header("Task Settings - Set Before Play")]
    public float voltageChange = 100f;
    public float targetToleranceVolts = 5f;

    [Header("Researcher Keyboard Controls")]
    public bool enableKeyboardControls = true;

    [Header("Runtime Information - Do Not Edit")]
    [SerializeField] private string status = "Not started";
    [SerializeField] private string currentVisualization;
    [SerializeField] private string questionnaireCode;
    [SerializeField] private string operatorInstruction;
    [SerializeField] private string expectedAnswer;
    [SerializeField] private float balanceVoltage;
    [SerializeField] private float targetVoltage;
    [SerializeField] private float currentVoltage;

    private OilDrop trialDrop;
    private SelectableDrop selectable;
    private Rigidbody body;
    private VoltageKnobInput knob;

    private Method[] methodOrder;
    private int methodIndex;
    private int taskIndex;
    private int forceState;
    private float stableTime;
    private float sessionVoltageChange;
    private Stage stage = Stage.Preparing;

    // Prevent a held key from repeatedly advancing the evaluation.
    private readonly HashSet<KeyCode> heldKeys =
        new HashSet<KeyCode>();

    private Method CurrentMethod => methodOrder[methodIndex];

    private IEnumerator Start()
    {
        yield return null;

        if (trialDropStart == null ||
            spraySpawner == null ||
            spraySpawner.dropPrefab == null ||
            electricField == null ||
            electricField.voltageSource == null ||
            selectionManager == null ||
            visualizationController == null)
        {
            Fail("Please assign all required references.");
            yield break;
        }

        if (!electricField.isActiveAndEnabled)
        {
            Fail("ElectricFieldVolume is disabled.");
            yield break;
        }

        methodOrder = GetMethodOrder(order);
        sessionVoltageChange = voltageChange;

        if (experimentController != null)
            experimentController.enabled = false;

        spraySpawner.ResetAllDrops();
        spraySpawner.maxTotalDrops = 0;

        selectionManager.SetSelectionEnabled(false);
        selectionManager.ClearSelectionAndHover();
        visualizationController.HideAllVisualizations();

        knob = electricField.voltageSource;
        knob.SetInteractionEnabled(false);

        trialDrop = Instantiate(spraySpawner.dropPrefab);
        trialDrop.name = "EvaluationDrop";

        trialDrop.gameObject.SetActive(true);
        trialDrop.gameObject.SetActive(false);

        DropProperties properties =
            trialDrop.GetComponentInChildren<DropProperties>(true);

        selectable =
            trialDrop.GetComponentInChildren<SelectableDrop>(true);

        body = trialDrop.GetComponent<Rigidbody>();

        if (properties == null ||
            selectable == null ||
            body == null)
        {
            Fail("The droplet prefab is missing a required component.");
            yield break;
        }

        properties.randomizeOnSpawn = false;
        properties.ApplyRadiusAndCharge(
            radiusMicrometer,
            chargeMultiple);

        Vector3 direction =
            electricField.fieldDirection.sqrMagnitude > 0.000001f
                ? electricField.fieldDirection.normalized
                : Vector3.up;

        float gravity = Mathf.Abs(
            Vector3.Dot(trialDrop.customGravity, direction));

        float spacing = electricField.GetPlateSpacingMeters();
        float scale = Mathf.Max(
            0.000001f,
            electricField.fieldScale);

        float charge = Mathf.Abs(properties.ChargeC);

        if (gravity <= 0f ||
            spacing <= 0f ||
            charge <= 0f ||
            properties.MassKg <= 0f)
        {
            Fail("Invalid droplet or electric field parameters.");
            yield break;
        }

        balanceVoltage =
            properties.MassKg * gravity * spacing /
            (charge * scale);

        float deadZone = Mathf.Clamp(
            trialDrop.hoverDeadZone,
            0f,
            0.99f);

        if (float.IsNaN(balanceVoltage) ||
            float.IsInfinity(balanceVoltage) ||
            float.IsNaN(sessionVoltageChange) ||
            float.IsInfinity(sessionVoltageChange) ||
            sessionVoltageChange <= balanceVoltage * deadZone ||
            balanceVoltage - sessionVoltageChange < knob.minVoltage ||
            balanceVoltage + sessionVoltageChange > knob.maxVoltage)
        {
            Fail(
                "Task voltages are invalid. Check the voltage range, " +
                "Voltage Change and balance tolerance.");
            yield break;
        }

        knob.snapToStep = false;

        yield return PrepareDroplet();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !enableKeyboardControls)
            return;

        Event keyboardEvent = Event.current;

        if (keyboardEvent == null)
            return;

        KeyCode key = keyboardEvent.keyCode;
        int command = GetKeyboardCommand(key);

        if (command == 0)
            return;

        if (keyboardEvent.type == EventType.KeyUp)
        {
            heldKeys.Remove(key);
            keyboardEvent.Use();
            return;
        }

        if (keyboardEvent.type != EventType.KeyDown)
            return;

        // Leave modified shortcuts such as Ctrl+1 alone.
        if (keyboardEvent.control ||
            keyboardEvent.alt ||
            keyboardEvent.command ||
            keyboardEvent.shift)
        {
            return;
        }

        bool firstPress = heldKeys.Add(key);
        keyboardEvent.Use();

        if (!firstPress)
            return;

        switch (command)
        {
            case 1:
                StartTask();
                break;

            case 2:
                PrepareAgain();
                break;

            case 3:
                NextTask();
                break;

            case 4:
                NextVisualization();
                break;
        }
    }

    private int GetKeyboardCommand(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Alpha1:
            case KeyCode.Keypad1:
                return 1;

            case KeyCode.Alpha2:
            case KeyCode.Keypad2:
                return 2;

            case KeyCode.Alpha3:
            case KeyCode.Keypad3:
                return 3;

            case KeyCode.Alpha4:
            case KeyCode.Keypad4:
                return 4;

            default:
                return 0;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            heldKeys.Clear();
    }

    private void OnDisable()
    {
        heldKeys.Clear();
    }

    private void Update()
    {
        if (knob != null)
            currentVoltage = knob.CurrentVoltage;

        if (!enableKeyboardControls)
            heldKeys.Clear();

        if (stage != Stage.Running)
            return;

        if (!DropIsInsideField())
        {
            stableTime = 0f;
            status = "Drop outside field - press 2 to prepare again";
            return;
        }

        status = "Task running - adjust voltage";

        float tolerance = Mathf.Clamp(
            targetToleranceVolts,
            0.1f,
            5f);

        if (Mathf.Abs(knob.CurrentVoltage - targetVoltage) > tolerance)
        {
            stableTime = 0f;
            return;
        }

        // Input-stability check, not answer-time measurement.
        stableTime += Time.deltaTime;

        if (stableTime < 0.5f)
            return;

        knob.SetInteractionEnabled(false);
        knob.SetVoltageFromExternal(targetVoltage);

        stage = Stage.Answer;
        status = "Target reached - observe, then record answer";

        operatorInstruction =
            "Wait for the visualization to settle. Record " +
            questionnaireCode + " and " + questionnaireCode + "C.";

        Debug.Log(
            "[Evaluation] " + questionnaireCode +
            ": target reached. Record answer and confidence. " +
            (taskIndex == 2
                ? "Complete Part C, then press 4."
                : "Then press 3."),
            this);
    }

    private IEnumerator PrepareDroplet()
    {
        stage = Stage.Preparing;
        stableTime = 0f;
        status = "Preparing";

        UpdateTaskInformation();

        knob.SetInteractionEnabled(false);
        visualizationController.HideAllVisualizations();

        selectionManager.ClearSelectionAndHover();
        selectionManager.SetSelectionEnabled(false);

        trialDrop.ResetDrop();

        yield return new WaitForFixedUpdate();

        knob.SetVoltageFromExternal(balanceVoltage);

        trialDrop.launchPhaseDuration = 30f;
        trialDrop.Launch(
            trialDropStart.position,
            Vector3.zero);

        float deadline = Time.realtimeSinceStartup + 10f;
        bool balanced = false;

        while (Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();

            if (trialDrop == null ||
                !trialDrop.gameObject.activeInHierarchy)
            {
                break;
            }

            if (electricField.TryGetBalanceState(
                    selectable,
                    out float ratio,
                    out float tolerance) &&
                Mathf.Abs(ratio - 1f) <= Mathf.Min(0.001f, tolerance))
            {
                balanced = true;
                break;
            }
        }

        if (!balanced)
        {
            Fail(
                "Preparation failed. Check TrialDropStart is inside " +
                "PlateGapVolume and the droplet has the OilDrop tag.");
            yield break;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        trialDrop.launchPhaseDuration = 0f;

        selectionManager.SetSelected(selectable);
        ApplyCurrentVisualization();

        stage = Stage.Ready;
        status = "Ready - press 1 to start";

        Debug.Log(
            "[Evaluation] " + participantId + " | " +
            currentVisualization + " | " +
            questionnaireCode + " | Target: " +
            targetVoltage.ToString("0.0") + " V | Press 1 to start.",
            this);
    }

    private void UpdateTaskInformation()
    {
        string prefix;

        // -1: electric force smaller than gravity.
        //  0: approximately equal.
        // +1: electric force larger than gravity.
        switch (CurrentMethod)
        {
            case Method.Arrows:
                currentVisualization = "Force Arrows";
                prefix = "FA";
                forceState = new int[] { -1, 0, 1 }[taskIndex];
                break;

            case Method.Paths:
                currentVisualization = "Force Imbalance Paths";
                prefix = "IP";
                forceState = new int[] { 0, 1, -1 }[taskIndex];
                break;

            default:
                currentVisualization = "Particle-Based Force Cloud";
                prefix = "PC";
                forceState = new int[] { 1, -1, 0 }[taskIndex];
                break;
        }

        questionnaireCode = prefix + "T" + (taskIndex + 1);

        targetVoltage =
            balanceVoltage + forceState * sessionVoltageChange;

        expectedAnswer =
            forceState < 0 ? "Smaller" :
            forceState > 0 ? "Larger" :
            "Equal";

        operatorInstruction = forceState == 0
            ? "Keep the initial voltage and observe."
            : "Ask the participant to set the voltage to " +
              targetVoltage.ToString("0.0") + " V.";
    }

    [ContextMenu("Evaluation/1 Start Task")]
    public void StartTask()
    {
        if (!Application.isPlaying || stage != Stage.Ready)
            return;

        if (!DropIsInsideField())
        {
            status = "Drop outside field - press 2 to prepare again";
            return;
        }

        stableTime = 0f;

        if (forceState == 0)
        {
            knob.SetInteractionEnabled(false);
            stage = Stage.Answer;

            status = "Balance task - observe, then record answer";
            operatorInstruction =
                "Keep the initial voltage. Record " +
                questionnaireCode + " and " +
                questionnaireCode + "C.";
        }
        else
        {
            stage = Stage.Running;
            knob.SetInteractionEnabled(true);
            status = "Task running - adjust voltage";
        }

        Debug.Log(
            "[Evaluation] Started " + questionnaireCode +
            ". " + operatorInstruction,
            this);
    }

    [ContextMenu("Evaluation/2 Prepare Again")]
    public void PrepareAgain()
    {
        if (!Application.isPlaying ||
            stage == Stage.Preparing ||
            stage == Stage.Finished ||
            stage == Stage.Error ||
            trialDrop == null)
        {
            return;
        }

        StartCoroutine(PrepareDroplet());
    }

    [ContextMenu("Evaluation/3 Next Task - After Recording Answers")]
    public void NextTask()
    {
        if (!Application.isPlaying || stage != Stage.Answer)
            return;

        if (taskIndex == 2)
        {
            status = "Complete Part C, then press 4";

            Debug.Log(
                "[Evaluation] Three tasks completed for " +
                currentVisualization +
                ". Complete its Part C, then press 4.",
                this);

            return;
        }

        taskIndex++;
        StartCoroutine(PrepareDroplet());
    }

    [ContextMenu("Evaluation/4 Next Visualization - After Part C")]
    public void NextVisualization()
    {
        if (!Application.isPlaying ||
            stage != Stage.Answer ||
            taskIndex != 2)
        {
            return;
        }

        if (methodIndex == methodOrder.Length - 1)
        {
            knob.SetInteractionEnabled(false);
            selectionManager.ClearSelectionAndHover();
            visualizationController.HideAllVisualizations();
            trialDrop.ResetDrop();

            stage = Stage.Finished;
            status = "VR complete - fill Part D";
            operatorInstruction =
                "Complete the final comparison in LimeSurvey.";

            Debug.Log(
                "[Evaluation] VR complete. Complete Part D in LimeSurvey.",
                this);

            return;
        }

        methodIndex++;
        taskIndex = 0;

        StartCoroutine(PrepareDroplet());
    }

    private bool DropIsInsideField()
    {
        return trialDrop != null &&
               trialDrop.gameObject.activeInHierarchy &&
               electricField.TryGetBalanceState(
                   selectable,
                   out _,
                   out _);
    }

    private void ApplyCurrentVisualization()
    {
        visualizationController.HideAllVisualizations();

        switch (CurrentMethod)
        {
            case Method.Arrows:
                visualizationController.SetForceArrowsVisible(true);
                break;

            case Method.Paths:
                visualizationController.SetTrailPathVisible(true);
                break;

            case Method.Cloud:
                visualizationController.SetFieldCloudVisible(true);
                break;
        }
    }

    private Method[] GetMethodOrder(VisualizationOrder selectedOrder)
    {
        switch (selectedOrder)
        {
            case VisualizationOrder.O2_Arrows_Cloud_Paths:
                return new[]
                {
                    Method.Arrows, Method.Cloud, Method.Paths
                };

            case VisualizationOrder.O3_Paths_Arrows_Cloud:
                return new[]
                {
                    Method.Paths, Method.Arrows, Method.Cloud
                };

            case VisualizationOrder.O4_Paths_Cloud_Arrows:
                return new[]
                {
                    Method.Paths, Method.Cloud, Method.Arrows
                };

            case VisualizationOrder.O5_Cloud_Arrows_Paths:
                return new[]
                {
                    Method.Cloud, Method.Arrows, Method.Paths
                };

            case VisualizationOrder.O6_Cloud_Paths_Arrows:
                return new[]
                {
                    Method.Cloud, Method.Paths, Method.Arrows
                };

            default:
                return new[]
                {
                    Method.Arrows, Method.Paths, Method.Cloud
                };
        }
    }

    private void Fail(string message)
    {
        stage = Stage.Error;
        status = "Error - check Console";

        if (knob != null)
            knob.SetInteractionEnabled(false);

        Debug.LogError("[Evaluation] " + message, this);

        if (trialDrop != null)
            Destroy(trialDrop.gameObject);
    }
}