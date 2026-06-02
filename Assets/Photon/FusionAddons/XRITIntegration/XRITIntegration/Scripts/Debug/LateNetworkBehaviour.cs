using Fusion;
using UnityEngine;

[DefaultExecutionOrder(10_000)]
public class LateNetworkBehaviour : NetworkBehaviour, IBeforeAllTicks, IAfterAllTicks, IBeforeCopyPreviousState
{
    public void AfterAllTicks(bool resimulation, int tickCount)
    {
        Debug.LogError($"[LateNetworkBehaviour] AfterAllTicks {transform.position}");
    }

    public void BeforeAllTicks(bool resimulation, int tickCount)
    {
        Debug.LogError($"[LateNetworkBehaviour] BeforeAllTicks {transform.position}");
    }

    public void BeforeCopyPreviousState()
    {
        Debug.LogError($"[LateNetworkBehaviour] BeforeCopyPreviousState {transform.position}");
    }

    public override void Render()
    {
        base.Render();
        Debug.LogError($"[LateNetworkBehaviour] Render {transform.position} (parent:{transform.parent})");
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        Debug.LogError($"[LateNetworkBehaviour] FixedUpdateNetwork {transform.position}");
    }
}
