using Fusion.XR.Shared.Core;
using UnityEngine;


public class UISync_Toggle_AudioFeedback : MonoBehaviour
{
    [SerializeField] private UISync_Toggle networked_toggle;
    [SerializeField] IFeedbackHandler feedback;
    [SerializeField] string audioType;
    public bool playSoundWhenTouched = true;

    private void Awake()
    {
        if (networked_toggle == null)
        {
            networked_toggle = GetComponent<UISync_Toggle>();
        }

        if (networked_toggle == null)
        {
            Debug.LogError("UISync_Toggle not found");
        }
        networked_toggle.onToogleValueChanged.AddListener(OnToogleValueChanged);

        if (feedback == null)
        {
            feedback = GetComponentInParent<IFeedbackHandler>();
        }
    }

    private void OnToogleValueChanged()
    {
        if (playSoundWhenTouched && feedback != null && feedback.IsAudioFeedbackIsPlaying() == false)
            feedback.PlayAudioFeedback(audioType);
    }
}
