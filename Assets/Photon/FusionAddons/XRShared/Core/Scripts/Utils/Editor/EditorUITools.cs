#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fusion.XRShared.Tools
{
    public class ConfirmWindow : EditorWindow
    {
        public delegate void ConfirmCallback();
        public delegate void CancelCallback();
        ConfirmCallback confirmCallback = null;
        CancelCallback cancelCallback = null;
        string confirmText = "Confirm ?";
        bool cancelled = false;

        public static void ShowConfirmation(string title, string confirmText, ConfirmCallback confirmCallback, CancelCallback cancelCallback = null)
        {
            ConfirmWindow window = ScriptableObject.CreateInstance(typeof(ConfirmWindow)) as ConfirmWindow;
            window.maxSize = new Vector2(400, 150);
            window.titleContent.text = title;
            window.Configure(confirmText, confirmCallback, cancelCallback);
            var position = EditorGUIUtility.GetMainWindowPosition().center - window.maxSize / 2;
            window.position = new Rect(position, window.maxSize);
            window.ShowModalUtility();
        }

        void Install()
        {
            if (confirmCallback != null) confirmCallback();
            Close();
        }

        void Cancel()
        {
            cancelled = true;
            if (cancelCallback != null) cancelCallback();
            Close();
        }

        public void Configure(string text, ConfirmCallback confirmCallback, CancelCallback cancelCallback = null)
        {
            confirmText = text;
            this.confirmCallback = confirmCallback;
            this.cancelCallback = cancelCallback;
        }

        void OnGUI()
        {
            GUIStyle labelStyle = new GUIStyle(GUI.skin.GetStyle("label"))
            {
                wordWrap = true
            };
            EditorGUILayout.LabelField(confirmText, labelStyle);

            if (GUILayout.Button("Yes"))
                Install();

            if (GUILayout.Button("No"))
                Cancel();
        }

        void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnDestroy()
        {
            if(cancelled == false)
            {
                if (cancelCallback != null) cancelCallback();
            }
        }
    }
}
#endif