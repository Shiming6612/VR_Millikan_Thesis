using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class MillikanEvaluationDialogue : MonoBehaviour
{
    private enum Section { PartB, PartC }
    private struct Page
    {
        public int visualization, state, question;
        public Section section;
        public Page(int v, Section part, int s, int q)
        { visualization = v; section = part; state = s; question = q; }
    }

    [Header("Existing Millikan Dialogue")]
    public GameObject dialogueRoot;
    public TMP_Text dialogueText;
    public TMP_Text buttonHintText;
    public GameObject millikanProfessor;

    [Header("Existing Experiment Components")]
    public VoltageKnobInput voltageKnob;
    public VisualizationModeController visualizations;
    public ElectricFieldVolume fieldVolume;
    public DropSelectionManager selectionManager;
    public OilDrop oilDropPrefab;
    public VisualizationTogglePanelDirect visualizationTogglePanel;

    [Header("Part C Original Experiment")]
    public ExperimentController partCExperimentController;
    public SpraySpawner partCSpraySpawner;
    public PressBulbTrigger[] partCBulbs = new PressBulbTrigger[0];
    public float partCStartingVoltage = 0f;

    [Header("Legacy Controllers - scan these BEFORE building")]
    [Tooltip("Old evaluation/state/input/spawn scripts. Disable COMPONENTS, not their GameObjects. The Editor scan fills this list.")]
    public MonoBehaviour[] legacyControllersToDisable = new MonoBehaviour[0];
    [SerializeField, HideInInspector] private bool legacyScanCompleted;

    [Header("Evaluation Order Source")]
    public MonoBehaviour evaluationController;
    [SerializeField, HideInInspector] private int[] visualizationOrder = new int[0];
    [SerializeField, HideInInspector] private bool orderCaptured;
    [SerializeField, HideInInspector] private string capturedOrderName;
    [Tooltip("Open the demonstration on scene start. A starts the evaluation.")]
    public bool beginOnStart = true;

    [Header("Controlled Oil Drop (state order: rising, falling, hovering)")]
    public float hoverVoltage = 500f;
    public float risingVoltage = 600f;
    public float fallingVoltage = 350f;
    public float statePreparationSeconds = 0.6f;
    [Tooltip("Optional observation centre INSIDE the electric field. Empty = field centre.")]
    public Transform observationCenter;
    [Tooltip("Return the SAME droplet to the centre before it leaves the observation area. Clears the old trail.")]
    public bool repeatObservationAtBoundary = true;
    [Range(0.2f, 0.85f)] public float observationAreaFraction = 0.6f;

    [Header("Part B, Question 5 (+100 V)")]
    public float voltageIncrease = 100f;
    public float voltageTolerance = 10f;
    public float requiredStableSeconds = 1f;

    [Header("Optional Dialogue Resize")]
    public bool enlargeExistingDialogue = false;
    public Vector2 panelSize = new Vector2(900f, 520f);

    public Vector2 textSize = new Vector2(840f, 400f);

    [Header("Input / Diagnostics")]
    public float navigationDebounceSeconds = 0.18f;
    public bool logPageChanges = true;
    public float preparationTimeoutSeconds = 4f;

    private static MillikanEvaluationDialogue owner;
    private readonly List<Page> pages = new List<Page>();
    private readonly List<MonoBehaviour> blocked = new List<MonoBehaviour>();
    private int pageIndex = -1;
    private int lastNavigationFrame = -1;
    private float nextNavigationTime;
    private float nextIntegrityCheck;
    private bool running, busy, completed, faulted, initialized;
    private bool nextWasHeld, backWasHeld, boostWasHeld, waitForRelease = true;
    private Coroutine preparation;
    private OilDrop evaluationDrop;
    private Rigidbody dropBody;
    private DropProperties dropProperties;
    private SelectableDrop selectedDrop;
    private BoxCollider fieldBox;
    private float baseVoltage, stableTime, acceptedVoltage;
    private bool voltageTargetHeld;
    private string status = "";
    private float repeatNoticeUntil;
    private float savedSmoothing;
    private bool smoothingOverridden;
    private bool freeInteractionActive;
    private readonly Dictionary<SpraySpawner, int> originalSprayLimits = new Dictionary<SpraySpawner, int>();

    private static readonly string[] VisualizationNames =
    { "Force Arrows", "Force Imbalance Paths", "Particle-Based Force Cloud" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticOwner() { owner = null; }

    private void Awake()
    {
        if (owner != null && owner != this)
        {
            Debug.LogError("[Evaluation] Duplicate questionnaire controller: " + name +
                ". Keep exactly one enabled MillikanEvaluationDialogue.", this);
            enabled = false;
            return;
        }
        owner = this;
        if (dialogueRoot == null) dialogueRoot = gameObject;
        ResolvePartCReferences();
#if UNITY_EDITOR

        ScanLegacyControllersInEditor();
#endif
        BlockLegacyControllers();
    }

    private IEnumerator Start()
    {

        yield return null;
        if (beginOnStart) BeginEvaluation();
    }

    private void Update()
    {
        bool nextHeld, backHeld, boostHeld;
        ReadKeyboardHeld(out nextHeld, out backHeld, out boostHeld);
        nextHeld |= OVRInput.Get(OVRInput.RawButton.A);
        backHeld |= OVRInput.Get(OVRInput.RawButton.B);
        InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool xr;
        if (right.isValid && right.TryGetFeatureValue(CommonUsages.primaryButton, out xr)) nextHeld |= xr;
        if (right.isValid && right.TryGetFeatureValue(CommonUsages.secondaryButton, out xr)) backHeld |= xr;

        bool nextDown = nextHeld && !nextWasHeld;
        bool backDown = backHeld && !backWasHeld;
        bool boostDown = boostHeld && !boostWasHeld;
        nextWasHeld = nextHeld; backWasHeld = backHeld; boostWasHeld = boostHeld;
        if (waitForRelease)
        {
            if (!nextHeld && !backHeld && !boostHeld) waitForRelease = false;
            nextDown = backDown = boostDown = false;
        }
        if (!running || faulted) return;
        KeepLegacyControllersBlocked();
        if (!completed) SyncVisualizationPanel(pageIndex >= 0 ? pages[pageIndex].visualization : -1);
        if (Time.unscaledTime >= nextIntegrityCheck)
        {
            nextIntegrityCheck = Time.unscaledTime + 0.25f;
            if (!CheckSingleDrop()) return;
        }
        if (busy) return;

        if (IsVoltageQuestion() && !voltageTargetHeld)
        {
            if (boostDown) IncreaseVoltage();
            CheckVoltageTarget();
        }
        RefreshHint();
        if (nextHeld && backHeld) return;
        if (backDown) PreviousPage();
        else if (nextDown) NextPage();
    }

    private static void ReadKeyboardHeld(out bool next, out bool back, out bool boost)
    {
        next = back = boost = false;
#if ENABLE_INPUT_SYSTEM

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;
        next = keyboard.digit3Key.isPressed || keyboard.numpad3Key.isPressed;
        back = keyboard.digit1Key.isPressed || keyboard.numpad1Key.isPressed;
        boost = keyboard.digit5Key.isPressed || keyboard.numpad5Key.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER

        next = UnityEngine.Input.GetKey(KeyCode.Alpha3) || UnityEngine.Input.GetKey(KeyCode.Keypad3);
        back = UnityEngine.Input.GetKey(KeyCode.Alpha1) || UnityEngine.Input.GetKey(KeyCode.Keypad1);
        boost = UnityEngine.Input.GetKey(KeyCode.Alpha5) || UnityEngine.Input.GetKey(KeyCode.Keypad5);
#endif
    }

    private void FixedUpdate()
    {
        if (!running || faulted || busy || completed || evaluationDrop == null || pageIndex < 0 || IsPartC())
            return;
        if (!evaluationDrop.gameObject.activeInHierarchy)
        { Fail("The evaluation droplet was disabled by another component."); return; }
        if (repeatObservationAtBoundary)
        {
            Vector3 local = fieldBox.transform.InverseTransformPoint(dropBody.position) - fieldBox.center;
            Vector3 half = fieldBox.size * (0.5f * observationAreaFraction);
            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z)
            {

                Vector3 velocity = dropBody.linearVelocity;
                dropBody.position = ObservationPosition();
                evaluationDrop.transform.position = dropBody.position;
                dropBody.linearVelocity = velocity;
                ClearTrail();
                repeatNoticeUntil = Time.unscaledTime + 1f;
            }
        }
    }

    [ContextMenu("Begin / Restart Evaluation (Play Mode)")]
    public void BeginEvaluation()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || owner != this) return;
        CancelPreparation();
        StopFreeInteraction();
        faulted = false; running = false; completed = false;
        ResolvePartCReferences();
#if UNITY_EDITOR
        if (!CaptureOrderInEditor(out string orderError))
        { Fail(orderError); return; }
#endif
        if (!ValidateSetup()) return;
        BlockLegacyControllers();
        BuildPages();
        if (logPageChanges) Debug.Log("[Evaluation] Order from EvaluationController: " + capturedOrderName, this);
        RemoveAllSceneDrops();
        if (millikanProfessor != null) millikanProfessor.SetActive(true);
        dialogueRoot.SetActive(true);
        ApplyDialogueLayout();
        pageIndex = -1; busy = false; running = true; initialized = true;
        voltageTargetHeld = false; stableTime = 0f;
        waitForRelease = true; lastNavigationFrame = -1; nextNavigationTime = 0f;
        visualizations.HideAllVisualizations();
        SyncVisualizationPanel(-1);
        status = "";
        StartFreeInteraction();
    }

    public void NextPage()
    {
        if (!CanNavigate() || completed) return;
        if (IsVoltageQuestion() && !voltageTargetHeld)
        { status = "Reach the voltage target and hold it for 1 second first."; RefreshHint(); return; }
        RecordNavigation();
        if (pageIndex + 1 == pages.Count)
        {
            completed = true;
            if (IsPartC())
            {
                StopFreeInteraction();
                RemoveAllSceneDrops();
            }
            FreezeDrop();
            voltageKnob.SetInteractionEnabled(false);
            dialogueText.text = "Part B and Part C are complete.\n\nThank you.";
            status = "";
            RefreshHint();
            return;
        }
        OpenPage(pageIndex + 1);
    }

    public void PreviousPage()
    {
        if (!CanNavigate() || pageIndex < 0) return;
        RecordNavigation();
        if (completed)
        {
            completed = false;
            if (IsPartC()) StartFreeInteraction();
            else StartPreparation(pages[pageIndex], voltageKnob.CurrentVoltage);
            return;
        }
        if (pageIndex == 0) return;
        OpenPage(pageIndex - 1);
    }

    private bool CanNavigate()
    {
        return running && !faulted && !busy && isActiveAndEnabled &&
            lastNavigationFrame != Time.frameCount && Time.unscaledTime >= nextNavigationTime;
    }

    private void RecordNavigation()
    {
        lastNavigationFrame = Time.frameCount;
        nextNavigationTime = Time.unscaledTime + Mathf.Max(0f, navigationDebounceSeconds);
    }

    private void OpenPage(int index)
    {
        bool hadPage = pageIndex >= 0;
        Page previous = hadPage ? pages[pageIndex] : default(Page);
        Page current = pages[index];
        bool differentState = !hadPage || previous.visualization != current.visualization ||
            previous.section != current.section ||
            (current.section == Section.PartB && previous.state != current.state);
        bool backFromVoltage = hadPage && previous.section == Section.PartB &&
            previous.question == 2 && current.section == Section.PartB && current.question < 2;
        pageIndex = index;
        status = ""; stableTime = 0f; voltageTargetHeld = false;
        baseVoltage = current.section == Section.PartB ? StateVoltage(current.state) : hoverVoltage;
        if (differentState) SetVisualization(current.visualization);
        if (current.section == Section.PartC)
        {
            if (differentState) StartFreeInteraction();
            else ShowCurrentPage();
            return;
        }
        if (!hadPage || previous.section == Section.PartC)
        {
            StopFreeInteraction();
            RemoveAllSceneDrops();
            if (!CreateSingleDrop()) return;
        }
        if (differentState || backFromVoltage)
        { StartPreparation(current, baseVoltage); return; }

        ShowCurrentPage();
    }

    private void StartPreparation(Page page, float voltage)
    {
        CancelPreparation();
        busy = true;
        preparation = StartCoroutine(PreparePage(page, voltage));
    }

    private IEnumerator PreparePage(Page page, float voltage)
    {
        dialogueText.text = VisualizationNames[page.visualization] + "\n\nPreparing the observation...";
        buttonHintText.text = "Please wait.";
        voltageKnob.SetInteractionEnabled(false);
        SetVoltageImmediately(voltage);
        FreezeDrop();
        ClearTrail();
        float elapsed = 0f;
        float ratio = 0f, tolerance;
        bool ready = false;
        while (elapsed < Mathf.Max(1f, preparationTimeoutSeconds))
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            ready = fieldVolume.TryGetBalanceState(selectedDrop, out ratio, out tolerance) &&
                Mathf.Abs(fieldVolume.SmoothedVoltageMagnitude - voltage) <= 0.5f;
            if (ready && elapsed >= Mathf.Max(0.05f, statePreparationSeconds)) break;
        }
        RestoreFieldSmoothing();
        if (!ready)
        {
            preparation = null; busy = false;
            Fail("The droplet is not receiving the expected electric field. Check the field collider, " +
                 "OilDrop tag, collision layers and voltage source.");
            yield break;
        }
        dropBody.isKinematic = false;
        evaluationDrop.enabled = true;
        dropBody.linearVelocity = ObservationVelocity(ratio);
        selectionManager.SetSelectionEnabled(true);
        selectionManager.SetSelected(selectedDrop);
        ClearTrail();
        preparation = null; busy = false;
        ShowCurrentPage();
    }

    private void ShowCurrentPage()
    {
        Page page = pages[pageIndex];
        stableTime = 0f; voltageTargetHeld = false; status = "";
        voltageKnob.SetInteractionEnabled(page.section == Section.PartC || page.question == 2);

        baseVoltage = page.section == Section.PartB ? StateVoltage(page.state) : hoverVoltage;
        dialogueText.text = page.section == Section.PartB ? PartBText(page) : PartCText(page);
        RefreshHint();
        if (logPageChanges)
            Debug.Log("[Evaluation] Page " + (pageIndex + 1) + "/" + pages.Count +
                " | " + page.section + " | visualization=" + page.visualization +
                " | state=" + (page.state + 1) + " | question=" + (page.question + 1) +
                " | droplet instance=" + (evaluationDrop != null ? evaluationDrop.GetInstanceID().ToString() : "participant-generated"), this);
    }

    public void IncreaseVoltage()
    {
        if (!running || busy || faulted || !IsVoltageQuestion() || voltageTargetHeld) return;

        voltageKnob.SetInteractionEnabled(false);
        voltageKnob.SetVoltageFromExternal(baseVoltage + voltageIncrease);
        voltageKnob.SetInteractionEnabled(true);
        stableTime = 0f; status = "";
    }

    private void CheckVoltageTarget()
    {
        float target = baseVoltage + voltageIncrease;
        bool inRange = Mathf.Abs(voltageKnob.CurrentVoltage - target) <= voltageTolerance + 0.001f;
        bool fieldCaughtUp = Mathf.Abs(fieldVolume.SmoothedVoltageMagnitude - voltageKnob.CurrentVoltage) <= 1f;
        if (!inRange || !fieldCaughtUp) { stableTime = 0f; return; }
        stableTime += Time.unscaledDeltaTime;
        if (stableTime < requiredStableSeconds) return;
        voltageTargetHeld = true;
        acceptedVoltage = voltageKnob.CurrentVoltage;
        voltageKnob.SetInteractionEnabled(false);
        status = "Voltage reached. Observe and answer aloud.";

    }

    private bool IsVoltageQuestion()
    {
        return !completed && pageIndex >= 0 && pageIndex < pages.Count &&
        pages[pageIndex].section == Section.PartB && pages[pageIndex].question == 2;
    }

    private void RefreshHint()
    {
        if (buttonHintText == null || faulted || busy) return;
        if (completed) { buttonHintText.text = "B: Back to the last question"; return; }
        if (pageIndex < 0) { buttonHintText.text = "A: Start evaluation when the demonstration is finished"; return; }
        string line = "Answer aloud.";
        if (pages[pageIndex].section == Section.PartC)
            line = selectionManager.CurrentSelected == null
                ? "Press and release the bulb, then select a droplet with the trigger."
                : "Use the knob to make the droplet rise, fall and hover. Answer aloud.";
        else if (IsVoltageQuestion() && !voltageTargetHeld)
        {
            float target = baseVoltage + voltageIncrease;
            line = "Use the knob to increase the voltage by 100 V. Target: " + (target - voltageTolerance).ToString("0") +
                "-" + (target + voltageTolerance).ToString("0") + " V; hold for " +
                requiredStableSeconds.ToString("0.#") + " s.";
        }
        if (!string.IsNullOrEmpty(status)) line = status;
        if (Time.unscaledTime < repeatNoticeUntil) line += " Observation repeats.";
        string hint = line + "\nA: Next    B: Back";
        if (buttonHintText.text != hint) buttonHintText.text = hint;
    }

    private string PartBText(Page page)
    {

        string heading = "Part B | " + VisualizationNames[page.visualization] +
            "\nObservation " + (page.state + 1) + "/3 | Question pair " + (page.question + 1) + "/3\n\n";
        string question;
        if (page.question == 0)
            question = "1. In which direction is the electric force acting?\nA: Upwards\nB: Downwards";
        else if (page.question == 1)
            question = "3. How close is the droplet to a state of balanced forces?\n" +
                "Far from balanced\nClose to balanced\nBalanced";
        else
            question = "5. After increasing the voltage by 100 V, how is the droplet moving?\n" +
                "Moving upward\nStationary\nMoving downward";
        return heading + question + "\n\n" + (page.question * 2 + 2) +
            ". How confident are you that your answer is correct?\n" +
            "1 = Not confident at all     6 = Very confident\n1    2    3    4    5    6";
    }

    private string PartCText(Page page)
    {
        string[] questions = {
            "The visualization helped me judge whether the upward electric force and gravity were balanced.",
            "It was easy to understand what the visual elements represented.",
            "The visualization was visually cluttered.",
            "Was anything about this visualization unclear or difficult to interpret?"
        };
        return "Part C | " + VisualizationNames[page.visualization] + "\nQuestion " +
            (page.question + 1) + "/4\nPress the bulb, select a droplet, then use the knob to make it rise, fall and hover.\n\n" +
            (page.question + 1) + ". " + questions[page.question] + (page.question < 3
            ? "\n\n1 = Strongly disagree     6 = Strongly agree\n1    2    3    4    5    6"
            : "\n\nPlease answer aloud.");
    }

    private void BuildPages()
    {
        pages.Clear();
        foreach (int visualization in visualizationOrder)
        {
            for (int state = 0; state < 3; state++)
                for (int q = 0; q < 3; q++) pages.Add(new Page(visualization, Section.PartB, state, q));
        }

        foreach (int visualization in visualizationOrder)
        {
            for (int q = 0; q < 4; q++) pages.Add(new Page(visualization, Section.PartC, 0, q));
        }
    }

    private float StateVoltage(int state)
    { return state == 0 ? risingVoltage : state == 1 ? fallingVoltage : hoverVoltage; }

    private void SetVisualization(int index)
    {
        visualizations.HideAllVisualizations();
        if (index == 0) visualizations.SetForceArrowsVisible(true);
        else if (index == 1) visualizations.SetTrailPathVisible(true);
        else visualizations.SetFieldCloudVisible(true);
        SyncVisualizationPanel(index);
    }

    private void SyncVisualizationPanel(int index)
    {
        if (visualizationTogglePanel == null) return;
        UnityEngine.UI.Toggle[] toggles = {
            visualizationTogglePanel.forceArrowsToggle,
            visualizationTogglePanel.forceImbalancePathsToggle,
            visualizationTogglePanel.fieldCloudToggle
        };
        if (index >= 0 && index < toggles.Length && toggles[index] != null)
            toggles[index].SetIsOnWithoutNotify(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i] == null) continue;
            toggles[i].transition = UnityEngine.UI.Selectable.Transition.None;
            toggles[i].interactable = false;
            UnityEngine.UI.ToggleGroup group = toggles[i].group;
            bool allowSwitchOff = group != null && group.allowSwitchOff;
            if (index < 0 && group != null) group.allowSwitchOff = true;
            toggles[i].SetIsOnWithoutNotify(i == index);
            if (index < 0 && group != null) group.allowSwitchOff = allowSwitchOff;
        }
    }

    private bool IsPartC()
    { return pageIndex >= 0 && pageIndex < pages.Count && pages[pageIndex].section == Section.PartC; }

    private void ResolvePartCReferences()
    {
        if (partCExperimentController == null)
        {
            var matches = new List<ExperimentController>();
            foreach (ExperimentController candidate in FindObjectsByType<ExperimentController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (InEvaluationScene(candidate) && candidate.electricFieldVolume == fieldVolume &&
                    candidate.dropSelectionManager == selectionManager) matches.Add(candidate);
            if (matches.Count == 1) partCExperimentController = matches[0];
        }
        if (partCSpraySpawner == null && partCExperimentController != null)
            partCSpraySpawner = partCExperimentController.spraySpawner;
        if (partCBulbs == null || partCBulbs.Length == 0)
        {
            var bulbs = new List<PressBulbTrigger>();
            foreach (PressBulbTrigger bulb in FindObjectsByType<PressBulbTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (InEvaluationScene(bulb) && partCExperimentController != null &&
                    bulb.experimentController == partCExperimentController) bulbs.Add(bulb);
            partCBulbs = bulbs.ToArray();
        }
        if (visualizationTogglePanel == null)
        {
            var panels = new List<VisualizationTogglePanelDirect>();
            foreach (VisualizationTogglePanelDirect panel in FindObjectsByType<VisualizationTogglePanelDirect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (InEvaluationScene(panel) && panel.visualizationController == visualizations) panels.Add(panel);
            if (panels.Count == 1) visualizationTogglePanel = panels[0];
        }
    }

    private bool IsFreeInteractionComponent(MonoBehaviour component)
    {
        if (!freeInteractionActive) return false;
        if (component == partCExperimentController || component == partCSpraySpawner) return true;
        if (partCBulbs != null)
            foreach (PressBulbTrigger bulb in partCBulbs) if (component == bulb) return true;
        return false;
    }

    private void StopFreeInteraction()
    {
        freeInteractionActive = false;
        if (partCBulbs != null)
            foreach (PressBulbTrigger bulb in partCBulbs) if (bulb != null) Block(bulb);
        if (partCExperimentController != null) Block(partCExperimentController);
        if (partCSpraySpawner != null)
        {
            if (!originalSprayLimits.ContainsKey(partCSpraySpawner))
                originalSprayLimits.Add(partCSpraySpawner, partCSpraySpawner.maxTotalDrops);
            Block(partCSpraySpawner);
            partCSpraySpawner.maxTotalDrops = 0;
            partCSpraySpawner.ResetAllDrops();
        }
    }

    private void StartFreeInteraction()
    {
        CancelPreparation();
        StopFreeInteraction();
        busy = true;
        preparation = StartCoroutine(PrepareFreeInteraction());
    }

    private IEnumerator PrepareFreeInteraction()
    {
        bool demonstration = pageIndex < 0;
        dialogueText.text = (demonstration ? "Demonstration" : VisualizationNames[pages[pageIndex].visualization]) +
            "\n\nPreparing the experiment...";
        buttonHintText.text = "Release the bulb. Please wait.";
        voltageKnob.SetInteractionEnabled(false);
        partCExperimentController.ResetExperiment();
        RemoveAllSceneDrops();
        voltageKnob.SetVoltageFromExternal(partCStartingVoltage);
        repeatNoticeUntil = 0f;
        yield return null;
        partCSpraySpawner.ReturnToRandomModeAndClearDrops();
        partCSpraySpawner.maxTotalDrops = originalSprayLimits[partCSpraySpawner];
        freeInteractionActive = true;
        partCSpraySpawner.enabled = true;
        partCExperimentController.enabled = true;
        selectionManager.allowPrimaryButtonFallback = false;
        yield return null;
        selectionManager.ClearSelectionAndHover();
        selectionManager.SetSelectionEnabled(fieldVolume.HasBodiesInside);
        foreach (PressBulbTrigger bulb in partCBulbs) bulb.enabled = true;
        preparation = null;
        busy = false;
        if (demonstration)
        {
            visualizations.HideAllVisualizations();
            SyncVisualizationPanel(-1);
            voltageKnob.SetInteractionEnabled(true);
            dialogueText.text = "Demonstration\nNo visualization active\n\n" +
                "The researcher will demonstrate how to press the bulb, select a droplet, " +
                "and use the voltage knob to make it rise, fall and hover.\n\n" +
                "The evaluation has not started. No answers are required yet.";
            RefreshHint();
        }
        else ShowCurrentPage();
    }

    private bool CreateSingleDrop()
    {
        evaluationDrop = Instantiate(oilDropPrefab);
        evaluationDrop.name = "EvaluationOilDrop_SINGLE";
        SceneManager.MoveGameObjectToScene(evaluationDrop.gameObject, fieldVolume.gameObject.scene);
        dropProperties = evaluationDrop.GetComponent<DropProperties>();
        selectedDrop = evaluationDrop.GetComponent<SelectableDrop>();
        dropBody = evaluationDrop.GetComponent<Rigidbody>();

        dropProperties.randomizeOnSpawn = false;
        evaluationDrop.gameObject.SetActive(true);
        double charge = 1.602176634e-19;
        float g = Mathf.Abs(Vector3.Dot(evaluationDrop.customGravity, fieldVolume.fieldDirection.normalized));
        double radius = Math.Pow(3.0 * charge * hoverVoltage * fieldVolume.fieldScale /
            (4.0 * Math.PI * dropProperties.oilDensityKgPerM3 * g * fieldVolume.GetPlateSpacingMeters()), 1.0 / 3.0) * 1e6;
        evaluationDrop.launchPhaseDuration = 0f;
        evaluationDrop.destroyOnCollision = false;
        evaluationDrop.Launch(ObservationPosition(), Vector3.zero);

        dropProperties.ApplyRadiusAndCharge((float)radius, 1);
        selectionManager.allowPrimaryButtonFallback = false;
        selectionManager.SetSelectionEnabled(true);
        selectionManager.SetSelected(selectedDrop);
        return true;
    }

    private Vector3 ObservationPosition()
    { return observationCenter != null ? observationCenter.position : fieldBox.transform.TransformPoint(fieldBox.center); }

    private Vector3 ObservationVelocity(float ratio)
    {
        float radiusFactor = evaluationDrop.radiusAffectsFallSpeed
            ? Mathf.Pow(dropProperties.RadiusMicrometer / Mathf.Max(0.01f, evaluationDrop.referenceRadiusMicrometer),
                Mathf.Max(0f, evaluationDrop.radiusFallSpeedPower)) : 1f;
        float speed = Mathf.Max(0.01f, evaluationDrop.baseFallSpeed * radiusFactor);
        float delta = ratio - 1f;
        float vertical = Mathf.Abs(delta) <= Mathf.Clamp(evaluationDrop.hoverDeadZone, 0f, 0.99f) ? 0f :
            Mathf.Sign(delta) * speed * Mathf.Pow(Mathf.Abs(delta), Mathf.Max(0.01f, evaluationDrop.electricResponsePower));
        return Vector3.up * Mathf.Clamp(vertical, -evaluationDrop.maxVerticalSpeed, evaluationDrop.maxVerticalSpeed);
    }

    private void FreezeDrop()
    {
        if (dropBody == null) return;

        evaluationDrop.enabled = false;
        if (!dropBody.isKinematic) { dropBody.linearVelocity = Vector3.zero; dropBody.angularVelocity = Vector3.zero; }
        dropBody.isKinematic = true;
        dropBody.position = ObservationPosition();
        evaluationDrop.transform.position = dropBody.position;
    }

    private void ClearTrail()
    {
        if (evaluationDrop == null) return;
        foreach (SelectedDropletTrail trail in evaluationDrop.GetComponentsInChildren<SelectedDropletTrail>(true)) trail.ClearTrail();
        foreach (TrailRenderer trail in evaluationDrop.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
    }

    private void SetVoltageImmediately(float voltage)
    {
        if (!smoothingOverridden) { savedSmoothing = fieldVolume.voltageSmoothing; smoothingOverridden = true; }

        fieldVolume.voltageSmoothing = 1000f;
        voltageKnob.SetVoltageFromExternal(voltage);
    }

    private void RestoreFieldSmoothing()
    {
        if (smoothingOverridden && fieldVolume != null) fieldVolume.voltageSmoothing = savedSmoothing;
        smoothingOverridden = false;
    }

    private void CancelPreparation()
    {
        if (preparation != null) StopCoroutine(preparation);
        preparation = null; busy = false;
        RestoreFieldSmoothing();
    }

    private bool InEvaluationScene(Component component)
    {
        return component != null && component.gameObject.scene.IsValid() &&
        (component.gameObject.scene == gameObject.scene ||
         (fieldVolume != null && component.gameObject.scene == fieldVolume.gameObject.scene));
    }

    private void RemoveAllSceneDrops()
    {
        selectionManager.ClearSelectionAndHover();
        foreach (OilDrop drop in FindObjectsByType<OilDrop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!InEvaluationScene(drop)) continue;
            drop.gameObject.SetActive(false);
            Destroy(drop.gameObject);
        }
        evaluationDrop = null; dropBody = null; selectedDrop = null; dropProperties = null;
    }

    private bool CheckSingleDrop()
    {
        if (completed) return true;
        if (pageIndex < 0 || IsPartC())
        {
            if (busy) return true;
            if (!freeInteractionActive || !partCExperimentController.isActiveAndEnabled || !partCSpraySpawner.isActiveAndEnabled)
            { Fail("The demonstration / Part C experiment or spray component is disabled."); return false; }
            if (pageIndex < 0)
            {
                if (visualizations.showForceArrows || visualizations.showTrailPath || visualizations.showFieldCloud)
                { Fail("Another controller enabled a visualization during the demonstration."); return false; }
                return true;
            }
            Page current = pages[pageIndex];
            if (visualizations.showForceArrows != (current.visualization == 0) ||
                visualizations.showTrailPath != (current.visualization == 1) ||
                visualizations.showFieldCloud != (current.visualization == 2))
            { Fail("Another controller changed the assigned Part C visualization."); return false; }
            return true;
        }
        if (evaluationDrop == null) { Fail("The evaluation droplet was destroyed by another component."); return false; }
        foreach (OilDrop drop in FindObjectsByType<OilDrop>(FindObjectsSortMode.None))
        {
            if (drop == evaluationDrop || !InEvaluationScene(drop)) continue;
            drop.gameObject.SetActive(false);
            Destroy(drop.gameObject);
            Fail("Another script created an OilDrop. Run Scan Legacy Controllers, and add the old " +
                 "state/spawn controller to Legacy Controllers To Disable. Evaluation paused.");
            return false;
        }
        if (pageIndex >= 0 && !busy && !completed)
        {
            if (selectionManager.CurrentSelected != selectedDrop)
            { Fail("Another controller changed the selected droplet. Check Legacy Controllers To Disable."); return false; }
            Page page = pages[pageIndex];
            if (visualizations.showForceArrows != (page.visualization == 0) ||
                visualizations.showTrailPath != (page.visualization == 1) ||
                visualizations.showFieldCloud != (page.visualization == 2))
            { Fail("Another controller changed the visualization. Check Legacy Controllers To Disable."); return false; }
            if (page.section == Section.PartB && (page.question < 2 || voltageTargetHeld))
            {
                float expected = voltageTargetHeld ? acceptedVoltage : baseVoltage;
                if (Mathf.Abs(voltageKnob.CurrentVoltage - expected) > 0.51f)
                { Fail("Another controller changed the locked Part B voltage. Check the old state controller."); return false; }
            }
        }
        return true;
    }

    private static readonly HashSet<string> KnownLegacyTypes = new HashSet<string> {
        "BottomTutorialController", "Seat01TutorialController", "PrePostQuizController",
        "RoomIntroQuizController", "StoryPlaceholderController", "StoryMeasurementRecorder",
        "ExperimentController", "SpraySpawner", "PressBulbTrigger", "RadiusSliderController",
        "VisualizationTogglePanelDirect", "PumpPressInteraction", "DropletVoltageAnimator",
        "DropletSpeedAfterEntry", "VoltageKnobRotator", "EvaluationController"
    };

    private bool IsProtected(MonoBehaviour component)
    {
        if (component == null) return true;
        string type = component.GetType().Name;
        return component is MillikanEvaluationDialogue || component == voltageKnob ||
            component == visualizations || component == fieldVolume || component == selectionManager ||
            type == "VoltageKnobInput" || type == "VisualizationModeController" || type == "ElectricFieldVolume" ||
            type == "DropSelectionManager" || type == "OilDrop" || type == "DropProperties" ||
            type == "SelectableDrop" || type == "SelectedDropletTrail" || type == "FieldParticleCloudVisualizer" ||
            type == "ForceArrowOverlay" || type == "ForcesVisualizer" || type == "LegendUIController";
    }

    private void BlockLegacyControllers()
    {
        if (evaluationController != null) Block(evaluationController);
        foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (InEvaluationScene(component) && KnownLegacyTypes.Contains(component.GetType().Name)) Block(component);
        if (visualizationTogglePanel != null) Block(visualizationTogglePanel);
        if (legacyControllersToDisable != null)
            foreach (MonoBehaviour component in legacyControllersToDisable) Block(component);

        foreach (SpraySpawner spawner in FindObjectsByType<SpraySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (InEvaluationScene(spawner))
            {
                if (!originalSprayLimits.ContainsKey(spawner)) originalSprayLimits.Add(spawner, spawner.maxTotalDrops);
                if (IsFreeInteractionComponent(spawner)) continue;
                spawner.StopAllCoroutines(); spawner.maxTotalDrops = 0; spawner.ResetAllDrops();
            }
    }

    private void Block(MonoBehaviour component)
    {
        if (component == null || IsProtected(component) || IsFreeInteractionComponent(component) || !component.gameObject.scene.IsValid()) return;
        bool first = !blocked.Contains(component);
        component.StopAllCoroutines(); component.CancelInvoke(); component.enabled = false;
        if (first)
        {
            blocked.Add(component); Debug.Log("[Evaluation] Disabled legacy component: " +
            component.GetType().Name + " on " + component.name, component);
        }
    }

    private void KeepLegacyControllersBlocked()
    {
        for (int i = 0; i < blocked.Count; i++)
            if (blocked[i] != null && blocked[i].enabled) Block(blocked[i]);
    }

    private bool ValidateSetup()
    {
        if (partCExperimentController == null || partCSpraySpawner == null ||
            !InEvaluationScene(partCExperimentController) || !InEvaluationScene(partCSpraySpawner) ||
            !partCExperimentController.gameObject.activeInHierarchy || !partCSpraySpawner.gameObject.activeInHierarchy ||
            partCExperimentController.spraySpawner != partCSpraySpawner ||
            partCExperimentController.electricFieldVolume != fieldVolume ||
            partCExperimentController.dropSelectionManager != selectionManager)
        { Fail("Assign the original ExperimentController and its SpraySpawner for Part C; they must share this field and selection manager."); return false; }
        if (partCSpraySpawner.spawnOrigin == null || partCSpraySpawner.dropPrefab == null ||
            !originalSprayLimits.TryGetValue(partCSpraySpawner, out int sprayLimit) || sprayLimit <= 0)
        { Fail("The Part C SpraySpawner needs a spawn origin, a droplet prefab and an original Max Total Drops above zero."); return false; }
        if (partCBulbs == null || partCBulbs.Length == 0)
        { Fail("Assign the original PressBulbTrigger in Part C Bulbs."); return false; }
        foreach (PressBulbTrigger bulb in partCBulbs)
            if (bulb == null || !InEvaluationScene(bulb) || !bulb.gameObject.activeInHierarchy ||
                bulb.experimentController != partCExperimentController)
            { Fail("Every Part C bulb must use the assigned original ExperimentController and have an active GameObject."); return false; }
        if (visualizationTogglePanel == null || visualizationTogglePanel.visualizationController != visualizations ||
            visualizationTogglePanel.forceArrowsToggle == null || visualizationTogglePanel.forceImbalancePathsToggle == null ||
            visualizationTogglePanel.fieldCloudToggle == null)
        { Fail("Assign the VisualizationTogglePanelDirect with all three toggles and the same visualization controller."); return false; }
        if (!orderCaptured || evaluationController == null || !InEvaluationScene(evaluationController))
        { Fail("Assign the scene EvaluationController as Evaluation Controller, then restart Play or rebuild."); return false; }
        if (dialogueRoot == null || dialogueText == null || buttonHintText == null || voltageKnob == null ||
            visualizations == null || fieldVolume == null || selectionManager == null || oilDropPrefab == null)
        { Fail("Assign all dialogue and experiment references, including the OilDrop prefab."); return false; }
        if (!legacyScanCompleted)
        { Fail("Run Scan Legacy Controllers in the Inspector, save the scene, then test/build again."); return false; }
        if (!dialogueRoot.scene.IsValid() || !dialogueText.gameObject.scene.IsValid() ||
            !buttonHintText.gameObject.scene.IsValid() ||
            !dialogueText.transform.IsChildOf(dialogueRoot.transform) ||
            !buttonHintText.transform.IsChildOf(dialogueRoot.transform))
        { Fail("Assign the scene's BottomTutorialUI and its two child text objects, not prefab assets or another UI instance."); return false; }
        if (oilDropPrefab.gameObject.scene.IsValid())
        { Fail("Oil Drop Prefab must be the asset from Project, not a scene droplet."); return false; }
        if (!voltageKnob.isActiveAndEnabled || !fieldVolume.isActiveAndEnabled ||
            !selectionManager.isActiveAndEnabled || !visualizations.isActiveAndEnabled)
        { Fail("Keep VoltageKnobInput, ElectricFieldVolume, DropSelectionManager and VisualizationModeController enabled."); return false; }
        if (partCStartingVoltage < voltageKnob.minVoltage || partCStartingVoltage > voltageKnob.maxVoltage)
        { Fail("Part C Starting Voltage must be within the knob's range."); return false; }
        fieldBox = fieldVolume.GetComponent<BoxCollider>();
        if (fieldBox == null || !fieldBox.enabled || !fieldBox.isTrigger || fieldVolume.voltageSource != voltageKnob)
        { Fail("The field needs an enabled trigger BoxCollider and must use this same voltage knob."); return false; }
        if (oilDropPrefab.GetComponent<Rigidbody>() == null || oilDropPrefab.GetComponent<DropProperties>() == null ||
            oilDropPrefab.GetComponent<SelectableDrop>() == null || oilDropPrefab.GetComponent<Collider>() == null)
        { Fail("The OilDrop prefab root needs Rigidbody, Collider, DropProperties and SelectableDrop."); return false; }
        if (!oilDropPrefab.CompareTag(fieldVolume.oilDropTag) ||
            Physics.GetIgnoreLayerCollision(oilDropPrefab.gameObject.layer, fieldVolume.gameObject.layer))
        { Fail("The OilDrop tag/layer must allow the droplet collider to enter the electric field."); return false; }
        if (Vector3.Dot(fieldVolume.fieldDirection.normalized, Vector3.up) < 0.999f ||
            fieldVolume.fieldScale <= 0f || fieldVolume.GetPlateSpacingMeters() <= 0f ||
            Mathf.Abs(oilDropPrefab.customGravity.y) < 0.001f ||
            oilDropPrefab.GetComponent<DropProperties>().oilDensityKgPerM3 <= 0f)
        { Fail("This project's OilDrop uses vertical motion. Check upward field direction, gravity, density and plate spacing."); return false; }
        if (visualizationOrder == null || visualizationOrder.Length != 3 ||
            new HashSet<int>(visualizationOrder).Count != 3)
        { Fail("Visualization Order must be a permutation of 0, 1, 2."); return false; }
        foreach (int v in visualizationOrder) if (v < 0 || v > 2)
            { Fail("Visualization Order must contain only 0, 1, 2."); return false; }
        float deadZone = Mathf.Clamp(oilDropPrefab.hoverDeadZone, 0f, 0.99f);
        if (hoverVoltage <= 0f || risingVoltage <= hoverVoltage * (1f + deadZone) ||
            fallingVoltage >= hoverVoltage * (1f - deadZone) || voltageIncrease <= 0f ||
            voltageTolerance < 0f || voltageTolerance >= voltageIncrease || requiredStableSeconds <= 0f)
        { Fail("Check rising/falling/hover voltages and the +100 V tolerance/hold duration."); return false; }
        for (int state = 0; state < 3; state++)
        {
            float v = StateVoltage(state);
            if (v < voltageKnob.minVoltage || v + voltageIncrease > voltageKnob.maxVoltage)
            { Fail("A state voltage or its +100 V target is outside the knob's range."); return false; }
            if (voltageKnob.snapToStep && voltageKnob.voltageStep > 0f &&
                (Mathf.Abs(v - Mathf.Round(v / voltageKnob.voltageStep) * voltageKnob.voltageStep) > 0.1f ||
                 Mathf.Abs((v + voltageIncrease) - Mathf.Round((v + voltageIncrease) / voltageKnob.voltageStep) * voltageKnob.voltageStep) > 0.1f))
            { Fail("The state voltages and +100 V targets must be representable by the knob's voltage step."); return false; }
        }
        Vector3 local = fieldBox.transform.InverseTransformPoint(ObservationPosition()) - fieldBox.center;
        Vector3 half = fieldBox.size * (0.5f * observationAreaFraction * 0.9f);
        if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z)
        { Fail("Observation Center must be inside the observation area, away from the plates."); return false; }
        return true;
    }

    private void ApplyDialogueLayout()
    {
        dialogueText.enableAutoSizing = true; dialogueText.fontSizeMin = 18f; dialogueText.fontSizeMax = 32f;
        buttonHintText.color = Color.white;
        if (!enlargeExistingDialogue) return;

        RectTransform panel = dialogueText.transform.parent as RectTransform;
        if (panel == null) return;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(Mathf.Max(900f, panelSize.x), Mathf.Max(520f, panelSize.y));
        RectTransform body = dialogueText.rectTransform;
        body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(25f, 100f); body.offsetMax = new Vector2(-25f, -20f);
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        RectTransform hint = buttonHintText.rectTransform;
        hint.anchorMin = Vector2.zero; hint.anchorMax = new Vector2(1f, 0f);
        hint.pivot = new Vector2(0.5f, 0f); hint.sizeDelta = new Vector2(-50f, 80f);
        hint.anchoredPosition = new Vector2(0f, 12f);
        buttonHintText.enableAutoSizing = true; buttonHintText.fontSizeMin = 18f; buttonHintText.fontSizeMax = 22f;
        buttonHintText.alignment = TextAlignmentOptions.BottomRight;
    }

    private void Fail(string message)
    {
        faulted = true; running = false; busy = false;
        CancelPreparation();
        StopFreeInteraction();
        if (voltageKnob != null) voltageKnob.SetInteractionEnabled(false);
        if (dropBody != null && fieldBox != null) FreezeDrop();
        if (dialogueText != null) dialogueText.text = "Evaluation paused\n\nResearcher: " + message;
        if (buttonHintText != null) buttonHintText.text = "Check Console. Stop Play, fix the setup, then restart.";
        Debug.LogError("[Evaluation] " + message, this);
    }

    private void OnDisable()
    {
        if (owner != this) return;
        CancelPreparation(); running = false;
        bool wasFreeInteraction = freeInteractionActive;
        StopFreeInteraction();
        if (wasFreeInteraction && selectionManager != null) RemoveAllSceneDrops();
        if (initialized && voltageKnob != null) voltageKnob.SetInteractionEnabled(false);
        if (selectedDrop != null && selectionManager != null && selectionManager.CurrentSelected == selectedDrop)
            selectionManager.ClearSelectionAndHover();
        if (evaluationDrop != null) { evaluationDrop.gameObject.SetActive(false); Destroy(evaluationDrop.gameObject); }
        evaluationDrop = null; dropBody = null; selectedDrop = null;

    }

    private void OnDestroy() { if (owner == this) owner = null; }
    public bool IsComplete { get { return completed; } }

#if UNITY_EDITOR
    public bool CaptureOrderInEditor(out string error)
    {
        orderCaptured = false;
        capturedOrderName = "";
        visualizationOrder = new int[0];
        error = "";
        if (evaluationController == null)
        {
            foreach (MonoBehaviour candidate in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!InEvaluationScene(candidate) || candidate.GetType().Name != "EvaluationController") continue;
                if (evaluationController != null)
                {
                    evaluationController = null;
                    error = "Multiple EvaluationControllers found. Assign the intended component as Evaluation Controller.";
                    return false;
                }
                evaluationController = candidate;
            }
        }
        if (evaluationController == null || !InEvaluationScene(evaluationController) ||
            evaluationController.GetType().Name != "EvaluationController")
        {
            error = "Assign the scene's EvaluationController component as Evaluation Controller.";
            return false;
        }
        string[] names = {
            "O1_Arrows_Paths_Cloud", "O2_Arrows_Cloud_Paths",
            "O3_Paths_Arrows_Cloud", "O4_Paths_Cloud_Arrows",
            "O5_Cloud_Arrows_Paths", "O6_Cloud_Paths_Arrows"
        };
        int[][] orders = {
            new[] { 0, 1, 2 }, new[] { 0, 2, 1 },
            new[] { 1, 0, 2 }, new[] { 1, 2, 0 },
            new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
        };
        var source = new UnityEditor.SerializedObject(evaluationController);
        source.Update();
        var property = source.GetIterator();
        int matches = 0;
        while (property.Next(true))
        {
            if (property.propertyType != UnityEditor.SerializedPropertyType.Enum) continue;
            string[] enumNames = property.enumNames;
            if (enumNames.Length != names.Length) continue;
            bool compatible = true;
            foreach (string expected in names)
                if (Array.IndexOf(enumNames, expected) < 0) { compatible = false; break; }
            if (!compatible) continue;
            int selected = property.enumValueIndex;
            if (selected < 0 || selected >= enumNames.Length)
            {
                error = "EvaluationController has an invalid Order value. Select O1 through O6 before Play.";
                return false;
            }
            matches++;
            capturedOrderName = enumNames[selected];
            visualizationOrder = (int[])orders[Array.IndexOf(names, capturedOrderName)].Clone();
        }
        if (matches != 1)
        {
            error = "Could not uniquely identify the O1-O6 Order field in EvaluationController. Check its script and selection.";
            visualizationOrder = new int[0];
            return false;
        }
        orderCaptured = true;
        return true;
    }

    [ContextMenu("Scan Legacy Controllers (Editor)")]
    public void ScanLegacyControllersInEditor()
    {
        if (!Application.isPlaying) UnityEditor.Undo.RecordObject(this, "Scan evaluation legacy controllers");
        ResolvePartCReferences();
        if (!CaptureOrderInEditor(out string orderError)) Debug.LogError("[Evaluation] " + orderError, this);
        var result = new List<MonoBehaviour>();
        if (legacyControllersToDisable != null)
            foreach (MonoBehaviour item in legacyControllersToDisable)
                if (item != null && !IsProtected(item) && !result.Contains(item)) result.Add(item);
        foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!InEvaluationScene(component) || IsProtected(component)) continue;
            bool match = KnownLegacyTypes.Contains(component.GetType().Name);
            if (!match)
            {
                UnityEditor.MonoScript script = UnityEditor.MonoScript.FromMonoBehaviour(component);
                if (script == null) continue;
                string path = UnityEditor.AssetDatabase.GetAssetPath(script);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                string code = script.text;

                code = System.Text.RegularExpressions.Regex.Replace(code,
                    @"@""(?:""""|[^""])*""|""(?:\\.|[^""\\])*""|//[^\r\n]*|/\*[\s\S]*?\*/", " ");
                bool key = System.Text.RegularExpressions.Regex.IsMatch(code,
                    @"KeyCode\s*\.\s*(Alpha[135]|Keypad[135])\b|\.(digit[135]Key|numpad[135]Key)\b|OVRInput\s*\.\s*(Button\s*\.\s*(One|Two)|RawButton\s*\.\s*[AB])\b|CommonUsages\s*\.\s*(primaryButton|secondaryButton)\b");

                bool experiment = System.Text.RegularExpressions.Regex.IsMatch(code,
                    @"\b(OilDrop|DropProperties|SpraySpawner|VoltageKnobInput|ElectricFieldVolume|DropSelectionManager|VisualizationModeController)\b") ||
                    System.Text.RegularExpressions.Regex.IsMatch(component.GetType().Name,
                        @"Millikan|Evaluation|Trial|Scenario|Study", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                match = key && experiment;
            }
            if (match && !result.Contains(component)) result.Add(component);
        }
        legacyControllersToDisable = result.ToArray();
        legacyScanCompleted = true;
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        Debug.Log("[Evaluation] Scan found " + result.Count + " legacy components. They will be disabled in Play mode. " +
            "Review Legacy Controllers To Disable and SAVE the scene before building. Custom InputAction/Event-only " +
            "state controllers may need to be added manually.", this);
        foreach (MonoBehaviour item in result)
            Debug.Log("[Evaluation] Legacy candidate: " + item.GetType().Name + " on " + item.name, item);
    }

    public bool LegacyScanCompleted { get { return legacyScanCompleted; } }
#endif
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(MillikanEvaluationDialogue))]
public class MillikanEvaluationDialogueInspector : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var controller = (MillikanEvaluationDialogue)target;
        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.HelpBox(
            "Set Order on the original EvaluationController before Play. Part B and Part C use that same order. " +
            "Play and Build capture it automatically; the old controller's behaviour is disabled during evaluation.",
            UnityEditor.MessageType.Info);
        UnityEditor.EditorGUILayout.HelpBox(
            "Before Play/build: scan, review the legacy list, and save the scene. Only one evaluation controller " +
            "may own A/B, 1/3/5 and the droplet. The scan cannot inspect dynamically bound InputActions.",
            controller.LegacyScanCompleted ? UnityEditor.MessageType.Info : UnityEditor.MessageType.Warning);
        if (GUILayout.Button("Scan Legacy Controllers")) controller.ScanLegacyControllersInEditor();
    }
}

public class MillikanEvaluationOrderBuildProcessor : UnityEditor.Build.IProcessSceneWithReport
{
    public int callbackOrder { get { return 1000; } }

    public void OnProcessScene(Scene scene, UnityEditor.Build.Reporting.BuildReport report)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MillikanEvaluationDialogue controller in root.GetComponentsInChildren<MillikanEvaluationDialogue>(true))
            {
                if (!controller.enabled) continue;
                if (!controller.CaptureOrderInEditor(out string error))
                    throw new UnityEditor.Build.BuildFailedException("[Evaluation] " + error);
            }
    }
}
#endif
