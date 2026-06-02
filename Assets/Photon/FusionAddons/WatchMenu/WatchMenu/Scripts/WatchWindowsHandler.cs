using Fusion.XR.Shared.Core;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  This component should be located on rig(s) with a watch menu that needs to open a window.
///  WatchWindowsHandler receives requests to open windows from the `RadialMenuButtonWindows` component on buttons intended to open a window.
///  It spawns the windows if not yet created and centralizes all user-opened windows in a list
///  Then it manages their activation with `ToggleWindow()`, and keeps their position aligned with the user.
/// </summary>

namespace Fusion.Addons.WatchMenu
{
    public class WatchWindowsHandler : MonoBehaviour
    {
        [SerializeField] List<WatchScreen> watchScreens = new List<WatchScreen>();

        [System.Serializable]
        public struct WindowDescription
        {
            public string windowName;
            public WatchWindow windowPrefab;
            public bool instantiateHiddenAtStart;
        }

        [System.Serializable]
        public class RegisteredWindows
        {
            public WindowDescription windowDescription;
            public WatchWindow windowInstance;
        }

        public List<WindowDescription> windowsDescriptions = new List<WindowDescription>();
        Dictionary<string, RegisteredWindows> registeredWindows = new Dictionary<string, RegisteredWindows>();
        public List<RegisteredWindows> registeredWindowList = new List<RegisteredWindows>();

        // Related network object if we place the watch on a network rig
        NetworkObject networkObject;

        [Header("Various settings")]
        public Vector3 windowSpawnPositionOffsetRelativeToHeadset = new Vector3(0.15f, -0.1f, 0.5f);
        [SerializeField] bool flipWindow = false;
        [SerializeField] string defaultWindowText = "";

        public bool IsLocalUserWindowHandler => networkObject == null || networkObject.HasStateAuthority;

        IHardwareRig hardwareRig;

        private void Awake()
        {
            if (watchScreens == null || watchScreens.Count == 0)
            {
                watchScreens = new List<WatchScreen>(GetComponentsInChildren<WatchScreen>(true));
            }

            networkObject = GetComponentInParent<NetworkObject>();

            foreach (var info in windowsDescriptions) RegisterWindow(info);
        }

        private void Start()
        {
            UpdateWatchText(defaultWindowText);
        }

        public void RegisterWindow(WindowDescription description)
        {
            if (registeredWindows.ContainsKey(description.windowName))
            {
                Debug.LogError($"Window {description.windowName} already known (with prefab {registeredWindows[description.windowName].windowDescription.windowPrefab})." +
                    $" Cancelling new registration with prefab {description.windowPrefab}");
                return;
            }
            registeredWindows[description.windowName] = new RegisteredWindows { windowDescription = description, windowInstance = null };
            registeredWindowList.Add(registeredWindows[description.windowName]);
        }

        private WatchWindow InstantiateWindowByName(string name, bool startOpen = false)
        {
            if (registeredWindows.ContainsKey(name))
            {
                InstantiateWindow(ref registeredWindows[name].windowInstance, registeredWindows[name].windowDescription.windowPrefab, startOpen);
            }
            else
            {
                Debug.LogError("Unregistered window " + name);
            }
            return null;
        }

        public WatchWindow InstantiateWindow(ref WatchWindow window, WatchWindow windowPrefab, bool startOpen = false)
        {
            if (window == null && windowPrefab != null)
            {
                var windowSpawnPosition = hardwareRig.Headset.gameObject.transform.TransformPoint(windowSpawnPositionOffsetRelativeToHeadset);
                window = Instantiate(windowPrefab, windowSpawnPosition, Quaternion.identity);
                if (startOpen == false)
                {
                    window.gameObject.SetActive(false);
                }
                window.watchWindowsHandler = this;
            }
            return window;
        }

        public void UpdateWatchText(string text)
        {
            foreach (var watchScreen in watchScreens)
            {
                watchScreen.UpdateWatchText(text);
            }
        }

        /// <summary>
        /// Register a window description if not yet know, then toggle it
        /// </summary>
        /// <param name="windowDescription"></param>
        public WatchWindow ToggleWindow(WindowDescription windowDescription)
        {
            if (registeredWindows.ContainsKey(windowDescription.windowName) == false)
            {
                RegisterWindow(windowDescription);
            }
            return ToggleWindowByName(windowDescription.windowName);
        }

        public void DoToggleWindowByName(string name)
        {
            ToggleWindowByName(name);
        }

        public WatchWindow GetWindowByName(string windowName, bool startOpen = false)
        {
            if (registeredWindows.ContainsKey(windowName))
            {
                if (IsLocalUserWindowHandler)
                {
                    if (registeredWindows[windowName].windowInstance == null)
                    {
                        InstantiateWindowByName(windowName, startOpen);
                    }
                    if (registeredWindows[windowName].windowInstance == null)
                    {
                        Debug.LogError("Unable to instanciate window " + windowName);
                    }
                    else
                    {
                        return registeredWindows[windowName].windowInstance;
                    }
                }
            }
            else
            {
                Debug.LogError($"[WatchWindowsHandler {this.name}] Unregistered window " + windowName);
            }
            return null;
        }

        public WatchWindow ToggleWindowByName(string windowName)
        {
            var window = GetWindowByName(windowName);
            if (window)
            {
                return ToggleWindow(window);
            }
            return null;
        }

        WatchWindow ToggleWindow(WatchWindow window)
        {
            if (window)
            {
                DisplayWindow(window, shouldDisplay: !window.gameObject.activeSelf);

                return window;
            }
            return null;
        }

        public WatchWindow DisplayWindowByName(string windowName, bool shouldDisplay = true)
        {
            var menu = GetWindowByName(windowName, startOpen: shouldDisplay);
            if (menu)
            {
                DisplayWindow(menu);
            }
            return menu;
        }

        public void DisplayWindow(WatchWindow window, bool shouldDisplay = true)
        {
            window.gameObject.SetActive(shouldDisplay);
            PositionWindow(window);
        }

        public void PositionWindow(WatchWindow window)
        {
            if (window && window.gameObject.activeSelf)
            {
                var headsetTransform = hardwareRig.Headset.gameObject.transform;

                var windowPosition = headsetTransform.TransformPoint(windowSpawnPositionOffsetRelativeToHeadset);
                Quaternion windowRotation;
                if (flipWindow)
                {
                    windowRotation = Quaternion.Euler(0, headsetTransform.eulerAngles.y, 0);
                }
                else
                {
                    windowRotation = Quaternion.Euler(0, 180 + headsetTransform.eulerAngles.y, 0);
                }
                window.transform.position = windowPosition;
                window.transform.rotation = windowRotation;
                window.transform.localScale = hardwareRig.transform.localScale;
            }
        }

        bool rigPositionChecked = false;
        Vector3 lastRigPosition = Vector3.zero;
        bool initialInstantiationChecked = false;

        private void Update()
        {
            if (hardwareRig == null)
            {
                hardwareRig = HardwareRigsRegistry.GetHardwareRig();
            }
            if (hardwareRig == null) return;

            if(initialInstantiationChecked == false && IsLocalUserWindowHandler)
            {
                initialInstantiationChecked = true;
                foreach (var registeredWindow in registeredWindows.Values)
                {
                    if (registeredWindow.windowDescription.instantiateHiddenAtStart && registeredWindow.windowInstance == null) 
                    { 
                        InstantiateWindowByName(registeredWindow.windowDescription.windowName); 
                    }
                }
            }

            // We close the window in case of large rig teleportation
            if (rigPositionChecked && Vector3.Distance(lastRigPosition, hardwareRig.transform.position) > 0.5f)
            {
                foreach (var registeredWindow in registeredWindows.Values)
                {
                    if (registeredWindow.windowInstance != null && registeredWindow.windowInstance.gameObject.activeSelf && registeredWindow.windowInstance.closeOnLargeRigMove)
                    {
                        ToggleWindowByName(registeredWindow.windowDescription.windowName);
                    }                        
                }
            }
            lastRigPosition = hardwareRig.transform.position;
            rigPositionChecked = true;
        }
    }
}
