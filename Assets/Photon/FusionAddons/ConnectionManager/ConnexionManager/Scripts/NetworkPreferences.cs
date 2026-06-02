#if FUSION_2_1_OR_NEWER
using Photon.Client;
#else
using ExitGames.Client.Photon; 
# endif
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fusion.Addons.ConnectionManagerAddon
{
    /// <summary>
    /// Load network preferences from settings. If fusionBootstrap/connectionManager are not set, should be place on the gameobject of the object handling connection
    /// </summary>
    [DefaultExecutionOrder(-100_000)]
    public class NetworkPreferences : MonoBehaviour
    {
        [SerializeField] FusionBootstrap fusionBootstrap;
        [SerializeField] ConnectionManager connectionManager;

        const string PHOTON_SETTINGS_PROTOCOL_PREF = "PHOTON_SETTINGS_PROTOCOL_PREF";
        const string PHOTON_SETTINGS_REGION_PREF = "PHOTON_SETTINGS_REGION_PREF";
        const string PHOTON_SETTINGS_SERVER_PREF = "PHOTON_SETTINGS_SERVER_PREF";
        const string PHOTON_SETTINGS_PORT_PREF = "PHOTON_SETTINGS_PORT_PREF";
        const string PHOTON_SETTINGS_ROOMNAME_PREF = "PHOTON_SETTINGS_ROOMNAME_PREF";

        string initialRegion;
        string initialServer;
        string initialRoom;
        int initialPort;
        ConnectionProtocol initialProtocol;

        #region Preference loading logic
        private void Awake()
        {
            if (fusionBootstrap == null) fusionBootstrap = GetComponent<FusionBootstrap>();
            if (connectionManager == null) connectionManager = GetComponent<ConnectionManager>();
            BackupSettings();
            LoadPreferences();
        }

        private void OnDestroy()
        {
            // We restore PhotonAppSettings
            ReapplyBackedSettings();
        }

        void LoadPreferences()
        {
            if (PlayerPrefs.HasKey(PHOTON_SETTINGS_PROTOCOL_PREF))
            {
                ConnectionProtocol protocol = (ConnectionProtocol)PlayerPrefs.GetInt(PHOTON_SETTINGS_PROTOCOL_PREF);
                PhotonAppSettings.Global.AppSettings.Protocol = protocol;
            }
            if (PlayerPrefs.HasKey(PHOTON_SETTINGS_REGION_PREF))
            {
                string region = PlayerPrefs.GetString(PHOTON_SETTINGS_REGION_PREF);
                PhotonAppSettings.Global.AppSettings.FixedRegion = region;
            }
            if (PlayerPrefs.HasKey(PHOTON_SETTINGS_SERVER_PREF))
            {
                string server = PlayerPrefs.GetString(PHOTON_SETTINGS_SERVER_PREF);
                PhotonAppSettings.Global.AppSettings.Server = server;
            }
            if (PlayerPrefs.HasKey(PHOTON_SETTINGS_PROTOCOL_PREF))
            {
                int port = PlayerPrefs.GetInt(PHOTON_SETTINGS_PORT_PREF);
                PhotonAppSettings.Global.AppSettings.Port = (ushort)port;
            }
            if (PlayerPrefs.HasKey(PHOTON_SETTINGS_ROOMNAME_PREF))
            {
                string roomName = PlayerPrefs.GetString(PHOTON_SETTINGS_ROOMNAME_PREF);
                if (connectionManager)
                {
                    connectionManager.roomName = roomName;
                }
                if (fusionBootstrap)
                {
                    fusionBootstrap.DefaultRoomName = roomName;
                }
            }
        }

        void BackupSettings()
        {
            // PhotonAppSettings
            initialProtocol = PhotonAppSettings.Global.AppSettings.Protocol;
            initialRegion = PhotonAppSettings.Global.AppSettings.FixedRegion;
            initialServer = PhotonAppSettings.Global.AppSettings.Server;
            initialPort = PhotonAppSettings.Global.AppSettings.Port;

            // Connection handling
            if (connectionManager)
            {
                initialRoom = connectionManager.roomName;
            }
            if (fusionBootstrap)
            {
                initialRoom = fusionBootstrap.DefaultRoomName;
            }
        }

        [ContextMenu("ReapplyBackedSettings")]
        public void ReapplyBackedSettings()
        {
            // PhotonAppSettings
            PhotonAppSettings.Global.AppSettings.Protocol = initialProtocol;
            PhotonAppSettings.Global.AppSettings.FixedRegion = initialRegion;
            PhotonAppSettings.Global.AppSettings.Server = initialServer;
            PhotonAppSettings.Global.AppSettings.Port = (ushort)initialPort;

            // Connection handling is set in the scene, no need to restore it if the scene is reloaded. Doing it anyway in case of other usages
            if (connectionManager)
            {
                connectionManager.roomName = initialRoom;
            }
            if (fusionBootstrap)
            {
                fusionBootstrap.DefaultRoomName = initialRoom;
            }
        }

        public void DeleteSetting(string settingKey)
        {
            PlayerPrefs.DeleteKey(settingKey);
            PlayerPrefs.Save();
        }
        #endregion

        public void ReloadScene()
        {

            if (fusionBootstrap)
            {
                // Fusion bootstrap's ShutdownAll also reloads initial scene
                fusionBootstrap.ShutdownAll();
            } 
            else
            {
                Scene scene = SceneManager.GetActiveScene();
                Debug.LogError("Active scene:" + scene.name);
                var runner = NetworkRunner.GetRunnerForGameObject(gameObject);
                runner.Shutdown();
                Destroy(runner);
                Destroy(gameObject);
                SceneManager.LoadScene(scene.name);
            }

        }

        public void ChangeProtocol(ConnectionProtocol protocol, bool reloadScene = true)
        {
            PhotonAppSettings.Global.AppSettings.Protocol = protocol;
            PlayerPrefs.SetInt(PHOTON_SETTINGS_PROTOCOL_PREF, (int)protocol);
            PlayerPrefs.Save();
            if (reloadScene) ReloadScene();
        }

        [ContextMenu("ChangeProtocolToTcp")]
        public void ChangeProtocolToTcp(bool reloadScene = true)
        {
            ChangeProtocol(ConnectionProtocol.Tcp, reloadScene);
        }

        [ContextMenu("ChangeProtocolToUdp")]
        public void ChangeProtocolToUdp(bool reloadScene = true)
        {
            ChangeProtocol(ConnectionProtocol.Udp, reloadScene);
        }

        [ContextMenu("RestoreProtocol")]
        public void DeleteProtocolPreference(bool reloadScene = true)
        {
            DeleteSetting(PHOTON_SETTINGS_PROTOCOL_PREF);
            PhotonAppSettings.Global.AppSettings.Protocol = initialProtocol;
            if (reloadScene) ReloadScene();
        }

        public void ChangeRegion(string region,bool reloadScene = true)
        {
            PhotonAppSettings.Global.AppSettings.FixedRegion = region;
            PlayerPrefs.SetString(PHOTON_SETTINGS_REGION_PREF, region);
            PlayerPrefs.Save();
            if (reloadScene) ReloadScene();
        }

        [ContextMenu("RestoreRegion")]
        public void DeleteRegionPreference(bool reloadScene = true)
        {
            DeleteSetting(PHOTON_SETTINGS_REGION_PREF);
            PhotonAppSettings.Global.AppSettings.FixedRegion = initialRegion;
            if (reloadScene) ReloadScene();
        }

        public void ChangeServer(string server, bool reloadScene = true)
        {
            PhotonAppSettings.Global.AppSettings.Server = server;
            PlayerPrefs.SetString(PHOTON_SETTINGS_SERVER_PREF, server);
            PlayerPrefs.Save();
            if (reloadScene) ReloadScene();
        }

        [ContextMenu("RestoreServer")]
        public void DeleteServerPreference(bool reloadScene = true)
        {
            DeleteSetting(PHOTON_SETTINGS_SERVER_PREF);
            PhotonAppSettings.Global.AppSettings.Server = initialServer;
            if (reloadScene) ReloadScene();
        }

        public void ChangePort(int port, bool reloadScene = true)
        {
            PhotonAppSettings.Global.AppSettings.Port = (ushort)port;
            PlayerPrefs.SetInt(PHOTON_SETTINGS_PORT_PREF, port);
            PlayerPrefs.Save();
            if (reloadScene) ReloadScene();
        }

        [ContextMenu("RestorePort")]
        public void DeletePortPreference(bool reloadScene = true)
        {
            DeleteSetting(PHOTON_SETTINGS_PORT_PREF);
            PhotonAppSettings.Global.AppSettings.Port = (ushort)initialPort;
            if (reloadScene) ReloadScene();
        }

        public void ChangeRoomName(string roomName, bool reloadScene = true)
        {
            if (connectionManager)
            {
                connectionManager.roomName = roomName;
            }
            if (fusionBootstrap)
            {
                fusionBootstrap.DefaultRoomName = roomName;
            }
            PlayerPrefs.SetString(PHOTON_SETTINGS_ROOMNAME_PREF, roomName);
            PlayerPrefs.Save();
            if (reloadScene) ReloadScene();
        }


        public string GetRoomName()
        {
            string roomName = null;
            if (connectionManager)
            {
                roomName = connectionManager.roomName;
            }
            if (fusionBootstrap)
            {
                roomName = fusionBootstrap.DefaultRoomName;
            }
            return roomName;
        }

        [ContextMenu("RestoreRoomName")]
        public void DeleteRoomNamePreference(bool reloadScene = true)
        {
            DeleteSetting(PHOTON_SETTINGS_ROOMNAME_PREF);
            if (connectionManager)
            {
                connectionManager.roomName = initialRoom;
            }
            if (fusionBootstrap)
            {
                fusionBootstrap.DefaultRoomName = initialRoom;
            }
            if (reloadScene) ReloadScene();
        }
    }
}
