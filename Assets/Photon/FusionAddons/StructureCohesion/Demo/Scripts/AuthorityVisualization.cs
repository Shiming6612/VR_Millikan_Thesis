using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.Tools
{
    public class AuthorityVisualization : NetworkBehaviour
    {
        [SerializeField]
        Material stateAuthorityMaterial;
        [SerializeField]
        Material proxyMaterial;
        [Tooltip("target renderers")]
        [SerializeField]
        List<Renderer> renderers = new List<Renderer>();
        Material defaultMaterial;

        bool wasStateAuthority = false;
        bool init = false;
        private void Awake()
        {
            if(renderers.Count == 0)
            {
                renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
            }
            if(renderers.Count > 0)
            {
                defaultMaterial = renderers[0].material;
                if (stateAuthorityMaterial == null) stateAuthorityMaterial = defaultMaterial;
                if (proxyMaterial == null) proxyMaterial = defaultMaterial;
            }
        }

        public override void Render()
        {
            base.Render();
            if (init == false || wasStateAuthority != Object.HasStateAuthority)
            {
                init = true;
                wasStateAuthority = Object.HasStateAuthority;
                UpdateMaterial();
            }
        }

        void UpdateMaterial() { 
            foreach(var r in renderers)
            {
                r.material = Object.HasStateAuthority ? stateAuthorityMaterial : proxyMaterial;
            }
        }

        [EditorButton("Take authority")]
        void TakeAuthority()
        {
            Object.RequestStateAuthority();
        }
    }

}
