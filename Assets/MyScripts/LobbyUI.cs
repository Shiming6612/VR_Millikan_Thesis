using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField nameInput;
    public TMP_Dropdown sceneDropdown;

    [Tooltip("Scene that this lobby should load directly when Start is pressed.")]
    public string targetSceneName = "TAI";

    [Tooltip("Use dropdown selection instead of targetSceneName.")]
    public bool useDropdownSelection = false;

    [Tooltip("Drag the prefab object that has ColorWheelControl on it.")]
    public ColorWheelControl colorWheel;

    [Tooltip("Optional: small Image to preview the selected color.")]
    public Image colorPreview;

    private Color lastColor;

    private readonly string[] sceneNames =
    {
        "TAI_Classroom01_NoNPC",
        "TAI_Classroom01_NPC",
        "TAI_Classroom02_NoNPC",
        "TAI_Classroom02_NPC",
        "TAI_Classroom03_NoNPC",
        "TAI_Classroom03_NPC",
        "TAI_Classroom04_NoNPC",
        "TAI_Classroom04_NPC"
    };

    private void Start()
    {
        if (sceneDropdown != null)
        {
            sceneDropdown.ClearOptions();
            sceneDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Classroom 1 - No NPC",
                "Classroom 1 - NPC",
                "Classroom 2 - No NPC",
                "Classroom 2 - NPC",
                "Classroom 3 - No NPC",
                "Classroom 3 - NPC",
                "Classroom 4 - No NPC",
                "Classroom 4 - NPC"
            });
        }

        if (ClientData.Instance != null)
        {
            lastColor = ClientData.Instance.PlayerColor;

            if (colorWheel != null)
            {
                colorWheel.PickColor(lastColor);
            }

            ApplyColorToClientData(lastColor);
        }
        else
        {
            lastColor = Color.red;
            ApplyColorToClientData(lastColor);
        }
    }

    private void Update()
    {
        if (colorWheel == null || ClientData.Instance == null)
            return;

        Color current = colorWheel.Selection;

        if (current != lastColor)
        {
            lastColor = current;
            ApplyColorToClientData(current);
        }
    }

    private void ApplyColorToClientData(Color c)
    {
        if (ClientData.Instance != null)
            ClientData.Instance.PlayerColor = c;

        if (colorPreview != null)
            colorPreview.color = c;
    }

    public void SaveName()
    {
        if (nameInput != null && ClientData.Instance != null)
            ClientData.Instance.PlayerName = nameInput.text;
    }

    public void StartGame()
    {
        SaveName();

        string sceneToLoad = targetSceneName;

        if (useDropdownSelection && sceneDropdown != null)
        {
            int index = sceneDropdown.value;

            if (index >= 0 && index < sceneNames.Length)
            {
                sceneToLoad = sceneNames[index];
            }
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}