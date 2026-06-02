using UnityEngine;

public class Test_button_event : MonoBehaviour
{

    [SerializeField] private UISync_Button networked_button;

    private void Awake()
    {
        if (networked_button == null)
        {
            networked_button = GetComponent<UISync_Button>();
        }

        if (networked_button == null)
        {
            Debug.LogError("UISync_Button not found");
        }
        networked_button.onButtonTouched.AddListener(OnButtonTouched);
    }

    private void OnButtonTouched()
    {
        Debug.Log("UISync_Button touched : " + networked_button.ButtonClickValue);
    }
}
