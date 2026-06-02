using Fusion;
using Fusion.XR.Shared.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UISync_Toggle : UISync_Core
{
    [Header("UISync_Toggle")]

    [SerializeField] private Toggle toggle;

    [Networked, OnChangedRender(nameof(OnNetworkedToggleValueChanged))]
    public NetworkBool ToggleIsOn { get; set; }

    [Header("Event")]
    public UnityEvent onToogleValueChanged = new UnityEvent();

    private bool toggleIsInitialized = false;

    protected override void Awake()
    {
        base.Awake();
        if (toggle == null)
        {
            toggle = GetComponent<Toggle>();
        }

        if (toggle == null)
        {
            Debug.LogError("Toggle not found");
        }
        toggle.onValueChanged.AddListener(OnToggleValueChanged);
    }


    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (toggleIsInitialized == false)
        {
            UpdateToggleUIComponentWithNetworkedValue();
            toggleIsInitialized = true;
        }
    }

    private void UpdateToggleUIComponentWithNetworkedValue()
    {
        toggle.SetIsOnWithoutNotify(ToggleIsOn);

        // can not use click effect on toggle
    }

    // OnToggleValueChanged is called when the local user interacts with the toggle
    private async void OnToggleValueChanged(bool change)
    {
        // The state authority inform proxies
        if (Object && Object.HasStateAuthority)
        {
            ToggleIsOn = toggle.isOn;
        }
        else
        {
            // Take the state authority if proxies' interaction is allowed
            if (disableInteractionWhenNotStateAuthority == false)
            {
                await Object.WaitForStateAuthority();
                ToggleIsOn = toggle.isOn;
            }
        }
    }

    // OnNetworkedToggleValueChanged is called when the networked variable ToggleIsOn is updated by the StateAuthority
    private void OnNetworkedToggleValueChanged()
    {
        if (Object && Object.HasStateAuthority == false)
        {
            UpdateToggleUIComponentWithNetworkedValue();
        }

        // event 
        if (onToogleValueChanged != null) onToogleValueChanged.Invoke();
    }
    private void OnEnable()
    {
        if (toggleIsInitialized)
        {
            UpdateToggleUIComponentWithNetworkedValue();
        }
    }
}
