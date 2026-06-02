using System.Collections;
using UnityEngine;
using Fusion.Addons.WatchMenu;

public class SettingsWindow3 : WatchWindow
{

    float delayBeforeClosing = 0.3f;
  
    public void CloseSettingsMenu()
    {
        StartCoroutine(CloseAfterDelay(delayBeforeClosing));
    }

    IEnumerator CloseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
    }
  
}
