using UnityEngine;
using Fusion;
using Fusion.XR.Shared.Core;

public class DrumStick : NetworkBehaviour
{
    [SerializeField] private IFeedbackHandler feedback;
    [SerializeField] private string DrumStickSound = "DrumStick";

    private void Awake()
    {
        if (feedback == null)
            feedback = GetComponent<IFeedbackHandler>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Object && Object.HasStateAuthority == false)
            return;

        // Not feedback when the stick is grabbed by a hand
        if (other.gameObject.GetComponentInParent<IRigPart>() != null)
            return;

        // Create local feedbacks when the drumstick collides with an object
        if (feedback != null)
        {
            // Audio & Haptic feedback
            feedback.PlayAudioAndHapticFeedback(DrumStickSound);
        }

        // Play the drum's sound if the drumstick collides with a drum pad for all players thanks to RPC
        Drum drum = other.GetComponent<Drum>();
        if (drum)
        {
            drum.PlayDrumSound();
        }
    }
}
