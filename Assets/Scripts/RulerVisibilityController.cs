using UnityEngine;

public class RulerVisibilityController : MonoBehaviour
{
    [Header("Refs")]
    public DropSelectionManager dropSelectionManager;
    public Renderer targetRenderer;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        UpdateVisibility();
    }

    private void Update()
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        bool shouldShow = false;

        if (dropSelectionManager != null)
            shouldShow = dropSelectionManager.selectionEnabled;

        if (targetRenderer != null)
            targetRenderer.enabled = shouldShow;
    }
}