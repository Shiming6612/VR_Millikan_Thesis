using UnityEngine;

public class VisualizationModeController : MonoBehaviour
{
    [Header("Visualization Switches")]
    public bool showForceArrows = true;
    public bool showTrailPath = false;
    public bool showFieldCloud = false;

    [Header("Force Arrows")]
    public GameObject[] forceArrowObjects;

    [Header("Field Cloud")]
    public FieldParticleCloudVisualizer fieldCloud;

    public bool TrailEnabled => showTrailPath;
    public bool ForceArrowsEnabled => showForceArrows;
    public bool FieldCloudEnabled => showFieldCloud;

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

        if (!showTrailPath)
            HideAllTrails();

        ApplyVisualizationSwitches();
    }

    public void SetFieldCloudVisible(bool visible)
    {
        showFieldCloud = visible;
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

    private void SetArrowVisibility(bool visible)
    {
        if (forceArrowObjects == null)
            return;

        foreach (GameObject obj in forceArrowObjects)
        {
            if (obj == null)
                continue;

            obj.SetActive(visible);

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
                renderer.enabled = visible;

            LineRenderer[] lineRenderers = obj.GetComponentsInChildren<LineRenderer>(true);
            foreach (LineRenderer lineRenderer in lineRenderers)
                lineRenderer.enabled = visible;
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