//© 2023 by MADKEV Studio, all rights reserved
///Info: Default Animation Clip States:
///      attack
///      speed
///      idle
///      forceState (boolean) //To stop idle and speed transitions
///Override functions for specific AI implementations      
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
[RequireComponent(typeof(GhostIdentity))]
public class GhostAI : NetworkBehaviour
{
    #region Default Fields
    [Header("Setup")]
    public Animator animator;
    public NavMeshAgent agent;
    public Transform head;
    [Header("Settings")]
    public float tickRate = 0.5f;
    public int attackDamage = 25;
    public int chargedAttackDamage = 100;
    public float attackDistance = 1.68f;
    public float visionDistance = 10f;
    public float losePlayerTime = 6f;
    private float targetReachedDistance = 1.86f;
    public float normalSpeed = 1.6f;
    public float runSpeed = 2.5f;
    public static string FORCE_STATE = "forceState";
    [Header("Cache settings")]
    public int obsCacheSize = 128;
    //Field of View
    [Header("Field Of View")]
    public LayerMask targetMask; //For players
    public LayerMask obstacleMask; //For walls/blocks etc
    protected LayerMask doorMask;
    protected LayerMask excludeSelfMask; //Everything except Ghost layer
    [Range(0, 360)]
    public float viewAngle = 145f;
    static int DEFAULT_LEANING_PERCENTAGE = 35;
    #endregion

    #region Runtime Cache
    GlobalWaypoint globalWaypoint;
    protected List<Transform> visibleTargets = new List<Transform>();
    protected List<NavMeshObstacle> obsCache = new List<NavMeshObstacle>();
    protected List<Transform> obstacles = new List<Transform>();
    protected float currentSpeed;
    public AITarget currentTarget;
    public AIAttackingTarget currentAttackTarget;
    #endregion

    #region Coroutines Reference
    protected Coroutine AIPlaySingleAnimationCoroutine;
    protected Coroutine AIWatchForPlayerDuringPauseCoroutine;
    protected Coroutine AIWaypointReachedCoroutine;
    protected Coroutine AIDoorHandlingCoroutine;
    protected Coroutine AIAnimatedAttackCoroutine;
    protected Coroutine AIRotationCoroutine;
    protected Coroutine AIBrainCoroutine;
    protected Coroutine AITimedLosePlayerCoroutine;
    #endregion

    #region Initialization
    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    public virtual void Start()
    {
        if (SceneManager.GetActiveScene().name == "Forest")
        {
            ForestManager.Instance.AddClient(transform);
        }
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        agent.autoBraking = false;
        doorMask = LayerMask.GetMask("Door");
        excludeSelfMask = ~LayerMask.GetMask("Ghost");
        SetSpeed(normalSpeed);
        globalWaypoint = GlobalWaypoint.Instance;

        InitializeGhostAI();
        AIBrainCoroutine = StartCoroutine("TickAI", tickRate);
    }

    public virtual void InitializeGhostAI()
    {
        //Set obstacle mask to include BuildItem by default
        obstacleMask = (1 << LayerMask.NameToLayer("BuildItem"));
    }

    /// <summary>
    /// By default returns middle ground Vector3 between transform.position and head.position. Returns transform.position if head is null.
    /// </summary>
    public virtual Vector3 CalculateVisionPosition()
    {
        if (head == null) return transform.position;
        return new Vector3(transform.position.x, transform.position.y + ((head.position.y - transform.position.y) / 2), transform.position.z);
    }

    #endregion

    #region Core Coroutine

    protected virtual IEnumerator TickAI(float rate)
    {
        while (true)
        {
            yield return new WaitForSeconds(rate);
            OnAITick();
        }
    }
    protected virtual IEnumerator TimedLosePlayer(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (currentTarget == null || currentTarget.playerTargetCache == null)
        {
            currentTarget = null;
            AITimedLosePlayerCoroutine = null;
            yield break;
        }

        if (!ValidateTarget(currentTarget.targetTransform).HasVisibleTarget())
        {
            OnPlayerTargetChanged(currentTarget.playerTargetCache, null);
            AITimedLosePlayerCoroutine = null;
            currentTarget = null;
            yield break;
        }

        AITimedLosePlayerCoroutine = null;
    }

    protected virtual IEnumerator SmoothRotation(Quaternion rotation)
    {
        float progress = 0;
        while (progress < 1)
        {
            progress += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * normalSpeed);

            yield return null;
        }
    }

    protected virtual IEnumerator AnimatedAttack()
    {
        PauseMainAILoop();
        FaceTarget(currentAttackTarget.targetTransform);
        PlaySingleAnimation("attack");
        yield return new WaitForEndOfFrame();

        //By default, we want to damage player during 1/2 of AI attack animation as that's when the arm swings down
        float attackAnimationDuration = animator.GetCurrentAnimatorStateInfo(0).length;
        float timeToDealDamage = attackAnimationDuration - attackAnimationDuration / 2;
        yield return new WaitForSeconds(timeToDealDamage);

        //Because player may be near AI, vision field is narrow and player may be outside of field if AI animation swings too big
        //To get around this, I will increase the vision range to 205 for simply checking AI is facing the general direction of player
        float tempViewAngle = viewAngle;
        viewAngle = 205;
        if (IsAttackDistance(currentAttackTarget.targetTransform) && ValidateTarget(currentAttackTarget.targetTransform).HasVisibleTarget())
        {
            DealPlayerDamage();
        }
        //else
        //{
        //    //Debug.LogError("Attack failed: Target in view: " + IsInViewFrustum(currentAttackTarget.targetTransform) + " Attack Distance: " + IsAttackDistance(currentAttackTarget.targetTransform));
        //}

        //Reset viewAngle back to original defined value
        viewAngle = tempViewAngle;

        //Wait till remaining amount of animation finishes playing
        yield return new WaitForSeconds(attackAnimationDuration - timeToDealDamage);
        AIAnimatedAttackCoroutine = null;
        ResumeMainAILoop();
    }
    protected virtual IEnumerator AnimatedDoorHandling(Door door)
    {
        PauseMainAILoop();
        yield return new WaitForSeconds(Random.Range(1, 3));

        //Need another check here as player could have opened the door during AI wait period.
        if (!door.isOpened)
        {
            door.Interact();
        }

        AIDoorHandlingCoroutine = null;
        ResumeMainAILoop();
    }
    protected virtual IEnumerator ProcessWaypointReached()
    {
        PauseMainAILoop();
        yield return new WaitForSeconds(Random.Range(3, 8));
        AssignWaypointTarget(GetNextWaypoint(currentTarget.waypointTargetCache));

        AIWaypointReachedCoroutine = null;
        ResumeMainAILoop();
    }

    /// <summary>
    /// This tick is irregular and only used when AI main loop is paused (animating seeking at a target destination without speed), but still want to maintain ability to detect player
    /// </summary>
    /// <returns></returns>
    protected virtual IEnumerator PausedPlayerDetectionTick()
    {
        while (true)
        {
            OnLookForPlayerTargetTick();
            yield return new WaitForSeconds(tickRate);
        }
    }

    /// <summary>
    /// Stops default animator states from playing, plays a clip and resumes default animator state when finished.
    /// </summary>
    /// <param name="clipName"></param>
    /// <returns></returns>
    protected virtual IEnumerator PlaySingleAnimationClip(string clipName)
    {
        StopDefaultAnimationStates();
        animator.Play(clipName);
        yield return new WaitForEndOfFrame();
        float animationDuration = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(animationDuration);
        ResumeDefaultAnimationStates();
        AIPlaySingleAnimationCoroutine = null;
    }
    #endregion

    #region Core Ticks
    public virtual void OnAITick()
    {
        OnOBSTick();
        if (currentTarget == null || currentTarget.IsWaypointTarget())
        {
            OnAIWaypointTick();
        }
        else
        {
            if (currentTarget.playerTargetCache && currentTarget.playerTargetCache.health > 0)
            {
                OnAIChasePlayerTick();
            }
            else
            {
                currentTarget = null;
            }
        }
    }
    public virtual void OnAIChasePlayerTick()
    {
        //Player is visible
        bool playerVisible = ValidateTarget(currentTarget.targetTransform).HasVisibleTarget();
        if (playerVisible)
        {
            //Reset lose player timer
            if (AITimedLosePlayerCoroutine != null)
            {
                StopCoroutine(AITimedLosePlayerCoroutine);
            }
            AITimedLosePlayerCoroutine = StartCoroutine(TimedLosePlayer(GetSceneMultiplayer() * Random.Range(12, 18)));
        }

        if (IsAttackDistance(currentTarget.targetTransform))
        {
            SetPlayerAttackTarget(currentTarget.playerTargetCache);
            Attack();
            return;
        }
        //Pathfinding reachable is handled through OnOBSTick()
        OnMovementTick(currentTarget.targetTransform);
    }
    public virtual void OnAIWaypointTick()
    {
        if (currentTarget == null)
        {
            AssignWaypointTarget(GetRandomWaypoint());
        }

        if (ReachedCurrentTarget())
        {
            if (AIWaypointReachedCoroutine == null)
            {
                AIWaypointReachedCoroutine = StartCoroutine(ProcessWaypointReached());
            }
        }

        OnLookForPlayerTargetTick();
        OnMovementTick(currentTarget.targetTransform);
    }

    public virtual void OnDestroyBuildingTick()
    {
        if (currentTarget == null || !currentTarget.IsValid()) return;
        //Remove closest obstacle till path becomes available
        if (HasUnreachableTarget())
        {
            if (currentAttackTarget == null && obstacles.Count > 0)
            {
                SetBuildingAttackTarget();
            }
        }
        else
        {
            currentAttackTarget = null;
        }

        if (currentAttackTarget != null)
        {
            //Remove BuildItem from OBS Cache if it's already destroyed (isPickupMode)
            if (currentAttackTarget.buildingCache != null && currentAttackTarget.buildingCache.isPickupMode)
            {
                RemoveOBSCache(obstacles.IndexOf(currentAttackTarget.buildingCache.transform));
                currentAttackTarget = null;
                return;

            }

            //If the picked BuildItem to attack is not path finding reachable, force select new one near AI since the previous one could be selected near Player.
            if (!ValidateTarget(currentAttackTarget.targetTransform).HasVisibleAndReachableTarget())
            {
                SetBuildingAttackTarget(true);
            }

            if (Distance2D(currentAttackTarget.targetTransform, transform) <= attackDistance)
            {
                FaceTarget(currentAttackTarget.targetTransform);
                Attack();
            }
            else
            {
                OnMovementTick(currentAttackTarget.targetTransform);
            }
        }
    }

    /// <summary>
    /// Handles door detection and NavmeshObstacle cache
    /// </summary>
    public virtual void OnOBSTick()
    {
        if (AIDoorHandlingCoroutine == null)
        {
            Collider[] doorsInView = Physics.OverlapSphere(transform.position, visionDistance, doorMask);
            foreach (var d in doorsInView)
            {
                if (IsInViewFrustum(d.transform.position) && Distance2D(transform, d.transform) <= targetReachedDistance && AIDoorHandlingCoroutine == null)
                {
                    Door door = d.GetComponentInParent<Door>();
                    if (!door.isOpened || door.IsDoorMoving())
                    {
                        StartCoroutine(AnimatedDoorHandling(door));
                        break;
                    }
                }
            }
        }

        //RaycastHit hit;
        //if (Physics.Raycast(CalculateVisionPosition(), transform.forward, out hit, visionDistance, doorMask))
        //{
        //    if (Distance2D(transform, hit.collider.transform) <= targetReachedDistance && AIDoorHandlingCoroutine == null)
        //    {
        //        Door door = hit.collider.GetComponentInParent<Door>();
        //        if (!door.isOpened)
        //        {
        //            StartCoroutine(AnimatedDoorHandling(door));
        //        }
        //    }
        //}

        Collider[] obstaclesInViewRadius = Physics.OverlapSphere(transform.position, visionDistance, obstacleMask);
        for (int i = 0; i < obstaclesInViewRadius.Length; i++)
        {
            Transform temp = obstaclesInViewRadius[i].transform.root != null ? obstaclesInViewRadius[i].transform.root : obstaclesInViewRadius[i].transform;
            AddOBSCache(temp, temp.GetComponent<NavMeshObstacle>());
        }

        if (HasUnreachableTarget())
        {
            OnTargetUnreachableTick();
        }
    }
    public virtual void OnLookForPlayerTargetTick()
    {
        if (currentTarget != null && currentTarget.IsPlayerTarget()) return;

        visibleTargets.Clear();
        Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, visionDistance, targetMask);
        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            if (IsAlertDistance(targetsInViewRadius[i].transform) || ValidateTarget(targetsInViewRadius[i].transform).HasVisibleTarget())
            {
                Transform t = targetsInViewRadius[i].transform.root != null ? targetsInViewRadius[i].transform.root : targetsInViewRadius[i].transform;
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

    /// <summary>
    /// By default ticks OnDestroyBuildingTick(). Note Door NavmeshObstacle has been removed to simple Raycast Check
    /// </summary>
    public virtual void OnTargetUnreachableTick()
    {
        OnDestroyBuildingTick();
    }

    #endregion

    #region Break Main Functions - These pause main loop
    /// <summary>
    /// When AI is main loop is paused but want to maintain ability to detect player.
    /// </summary>
    public virtual void StartPausedPlayerWatch()
    {
        if (AIWatchForPlayerDuringPauseCoroutine == null)
        {
            AIWatchForPlayerDuringPauseCoroutine = StartCoroutine(PausedPlayerDetectionTick());
        }
    }

    /// <summary>
    /// Stop paused player detection watch.
    /// </summary>
    public virtual void StopPausedPlayerWatch()
    {
        if (AIWatchForPlayerDuringPauseCoroutine != null)
        {
            StopCoroutine(AIWatchForPlayerDuringPauseCoroutine);
            AIWatchForPlayerDuringPauseCoroutine = null;
        }
    }

    /// <summary>
    /// Pauses main AI Tick, useful for functions that may need pause AI logic (ie. Attack, Dashing, etc)
    /// </summary>
    public virtual void PauseMainAILoop(bool pauseSpeed = true, bool maintainPlayerWatch = true)
    {
        if (AIBrainCoroutine == null) return;
        if (maintainPlayerWatch)
        {
            StartPausedPlayerWatch();
        }
        if (pauseSpeed)
        {
            SetSpeed(0);
        }
        StopCoroutine(AIBrainCoroutine);
        AIBrainCoroutine = null;
    }

    /// <summary>
    /// Resumes main AI Tick, useful for functions that may need pause AI logic (ie. Attack, Dashing, etc)
    /// </summary>
    public virtual void ResumeMainAILoop(bool resumeSpeed = true)
    {
        if (AIBrainCoroutine != null) return;
        StopPausedPlayerWatch();
        if (resumeSpeed)
        {
            SetSpeed(normalSpeed);
        }
        AIBrainCoroutine = StartCoroutine("TickAI", tickRate);
    }

    /// <summary>
    /// By default does target validation and starts AnimatedAttack coroutine
    /// </summary>
    public virtual void Attack()
    {
        if (currentAttackTarget == null) return;

        //Pre-Attack target validation
        switch (currentAttackTarget.targetType)
        {
            case AIAttackingTarget.TargetType.Building:
                if (currentAttackTarget.buildingCache == null || currentAttackTarget.buildingCache.isPickupMode)
                {
                    RemoveOBSCache(obstacles.IndexOf(currentAttackTarget.targetTransform));
                    currentAttackTarget = null;
                    return;
                }
                break;

            case AIAttackingTarget.TargetType.Player:
                if (currentAttackTarget.playerCache == null || currentAttackTarget.playerCache.health <= 0)
                {
                    currentAttackTarget = null;
                    return;
                }
                break;
        }

        //Start attack sequence, animation and time events are handled through coroutine
        if (AIAnimatedAttackCoroutine == null)
        {
            AIAnimatedAttackCoroutine = StartCoroutine(AnimatedAttack());
        }
    }

    /// <summary>
    /// By default subtracts player health by AI attack damage.
    /// </summary>
    public virtual void DealPlayerDamage()
    {
        currentAttackTarget?.playerCache?.OnSanityHit(attackDamage);
    }
    #endregion

    #region Core Processing Functions (Virtual)
    /// <summary>
    /// This function runs on the server only, used for starting and stopping Player chased audio.
    /// </summary>
    /// <param name="oldPlayer"></param>
    /// <param name="newPlayer"></param>
    [Server]
    public virtual void OnPlayerTargetChanged(Player oldPlayer, Player newPlayer)
    {
        if (oldPlayer != null)
        {
            oldPlayer.OnBeingChasedByAI(false, oldPlayer.gameObject);
        }

        if (newPlayer != null)
        {
            newPlayer.OnBeingChasedByAI(true, newPlayer.gameObject);
        }
    }

    /// <summary>
    /// Picks a build item (building piece) near player or near AI, depending on random option with DEFAULT_LEANING_PERCENTAGE leaning towards near player. 
    /// </summary>
    /// <param name="forceNearAI">Forces picking a build item near AI, useful if the previous near player pick is not path finding reachable.</param>
    public virtual void SetBuildingAttackTarget(bool forceNearAI = false)
    {
        if (obstacles == null || obstacles.Count == 0) return;
        currentAttackTarget = new AIAttackingTarget();
        Option option = forceNearAI ? Option.B : RandomOption(Option.A, DEFAULT_LEANING_PERCENTAGE);

        //Select build item near player
        if (option == Option.A)
        {
            currentAttackTarget.selectionMethod = AIAttackingTarget.SelectionMethod.nearPlayer;
            currentAttackTarget.targetTransform = FindClosestTransform(obstacles, currentTarget.targetTransform, true);
        }
        else
        {
            currentAttackTarget.selectionMethod = AIAttackingTarget.SelectionMethod.nearAI;
            currentAttackTarget.targetTransform = FindClosestTransform(obstacles);
        }

        currentAttackTarget.buildingCache = currentAttackTarget.targetTransform.GetComponent<BuildItem>();
    }

    /// <summary>
    /// Sets Player as attack target, will override current attack target if type isn't player.
    /// </summary>
    /// <param name="player"></param>
    public virtual void SetPlayerAttackTarget(Player player)
    {
        if (currentAttackTarget == null || currentAttackTarget.targetType != AIAttackingTarget.TargetType.Player)
        {
            currentAttackTarget = new AIAttackingTarget();
            currentAttackTarget.targetType = AIAttackingTarget.TargetType.Player;
            currentAttackTarget.targetTransform = player.transform;
            currentAttackTarget.playerCache = player;
        }
    }

    /// <summary>
    /// Decides if provided target (Player Transform) should be chased by AI. By default will immediately chase any player wit heath above 0.
    /// </summary>
    /// <param name="target"></param>
    public virtual void ProcessShouldChasePlayer(Transform target)
    {
        Player player = target.GetComponent<Player>();
        if (player != null && player.health > 0)
        {
            OnPlayerTargetChanged(currentTarget?.playerTargetCache, player);
            currentTarget = new AITarget(target, AITarget.TargetType.Player, player, null);
            ResumeMainAILoop();
        }
    }

    #endregion

    #region Helper Functions
    /// <summary>
    /// Used for playing animations like Attack, where it needs to exit out of idle and speed. Only played once
    /// </summary>
    public virtual void PlaySingleAnimation(string clipName)
    {
        if (AIPlaySingleAnimationCoroutine == null)
        {
            AIPlaySingleAnimationCoroutine = StartCoroutine(PlaySingleAnimationClip(clipName));
        }
    }

    public void StopDefaultAnimationStates()
    {
        animator.SetBool(FORCE_STATE, true);
    }
    public void ResumeDefaultAnimationStates()
    {
        animator.SetBool(FORCE_STATE, false);
    }
    /// <summary>
    /// Sets AI target to a waypoint if currentTarget isn't player
    /// </summary>
    /// <param name="waypoint"></param>
    protected void AssignWaypointTarget(Waypoint waypoint)
    {
        if (currentTarget == null || currentTarget.targetType != AITarget.TargetType.Player)
        {
            currentTarget = new AITarget(waypoint.transform, AITarget.TargetType.Waypoint, null, waypoint);
        }
    }

    /// <summary>
    /// Checks whether a position in world space is within the view frustum of AI. This does not check for blockage and obstacles.
    /// </summary>
    /// <param name="position">Specified position in world space</param>
    /// <param name="checkDistance">Whether to check if target is within AI view distance range. True by default</param>
    /// <returns></returns>
    protected bool IsInViewFrustum(Vector3 position, bool checkDistance = true)
    {
        if (checkDistance && Distance2D(transform.position, position) > visionDistance) return false;
        if (Vector3.Angle(transform.forward, (position - transform.position).normalized) < viewAngle / 2)
        {
            return true;
        }
        return false;
    }
    protected bool IsInViewFrustum(Transform target)
    {
        return IsInViewFrustum(target.position);
    }

    /// <summary>
    /// Validates if a target is in view frustum, not blocked by obstacles and path finding reachable. Results are returned in a struct.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    protected AITargetValidationResult ValidateTarget(Transform target)
    {
        Vector3 tPos = target.position;

        if (IsInViewFrustum(tPos))
        {
            //09/10/2023, re-factored to InternalCheckForVisionBlockage() for more accurate multple ray cast check.
            //if (!Physics.Raycast(CalculateVisionPosition(), dirToTarget, out info, dstToTarget, excludeSelfMask))
            //{
            //    return new AITargetValidationResult(true, CanReachTarget(tPos), null);
            //}
            Transform _obstacle;
            if(InternalCheckForVisionBlockage(target, out _obstacle))
            {
                return new AITargetValidationResult(true, CanReachTarget(tPos), null);
            }

            //Debug.LogError("AI Obstacle: " + _obstacle.transform.gameObject.name);
            return new AITargetValidationResult(false, CanReachTarget(tPos), _obstacle);
        }
        return new AITargetValidationResult(false, CanReachTarget(tPos), null);
    }

    /// <summary>
    /// Internal function for validating if target is blocked by obstacle and cannot be seen by AI.
    /// This checks for partial blockage by offsetting multiple ray casts up/down, left/right from the ray directly to target.
    /// Increasing the number of rays could lead to higher accuracy but more performance hit.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="obstacle"></param>
    /// <returns>true if target can be seen by AI, false otherwise</returns>
    protected virtual bool InternalCheckForVisionBlockage(Transform target, out Transform obstacle)
    {
        Vector3 toTarget = target.position - transform.position;

        // Define the number of ray casts and the angle step for horizontal and vertical scans.
        int horizontalRays = 5; // Number of horizontal rays
        int verticalRays = 3;   // Number of vertical rays

        float horizontalStep = 5.0f; // Angle step for horizontal rays
        float verticalStep = 15.0f;  // Angle step for vertical rays

        // Calculate the initial ray direction (straight towards the target).
        Vector3 initialRayDirection = toTarget.normalized;
        float dstToTarget = Vector3.Distance(transform.position, target.position);

        RaycastHit info;
        obstacle = null;
        for (int h = 0; h < horizontalRays; h++)
        {
            for (int v = 0; v < verticalRays; v++)
            {
                float horizontalOffset = (horizontalRays > 1) ? (-horizontalStep * 0.5f + h * horizontalStep / (horizontalRays - 1)) : 0.0f;
                float verticalOffset = (verticalRays > 1) ? (-verticalStep * 0.5f + v * verticalStep / (verticalRays - 1)) : 0.0f;

                Vector3 rayDirection = Quaternion.Euler(verticalOffset, horizontalOffset, 0) * initialRayDirection;


                if (!Physics.Raycast(CalculateVisionPosition(), rayDirection, out info, dstToTarget, excludeSelfMask))
                {
                    obstacle = null;
                    return true;
                }

                //Update obstacle, currently we update each time so the last obstacle will be returned
                obstacle = info.collider.transform;
            }
        }

        return false;
    }
    /// <summary>
    /// Returns current waypoint.next, if current or current.next is null, picks a random waypoint.
    /// </summary>
    /// <param name="current"></param>
    /// <returns></returns>
    protected Waypoint GetNextWaypoint(Waypoint current)
    {
        if (current?.next != null)
        {
            return current.next;
        }

        return GetRandomWaypoint();
    }

    /// <summary>
    /// Returns true if currentTarget is not null and path finding unreachable.
    /// </summary>
    /// <returns></returns>
    protected bool HasUnreachableTarget()
    {
        if (currentTarget == null) return false;

        return !CanReachTarget(currentTarget.targetTransform.position);
    }

    /// <summary>
    /// Adds Navmesh Obstacles reference to cache where they contribute to AI path finding. For optimization, when cache exceeds obsCacheSize, first added reference will be removed for every new addition.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="obs"></param>
    protected void AddOBSCache(Transform t, NavMeshObstacle obs)
    {
        if (t.GetComponent<BuildItem>().isPickupMode) return;
        if (!obs.enabled)
        {
            if (obsCache.Count < obsCacheSize)
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

    /// <summary>
    /// Removes NavmeshObstacle reference from cache based on given index.
    /// </summary>
    /// <param name="index"></param>
    protected void RemoveOBSCache(int index)
    {
        if (index < 0 || index > obsCache.Count - 1) return;
        //obsCache[index].enabled = false;
        obsCache.RemoveAt(index);
        obstacles.RemoveAt(index);
    }

    /// <summary>
    /// Returns closest Transform in a list, null if list is null or empty.
    /// </summary>
    /// <param name="list"></param>
    /// <returns></returns>
    protected Transform FindClosestTransform(List<Transform> list)
    {
        return FindClosestTransform(list, transform, false);
    }

    /// <summary>
    /// Returns closest Transform in a list, null if list is null or empty. If validate = true, returns closest path finding reachable Transform, null if not found.
    /// </summary>
    /// <param name="list"></param>
    /// <param name="point"></param>
    /// <param name="validate"></param>
    /// <returns></returns>
    protected Transform FindClosestTransform(List<Transform> list, Transform point, bool validate)
    {
        if (list == null || list.Count == 0) return null;

        Transform _closest = list[0];
        float min = Distance2D(point, _closest);

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
                min = Distance2D(point, _validT);
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
            float d = Distance2D(t, point);
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

    /// <summary>
    /// Returns true if target Transform has distance <= attackDistance.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    protected bool IsAttackDistance(Transform target)
    {
        return Distance2D(target, transform) <= attackDistance ? true : false;
    }

    /// <summary>
    /// Returns true if target Transform has distance <= distance
    /// </summary>
    /// <param name="target"></param>
    /// <param name="distance"></param>
    /// <returns></returns>
    protected bool IsWithinDistance(Transform target, float distance)
    {
        return Distance2D(target, transform) <= distance ? true : false;
    }

    /// <summary>
    /// Returns true if Player is in very close to the AI, by default visionDistance/2. Even if player is not in view frustum, AI would be alerted.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    protected virtual bool IsAlertDistance(Transform target)
    {
        return Vector3.Distance(target.position, transform.position) <= (visionDistance / 2) ? true : false;
    }


    /// <summary>
    /// Returns true if currentTarget isn't null and has distance to AI <= targetReachedDistance
    /// </summary>
    /// <returns></returns>
    protected bool ReachedCurrentTarget()
    {
        if (currentTarget == null) return false;
        return Distance2D(currentTarget.targetTransform, transform) <= targetReachedDistance ? true : false;
    }

    /// <summary>
    /// Returns a random Waypoint from globalWaypoint group. Uses HMSpawnPoints for levels other than Forest.
    /// </summary>
    /// <returns></returns>
    protected Waypoint GetRandomWaypoint()
    {
        WaypointGroup group = globalWaypoint.groups[Random.Range(0, globalWaypoint.groups.Length)];
        return group.waypoints[Random.Range(0, group.waypoints.Length)];
    }

    /// <summary>
    /// Returns true if successfully sampled a Navmesh point near AI in the range provided. Vector3 of successful sample returned through out parameter. 
    /// </summary>
    /// <param name="range">Range of sampling.</param>
    /// <param name="result">Vector3 of sample result, returns Vector3.zero if unsuccessful</param>
    /// <returns></returns>
    protected bool SampleRandomNextPoint(float range, out Vector3 result)
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

    /// <summary>
    /// Returns 2D distance between two Vectors, ignores Y.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    protected float Distance2D(Vector3 a, Vector3 b)
    {
        float xDiff = a.x - b.x;
        float zDiff = a.z - b.z;
        return Mathf.Sqrt((xDiff * xDiff) + (zDiff * zDiff));
    }

    /// <summary>
    /// Returns 2D distance between two Transforms, ignores Y.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    protected float Distance2D(Transform a, Transform b)
    {
        float xDiff = a.position.x - b.position.x;
        float zDiff = a.position.z - b.position.z;
        return Mathf.Sqrt((xDiff * xDiff) + (zDiff * zDiff));
    }

    /// <summary>
    /// Returns true if a Vector3 position can be reached by AI.
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    protected bool CanReachTarget(Vector3 pos)
    {
        NavMeshPath p = new NavMeshPath();
        return (agent.CalculatePath(pos, p) && p.status == NavMeshPathStatus.PathComplete);
    }

    /// <summary>
    /// Clears OBS Cache (both the Transform list and NavmeshObstacle list).
    /// </summary>
    protected void ResetOBSCache()
    {
        foreach (NavMeshObstacle nmo in obsCache)
        {
            nmo.enabled = false; ;
        }
        obsCache.Clear();
        obstacles.Clear();
    }
    #endregion

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

    #region Decision Making

    /// <summary>
    /// Returns different multiplier value depending on Scene for different AI behaviors (probability of picking a certain option).
    /// </summary>
    /// <returns></returns>
    public virtual float GetSceneMultiplayer()
    {
        if (SceneManager.GetActiveScene().name == "Forest")
        {
            return 1.5f;
        }

        return 1;
    }

    protected enum Option
    {
        A,
        B
    }

    /// <summary>
    /// Returns a random option (A/B) with leaning to specified leaning side of DEFAULT_LEANING_PERCENTAGE.
    /// </summary>
    /// <param name="leaningSide"></param>
    /// <returns></returns>
    protected Option RandomOption(Option leaningSide)
    {
        return RandomOption(leaningSide, DEFAULT_LEANING_PERCENTAGE);
    }

    /// <summary>
    /// Returns a random option (A/B) with leaning to specified leaning side of specified leaning percentage.
    /// </summary>
    /// <param name="leaningSide"></param>
    /// <param name="leaningPercentage"></param>
    /// <returns></returns>
    protected Option RandomOption(Option leaningSide, float leaningPercentage)
    {
        float rand = Random.Range(0f, 1f);
        float c = leaningPercentage / 100;
        if (rand < 0.5f + c)
        {
            return leaningSide;
        }
        else
        {
            if (leaningSide == Option.A)
            {
                return Option.B;
            }
            else
            {
                return Option.A;
            }
        }
    }
    #endregion

    #region Movement
    public virtual void SetSpeed(float _speed)
    {
        agent.speed = _speed;
        currentSpeed = _speed;
        animator.SetFloat("speed", _speed / runSpeed);
    }

    public virtual void StopMovement()
    {
        if (!agent.isStopped)
        {
            agent.isStopped = true;
            SetSpeed(0);
        }
    }

    public virtual void ResumeMovement()
    {
        if (agent.isStopped)
        {
            agent.isStopped = false;
            SetSpeed(normalSpeed);
        }
    }

    public virtual void OnMovementTick(Transform target)
    {
        ResumeMovement();
        //Path recalculation is needed as target could be moving (ie. Player)
        agent.SetDestination(target.position);
    }

    public void FaceTarget(Transform target)
    {
        Vector3 lookPos = target.position - transform.position;
        lookPos.y = 0;
        Quaternion rotation = Quaternion.LookRotation(lookPos);
        if (AIRotationCoroutine != null)
        {
            StopCoroutine(AIRotationCoroutine);
        }
        AIRotationCoroutine = StartCoroutine(SmoothRotation(rotation));
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

    #region Data Structure
    public class AIAttackingTarget
    {
        public Transform targetTransform;
        public enum TargetType
        {
            Building,
            Player
        }
        public TargetType targetType;

        public enum SelectionMethod
        {
            nearAI,
            nearPlayer
        }
        public SelectionMethod selectionMethod;
        public BuildItem buildingCache;
        public Player playerCache;
    }
    public class AITarget
    {
        public AITarget(Transform target, TargetType type, Player playerCache, Waypoint waypointCache)
        {
            targetTransform = target;
            targetType = type;
            playerTargetCache = playerCache;
            waypointTargetCache = waypointCache;
        }
        public enum TargetType
        {
            Waypoint,
            Player
        }

        public TargetType targetType;

        public Transform targetTransform;
        public Waypoint waypointTargetCache;
        public Player playerTargetCache;

        public bool IsWaypointTarget()
        {
            return targetType == TargetType.Waypoint;
        }

        public bool IsPlayerTarget()
        {
            return targetType == TargetType.Player;
        }

        public bool IsValid()
        {
            return targetTransform != null;
        }
    }

    public struct AITargetValidationResult
    {
        public bool isTargetVisible;
        public bool isTargetReachable;
        //if target is blocked by something (checked by ray cast hit)
        public Transform obstacle;

        //Default Constructor
        public AITargetValidationResult(bool _isTargetVisible, bool _isTargetReachable, Transform _obstacle) : this()
        {
            this.isTargetVisible = _isTargetVisible;
            this.isTargetReachable = _isTargetReachable;
            this.obstacle = _obstacle;
        }

        //Returns true if target is in view frustum. Does not check for path finding target reachable.
        public bool HasVisibleTarget()
        {
            return isTargetVisible;
        }

        //Checks path finding target reachable.
        public bool HasVisibleAndReachableTarget()
        {
            return isTargetVisible && isTargetReachable;
        }
    }
    #endregion
}
