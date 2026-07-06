using UnityEngine;

public class SelectableDrop : MonoBehaviour
{
    [Header("Render")]
    public Renderer targetRenderer;

    [Header("Highlight")]
    public bool useEmissionHighlight = true;
    public Color highlightEmissionColor = Color.yellow;
    [Range(0f, 10f)] public float emissionIntensity = 2f;

    [Header("Optional")]
    public int dropId = -1;

    Material _matInstance;
    Color _baseEmission;
    bool _baseEmissionKeyword;
    bool _isHovered;
    bool _isSelected;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
        {
            // Create per-instance material at runtime
            _matInstance = targetRenderer.material;

            _baseEmission = _matInstance.HasProperty("_EmissionColor")
                ? _matInstance.GetColor("_EmissionColor")
                : Color.black;

            _baseEmissionKeyword = _matInstance.IsKeywordEnabled("_EMISSION");
        }
    }

    public void SetHovered(bool hovered)
    {
        _isHovered = hovered;
        ApplyHighlight();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        ApplyHighlight();
    }

    void ApplyHighlight()
    {
        if (_matInstance == null || !useEmissionHighlight) return;

        bool on = _isSelected || _isHovered;

        if (on)
        {
            _matInstance.EnableKeyword("_EMISSION");
            Color c = highlightEmissionColor * Mathf.Max(0f, emissionIntensity);
            _matInstance.SetColor("_EmissionColor", c);
        }
        else
        {
            if (_baseEmissionKeyword) _matInstance.EnableKeyword("_EMISSION");
            else _matInstance.DisableKeyword("_EMISSION");

            if (_matInstance.HasProperty("_EmissionColor"))
                _matInstance.SetColor("_EmissionColor", _baseEmission);
        }
    }
}
