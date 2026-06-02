using UnityEngine;

public static class PlayerProfileStore
{
    public static string PlayerName = "Player";
    public static Color AvatarColor = Color.blue;
    public static bool HasProfile = false;

    public static void Save(string playerName, Color avatarColor)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        PlayerName = playerName.Trim();
        AvatarColor = avatarColor;
        HasProfile = true;

        Debug.Log($"[PlayerProfileStore] Saved: {PlayerName}, {AvatarColor}");
    }
}