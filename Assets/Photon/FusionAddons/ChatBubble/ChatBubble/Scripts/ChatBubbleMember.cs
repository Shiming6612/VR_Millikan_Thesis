using Fusion.Addons.AudioRoomAddon;


namespace Fusion.Addons.AudioChatBubble {

    /**
     * 
     * Synchronize a Chatbubble, if the audio room entered by this AudioroomMember is a Chatbubble, to allow late joiners to receive the information without passing through the usual check
     * For instance, it is required if from the point of view of the late joiner, entering the bubble is forbidden (if locked is true for instance)
     * 
     **/
    public class ChatBubbleMember : AudioRoomMember
    {
        [Networked]
        public ChatBubble CurrentChatBubble { get; set; }

        ChangeDetector changeDetector;

#if PHOTON_VOICE_AVAILABLE

        // Store the ChatBubble upon entering it (if the entered room is indeed a Chatbubble) in a NetworkVar, to be able to sync it (useful for late joiners)
        protected override void RoomChange(IAudioRoom room)
        {
            base.RoomChange(room);
            if (Object == null) return;
            if (Object.HasStateAuthority == false) return;
            if(room is ChatBubble c)
            {
                CurrentChatBubble = c;
            }
            else
            {
                CurrentChatBubble = null;
            }
        }

        #region Change detection
        public override void Spawned()
        {
            base.Spawned();
            if (Object.HasStateAuthority == false && CurrentChatBubble != null)
            {
                RoomChange(CurrentChatBubble);
            }
            changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        }

        public override void Render()
        {
            base.Render();
            
            foreach (var changedVarName in changeDetector.DetectChanges(this))
            {
                if(changedVarName == nameof(CurrentChatBubble))
                {
                    RoomChange(CurrentChatBubble);
                }
            }
        }
        #endregion
#endif
    }
}
