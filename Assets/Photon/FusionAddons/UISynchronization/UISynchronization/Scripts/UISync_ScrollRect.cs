using Fusion;
using Fusion.XR.Shared.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UISync_ScrollRect : NetworkBehaviour
{
    [Header("UISync_ScrollRect")]
    public bool disableInteractionWhenNotStateAuthority = false;

    [SerializeField] private ScrollRect scrollRect;

    [Networked, OnChangedRender(nameof(OnNetworkedScrollRectValueChanged))]
    public Vector2 ScrollRectPosition { get; set; }

    private bool scrollRectIsInitialized = false;

    [Header("Event")]
    public UnityEvent onScrollRectValueChanged = new UnityEvent();

    private void Awake()
    {
        if (scrollRect == null)
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        if (scrollRect == null)
        {
            Debug.LogError("ScrollRect not found");
        }
        scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
    }


    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (scrollRectIsInitialized == false)
        {
            if (Object.HasStateAuthority)
            {
                ScrollRectPosition = Vector2.one;
            }
            UpdateScrollRectUIComponentWithNetworkedValue();
            scrollRectIsInitialized = true;
        }
    }

    // OnScrollRectValueChanged is called when the local user interacts with the scroll view
    private async void OnScrollRectValueChanged(Vector2 normalizedPosition)
    {
        // The state authority inform proxies of the new scroll rect position
        if (Object && Object.HasStateAuthority)
        {
            ScrollRectPosition = normalizedPosition;
        }
        else
        {
            // Take the state authority if proxies' interaction is allowed
            if (disableInteractionWhenNotStateAuthority == false)
            {
                await Object.WaitForStateAuthority();
                ScrollRectPosition = normalizedPosition;
            }
        }
    }

    private void UpdateScrollRectUIComponentWithNetworkedValue()
    {
        scrollRect.normalizedPosition = ScrollRectPosition;
    }

    // OnNetworkedScrollRectValueChanged is called when the networked variable ScrollRectPosition is updated by the StateAuthority
    private void OnNetworkedScrollRectValueChanged()
    {
        if (Object && Object.HasStateAuthority == false)
        {
            UpdateScrollRectUIComponentWithNetworkedValue();
        }

        // event 
        if (onScrollRectValueChanged != null) onScrollRectValueChanged.Invoke();
    }

    private void OnEnable()
    {
        if (scrollRectIsInitialized)
        {
            UpdateScrollRectUIComponentWithNetworkedValue();
        }
    }
}
