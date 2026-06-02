using Fusion.Addons.Hover;
using UnityEngine;

public class DemoHoverableTarget : MonoBehaviour
{
    public BeamHoverable hoverable;
     
    private void Awake()
    {
        hoverable = GetComponent<BeamHoverable>();
        hoverable.onBeamHoverStart.AddListener(HoverStart);
        hoverable.onBeamHoverEnd.AddListener(HoverEnd);
        hoverable.onBeamRelease.AddListener(HoverRelease);
    }

    private void HoverStart()
    {
        Debug.LogError($"[{name}] HoverStart");
    }

    private void HoverEnd()
    {
        Debug.LogError($"[{name}] HoverEnd");
    }

    private void HoverRelease()
    {
        Debug.LogError($"[{name}] HoverRelease");
    }
}
