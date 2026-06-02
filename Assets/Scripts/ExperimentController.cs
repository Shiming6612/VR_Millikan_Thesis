using System.Collections;
using UnityEngine;

public class ExperimentController : MonoBehaviour
{
    [Header("Shell")]
    public OuterShellToggle shellToggle;

    [Header("Spray")]
    public SpraySpawner spraySpawner;

    [Header("Field Volume")]
    public ElectricFieldVolume electricFieldVolume;

    [Header("Selection Ray")]
    public DropSelectionManager dropSelectionManager;

    [Header("Input")]
    public float longPressSeconds = 3f;

    private bool isHolding = false;
    private bool longPressTriggered = false;
    private Coroutine holdRoutine;

    private void OnEnable()
    {
        if (electricFieldVolume != null)
            electricFieldVolume.OnOccupiedStateChanged += HandleFieldOccupiedChanged;
    }

    private void OnDisable()
    {
        if (electricFieldVolume != null)
            electricFieldVolume.OnOccupiedStateChanged -= HandleFieldOccupiedChanged;
    }

    private void Start()
    {
        if (dropSelectionManager != null)
            dropSelectionManager.SetSelectionEnabled(false);
    }

    private void HandleFieldOccupiedChanged(bool hasDropsInside)
    {
        if (dropSelectionManager == null)
            return;

        if (hasDropsInside)
        {
            dropSelectionManager.SetSelectionEnabled(true);
        }
        else
        {
            dropSelectionManager.ClearSelectionAndHover();
            dropSelectionManager.SetSelectionEnabled(false);
        }
    }

    public void BulbPressed()
    {
        Bulb_Select();
    }

    public void BulbReleased()
    {
        Bulb_Unselect();
    }

    public void Bulb_Select()
    {
        isHolding = true;
        longPressTriggered = false;

        if (holdRoutine != null)
            StopCoroutine(holdRoutine);

        holdRoutine = StartCoroutine(LongPressWatcher());
    }

    public void Bulb_Unselect()
    {
        isHolding = false;

        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        if (longPressTriggered)
            return;

        if (shellToggle != null)
            shellToggle.SetCutaway(true);

        if (spraySpawner != null)
            spraySpawner.SprayOnce();

        // 不在这里开红射线
        // 只有真正有 OilDrop 进入 ElectricFieldVolume 后，才由事件开启
    }

    public void ResetExperiment()
    {
        if (shellToggle != null)
            shellToggle.SetCutaway(false);

        if (spraySpawner != null)
            spraySpawner.ResetAllDrops();

        if (dropSelectionManager != null)
        {
            dropSelectionManager.ClearSelectionAndHover();
            dropSelectionManager.SetSelectionEnabled(false);
        }
    }

    private IEnumerator LongPressWatcher()
    {
        float t = 0f;

        while (isHolding)
        {
            t += Time.deltaTime;

            if (t >= longPressSeconds)
            {
                longPressTriggered = true;
                ResetExperiment();
                yield break;
            }

            yield return null;
        }
    }
}