using UnityEngine;

public class Test_slider_event : MonoBehaviour
{

    [SerializeField] private UISync_Slider networked_slider;

    private void Awake()
    {
        if (networked_slider == null)
        {
            networked_slider = GetComponent<UISync_Slider>();
        }

        if (networked_slider == null)
        {
            Debug.LogError("UISync_Slider not found");
        }
        networked_slider.onSliderValueChanged.AddListener(OnSliderValueChanged);
    }

    private void OnSliderValueChanged()
    {
        Debug.Log("UISync_Slider change : " + networked_slider.SliderValue);
    }


}
