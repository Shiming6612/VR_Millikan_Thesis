using Fusion;
using Fusion.Addons.Spaces;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion.XR.Shared.Core;


/**
 * 
 * SpaceLoader is in charge to load a new scene when the player collides with the box collider
 * 
 **/
[DefaultExecutionOrder(-1)]
public class SimpleSpaceLoader : MonoBehaviour
{
    [Header("Target space")]
    [SerializeField] private string spaceId;
    [SerializeField] private SpaceDescription spaceDescription;

    public SpaceDescription SpaceDescription
    {
        get
        {
            LoadSpaceDescriptionInfo();
            return spaceDescription;
        }
    }

    string SceneName => (spaceDescription != null) ? spaceDescription.sceneName : spaceId;

    [Header("Automatically set")]
    [SerializeField] private NetworkRunner runner;

    // Position to spawn at when we come back from this scene
    [SerializeField] private Transform returnPosition;
    [SerializeField] private float returnRadius = 1f;

    private void Awake()
    {
        LoadSpaceDescriptionInfo();

        if (returnPosition == null)
            returnPosition = transform;

        SceneSpawnManager spawnManager = FindAnyObjectByType<SceneSpawnManager>(FindObjectsInactive.Include);

        if (spawnManager)
            spawnManager.RegisterSpawnPosition(spaceId, returnPosition, returnRadius);
    }

    void LoadSpaceDescriptionInfo()
    {
        if (spaceDescription && string.IsNullOrEmpty(spaceId))
            spaceId = spaceDescription.spaceId;

        if (spaceDescription == null && !string.IsNullOrEmpty(spaceId))
            spaceDescription = SpaceDescription.FindSpaceDescription(spaceId);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<ILateralizedHardwareRigPart>() != null)
            SwitchScene();
    }

    private async void SwitchScene()
    {
        await runner.Shutdown(true);
        Debug.Log("Loading new scene " + SceneName);
        SpaceRoom.RegisterSpaceRequest(spaceDescription);
        SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
    }
}
