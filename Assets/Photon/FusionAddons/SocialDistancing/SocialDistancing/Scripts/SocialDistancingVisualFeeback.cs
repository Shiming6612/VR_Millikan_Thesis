using System.Collections;
using UnityEngine;

namespace Fusion.Addons.SocialDistancingAddon
{
    /**
     * 
     * SocialDistancingVisualFeeback is in charge to display the forbidden area arround the player
     *
     **/
    public class SocialDistancingVisualFeeback : MonoBehaviour
    {
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float visualEffectDuration = 0.5f;

        private void Awake()
        {
            if (!spriteRenderer)
                spriteRenderer = GetComponent<SpriteRenderer>();

            spriteRenderer.enabled = false;
        }

        float endTime;

        public enum Status
        {
            ProtectionDisabled,
            ProtectionEnabled,
        }

        Status status = Status.ProtectionDisabled;

        // DisplayForbiddenArea() is called by SocialDistancing() 
        public IEnumerator DisplayForbiddenArea()
        {
            // Gradually increase the alpha value of the sprite
            float duration = visualEffectDuration;
            float startTime = Time.time;
            endTime = startTime + duration;

            if (status == Status.ProtectionEnabled)
                yield break;

            spriteRenderer.enabled = true;

            status = Status.ProtectionEnabled;

            while (Time.time < endTime)
            {
                float progress = (Time.time - startTime) / duration;
                Color color = spriteRenderer.material.color;
                color.a = progress;
                spriteRenderer.material.color = color;
                yield return null;
            }

            status = Status.ProtectionDisabled;
            // Gradually decrease the alpha value of the sprite
            duration = visualEffectDuration;
            startTime = Time.time;
            endTime = startTime + duration;
            while (Time.time < endTime)
            {
                float progress = 1 - (Time.time - startTime) / duration;
                Color color = spriteRenderer.material.color;
                color.a = progress;
                spriteRenderer.material.color = color;
                yield return null;
                if (status == Status.ProtectionEnabled)
                    yield break;

            }

            // Reset the sprite's material and hide the sprite
            spriteRenderer.enabled = false;

        }
    }
}
