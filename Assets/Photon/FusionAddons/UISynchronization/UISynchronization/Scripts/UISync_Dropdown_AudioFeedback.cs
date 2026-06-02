using Fusion.XR.Shared.Core;
using UnityEngine;


public class UISync_Dropdown_AudioFeedback : MonoBehaviour
{
    [SerializeField] private UISync_Dropdown networked_dropdown;
    [SerializeField] IFeedbackHandler feedback;
    [SerializeField] string audioType;
    public bool playSoundWhenTouched = true;

    private void Awake()
    {
        if (networked_dropdown == null)
        {
            networked_dropdown = GetComponent<UISync_Dropdown>();
        }

        if (networked_dropdown == null)
        {
            Debug.LogError("UISync_Dropdown not found");
        }
        networked_dropdown.onDropdownValueChanged.AddListener(OnDropdownValueChanged);

        if (feedback == null)
        {
            feedback = GetComponentInParent<IFeedbackHandler>();
        }
    }

    private void OnDropdownValueChanged()
    {
        if (playSoundWhenTouched && feedback != null && feedback.IsAudioFeedbackIsPlaying() == false)
            feedback.PlayAudioFeedback(audioType);
    }
}
