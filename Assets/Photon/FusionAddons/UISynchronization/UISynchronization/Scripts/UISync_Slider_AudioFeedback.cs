using Fusion.XR.Shared.Core;
using UnityEngine;


public class UISync_Slider_AudioFeedback : MonoBehaviour
{
    [SerializeField] private UISync_Slider networked_slider;
    [SerializeField] IFeedbackHandler feedback;
    [SerializeField] string audioType;
    public bool playSoundWhenTouched = true;

    private void Awake()
    {
        if (networked_slider == null)
        {
            networked_slider = GetComponent<UISync_Slider>();
        }

        if (networked_slider == null)
        {
            Debug.LogError("UISync_Slider not found");
        }
        networked_slider.onSliderValueChanged.AddListener(OnSliderValueChanged);

        if (feedback == null)
        {
            feedback = GetComponentInParent<IFeedbackHandler>();
        }
    }

    private void OnSliderValueChanged()
    {
        if (playSoundWhenTouched && feedback != null && feedback.IsAudioFeedbackIsPlaying() == false)
            feedback.PlayAudioFeedback(audioType);
    }
}
