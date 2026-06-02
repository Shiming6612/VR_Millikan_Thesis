using UnityEngine;
using UnityEngine.Events;

namespace Fusion.Addons.WatchMenu
{
    /// <summary>
    /// Any window opened from a watch button should inherit from this class.
    /// This class triggers events when windows open or close and provide the function to update the text on the watch (through the WatchWindowsHandler).
    /// </summary>

    public class WatchWindow : MonoBehaviour
    {
        public WatchWindowsHandler watchWindowsHandler;
        public UnityEvent onOpenWindow = new UnityEvent();
        public UnityEvent onCloseWindow = new UnityEvent();

        public bool closeOnLargeRigMove = true;

        public bool IsOpened => gameObject.activeSelf;

        public void UpdateWatchText(string text)
        {
            watchWindowsHandler.UpdateWatchText(text);
        }

        public virtual void OnOpenWindow()
        {
            if (onOpenWindow != null) onOpenWindow.Invoke();
        }

        public virtual void OnCloseWindow()
        {
            if (onCloseWindow != null) onCloseWindow.Invoke();
        }

        protected virtual void OnEnable()
        {
            OnOpenWindow();
        }

        protected virtual void OnDisable()
        {
            OnCloseWindow();
        }

        [ContextMenu("Close")]
        public virtual void Close()
        {
            gameObject.SetActive(false);
        }

        [ContextMenu("Open")]
        public virtual void Open()
        {
            gameObject.SetActive(true);
        }
    }
}