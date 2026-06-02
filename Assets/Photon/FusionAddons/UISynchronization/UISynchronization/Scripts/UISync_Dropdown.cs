using UnityEngine;
using Fusion;
using UnityEngine.UI;
using Fusion.XR.Shared.Core;
using UnityEngine.Events;
using UnityEngine.EventSystems;


public class UISync_Dropdown : UISync_Core
{
    [Header("UISync_Dropdown")]

    [SerializeField] private Dropdown dropdown;

    [Networked, OnChangedRender(nameof(OnNetworkedDropdownValueChanged))]
    public int DropdownValue { get; set; } = 0;

    private bool dropdownIsInitialized = false;


    [Header("Event")]
    public UnityEvent onDropdownValueChanged = new UnityEvent();

    protected override void Awake()
    {
        base.Awake();
        if (dropdown == null)
        {
            dropdown = GetComponent<Dropdown>();
        }

        if (dropdown == null)
        {
            Debug.LogError("Dropdown not found");
        }
        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }


    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (dropdownIsInitialized == false)
        {
            UpdateDropdownUIComponentWithNetworkedValue();
            dropdownIsInitialized = true;
        }
    }
    private async void UpdateDropdownUIComponentWithNetworkedValue()
    {
        dropdown.SetValueWithoutNotify(DropdownValue);

        // click effect
        ExecuteEvents.Execute(dropdown.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
        await AsyncTask.Delay(pressVisualFeedbackDuration);
        ExecuteEvents.Execute(dropdown.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerUpHandler);
    }

    // OnDropdownValueChanged is called when the local user interacts with the dropdown
    private async void OnDropdownValueChanged(int index)
    {
        // The state authority inform proxies
        if (Object && Object.HasStateAuthority)
        {
            DropdownValue = dropdown.value;
        }
        else
        {
            // Take the state authority if proxies' interaction is allowed
            if (disableInteractionWhenNotStateAuthority == false)
            {
                await Object.WaitForStateAuthority();
                DropdownValue = dropdown.value;
            }
        }
    }

    // OnNetworkedDropdownValueChanged is called when the networked variable DropdownValue is updated by the StateAuthority
    private void OnNetworkedDropdownValueChanged()
    {
        if (Object && Object.HasStateAuthority == false)
        {
            UpdateDropdownUIComponentWithNetworkedValue();
        }

        // event 
        if (onDropdownValueChanged != null) onDropdownValueChanged.Invoke();
    }

    private void OnEnable()
    {
        if (dropdownIsInitialized)
        {
            UpdateDropdownUIComponentWithNetworkedValue();
        }
    }
}
