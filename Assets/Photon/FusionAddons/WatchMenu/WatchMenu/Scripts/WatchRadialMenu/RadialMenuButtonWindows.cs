using UnityEngine;

namespace Fusion.Addons.WatchMenu
{
    /// <summary>
    /// RadialMenuButtonWindows inherits from RadialMenuButtonAction and is in charge of opening a window.
    /// It takes as parameters the prefab of the window to spawn and an associated name.
    /// These are passed to the WatchWindowsHandler, which manages the created window instances.
    /// This class also listens to the onOpenWindow and onCloseWindow events of WatchWindow to update the button’s status.
    /// </summary>

    public class RadialMenuButtonWindows : RadialMenuButtonAction
    {
        [Header("Set automatically")]
        public WatchWindowsHandler watchWindowsHandler;
        [SerializeField] WatchWindow watchWindow;
        [SerializeField] WatchWindowsHandler.WindowDescription windowDescription = new WatchWindowsHandler.WindowDescription { windowName = "ActionSettings" };


        bool eventRegistrationDone = false;

        protected override void OnButtonClick()
        {
            if (watchWindowsHandler)
            {
                watchWindow = watchWindowsHandler.ToggleWindow(windowDescription);

                if (eventRegistrationDone == false)
                {
                    if (watchWindow != null)
                    {
                        watchWindow.onOpenWindow.AddListener(OnWindowIsOpen);
                        watchWindow.onCloseWindow.AddListener(OnWindowIsClose);
                        eventRegistrationDone = true;
                        // We just opened the window (before subscribing to its callbacks)
                        if (watchWindow.IsOpened) OnWindowIsOpen();
                    }
                    else
                    {
                        Debug.LogError($"OnButtonClick : watchWindow not found in watchWindowsHandler {watchWindowsHandler.gameObject.name}");
                    }
                }
                PlayAudioFeedback();
            }
            else
            {
                Debug.LogError("Missing watchWindowsHandler");
            }
        }

        private void OnWindowIsClose()
        {
            isActive = false;
            UpdateButtonColor();
        }

        private void OnWindowIsOpen()
        {
            isActive = true;
            UpdateButtonColor();
        }

        private void OnDestroy()
        {
            if (watchWindow != null)
            {
                watchWindow.onOpenWindow.RemoveListener(OnWindowIsOpen);
                watchWindow.onCloseWindow.RemoveListener(OnWindowIsClose);
            }
        }
    }
}
