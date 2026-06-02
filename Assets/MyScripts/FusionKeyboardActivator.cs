using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class FusionKeyboardActivator : MonoBehaviour, IPointerClickHandler
{
    [Header("Settings")]
    public GameObject KeyboardRootObject; // Drag the Keyboard Prefab here
    public float DistanceFromFace = 0.5f; // How far away needed?
    public float HeightOffset = -0.2f;    // Lower it slightly (chest height)

    private TMP_InputField targetField;

    void Start()
    {
        targetField = GetComponent<TMP_InputField>();

        // Hide keyboard on start
        if (KeyboardRootObject != null)
        {
            KeyboardRootObject.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetField == null || KeyboardRootObject == null) return;

        // 1. Activate Input Field
        targetField.ActivateInputField();

        // 2. Teleport Keyboard to Player
        RepositionKeyboard();

        // 3. Show Keyboard
        KeyboardRootObject.SetActive(true);

        Debug.Log("Keyboard spawned in front of player.");
    }

    void RepositionKeyboard()
    {
        // Find the player's head (Camera)
        Camera head = Camera.main;
        if (head == null)
        {
            Debug.LogError("NO MAIN CAMERA FOUND! Please tag your VR Camera as 'MainCamera'.");
            return;
        }

        // 1. Calculate Position:
        // Start at head position -> Move forward by 'Distance' -> Move down by 'Height'
        Vector3 targetPos = head.transform.position + (head.transform.forward * DistanceFromFace);
        targetPos.y += HeightOffset;

        KeyboardRootObject.transform.position = targetPos;

        // 2. Calculate Rotation:
        // Make the keyboard look AT the player
        KeyboardRootObject.transform.LookAt(head.transform);

        // 3. Flip Rotation:
        // Unity UI usually faces 'backwards' when using LookAt. 
        // We rotate 180 degrees on Y so the keys face the user.
        KeyboardRootObject.transform.Rotate(0, 180, 0);

        // Optional: Remove X rotation if you want the keyboard perfectly vertical (not tilted up/down)
        // Vector3 euler = KeyboardRootObject.transform.eulerAngles;
        // KeyboardRootObject.transform.eulerAngles = new Vector3(0, euler.y, 0);
    }
}