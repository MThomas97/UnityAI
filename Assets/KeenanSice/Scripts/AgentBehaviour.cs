﻿using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using static BehaviourTreeHelperFunctions;

public class AgentBehaviour : MonoBehaviour
{
    BehaviourTree bt;

    void SetupBehaviour()
    {
        bt = behaviourTree(
            sequence(
                action(CheckResetTargetTimer),
                condition(agentController.HasAmmo,
                    //HasAmmo - true
                    condition(SetTargetToEnemyInSight,
                        action(),
                        action(StartPatrol)
                    ),
                    //HasAmmo - false
                    action(SetTargetToClosestActiveAmmo)
                ),
                condition(HasTarget,
                    //HasTarget - true
                    condition(IsTargetInRange,
                        //IsTargetInRange - true
                        condition(IsFacingTarget,
                            //IsFacingTarget - true
                            selector(
                                action(AttackAgent),
                                action(PickupCollectable),
                                action(PatrolToNextTarget)
                            ),                      
                            //IsFacingTarget - false
                            action(RotateTowards)                        
                        ),
                        condition(IsFacingTarget,
                            //IsFacingTarget - true
                            action(MoveForward),
                            //IsFacingTarget - false
                            action(RotateTowards)
                        )                              
                    ),
                    action()
                )
            )
        );
    }

    void Start()
    {
        agentController = GetComponent<AgentController>();
        SetupBehaviour();
    }

    void Update()
    {
        bt.Tick();
    }


    public Node targetNode = null;
    public GameObject targetObject = null;

    AgentController agentController;

    float reactionTimer = 0.0f;

    bool patrolling = false;


    bool AttackAgent()
    {
        if (targetObject != null)
        {
            AgentController ac = targetObject.GetComponent<AgentController>();

            if (ac != null)
            {
                agentController.Attack(ac);
                return true;
            }
        }
        return false;
    }

    bool PickupCollectable()
    {
        if (targetObject != null)
        {
            BasePickup pickup = targetObject.GetComponent<BasePickup>();

            if (pickup != null)
            {
                agentController.Pickup(pickup);
                return true;
            }
        }
        return false;
    }

    bool CheckResetTargetTimer()
    {
        if (targetObject != null && targetObject.CompareTag("AIPlayer"))
        {
            reactionTimer += Time.deltaTime;

            if (reactionTimer > agentController.reactionTime)
            {
                SetTarget(null);
                reactionTimer = 0.0f;
            }
        }

        return true;
    }

    bool HasAmmo()
    {
        return agentController.HasAmmo();
    }

    bool HasTarget()
    {
        return targetObject != null || targetNode != null;
    }

    bool SetTargetToClosestActiveAmmo()
    {
        //Already have ammo target;
        if (targetObject != null)
        {
            AmmoPickup targetAmmo = targetObject.GetComponent<AmmoPickup>();
            if(targetAmmo && targetAmmo.IsPickupActive())
            {
                return true;
            }
        }

        Debug.Log("AMMO");

        GameObject target = null;

        if (World.ammoTiles.Count > 0)
        {
            float closestDistance = -1;

            for(int i = 0; i < World.ammoTiles.Count; i++)
            {
                //If not active skip.
                if (!World.ammoTiles[i].mTileObject.GetComponent<AmmoPickup>().IsPickupActive()) continue;

                float distance = Vector3.Distance(transform.position, World.ammoTiles[i].mTileObject.transform.position);
                if (distance < closestDistance || closestDistance < 0.0f)
                {
                    closestDistance = distance;
                    target = World.ammoTiles[i].mTileObject;
                }
            }
        }

        SetTarget(target);

        return target != null;
    }

    void SetTarget(GameObject target, bool isPatrolTarget = false)
    {
        string outDebugString = "";
        Vector3 position = transform.position;
        Vector3 targetPosition;

        if (target == null)
            targetPosition = Vector3.zero;
        else
            targetPosition = target.transform.position;

        if (target != targetObject)
        {

            //targetNode = target == null ? null : PathFinding.CalculatePath(transform.position, target.transform.position, out outDebugString);
            //targetNode = targetNode != null ? targetNode.parent : targetNode;
            ThreadStart newThread = delegate
            {
                targetNode = target == null ? null : PathFinding.CalculatePath(position, targetPosition, out outDebugString);
                targetNode = targetNode != null ? targetNode.parent : targetNode;

            };

            Thread backgroundThread = new Thread(newThread);
            backgroundThread.Start();
        }
        else if (targetNode != null && Vector3.Distance(transform.position, targetNode.GetVector3Position()) < 0.1f)
        {
            targetNode = targetNode != null ? targetNode.parent : targetNode;
        }

        agentController.UpdatePathfindingDebug(targetNode, outDebugString);

        targetObject = target;
        if (target == null) targetNode = null;
        patrolling = isPatrolTarget;
    }

    bool IsTargetInRange()
    {
        if(targetNode != null && targetNode.parent != null)
        {
            if (Vector3.Distance(transform.position, targetNode.GetVector3Position()) < 0.1f)
            {
                SetTarget(targetObject, patrolling);
            }
        }

        //No range layer. Must be on
        if (targetObject != null && targetObject.layer == 8)
        {
            return (Vector3.Distance(transform.position, targetObject.transform.position) < 0.1f);
        }
        else if (targetNode != null && targetNode.parent == null)
        {
            RaycastHit2D hit = Physics2D.Linecast(transform.position, targetNode.GetVector3Position());
            if (hit.collider != null) return false;
            return (Vector3.Distance(transform.position, targetNode.GetVector3Position()) < agentController.attackRange);
        }

        return false;
    }

    bool SetTargetToEnemyInSight()
    {
        float closestDistance = -1.0f;
        GameObject target = null;

        foreach (AgentController enemy in World.agents)
        {
            if (enemy.teamNumber == agentController.teamNumber || enemy == null) continue;

            Vector3 enemyDir = Vector3.Normalize(enemy.transform.position - transform.position);
            float angle = Vector3.Angle(transform.right, enemyDir);
            if(angle > agentController.fieldOfView * 0.5f) continue;
            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance < closestDistance || closestDistance < 0)
            {
                //If we hit a collider trying to cast to the enemy, we technically can't see them, so ignore them. Most expensive check so left till last.
                RaycastHit2D hit = Physics2D.Linecast(transform.position, enemy.transform.position);
                if (hit.collider != null) continue;

                closestDistance = distance;
                target = enemy.gameObject;

                Debug.DrawRay(transform.position, enemyDir, Color.red);
            }
        }

        if (target != null) SetTarget(target);

        return target != null;
    }

    bool HasReachedTarget()
    {
        bool reachedNode = Vector3.Distance(transform.position, targetNode.GetVector3Position()) < 0.05f;

        if(reachedNode)
        {
            targetNode = targetNode.parent;
            return targetNode == null;
        }

        return reachedNode;
    }

    bool IsFacingTarget()
    {
        return transform.right == GetDirectionToTarget();
    }

    Vector3 GetDirectionToTarget()
    {
        Vector3 targetPosition = targetNode != null ? targetNode.GetVector3Position() : targetObject.transform.position;
        return Vector3.Normalize(targetPosition - transform.position);
    }

    bool RotateTowards()
    {
        if (targetObject != null)
        {
            Vector3 targetDirection = GetDirectionToTarget();
            float angle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
            Quaternion quart = Quaternion.AngleAxis(angle, Vector3.forward);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, quart, Time.deltaTime * agentController.rotationSpeed);
            return true;
        }

        return false;
    }

    bool RotateAround()
    {
        transform.Rotate(Vector3.forward, Time.deltaTime * agentController.rotationSpeed);
        return true;
    }

    bool MoveForward()
    {
        transform.localPosition += transform.right * Time.deltaTime * agentController.movementSpeed;
        return true;
    }

    bool SetTargetToRandomSpawn()
    {
        int randomSpawnpointIndex = Mathf.RoundToInt(Random.Range(0.0f, World.spawnpointTiles.Count - 1));

        if (Vector3.Distance(transform.position, World.spawnpointTiles[randomSpawnpointIndex].mTileObject.transform.position) < 5.0f)
        {
            randomSpawnpointIndex = (int)Mathf.Repeat(randomSpawnpointIndex + 1, World.spawnpointTiles.Count - 1);
        }

        SetTarget(World.spawnpointTiles[randomSpawnpointIndex].mTileObject, true);

        return true;
    }

    bool PatrolToNextTarget()
    {
        if (patrolling)
        {
            SetTargetToRandomSpawn();
        }

        return false;
    }

    bool StartPatrol()
    {
        if (!patrolling) SetTargetToRandomSpawn();

        return patrolling;

    }
}