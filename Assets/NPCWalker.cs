using UnityEngine;
using UnityEngine.AI;

public class NPCWalker : MonoBehaviour
{
    public Transform playerHead;        // in VR: CenterEyeAnchor / Main Camera
    public float followDistance = 2.0f;
    public float stopDistance = 1.2f;

    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (playerHead == null) return;

        float d = Vector3.Distance(transform.position, playerHead.position);

        if (d > followDistance)
        {
            agent.isStopped = false;
            agent.SetDestination(playerHead.position);
        }
        else if (d < stopDistance)
        {
            agent.isStopped = true;
        }
    }
}

