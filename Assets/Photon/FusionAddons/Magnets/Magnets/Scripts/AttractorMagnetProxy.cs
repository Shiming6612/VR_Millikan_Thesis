using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XRShared.GrabbableMagnet
{
    public class AttractorMagnetProxy : MonoBehaviour, IAttractorMagnet
    {
        public IAttractorMagnet target;
        [SerializeField]
        GameObject targetObject;

        private void Awake()
        {
            if (targetObject == null)
            {
                Debug.LogError("targetObject is not defined in MagnetProxy");
                return;
            }
            target = targetObject.GetComponent<IAttractorMagnet>();
            if (target == null)
            {
                Debug.LogError("target is not defined in MagnetProxy");
                return;
            }

        }

        #region IAttractorMagnet implementation : nothing is implemented only the target will handle the interface
        public AlignmentAxisAsAttractor AlignmentAxisAsAttractor => throw new System.NotImplementedException();

        public AttractedMagnetMove AttractedMagnetMove { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public AttractedMagnetRotation AttractedMagnetRotation { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public IMagnetConfigurator MagnetConfigurator { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public MagnetCoordinator MagnetCoordinator => throw new System.NotImplementedException();

        public List<string> Tags => throw new System.NotImplementedException();

        public Vector3 SnapTargetPosition(Vector3 position)
        {
            throw new System.NotImplementedException();
        }
        #endregion
    }
}
