using UnityEngine;
using UnityEngine.XR;

public class PressBulbTrigger : MonoBehaviour
{
    [Header("References")]
    public ExperimentController experimentController;
    public Renderer targetRenderer;

    [Header("Interactor Filter")]
    public string colliderNameA = "GrabbingCollider";
    public string colliderNameB = "ControllerGrabLocation";

    [Header("Controller Button")]
    public bool useGripButton = true;
    public bool useTriggerButton = false;

    [Header("Visual")]
    public bool highlightOnHover = false;
    public Color hoverColor = Color.yellow;
    public Color pressedColor = Color.red;

    private int insideCount = 0;
    private bool isPressed = false;
    private Color originalColor;

    private void Awake()
    {
        if (targetRenderer != null)
        {
            originalColor = targetRenderer.material.color;
        }
    }

    private void Update()
    {
        bool isInside = insideCount > 0;
        bool buttonHeld = isInside && IsControllerButtonPressed();

        // 只有“在按钮附近 + 按下手柄按钮”才算按下
        if (buttonHeld && !isPressed)
        {
            isPressed = true;

            if (targetRenderer != null)
                targetRenderer.material.color = pressedColor;

            if (experimentController != null)
                experimentController.BulbPressed();

            Debug.Log("PressBulb: PRESSED by controller button");
        }
        // 松开按钮或离开按钮区域时，算松开
        else if (!buttonHeld && isPressed)
        {
            isPressed = false;

            if (targetRenderer != null)
            {
                if (highlightOnHover && isInside)
                    targetRenderer.material.color = hoverColor;
                else
                    targetRenderer.material.color = originalColor;
            }

            if (experimentController != null)
                experimentController.BulbReleased();

            Debug.Log("PressBulb: RELEASED by controller button");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!MatchesInteractor(other))
            return;

        insideCount++;

        Debug.Log("PressBulb hover enter by: " + other.name);

        if (!isPressed && targetRenderer != null && highlightOnHover)
        {
            targetRenderer.material.color = hoverColor;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!MatchesInteractor(other))
            return;

        insideCount = Mathf.Max(insideCount - 1, 0);

        Debug.Log("PressBulb hover exit by: " + other.name);

        if (insideCount == 0 && !isPressed && targetRenderer != null)
        {
            targetRenderer.material.color = originalColor;
        }

        // 如果已经按着，但手柄离开了区域，也要算松开
        if (insideCount == 0 && isPressed)
        {
            isPressed = false;

            if (targetRenderer != null)
                targetRenderer.material.color = originalColor;

            if (experimentController != null)
                experimentController.BulbReleased();

            Debug.Log("PressBulb: RELEASED because interactor left trigger zone");
        }
    }

    private bool MatchesInteractor(Collider other)
    {
        return other.name.Contains(colliderNameA) || other.name.Contains(colliderNameB);
    }

    private bool IsControllerButtonPressed()
    {
        return ReadButton(XRNode.LeftHand) || ReadButton(XRNode.RightHand);
    }

    private bool ReadButton(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        bool pressed;

        if (useGripButton &&
            device.TryGetFeatureValue(CommonUsages.gripButton, out pressed) &&
            pressed)
        {
            return true;
        }

        if (useTriggerButton &&
            device.TryGetFeatureValue(CommonUsages.triggerButton, out pressed) &&
            pressed)
        {
            return true;
        }

        return false;
    }
}