using UnityEngine;

public class Test_dropdown_event : MonoBehaviour
{

    [SerializeField] private UISync_Dropdown networked_dropdown;

    private void Awake()
    {
        if (networked_dropdown == null)
        {
            networked_dropdown = GetComponent<UISync_Dropdown>();
        }

        if (networked_dropdown == null)
        {
            Debug.LogError("UISync_Dropdown not found");
        }
        networked_dropdown.onDropdownValueChanged.AddListener(OnDropdownValueChanged);
    }

    private void OnDropdownValueChanged()
    {
        Debug.Log("UISync_Dropdown changed: " + networked_dropdown.DropdownValue);
    }


}
