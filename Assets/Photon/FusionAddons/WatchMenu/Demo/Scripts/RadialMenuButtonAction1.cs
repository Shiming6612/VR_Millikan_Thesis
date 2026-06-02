using UnityEngine;
using Fusion.Addons.WatchMenu;

public class RadialMenuButtonAction1 : RadialMenuButtonAction
{

    protected override void OnButtonClick()
    {
        Debug.LogError("OnButtonClick RadialMenuButtonAction1. Button Not interactable anymore.");
        button.interactable = false;
    }

}
