using Fusion;
using UnityEngine;

[DefaultExecutionOrder(-10_000)]
public class EarlyNetworkBehaviour : NetworkBehaviour, IBeforeAllTicks, IAfterAllTicks, IBeforeCopyPreviousState
{
    public void AfterAllTicks(bool resimulation, int tickCount)
    {
        Debug.LogError($"[EarlyNetworkBehaviour] AfterAllTicks {transform.position} (parent:{transform.parent})");
    }

    public void BeforeAllTicks(bool resimulation, int tickCount)
    {
        Debug.LogError($"[EarlyNetworkBehaviour] BeforeAllTicks {transform.position} (parent:{transform.parent})");
    }

    public void BeforeCopyPreviousState()
    {
        Debug.LogError($"[EarlyNetworkBehaviour] BeforeCopyPreviousState {transform.position} (parent:{transform.parent})");
    }

    public override void Render()
    {
        base.Render();
        Debug.LogError($"[EarlyNetworkBehaviour] Render {transform.position} (parent:{transform.parent})");
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        Debug.LogError($"[EarlyNetworkBehaviour] FixedUpdateNetwork {transform.position} (parent:{transform.parent})");
    }
}
