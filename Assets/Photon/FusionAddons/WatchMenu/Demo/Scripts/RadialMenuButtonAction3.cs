using UnityEngine;
using Fusion.Addons.WatchMenu;

public class RadialMenuButtonAction3 : RadialMenuButtonAction
{

    protected override void OnButtonClick()
    {
        Debug.LogError("OnButtonClick RadialMenuButtonAction3. Button color updated.");
        isActive = !isActive;
        UpdateButtonColor();
    }
}
