using UnityEngine;

namespace Fusion.Addons.Containment
{
    public class ContainmentManager : MonoBehaviour
    {
        public bool disableContainerParenting = false;
        public NetworkObject containerPrefab = null;
        public bool deepDebug = false;

        #region Shared instance
        static ContainmentManager _sharedInstance;

        public static ContainmentManager SharedInstance
        {
            get
            {
                if (_sharedInstance == null) Debug.LogError("Missing ContainmentManager");
                return _sharedInstance;
            }
        }

        public static bool SharedInstanceAvailable
        {
            get
            {
                return _sharedInstance != null;
            }
        }


        private void Awake()
        {
            if (_sharedInstance != null)
            {
                Debug.LogError("Multiple ContainmentManager is not authorized");
            }
            _sharedInstance = this;
        }

        private void OnDestroy()
        {
            if (_sharedInstance == this) _sharedInstance = null;
        }
        #endregion
    }

}
