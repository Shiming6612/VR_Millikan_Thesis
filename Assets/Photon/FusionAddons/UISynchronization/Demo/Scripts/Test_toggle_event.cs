using UnityEngine;

public class Test_toggle_event : MonoBehaviour
{

    [SerializeField] private UISync_Toggle networked_toggle;

    private void Awake()
    {
        if (networked_toggle == null)
        {
            networked_toggle = GetComponent<UISync_Toggle>();
        }

        if (networked_toggle == null)
        {
            Debug.LogError("UISync_Toggle not found");
        }
        networked_toggle.onToogleValueChanged.AddListener(OnToogleValueChanged);
    }

    private void OnToogleValueChanged()
    {
        Debug.Log("UISync_Toggle Changed : " + networked_toggle.ToggleIsOn);
    }


}
