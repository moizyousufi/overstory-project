using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Idle, Run, DropObstacle, Jump, EscapeRun, Escaped, Laugh }
    
    [Header("AI State System")]
    public AIState currentState = AIState.Idle;
    public Transform playerTransform;
    public float detectionRange = 15f; 

    [Header("AI Navigation Settings (6-Node Pool)")]
    public Transform[] waypoints = new Transform[6];
    public Transform beanstalkDestination; 
    
    private Queue<Transform> activeMatchRoute = new Queue<Transform>();
    private Transform currentTargetNode = null;
    private NavMeshAgent agent; 

    [Header("Beanstalk Escape Settings")]
    public float escapeSpeedMultiplier = 1.35f;

    [Header("Spawning Settings (Dynamic Proximity Scaling)")]
    public GameObject slimePrefab;
    public Transform dropPoint; 
    private float spawnTimer = 0f;
    
    public float spawnInterval = 3.0f; 
    public float minimumSpawnInterval = 1.2f; 
    public float proximityPanicDistance = 12f;

    [Header("Slowing Settings for Projectile Collision")]
    private float normalSpeed;
    private bool isSlowed = false;

    [Header("Troll Waddle & Animation Tuning")]
    public float waddleSpeed = 10f;      
    public float tiltIntensity = 18f;    
    public Transform visualMeshTransform; 
    private Animator anim;
    
    private bool isJumping = false;
    private bool isDroppingObstacle = false;

    private TrollAudio trollAudio;

    private AIState stateBeforeJump = AIState.Run;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        
        // UPDATED: Enabled to allow AI to recognize and follow NavMesh Links
        agent.autoTraverseOffMeshLink = true; 
        
        normalSpeed = agent.speed;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; 
        agent.acceleration = 20f; 

        if (playerTransform == null)
        {
            playerTransform = GameObject.FindWithTag("Player")?.transform;
        }

        trollAudio = GetComponent<TrollAudio>();

        InitializeRandomMatchRoute();
        TransitionToNextTarget();
    }

    void Update()
    {
        // Traversal Logic: If the link is set to 'Jump' in the Inspector, this handles the leap.
        // If the link is set to 'Walkable', this block is bypassed automatically.
        if (agent.isOnNavMesh && agent.isOnOffMeshLink && currentState != AIState.Jump && !isJumping)
        {
            stateBeforeJump = currentState;
            currentState = AIState.Jump;
            StartCoroutine(TriggerTrollJump());
            return;
        }

        if (currentState == AIState.Run || currentState == AIState.EscapeRun)
        {
            EnforceNavMeshGroundingBounds();
        }

        switch (currentState)
        {
            case AIState.Idle: HandleIdleState(); break;
            case AIState.Run: HandleRunState(); break;
            case AIState.DropObstacle: HandleDropObstacleState(); break;
            case AIState.Jump: 
                if (anim) anim.SetBool("IsRunning", false);
                ResetWaddleOrientation();
                break;
            case AIState.EscapeRun: HandleEscapeRunState(); break;
            case AIState.Escaped: HandleEscapedState(); break;
            case AIState.Laugh: HandleLaughState(); break;
        }
    }

    private void HandleIdleState()
    {
        if (anim) anim.SetBool("IsRunning", false);
        ResetWaddleOrientation();
        if (currentTargetNode != null) currentState = AIState.Run;
    }

    private void HandleRunState()
    {
        if (isJumping || isDroppingObstacle) return;
        if (anim) anim.SetBool("IsRunning", true);

        if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) < 5f)
        {
            agent.speed = isSlowed ? normalSpeed * 0.5f : normalSpeed * 1.3f; 
        }
        else if (!isSlowed) agent.speed = normalSpeed;

        ApplyProceduralWaddle();
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= CalculateDynamicSpawnInterval()) currentState = AIState.DropObstacle;
    }

    private void HandleDropObstacleState()
    {
        if (isJumping || isDroppingObstacle) return;
        StartCoroutine(PerformDropObstacleSequence());
    }

    private IEnumerator PerformDropObstacleSequence()
    {
        isDroppingObstacle = true;
        spawnTimer = 0f;
        if (anim != null) anim.SetTrigger("Attack"); 
        SpawnSlimePuddle();
        yield return new WaitForSeconds(1.2f);
        isDroppingObstacle = false;
        currentState = (activeMatchRoute.Count == 0 && currentTargetNode == beanstalkDestination) ? AIState.EscapeRun : AIState.Run;
    }

    private void HandleEscapeRunState()
    {
        if (isJumping || isDroppingObstacle) return;
        if (anim) anim.SetBool("IsRunning", true);
        ApplyProceduralWaddle();
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= CalculateDynamicSpawnInterval()) currentState = AIState.DropObstacle;
    }

    private void HandleEscapedState()
    {
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        ResetWaddleOrientation();
        TriggerVictoryState();
    }

    private void HandleLaughState()
    {
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        if (anim) { anim.SetBool("IsRunning", false); anim.SetTrigger("Laugh"); }
        ResetWaddleOrientation();
    }

    private void InitializeRandomMatchRoute()
    {
        List<Transform> temporaryPool = new List<Transform>(waypoints);
        activeMatchRoute.Clear();
        for (int i = 0; i < 3; i++)
        {
            if (temporaryPool.Count == 0) break;
            int randomIndex = Random.Range(0, temporaryPool.Count);
            activeMatchRoute.Enqueue(temporaryPool[randomIndex]);
            temporaryPool.RemoveAt(randomIndex); 
        }
    }

    private void TransitionToNextTarget()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        Transform nextNode = null;
        if (activeMatchRoute.Count > 0) nextNode = activeMatchRoute.Dequeue();
        else if (currentTargetNode != beanstalkDestination)
        {
            currentState = AIState.EscapeRun;
            nextNode = beanstalkDestination;
            normalSpeed *= escapeSpeedMultiplier;
            agent.speed = normalSpeed;
            agent.acceleration = 24f;
        }

        if (nextNode != null)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(nextNode.position, out hit, 3.0f, NavMesh.AllAreas))
            {
                currentTargetNode = nextNode;
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }
    }

    private void EnforceNavMeshGroundingBounds()
    {
        if (agent.velocity.sqrMagnitude < 0.1f && !agent.pathPending && !isJumping && !isDroppingObstacle)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 1.5f, NavMesh.AllAreas))
            {
                if (Vector3.Distance(transform.position, hit.position) > 0.2f)
                {
                    transform.position = hit.position;
                    if (currentTargetNode != null) agent.SetDestination(currentTargetNode.position);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentTargetNode != null && other.transform == currentTargetNode)
        {
            if (currentState == AIState.EscapeRun && currentTargetNode == beanstalkDestination) currentState = AIState.Escaped;
            else TransitionToNextTarget();
        }
    }

    private float CalculateDynamicSpawnInterval()
    {
        if (playerTransform == null) return spawnInterval;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float normDistance = Mathf.Clamp01(distanceToPlayer / proximityPanicDistance);
        return Mathf.Lerp(minimumSpawnInterval, spawnInterval, normDistance);
    }

    void SpawnSlimePuddle()
    {
        if (slimePrefab != null)
        {
            Vector3 pos = dropPoint != null ? dropPoint.position : (transform.position - transform.forward * 2.5f);
            pos.y = transform.position.y - 0.1f; 
            GameObject puddle = Instantiate(slimePrefab, pos, Quaternion.identity);
            if (trollAudio != null) trollAudio.PlaySlimeDrop();
            if (puddle.GetComponent<SlimeTrigger>() == null) puddle.AddComponent<SlimeTrigger>();
        }
    }

    private IEnumerator TriggerTrollJump()
    {
        isJumping = true;
        agent.isStopped = true;
        if (anim) anim.SetBool("IsRunning", false);
        ResetWaddleOrientation();

        OffMeshLinkData data = agent.currentOffMeshLinkData;
        Vector3 startPos = transform.position;
        Vector3 endPos = data.endPos;

        float jumpDuration = 1.6f; 
        for (float t = 0; t < jumpDuration; t += Time.deltaTime)
        {
            float normalizedT = t / jumpDuration;
            transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0, 1, normalizedT)) + Vector3.up * (Mathf.Sin(normalizedT * Mathf.PI) * 3.2f);
            if (anim) anim.Play("jumping", 0, Mathf.Clamp01(normalizedT * 0.7f));
            yield return null;
        }
        transform.position = endPos;
        agent.CompleteOffMeshLink();
        agent.isStopped = false;
        isJumping = false;
        currentState = stateBeforeJump;
    }

    private void ApplyProceduralWaddle()
    {
        if (visualMeshTransform != null && agent.velocity.sqrMagnitude > 0.1f)
            visualMeshTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * waddleSpeed) * tiltIntensity);
    }

    private void ResetWaddleOrientation() { if (visualMeshTransform != null) visualMeshTransform.localRotation = Quaternion.identity; }
    public void TriggerVictoryState() => currentState = AIState.Laugh;
    public void ApplySlow(float s, float d) => StartCoroutine(SlowRoutine(s, d));
    private IEnumerator SlowRoutine(float s, float d) { isSlowed = true; agent.speed = normalSpeed * s; yield return new WaitForSeconds(d); agent.speed = normalSpeed; isSlowed = false; }
}