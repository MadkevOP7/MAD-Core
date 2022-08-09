//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Mirror;
[RequireComponent(typeof(GhostIdentity))]
public class GhostAI : NetworkBehaviour
{
    #region Default Fields
    [Header("Setup")]
    int difficulty = 1;
    public Animator animator;
    public NavMeshAgent agent;
    [Header("Settings")]
    public Transform head;
    public enum ChargedAttackMode
    {
        specified,
        useMultiplier
    }
    public ChargedAttackMode chargedAttackMode;
    public int attack_damage = 25;
    public int charged_attack_damage = 100;
    public float attack_distance = 1.68f;
    public float vision_range = 10f;
    public float lose_player_time = 6f;
    public float target_reached_distance = 1;
    public float normalSpeed = 1.6f;
    public float runSpeed = 2.5f;
    [Header("Cache settings")]
    public int max_obstacles_cache = 128;
    //Field of View
    [Header("Field Of View")]
    public LayerMask targetMask; //For players
    public LayerMask obstacleMask; //For walls/blocks etc
    public float viewRadius = 10f;
    [Range(0, 360)]
    public float viewAngle = 145f;
    public float meshResolution;
    public int edgeResolveIterations;
    public float edgeDstThreshold;
    #endregion

    #region Waypoint Server Cache
    GlobalWaypoint globalWaypoint;

    #endregion

    #region Runtime
    static int DEFAULT_LEANING_PERCENTAGE = 35;
    #endregion
    #region Runtime Cache
    //Runtime var
    public List<Transform> visibleTargets = new List<Transform>();
    public List<NavMeshObstacle> obsCache = new List<NavMeshObstacle>();
    public List<Transform> obstacles = new List<Transform>();
    public BuildItem attacking_item;
    public TargetInfo aTargetInfo = new TargetInfo();

    //booleans
    public bool canAttack = true;
    float speed;
    //Player Target
    public Player playerCache;
    private Transform _playerTarget = null;
    public Transform playerTarget;
    //{
    //    get { return _playerTarget; }
    //    set
    //    {
    //        if (_playerTarget != value)
    //        {
    //            OnPlayerTargetChanged(_playerTarget, value);
    //            _playerTarget = value;
    //        }
    //    }
    //}
    public Transform waypointTarget;
    #endregion

    #region Coroutines Reference
    Coroutine brain;
    Coroutine attackCoolDownTimer;
    Coroutine finalAttacking;
    #endregion

    #region Actions
    public enum Action
    {
        root,
        hasPlayer,
        wayPoint,
        destroyBuilding,
        attackPlayer
    }

    public Action _action;
    public Action action
    {
        get { return _action; }
        set
        {
            if (_action != value)
            {
                _action = value;
                RefreshAIState();
            }
        }
    }
    #endregion

    #region Debug Cache
    LineRenderer line;
    #endregion
    private void Awake()
    {
        action = Action.root;
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        speed = normalSpeed;
        agent.speed = speed;
        globalWaypoint = GlobalWaypoint.Instance;

        //Start AI Core Tick
        brain = StartCoroutine("TickAI", .5f);
    }

    #region AI Core
    IEnumerator TickAI(float rate)
    {
        while (true)
        {
            yield return new WaitForSeconds(rate);
            OnAITick();
        }
    }

    void OnAITick()
    {
        OnOBSTick();
        if (action == Action.destroyBuilding)
        {
            OnDestroyBuildingTick();
            return;
        }
        if (playerTarget == null)
        {
            OnWaypointTick();
        }
        else
        {
            if (playerCache && playerCache.sanity > 0)
            {
                OnChasePlayerTick();
            }
            else
            {
                playerCache = null;
                playerTarget = null;
            }
        }
        OnSpeedTick();

    }

    #region Core Loops

    void OnChasePlayerTick()
    {
        action = Action.hasPlayer;
        if (IsAttackDistance(playerTarget))
        {
            if (canAttack)
            {
                if (Distance(transform, playerTarget) < attack_distance / 2)
                {
                    Attack(AttackType.shortAttack);
                }
                else
                {
                    Attack(AttackType.longAttack);
                }
            }
        }
        else
        {
            if (CanReachTarget(playerTarget.position))
            {
                OnMovementTick(playerTarget);
            }
            else
            {
                action = Action.destroyBuilding;
            }
        }
    }


    IEnumerator AttackTimer()
    {
        animator.SetBool("attacking", true);
        canAttack = false;
        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.2f);
        float d = animator.GetCurrentAnimatorStateInfo(0).length;
        Debug.Log("Time: " + (d));
        yield return new WaitForSeconds(d);
        canAttack = true;
        animator.SetBool("attacking", false);
    }

    void OnWaypointTick()
    {
        action = Action.wayPoint;
        if (waypointTarget == null)
        {
            waypointTarget = GetRandomWaypoint().transform;
        }

        if (ReachedTarget(waypointTarget))
        {
            Waypoint cur = waypointTarget.GetComponent<Waypoint>();
            waypointTarget = (cur.next != null ? cur.next.transform : cur.prev.transform);
        }

        //View check
        IsInView(waypointTarget, true);
        OnLookForPlayerTarget();
        OnMovementTick(waypointTarget);
    }

    void OnDestroyBuildingTick()
    {
        if (playerTarget == null)
        {
            OnLookForPlayerTarget();
        }
        //Remove closest obstacle till path becomes available
        if (HasUnreachableTarget())
        {
            if (attacking_item == null)
            {
                AssignAttackingItem();
            }
        }
        else
        {
            Option option = RandomOption(Option.a, DEFAULT_LEANING_PERCENTAGE);

            if (option == Option.a)
            {
                //Set state back to root
                action = Action.root;
                return;
            }
            else
            {
                if (attacking_item == null)
                {
                    //Set state back to root when lost attacking_item
                    action = Action.root;
                    return;
                }
            }
        }

        if (attacking_item)
        {
            //Validate current attacking_item
            if(attacking_item.isPickupMode)
            {

                attacking_item = null;
                action = Action.root;
                return;

            }
            if (!CanReachTarget(attacking_item.transform.position))
            {
                if(aTargetInfo.selectionMethod == TargetInfo.SelectionMethod.nearPlayer)
                {
                    AssignAttackingItem(true);
                }
                else
                {
                    //Fallback, shouldn't reach
                    Debug.LogError("AI Fallback: Can't reach selected attack_items near AI");
                }
            }

            if (Distance(attacking_item.transform, transform) <= attack_distance)
            {
                FaceTarget(attacking_item.transform);
                if (canAttack)
                {
                    StopMovement();
                    DamageBlock();
                }

            }
            else
            {
                ResumeMovement();
                OnMovementTick(attacking_item.transform);
            }
        }
        else
        {
            action = Action.root;
        }


    }
    #endregion

    #region Core Processing Functions

    void DamageBlock()
    {
        if (!attacking_item.isPickupMode)
        {
            Attack(AttackType.building);
        }
        else
        {
            RemoveOBSCache(obstacles.IndexOf(attacking_item.transform));
            attacking_item = null;
        }
    }

    void ProcessShouldChasePlayer(Transform target)
    {
        Player player = target.GetComponent<Player>();
        if (player != null && player.sanity > 0)
        {
            playerTarget = target;
            playerCache = player;
        }
    }

    #endregion
    #endregion
    #region Vision Core

    void OnOBSTick()
    {
        Collider[] obstaclesInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, obstacleMask);
        for (int i = 0; i < obstaclesInViewRadius.Length; i++)
        {
            Transform t = obstaclesInViewRadius[i].transform.parent != null ? obstaclesInViewRadius[i].transform.parent : obstaclesInViewRadius[i].transform;
            NavMeshObstacle obs = t.gameObject.GetComponent<NavMeshObstacle>();
            AddOBSCache(t, obs);
        }

        if (HasUnreachableTarget())
        {
            action = Action.destroyBuilding;
        }
    }
    //Add Navmesh Obstacle component and add to cache when size < 100, when greater than 100 start offload cache
    void OnLookForPlayerTarget()
    {
        if (playerTarget != null) return;
        visibleTargets.Clear();
        Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);
        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            if (IsInView(targetsInViewRadius[i].transform, false))
            {
                Transform t = targetsInViewRadius[i].transform.parent != null ? targetsInViewRadius[i].transform.parent : targetsInViewRadius[i].transform;
                if (!visibleTargets.Contains(t))
                {
                    visibleTargets.Add(t);
                }
            }

        }

        if (visibleTargets.Count == 0)
        {
            return;
        }

        ProcessShouldChasePlayer(FindClosestTransform(visibleTargets));

    }

    #region Check Functions
    bool IsInViewFrustum(Vector3 pos)
    {
        Vector3 dirToTarget = (pos - transform.position).normalized;
        if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2)
        {
            return true;
        }
        return false;
    }

    bool IsInViewFrustum(Transform t)
    {
        return IsInViewFrustum(t.position);
    }

    public bool IsInView(Transform t, bool checkPath)
    {
        if (t == null)
        {
            return false;
        }

        return IsInView(t.position, checkPath);
    }

    public bool IsInView(Transform target, out Transform obstacle)
    {
        Vector3 t = target.position;
        Vector3 dirToTarget = (t - transform.position).normalized;
        RaycastHit info;
        if (IsInViewFrustum(t))
        {
            float dstToTarget = Vector3.Distance(transform.position, t);
            if (!Physics.Raycast(transform.position, dirToTarget, out info, dstToTarget, obstacleMask))
            {
                obstacle = null;
                return true;
            }
            else
            {
                obstacle = info.collider.transform;
                return false;
            }
        }

        obstacle = null;
        return false;
    }
    //Core
    public bool IsInView(Vector3 t, bool checkPath)
    {

        Vector3 dirToTarget = (t - transform.position).normalized;
        RaycastHit info;
        if (IsInViewFrustum(t))
        {
            float dstToTarget = Vector3.Distance(transform.position, t);
            if (!Physics.Raycast(transform.position, dirToTarget, out info, dstToTarget, obstacleMask))
            {
                return true;
            }
            else
            {

                if (checkPath)
                {
                    if (!CanReachTarget(t))
                    {
                        action = Action.destroyBuilding;
                    }

                }
                return false;
            }
        }
        else
        {
            if (checkPath)
            {
                if (!CanReachTarget(t))
                {
                    action = Action.destroyBuilding;
                }

            }
        }
        return false;
    }
    #endregion
    #endregion

    #region Helper Functions

    bool HasUnreachableTarget()
    {
        return (HasValidTarget() && !HasReachableTarget());
    }

    bool HasValidTarget()
    {
        Transform target = playerTarget != null ? playerTarget : waypointTarget;
        if (target)
        {
            return true;
        }
        return false;
    }
    bool HasReachableTarget()
    {
        Transform target = playerTarget != null ? playerTarget : waypointTarget;
        if (target != null && CanReachTarget(target.position))
        {
            return true;
        }
        return false;
    }

    void AssignAttackingItem(bool forceNearAI)
    {
        if (forceNearAI)
        {
            aTargetInfo.selectionMethod = TargetInfo.SelectionMethod.nearAI;
            attacking_item = FindClosestTransform(obstacles)?.GetComponent<BuildItem>();
        }
        else
        {
            AssignAttackingItem();
        }
        
    }

    void AssignAttackingItem()
    {
        if (playerTarget)
        {
            Option option = RandomOption(Option.a, DEFAULT_LEANING_PERCENTAGE);

            //Pick a block closest to player target
            if (option == Option.a)
            {
                aTargetInfo.selectionMethod = TargetInfo.SelectionMethod.nearPlayer;
                attacking_item = FindClosestTransform(obstacles, playerTarget, true)?.GetComponent<BuildItem>();
            }
        }
        AssignAttackingItem(true);
    }

    void AddOBSCache(Transform t, NavMeshObstacle obs)
    {
        if (t.GetComponent<BuildItem>().isPickupMode) return;
        if (!obs.enabled)
        {
            if (obsCache.Count < max_obstacles_cache)
            {
                obs.enabled = true;
                obsCache.Add(obs);
                obstacles.Add(t);

            }
            else
            {
                //Remove oldest cache and insert new one
                RemoveOBSCache(0);

                obs.enabled = true;
                obsCache.Add(obs);
                obstacles.Add(t);
            }
        }
    }

    void RemoveOBSCache(int index)
    {
        if (index < 0 || index > obsCache.Count - 1) return;
        obsCache[index].enabled = false;
        obsCache.RemoveAt(index);
        obstacles.RemoveAt(index);
    }

    Transform FindClosestTransform(List<Transform> list)
    {
        return FindClosestTransform(list, transform, false);
    }

    Transform FindClosestTransform(List<Transform> list, Transform point, bool validate)
    {
        if (list == null || list.Count == 0) return null;

        Transform _closest = list[0];
        float min = Distance(point, _closest);

        if (validate)
        {
            Transform _validT = null;
            foreach (Transform t in list)
            {
                if (CanReachTarget(t.position))
                {
                    _validT = t;
                    break;
                }
            }

            if (_validT)
            {
                _closest = _validT;
                min = Distance(point, _validT);
            }
            else
            {
                return null;
            }

        }

        //Find closest building block
        foreach (Transform t in list)
        {
            if (t == null) continue;
            float d = Distance(t, point);
            if (d < min)
            {
                if (validate)
                {
                    if (CanReachTarget(t.position))
                    {
                        min = d;
                        _closest = t;
                        continue;
                    }
                    continue;
                }
                else
                {
                    min = d;
                    _closest = t;
                }

            }
        }

        return _closest;
    }

    bool IsAttackDistance(Transform target)
    {
        return Distance(target, transform) <= attack_distance ? true : false;
    }

    bool ReachedTarget(Vector3 a, Vector3 b)
    {
        return Distance(a, b) <= target_reached_distance ? true : false;
    }

    bool ReachedTarget(Transform target)
    {
        return Distance(target, transform) <= target_reached_distance ? true : false;
    }
    Waypoint GetRandomWaypoint()
    {
        WaypointGroup group = globalWaypoint.groups[Random.Range(0, globalWaypoint.groups.Length)];
        return group.waypoints[Random.Range(0, group.waypoints.Length)];
    }

    public bool RandomNextPoint(float range, out Vector3 result)
    {
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomPoint = transform.position + Random.insideUnitSphere * range;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 1.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = Vector3.zero;
        return false;
    }

    private float Distance2D(Vector3 a, Vector3 b)
    {
        float xDiff = a.x - b.x;
        float zDiff = a.z - b.z;
        return Mathf.Sqrt((xDiff * xDiff) + (zDiff * zDiff));
    }

    public float Distance(Transform a, Transform b)
    {
        float xDiff = a.position.x - b.position.x;
        float zDiff = a.position.z - b.position.z;
        return Mathf.Sqrt((xDiff * xDiff) + (zDiff * zDiff));
    }

    public float Distance(Vector3 a, Vector3 b)
    {
        float xDiff = a.x - b.x;
        float zDiff = a.z - b.z;
        return Mathf.Sqrt((xDiff * xDiff) + (zDiff * zDiff));
    }

    void OnPlayerTargetChanged(Transform oldPlayer, Transform newPlayer)
    {

        Player oPlayer = oldPlayer.GetComponent<Player>();
        Player nPlayer = newPlayer.GetComponent<Player>();
        if (oPlayer != null)
        {
            oPlayer.OnBeingChasedByAI(false, oPlayer.gameObject, netId);
        }
        if (nPlayer != null)
        {
            nPlayer.OnBeingChasedByAI(true, nPlayer.gameObject, netId);
        }
    }

    bool CanReachTarget(Vector3 pos)
    {
        return CanReachTarget(pos, false);
    }

    bool CanReachTarget(Vector3 pos, bool updateAgent)
    {
        NavMeshPath p = new NavMeshPath();

        if (agent.CalculatePath(pos, p) && p.status != NavMeshPathStatus.PathPartial)
        {
            if (updateAgent)
            {
                agent.path = p;
            }
            return true;
        }

        return false;
    }
    void ResetOBSCache()
    {
        foreach (NavMeshObstacle nmo in obsCache)
        {
            nmo.enabled = false; ;
        }
        obsCache.Clear();
        obstacles.Clear();
    }
    #region Debug Functions
    public enum LogType
    {
        stateChange,
        fallBack,
        simple
    }
    public void AILog(string msg, LogType type)
    {
        if (type == LogType.simple)
        {
            Debug.Log("AI: [" + name + "] " + msg);
        }
        else if (type == LogType.stateChange)
        {
            Debug.Log("AI: [" + name + "] state changed to " + msg);
        }
        else if (type == LogType.fallBack)
        {
            Debug.Log("AI: [" + name + "] path fallback, reason: " + msg);
        }
    }
    #endregion
    #endregion

    #region Decision Making
    enum Option
    {
        a,
        b
    }

    Option RandomOption(Option leaningSide)
    {
        return RandomOption(leaningSide, 30);
    }

    Option RandomOption(Option leaningSide, float leaningPercentage)
    {
        float rand = Random.Range(0f, 1f);
        float c = leaningPercentage / 100;
        if (rand < 0.5f + c)
        {
            return leaningSide;
        }
        else
        {
            if (leaningSide == Option.a)
            {
                return Option.b;
            }
            else
            {
                return Option.a;
            }
        }
    }
    #endregion

    #region AI Movement & Networking Functions

    public enum AttackType
    {
        shortAttack,
        longAttack,
        building
    }

    void Attack(AttackType type)
    {
        canAttack = false;
        StopMovement();
        animator.SetBool("attacking", true);

        if (type == AttackType.shortAttack)
        {
            animator.SetTrigger("s_attack");
        }
        else if (type == AttackType.longAttack)
        {
            animator.SetTrigger("l_attack");
        }
        else if (type == AttackType.building)
        {
            animator.SetTrigger("l_attack");
        }

        if (finalAttacking != null)
        {
            StopCoroutine(finalAttacking);
        }
        finalAttacking = StartCoroutine(DelayedFinalAttack(type));

        if (attackCoolDownTimer != null)
        {
            StopCoroutine(attackCoolDownTimer);
        }
        attackCoolDownTimer = StartCoroutine(AttackTimer());
    }

    IEnumerator DelayedFinalAttack(AttackType type)
    {
        float delay = type == AttackType.shortAttack ? 0.86f : 1.38f; //Allow player to dodge attack by view & distance checking after delay before damage
        yield return new WaitForSeconds(delay);

        float extraReachBias = 0.68f; //[Difficulty] Manipulate for difficulty
        int chargedAttackDamage = chargedAttackMode == ChargedAttackMode.specified ? charged_attack_damage : (int)(attack_damage + attack_damage * 0.5f); //[Difficulty] Manipulate multiply bias for difficulty
        Transform obstacle;
        if (type == AttackType.shortAttack)
        {
            if (playerTarget)
            {
                if (IsInView(playerTarget, out obstacle) && Distance(transform, playerTarget) <= (attack_distance / 2) + extraReachBias)
                {
                    playerCache.OnSanityHit(chargedAttackDamage);
                }
                else if (obstacle)
                {
                    obstacle?.GetComponent<BuildItem>().Damage(chargedAttackDamage);
                }
            }
           
        }

        else if (type == AttackType.longAttack)
        {
            if (playerTarget)
            {
                if (IsInView(playerTarget, out obstacle) && Distance(transform, playerTarget) <= (attack_distance + extraReachBias))
                {
                    playerCache.OnSanityHit(attack_damage);
                }
                else if (obstacle)
                {
                    obstacle?.GetComponent<BuildItem>().Damage(attack_damage);
                }
            }
            
        }

        else if (type == AttackType.building)
        {
            if (attacking_item)
            {
                if (Distance2D(transform.position, attacking_item.transform.position) <= attack_distance)
                {
                    attacking_item.Damage(attack_damage);
                }
            }
            
        }

    }
    void OnSpeedTick()
    {
        if (canAttack)
        {
            animator.SetFloat("speed", speed / runSpeed);
        }
    }

    void RefreshAgentSpeed()
    {
        agent.speed = speed;
    }
    void StopMovement()
    {
        if (!agent.isStopped)
        {
            agent.isStopped = true;
            speed = 0;
            RefreshAgentSpeed();
        }
    }

    void ResumeMovement()
    {
        if (agent.isStopped)
        {
            agent.isStopped = false;
            speed = normalSpeed;
            RefreshAgentSpeed();
        }
    }

    bool OnMovementTick(Transform target)
    {
        ResumeMovement();
        return (agent.SetDestination(target.position));
    }

    void FaceTarget(Transform target)
    {
        Vector3 lookPos = target.position - transform.position;
        lookPos.y = 0;
        Quaternion rotation = Quaternion.LookRotation(lookPos);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 2);
    }
    #endregion

    #region AI States

    void RefreshAIState()
    {
        if (action == Action.hasPlayer)
        {
            ResetOBSCache();
        }
    }

    #endregion

    #region Debug View
    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
    #endregion

    #region Cache & Data Structures
    public class TargetInfo
    {
        public enum SelectionMethod
        {
            nearAI,
            nearPlayer
        }
        public SelectionMethod selectionMethod;

    }
    #endregion
}

