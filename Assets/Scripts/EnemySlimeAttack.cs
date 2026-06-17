using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemySlimeAttack : MonoBehaviour
{
    [Header("Slime Settings")]
    public GameObject slimePrefab;
    public Transform slimeSpawnPoint;
    public float spawnInterval = 2.0f;

    [Header("Animation Settings")]
    public string slimeAttackTrigger = "DropSlime";
    public float slimeSpawnDelay = 0.4f;
    public float attackAnimationDuration = 1.0f;

    private NavMeshAgent agent;
    private Animator animator;
    private float spawnTimer = 0f;
    private bool isAttacking = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (isAttacking)
            return;

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            StartCoroutine(SlimeAttackRoutine());
        }
    }

    private IEnumerator SlimeAttackRoutine()
    {
        isAttacking = true;

        // Stop the enemy while doing the attack animation
        if (agent != null)
        {
            agent.isStopped = true;
        }

        // Play attack animation
        if (animator != null)
        {
            animator.SetTrigger(slimeAttackTrigger);
        }

        // Wait before slime appears so it matches the attack animation
        yield return new WaitForSeconds(slimeSpawnDelay);

        SpawnSlimePuddle();

        // Wait for the rest of the animation
        yield return new WaitForSeconds(attackAnimationDuration);

        // Resume enemy movement
        if (agent != null)
        {
            agent.isStopped = false;
        }

        isAttacking = false;
    }

    private void SpawnSlimePuddle()
    {
        if (slimePrefab == null)
            return;

        Vector3 spawnPosition = transform.position;

        if (slimeSpawnPoint != null)
        {
            spawnPosition = slimeSpawnPoint.position;
        }

        Instantiate(slimePrefab, spawnPosition, Quaternion.identity);
        Debug.Log("Troll dropped a slime puddle!");
    }
}
