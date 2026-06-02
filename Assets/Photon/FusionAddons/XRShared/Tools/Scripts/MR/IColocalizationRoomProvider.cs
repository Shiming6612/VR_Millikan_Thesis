using UnityEngine;

namespace Fusion.XR.Shared.Tools {
    /// <summary>
    /// Interface for colocalization scenario, to provide the id of the current life room for an user
    /// </summary>
    public interface IColocalizationRoomProvider
    {
        public string IRLRoomId { get; }
    }
}
