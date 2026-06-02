using Fusion.Addons.AudioChatBubble;
using System.Collections;
using UnityEngine;

public class ChatBubbleGraphicManagement : MonoBehaviour, IDynamicChatBubbleListener
{
    ChatBubble chatBubble;

    public Material lockMaterial;
    private Material defaultMaterial;

    [SerializeField]
    private MeshRenderer domeMeshRenderer;
    [SerializeField]
    private MeshRenderer domeInsideMeshRenderer;
    [SerializeField]
    private MeshRenderer ringMeshRenderer;

    [Header("Lock indicator")]
    public Material lockedIndicatorMaterial;
    private Material defaultIndicatorMaterial;
    public Renderer lockIndicatorRenderer;

    private void Awake()
    {
        if (chatBubble == null)
            chatBubble = GetComponent<ChatBubble>();
        if (chatBubble == null)
            Debug.LogError("ChatBubble not found");

        if (domeMeshRenderer) defaultMaterial = domeMeshRenderer.material;
        else if (ringMeshRenderer) defaultMaterial = ringMeshRenderer.material;

        if (lockIndicatorRenderer)
        {
            defaultIndicatorMaterial = lockIndicatorRenderer.material;
        }
    }

    private void Update()
    {
        UpdateLockDisplay();
    }

    // UpdateLockDisplay update the dome meshes materials according to seat availability in the zone
    public void UpdateLockDisplay()
    {
        // Return if not yet connected as we can not read network variable Islocked
        if (chatBubble.Object == null)
            return;

        // Check if the ChatBubble is full
        if (chatBubble.AcceptMoreMembers == false )
        {
            // The ChatBubble is full 
            // Update mesh renderers with lock material
            if (domeMeshRenderer) domeMeshRenderer.material = lockMaterial;
            if (domeInsideMeshRenderer) domeInsideMeshRenderer.material = lockMaterial;
            if (ringMeshRenderer) ringMeshRenderer.material = lockMaterial;
            if (lockIndicatorRenderer) lockIndicatorRenderer.material = lockedIndicatorMaterial;
        }
        else 
        {
            // The ChatBubble can accept player
            // restore defaults material
            if (domeMeshRenderer) domeMeshRenderer.material = defaultMaterial;
            if (domeInsideMeshRenderer) domeInsideMeshRenderer.material = defaultMaterial;
            if (ringMeshRenderer) ringMeshRenderer.material = defaultMaterial;
            if (lockIndicatorRenderer) lockIndicatorRenderer.material = defaultIndicatorMaterial;
        }
    }

    [Header("Dynamic ChatBubble")]
    public bool isDynamicChatBubble = false;
    public Transform dynamicChatBubbleVisual;
    float animatedScale = 1;
    float animatedScaleTarget = 1;
    const float step = 0.01f;
    const float animationDuration = 0.1f;


    [ContextMenu("EnableDynamicChatBubble")]
    public void EnableDynamicChatBubble() 
    {
        StartCoroutine(ChangesDynamicChatBubbleVisibility(true));
    }

    [ContextMenu("DisableDynamicChatBubble")]
    public void DisableDynamicChatBubble()
    {
        StartCoroutine(ChangesDynamicChatBubbleVisibility(false));
    }

    // ChangeChatBubbleVisibility is in charge of animating the chatBubble when it should appear or disappear
    public IEnumerator ChangesDynamicChatBubbleVisibility(bool visible)
    {
        float currentRequestTarget = visible ? 1 : 0;
        animatedScaleTarget = currentRequestTarget;

        // Check the ChatBubble type
        if (isDynamicChatBubble)
        {
            // check if the ChatBubble should be visible or not
            if (visible)
            {
                // ChatBubble should be visible


                // enable ChatBubble renderer
                ringMeshRenderer.enabled = true;

                // Animation to rescale the dome to 1
                while (animatedScale < 1 && animatedScaleTarget == 1)
                {
                    dynamicChatBubbleVisual.localScale = animatedScale * Vector3.one;
                    animatedScale = Mathf.Min(1, step + animatedScale);
                    yield return new WaitForSeconds(animationDuration * step);
                }
                if (animatedScaleTarget == 1) dynamicChatBubbleVisual.localScale = Vector3.one;
            }
            else
            {
                // ChatBubble should not be visible

                // enable ChatBubble renderer
                ringMeshRenderer.enabled = true;

                // Animation to rescale the dome to 0
                while (animatedScale > 0 && animatedScaleTarget == 0)
                {
                    dynamicChatBubbleVisual.localScale = animatedScale * Vector3.one;
                    animatedScale = Mathf.Max(0, animatedScale - step);
                    yield return new WaitForSeconds(animationDuration * step);
                }
                if (animatedScaleTarget == 1) dynamicChatBubbleVisual.localScale = Vector3.zero;
            }
        }
    }

    #region IDynamicChatBubbleListener
    public void Register(DynamicChatBubble bubble)
    {
        // Force the bubble to be visually closed at start
        animatedScale = 0;
        EnableDynamicChatBubble();
    }

    public void Unregister(DynamicChatBubble bubble)
    {
    }

    public void CancelingRemovingBubble(DynamicChatBubble bubble)
    {
        EnableDynamicChatBubble();
    }

    public void EvaluatingRemovingBubble(DynamicChatBubble bubble, float emptyBubbleConservationRemainingTime)
    {
        DisableDynamicChatBubble();
    }

    public void RemoveBubble(DynamicChatBubble bubble)
    {
        StopAllCoroutines();
        StartCoroutine(FinalShutdown(bubble));
    }

    IEnumerator FinalShutdown(DynamicChatBubble bubble)
    {
        yield return ChangesDynamicChatBubbleVisibility(false);
        bubble.Object.Runner.Despawn(bubble.Object);
    }

    #endregion
}
