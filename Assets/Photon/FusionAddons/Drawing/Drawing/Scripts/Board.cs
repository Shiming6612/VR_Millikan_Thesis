using UnityEngine;

namespace Fusion.Addons.Drawing
{
    /***
     * 
     *  Board is in charge to provide a mipmap texture so the board can be view without flickering effect 
     *  To do so, it provides an ActivateBoard() method that is used to update the cameraTexture for a limited duration (recordDuration).
     *  The camera is a children object and should be setup in order to record only the board surface (orthographic)
     * 
     ***/
    public class Board : NetworkBehaviour
    {
        Camera renderCamera;
        float recordEndTime = -1;
        float recordDuration = 1;
        bool shouldBoardsRecord;
        RenderTexture cameraTexture;

        private void Awake()
        {
            renderCamera = GetComponentInChildren<Camera>();
            var renderer = GetComponent<MeshRenderer>();
            cameraTexture = new RenderTexture(renderCamera.targetTexture.width, renderCamera.targetTexture.height, renderCamera.targetTexture.height, renderCamera.targetTexture.format);
            cameraTexture.useMipMap = true;
            renderCamera.targetTexture = cameraTexture;
            renderer.material.mainTexture = renderCamera.targetTexture;
            renderCamera.enabled = false;

        }
        private void OnDestroy()
        {
            cameraTexture.Release();
        }


        void Recording(bool state)
        {
            if (state)
            {
                renderCamera.Render();
            }
        }

        private void Update()
        {
            shouldBoardsRecord = Time.time < recordEndTime;
            Recording(state: shouldBoardsRecord);
        }

        private void Start()
        {
            ActivateBoard();
        }

        public override void Spawned()
        {
            base.Spawned();
            ActivateBoard();
        }

        public void ActivateBoard()
        {
            recordEndTime = recordDuration + Time.time;
        }
    }
}
