using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LegendUIController : MonoBehaviour
{
    [Header("Refs")]
    public DropSelectionManager selectionManager;
    public VoltageKnobInput voltageSource;
    public ElectricFieldVolume fieldVolume;

    [Header("UI")]
    public CanvasGroup panelGroup;
    public TMP_Text titleText;
    public TMP_Text massText;
    public TMP_Text chargeText;
    public TMP_Text radiusText;
    public TMP_Text voltageText;
    public TMP_Text hintText;

    [Header("Correct State")]
    public float toleranceV = 1f;
    public Color correctColor = Color.green;
    public float correctFontSizeMultiplier = 1.25f;
    public AudioSource correctSfxSource;
    public AudioClip correctSfx;

    [Header("Tutorial")]
    public float tutorialHoldCorrectSeconds = 1f;

    [Header("Debug")]
    public bool logDebug = false;

    private SelectableDrop lastSelected;
    private BottomTutorialController tutorialController;

    private bool wasCorrect;
    private bool tutorialSolvedSent;
    private float correctHoldTimer;

    private Color baseColor;
    private float baseSize;
    private FontStyles baseStyle;
    private bool cached;

    private readonly Dictionary<SelectableDrop, int> runtimeIds = new Dictionary<SelectableDrop, int>();
    private int nextRuntimeId = 1;

    private void Awake()
    {
        tutorialController = FindFirstObjectByType<BottomTutorialController>();
    }

    private void OnEnable()
    {
        if (selectionManager != null)
            selectionManager.OnSelectionChanged += HandleSelectionChanged;

        CacheBaseStyle();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (selectionManager != null)
            selectionManager.OnSelectionChanged -= HandleSelectionChanged;
    }

    private void Update()
    {
        SelectableDrop selected = selectionManager != null ? selectionManager.CurrentSelected : null;

        if (selected != lastSelected)
        {
            lastSelected = selected;
            RefreshAll();
        }

        RefreshVoltage(selected);
    }

    private void HandleSelectionChanged(SelectableDrop selected)
    {
        lastSelected = selected;
        RefreshAll();
    }

    private void RefreshAll()
    {
        SelectableDrop selected = selectionManager != null ? selectionManager.CurrentSelected : null;

        SetPanel(true);

        correctHoldTimer = 0f;
        tutorialSolvedSent = false;
        wasCorrect = false;

        if (selected == null)
        {
            if (titleText != null) titleText.text = "Drop--";
            if (massText != null) massText.text = "Mass: --";
            if (chargeText != null) chargeText.text = "Charge: --";
            if (radiusText != null) radiusText.text = "Radius: --";
            if (hintText != null) hintText.text = "";

            RestoreStyle();
            RefreshVoltage(null);
            return;
        }

        DropProperties dp = FindDropProperties(selected);

        if (titleText != null)
            titleText.text = "Drop" + GetDisplayId(selected).ToString("00");

        if (dp != null)
        {
            if (massText != null)
                massText.text = "Mass: " + (dp.MassKg * 1e15f).ToString("0.000") + " pg";

            if (chargeText != null)
                chargeText.text = "Charge: " + dp.ChargeMultiple + " e";

            if (radiusText != null)
                radiusText.text = "Radius: " + dp.RadiusMicrometer.ToString("0.00") + " µm";
        }
        else
        {
            if (massText != null) massText.text = "Mass: --";
            if (chargeText != null) chargeText.text = "Charge: --";
            if (radiusText != null) radiusText.text = "Radius: --";
        }

        RestoreStyle();
        RefreshVoltage(selected);
    }

    private void RefreshVoltage(SelectableDrop selected)
    {
        CacheBaseStyle();

        float currentVoltage = voltageSource != null ? voltageSource.CurrentVoltage : 0f;

        if (fieldVolume != null && fieldVolume.invertVoltage)
            currentVoltage = -currentVoltage;

        float currentRounded = RoundToOneDecimal(Mathf.Abs(currentVoltage));

        float hoverVoltage = 0f;
        bool canCalculate = selected != null && TryHoverVoltage(selected, out hoverVoltage);
        float hoverRounded = RoundToOneDecimal(hoverVoltage);

        if (voltageText != null)
        {
            voltageText.text = voltageSource != null
                ? "Voltage: " + currentRounded.ToString("0.0") + " V"
                : "Voltage: --";
        }

        bool correct =
            canCalculate &&
            voltageSource != null &&
            Mathf.Abs(currentRounded - hoverRounded) <= toleranceV;

        if (correct)
            ApplyCorrectStyle();
        else
            RestoreStyle();

        if (correct && !wasCorrect)
        {
            if (correctSfxSource != null && correctSfx != null)
                correctSfxSource.PlayOneShot(correctSfx);

            if (logDebug)
                Debug.Log("[LegendUI] Correct: " + currentRounded + " V / Hover: " + hoverRounded + " V");
        }

        if (correct)
        {
            correctHoldTimer += Time.deltaTime;

            if (!tutorialSolvedSent && correctHoldTimer >= tutorialHoldCorrectSeconds)
            {
                if (tutorialController == null)
                    tutorialController = FindFirstObjectByType<BottomTutorialController>();

                if (tutorialController != null)
                    tutorialController.NotifyVoltageSolved();

                tutorialSolvedSent = true;
            }
        }
        else
        {
            correctHoldTimer = 0f;
            tutorialSolvedSent = false;
        }

        wasCorrect = correct;

        if (hintText != null)
        {
            if (!canCalculate || voltageSource == null)
                hintText.text = "";
            else if (currentRounded > hoverRounded + toleranceV)
                hintText.text = "Status: Steigt";
            else if (currentRounded < hoverRounded - toleranceV)
                hintText.text = "Status: Fällt";
            else
                hintText.text = "Status: Schwebt";
        }
    }

    private bool TryHoverVoltage(SelectableDrop selected, out float hoverVoltage)
    {
        hoverVoltage = 0f;

        if (fieldVolume == null)
            return false;

        DropProperties dp = FindDropProperties(selected);

        if (dp == null)
            return false;

        float mass = Mathf.Max(1e-18f, dp.MassKg);
        float charge = Mathf.Abs(dp.ChargeC);

        if (charge < 1e-20f)
            return false;

        float d = fieldVolume.GetPlateSpacingMeters();

        if (d <= 1e-6f)
            return false;

        Vector3 dir = fieldVolume.fieldDirection.sqrMagnitude > 1e-6f
            ? fieldVolume.fieldDirection.normalized
            : Vector3.up;

        Vector3 gravity = GetGravityVector(selected);
        float g = Mathf.Abs(Vector3.Dot(gravity, dir));

        if (g <= 1e-6f)
            return false;

        float scale = Mathf.Max(1e-6f, fieldVolume.fieldScale);

        // PDF formula:
        // F_el = F_G
        // q * U / d = m * g
        // U = m * g * d / q
        hoverVoltage = (mass * g * d) / (charge * scale);

        return hoverVoltage > 0f;
    }

    private Vector3 GetGravityVector(SelectableDrop selected)
    {
        Vector3 gravity = Physics.gravity;

        if (selected == null)
            return gravity;

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
        if (selected == null)
            return null;

        DropProperties dp = selected.GetComponent<DropProperties>();

        if (dp == null)
            dp = selected.GetComponentInParent<DropProperties>();

        if (dp == null)
            dp = selected.GetComponentInChildren<DropProperties>();

        return dp;
    }

    private int GetDisplayId(SelectableDrop selected)
    {
        if (selected == null)
            return -1;

        if (selected.dropId >= 0)
            return selected.dropId + 1;

        if (runtimeIds.TryGetValue(selected, out int id))
            return id;

        id = nextRuntimeId++;
        runtimeIds[selected] = id;

        return id;
    }

    private float RoundToOneDecimal(float value)
    {
        return Mathf.Round(value * 10f) / 10f;
    }

    private void SetPanel(bool on)
    {
        if (panelGroup == null)
            return;

        panelGroup.alpha = on ? 1f : 0f;
        panelGroup.interactable = on;
        panelGroup.blocksRaycasts = on;
    }

    private void CacheBaseStyle()
    {
        if (cached || voltageText == null)
            return;

        baseColor = voltageText.color;
        baseSize = voltageText.fontSize;
        baseStyle = voltageText.fontStyle;
        cached = true;
    }

    private void ApplyCorrectStyle()
    {
        if (voltageText == null)
            return;

        voltageText.color = correctColor;
        voltageText.fontStyle = baseStyle | FontStyles.Bold;
        voltageText.fontSize = baseSize * Mathf.Max(1f, correctFontSizeMultiplier);
    }

    private void RestoreStyle()
    {
        if (voltageText == null || !cached)
            return;

        voltageText.color = baseColor;
        voltageText.fontStyle = baseStyle;
        voltageText.fontSize = baseSize;
    }
}