using UnityEngine;

namespace Fusion.Addons.ScreenSharing
{
    /**
     * Forward a UwcWindowTexture to an UI image
     */
    public class UwcImage : MonoBehaviour
    {
#if U_WINDOW_CAPTURE_RECORDER_ENABLE
        public int desktopIndex = 0;
        public uWindowCapture.UwcWindowTexture uwcTexture;
        public UnityEngine.UI.Image image;
        Renderer uwcTextureRenderer;
        public bool hideTextureRenderer = true;
        public int framesBeforeReady = 30;
        int framesSinceMaterialSet = 0;

        private void Awake()
        {
            if (image == null) image = GetComponent<UnityEngine.UI.Image>();
            if (uwcTexture == null) uwcTexture = GetComponentInChildren<uWindowCapture.UwcWindowTexture>();
            if (uwcTexture == null)
            {
                Debug.LogError("Ou have to provide a UwcWindowTexture component, on a GameObject with a renderer having the uwcUnlit material");
            }
            uwcTextureRenderer = uwcTexture.GetComponent<Renderer>();
        }

        enum Status
        {
            Starting,
            Registered,
            MaterialSet,
            Ready
        }

        Status status = Status.Starting;

        private void Update()
        {
            Register();
            if (desktopIndex != uwcTexture.desktopIndex)
            {
                uwcTexture.desktopIndex = desktopIndex;
                image.SetAllDirty();
            }
        }

        private void OnDestroy()
        {
            if (uwcTexture && uwcTexture.window != null)
                uwcTexture.window.onCaptured.RemoveListener(OnCaptured);
        }

        private void LateUpdate()
        {
            if (hideTextureRenderer) uwcTextureRenderer.enabled = false;
        }

        void Register()
        {
            if (status != Status.Starting) return;
            if (uwcTexture.window == null) return;
            Debug.Log("Registering uwc Texture");
            status = Status.Registered;
            uwcTexture.window.onCaptured.AddListener(OnCaptured);
        }


        private void OnCaptured()
        {
            if (status == Status.MaterialSet)
            {
                if (framesSinceMaterialSet < framesBeforeReady)
                {
                    framesSinceMaterialSet++;
                }
                else
                {
                    Debug.Log("OnCaptured : Ready");
                    status = Status.Ready;
                    ForceImageRefresh();
                }
            }
            if (status == Status.Registered)
            {
                Debug.Log("OnCaptured : Material set");
                status = Status.MaterialSet;
                image.material = uwcTextureRenderer.material;
                image.material.mainTextureScale = new Vector2(1, -1);
                framesSinceMaterialSet = 0;
            }
        }

        [ContextMenu("ForceImageRefresh")]
        void ForceImageRefresh()
        {
            image.SetAllDirty();

        }
#endif
    }
}
