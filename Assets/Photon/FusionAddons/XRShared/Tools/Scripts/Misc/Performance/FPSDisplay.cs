using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Fusion.XR.Shared.Utils
{
    /**
     * 
     * FPSDisplay is in charge to display the number of frame per second
     * Also, the number of spawned bots is displayed in editor mode (update every 2 seconds)
     * 
     **/
    public class FPSDisplay : MonoBehaviour
    {
        public TextMeshProUGUI m_Text;
        public string suffix = " FPS";

        // Start is called before the first frame update
        void Awake()
        {
            if(m_Text == null)
                m_Text = GetComponentInChildren<TextMeshProUGUI>();

            StartCoroutine(UpdateTextRoutine());
        }

        // Update is called once per frame
        IEnumerator UpdateTextRoutine()
        {
            while (true)
            {
                float current = (int)(1f / Time.unscaledDeltaTime);

                m_Text.text = current.ToString() + suffix;

                yield return new WaitForSeconds(2f);
            }
        }
    }
}
