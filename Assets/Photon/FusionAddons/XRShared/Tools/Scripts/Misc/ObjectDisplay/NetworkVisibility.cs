using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace Fusion.XR.Shared.Tools
{
    public class NetworkVisibility : NetworkBehaviour
    {
        [Networked, OnChangedRender(nameof(OnVisibilityChange))]
        public NetworkBool IsVisible { get; set; }

        public List<Renderer> renderers = new List<Renderer>();
        public List<Image> images = new List<Image>();
        public List<TextMeshProUGUI> tmpTexts = new List<TextMeshProUGUI>();

        private void Awake()
        {
            if (renderers == null || renderers.Count == 0)
            {
                renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
            }
            if (images == null || images.Count == 0)
            {
                images = new List<Image>(GetComponentsInChildren<Image>());
            }
            if (tmpTexts == null || tmpTexts.Count == 0)
            {
                tmpTexts = new List<TextMeshProUGUI>(GetComponentsInChildren<TextMeshProUGUI>());
            }
        }

        public override void Spawned()
        {
            base.Spawned();
            OnVisibilityChange();
        }

        private void OnVisibilityChange()
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.enabled != IsVisible)
                {
                    renderer.enabled = IsVisible;
                }
            }

            foreach (var image in images)
            {
                if (image != null && image.enabled != IsVisible)
                {
                    image.enabled = IsVisible;
                }
            }

            foreach (var text in tmpTexts)
            {
                if (text != null && text.enabled != IsVisible)
                {
                    text.enabled = IsVisible;
                }
            }
        }
    }
}
