using UnityEngine;

public class PlayerConfig : MonoBehaviour
{
    public static PlayerConfig Instance;

    [Header("Values chosen in lobby")]
    public string playerName = "Player";
    public Color avatarColor = Color.cyan;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
