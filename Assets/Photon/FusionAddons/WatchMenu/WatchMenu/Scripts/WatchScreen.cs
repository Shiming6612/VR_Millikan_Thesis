using TMPro;
using UnityEngine;

namespace Fusion.Addons.WatchMenu
{
    /// <summary>
    /// WatchScreen provides the function to update the text on the watch
    /// </summary>

    public class WatchScreen : MonoBehaviour
    {
        [SerializeField] TMP_Text watchText = null;

        private void Awake()
        {
            if (watchText == null)
            {
                watchText = GetComponentInChildren<TMP_Text>();
            }
        }

        public void UpdateWatchText(string text)
        {
            if (watchText)
            {
                watchText.text = text;
            }
        }
    }
}
