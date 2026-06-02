using UnityEngine;
using Fusion.Addons.WatchMenu;

public class RadialMenuButtonWindowsSettings2 : RadialMenuButtonWindows
{

    protected override void Awake()
    {
        base.Awake();

#if !RADIALMENUBUTTONWINDOWSSETTINGS2
        shouldBeDisplayed = false;
#endif

    }
}
