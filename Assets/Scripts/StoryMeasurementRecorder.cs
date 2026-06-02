using UnityEngine;

public class StoryMeasurementRecorder : MonoBehaviour
{
    public HistogramPanel histogramPanel;

    public void ClearMeasurements()
    {
        if (histogramPanel != null)
            histogramPanel.Clear();
    }

    public void RecordSelectedDrop(DropSelectionManager selectionManager)
    {
        if (selectionManager == null || selectionManager.CurrentSelected == null)
            return;

        DropProperties dp = FindDropProperties(selectionManager.CurrentSelected);

        if (dp == null)
            return;

        if (histogramPanel != null)
            histogramPanel.AddMeasurement(Mathf.Abs(dp.ChargeC));
    }

    private DropProperties FindDropProperties(SelectableDrop selected)
    {
        if (selected == null)
            return null;

        DropProperties dp = selected.GetComponent<DropProperties>();

        if (dp == null)
            dp = selected.GetComponentInParent<DropProperties>();

        if (dp == null)
            dp = selected.GetComponentInChildren<DropProperties>();

        return dp;
    }
}