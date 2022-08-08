//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    private const string FILENAME = "/World_Data.sav";
    private const string DEFAULT_DATA_PATH = "World_Data";
    //Dictionary===========================================
    public List<GameObject> poolObjects = new List<GameObject>();
    private Dictionary<string, GameObject> objectDictionary = new Dictionary<string, GameObject>();
    private Dictionary<string, List<GameObject>> poolDictionary = new Dictionary<string, List<GameObject>>();
    //public Dictionary<string, List<GameObject>> allocatedDictionary;

    //Runtime==============================================
    public List<Transform> players = new List<Transform>();
    List<int> active_chunks = new List<int>();
    private Transform global_chunks;
    private RuntimeChunk[] chunk_regions;
    static int default_pool_size = 64;

    //=====================================================
    private GlobalChunkSave global_chunk_data;
    //=====================================================
    //New Player tracking and chunk loading
    public List<RuntimeChunk> active_center_chunks = new List<RuntimeChunk>(); //Stores the CENTER chunk of currently should active chunks patterns
    //=====================================================
    public void InitializeChunkDictionary(List<GameObject> list)
    {

        objectDictionary = new Dictionary<string, GameObject>(list.Count);

        foreach (GameObject obj in list)
        {
            //Add object dictionary for instantiation (extending pool)
            if (obj.GetComponent<Trees>() == null)
            {
                Debug.LogWarning(obj.name + " doesn't have Tree component setup.");
                obj.AddComponent<Trees>();
            }
            objectDictionary.Add(obj.name, obj);

            //Create a list of each individual pooled element, a list of prefabs
            List<GameObject> p = new List<GameObject>();
            GameObject pool_class = new GameObject(obj.name + " (Pool)");

            for (int i = 0; i < default_pool_size; i++)
            {
                GameObject go = Instantiate(obj) as GameObject;
                p.Add(go);
                go.name = obj.name; //Remove "(Clone)"
                go.transform.SetParent(pool_class.transform, true);
                go.GetComponent<MeshRenderer>().enabled = true;
                go.SetActive(false); //Default disable pool

            }
            pool_class.transform.SetParent(this.transform, true);
            poolDictionary.Add(obj.name, p);
        }
    }
    //=====================================================
    #region Singleton Setup
    public static ObjectPool Instance { get; private set; }
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
        global_chunk_data = Load();

        global_chunks = GameObject.Find("Global Chunks").transform;
        if (global_chunks != null)
        {
            chunk_regions = global_chunks.GetComponentsInChildren<RuntimeChunk>();
        }
        else
        {
            Debug.LogError("Gloabl Chunks missing!");
        }
        //Initialize Pool Dictionary
        InitializeChunkDictionary(poolObjects);
    }
    #endregion

    #region Runtime
  
    public void UpdateCenterChunk(RuntimeChunk chunk, RuntimeChunk old)
    {
        //Gets all the chunks that should be active without duplicate
        if (old != null)
        {
            active_center_chunks.Remove(old);
        }
        active_center_chunks.Add(chunk);
        List<RuntimeChunk> current_actives = new List<RuntimeChunk>(active_center_chunks);
        foreach (RuntimeChunk c in active_center_chunks)
        {
            foreach(RuntimeChunk n in c.neighbors)
            {
                if (!current_actives.Contains(n))
                {
                    current_actives.Add(n);
                }
            }
        }

        //Calculate chunk removals
        if (old == null) return;
        foreach (RuntimeChunk o in old.neighbors)
        {
            if (!current_actives.Contains(o))
            {
                o.OffloadChunk();
            }
        }
        if (!current_actives.Contains(old))
        {
            old.OffloadChunk();
        }

        //Load in active chunks
        foreach(RuntimeChunk c in current_actives)
        {
            c.LoadChunk();
        }
    }

    #region Old Code
    //OLD Update CODE=======================================
    public void ProcessRuntime()
    {
        CheckForDisconnectedPlayers();
        List<int> temp = new List<int>();
        if (players.Count == 0) return;

        if (global_chunks == null)
        {
            Debug.LogError("Object Pool: Global Chunks not found");
            return;
        }
        foreach(RuntimeChunk c in chunk_regions)
        {
            //if any player is active in this chunk, mark itself and neighboring chunks active
            if (ContainsPlayer(c))
            {
                temp.Add(c.chunk_id);
            }
        }
        if(!IsEqual(active_chunks, temp))
        {
            OnPlayerGridChanged(temp);
        }
    }

    public void CheckForDisconnectedPlayers()
    {
        for(int i = players.Count - 1; i >= 0; --i){
            if (players[i] == null)
            {
                players.RemoveAt(i);
            }
        }
    }
  
    public void OnPlayerGridChanged(List<int> new_active)
    {
        active_chunks = new_active;
        RefreshGlobalChunks();
    }

    public void RefreshGlobalChunks()
    {
        List<int> withNeighbors = new List<int>(active_chunks);
        //Refresh Chunk active states, enable new chunks
        foreach (RuntimeChunk r in chunk_regions)
        {
            if (active_chunks.Contains(r.chunk_id))
            {
                withNeighbors.AddRange(r.neighbors);
            }
        }

        //remove duplicate ids
        withNeighbors = withNeighbors.Distinct().ToList();

        foreach (RuntimeChunk r in chunk_regions)
        {
            if (withNeighbors.Contains(r.chunk_id))
            {
                r.LoadChunk();
            }
            else
            {
                r.OffloadChunk();
            }
        }
    }

    public bool IsEqual(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return false;
        foreach(int temp in a)
        {
            if (!b.Contains(temp)) return false;
        }
        return true;
    }
    public bool ContainsPlayer(RuntimeChunk c)
    {
        foreach(Transform t in players)
        {
            if (IsInBound2D(c, t)) return true;
        }
        return false;
    }
    public bool IsInBound2D(RuntimeChunk b, Transform t)
    {
        //Only check x, z
        float x = t.position.x;
        float z = t.position.z;
        if (x >= b.Xmin && x <= b.Xmax && z >= b.Zmin && z <= b.ZMax)
        {
            return true;
        }
        return false;
    }
    #endregion
    #endregion

    #region Pool Usage Functions
    public List<GameObject> AllocatePool(int chunk_id, bool autoExtend)
    {
        List<ObjectData> data = global_chunk_data.save[chunk_id].data;
        //Store allocated reference
        List<GameObject> allocated = new List<GameObject>();

        //Allocate
        foreach (ObjectData d in data)
        {
            List<GameObject> pool = new List<GameObject>();
            if (!poolDictionary.TryGetValue(d.m_name, out pool))
            {
                Debug.LogError("Object key not found in dictionary: " + d.m_name);
                continue;
            }
            if (pool.Count == 0)
            {
                if (!autoExtend)
                {
                    Debug.Log("Pool size reached for " + d.m_name);
                    continue;
                }
                GameObject temp = Instantiate(objectDictionary[d.m_name]) as GameObject;
                temp.name = d.m_name; //Remove "(Clone)"
                pool.Add(temp);
            }

            //Update attributes
            GameObject go = pool[pool.Count - 1];


            go.transform.position = ToVector3(d.position);
            go.transform.eulerAngles = ToVector3(d.rotation);
            //go.transform.localScale = ToVector3(d.scale);
            Trees tree = go.GetComponent<Trees>();
            if (tree == null) Debug.LogError("Tree component not found! " + go.name);
            tree.health = d.health;
            tree.reward_id = d.reward_id;
            allocated.Add(go);
            pool.RemoveAt(pool.Count - 1);
            go.GetComponent<MeshRenderer>().enabled = true;
            go.SetActive(true);
        }
        return allocated;
    }

    public void DeAllocatePool(List<GameObject> allocated)
    {
        foreach (GameObject g in allocated)
        {
            List<GameObject> pool = poolDictionary[g.name];
            pool.Add(g);
            g.SetActive(false);
        }
        allocated = null;
    }
    #endregion

    #region Data Translation
    public Vector3 ToVector3(VectorThree vector)
    {
        float x = vector.x;
        float y = vector.y;
        float z = vector.z;
        return new Vector3(x, y, z);
    }
    #endregion

    #region Read/Write Chunk data

    public static GlobalChunkSave Load()
    {
        if (File.Exists(Application.persistentDataPath + FILENAME))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Open);

            GlobalChunkSave data = bf.Deserialize(stream) as GlobalChunkSave;

            stream.Close();

            return data;
        }
        else
        {
            return CreateDefaultWorldData();
        }
    }

    public static GlobalChunkSave CreateDefaultWorldData()
    {
        BinaryFormatter bf = new BinaryFormatter();
        FileStream stream = new FileStream(Application.streamingAssetsPath + FILENAME, FileMode.Open);

        GlobalChunkSave data = bf.Deserialize(stream) as GlobalChunkSave;

        stream.Close();
        Save(data);
        return data;
    }


    public static void Save(GlobalChunkSave data)
    {
        BinaryFormatter bf = new BinaryFormatter();
        FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Create);

        bf.Serialize(stream, data);
        stream.Close();
        Debug.Log("successfully saved chunk data");
    }
    #endregion

    #region Testing & Debug
    private void Start()
    {
        DebugAllKeys();
    }

    public void DebugAllKeys()
    {
        Debug.Log("Object Keys count: " + poolDictionary.Keys.Count);
        foreach (string key in objectDictionary.Keys)
        {
            Debug.Log("Object Key: " + key);
        }
        Debug.Log("Pool Keys count: " + poolDictionary.Keys.Count);
        foreach (string key in poolDictionary.Keys)
        {
            Debug.Log("Pool Key: " + key);
        }
    }
    #endregion

}
