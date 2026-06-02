using Fusion.Addons.ConnectionManagerAddon;
using Fusion.XR.Shared;
using Fusion.XR.Shared.Core.Interaction;
using Fusion.XR.Shared.Locomotion;
using Fusion.XR.Shared.Rig;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fusion.Addons.Spaces
{

    /**
     * 
     * Change the Fusion room name to manage group sessions
     * 
     * Build a room name with 4 parts:
     * - appId: base of all room names, common throughout the application
     * - spaceId: id associated to the current position (usually, related to the scene)
     * - groupId: id for the current party in the room (or empty for a common public room)
     * - instanceId: additional id to handle things like load balancing for crowded rooms
     * 
     * Prevent the ConnexionManager to start normally to be sure that the room name has been changed properly
     * 
     **/
    public class SpaceRoom : MonoBehaviour
    {
        public const string SETTINGS_GROUPID = "SPACES_NAVIGATION_GROUPID";
        public const string PUBLIC_GROUPID = "";
        public const int MINIMAL_GROUPID_LENGTH = 4;

        public static bool IsValidPrivateGroupId(string groupId)
        {
            return groupId != null && groupId != PUBLIC_GROUPID && groupId.Length >= MINIMAL_GROUPID_LENGTH;
        }
        public static bool IsValidPublicGroupId(string groupId)
        {
            return groupId == PUBLIC_GROUPID;
        }

        [Header("Space info")]
        [Tooltip("The actual Id of this room. If not set, the scene name is used")]
        public string spaceId = "";
        public SpaceDescription spaceDescription = null;

        [Header("Network room additional info")]
        [Tooltip("If not set, the connexion manager room name will be used")]
        public string appId = "";
        [Tooltip("Define a private group, or a shard. Leave empty for a common public room")]
        public string groupId = "";
        [Tooltip("Can be used for load balancing for crowded rooms, ...")]
        public string instanceId = "";
        [Tooltip("If true, the app build version will be added to app id, to avoid ")]
        public bool addVersionToAppId = true;


        [Header("Connection")]
        public ConnectionManager connectionManager;
        public ConnectionManager.ConnectionCriterias connectionCriterias = ConnectionManager.ConnectionCriterias.SessionProperties;
        public SceneSpawnManager sceneSpawnManager;
        public RigInfo rigInfo;
        bool connectOnStart = false;
        public string RoomName => string.Join("-", new string[]{appId, spaceId, groupId, instanceId}.Where(s => !string.IsNullOrEmpty(s)));

        public static List<string> SpaceIdHistory = new List<string>();
        public static string CurrentSpaceId = null;

        private void Awake()
        {
            if (sceneSpawnManager == null) sceneSpawnManager = GetComponentInChildren<SceneSpawnManager>(true);
            if (sceneSpawnManager == null) sceneSpawnManager = FindAnyObjectByType<SceneSpawnManager>(FindObjectsInactive.Include);
            if (connectionManager == null) connectionManager = GetComponent<ConnectionManager>();

            // SpaceId
            LoadSpaceInfo();
            
            // GrouId
            LoadGroupId();

            // AppId
            LoadAppId();

            if (connectionManager && connectionManager.connectOnStart)
            {
                connectOnStart = true;
                connectionManager.connectOnStart = false;
                connectionManager.connectionCriterias = connectionCriterias;
                if ((connectionCriterias & ConnectionManager.ConnectionCriterias.RoomName) != 0)
                {
                    connectionManager.roomName = RoomName;
                }
                if ((connectionCriterias & ConnectionManager.ConnectionCriterias.SessionProperties) != 0)
                {
                    connectionManager.sessionProperties = new Dictionary<string, SessionProperty> {
                        { "appId", appId },
                        { "spaceId", spaceId },
                        { "groupId", groupId }
                    };
                }
            }

            if (rigInfo == null && connectionManager) rigInfo = connectionManager.GetComponentInChildren<RigInfo>();
            CurrentSpaceId = spaceId;

        }

        private void OnDestroy()
        {
            if (SpaceIdHistory.Count == 0 || SpaceIdHistory[SpaceIdHistory.Count - 1] != CurrentSpaceId)
            {
                SpaceIdHistory.Add(spaceId);
            }
        }

        private async void Start()
        {
            if (connectOnStart)
            {
                await connectionManager.Connect();
            }

            // We warn the spawn manager of the space id if the space id is not the same as the scene name, as it will later use it to detect where we come from
            sceneSpawnManager.RegisterCurrentSpaceId(spaceId);
        }

        static SpaceDescription IncomingSpace = null;
        public static void RegisterSpaceRequest(SpaceDescription spaceDescription)
        {
            IncomingSpace = spaceDescription;
        }

        void LoadSpaceInfo()
        {
            if (IncomingSpace)
            {
                // The SpaceLoader from previous space forced a precise space desciption
                spaceDescription = IncomingSpace;
                IncomingSpace = null;
            }
            if (string.IsNullOrEmpty(spaceId) && spaceDescription)
            {
                spaceId = spaceDescription.spaceId;
            }
            if (string.IsNullOrEmpty(spaceId))
            {
                spaceId = SceneManager.GetActiveScene().name;
            }
            if (spaceDescription == null)
            {
                spaceDescription = SpaceDescription.FindSpaceDescription(spaceId);
            }
            if (spaceDescription && spaceDescription.spaceId != spaceId)
            {
                Debug.LogError("Space description does not match space id");
            }
        }

        void LoadGroupId()
        {
            groupId = PlayerPrefs.GetString(SETTINGS_GROUPID);
        }

        void LoadAppId()
        {
            if (connectionManager && string.IsNullOrEmpty(appId))
            {
                appId = connectionManager.roomName;
            }
            if (addVersionToAppId)
            {
                appId += Application.version;
            }
        }

        async void ReloadScene()
        {
            Debug.LogError("Disconnecting...");
            // The app manager might detect a network disconnection: we prevent him from thinking there is an error
            foreach(var appManager in FindObjectsByType<ApplicationLifeCycleManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                appManager.OnApplicationQuitRequest();
            }

            await connectionManager.runner.Shutdown();
            Debug.LogError("Disconnected");
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }

        IEnumerator Disconnection()
        {
            if (!rigInfo) yield break;
            if (sceneSpawnManager)
            {
                sceneSpawnManager.SaveReconnectionPosition(rigInfo.localHardwareRig.transform.position, rigInfo.localHardwareRig.transform.rotation);
            }
            else
            {
                Debug.LogError("Unable to save reconnection position: no sceneSpawnManager in scene");
            }

            // To  prevent any teleport while fading out for disconenction, we disable the locomotion handler
            var locomotion = rigInfo.localHardwareRig.gameObject.GetComponentInChildren<RigLocomotion>();
            if (locomotion) locomotion.enabled = false;


            if (rigInfo.localHardwareRig.Headset is IFadeable fadeable && fadeable.Fader != null)
            {
                yield return fadeable.Fader.FadeIn();
                // We make sure we see the black screen for one frame
                yield return null;
            }
            ReloadScene();
        }

        /**
         * Change the group id (for instance to go from public space to private space)
         * Disconnect the user and reload the scene
         * Save the reconnection position, and it will be restored in scene (if a spawn manager is available)
         */
        public void ChangeGroupId(string gid)
        {
            PlayerPrefs.SetString(SETTINGS_GROUPID, gid);
            PlayerPrefs.Save();
            StartCoroutine(Disconnection());
        }

#if UNITY_EDITOR
        [ContextMenu("Save current groupId")]
        void SaveGroupId(){
            ChangeGroupId(groupId);
        }        
        
        [ContextMenu("Reset groupId")]
        void ResetGroupId(){
            PlayerPrefs.SetString(SETTINGS_GROUPID, "");
            PlayerPrefs.Save();
        }
#endif
    }
}

