using UnityEngine;
using Fusion;

public class UISync_CanvasGroup : NetworkBehaviour, IStateAuthorityChanged
{
    [SerializeField] private CanvasGroup canvasGroup;

    public bool disableInteractionWhenNotStateAuthority = false;
    public float alphaAppliedOnCanvasGroupForStateAuthority = 1f;
    public float alphaAppliedOnCanvasGroupForProxies = 0.6f;

    private bool canvasGroupIsInitialized = false;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            Debug.LogError("CanvasGroup not found");
        }
    }


    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (canvasGroupIsInitialized == false)
        {
            ConfigureSelectableInteractionBasedOnStateAuthority();
            canvasGroupIsInitialized = true;
        }
    }
    public void StateAuthorityChanged()
    {
        ConfigureSelectableInteractionBasedOnStateAuthority();
    }

    protected void ConfigureSelectableInteractionBasedOnStateAuthority()
    {
        if (canvasGroup)
        {
            if (Object.HasStateAuthority)
            {
                if (canvasGroup.interactable == false)
                {
                    canvasGroup.interactable = true;
                }
                canvasGroup.alpha = alphaAppliedOnCanvasGroupForStateAuthority;
            }
            else
            {
                if (canvasGroup.interactable && disableInteractionWhenNotStateAuthority == true)
                {
                    canvasGroup.interactable = false;
                }
                canvasGroup.alpha = alphaAppliedOnCanvasGroupForProxies;
            }
        }
    }
}
