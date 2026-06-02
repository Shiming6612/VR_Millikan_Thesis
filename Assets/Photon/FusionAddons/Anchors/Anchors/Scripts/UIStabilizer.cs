using Fusion.XR.Shared.Core;
using UnityEngine;

/// <summary>
/// UIStabilizer can be used to display an UI oriented to the user's headset
/// </summary>
public class UIStabilizer : MonoBehaviour
{
    public Transform reference;
    [SerializeField] GameObject informationPanel;
    [SerializeField] float rotationAngle = 180f;

    private void Awake()
    {
        if (reference == null) reference = transform.parent;
    }

    private void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRender;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    [BeforeRenderOrder(100_000)]
    private void OnBeforeRender()
    {
        Vector3 desiredUp = reference.up;
        var forward = reference.forward;

        bool referenceFacingDown = Vector3.Dot(reference.forward, Vector3.up) < 0;
        Vector3 userDirection = Vector3.forward;
        var hardwareRig = HardwareRigsRegistry.GetHardwareRig();

        if (hardwareRig == null || hardwareRig.Headset == null) return;
        userDirection = transform.position - hardwareRig.Headset.transform.position;


        var targetUp = userDirection + Vector3.up;
        if (Vector3.Cross(reference.forward, targetUp).magnitude < 0.01f)
        {
            // forward is colinear to Vector3.up, projection won't work
            desiredUp = Vector3.Project(reference.up, userDirection);

        }
        else
        {
            desiredUp = Vector3.ProjectOnPlane(targetUp, reference.forward);
        }

        transform.rotation = Quaternion.LookRotation(forward, desiredUp.normalized);

        if (informationPanel)
        {
            informationPanel.transform.LookAt(hardwareRig.Headset.transform.position);
            informationPanel.transform.Rotate(rotationAngle, rotationAngle, 0f);
        }
    }
}
