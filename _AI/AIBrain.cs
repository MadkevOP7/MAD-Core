//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.AI;

public class AIBrain : NetworkBehaviour
{
    [Header("Runtime")]
    HostMachineManager hostmachineManager;
    private GameObject[] allGhosts;
    public List<GhostAI> spawnedGhosts = new List<GhostAI>(); //spawned ghosts

    #region Core

    #region Callbacks/Signalling
    public void OnHostMachineAttacked(HostMachine hm, Player instigator = null)
    {

    }

    public void OnHostMachineDestroyed(HostMachine hm, Player instigator = null)
    {

    }

    #endregion



    #endregion



    #region Singleton Setup & Initialization
    public static AIBrain Instance { get; private set; }
    private void Awake()
    {
        //Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(Instance);
        }

        Instance = this;
    }
    #endregion

    #region Client Side Registering
    public override void OnStartClient()
    {
        base.OnStartClient();
        allGhosts = GlobalContainer.Instance.globalGhosts;
        foreach (GameObject g in allGhosts)
        {
            NetworkClient.RegisterPrefab(g);
        }
    }
    #endregion
    // Start is called before the first frame update
    public override void OnStartServer()
    {
        base.OnStartServer();
        hostmachineManager = GetComponent<HostMachineManager>();
        StartCoroutine(TimedGhostSpawn());
    }

    IEnumerator TimedGhostSpawn()
    {
        yield return new WaitForSeconds(4f);
        SpawnGhost(0, hostmachineManager.host_Machines[0].transform.position);

    }

    #region Ghost Spawning Functions (Server)
    /// <summary>
    /// [Server Only] Spawns ghost with specified index in pool at given Vector3 position, without NavMesh path sampling to ensure spawn position is valid
    /// </summary>
    /// <param name="index">The index of the ghost to spawn</param>
    /// <param name="position">The position to spawn at</param>
    [Server]
    public void SpawnGhost(int index, Vector3 position)
    {
        Vector3 pos = position + new Vector3(0, 0.1f, 0);
        GameObject g = Instantiate(allGhosts[0], pos, allGhosts[index].transform.rotation);
        NetworkServer.Spawn(g);
        spawnedGhosts.Add(g.GetComponent<GhostAI>());

        //Test FindGhostByIndex
        Debug.Log("AI spawned at index: " + FindGhostByIndex(g.GetComponent<GhostAI>()));
    }

    /// <summary>
    /// [Server Only] Spawns AI given index and near provided position based on NavMesh path sampling, which validates the position to be NavMesh walkable
    /// </summary>
    /// <param name="index"></param>
    /// <param name="position"></param>
    /// <returns>True if successfully found valid point near given position, false otherwise</returns>
    [Server]
    public bool SpawnGhostNearPosition(int index, Vector3 position)
    {
        Transform nearestHMPt = FindNearestHMSpawnPointTransform(position);
        Debug.Log("Picked PT: " + nearestHMPt + " Target: " + position + " Distance: " + Vector3.Distance(position, nearestHMPt.position));
        //Calculate path from position to HMSpawnPoint nearest to position
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(position, nearestHMPt.position, NavMesh.AllAreas, path) && path.status != NavMeshPathStatus.PathInvalid)
        {
            //We use the last corner in the calculated path as spawn location
            //Since it should be the closest Navmesh validated position to target
            if (path.corners == null || path.corners.Length == 0)
            {
                Debug.Log("Path doesn't contain corners, invalid path!");
                return false;
            }
            SpawnGhost(index, path.corners[path.corners.Length - 1]);
            Debug.Log("Successfully spawned ghost near position: " + position + " at: " + path.corners[path.corners.Length - 1]);
            return true;
        }

        Debug.Log("Failed to spawn ghost near position: " + position);
        return false;
    }
    #endregion

    #region Ghost Filtering Functions (Server)
    public enum FilterMode
    {
        FilterHigher,
        FilterHigherInclusive,
        FilterLower,
        FilterLowerInclusive
    }

    public int FindGhostByIndex(GhostAI ai)
    {
        return spawnedGhosts.IndexOf(ai);
    }

    //Returns a list of index of ghosts by certain level
    public List<int> FindGhostsByLevel(int level)
    {
        List<int> l = new List<int>();
        for (int i = 0; i < spawnedGhosts.Count; i++)
        {
            if (spawnedGhosts[i].GetComponent<GhostIdentity>().level == level)
            {
                l.Add(i);
            }
        }
        return l;
    }
    public List<int> FindGhostsByLevel(int level, FilterMode mode)
    {
        List<int> l = new List<int>();
        if (mode == FilterMode.FilterHigher)
        {
            for (int i = 0; i < spawnedGhosts.Count; i++)
            {
                if (spawnedGhosts[i].GetComponent<GhostIdentity>().level > level)
                {
                    l.Add(i);
                }
            }
        }
        else if (mode == FilterMode.FilterHigherInclusive)
        {
            for (int i = 0; i < spawnedGhosts.Count; i++)
            {
                if (spawnedGhosts[i].GetComponent<GhostIdentity>().level >= level)
                {
                    l.Add(i);
                }
            }
        }
        else if (mode == FilterMode.FilterLower)
        {
            for (int i = 0; i < spawnedGhosts.Count; i++)
            {
                if (spawnedGhosts[i].GetComponent<GhostIdentity>().level < level)
                {
                    l.Add(i);
                }
            }
        }
        else if (mode == FilterMode.FilterLowerInclusive)
        {
            for (int i = 0; i < spawnedGhosts.Count; i++)
            {
                if (spawnedGhosts[i].GetComponent<GhostIdentity>().level <= level)
                {
                    l.Add(i);
                }
            }
        }
        return l;
    }

    public int FindGhostByLevel(int level)
    {
        for (int i = 0; i < spawnedGhosts.Count; i++)
        {
            if (spawnedGhosts[i].GetComponent<GhostIdentity>().level == level)
            {
                return i;
            }
        }
        return -1;
    }

    public int GetRandomGhostIndexByLevel(int level, FilterMode mode)
    {
        List<int> k = FindGhostsByLevel(level, mode);
        return k[Random.Range(0, k.Count)];
    }
    public GhostAI GetRandomGhostByLevel(int level, FilterMode mode)
    {
        List<int> k = FindGhostsByLevel(level, mode);
        return spawnedGhosts[k[Random.Range(0, k.Count)]];
    }
    #endregion

    #region Helper Functions (Server)
    Transform FindNearestHMSpawnPointTransform(Vector3 targetPosition)
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;
        foreach (Transform child in hostmachineManager.hmSpawnPoints.transform)
        {
            float dist = Vector3.Distance(child.position, targetPosition);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closest = child;
            }
        }

        return closest;
    }

    /// <summary>
    /// Gets a random point on NavMesh based on range search from current transform position
    /// </summary>
    /// <param name="position"></param>
    /// <param name="max_attempt"></param>
    /// <param name="range"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool SamplePosition(Vector3 position, float max_attempt, float range, out Vector3 result)
    {
        for (int i = 0; i < max_attempt; i++)
        {
            Vector2 randomVector2 = Random.insideUnitCircle * range;
            Vector3 randomPoint = new Vector3(position.x + randomVector2.x, position.y, position.z + randomVector2.y);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 1.0f, 0))
            {
                result = hit.position;
                return true;
            }
        }
        result = Vector3.zero;
        return false;
    }
    #endregion

    #region Command Testing Functions (Internal Command, Can call from client)
    [Command(requiresAuthority = false)]
    public void TInternalCMDSpawnGhostNearPosition(Vector3 position)
    {
        bool status = SpawnGhostNearPosition(0, position);
        if (!status)
        {
            Debug.LogError("Failed");
        }
    }
    #endregion
}
