using Fusion;
using Fusion.Addons.InteractiveMenuAddon;


public class InteractiveMenuObjectEraser : NetworkBehaviour
{
    private InteractiveMenu interactiveMenu;
    private float delayBeforeDeletion = 0.3f;

    private void Awake()
    {
        interactiveMenu = GetComponent<InteractiveMenu>();              
    }

    public void Despawn()
    {
        if (Object.HasStateAuthority)
        {
            Delete();
        }
    }

    private void Delete()
    {
        Destroy(interactiveMenu.curveGO, delayBeforeDeletion);
        Destroy(interactiveMenu.interactiveMenuGO, delayBeforeDeletion);
        Object.Runner.Despawn(this.Object);
    }


    // We use Despawned call back so that remote clients can remove local menu objects
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        DeleteMenu();
    }


    private void DeleteMenu()
    {
        Destroy(interactiveMenu.curveGO, delayBeforeDeletion);
        Destroy(interactiveMenu.interactiveMenuGO, delayBeforeDeletion);
    }
}
