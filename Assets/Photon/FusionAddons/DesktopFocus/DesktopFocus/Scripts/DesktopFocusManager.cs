using Fusion.XR.Shared;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Desktop;
using Fusion.XR.Shared.Rig;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


namespace Fusion.Addons.DesktopFocusAddon
{
    /***
     * 
     * The DesktopFocusManager is in charge to manage focus requests from objects in the scene.
     * It provides a public GiveFocus method that is called by DesktopFocus objects when they need the focus.
     * If an object already had the focus, it loses it and the focus is allocated to the new requester.
     * When the focus is assigned, desktop rig camera is disabled, the focusCamera camera child object is activated and moved to the position specified by the requester thanks to the IFocus interface
     * If the DesktopFocus object defines a specific UI (i.e. canvas), it is enabled.
     * Finally, desktop rig control elements are deactivated (desktopController, mouseCamera & mouseTeleport) in order to use the DesktopFocus object UI properly.
     * 
     ***/
    public class DesktopFocusManager : MonoBehaviour
    {
        public interface IFocus
        {
            public Transform CameraAnchor { get; }
            public List<Canvas> AssociatedCanvas { get; }
            public IFocus PreviousFocus { get; }
            public IFocus NextFocus { get; }
            public void FocusChanged(bool hasFocus);
        }

        [SerializeField] private RigInfo rigInfo;
        [SerializeField] private Camera focusCamera;
        private List<ApplicationLifeCycleManager> applicationManagers;
        [SerializeField] private InputActionProperty closeAction;
        [SerializeField] private InputActionProperty nextAction;
        [SerializeField] private InputActionProperty prevAction;

        public IFocus currentFocus;

        bool rigAnalysed = false;
        IHardwareRig desktopRig;
        Transform initialFocusCameraParent;

        MouseCamera mouseCamera;
        MouseTeleport mouseTeleport;
        DesktopController desktopController;

        Camera _desktopCamera;

        Dictionary<Canvas, Camera> originalEventCameras = new Dictionary<Canvas, Camera>();

        private void Awake()
        {
            focusCamera.gameObject.SetActive(false);
            initialFocusCameraParent = focusCamera.transform.parent;
            if (!rigInfo)
            {
                rigInfo = GetComponentInParent<RigInfo>();
            }
            if (!rigInfo) Debug.LogError("Missing rig info");
            if (!focusCamera) Debug.LogError("Missing focusCamera");
            applicationManagers = new List<ApplicationLifeCycleManager>(FindObjectsByType<ApplicationLifeCycleManager>(FindObjectsInactive.Include,FindObjectsSortMode.None));

        }

        private void Start()
        {
            // Set the input
            ActivateKeyAction(closeAction, "escape");
            ActivateKeyAction(prevAction, "leftArrow");
            ActivateKeyAction(nextAction, "rightArrow");
        }

        public void ActivateKeyAction(InputActionProperty action, string defaultKey)
        {
            if (action.reference == null && action.action.bindings.Count == 0)
            {
                action.action.AddBinding($"<Keyboard>/{defaultKey}");
            }
            action.action.Enable();
        }

        private void Update()
        {
            if (currentFocus == null) return;

            // Check inputs
            if (closeAction.action.WasPerformedThisFrame())
            {
                DisableFocus();
            }
            if (nextAction.action.WasPerformedThisFrame())
            {
                if (currentFocus.NextFocus != null) GiveFocus(currentFocus.NextFocus);
            }
            if (prevAction.action.WasPerformedThisFrame())
            {
                if (currentFocus.PreviousFocus != null) GiveFocus(currentFocus.PreviousFocus);
            }
        }

        // Find and return the desktop camera
        public Camera DesktopCamera
        {
            get
            {
                if (!rigAnalysed)
                {
                    if (rigInfo == null) return null;
                    desktopController = rigInfo.localHardwareRig.gameObject.GetComponentInChildren<DesktopController>();
                    if (desktopController)
                    {
                        desktopRig = rigInfo.localHardwareRig;
                        _desktopCamera = desktopRig.Headset.gameObject.GetComponentInChildren<Camera>();
                        mouseCamera = desktopRig.gameObject.GetComponentInChildren<MouseCamera>();
                        mouseTeleport = desktopRig.gameObject.GetComponentInChildren<MouseTeleport>();
                    }
                    rigAnalysed = true;
                }
                return _desktopCamera;
            }
        }

        public void GiveFocus(IFocus focus)
        {
            // Exit if there is no desktop camera (VR mode)
            if (!DesktopCamera) return;

            // remove focus on the current desktopFocus object
            RemoveFocus();

            currentFocus = focus;
            var targetParent = focus.CameraAnchor;

            // Disable the desktop camera
            DesktopCamera.enabled = false;

            // Set the focus camera
            focusCamera.gameObject.SetActive(true);
            focusCamera.transform.parent = targetParent;
            focusCamera.transform.localPosition = Vector3.zero;
            focusCamera.transform.localRotation = Quaternion.identity;

            // Displays the UI
            if (focus.AssociatedCanvas != null)
            {
                foreach (var canvas in focus.AssociatedCanvas)
                {
                    if (!originalEventCameras.ContainsKey(canvas))
                    {
                        originalEventCameras[canvas] = canvas.worldCamera;
                    }
                    canvas.worldCamera = focusCamera;
                    canvas.gameObject.SetActive(true);
                }
            }

            // Disable the desktop controls
            ActivateDesktopControls(false);

            // Inform the desktopFocus object that the focus is enable
            focus.FocusChanged(true);
        }

        public void RemoveFocus(IFocus focus)
        {
            if (currentFocus != focus) return;
            DisableFocus();
        }

        void RemoveFocus()
        {
            // Exit if there is no desktop focus object
            if (currentFocus == null) return;

            // Hide the UI
            foreach (var canvas in currentFocus.AssociatedCanvas)
            {
                if (originalEventCameras.ContainsKey(canvas))
                {
                    canvas.worldCamera = originalEventCameras[canvas];
                }
                canvas.gameObject.SetActive(false);
            }

            // Inform the desktopFocus object that the focus is disable
            currentFocus.FocusChanged(false);
            currentFocus = null;
        }

        [ContextMenu("Disable focus")]
        public void DisableFocus()
        {
            // Exit if there is no desktop camera (VR mode)
            if (!DesktopCamera) return;

            RemoveFocus();

            // restore the destop camera
            DesktopCamera.enabled = true;
            focusCamera.gameObject.SetActive(false);
            focusCamera.transform.parent = initialFocusCameraParent;

            // activate the desktop controls
            ActivateDesktopControls(true);
        }

        void ActivateDesktopControls(bool activate)
        {
            if (activate)
            {
                // We have to wait one frame to be sure that the main menu doesn't appears when the focus is disabled
                StartCoroutine(DoActivateDesktopControlsCoroutine(activate));
            }
            else
            {
                // We want one frame before disabling the desktop (to allow the line renderer of the ray beamer to cut the line renderer)
                StartCoroutine(DoActivateDesktopControlsCoroutine(activate));
            }
        }

        IEnumerator DoActivateDesktopControlsCoroutine(bool activate)
        {
            yield return null;

            DoActivateDesktopControls(activate);
        }

        void DoActivateDesktopControls(bool activate)
        {
            if (desktopController) desktopController.enabled = activate;
            if (mouseCamera) mouseCamera.enabled = activate;
            if (mouseTeleport) mouseTeleport.enabled = activate;

            foreach (var applicationManager in applicationManagers)
                applicationManager.ChangeMenuAuthorization(activate);
        }

    }
}
