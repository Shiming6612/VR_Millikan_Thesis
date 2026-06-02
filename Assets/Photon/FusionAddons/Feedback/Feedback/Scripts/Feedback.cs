using UnityEngine;
using Fusion.XR.Shared.Core;

namespace Fusion.Addons.HapticAndAudioFeedback
{
    /***
     * 
     * Feedback manages the audio and haptic feedbacks for NetworkGrabbable
     * It provides methods to :
     *  - start/pause/stop playing audio feedback only
     *  - start playing audio and haptic feeback in the same time
     * If the audio source is not defined or not find on the object, Feedback uses the SoundManager audio source.
     * 
     ***/
    public class Feedback : MonoBehaviour, IFeedbackHandler
    {
        public bool EnableAudioFeedback = true;
        public bool EnableHapticFeedback = true;

        public AudioSource audioSource;
        private SoundManager soundManager;

        [Header("Haptic feedback")]
        public float defaultHapticAmplitude = 0.2f;
        public float defaultHapticDuration = 0.05f;

        INetworkGrabbable grabbable;
        public bool IsGrabbed => grabbable.IsGrabbed;
        public bool IsGrabbedByLocalPLayer => IsGrabbed && grabbable.CurrentGrabber.Object.StateAuthority == grabbable.CurrentGrabber.Object.Runner.LocalPlayer;

        private void Awake()
        {
            grabbable = GetComponent<INetworkGrabbable>();
        }

        void Start()
        {
            if (soundManager == null) soundManager = SoundManager.FindInstance();

            FindAudioSource();
        }

        private void FindAudioSource()
        {
            if (audioSource != null) return;

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource == null && soundManager)
                audioSource = soundManager.GetComponent<AudioSource>();
            if (audioSource == null)
                Debug.LogError("AudioSource not found");
        }
        IHapticFeedbackProviderRigPart GrabbingHand()
        {
            if (grabbable != null)
            {
                return grabbable.CurrentLocalGrabberHapticFeedbackProvider();
            }
            return null;
        }

        #region IFeedbackHandler
        public void PlayAudioAndHapticFeedback(string audioType = null, float hapticAmplitude = -1, float hapticDuration = -1, IHapticFeedbackProviderRigPart hardwareRigPart = null, FeedbackMode feedbackMode = FeedbackMode.AudioAndHaptic, bool audioOverwrite = true)
        {
            if ((feedbackMode & FeedbackMode.Audio) != 0)
            {
                if (IsAudioFeedbackIsPlaying() == false || audioOverwrite == true)
                    PlayAudioFeedback(audioType);
            }

            if ((feedbackMode & FeedbackMode.Haptic) != 0)
            {
                PlayHapticFeedback(hapticAmplitude, hardwareRigPart, hapticDuration);
            }
        }

        public void StopAudioAndHapticFeedback(IHapticFeedbackProviderRigPart hardwareRigPart = null)
        {
            StopAudioFeedback();
            StopHapticFeedback(hardwareRigPart);
        }
        #endregion

        #region IAudioFeedbackHandler
        public void PlayAudioFeedback(string audioType)
        {
            if (EnableAudioFeedback == false) return;

            if (audioSource && audioSource.isPlaying == false && soundManager)
                soundManager.Play(audioType, audioSource);
        }

        public void StopAudioFeedback()
        {
            if (audioSource && audioSource.isPlaying)
                audioSource.Stop();
        }

        public void PauseAudioFeedback()
        {
            if (audioSource && audioSource.isPlaying)
                audioSource.Pause();
        }

        public bool IsAudioFeedbackIsPlaying()
        {
            FindAudioSource();
            return audioSource && audioSource.isPlaying;
        }
        #endregion

        #region IHapticFeedbackHandler
        public void PlayHapticFeedback(float hapticAmplitude = -1, IHapticFeedbackProviderRigPart hardwareRigPart = null, float hapticDuration = -1)
        {
            if (hapticAmplitude == IFeedbackHandler.USE_DEFAULT_VALUES) hapticAmplitude = defaultHapticAmplitude;
            if (hapticDuration == IFeedbackHandler.USE_DEFAULT_VALUES) hapticDuration = defaultHapticDuration;
            if (hardwareRigPart == null)
            {
                hardwareRigPart = GrabbingHand();

            }
            if (EnableHapticFeedback == false || hardwareRigPart == null) return;
            hardwareRigPart.SendHapticImpulse(amplitude: hapticAmplitude, duration: hapticDuration);
        }

        public void StopHapticFeedback(IHapticFeedbackProviderRigPart hardwareRigPart = null)
        {
            if (hardwareRigPart == null) return;

            hardwareRigPart.StopHaptics();
        }
        #endregion
    }
}
