using UnityEngine;

namespace Fusion.Addons.Spaces
{
    /**
     * 
     * Space parameters has been grouped into the SpaceDescription scriptable object :
     *      - `spaceId` : technical data field to uniquely identify each space.
     *      - `sceneName` : the Unity scene that must be loaded when a player use the portal.
     *      - `spaceName` : the name of the space. It is a not a technical data. It is loaded on the portal panel.
     *      - `spaceDescription` : a description of the space. It is loaded on the portal panel to explain the purpose of the space to the user.
     *      - `spaceParam` : optionnal technical datas. For future use.
     *      - `spaceSecondaryParam` : optionnal technical datas. For future use.
     * 
     **/

    [CreateAssetMenu(fileName = "Space", menuName = "Fusion Addons/Space", order = 1)]
    public class SpaceDescription : ScriptableObject
    {
        public string spaceId;
        public string sceneName;
        public string spaceName;
        [TextArea]
        public string spaceDescription;
        [TextArea]
        public string spaceParam;
        [TextArea]
        public string spaceSecondaryParam;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(spaceId) && string.IsNullOrEmpty(sceneName)) spaceId = name;
            if (string.IsNullOrEmpty(spaceId) && !string.IsNullOrEmpty(sceneName)) spaceId = sceneName;
            if (string.IsNullOrEmpty(sceneName) && !string.IsNullOrEmpty(spaceId)) sceneName = spaceId;
            if (string.IsNullOrEmpty(spaceName) && !string.IsNullOrEmpty(spaceId)) spaceName = spaceId;
        }

        public static SpaceDescription FindSpaceDescription(string name)
        {
            SpaceDescription spaceDescription = Resources.Load<SpaceDescription>(name);
            if (spaceDescription == null) spaceDescription = Resources.Load<SpaceDescription>("Spaces/" + name);
            return spaceDescription;
        }
    }
}