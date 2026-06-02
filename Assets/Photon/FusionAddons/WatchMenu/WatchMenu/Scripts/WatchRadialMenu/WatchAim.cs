using Fusion.XR.Shared.Core;
using UnityEngine;

namespace Fusion.Addons.WatchMenu
{
    /// <summary>
    /// This component, placed on the watch, checks whether the user is looking at it.
    /// When they are, it instructs the configured radial menu to display its buttons.
    /// Please note this class implements the `IRigPartVisualizerGameObjectToAdapt` interface in order to add the watch in the  `Game Objects To Adapt` list of the `RigPartVisualizer` (added on runtime).
    /// </summary>


    [DefaultExecutionOrder(WatchAim.EXECUTION_ORDER)]
    public class WatchAim : MonoBehaviour, IRigPartVisualizerGameObjectToAdapt
    {
        // We move the menu late, to be sure to follow components after their final moves for the frame (rig parts, ...)
        const int EXECUTION_ORDER = 100_000;
        [SerializeField] Transform aimObject;
        [SerializeField] Vector3 aimObjectTranslationOffset = new Vector3(0f, 0.015f, 0f);
        [SerializeField] Vector3 aimObjectRotationOffset = new Vector3(0f, 0f, 90f);
        [SerializeField] RadialMenu radialMenu;
        [SerializeField] float acceptedAngleBetweenHeadsetAndWatch = 15f;
        [SerializeField] bool disableWhenOnline = false;

        [Header("Set automatically")]
        [SerializeField] Transform headsetTransform;
        IHardwareRig hardwareRig;
        NetworkObject networkObject;
        [SerializeField] RigPartVisualizer rigPartVisualizer;

        private void Awake()
        {
            networkObject = GetComponentInParent<NetworkObject>();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        private void Start()
        {
            if (radialMenu == null)
            {
                Debug.LogError($"RadialMenu not set {name} ({transform.root?.name ?? ""})");
            }
            if (aimObject == null)
            {
                aimObject = transform;
            }
            SetHeadset();
        }

        [BeforeRenderOrder(WatchAim.EXECUTION_ORDER)]
        void OnBeforeRender()
        {
            WatchMenuHandling();
        }

        void SetHeadset()
        {
            if (hardwareRig == null)
            {
                hardwareRig = HardwareRigsRegistry.GetHardwareRig();
            }
            if (hardwareRig == null)
            {
                Debug.LogError("No hardwareRig");
            }
            else
            {
                if (headsetTransform == null && hardwareRig.Headset != null)
                {
                    headsetTransform = hardwareRig.Headset.transform;
                }
                if (headsetTransform == null)
                {
                    Debug.LogError("headsetTransform not set and Headset not found");
                }
            }
        }

        void WatchMenuHandling()
        {
            if (radialMenu == null) return;
            if (networkObject && networkObject.HasStateAuthority == false) return;

            if (rigPartVisualizer == null)
            {
                rigPartVisualizer = GetComponentInParent<RigPartVisualizer>();
            }

            if (rigPartVisualizer && rigPartVisualizer.ShouldDisplay() == false)
            {
                radialMenu.CloseRadialMenu();
                return;
            }

            if (disableWhenOnline && hardwareRig.LocalUserNetworkRig != null && (hardwareRig.LocalUserNetworkRig.Object?.Runner?.IsRunning ?? false))
            {
                radialMenu.CloseRadialMenu();
                return;
            }

            if (hardwareRig == null) return;
            radialMenu.transform.rotation = aimObject.rotation * Quaternion.Euler(aimObjectRotationOffset);
            radialMenu.transform.position = aimObject.transform.TransformPoint(aimObjectTranslationOffset);

            if (headsetTransform == null || radialMenu == null || aimObject == null) return;

            if (IsHeadsetIsTurnedTowardWatch())
            {
                radialMenu.OpenRadialMenu();
            }
            else
            {
                radialMenu.CloseRadialMenu();
            }
        }



        private bool IsHeadsetIsTurnedTowardWatch()
        {
            Vector3 directionToWatch = (radialMenu.transform.position - headsetTransform.position).normalized;
            float angleBetweenWatchandHeadset = Vector3.Angle(radialMenu.transform.forward, directionToWatch);

            if (angleBetweenWatchandHeadset < acceptedAngleBetweenHeadsetAndWatch)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
