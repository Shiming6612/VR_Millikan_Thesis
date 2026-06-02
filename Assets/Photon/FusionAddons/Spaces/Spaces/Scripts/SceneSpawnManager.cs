using Fusion.XR.Shared;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Fusion.Addons.Spaces
{

    [System.Serializable]
    struct SpaceSpawnPoint
    {
        public string spaceId;
        public Transform spawnPosition;
        public float randomRadius;
    }

    [System.Serializable]
    public struct SpawnPosition
    {
        public Vector3 position;
        public Quaternion rotation;

    }

    /**
     * Change the default behavior of the RandomizeStartPosition components, to handle specific spawn position needs, either for:
     * - reconnection (the user should stay in place)
     * - coming from another scene: the scenes SceneSpawnPoint can define where we should appear when coming from another scene 
     */
    [DefaultExecutionOrder(0)]
    public class SceneSpawnManager : MonoBehaviour
    {
        public const string SETTINGS_RECONNECTION_POSITION = "ReconnectionPosition";
        public static string PreviousSpaceId { get; private set; }
        [SerializeField] private List<SpaceSpawnPoint> spawnPoints = new List<SpaceSpawnPoint>();

        public List<RandomizeStartPosition> startPositionHandlers = new List<RandomizeStartPosition>();

        private string currentSpaceId;

        private void OnDestroy()
        {
            // Store the space id to allow determining where we came from in the next SceneSpawnmanager Awake()
            PreviousSpaceId = currentSpaceId;
        }

        private void Awake()
        {
            // By defualt, we use the scene name as the currentSpaceId. Other component can use RegisterCurrentSpaceId to update that before it is used on OnDestroy
            currentSpaceId = SceneManager.GetActiveScene().name;
            var previousSpaceId = PreviousSpaceId;
            PreviousSpaceId = null;

            if (startPositionHandlers.Count == 0)
            {
                startPositionHandlers = new List<RandomizeStartPosition>(FindObjectsByType<RandomizeStartPosition>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            }

            // Check if we should restore a position after a reconnection request
            if (RestorePositionOnReload()) return;

            // Check if we should restore a position due to the scene we come from
            if (RestoreSceneTransitionPosition(previousSpaceId)) return;
        }

        // use this to change the id of the current scene. Will be stored in the static PreviousSpaceId during the OnDestroy
        public void RegisterCurrentSpaceId(string spaceId)
        {
            currentSpaceId = spaceId;
        }

        public void RegisterSpawnPosition(string spaceId, Transform position, float randomRadius)
        {
            foreach (var spawnPoint in spawnPoints)
            {
                if (spawnPoint.spaceId == spaceId)
                {
                    Debug.LogError("A spawn position is already set for this scene " + spaceId + ".");
                    return;
                }
            }
            spawnPoints.Add(new SpaceSpawnPoint { spaceId = spaceId, spawnPosition = position, randomRadius = randomRadius });
        }

        /**
         * Store in PlayerPrefs a position, that will be reused once 
         */
        public void SaveReconnectionPosition(Vector3 position, Quaternion rotation)
        {
            PlayerPrefs.SetString(SETTINGS_RECONNECTION_POSITION, JsonUtility.ToJson(new SpawnPosition { position = position, rotation = rotation }));
            PlayerPrefs.Save();
        }

        public SpawnPosition? ConsumeReconnectionPosition()
        {
            var json = PlayerPrefs.GetString(SETTINGS_RECONNECTION_POSITION, null);
            if (json == null || json == "") return null;
            PlayerPrefs.DeleteKey(SETTINGS_RECONNECTION_POSITION);
            PlayerPrefs.Save();
            return JsonUtility.FromJson<SpawnPosition>(json);
        }

        bool RestorePositionOnReload()
        {
            var spawnPositionOpt = ConsumeReconnectionPosition();
            if (spawnPositionOpt != null)
            {
                // Reloading a scene: we restore the position
                var spawnPosition = spawnPositionOpt.GetValueOrDefault();
                var reconnectionPosition = new GameObject("ReconnectionPosition");
                reconnectionPosition.transform.position = spawnPosition.position;
                reconnectionPosition.transform.rotation = spawnPosition.rotation;
                ChangeStartPosition(reconnectionPosition.transform, 0);
                return true;
            }
            return false;
        }

        bool RestoreSceneTransitionPosition(string previousSpaceId)
        {
            Debug.Log($"Previous space id :  {previousSpaceId}");

            foreach (SpaceSpawnPoint spawnPoint in spawnPoints)
            {
                if (previousSpaceId == spawnPoint.spaceId)
                {
                    Debug.Log($"Previous space was {spawnPoint.spaceId} => Set Spawn Position = {spawnPoint.spawnPosition.position}");
                    ChangeStartPosition(spawnPoint.spawnPosition, spawnPoint.randomRadius);
                    return true;
                }
            }
            return false;
        }

        void ChangeStartPosition(Transform startTransform, float randomRadius)
        {
            foreach (var startPositionHandler in startPositionHandlers)
            {
                startPositionHandler.startCenterPosition = startTransform;
                startPositionHandler.randomRadius = randomRadius;
                startPositionHandler.FindStartPosition();
            }
        }
    }
}
