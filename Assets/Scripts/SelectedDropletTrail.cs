using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class SelectedDropletTrail : MonoBehaviour
{
    private TrailRenderer trail;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();

        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
    }

    public void ShowTrail()
    {
        if (trail == null)
            return;

        trail.Clear();
        trail.emitting = true;
    }

    public void HideTrail()
    {
        if (trail == null)
            return;

        trail.emitting = false;
        trail.Clear();
    }
}