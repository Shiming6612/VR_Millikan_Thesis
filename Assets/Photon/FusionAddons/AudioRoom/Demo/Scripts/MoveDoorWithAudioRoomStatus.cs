using Fusion.Addons.AudioRoomAddon;
using UnityEngine;

public class MoveDoorWithAudioRoomStatus : MonoBehaviour
{
    [SerializeField] private AudioDoor audioDoor;
    [SerializeField] private GameObject doorMeshRenderer;
    [SerializeField] private Transform closePosition;
    [SerializeField] private Transform openPosition;
#if PHOTON_VOICE_AVAILABLE

    private void Awake()
    {
        if (!audioDoor)
            audioDoor = GetComponent<AudioDoor>();
        if (audioDoor)
            audioDoor.OnStatusChange.AddListener(DoorStatusChanged);
    }

    private void DoorStatusChanged()
    {
        Debug.Log("DoorStatusChanged");
        if (doorMeshRenderer)
        {
            if (audioDoor.IsOpened)
            {
                doorMeshRenderer.transform.position = openPosition.position;
            }
            else
            {
                doorMeshRenderer.transform.position = closePosition.position;
            }
        }
    }
#endif
}