using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HistogramPanel : MonoBehaviour
{
    [Header("Root")]
    public GameObject histogramRoot;

    [Header("Bars")]
    public RectTransform[] barRoots;
    public RectTransform[] barFills;
    public TMP_Text[] barLabels;
    public TMP_Text[] countLabels;

    [Header("Layout")]
    public float groupWidth = 500f;
    public float groupHeight = 170f;
    public float barWidth = 45f;
    public float maxBarHeight = 95f;
    public float minVisibleBarHeight = 8f;
    public float labelY = -65f;
    public float fillBottomY = -35f;
    public float countOffsetY = 16f;

    [Header("Preview")]
    public bool previewInEditor = true;
    public int[] previewCounts = new int[] { 1, 2, 1, 0, 1 };

    [Header("Data")]
    public int maxBin = 5;

    private const float ElementaryCharge = 1.602176634e-19f;
    private readonly List<float> qOverEValues = new List<float>();

    public int MeasurementCount => qOverEValues.Count;

    private void Awake()
    {
        Hide();
        UpdateBars();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!previewInEditor)
            return;

        maxBin = Mathf.Max(1, maxBin);
        groupWidth = Mathf.Max(100f, groupWidth);
        groupHeight = Mathf.Max(80f, groupHeight);
        barWidth = Mathf.Max(5f, barWidth);
        maxBarHeight = Mathf.Max(10f, maxBarHeight);
        minVisibleBarHeight = Mathf.Clamp(minVisibleBarHeight, 0f, maxBarHeight);

        PreviewBarsInEditor();
    }
#endif

    public void Clear()
    {
        qOverEValues.Clear();
        UpdateBars();
    }

    public void AddMeasurement(float chargeCoulomb)
    {
        float qOverE = Mathf.Abs(chargeCoulomb) / ElementaryCharge;
        qOverEValues.Add(qOverE);
        UpdateBars();
    }

    public void Show()
    {
        if (histogramRoot != null)
            histogramRoot.SetActive(true);

        UpdateBars();
    }

    public void Hide()
    {
        if (histogramRoot != null)
            histogramRoot.SetActive(false);
    }

    private void UpdateBars()
    {
        int[] bins = new int[maxBin + 1];

        for (int i = 0; i < qOverEValues.Count; i++)
        {
            int n = Mathf.RoundToInt(qOverEValues[i]);
            n = Mathf.Clamp(n, 1, maxBin);
            bins[n]++;
        }

        ApplyBins(bins);
    }

    private void PreviewBarsInEditor()
    {
        int[] bins = new int[maxBin + 1];

        for (int i = 0; i < maxBin; i++)
        {
            int count = 0;

            if (previewCounts != null && i < previewCounts.Length)
                count = Mathf.Max(0, previewCounts[i]);

            bins[i + 1] = count;
        }

        ApplyBins(bins);
    }

    private void ApplyBins(int[] bins)
    {
        int highestCount = 1;

        for (int i = 1; i <= maxBin; i++)
            highestCount = Mathf.Max(highestCount, bins[i]);

        for (int i = 0; i < maxBin; i++)
        {
            int binIndex = i + 1;
            int count = bins[binIndex];
            float x = GetBarX(i);

            if (barRoots != null && i < barRoots.Length && barRoots[i] != null)
                SetupBarRoot(barRoots[i], x);

            if (barFills != null && i < barFills.Length && barFills[i] != null)
                SetupFill(barFills[i], count, highestCount);

            if (barLabels != null && i < barLabels.Length && barLabels[i] != null)
                SetupLabel(barLabels[i], binIndex);

            if (countLabels != null && i < countLabels.Length && countLabels[i] != null)
                SetupCount(countLabels[i], count, highestCount);
        }
    }

    private float GetBarX(int index)
    {
        if (maxBin <= 1)
            return 0f;

        float step = groupWidth / (maxBin - 1);
        return -groupWidth * 0.5f + step * index;
    }

    private void SetupBarRoot(RectTransform rt, float x)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(80f, groupHeight);
        rt.localScale = Vector3.one;
    }

    private void SetupFill(RectTransform fill, int count, int highestCount)
    {
        float height = 0f;

        if (count > 0)
            height = Mathf.Lerp(minVisibleBarHeight, maxBarHeight, count / (float)highestCount);

        fill.anchorMin = new Vector2(0.5f, 0.5f);
        fill.anchorMax = new Vector2(0.5f, 0.5f);
        fill.pivot = new Vector2(0.5f, 0f);
        fill.anchoredPosition = new Vector2(0f, fillBottomY);
        fill.sizeDelta = new Vector2(barWidth, height);
        fill.localScale = Vector3.one;

        Image img = fill.GetComponent<Image>();
        if (img != null)
            img.enabled = count > 0;
    }

    private void SetupLabel(TMP_Text label, int binIndex)
    {
        RectTransform rt = label.rectTransform;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, labelY);
        rt.sizeDelta = new Vector2(70f, 28f);
        rt.localScale = Vector3.one;

        label.text = binIndex + "e";
        label.alignment = TextAlignmentOptions.Center;
    }

    private void SetupCount(TMP_Text countText, int count, int highestCount)
    {
        RectTransform rt = countText.rectTransform;

        float height = 0f;

        if (count > 0)
            height = Mathf.Lerp(minVisibleBarHeight, maxBarHeight, count / (float)highestCount);

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, fillBottomY + height + countOffsetY);
        rt.sizeDelta = new Vector2(70f, 28f);
        rt.localScale = Vector3.one;

        countText.text = count.ToString();
        countText.alignment = TextAlignmentOptions.Center;
    }
}