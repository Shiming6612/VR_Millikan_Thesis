using UnityEngine;

public class VisualizationModeController : MonoBehaviour
{
    public enum VisualizationMode
    {
        ForceArrowsOnly,
        TrailPathOnly,
        Both,
        HideAll
    }

    [Header("Mode")]
    public VisualizationMode currentMode = VisualizationMode.ForceArrowsOnly;

    [Header("Force Arrows")]
    public GameObject[] forceArrowObjects;

    public bool TrailEnabled =>
        currentMode == VisualizationMode.TrailPathOnly ||
        currentMode == VisualizationMode.Both;

    private void Start()
    {
        ApplyMode();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            ApplyMode();
    }

    public void SetForceArrowsOnly()
    {
        currentMode = VisualizationMode.ForceArrowsOnly;
        ApplyMode();
    }

    public void SetTrailPathOnly()
    {
        currentMode = VisualizationMode.TrailPathOnly;
        ApplyMode();
    }

    public void SetBoth()
    {
        currentMode = VisualizationMode.Both;
        ApplyMode();
    }

    public void SetHideAll()
    {
        currentMode = VisualizationMode.HideAll;
        ApplyMode();
    }

    public void ApplyMode()
    {
        bool showArrows =
            currentMode == VisualizationMode.ForceArrowsOnly ||
            currentMode == VisualizationMode.Both;

        SetActive(forceArrowObjects, showArrows);

        if (!TrailEnabled)
            HideAllTrails();
    }

    private void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        foreach (GameObject obj in objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }

    private void HideAllTrails()
    {
        SelectedDropletTrail[] trails =
            FindObjectsByType<SelectedDropletTrail>(FindObjectsSortMode.None);

        foreach (SelectedDropletTrail trail in trails)
        {
            if (trail != null)
                trail.HideTrail();
        }
    }
}