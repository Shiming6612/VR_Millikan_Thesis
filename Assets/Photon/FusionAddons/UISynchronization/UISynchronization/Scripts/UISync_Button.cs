using UnityEngine;
using Fusion;
using Fusion.XR.Shared.Core;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class UISync_Button : UISync_Core
{
    [Header("UISync_Button")]
    [SerializeField] private Button button;

    [Networked, OnChangedRender(nameof(OnNetworkedButtonClickValueChanged))]
    public int ButtonClickValue { get; set; } = 0;

    private int _buttonClickValue = 0;
    private bool buttonIsInitialized = false;


    [Header("Event")]
    public UnityEvent onButtonTouched = new UnityEvent();

    protected override void Awake()
    {
        base.Awake();
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button == null)
        {
            Debug.LogError("button not found");
        }
        button.onClick.AddListener(OnButtonClick);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (buttonIsInitialized == false)
        {
            UpdateButtonUIComponentWithNetworkedValue();
            buttonIsInitialized = true;
        }
    }

    // OnButtonClick is called when the local user interacts with the button
    private async void OnButtonClick()
    {
        // The state authority inform proxies of the button has been pressed
        if (Object && Object.HasStateAuthority)
        {
           _buttonClickValue += 1;
           ButtonClickValue = _buttonClickValue;
        }
        else
        {
            // Take the state authority if proxies' interaction is allowed
            if (disableInteractionWhenNotStateAuthority == false)
            {
                await Object.WaitForStateAuthority();
                _buttonClickValue += 1;
                ButtonClickValue = _buttonClickValue;
            }
        }
    }

    private async void UpdateButtonUIComponentWithNetworkedValue()
    {
        _buttonClickValue = ButtonClickValue;

        // click effect
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerDownHandler);
        await AsyncTask.Delay(pressVisualFeedbackDuration);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerUpHandler);
    }

    // OnNetworkedButtonClickValueChanged is called when the networked variable ButtonClickValue is updated by the StateAuthority
    private void OnNetworkedButtonClickValueChanged()
    {
        if (Object && Object.HasStateAuthority == false)
        {
            UpdateButtonUIComponentWithNetworkedValue();
        }

        // event 
        if (onButtonTouched != null) onButtonTouched.Invoke();
    }

    [EditorButton("SimulatePressButton")]
    public void SimulatePressButton()
    {
        button.onClick.Invoke();
    }
}
