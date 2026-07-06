using System.Reflection;
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

    private Material _matInstance;
    private Color _baseEmission;
    private bool _baseEmissionKeyword;
    private bool _isHovered;
    private bool _isSelected;

    private SelectedDropletTrail _trail;
    private FieldInfo _trailIsSelectedField;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        _trail = GetComponent<SelectedDropletTrail>();

        if (_trail != null)
        {
            _trailIsSelectedField = typeof(SelectedDropletTrail).GetField(
                "isSelected",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
        }

        if (targetRenderer != null)
        {
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
        UpdateTrailSelection(selected);
    }

    private void UpdateTrailSelection(bool selected)
    {
        if (_trail == null)
            return;

        if (_trailIsSelectedField != null)
            _trailIsSelectedField.SetValue(_trail, selected);

        _trail.gameObject.SendMessage(
            "SetSelected",
            selected,
            SendMessageOptions.DontRequireReceiver
        );
    }

    private void ApplyHighlight()
    {
        if (_matInstance == null || !useEmissionHighlight)
            return;

        bool on = _isSelected || _isHovered;

        if (on)
        {
            _matInstance.EnableKeyword("_EMISSION");
            Color c = highlightEmissionColor * Mathf.Max(0f, emissionIntensity);
            _matInstance.SetColor("_EmissionColor", c);
        }
        else
        {
            if (_baseEmissionKeyword)
                _matInstance.EnableKeyword("_EMISSION");
            else
                _matInstance.DisableKeyword("_EMISSION");

            if (_matInstance.HasProperty("_EmissionColor"))
                _matInstance.SetColor("_EmissionColor", _baseEmission);
        }
    }
}