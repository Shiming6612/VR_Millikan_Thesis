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

    public bool ForceArrowsEnabled =>
        currentMode == VisualizationMode.ForceArrowsOnly ||
        currentMode == VisualizationMode.Both;

    private void Start()
    {
        ApplyMode();
    }

    private void LateUpdate()
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
        SetArrowVisibility(ForceArrowsEnabled);

        if (!TrailEnabled)
            HideAllTrails();
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
            foreach (Renderer r in renderers)
                r.enabled = visible;

            LineRenderer[] lines = obj.GetComponentsInChildren<LineRenderer>(true);
            foreach (LineRenderer line in lines)
                line.enabled = visible;
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