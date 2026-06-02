using UnityEngine;
using Fusion.Addons.WatchMenu;


public class RadialMenuButtonAction2 : RadialMenuButtonAction
{

    protected override void Awake()
    {
        base.Awake();

#if !RADIALMENUBUTTONACTION2
        shouldBeDisplayed = false;
#endif

    }
   
    protected override void OnButtonClick()
    {
        Debug.LogError("OnButtonClick RadialMenuButtonAction2. Button color updated.");
        isActive = !isActive;
        UpdateButtonColor();
    }

}
