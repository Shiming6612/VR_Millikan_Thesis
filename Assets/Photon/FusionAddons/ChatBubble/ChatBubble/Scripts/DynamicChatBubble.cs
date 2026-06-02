
namespace Fusion.Addons.AudioChatBubble
{
    public class DynamicChatBubble : ChatBubble
    {
        public DynamicChatBubbleManager manager;

        protected override void Awake()
        {
            base.Awake();
            if (manager == null) manager = FindAnyObjectByType<DynamicChatBubbleManager>();
            manager.RegisterDynamicChatbubble(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (manager) manager.UnregisterDynamicChatbubble(this);
        }
    }
}
