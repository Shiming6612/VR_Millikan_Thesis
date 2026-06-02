using UnityEngine;
using Fusion;
using Fusion.XR.Shared.Core;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class UISync_Slider : UISync_Core
{
    [Header("UISync_Slider")]

    [SerializeField] private Slider slider;
    [SerializeField] protected TMP_Text sliderTMP;

    [Networked, OnChangedRender(nameof(OnNetworkedSliderValueChanged))]
    public float SliderValue { get; set; } = 0.5f;

    [Header("Event")]
    public UnityEvent onSliderValueChanged = new UnityEvent();

    private bool sliderIsInitialized = false;


    protected override void Awake()
    {
        base.Awake();
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }

        if (slider == null)
        {
            Debug.LogError("slider not found");
        }
        slider.onValueChanged.AddListener(OnSliderValueChanged);

        if (sliderTMP == null)
        {
            Debug.LogError("sliderTMP not set");
        }
    }

  
    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if(sliderIsInitialized == false)
        {
            if (Object.HasStateAuthority)
            {
                SliderValue = slider.value;
            }
            UpdateSliderUIComponentWithNetworkedValue();
            sliderIsInitialized = true;
        }

    }

    // OnSliderValueChanged is called when the local user interacts with the slider
    private async void OnSliderValueChanged(float value)
    {
        // The state authority inform proxies of the new slider value
        if (Object && Object.HasStateAuthority)
        {
            SliderValue = slider.value;
            if (sliderTMP) sliderTMP.text = SliderValue.ToString("F1");
        }
        else
        {
            // Take the state authority if proxies' interaction is allowed
            if (disableInteractionWhenNotStateAuthority == false)
            {
                await Object.WaitForStateAuthority();
                SliderValue = slider.value;
                if (sliderTMP) sliderTMP.text = SliderValue.ToString("F1");
            }
        }
    }

    private void UpdateSliderUIComponentWithNetworkedValue()
    {
        slider.SetValueWithoutNotify(SliderValue);
        if(sliderTMP) sliderTMP.text = SliderValue.ToString("F1");

        // can not use click effect on slider
    }

    // OnNetworkedSliderValueChanged is called when the networked variable SliderValue is updated by the StateAuthority
    private void OnNetworkedSliderValueChanged()
    {
        if(Object && Object.HasStateAuthority == false)
        {
            UpdateSliderUIComponentWithNetworkedValue();
        }

        // event 
        if (onSliderValueChanged != null) onSliderValueChanged.Invoke();
    }

    private void OnEnable()
    {
        if(sliderIsInitialized)
        {
            UpdateSliderUIComponentWithNetworkedValue();
        }
    }
}
