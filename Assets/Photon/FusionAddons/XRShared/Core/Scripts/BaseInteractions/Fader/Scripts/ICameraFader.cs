using System.Collections;
using UnityEngine;

namespace Fusion.XR.Shared.Core.Interaction
{
    public interface ICameraFader
    {
        public const int USE_DEFAULT_DURATION = -1;
        public IEnumerator WaitBlinkDuration(float durationSpentIn = ICameraFader.USE_DEFAULT_DURATION);
        public IEnumerator FadeOut(float duration = ICameraFader.USE_DEFAULT_DURATION);
        public IEnumerator FadeIn(float duration = ICameraFader.USE_DEFAULT_DURATION);
    }

    public interface IFadeable: IUnityBehaviour {
        public ICameraFader Fader { get; set; }

    }
}
