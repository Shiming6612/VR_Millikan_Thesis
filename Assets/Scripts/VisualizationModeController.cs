using UnityEngine;

public class VisualizationModeController : MonoBehaviour
{
    [Header("Visualization Switches")]
    public bool showForceArrows = false;
    public bool showTrailPath = false;
    public bool showFieldCloud = false;

    [Header("Selection")]
    public DropSelectionManager selectionManager;

    [Header("Force Arrows")]
    public GameObject[] forceArrowObjects;

    [Header("Field Cloud")]
    public FieldParticleCloudVisualizer fieldCloud;

    public bool TrailEnabled => showTrailPath;
    public bool ForceArrowsEnabled => showForceArrows;
    public bool FieldCloudEnabled => showFieldCloud;

    private void Awake()
    {
        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<DropSelectionManager>();
    }

    private void Start()
    {
        ApplyVisualizationSwitches();
    }

    private void LateUpdate()
    {
        ApplyVisualizationSwitches();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            ApplyVisualizationSwitches();
    }

    public void SetForceArrowsVisible(bool visible)
    {
        showForceArrows = visible;
        ApplyVisualizationSwitches();
    }

    public void SetTrailPathVisible(bool visible)
    {
        showTrailPath = visible;

        if (showTrailPath)
            ShowTrailForCurrentSelection();
        else
            HideAllTrails();

        ApplyVisualizationSwitches();
    }

    public void SetFieldCloudVisible(bool visible)
    {
        showFieldCloud = visible;
        ApplyVisualizationSwitches();
    }

    public void ShowAllVisualizations()
    {
        showForceArrows = true;
        showTrailPath = true;
        showFieldCloud = true;

        ShowTrailForCurrentSelection();
        ApplyVisualizationSwitches();
    }

    public void HideAllVisualizations()
    {
        showForceArrows = false;
        showTrailPath = false;
        showFieldCloud = false;

        HideAllTrails();
        ApplyVisualizationSwitches();
    }

    public void ApplyVisualizationSwitches()
    {
        SetArrowVisibility(showForceArrows);

        if (!showTrailPath)
            HideAllTrails();

        if (fieldCloud != null)
            fieldCloud.SetVisualizationEnabled(showFieldCloud);
    }

    private void ShowTrailForCurrentSelection()
    {
        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<DropSelectionManager>();

        SelectableDrop selected =
            selectionManager != null ? selectionManager.CurrentSelected : null;

        if (selected == null)
            return;

        SelectedDropletTrail trail = selected.GetComponent<SelectedDropletTrail>();

        if (trail == null)
            trail = selected.GetComponentInChildren<SelectedDropletTrail>();

        if (trail == null)
            trail = selected.GetComponentInParent<SelectedDropletTrail>();

        if (trail != null)
            trail.ShowTrail();
    }

    private void SetArrowVisibility(bool visible)
    {
        if (visible || forceArrowObjects == null)
            return;

        foreach (GameObject obj in forceArrowObjects)
        {
            if (obj == null)
                continue;

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
                renderer.enabled = false;
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
