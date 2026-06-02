using System.Collections.Generic;
using Fusion.XR.Shared.Core;
using UnityEngine.Events;

namespace Fusion.Addons.AudioRoomAddon
{

    /***
     * 
     * `AudioDoor` must be added to a door game object separating audio rooms. 
     * The door can be open or close thanks to the `ToogleDoor()` and `Open()` public methods.
     * If the player requesting the opening and closing of the door does not have the authority to do so, then he send a RPC to the StateAuthority.
     * The door status (open or close) is synchronized over the network thanks to the networked bool `IsOpened`.
     * When the door status changed, rooms separated by this door are updated with the new status
     * 
     ***/
    public class AudioDoor : NetworkBehaviour, IPlayerLeft
    {
        public async void PlayerLeft(PlayerRef player)
        {
            // Ensure we have an owner to have someone able to send images to user joining later the session
            await Object.EnsureHasStateAuthority();
        }

#if PHOTON_VOICE_AVAILABLE

        public List<AudioRoom> separatedRooms = new List<AudioRoom>();
        public UnityEvent OnStatusChange;

        //  The door status is synchronized over the network thanks to the networked bool `IsOpened`.
        [Networked]
        public NetworkBool IsOpened { get; set; } = true;

        ChangeDetector renderChangeDetector;


        void OnIsOpenedChange()
        {
            // Update rooms separated by this door about the new status
            foreach (var room in separatedRooms)
            {
                room.Isolate(isIsolated: IsOpened == false);
            }
            if (OnStatusChange != null)
                OnStatusChange.Invoke();
        }

        public override void Spawned()
        {
            base.Spawned();
            OnIsOpenedChange();
            renderChangeDetector = GetChangeDetector(ChangeDetector.Source.SnapshotFrom);
        }

        bool TryDetectIsOpenedChange()
        {
            foreach (var changedNetworkedVarName in renderChangeDetector.DetectChanges(this))
            {
                if (changedNetworkedVarName == nameof(IsOpened))
                {
                    return true;
                }
            }
            return false;
        }

        public override void Render()
        {
            // Check if the IsOpened changed
            if (TryDetectIsOpenedChange())
            {
                OnIsOpenedChange();
            }
        }

        [EditorButton("Toogle is opened")]
        public void ToogleDoor()
        {
            Open(!IsOpened);
        }

        public void Open(bool isOpen)
        {
            if (Object.HasStateAuthority)
            {
                IsOpened = isOpen;
            }
            else
            {
                RPC_Open(isOpen);
            }
        }

        [Rpc(sources: RpcSources.All, targets: RpcTargets.StateAuthority)]
        public void RPC_Open(bool isOpen)
        {
            IsOpened = isOpen;
        }
#endif
    }
}
