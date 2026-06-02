using Fusion.XR.Shared.Locomotion;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.DesktopFocusAddon
{
    /***
     * 
     * DesktopFocus provides methods to activate/deactivate the focus mode on the object.
     * The CameraAnchor is used to define the camera position when focus mode is activated.
     * A list of canvas to be displayed can be specified when focus mode is activated. 
     * Also a list of renderers can be hidden when the focus mode is enabled.
     * It is also possible to define a previous/next DesktopFocus object to chain focus objects.
     * 
     ***/
    public class DesktopFocus : MonoBehaviour, DesktopFocusManager.IFocus
    {
        public DesktopFocusManager focusManager;

        [SerializeField] List<Canvas> _associatedCanvas = new List<Canvas>();

        [SerializeField] Transform _cameraAnchor;
        [SerializeField] Transform resizeTarget;

        // hideRenderers is used to hide a list of renderers when the focus mode is enabled 
        [SerializeField] bool hideRenderers = false;

        // closeOnClick bool is used to disable the focus mode if the player click on the mouse
        public bool closeOnClick = false;

        [Header("Navigation")]
        [SerializeField] DesktopFocus nextFocus;
        [SerializeField] DesktopFocus previousFocus;
        public DesktopFocusManager.IFocus PreviousFocus => previousFocus;
        public DesktopFocusManager.IFocus NextFocus => nextFocus;
        public Transform CameraAnchor => _cameraAnchor;
        public List<Canvas> AssociatedCanvas => _associatedCanvas;
        List<Renderer> renderers;

        public bool hasFocus = false;

        private void Awake()
        {
            renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
            if (_cameraAnchor == null) Debug.LogError("Missing cameraAnchor");
            if (focusManager == null) focusManager = FindAnyObjectByType<DesktopFocusManager>(FindObjectsInactive.Include);
            if (focusManager == null) Debug.LogError("Missing focusManager");
        }

        // ActivateFocus must be called to ask the focus 
        [ContextMenu("Activate focus")]
        public void ActivateFocus()
        {
            focusManager.GiveFocus(this);
        }

        float lastCloseOnClick = -1;
        List<RayBeamer> disabledOnCloseBeamers = new List<RayBeamer>();
        private void Update()
        {
            // Bounce prevention (closing/focusing with same click)
            if (lastCloseOnClick != -1 && (Time.time - lastCloseOnClick) >= 0.2f)
            {
                lastCloseOnClick = -1;
                foreach (var beamer in disabledOnCloseBeamers) beamer.enabled = true;
            }
            // Check if the focus should be deactivate
            if (hasFocus & closeOnClick && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
            {
                lastCloseOnClick = Time.time;
                disabledOnCloseBeamers = new List<RayBeamer>(FindObjectsByType<RayBeamer>(FindObjectsSortMode.None));
                foreach (var beamer in disabledOnCloseBeamers) beamer.enabled = false;
                DesactivateFocus();
            }
        }

        // DesactivateFocus must be called to release the focus 

        [ContextMenu("Desactivate focus")]
        public void DesactivateFocus()
        {
            focusManager.RemoveFocus(this);
        }

        // FocusChanged is called by the FocusManager when the focus status changed
        public void FocusChanged(bool hasFocus)
        {
            this.hasFocus = hasFocus;

            // check if we have to hide some renderers
            if (hideRenderers)
            {
                foreach (var renderer in renderers)
                {
                    renderer.enabled = !hasFocus;
                }
            }
            if (hasFocus)
            {
                if (resizeTarget)
                {
                    // Source: https://docs.unity3d.com/Manual/FrustumSizeAtDistance.html
                    Debug.LogError($"Frustum: {focusManager.DesktopCamera.fieldOfView} {resizeTarget.lossyScale.y} {Mathf.Tan(focusManager.DesktopCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)}");
                    var targetHeight = resizeTarget.lossyScale.y;
                    if (resizeTarget.lossyScale.y < resizeTarget.lossyScale.y)
                    {
                        var targetWidth = resizeTarget.lossyScale.x;
                        targetHeight = targetWidth / focusManager.DesktopCamera.aspect;
                        Debug.LogError($"Frustum: {resizeTarget.lossyScale.y} -> {targetHeight}");
                    }
                    CameraAnchor.transform.localPosition = new Vector3(CameraAnchor.transform.localPosition.x, CameraAnchor.transform.localPosition.y, -targetHeight * 0.5f / Mathf.Tan(focusManager.DesktopCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
                }
            }
        }

        [ContextMenu("Toggle focus")]
        public void ToggleFocus()
        {
            if (hasFocus)
                DesactivateFocus();
            else
                ActivateFocus();
        }
    }
}
