using Fusion.XR.Shared.Core.Touch;
using UnityEngine;

public class DemoTouchableToggleButtonTarget : MonoBehaviour
{
    TouchableButton touchableButton;

    private void Awake()
    {
        touchableButton = GetComponent<TouchableButton>();
        touchableButton.onStatusChanged.AddListener(OnStatusChanged);
    }

    private void OnStatusChanged(bool status)
    {
        Debug.LogError($"[{name}] OnStatusChanged {status}");
    }

}
