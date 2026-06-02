using TMPro;
using UnityEngine;

public class SimplePortalSpaceDescriptionLoader : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI spaceTitle;
    [SerializeField] private TextMeshProUGUI spaceDescription;
    [SerializeField] private SimpleSpaceLoader spaceLoader;

    // Start is called before the first frame update
    void Start()
    {
        if (!spaceTitle)
            Debug.LogError("spaceTitle has not been set !");

        if (!spaceDescription)
            Debug.LogError("spaceDescription has not been set !");

        if (!spaceLoader)
            spaceLoader = GetComponent<SimpleSpaceLoader>();
        if (!spaceLoader)
            Debug.LogError("spaceLoader has not been set !");

        if (spaceLoader)
        {
            if (spaceTitle)
            {
                spaceTitle.text = spaceLoader.SpaceDescription.spaceName;
            }
            if (spaceDescription)
            {
                spaceDescription.text = spaceLoader.SpaceDescription.spaceDescription;
            }
        }
    }
}

