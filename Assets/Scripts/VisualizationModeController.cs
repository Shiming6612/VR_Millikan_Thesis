using UnityEngine;

public class VisualizationModeController : MonoBehaviour
{
    [Header("Variant 1: Force arrows")]
    public GameObject[] forceArrowObjects;

    [Header("Variant 2: Trail path")]
    public GameObject[] trailObjects;

    public void ShowForceArrowsOnly()
    {
        SetObjectsActive(forceArrowObjects, true);
        SetObjectsActive(trailObjects, false);
    }

    public void ShowTrailOnly()
    {
        SetObjectsActive(forceArrowObjects, false);
        SetObjectsActive(trailObjects, true);
    }

    public void ShowBoth()
    {
        SetObjectsActive(forceArrowObjects, true);
        SetObjectsActive(trailObjects, true);
    }

    public void HideAll()
    {
        SetObjectsActive(forceArrowObjects, false);
        SetObjectsActive(trailObjects, false);
    }

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        foreach (GameObject obj in objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }
}