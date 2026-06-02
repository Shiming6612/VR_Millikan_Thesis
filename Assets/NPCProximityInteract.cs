using UnityEngine;

public class NPCProximityInteract : MonoBehaviour
{
    [Header("References")]
    public NetworkedNPCController npcController;
    public Transform npcRoot;                 // usually NPC root transform
    public Canvas promptCanvas;               // world-space canvas: "Press E / A to talk"
    public Transform promptFaceTarget;        // optional: make prompt face camera

    [Header("Proximity")]
    public float talkDistance = 2.0f;

    [Header("Talk (demo)")]
    public bool autoTalkOnce = false;

    private bool _inside;
    private bool _talkedOnce;
    private Transform _playerHead;

    void Start()
    {
        if (npcController == null) npcController = GetComponentInParent<NetworkedNPCController>();
        if (npcRoot == null && npcController != null) npcRoot = npcController.transform;

        if (promptCanvas != null) promptCanvas.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        // VR rigs often don't have "Player" tags.
        // Most reliable: detect a Camera in the entering object’s parents.
        var cam = other.GetComponentInParent<Camera>();
        if (cam == null) return;

        _inside = true;
        _playerHead = cam.transform;

        if (promptCanvas != null) promptCanvas.enabled = true;

        // Ask NPC (authority) to look at this player
        if (npcController != null)
            npcController.RequestLookAt(_playerHead.position);
    }

    void OnTriggerExit(Collider other)
    {
        var cam = other.GetComponentInParent<Camera>();
        if (cam == null) return;

        _inside = false;
        _playerHead = null;

        if (promptCanvas != null) promptCanvas.enabled = false;

        if (npcController != null)
            npcController.RequestStopLookAt();
    }

    void Update()
    {
        if (!_inside || _playerHead == null) return;

        // Keep look target updated (player moves)
        if (npcController != null)
            npcController.RequestLookAt(_playerHead.position);

        // Optional: make prompt face the camera
        if (promptCanvas != null)
        {
            Transform t = promptFaceTarget != null ? promptFaceTarget : promptCanvas.transform;
            Vector3 dir = t.position - _playerHead.position;
            if (dir.sqrMagnitude > 0.0001f)
                t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // Simple “talk” trigger
        // In Editor: E key works. In VR you can later replace this with Oculus Interaction event.
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryTalk();
        }

        if (autoTalkOnce && !_talkedOnce)
        {
            float d = Vector3.Distance(_playerHead.position, npcRoot.position);
            if (d <= talkDistance)
            {
                _talkedOnce = true;
                TryTalk();
            }
        }
    }

    private void TryTalk()
    {
        // Local-only UI / audio / subtitle
        Debug.Log("NPC: Hi! I can help you with the experiment.");

        // Here you can:
        // - open dialogue UI
        // - play audio clip
        // - highlight objects (pump, knob)
    }
}
