using UnityEngine;
using Fusion;
using UnityEngine.UI;

public class UISync_Core : NetworkBehaviour, IStateAuthorityChanged
{
    [Header("UISync_Core")]

    [SerializeField] protected Selectable selectable;
    [SerializeField] protected int pressVisualFeedbackDuration = 150;

    public bool disableInteractionWhenNotStateAuthority = false;
    private bool coreIsInitialized = false;

    [Header("Selectable")]
    [Tooltip("Make sure that selectable is true when we have State Authority")]
    [SerializeField] bool syncSelectableWithStateAuthority = true;

    protected virtual void Awake()
    {
        if (selectable == null)
        {
            selectable = GetComponent<Selectable>();
        }
        if (selectable == null)
        {
            Debug.LogError("Selectable not found");
        }
    }

   public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (coreIsInitialized == false)
        {
            ConfigureSelectableInteractionBasedOnStateAuthority();
            coreIsInitialized = true;
        }
    }
    public virtual void StateAuthorityChanged()
    {
        ConfigureSelectableInteractionBasedOnStateAuthority();
    }

    protected void ConfigureSelectableInteractionBasedOnStateAuthority()
    {
        if (selectable && syncSelectableWithStateAuthority)
        {
            if (Object.HasStateAuthority)
            {
                if (selectable.interactable == false)
                {
                    selectable.interactable = true;
                }
            }
            else
            {
                if (selectable.interactable && disableInteractionWhenNotStateAuthority == true)
                {
                    selectable.interactable = false;
                }
            }
        }
    }
}
