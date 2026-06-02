using UnityEngine;
using TMPro;

/**
 * 
 * DisplayVersion displays the application version in the UI
 *
 **/
namespace Fusion.XR.Shared.Utils
{
    public class DisplayVersion : MonoBehaviour
    {
        public TextMeshProUGUI versionTMP;
        public string prefix = "V: ";

        // Start is called before the first frame update
        void Start()
        {
            if (versionTMP == null)
                versionTMP = GetComponent<TextMeshProUGUI>();

            if (versionTMP != null)
                versionTMP.text = prefix + Application.version.ToString();
        }
    }
}
