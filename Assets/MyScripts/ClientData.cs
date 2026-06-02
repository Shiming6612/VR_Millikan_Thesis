using UnityEngine;

public class ClientData : MonoBehaviour
{
    public static ClientData Instance; // Allows us to access this from anywhere

    // The data we want to carry
    public Color PlayerColor = Color.white;
    public string PlayerName = "Player";

    private void Awake()
    {
        // Singleton Pattern: Ensure only one exists and it survives scene loads
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // <--- The magic line
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
