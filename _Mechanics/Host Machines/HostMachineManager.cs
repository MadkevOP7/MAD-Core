//Host Machine Spawning © MADKEV Studio
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine.AI;
public class HostMachineManager : NetworkBehaviour
{
    #region Global
    public static float GLOBAL_REGULAR_MACHINE_MAX_HEALTH = 100f;
    public const int NUM_RECHARGE_STATIONS = 3;
    private const int MAX_ATTEMPTS_RECHARGE_STATION_SPAWN = 10;
    private const float VERTICAL_STACKING_CHECK_SENSITIVITY = 3f; // x and z sensitivity to prevent vertical stacking
    private const float MINIMUM_FLATTENED_DISTANCE = 17f; // This distance ensures separating out spawns
    #endregion
    //Data should only live on server
    [Header("Components")]
    public GameObject PF_HMRay;
    public int mSpawnCount = 5; // Currently total of 5 host machines
    public GameObject PF_HostMachine;
    public GameObject PF_RechargeStation;

    // RUNTIME
    // Consider deprecate this cache in the future. It's possible due to race condition that we need this cache as it's immediately available
    // whereas the client cache relies on OnStartClient registration which could be unready within a few frames and cause exception for Server logic
    private List<HostMachine> mSpawnedHostMachinesServer = new List<HostMachine>(); // Server only
    private List<LineRenderer> mHMRayEffectCache = new List<LineRenderer>();
    private List<HostMachine> mSpawnedHostMachinesClientCache = new List<HostMachine>(); // On Server and Clients
    private List<Transform> mCalculatedSpawnLocationsCache = new List<Transform>();
    private List<Transform> mFinalizedSpawnLocationsCache = new List<Transform>();
    private Transform[] mSpawnedRechargeStations = new Transform[NUM_RECHARGE_STATIONS];
    private HMSpawnPoints mHMSpawnPoints;
    private int mNumHMDestroyed = 0; //Lives on client
    private void OnValidate()
    {
        if (mSpawnCount < 5)
        {
            mSpawnCount = 5;
            Debug.LogError("Minimum number of HostMachines per game is 5!");
        }
    }
    private void Awake()
    {
        NetworkClient.RegisterPrefab(PF_HostMachine);
        NetworkClient.RegisterPrefab(PF_RechargeStation);
    }

    /// <summary>
    /// Registers a Host Machine to client cache
    /// </summary>
    /// <param name="machine"></param>
    public void RegisterHostMachine(HostMachine machine)
    {
        mSpawnedHostMachinesClientCache.Add(machine);
    }
    #region Server Initialization
    [Server]
    public void InitiliazeHostMachines()
    {
        mHMSpawnPoints = FindObjectOfType<HMSpawnPoints>();
        mNumHMDestroyed = 0;
        if (mHMSpawnPoints == null)
        {
            Debug.LogError("Error Getting HM Spawn Points Root");
            return;
        }
        mCalculatedSpawnLocationsCache.Clear();
        foreach (Transform child in mHMSpawnPoints.transform)
        {
            if (child != mHMSpawnPoints.transform)
            {
                mCalculatedSpawnLocationsCache.Add(child);
            }
        }
        ServerSpawnMachines();
        ServerSpawnRechargeStations();
    }
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (SceneManager.GetActiveScene().name != "Lobby" && SceneManager.GetActiveScene().name != "Forest")
        {
            Debug.Log("Starting Host Machine Server");
            InitiliazeHostMachines();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        BaseTimeframeManager.Instance.OnRefreshLocalPlayerIsPastState += LocalRefreshTimeframeState;
        BaseTimeframeManager.Instance.OnLocalPlayerLimenBreakoccured += LocalOnLimenBreakOccured;
    }


    //Randomly chooses spawn locations
    [Server]
    public void RandomSpawnLocations()
    {
        Transform tempGO;
        for (int i = 0; i < mCalculatedSpawnLocationsCache.Count; i++)
        {
            int rnd = Random.Range(0, mCalculatedSpawnLocationsCache.Count);
            tempGO = mCalculatedSpawnLocationsCache[rnd];
            mCalculatedSpawnLocationsCache[rnd] = mCalculatedSpawnLocationsCache[i];
            mCalculatedSpawnLocationsCache[i] = tempGO;
        }
    }

    [Server]
    private void ServerSpawnRechargeStations()
    {
        List<Vector3> rechargePositions = new List<Vector3>();

        for (int i = 0; i < NUM_RECHARGE_STATIONS; i++)
        {
            Vector3 position;
            bool validPosition = false;

            // Attempt to find a valid position
            int attempts = 0;
            while (!validPosition && attempts < MAX_ATTEMPTS_RECHARGE_STATION_SPAWN) // Limit attempts to prevent infinite loops
            {
                position = GetRandomSampledPositionBetweenTwoSpawnPoints();

                // Check if this position is far enough from existing positions
                validPosition = true;
                foreach (Vector3 existingPos in rechargePositions)
                {
                    if (Vector3.Distance(position, existingPos) < MINIMUM_FLATTENED_DISTANCE) // Using min_h_distance for separation
                    {
                        validPosition = false;
                        break;
                    }
                }

                attempts++;
            }

            // Fallback: if no valid position found, use the last generated position
            if (!validPosition)
            {
                Debug.LogWarning($"HostMachineManager: Could not find a valid spawn position for Recharge Station {i + 1} after {attempts} attempts. Spawning in the last attempted position.");
            }

            // If a valid position was found, or fallback after attempts, spawn the recharge station
            position = GetRandomSampledPositionBetweenTwoSpawnPoints();
            rechargePositions.Add(position);
            GameObject rechargeStation = Instantiate(PF_RechargeStation, position, Quaternion.identity);
            NetworkServer.Spawn(rechargeStation);
            mSpawnedRechargeStations[i] = rechargeStation.transform; // Store reference for future use
        }

        Debug.Log("Recharge stations spawned successfully, with fallback positions where necessary.");
    }

    [Server]
    public void ServerSpawnMachines()
    {
        RandomSpawnLocations();
        for (int k = 0; k < mCalculatedSpawnLocationsCache.Count; k++)
        {
            if (ValidateLocation(mCalculatedSpawnLocationsCache[k]))
            {
                mFinalizedSpawnLocationsCache.Add(mCalculatedSpawnLocationsCache[k]);
            }
        }
        Debug.Log("Balanced Spawn Possible Locations: " + mFinalizedSpawnLocationsCache.Count);
        if (mFinalizedSpawnLocationsCache.Count < mSpawnCount)
        {
            Debug.Log("Warning: System wasn't able to randomize and balance machines up to Spawn Count");
            Debug.Log("Need Backup Fill " + (mSpawnCount - mFinalizedSpawnLocationsCache.Count) + " spawn points");
            //Backup fill
            while (mFinalizedSpawnLocationsCache.Count < mSpawnCount)
            {
                foreach (Transform t in mCalculatedSpawnLocationsCache)
                {
                    if (!mFinalizedSpawnLocationsCache.Contains(t))
                    {
                        mFinalizedSpawnLocationsCache.Add(t);
                    }
                }
            }
            Debug.Log("Backup completed new count is " + mFinalizedSpawnLocationsCache.Count);
        }
        for (int i = 0; i < mSpawnCount; i++)
        {

            if (i < mFinalizedSpawnLocationsCache.Count)
            {
                HostMachine hm = Instantiate(PF_HostMachine, mFinalizedSpawnLocationsCache[i].position, mFinalizedSpawnLocationsCache[i].rotation).GetComponent<HostMachine>();
                NetworkServer.Spawn(hm.gameObject);
                hm.hm = this;
                mSpawnedHostMachinesServer.Add(hm);

                //Set first HM as master HM
                if (i == 0)
                {
                    hm.isMasterHM = true;
                    hm.machine_Health = mSpawnCount * GLOBAL_REGULAR_MACHINE_MAX_HEALTH;
                    hm.max_health = mSpawnCount * GLOBAL_REGULAR_MACHINE_MAX_HEALTH;
                }
            }
        }

        Debug.Log("Host Machine spawn success");
        mCalculatedSpawnLocationsCache.Clear();
        mFinalizedSpawnLocationsCache.Clear();
    }

    [Server]
    public bool ValidateLocation(Transform t)
    {
        foreach (Transform temp in mFinalizedSpawnLocationsCache)
        {
            //Check vertical stacking
            if (Mathf.Abs(temp.position.z - t.position.z) < VERTICAL_STACKING_CHECK_SENSITIVITY || Mathf.Abs(temp.position.x - t.position.x) < VERTICAL_STACKING_CHECK_SENSITIVITY)
            {
                Debug.Log(t.transform.name + " fails: Vertical Stacking");
                return false;
            }

            //Check for ray passing (same area)
            if (!Physics.Linecast(temp.position, t.position))
            {
                Debug.Log(t.transform.name + " fails: Same area");
                return false;
            }
            float xDiff = temp.position.x - t.position.x;
            float zDiff = temp.position.z - t.position.z;
            if (Mathf.Sqrt((xDiff * xDiff) + (zDiff * zDiff)) < MINIMUM_FLATTENED_DISTANCE)
            {
                Debug.Log(t.transform.name + " fails: too close");
                return false;
            }

        }
        Debug.Log(t.transform.name + " passes checks");
        return true;
    }
    #endregion

    #region Getter
    public HMSpawnPoints GetHMSpawnPoints() { return mHMSpawnPoints; }
    /// <summary>
    /// Returns the number of HostMachines currently destroyed
    /// </summary>
    /// <returns></returns>
    public int GetNumDestroyed()
    {
        return mNumHMDestroyed;
    }

    /// <summary>
    /// Returns the cached HostMachines reference that's available on Server and Clients
    /// </summary>
    /// <returns></returns>
    public List<HostMachine> GetClientHostMachineCache()
    {
        return mSpawnedHostMachinesClientCache;
    }
    /// <summary>
    /// Returns closest HostMachine given world position
    /// </summary>
    /// <param name="worldPos"></param>
    /// <returns></returns>
    public HostMachine GetClosestHostMachine(Vector3 worldPos, out float distance)
    {
        HostMachine closest = null;
        float closestDistance = -1;

        foreach (HostMachine hostMachine in mSpawnedHostMachinesServer)
        {
            float _distance = Vector3.Distance(worldPos, hostMachine.transform.position);
            if (closest == null || _distance < closestDistance)
            {
                closestDistance = _distance;
                closest = hostMachine;
            }
        }
        distance = closestDistance;
        return closest;
    }

    /// <summary>
    /// Returns the distance to the closest host machine
    /// </summary>
    /// <param name="worldPos"></param>
    /// <returns></returns>
    public float GetClosestHostMachineDistance(Vector3 worldPos)
    {
        float dist = 0;
        GetClosestHostMachine(worldPos, out dist);
        return dist;
    }

    /// <summary>
    /// Returns a random spawned Host Machine, optionally specify an index to ignore to prevent selecting the same one
    /// </summary>
    /// <returns></returns>
    public HostMachine GetRandomHostMachine(int ignoreIndex = -1)
    {
        if (ignoreIndex == -1)
            return mSpawnedHostMachinesServer[Random.Range(0, mSpawnedHostMachinesServer.Count)];

        return mSpawnedHostMachinesServer[(ignoreIndex + 1 + Random.Range(0, mSpawnedHostMachinesServer.Count - 1)) % mSpawnedHostMachinesServer.Count];
    }

    /// <summary>
    /// Returns a random spawn point, optionally specify an index to ignore to prevent selecting the same one
    /// </summary>
    /// <param name="ignoreIndex"></param>
    /// <returns></returns>
    public Waypoint GetRandomHMSpawnPoint(int ignoreIndex = -1)
    {
        Waypoint[] spArray = mHMSpawnPoints.GetSpawnPoints();
        if (ignoreIndex == -1)
            return spArray[Random.Range(0, spArray.Length)];

        return spArray[(ignoreIndex + 1 + Random.Range(0, spArray.Length - 1)) % spArray.Length];
    }
    /// <summary>
    /// Gets the host machine with lowest health, if includes dead ones it will randomly return a machine with 0 health
    /// </summary>
    /// <returns></returns>
    public HostMachine GetHostMachineWithLowestHealth(bool includeDestroyed = false)
    {
        List<HostMachine> dead_pool = new List<HostMachine>();
        List<HostMachine> alive_pool = new List<HostMachine>();
        foreach (HostMachine h in mSpawnedHostMachinesServer)
        {
            if (h.isAlive)
            {
                alive_pool.Add(h);
                continue;
            }

            dead_pool.Add(h);
        }


        if (includeDestroyed || alive_pool.Count == 0)
        {

            if (dead_pool.Count > 0)
            {
                return dead_pool[Random.Range(0, dead_pool.Count - 1)];
            }
        }

        HostMachine min = alive_pool[0];
        foreach (HostMachine h in alive_pool)
        {
            if (h.machine_Health < min.machine_Health)
            {
                min = h;
            }
        }

        return min;
    }

    public List<HostMachine> GetAliveHostMachines()
    {
        List<HostMachine> result = new List<HostMachine>();
        foreach (HostMachine h in mSpawnedHostMachinesServer)
        {
            if (h.isAlive)
            {
                result.Add(h);
            }
        }

        return result;
    }
    #endregion

    #region Special Effects

    [ServerCallback]
    public void DamageAllMachines(float amount)
    {
        foreach (HostMachine hm in mSpawnedHostMachinesServer)
        {
            hm.ChangeHealth(amount);
        }
    }

    /// <summary>
    /// Starts the effect for connecting all HostMachines with the lighting line & animation
    /// </summary>
    [ClientRpc(includeOwner = true)]
    public void PlayConnectAllHMRayEffect(HostMachine originHM)
    {
        StopConnectAllHMRayEffect();

        //Find the master HM, as we want that to be the first position of the line renderer
        for (int i = mSpawnedHostMachinesClientCache.Count - 1; i >= 0; --i)
        {
            if (mSpawnedHostMachinesClientCache[i] == originHM)
            {
                //Move it to the beginning of the list
                HostMachine ptr = mSpawnedHostMachinesClientCache[i];
                mSpawnedHostMachinesClientCache.RemoveAt(i);
                mSpawnedHostMachinesClientCache.Insert(0, ptr);
            }
        }

        //Draw positions
        for (int i = 1; i < mSpawnedHostMachinesClientCache.Count; i++)
        {
            //Keep a separate line renderer for each line going from master hm to a hm
            List<Vector3> positions = new List<Vector3>();

            NavMeshPath path = new NavMeshPath();

            NavMesh.CalculatePath(mSpawnedHostMachinesClientCache[0].transform.position, mSpawnedHostMachinesClientCache[i].transform.position, NavMesh.AllAreas, path);

            Vector3 offset = new Vector3(0, 0.36f, 0);
            //Add all of the corners to positions
            for (int j = 1; j < path.corners.Length; j++)
            {
                LineRenderer _hmRayEffect = Instantiate(PF_HMRay).GetComponent<LineRenderer>();
                SignalRayEffect.SignalRay sRay = _hmRayEffect.GetComponent<SignalRayEffect.SignalRay>();
                sRay.StartPosition = path.corners[j - 1] + offset;
                sRay.EndPosition = path.corners[j] + offset;
                sRay.StartUpdateThroughCoroutine(0.068f);
                mHMRayEffectCache.Add(_hmRayEffect);
            }

            //_hmRayEffect.positionCount = positions.Count;
            //_hmRayEffect.SetPositions(positions.ToArray());
        }

        //Note: Unity requires that we must set positionCount property before calling SetPoszitions()

        //originalPositions = new Vector3[_hmRayEffect.positionCount];
        //_hmRayEffect.GetPositions(originalPositions);

        //// Create an array for modified positions
        //modifiedPositions = new Vector3[_hmRayEffect.positionCount];

        //StopConnectAllHMRayEffect();
        //COLightningEffect = StartCoroutine(IEPlayLightingEffect());
    }
    void LocalOnLimenBreakOccured()
    {
        //All machines need to be visible
        LocalRefreshTimeframeState(true);
        if (isServer)
        {
            //mHostMachies only live on server, but we can use it here
            foreach (HostMachine hm in mSpawnedHostMachinesServer)
            {
                hm.mIsDiscoveredAllTiemframe = true;
            }
        }
    }

    /// <summary>
    /// If we are in the past, set all host machine to be visible, if in the future, only enable discovered ones
    /// </summary>
    void LocalRefreshTimeframeState(bool isPast)
    {
        foreach (HostMachine hm in mSpawnedHostMachinesClientCache)
        {
            hm.LocalSetHostmachineTimeframeVisibility(isPast || hm.mIsDiscoveredAllTiemframe);
        }
    }
    #region Potential Optimization for SignalRay (Unfinished)
    private void UpdateLineRenderer(LineRenderer lineRenderer, List<KeyValuePair<Vector3, Vector3>> segments)
    {
        int segmentCount = segments.Count + 1;
        lineRenderer.positionCount = segmentCount;

        if (segmentCount < 1)
        {
            return;
        }

        int index = 0;
        lineRenderer.SetPosition(index++, segments[0].Key);

        for (int i = 0; i < segments.Count; i++)
        {
            lineRenderer.SetPosition(index++, segments[i].Value);
        }

        segments.Clear();
    }
    private List<KeyValuePair<Vector3, Vector3>> GenerateLightningBolt(Vector3 start, Vector3 end, int generation, int totalGenerations, float offsetAmount)
    {
        List<KeyValuePair<Vector3, Vector3>> segments = new List<KeyValuePair<Vector3, Vector3>>();

        segments.Add(new KeyValuePair<Vector3, Vector3>(start, end));


        Vector3 randomVector;
        if (offsetAmount <= 0.0f)
        {
            //0.15f is the chaos factor from SignalRay
            offsetAmount = (end - start).magnitude * 0.15f;
        }

        while (generation-- > 0)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                start = segments[i].Key;
                end = segments[i].Value;

                // determine a new direction for the split
                Vector3 midPoint = (start + end) * 0.5f;

                // adjust the mid point to be the new location
                RandomVector(ref start, ref end, offsetAmount, out randomVector);
                midPoint += randomVector;

                // add two new segments
                segments.Add(new KeyValuePair<Vector3, Vector3>(start, midPoint));
                segments.Add(new KeyValuePair<Vector3, Vector3>(midPoint, end));
            }

            // halve the distance the lightning can deviate for each generation down
            offsetAmount *= 0.5f;
        }

        return segments;
    }

    public void RandomVector(ref Vector3 start, ref Vector3 end, float offsetAmount, out Vector3 result)
    {
        System.Random RandomGenerator = new System.Random();

        Vector3 directionNormalized = (end - start).normalized;
        Vector3 side;
        GetPerpendicularVector(ref directionNormalized, out side);

        // generate random distance
        float distance = (((float)RandomGenerator.NextDouble() + 0.1f) * offsetAmount);

        // get random rotation angle to rotate around the current direction
        float rotationAngle = ((float)RandomGenerator.NextDouble() * 360.0f);

        // rotate around the direction and then offset by the perpendicular vector
        result = Quaternion.AngleAxis(rotationAngle, directionNormalized) * side * distance;
    }

    private void GetPerpendicularVector(ref Vector3 directionNormalized, out Vector3 side)
    {
        if (directionNormalized == Vector3.zero)
        {
            side = Vector3.right;
        }
        else
        {
            // use cross product to find any perpendicular vector around directionNormalized:
            // 0 = x * px + y * py + z * pz
            // => pz = -(x * px + y * py) / z
            // for computational stability use the component farthest from 0 to divide by
            float x = directionNormalized.x;
            float y = directionNormalized.y;
            float z = directionNormalized.z;
            float px, py, pz;
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y), az = Mathf.Abs(z);
            if (ax >= ay && ay >= az)
            {
                // x is the max, so we can pick (py, pz) arbitrarily at (1, 1):
                py = 1.0f;
                pz = 1.0f;
                px = -(y * py + z * pz) / x;
            }
            else if (ay >= az)
            {
                // y is the max, so we can pick (px, pz) arbitrarily at (1, 1):
                px = 1.0f;
                pz = 1.0f;
                py = -(x * px + z * pz) / y;
            }
            else
            {
                // z is the max, so we can pick (px, py) arbitrarily at (1, 1):
                px = 1.0f;
                py = 1.0f;
                pz = -(x * px + y * py) / z;
            }
            side = new Vector3(px, py, pz).normalized;
        }
    }


    /// <summary>
    /// Use to stop playback of HM ray effect
    /// </summary>
    public void StopConnectAllHMRayEffect()
    {
        //Clear HM Ray cache
        foreach (LineRenderer l in mHMRayEffectCache)
        {
            if (l == null) continue;
            Destroy(l.gameObject);
        }
        mHMRayEffectCache.Clear();
    }
    #endregion
    #endregion

    public void OnHostMachineDestroyed(HostMachine destroyedMachine)
    {
        mNumHMDestroyed++;
    }
    /// <summary>
    /// Returns a position that's reachable on NavMesh from two Transforms, Vector3.negativeInfinity if no valid path
    /// For this to work the two Transforms must be an entry from hmSpawnPoints to guarantee it's on NavMesh
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private Vector3 InternalGetRandomSampledPositionBetweenTwoTransforms(Transform a, Transform b)
    {
        NavMeshPath path = new NavMeshPath();
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        if (path.status != NavMeshPathStatus.PathInvalid && path.corners.Length > 0)
            //Return the middle point corner
            return path.corners[path.corners.Length / 2];

        Debug.LogError("HostMachineManager: Error getting sampled position between two Transforms, no valid path! Did you forget to bake/update NavMesh?");
        return Vector3.negativeInfinity;
    }

    #region Utility for Other Class
    /// <summary>
    /// Returns a random position that's NavMesh reachable between two HostMachines, meaning also reachable by player
    /// </summary>
    /// <returns></returns>
    public Vector3 GetRandomSampledPositionBetweenTwoHostMachine()
    {
        HostMachine hm1 = GetRandomHostMachine();
        HostMachine hm2 = GetRandomHostMachine(mSpawnedHostMachinesServer.IndexOf(hm1));
        return InternalGetRandomSampledPositionBetweenTwoTransforms(hm1.transform, hm2.transform);
    }

    /// <summary>
    /// Returns a random position reachable by player selected from two random HM spawn points
    /// </summary>
    public Vector3 GetRandomSampledPositionBetweenTwoSpawnPoints()
    {
        Waypoint pt1 = GetRandomHMSpawnPoint();
        Waypoint pt2 = GetRandomHMSpawnPoint(mHMSpawnPoints.GetSpawnPointIndex(pt1));
        return InternalGetRandomSampledPositionBetweenTwoTransforms(pt1.transform, pt2.transform);
    }
    #endregion
}
