using Fusion.XR.Shared.Core;
using UnityEngine;


public class UISync_ScrollRect_AudioFeedback : MonoBehaviour
{
    [SerializeField] private UISync_ScrollRect networked_scrollRect;
    [SerializeField] IFeedbackHandler feedback;
    [SerializeField] string audioType;
    public bool playSoundWhenTouched = true;

    private void Awake()
    {
        if (networked_scrollRect == null)
        {
            networked_scrollRect = GetComponent<UISync_ScrollRect>();
        }

        if (networked_scrollRect == null)
        {
            Debug.LogError("UISync_ScrollRect not found");
        }
        networked_scrollRect.onScrollRectValueChanged.AddListener(OnScrollRectValueChanged);

        if (feedback == null)
        {
            feedback = GetComponentInParent<IFeedbackHandler>();
        }
    }

    private void OnScrollRectValueChanged()
    {
        if (playSoundWhenTouched && feedback != null && feedback.IsAudioFeedbackIsPlaying() == false)
            feedback.PlayAudioFeedback(audioType);
    }
}
