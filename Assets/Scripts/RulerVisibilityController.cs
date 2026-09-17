using UnityEngine;

public class RulerVisibilityController : MonoBehaviour
{
    [Header("Refs")]
    // Retained to preserve existing Inspector references.
    public DropSelectionManager dropSelectionManager;
    public Renderer targetRenderer;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        UpdateVisibility();
    }

    private void OnEnable()
    {
        UpdateVisibility();
    }

    private void Update()
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (targetRenderer != null)
            targetRenderer.enabled = true;
    }
}
