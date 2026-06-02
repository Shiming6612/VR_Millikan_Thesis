using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class NetworkedNPCController : NetworkBehaviour
{
    [Header("Movement")]
    public NavMeshAgent agent;
    public Transform[] waypoints;

    [Tooltip("Extra tolerance added on top of agent.stoppingDistance to decide arrival (recommend ~0.15-0.25).")]
    public float waypointTolerance = 0.2f;

    public bool loopWaypoints = true;

    [Header("Waypoint Start Behavior")]
    public bool startAtFirstWaypoint = true;

    [Header("NavMesh Robustness")]
    public bool warpToNavMeshIfNeeded = true;
    public float navMeshWarpSearchRadius = 2.0f;
    public bool logNavMeshDebug = false;

    [Header("Agent Tuning (Applied on StateAuthority)")]
    public bool applyAgentTuningOnAuthority = true;

    public float agentSpeed = 1.2f;
    public float agentAcceleration = 6f;
    public float agentAngularSpeed = 360f;
    public float agentStoppingDistance = 0.1f;
    public bool agentAutoBraking = true;

    [Header("Look At Player (when nearby)")]
    public float lookAtDistance = 2.0f;
    public float turnSpeed = 8f;

    [Header("Look Where Moving (when following waypoints)")]
    public bool faceMoveDirection = true;
    public float minSpeedToUseVelocityFacing = 0.05f;
    public bool useDesiredVelocityForFacing = true;

    [Header("Animation")]
    public Animator animator;
    public string speedParam = "Speed";
    public float animSpeedMultiplier = 1f;

    [Networked] private float NetSpeed { get; set; }
    [Networked] private NetworkBool NetLookAtActive { get; set; }
    [Networked] private Vector3 NetLookTarget { get; set; }

    private int _wpIndex;

    // IMPORTANT: our own path/init state (do not rely on agent.hasPath for progression)
    private bool _initializedPath;
    private Vector3 _currentTarget;
    private bool _hasCurrentTarget;

    public override void Spawned()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Proxies should not run NavMeshAgent; they will be driven by NetworkTransform.
        if (!Object.HasStateAuthority && agent != null)
        {
            agent.enabled = false;
        }

        _initializedPath = false;
        _hasCurrentTarget = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (agent == null) return;
        if (!agent.enabled) return;

        if (applyAgentTuningOnAuthority)
        {
            agent.speed = agentSpeed;
            agent.acceleration = agentAcceleration;
            agent.angularSpeed = agentAngularSpeed;
            agent.stoppingDistance = agentStoppingDistance;
            agent.autoBraking = agentAutoBraking;
        }

        // Ensure agent is on NavMesh (especially after reload / spawn)
        if (!agent.isOnNavMesh)
        {
            if (warpToNavMeshIfNeeded && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshWarpSearchRadius, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                if (logNavMeshDebug) Debug.Log($"[NPC] Warped to NavMesh at {hit.position}");
            }
            else
            {
                if (logNavMeshDebug) Debug.LogWarning("[NPC] Agent not on NavMesh -> cannot move.");
                return;
            }
        }

        // -------- Waypoint movement (robust + reload-safe) --------
        if (waypoints != null && waypoints.Length > 0)
        {
            if (!_initializedPath)
            {
                _wpIndex = startAtFirstWaypoint ? 0 : Mathf.Clamp(_wpIndex, 0, waypoints.Length - 1);
                SetDestinationToWaypoint(_wpIndex);
                _initializedPath = true;
            }
            else
            {
                if (ArrivedAtCurrentTarget(agent))
                {
                    _wpIndex = loopWaypoints
                        ? (_wpIndex + 1) % waypoints.Length
                        : Mathf.Min(_wpIndex + 1, waypoints.Length - 1);

                    SetDestinationToWaypoint(_wpIndex);
                }
            }
        }

        // -------- Network animation speed --------
        NetSpeed = agent.velocity.magnitude * animSpeedMultiplier;

        // -------- Rotation (priority-based) --------
        if (NetLookAtActive)
        {
            SmoothTurnTowards(NetLookTarget);
            return;
        }

        if (faceMoveDirection && TryGetMoveLookPoint(out Vector3 lookPoint))
        {
            SmoothTurnTowards(lookPoint);
        }
    }

    public override void Render()
    {
        if (animator != null)
        {
            animator.SetFloat(speedParam, NetSpeed);
        }
    }

    private void SetDestinationToWaypoint(int index)
    {
        if (waypoints == null || waypoints.Length == 0) return;
        if (index < 0 || index >= waypoints.Length) return;

        Vector3 target = waypoints[index].position;

        // store our own target so progression doesn't depend on agent.hasPath
        _currentTarget = target;
        _hasCurrentTarget = true;

        bool ok = agent.SetDestination(target);

        if (logNavMeshDebug)
            Debug.Log($"[NPC] SetDestination wp={index} ok={ok} target={target} isOnNavMesh={agent.isOnNavMesh}");
    }

    // Arrival now uses distance-to-stored-target, not hasPath
    private bool ArrivedAtCurrentTarget(NavMeshAgent a)
    {
        if (!_hasCurrentTarget) return false;
        if (a.pathPending) return false;

        // planar distance check (ignore Y)
        Vector3 p = transform.position; p.y = 0f;
        Vector3 t = _currentTarget; t.y = 0f;

        float dist = Vector3.Distance(p, t);
        float threshold = a.stoppingDistance + waypointTolerance;

        if (dist > threshold) return false;

        // wait until basically stopped (prevents "grazing" at speed)
        if (a.velocity.sqrMagnitude > 0.01f) return false;

        return true;
    }

    private bool TryGetMoveLookPoint(out Vector3 lookPoint)
    {
        lookPoint = Vector3.zero;

        Vector3 v = useDesiredVelocityForFacing ? agent.desiredVelocity : agent.velocity;
        v.y = 0f;

        if (v.magnitude >= minSpeedToUseVelocityFacing)
        {
            lookPoint = transform.position + v.normalized;
            return true;
        }

        // fallback: face toward current target if we have one
        if (_hasCurrentTarget)
        {
            Vector3 target = _currentTarget;
            target.y = transform.position.y;

            Vector3 dir = target - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                lookPoint = target;
                return true;
            }
        }

        // fallback: steering target/destination
        if (agent.hasPath)
        {
            Vector3 target = agent.steeringTarget;
            target.y = transform.position.y;

            Vector3 dir = target - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                lookPoint = target;
                return true;
            }

            Vector3 dest = agent.destination;
            dest.y = transform.position.y;

            dir = dest - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                lookPoint = dest;
                return true;
            }
        }

        return false;
    }

    private void SmoothTurnTowards(Vector3 worldTarget)
    {
        Vector3 dir = worldTarget - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Runner.DeltaTime * turnSpeed);
    }

    // ---------------- Look-at API (unchanged behavior) ----------------
    public void RequestLookAt(Vector3 worldTarget) => RPC_SetLookAt(true, worldTarget);
    public void RequestStopLookAt() => RPC_SetLookAt(false, Vector3.zero);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetLookAt(bool active, Vector3 target)
    {
        NetLookAtActive = active;
        if (active) NetLookTarget = target;
    }
}
