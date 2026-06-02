using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// VR / XR-friendly color wheel.
/// Uses pointer events from the UI ray instead of Input.mousePosition.
/// Also updates an optional preview image / preview renderer in real time.
/// </summary>
public class ColorWheelControl : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Output Color")]
    public Color Selection;

    [Header("Preview")]
    [Tooltip("Optional UI image used as a simple avatar color preview.")]
    public Image previewImage;

    [Tooltip("Optional 3D preview renderer, e.g. a small capsule avatar.")]
    public Renderer previewRenderer;

    [Tooltip("If assigned, this material will be instantiated for the preview renderer.")]
    public Material previewMaterialTemplate;

    [Header("Selector Objects")]
    public RectTransform selectorOut;
    public RectTransform selectorIn;

    private float outer;
    private Vector2 inner;

    private bool dragOuter;
    private bool dragInner;

    private Material wheelMaterial;
    private Material previewMaterialInstance;

    private RectTransform rectTrans;
    private float halfSize;

    private void Start()
    {
        rectTrans = GetComponent<RectTransform>();

        // Keep the wheel square.
        rectTrans.sizeDelta = new Vector2(rectTrans.sizeDelta.x, rectTrans.sizeDelta.x);
        halfSize = rectTrans.sizeDelta.x / 2f;

        // Find selectors automatically if not assigned.
        if (selectorOut == null)
        {
            Transform t = transform.Find("Selector_Out");
            if (t != null) selectorOut = t.GetComponent<RectTransform>();
        }

        if (selectorIn == null)
        {
            Transform t = transform.Find("Selector_In");
            if (t != null) selectorIn = t.GetComponent<RectTransform>();
        }

        if (selectorOut != null)
            selectorOut.sizeDelta = rectTrans.sizeDelta / 20.0f;

        if (selectorIn != null)
            selectorIn.sizeDelta = rectTrans.sizeDelta / 20.0f;

        Image image = GetComponent<Image>();
        if (image != null)
            wheelMaterial = image.material;

        SetupPreviewMaterial();

        // Default selected color.
        Selection = Color.red;
        outer = 0f;
        inner = Vector2.zero;

        UpdateMaterial();
        UpdateColor();
        UpdateSelectors();
        UpdatePreview();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!TryGetLocalPoint(eventData, out Vector2 local))
            return;

        float dist = local.magnitude;
        float innerRing = halfSize - halfSize / 4f;

        if (dist <= halfSize && dist >= innerRing)
        {
            dragOuter = true;
            dragInner = false;

            UpdateOuterFromLocal(local);
            UpdateMaterial();
            UpdateColor();
            UpdateSelectors();
            UpdatePreview();
            return;
        }

        if (Mathf.Abs(local.x) <= halfSize / 2f && Mathf.Abs(local.y) <= halfSize / 2f)
        {
            dragInner = true;
            dragOuter = false;

            UpdateInnerFromLocal(local);
            UpdateColor();
            UpdateSelectors();
            UpdatePreview();
            return;
        }

        dragOuter = false;
        dragInner = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragOuter && !dragInner)
            return;

        if (!TryGetLocalPoint(eventData, out Vector2 local))
            return;

        if (dragOuter)
        {
            UpdateOuterFromLocal(local);
            UpdateMaterial();
            UpdateColor();
        }
        else if (dragInner)
        {
            UpdateInnerFromLocal(local);
            UpdateColor();
        }

        UpdateSelectors();
        UpdatePreview();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        dragOuter = false;
        dragInner = false;
    }

    private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTrans,
            eventData.position,
            eventData.pressEventCamera,
            out local
        );
    }

    private void UpdateOuterFromLocal(Vector2 local)
    {
        Vector2 dir = -local.normalized;
        outer = Mathf.Atan2(-dir.x, -dir.y);
    }

    private void UpdateInnerFromLocal(Vector2 local)
    {
        float x = Mathf.Clamp(local.x, -halfSize / 2f, halfSize / 2f);
        float y = Mathf.Clamp(local.y, -halfSize / 2f, halfSize / 2f);

        float nx = (-x + halfSize / 2f) / halfSize;
        float ny = (-y + halfSize / 2f) / halfSize;

        inner = new Vector2(Mathf.Clamp01(nx), Mathf.Clamp01(ny));
    }

    private void UpdateSelectors()
    {
        if (selectorOut != null)
        {
            selectorOut.localPosition = new Vector3(
                Mathf.Sin(outer) * halfSize * 0.85f,
                Mathf.Cos(outer) * halfSize * 0.85f,
                1f
            );
        }

        if (selectorIn != null)
        {
            selectorIn.localPosition = new Vector3(
                halfSize * 0.5f - inner.x * halfSize,
                halfSize * 0.5f - inner.y * halfSize,
                1f
            );
        }
    }

    private void UpdateMaterial()
    {
        if (wheelMaterial == null)
            return;

        Color c = GetHueColor();
        wheelMaterial.SetColor("_Color", c);
    }

    private void UpdateColor()
    {
        Color c = GetHueColor();

        c = Color.Lerp(c, Color.white, inner.x);
        c = Color.Lerp(c, Color.black, inner.y);

        Selection = c;
    }

    private Color GetHueColor()
    {
        Color c = Color.white;

        c.r = Mathf.Clamp(
            2 / Mathf.PI * Mathf.Asin(Mathf.Cos(outer)) * 1.5f + 0.5f,
            0,
            1
        );

        c.g = Mathf.Clamp(
            2 / Mathf.PI * Mathf.Asin(Mathf.Cos(2 * Mathf.PI * (1.0f / 3.0f) - outer)) * 1.5f + 0.5f,
            0,
            1
        );

        c.b = Mathf.Clamp(
            2 / Mathf.PI * Mathf.Asin(Mathf.Cos(2 * Mathf.PI * (2.0f / 3.0f) - outer)) * 1.5f + 0.5f,
            0,
            1
        );

        c.a = 1f;
        return c;
    }

    private void SetupPreviewMaterial()
    {
        if (previewRenderer == null)
            return;

        if (previewMaterialTemplate != null)
        {
            previewMaterialInstance = new Material(previewMaterialTemplate);
            previewRenderer.material = previewMaterialInstance;
        }
        else
        {
            previewMaterialInstance = previewRenderer.material;
        }
    }

    private void UpdatePreview()
    {
        if (previewImage != null)
            previewImage.color = Selection;

        if (previewMaterialInstance != null)
            previewMaterialInstance.color = Selection;
        else if (previewRenderer != null)
            previewRenderer.material.color = Selection;
    }

    public void PickColor(Color c)
    {
        float max = Mathf.Max(c.r, c.g, c.b);
        float min = Mathf.Min(c.r, c.g, c.b);

        float hue = Mathf.Atan2(
            Mathf.Sqrt(3) * (c.g - c.b),
            2 * c.r - c.g - c.b
        );

        float sat = 1 - min;
        if (Mathf.Approximately(max, min))
            sat = 0;

        outer = hue;
        inner.x = 1 - sat;
        inner.y = 1 - max;

        UpdateMaterial();
        UpdateColor();
        UpdateSelectors();
        UpdatePreview();
    }
}