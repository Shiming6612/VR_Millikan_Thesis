using UnityEngine;
using Fusion.XR.Shared.Tools;

namespace Fusion.Addons.DynamicAudioGroup
{
    /// <summary>
    /// DynamicAudioGroupMember that can be colocalized in the same real life room:
    ///  in this case, the audio is cut if they are close to each other
    /// </summary>
    public class ColocDynamicAudioGroupMember : DynamicAudioGroupMember
    {
#if PHOTON_VOICE_AVAILABLE

        public string IRLRoomId => colocalizationRoomProvider.IRLRoomId;
        public IColocalizationRoomProvider colocalizationRoomProvider;

        [Header("Colocalization settings")]
        [Tooltip("")]
        public float maxColocalizedProximityDistance = 6;
        float maxColocalizedProximityDistanceSqr;
        protected override void Awake()
        {
            base.Awake();
            if (colocalizationRoomProvider == null)
            {
                colocalizationRoomProvider = GetComponent<IColocalizationRoomProvider>();
            }
            if (colocalizationRoomProvider == null)
            {
                Debug.LogError("[Error] Missing IColocalizationRoomProvider");
            }
            maxColocalizedProximityDistanceSqr = maxColocalizedProximityDistance * maxColocalizedProximityDistance;
        }

        protected override void CheckProximityWithOtherMember(DynamicAudioGroupMember member)
        {
            if (member is ColocDynamicAudioGroupMember colocalizableMember && this.colocalizationRoomProvider.IRLRoomId == colocalizableMember.colocalizationRoomProvider.IRLRoomId)
            {
                // Members are in the same real life detected room (colocalized)
                if (DistanceSqr(member) < maxColocalizedProximityDistanceSqr)
                {
                    // In close range: they should hear each other in real life, shutting down voice between them
                    StopListeningToMember(member);
                    return;
                }
            }

            // Not in same room (or too far from each other): resuming normal dynamic audio group membership logic
            base.CheckProximityWithOtherMember(member);
        }
#endif
    }
}
