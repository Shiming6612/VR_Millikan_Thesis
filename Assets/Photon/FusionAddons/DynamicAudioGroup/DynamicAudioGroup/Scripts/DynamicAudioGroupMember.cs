#if PHOTON_VOICE_AVAILABLE
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using Photon.Realtime;
#endif
using System.Collections.Generic;
using UnityEngine;
using Fusion.XR.Shared.Core;

namespace Fusion.Addons.DynamicAudioGroup
{
    /**
     * 
     * DynamicAudioGroupMember is located on each network player prefab 
     * A unique audio GroupId is computed for the player (each player has it own GroupId)
     * The player voice client is configured to speak and listen only in this group Id.
     * The group in which the local player speaks will never be changed. 
     * However, the list of groups he listens to is changed dynamically according to the proximity of the other players.
     * Voice transmission is disabled if the player is not in proximty with another player.
     * 
     **/

    public class DynamicAudioGroupMember : NetworkBehaviour
    {
#if PHOTON_VOICE_AVAILABLE
        public const int NEVER_MATCHING_GROUP_FILTER = -1;
        [Header("Proximity definition")]
        [Tooltip("This distance should match the audio source range where the level is 0 for user voices, and indicates when we start being able to listen to an user")]
        public float proximityDistance = 15;
        [Tooltip("This distance should be a bit larger than proximityDistance, to determine when we stop listening without having frontier effects (goin in and out of proximity very quickly)")]
        public float proximityLeavingDistance = 16f;

        [Header("Audio groups usage")]
        public int minAudioGroup = 5;
        public int maxAudioGroup = 255;
        public int nonSpeakingRecorderAudioGroup = 1;

        public bool IsMuted => recorder == null || recorder.TransmitEnabled == false;


        float proximityDistanceSqr;
        float proximityLeavingDistanceSqr;
        static List<DynamicAudioGroupMember> members = new List<DynamicAudioGroupMember>();
        public static List<DynamicAudioGroupMember> AllMembers => members;
        public INetworkRig rig;
        bool isSpeakInitialized = false;

        [Networked]
        public byte GroupId { get; set; }
        // Contains the last received GroupId to be able to use this value during OnDestroy(), as the actual GroupId may not be set anymore at this point
        byte lastGroupId;

        [Header("Filter & whitelist")]
        // Additional value that has to be matching so that people ear to each other
        public int additionalFilter;

        // Those groups will always be heard, no matter the distance
        public List<DynamicAudioGroupMember> alwaysListenedMembers = new List<DynamicAudioGroupMember>();

        [Header("Current status")]
        public List<DynamicAudioGroupMember> listenedtoMembers = new List<DynamicAudioGroupMember>();
        public Recorder recorder;
        public FusionVoiceClient fusionVoiceClient;

        public virtual bool Listenable => true;

        int ConvertToGroupId(int source)
        {
            return minAudioGroup + (source % (maxAudioGroup - minAudioGroup));
        }

        ChangeDetector renderChangeDetector;

        public enum MuteMode
        {
            MuteWhenListeningToNoOne,
            // usefull if the white lists alwaysListenedMembers are not synchronized (someone is listening all the time to us and we can't know it)
            NoAutomaticMute
        }

        [Header("Mute handling")]
        public MuteMode muteMode = MuteMode.MuteWhenListeningToNoOne;

        protected virtual void Awake()
        {
            rig = GetComponent<INetworkRig>();
            if (proximityLeavingDistance < proximityDistance)
            {
                proximityLeavingDistance = proximityDistance + 1f;
            }
        }
        public override void Spawned()
        {
            base.Spawned();
            recorder = Runner.GetComponentInChildren<Recorder>();
            if (recorder == null)
                Debug.LogError("Recorder not found !");

            fusionVoiceClient = Runner.GetComponentInChildren<FusionVoiceClient>();
            proximityDistanceSqr = proximityDistance * proximityDistance;
            proximityLeavingDistanceSqr = proximityLeavingDistance * proximityLeavingDistance;
            members.Add(this);
            if (Object.HasStateAuthority)
            {
                // Apply a module on the player id to find an audio group. Limit the number of used player id to (maxAudioGroup - minaudioGroup) users
                GroupId = (byte)ConvertToGroupId(Runner.LocalPlayer.PlayerId);
                SpeakAndListenOnlyToGroup(GroupId);
            }
            renderChangeDetector = GetChangeDetector(ChangeDetector.Source.SnapshotFrom);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            members.Remove(this);
            foreach (var member in members)
            {
                if (member.alwaysListenedMembers.Contains(this))
                {
                    member.alwaysListenedMembers.Remove(this);
                }

                if (member.Object && member.Object.HasStateAuthority)
                {
                    if (member.listenedtoMembers.Contains(this))
                    {
                        Debug.Log("Stop listening to a leaving player");
                        member.StopListeningToGroupId(lastGroupId);
                        member.listenedtoMembers.Remove(this);
                    }
                    break;
                }
            }
        }


        bool TryDetectGroupIdChange()
        {
            foreach (var changedNetworkedVarName in renderChangeDetector.DetectChanges(this))
            {
                if (changedNetworkedVarName == nameof(GroupId))
                {
                    return true;
                }
            }
            return false;
        }

        public override void Render()
        {
            // Check if the groupID changed
            if (TryDetectGroupIdChange())
            {
                lastGroupId = GroupId;
            }

            CheckProximity();
        }

        protected virtual void Mute()
        {
            recorder.TransmitEnabled = false;
        }

        protected virtual void Unmute()
        {
            recorder.TransmitEnabled = true;
        }

        bool IsPhotonVoiceready => recorder != null && recorder.RecordingEnabled && fusionVoiceClient.ClientState == ClientState.Joined;

        // the local player Recorder audio interest group is configured with the group Id.
        // and the voice client is configured to listen it
        protected virtual async void SpeakAndListenOnlyToGroup(byte groupId)
        {
            if (!Object.HasStateAuthority) return;
            while (!IsPhotonVoiceready) await AsyncTask.Delay(10);
            Debug.Log("+ SpeakAndListenOnlyToGroup " + groupId);
            recorder.InterestGroup = groupId;
            fusionVoiceClient.Client.OpChangeGroups(groupsToRemove: new byte[] { }, groupsToAdd: new byte[] { groupId });
            if (muteMode == MuteMode.MuteWhenListeningToNoOne)
            {
                Mute();
            }
            isSpeakInitialized = true;
        }

        // Start to listen an audio group
        protected virtual async void ListentoGroupId(byte groupId)
        {
            if (!Object.HasStateAuthority) return;
            while (!IsPhotonVoiceready || !isSpeakInitialized) await AsyncTask.Delay(10);
            Debug.Log("+ ListentoGroupId " + groupId);
            fusionVoiceClient.Client.OpChangeGroups(groupsToRemove: null, groupsToAdd: new byte[] { groupId });
        }

        // Stop to listen an audio group
        protected virtual async void StopListeningToGroupId(byte groupId)
        {
            if (!Object.HasStateAuthority) return;
            while (!IsPhotonVoiceready || !isSpeakInitialized) await AsyncTask.Delay(10);
            Debug.Log("- StopListeningToGroupId " + groupId);
            fusionVoiceClient.Client.OpChangeGroups(groupsToRemove: new byte[] { groupId }, groupsToAdd: null);
        }

        protected float DistanceSqr(DynamicAudioGroupMember other)
        {
            return (other.rig.Headset.transform.position - rig.Headset.transform.position).sqrMagnitude;
        }

        protected virtual void CheckProximityWithOtherMember(DynamicAudioGroupMember member)
        {
            if (alwaysListenedMembers.Contains(member))
            {
                // In the users whitelist
                ListenToMember(member);
            }
            else if (additionalFilter == NEVER_MATCHING_GROUP_FILTER || additionalFilter != member.additionalFilter)
            {
                // No matching filter
                StopListeningToMember(member);
            }
            else if (DistanceSqr(member) < proximityDistanceSqr)
            {
                // Listen the GroupId of the remote player if he is within the defined distance
                ListenToMember(member);
            }
            else if (DistanceSqr(member) >= proximityLeavingDistanceSqr)
            {
                // Stop listening the remote player if he is not within the defined distance
                StopListeningToMember(member);
            }
        }

        protected void CheckProximity()
        {
            // Quit if we do not have the state authority on this player prefab
            if (Object == null || Object.IsValid == false ||Object.HasStateAuthority == false) return;

            foreach (var member in members)
            {
                if (member == null || member.rig == null || member == this) continue;

                CheckProximityWithOtherMember(member);
            }

            // TODO adapt proximityDistance to number of listenedtoMembers (to decrease it if the number is too hight)
            //  The voice audio source range curve should be adapted for its 0 distance to match this adaptation

            if (muteMode == MuteMode.MuteWhenListeningToNoOne)
            {
                bool inSomeUsersWhiteList = false;
                foreach(var m in AllMembers)
                {
                    if (m.alwaysListenedMembers.Contains(this))
                    {
                        inSomeUsersWhiteList = true;
                        break;
                    }
                }
                bool shouldSpeak = inSomeUsersWhiteList || listenedtoMembers.Count > 0;
                // Enable the voice transmission it was disabled and the player is in proximty with another player.
                if (shouldSpeak && IsMuted)
                {
                    Unmute();
                }

                // Voice transmission is disabled if the player is not in proximty with another player.
                if (shouldSpeak == false && IsMuted == false)
                {
                    Mute();
                }
            }
        }

        void ListenToMember(DynamicAudioGroupMember member)
        {
            if (listenedtoMembers.Contains(member)) return;
            if (member.Listenable == false) return;
            listenedtoMembers.Add(member);
            ListentoGroupId(member.GroupId);
        }

        protected void StopListeningToMember(DynamicAudioGroupMember member)
        {
            if (!listenedtoMembers.Contains(member)) return;
            if (member.Listenable == false) return;
            listenedtoMembers.Remove(member);
            StopListeningToGroupId(member.GroupId);
        }
#endif
    }
}
