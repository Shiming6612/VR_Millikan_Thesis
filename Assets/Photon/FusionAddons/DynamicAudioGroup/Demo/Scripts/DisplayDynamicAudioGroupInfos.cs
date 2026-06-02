using Fusion;
using Fusion.Addons.DynamicAudioGroup;
#if PHOTON_VOICE_AVAILABLE
using Photon.Voice.Unity;
#endif

using TMPro;
using UnityEngine;

public class DisplayDynamicAudioGroupInfos : NetworkBehaviour
{
#if PHOTON_VOICE_AVAILABLE
    [SerializeField] private TextMeshPro placeHolder;
    [SerializeField] private DynamicAudioGroupMember dynamicAudioGroupMember;
    [SerializeField] private Speaker speaker;
    [SerializeField] private Color proximityDistanceColor = new Color32(0x1E, 0xFF, 0x6A, 100);
    [SerializeField] private Color proximityLeavingDistanceColor = new Color32(0xFF, 0x00, 0x00, 50);

    private string infos;

    private void Awake()
    {
        if (!placeHolder)
            placeHolder = GetComponentInChildren<TextMeshPro>();

        if (!dynamicAudioGroupMember)
            dynamicAudioGroupMember = GetComponentInChildren<DynamicAudioGroupMember>();

        if (!speaker)
            speaker = GetComponentInChildren<Speaker>();
    }

    public override void Render()
    {
        UpdateDynamicAudioGroupInfo();
    }

    private void UpdateDynamicAudioGroupInfo()
    {
        if (dynamicAudioGroupMember && placeHolder)
        {
            infos = "Player's GroupID : " + dynamicAudioGroupMember.GroupId.ToString();
            infos += "\nThis player speak to the group : " + dynamicAudioGroupMember.GroupId.ToString();
            if (Object && Object.HasStateAuthority)
            {
                infos += "\nThis Player listen to groups : ";
                foreach (var listenedMember in dynamicAudioGroupMember.listenedtoMembers)
                {
                    if(listenedMember.Object == null)
                    {
                        // The member is being destroy
                        continue;
                    }
                    infos += listenedMember.GroupId.ToString() + " ";
                }
            }
            infos += "\nVoice transmission is enabled : " + !dynamicAudioGroupMember.IsMuted;
            infos += "\nProximity Distance : " + dynamicAudioGroupMember.proximityDistance;

            placeHolder.text = infos;
        }
    }

    void OnDrawGizmos()
    {
        if (dynamicAudioGroupMember && speaker)
        {
            Gizmos.color = proximityDistanceColor;
            Gizmos.DrawSphere(speaker.transform.position, dynamicAudioGroupMember.proximityDistance);
            Gizmos.color = proximityLeavingDistanceColor;
            Gizmos.DrawSphere(speaker.transform.position, dynamicAudioGroupMember.proximityLeavingDistance);
        }
    }
#endif
}