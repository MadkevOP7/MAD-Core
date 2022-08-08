//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.AI;

public class AIBrain : NetworkBehaviour
{
    [Header("Settings")]
    public Transform tempStartPos;
    public List<GameObject> ghosts = new List<GameObject>();

    [Header("Runtime")]
    public List<GhostAI> _ghosts = new List<GhostAI>(); //spawned ghosts

    #region Singleton Setup & Initialization
    public static AIBrain Instance { get; private set; }
    private void Awake()
    {
        //Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }
    #endregion

    #region Client Side Registering
    public override void OnStartClient()
    {
        base.OnStartClient();
        foreach (GameObject g in ghosts)
        {
            NetworkClient.RegisterPrefab(g);
        }
    }
    #endregion
    // Start is called before the first frame update
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(TimedGhostSpawn());
    }

    IEnumerator TimedGhostSpawn()
    {
        yield return new WaitForSeconds(4f);
        SpawnGhost(0, tempStartPos.position);

    }
    #region Ghost Spawning Functions (Server)
    public void SpawnGhost(int index, Vector3 position)
    {
        Vector3 pos = position + new Vector3(0, 0.1f, 0);
        GameObject g = Instantiate(ghosts[0], pos, ghosts[index].transform.rotation);
        NetworkServer.Spawn(g);
        _ghosts.Add(g.GetComponent<GhostAI>());

        //Test FindGhostByIndex
        Debug.Log("AI Spanwed at index: " + FindGhostByIndex(g.GetComponent<GhostAI>()));
    }

    public enum RangeMode
    {
        Strict, //Only at a given range, if not possible do nothing
        Increase, //If given range not possible, increase till found
    }
    //Spawns ghost with position sampling, returns true if sucessful
    public bool SpawnGhostNearPosition(int index, Vector3 position, float range, RangeMode mode)
    {
        if (mode == RangeMode.Strict)
        {
            //Only attempts at given range
            if (SamplePosition(position, 30, range, out Vector3 pos))
            {
                SpawnGhost(index, pos);
                return true;
            }
            else
            {
                Debug.Log("Server: Failed to spawn ghost near position " + position + " with STRICT mode and range of: " + range);
            }
        }
        else if (mode == RangeMode.Increase)
        {
            int max_attempts = 30;
            for(int i=0; i<max_attempts; i++)
            {
                //Only attempts at given range
                if (SamplePosition(position, 30, range, out Vector3 pos))
                {
                    SpawnGhost(index, pos);
                    return true;
                }
                else
                {
                    range += 0.5f;
                }
            }
            Debug.Log("Server: Failed to spawn ghost near position " + position + " with INCREASE mode and final max_range of: " + range);
        }
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
        return _ghosts.IndexOf(ai);
    }

    //Returns a list of index of ghosts by certain level
    public List<int> FindGhostsByLevel(int level)
    {
        List<int> l = new List<int>();
        for (int i = 0; i < _ghosts.Count; i++)
        {
            if (_ghosts[i].GetComponent<GhostIdentity>().level == level)
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
            for (int i = 0; i < _ghosts.Count; i++)
            {
                if (_ghosts[i].GetComponent<GhostIdentity>().level > level)
                {
                    l.Add(i);
                }
            }
        }
        else if (mode == FilterMode.FilterHigherInclusive)
        {
            for (int i = 0; i < _ghosts.Count; i++)
            {
                if (_ghosts[i].GetComponent<GhostIdentity>().level >= level)
                {
                    l.Add(i);
                }
            }
        }
        else if (mode == FilterMode.FilterLower)
        {
            for (int i = 0; i < _ghosts.Count; i++)
            {
                if (_ghosts[i].GetComponent<GhostIdentity>().level < level)
                {
                    l.Add(i);
                }
            }
        }
        else if (mode == FilterMode.FilterLowerInclusive)
        {
            for (int i = 0; i < _ghosts.Count; i++)
            {
                if (_ghosts[i].GetComponent<GhostIdentity>().level <= level)
                {
                    l.Add(i);
                }
            }
        }
        return l;
    }

    public int FindGhostByLevel(int level)
    {
        for (int i = 0; i < _ghosts.Count; i++)
        {
            if (_ghosts[i].GetComponent<GhostIdentity>().level == level)
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
        return _ghosts[k[Random.Range(0, k.Count)]];
    }
    #endregion

    #region Position Sampling Functions (Server)
    public bool SamplePosition(Vector3 position, float max_attempt, float range, out Vector3 result) //Gets a random point on navmesh based on range search from current transform position
    {
        for (int i = 0; i < max_attempt; i++)
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
    #endregion
}
