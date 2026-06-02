using UnityEngine;
using Fusion.Addons.TextureDrawing;
using Fusion.XR.Shared.Grabbing;

/***
 * 
 *  StickyNoteColorSelection is in charged to sync the StickyNote color modification.
 *          
 ***/
public class StickyNoteColorSelection : GrabbableColorSelection
{
    TextureSurface texture;

    protected override void Awake()
    {
        base.Awake();
        texture = GetComponent<TextureSurface>();
        if (texture == null)
            Debug.LogError("TextureSurface not found");
    }

    protected override void ApplyColorChange(Color color)
    {
        texture.ChangeBackgroundColor(color);
    }
}
