using TMPro;
using UnityEngine;

public class ApplySavedProfileToNPC : MonoBehaviour
{
    [Header("Cap")]
    public Renderer capRenderer;

    [Header("Name Label")]
    public TMP_Text nameLabel;

    [Header("Fallback")]
    public Color fallbackColor = Color.blue;
    public string fallbackName = "Player";

    [Header("Live Update")]
    public bool updateEveryFrame = false;
    public ColorWheelControl colorWheelForLivePreview;

    private void Start()
    {
        ApplySavedProfile();
    }

    private void Update()
    {
        if (updateEveryFrame && colorWheelForLivePreview != null)
        {
            ApplyCapColor(colorWheelForLivePreview.Selection);
        }
    }

    public void ApplySavedProfile()
    {
        Color color = PlayerProfileStore.HasProfile ? PlayerProfileStore.AvatarColor : fallbackColor;
        string playerName = PlayerProfileStore.HasProfile ? PlayerProfileStore.PlayerName : fallbackName;

        ApplyCapColor(color);

        if (nameLabel != null)
            nameLabel.text = playerName;
    }

    private void ApplyCapColor(Color color)
    {
        if (capRenderer == null)
            return;

        Material mat = capRenderer.material;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        else
            mat.color = color;
    }
}