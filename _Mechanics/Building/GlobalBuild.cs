//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using GPUInstancer;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System;

public class GlobalBuild : NetworkBehaviour
{
    //Settings
    //Saving
    private const string FILENAME = "/World_Data.sav";
    //static int instancingQueueMax = 100;
    public GPUInstancerPrefabManager instancer;
    //Storage=============================
    public List<BuildItem> global_items;
    public Dictionary<string, Sprite> previews = new Dictionary<string, Sprite>();
    public Dictionary<string, BuildItem> dictionary = new Dictionary<string, BuildItem>(); //For optimization, use name to access a BuildItem
    public List<uint> built; //Only hosting player stores built to write to disk save
    //====================================
    //Instancing==========================
    public Dictionary<string, GPUInstancerPrefabPrototype> instancing_dictionary = new Dictionary<string, GPUInstancerPrefabPrototype>();
    public Dictionary<string, IBuffer> instancing_buffer = new Dictionary<string, IBuffer>();
    private static int INSTANCING_BUFFER_SIZE = 100;
    //====================================
    #region Singleton Setup & Initialization
    public static GlobalBuild Instance { get; private set; }
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
        instancer = GameObject.Find("GPUI Prefab Manager").GetComponent<GPUInstancerPrefabManager>();
        RegisterAllItems();
    }

    void RegisterAllItems()
    {
        foreach (BuildItem b in global_items)
        {
            NetworkClient.RegisterPrefab(b.gameObject);
            previews.Add(b.m_name, b.image);
            dictionary.Add(b.m_name, b);
        }
    }
    #endregion

    #region Core
    //Retrieving objects===================================
    public int FindIndex(string name)
    {
        for (int i = 0; i < global_items.Count; i++)
        {
            if (global_items[i].m_name == name)
            {
                return i;
            }
        }
        return -1; //-1 is not found
    }
    public BuildItem GetObjectAtPosition(Vector3 position)
    {
        Collider[] col;
        col = Physics.OverlapSphere(position, 1f, 18); //18 is BuildItem layer mask
        if (col.Length == 0) return null;
        return col[0]?.GetComponent<BuildItem>();
    }

    //Building Objects=====================================
    //Overload for save/load
    public void Build(string name, Vector3 position, Vector3 rotation, bool isPickUpMode, int health)
    {
        CMDBuild(name, position, rotation, isPickUpMode, health);

    }
    [Command(requiresAuthority = false)]
    void CMDBuild(string name, Vector3 position, Vector3 rotation, bool isPickUpMode, int health)
    {
        BuildItem item;
        if (dictionary.TryGetValue(name, out item))
        {
            GameObject obj = Instantiate(item.gameObject, position, Quaternion.Euler(rotation));
            BuildItem i = obj.GetComponent<BuildItem>();

            NetworkServer.Spawn(obj);
            i.isPickupMode = isPickUpMode;
            i.health = health;
            if (isPickUpMode)
            {
                i.SetDestroyedFromSave();
            }
            uint id = obj.GetComponent<NetworkIdentity>().netId;
            built.Add(id);
            //RPCProcessInstance(item.m_name, id);
        }
        else
        {
            Debug.LogError("Global Build: Item with name " + name + " is not found in dictionary!");
        }
    }

    public void Build(GlobalItem item, Vector3 position, Vector3 rotation)
    {
        if (item.count > 0)
        {
            CMDBuild(item.m_name, position, rotation);
            item.count--;
        }
    }

    [Command(requiresAuthority = false)]
    void CMDBuild(string name, Vector3 position, Vector3 rotation)
    {
        BuildItem item;
        if (dictionary.TryGetValue(name, out item))
        {
            GameObject obj = Instantiate(item.gameObject, position, Quaternion.Euler(rotation));
            NetworkServer.Spawn(obj);
            uint id = obj.GetComponent<NetworkIdentity>().netId;
            built.Add(id);
            //RPCProcessInstance(item.m_name, id);
        }
        else
        {
            Debug.LogError("Global Build: Item with name " + name + " is not found in dictionary!");
        }
    }

    //Returns -1 if not buffered, else returns the buffer index
    public int ProcessInstance(string name, GameObject obj)
    {
        GPUInstancerPrefabPrototype prototype;
        if (!instancing_dictionary.TryGetValue(name, out prototype))
        {
            prototype = GPUInstancerAPI.DefineGameObjectAsPrefabPrototypeAtRuntime(instancer, obj);
            prototype.isFrustumCulling = true;
            prototype.isOcclusionCulling = true;
            prototype.useOriginalShaderForShadow = false;
            prototype.useCustomShadowDistance = true;
            prototype.cullShadows = true;
            prototype.shadowDistance = 18;
            prototype.frustumOffset = 0.5f;
            prototype.enableRuntimeModifications = true;
            prototype.autoUpdateTransformData = true;
            prototype.addRemoveInstancesAtRuntime = true;
            prototype.addRuntimeHandlerScript = false;
            instancing_dictionary.Add(name, prototype);
            IBuffer _buffer = new IBuffer();
            instancing_buffer.Add(name, _buffer);
        }
        IBuffer buffer = instancing_buffer[name];
        if (buffer.pointer + 1 < INSTANCING_BUFFER_SIZE)
        {
            buffer.queue[buffer.pointer] = obj;
            buffer.pointer++;
            return buffer.pointer - 1;
        }
        else
        {
            GPUInstancerAPI.AddInstancesToPrefabPrototypeAtRuntime(instancer, prototype, ProcessInstanceBuffer(buffer));
            return -1;
        }

    }

    List<GameObject> ProcessInstanceBuffer(IBuffer buffer)
    {
        List<GameObject> b = new List<GameObject>();
        for (int i = buffer.queue.Length - 1; i >= 0; --i)
        {
            if (buffer.queue[i] == null) continue;
            b.Add(buffer.queue[i]);
            buffer.queue[i].GetComponent<BuildItem>().isBufferedInstance = false;
            buffer.queue[i] = null;
        }
        buffer.pointer = 0;
        return b;
    }
    public void RemoveInstance(GPUInstancerPrefab instance)
    {
        GPUInstancerAPI.RemovePrefabInstance(instancer, instance, false);
    }

    //=====================================================
    //Removing Objects=====================================
    [Command(requiresAuthority = false)]
    public void Remove(Vector3 position)
    {
        BuildItem item = GetObjectAtPosition(position);
        if (item == null)
        {
            Debug.LogError("GlobalBuild: Unable to get object at position: " + position);
            return;
        }
        built.Remove(item.netId);
        NetworkServer.Destroy(item.gameObject);
    }

    [Command(requiresAuthority = false)]
    public void Remove(uint net_id)
    {
        if (NetworkClient.spawned[net_id] == null)
        {
            Debug.LogError("GlobalBuild: Unable to get object with net_id: " + net_id);
        }

        built.Remove(net_id);
        NetworkServer.Destroy(NetworkClient.spawned[net_id].gameObject);
    }
    //=====================================================

    #endregion

    #region Save/Load
    public void LoadWorld()
    {
        WorldData data = Load();
        foreach (BuildData cache in data.build_data)
        {
            //Load data
            Build(cache.m_name, ToVector3(cache.position), ToVector3(cache.rotation), cache.isPickupMode, cache.health);
        }
    }
    public void SaveWorld()
    {
        List<BuildData> cache = new List<BuildData>();
        //UI Update
        for (int i = 0; i < built.Count; i++)
        {
            BuildItem item = NetworkClient.spawned[built[i]].gameObject.GetComponent<BuildItem>();
            BuildData d = new BuildData();
            Transform t = item.transform;
            d.m_name = item.m_name;
            d.position = ToVectorThree(t.position);
            d.rotation = ToVectorThree(t.eulerAngles);
            d.health = item.health;
            d.isPickupMode = item.isPickupMode;
            cache.Add(d);
        }
        WorldData data = Load();
        data.build_data = cache;
        Save(data);
    }
    //Local Save Load to disk
    public static void Save(WorldData data)
    {

        BinaryFormatter bf = new BinaryFormatter();
        FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Create);

        bf.Serialize(stream, data);
        stream.Close();
    }

    public static WorldData Load()
    {
        if (File.Exists(Application.persistentDataPath + FILENAME))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Open);

            WorldData data = bf.Deserialize(stream) as WorldData;

            stream.Close();

            return data;
        }
        else
        {
            Debug.LogError("World data not found or corrupted, created new data save.");
            WorldData d = new WorldData();
            Save(d);
            return Load();
        }
    }


    #endregion

    #region Helper Functions

    public void SetInstancingBufferSize(int size)
    {
        INSTANCING_BUFFER_SIZE = size;
    }
    public VectorThree ToVectorThree(Vector3 v)
    {
        float x = v.x;
        float y = v.y;
        float z = v.z;
        VectorThree vector = new VectorThree();
        vector.x = x;
        vector.y = y;
        vector.z = z;
        return vector;
    }

    public Vector3 ToVector3(VectorThree v)
    {
        Vector3 vector = new Vector3();
        vector.x = v.x;
        vector.y = v.y;
        vector.z = v.z;
        return vector;
    }
    #endregion

    #region Buffer
    public class IBuffer
    {
        public int pointer = 0;
        public GameObject[] queue = new GameObject[INSTANCING_BUFFER_SIZE];
    }
    #endregion
}
