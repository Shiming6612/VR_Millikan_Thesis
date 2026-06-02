namespace Fusion.XR.Shared.Core
{
    [System.Flags]
    public enum FeedbackMode
    {
        None = 0,
        Audio = 1,
        Haptic = 2,
        AudioAndHaptic = Audio | Haptic,
    }

    public interface IFeedbackHandler: IAudioFeedbackHandler, IHapticFeedbackHandler
    {
        public void PlayAudioAndHapticFeedback(string audioType = null, float hapticAmplitude = USE_DEFAULT_VALUES, float hapticDuration = USE_DEFAULT_VALUES, IHapticFeedbackProviderRigPart hardwareRigPart = null, FeedbackMode feedbackMode = FeedbackMode.AudioAndHaptic, bool audioOverwrite = true);

        public void StopAudioAndHapticFeedback(IHapticFeedbackProviderRigPart hardwareRigPart = null);

    }

    public interface IAudioFeedbackHandler
    {
        public void PlayAudioFeedback(string audioType = null);
        public void PauseAudioFeedback();
        public void StopAudioFeedback();
        public bool IsAudioFeedbackIsPlaying();
    }

    public interface IHapticFeedbackHandler
    {
        public const float USE_DEFAULT_VALUES = -1;
        public void PlayHapticFeedback(float hapticAmplitude = USE_DEFAULT_VALUES, IHapticFeedbackProviderRigPart hardwareRigPart = null, float hapticDuration = USE_DEFAULT_VALUES);
        public void StopHapticFeedback(IHapticFeedbackProviderRigPart hardwareRigPart = null);
    }

    public interface IHapticConsumer
    {
        public void HapticFeedback(IHapticFeedbackProviderRigPart hapticFeedbackProviderRigPart);
    }
}
