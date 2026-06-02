using Fusion.XR.Shared.Core;
using UnityEngine;


public class UISync_Button_AudioFeedback : MonoBehaviour
{
    [SerializeField] private UISync_Button networked_button;
    [SerializeField] IFeedbackHandler feedback;
    [SerializeField] string audioType;
    public bool playSoundWhenTouched = true;

    private void Awake()
    {
        if (networked_button == null)
        {
            networked_button = GetComponent<UISync_Button>();
        }

        if (networked_button == null)
        {
            Debug.LogError("UISync_Button not found");
        }
        networked_button.onButtonTouched.AddListener(OnButtonTouched);

        if (feedback == null)
        {
            feedback = GetComponentInParent<IFeedbackHandler>();
        }
    }

    private void OnButtonTouched()
    {
        if (playSoundWhenTouched && feedback != null && feedback.IsAudioFeedbackIsPlaying() == false)
            feedback.PlayAudioFeedback(audioType);
    }
}
