using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("AI Navigation Settings")]
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;
    private NavMeshAgent agent;

    /*
    [Header("Spawning Settings")]
    public GameObject slimePrefab;
    private float spawnTimer = 0f;
    private float spawnInterval = 2.0f; // Maintains the faster spawn timing!
    */

    [Header("Slowing Settings for Projectile Collision")]
    private float normalSpeed;
    private bool isSlowed = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        CommandAIToNextNode();

        normalSpeed = agent.speed;
    }

    void Update()
    {
        // Reverted: Strictly checks path progress to move from node to node
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            AdvanceToNextNodeIndex();
            CommandAIToNextNode();
        }

        // Handle slime puddle generation timing
        /*
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            SpawnSlimePuddle();
            spawnTimer = 0f;
        }
        */
    }

    void AdvanceToNextNodeIndex()
    {
        if (waypoints.Length == 0) return;
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }

    void CommandAIToNextNode()
    {
        if (waypoints.Length > 0 && agent != null)
        {
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
    }

    /*
    void SpawnSlimePuddle()
    {
        if (slimePrefab != null)
        {
            Instantiate(slimePrefab, transform.position, Quaternion.identity);
            Debug.Log("Troll dropped a slime puddle!");
        }
    }
    */

    public void ApplySlow(float slowPercentage, float duration)
    {
        // Prevent stacking the coroutine if they are already slowed
        if (!isSlowed && agent != null)
        {
            StartCoroutine(SlowRoutine(slowPercentage, duration));
        }
    }
    private IEnumerator SlowRoutine(float slowPercentage, float duration)
    {
        isSlowed = true;

        agent.speed = normalSpeed * slowPercentage;
        Debug.Log($"Enemy slowed! NavMesh speed is now: {agent.speed}");

        yield return new WaitForSeconds(duration);

        agent.speed = normalSpeed;
        isSlowed = false;
        Debug.Log("Enemy returned to normal NavMesh speed.");
    }
}