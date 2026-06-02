using UnityEngine;

public class NPCInteractTrigger : MonoBehaviour
{
    public GameObject uiPrompt; // "Press A to talk" canvas (world space)
    public Transform npc;
    public Transform playerHead;

    private bool playerInside;

    void Start()
    {
        if (uiPrompt != null) uiPrompt.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = true;
        if (uiPrompt != null) uiPrompt.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = false;
        if (uiPrompt != null) uiPrompt.SetActive(false);
    }

    void Update()
    {
        if (!playerInside) return;

        // simple keyboard test in editor:
        if (Input.GetKeyDown(KeyCode.E))
        {
            Talk();
        }
    }

    void Talk()
    {
        if (uiPrompt != null) uiPrompt.SetActive(false);

        // Face player
        if (npc != null && playerHead != null)
        {
            Vector3 look = playerHead.position - npc.position;
            look.y = 0;
            npc.rotation = Quaternion.LookRotation(look);
        }

        Debug.Log("NPC: Hello! I can help you.");
        // Here you trigger dialogue UI, audio, quest, etc.
    }
}
