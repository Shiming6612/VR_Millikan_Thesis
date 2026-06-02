using Fusion.Addons.SubscriberRegistry;
using TMPro;
using UnityEngine;

public class SampleRegistryVisual : MonoBehaviour
{
    IRegistry sampleRegistry;
    [SerializeField] TextMeshProUGUI textMeshPro;
    [SerializeField] MeshRenderer m_Renderer;
    [SerializeField] Material materialWhenIsAvailable;
    [SerializeField] Material materialWhenNotAvailable;

    // Start is called before the first frame update
    void Start()
    {
        if(sampleRegistry == null)
            sampleRegistry = GetComponentInParent<IRegistry>();

        if(m_Renderer == null)
            m_Renderer = GetComponent<MeshRenderer>();

        m_Renderer.material = materialWhenNotAvailable;

        if(textMeshPro == null)
            textMeshPro = GetComponentInChildren<TextMeshProUGUI>();

        textMeshPro.text = transform.parent.name;
    }



    bool previousIsAvailable = false;
    // Update is called once per frame
    void Update()
    {
        if(sampleRegistry != null && sampleRegistry.Object && sampleRegistry.Object.IsValid)
        {
            if (previousIsAvailable != sampleRegistry.IsAvailable)
            {
                previousIsAvailable = sampleRegistry.IsAvailable;
                if (sampleRegistry.IsAvailable)
                {
                    m_Renderer.material = materialWhenIsAvailable;
                }
                else
                {
                    m_Renderer.material = materialWhenNotAvailable;
                }
            }
        }
        
    }
}
