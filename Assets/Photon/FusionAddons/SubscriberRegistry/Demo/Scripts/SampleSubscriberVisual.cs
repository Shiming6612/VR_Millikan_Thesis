using Fusion.Addons.SubscriberRegistry;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SubscriberVisual : MonoBehaviour, IDetailedRegistrationListener<SampleSubscriber>
{

    [SerializeField] SampleSubscriber sampleSubscriber;
    [SerializeField] TextMeshProUGUI textMeshPro;
    [SerializeField] MeshRenderer m_Renderer;
    [SerializeField] Material materialWhenIsAvailable;
    [SerializeField] Material materialWhenNotAvailable;

    [SerializeField] GameObject LineRendererPrefab;

    // Start is called before the first frame update
    void Start()
    {
        if (sampleSubscriber == null)
            sampleSubscriber = GetComponentInParent<SampleSubscriber>();
        sampleSubscriber.RegisterListener(this, true);

        if (m_Renderer == null)
            m_Renderer = GetComponent<MeshRenderer>();

        m_Renderer.material = materialWhenNotAvailable;

        if (textMeshPro == null)
            textMeshPro = GetComponentInChildren<TextMeshProUGUI>();

        textMeshPro.text = transform.parent.name;
    }



    bool previousIsAvailable = false;
    // Update is called once per frame
    void Update()
    {
        if (sampleSubscriber && sampleSubscriber.Object && sampleSubscriber.Object.IsValid)
        {
            if (previousIsAvailable != sampleSubscriber.IsAvailable)
            {
                previousIsAvailable = sampleSubscriber.IsAvailable;
                if (sampleSubscriber.IsAvailable)
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

    public Dictionary<Registry<SampleSubscriber>, LineRenderer> registryLineRenderers = new Dictionary<Registry<SampleSubscriber>, LineRenderer>();

    #region IRegistrationListener

    // Called when registry is available, before checking if we are available to register
    public void OnAvailableRegistryFound(Registry<SampleSubscriber> registry, Subscriber<SampleSubscriber> subscriber)
    {
        if (registryLineRenderers.ContainsKey(registry) == false)
        {
            var lineRendererObj = Instantiate(LineRendererPrefab, new Vector3(0, 0, 0), Quaternion.identity);
            var lineRenderer = lineRendererObj.GetComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, registry.transform.position);
            registryLineRenderers.Add(registry, lineRenderer);
            lineRenderer.material = materialWhenNotAvailable;
        }
    }
    public void OnSubscriberRegistration(Registry<SampleSubscriber> registry, Subscriber<SampleSubscriber> subscriber)
    {
        if (registryLineRenderers.ContainsKey(registry))
        {
            registryLineRenderers.TryGetValue(registry, out LineRenderer lineRenderer);
            if (lineRenderer != null)
            {
                lineRenderer.material = materialWhenIsAvailable;
            }
        }

    }
    public void OnSubscriberUnregistration(Registry<SampleSubscriber> registry, Subscriber<SampleSubscriber> subscriber)
    {
        if (registryLineRenderers.ContainsKey(registry))
        {
            registryLineRenderers.TryGetValue(registry, out LineRenderer lineRenderer);
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
                lineRenderer.material = materialWhenNotAvailable;
            }
            registryLineRenderers.Remove(registry);
        }
    }
    #endregion

}
