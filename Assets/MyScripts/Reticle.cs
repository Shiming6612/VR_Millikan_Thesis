using UnityEngine;

public class OverlayReticleFollower : MonoBehaviour
{
    public RectTransform canvasRect;
    public RectTransform reticle;
    public Transform rayOrigin;
    public Camera centerEye;

    public float depthOffset = 0.001f;

    void LateUpdate()
    {
        if (!canvasRect || !reticle || !rayOrigin || !centerEye)
            return;

        Plane p = new Plane(canvasRect.forward, canvasRect.position);
        Ray r = new Ray(rayOrigin.position, rayOrigin.forward);

        if (!p.Raycast(r, out float d))
        {
            reticle.gameObject.SetActive(false);
            return;
        }

        Vector3 hit = r.GetPoint(d);
        hit += (centerEye.transform.position - hit).normalized * depthOffset;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(centerEye, hit);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screen, centerEye, out Vector2 local))
        {
            reticle.gameObject.SetActive(true);
            reticle.anchoredPosition = local;
        }
        else
        {
            reticle.gameObject.SetActive(false);
        }
    }
}
