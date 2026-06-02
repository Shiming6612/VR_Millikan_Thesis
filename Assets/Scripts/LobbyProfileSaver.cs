using TMPro;
using UnityEngine;

public class LobbyProfileSaver : MonoBehaviour
{
    [Header("Refs")]
    public TMP_InputField nameInputField;
    public ColorWheelControl colorWheel;

    public void SaveProfile()
    {
        string playerName = "Player";

        if (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
            playerName = nameInputField.text.Trim();

        Color avatarColor = Color.blue;

        if (colorWheel != null)
            avatarColor = colorWheel.Selection;

        PlayerProfileStore.Save(playerName, avatarColor);
    }
}