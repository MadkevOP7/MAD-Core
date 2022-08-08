//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System;
using UnityEngine;
using Mirror;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public class EquipmentStorage : NetworkBehaviour
{
    private const string FILENAME = "/Equipment_Data.sav";
    private const string P_FILENAME = "/Player_Equipment_Data.sav";
    uint playerID;
    bool isLocalPlayerBool; //For track the instance this is if it's localPlayer, for tablet setting and other client/localplayer diff set
    static string TABLET_NAME = "Observer Tablet";
    [Header("Equipments Storage")]
    //[HideInInspector]
    public Equipment observe_tablet;
    public List<Equipment> all_equipments = new List<Equipment>();
    public List<Equipment> inventory_equipments = new List<Equipment>(); //In Game Equipments, local player
    public List<Equipment> player_equipments = new List<Equipment>(); //In game reference to spawned equipment, local

    public List<Equipment> all_biome_equipments = new List<Equipment>(); //Store all equipments existing in biome, on server, for data serailzation
    //Each player will initialize their own equipments
    public List<Equipment> InitializePlayerEquipments(NetworkConnection conn, uint id, bool isLocalPlayer)
    {
        playerID = id;
        isLocalPlayerBool = isLocalPlayer;
        List<string> list = LoadPlayerEquipmentData().inventory_equipment;
        //Loads player inventory save and allocates equipment to Inventory_equipments list
        foreach (Equipment e in all_equipments)
        {
            //Don't add tablet to player inventory list
            if (e.m_name == TABLET_NAME)
            {
                continue;
            }


            //[REMOVE] Temp testing
            inventory_equipments.Add(e);

            //[UnComment] For build
            //if (list.Contains(e.m_name))
            //{
            //    inventory_equipments.Add(e);
            //}
        }

        //Spawn equipment
        foreach (Equipment e in inventory_equipments)
        {
            CMDSpawn(FindID(e), id);
        }
        CMDSpawnTablet(id);
        return player_equipments;
    }
    public static EquipmentStorage Instance { get; private set; }

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
        RegisterEquipments();
    }
    public void RegisterEquipments()
    {
        foreach (Equipment g in all_equipments)
        {
            NetworkClient.RegisterPrefab(g.gameObject);
        }
    }
    #region World Equipment Functions (For biome, not separete game levels)
    //runs on server
    [Command(requiresAuthority = false)]
    void SpawnWorldEquipment(int id, Vector3 position, Vector3 rotation, int type)
    {
        GameObject temp = Instantiate(all_equipments[id].gameObject, position, Quaternion.Euler(rotation));

        //temp.SetActive(false);
        NetworkServer.Spawn(temp);
        Equipment equipment = temp.GetComponent<Equipment>();
        equipment.id = id;
        equipment.isCreatedFromSave = true;

        if (type == 0)
        {
            //Placed
            equipment.ServerPlaceItem(position, Quaternion.Euler(rotation));
        }
        else
        {
            equipment.ServerSetTransformData(position, rotation);
            equipment.ServerSetCanPickUp(true);
        }
        equipment.needRefresh = true;
        if (SceneManager.GetActiveScene().name == "Forest")
        {
            all_biome_equipments.Add(temp.GetComponent<Equipment>());
        }

    }
    #endregion
    #region Instance Game Scene Functions
    [Command(requiresAuthority = false)]
    public void CMDSpawn(int id, uint conn)
    {
        Player player = NetworkClient.spawned[conn].GetComponent<Player>();
        GameObject temp = Instantiate(all_equipments[id].gameObject);

        //temp.SetActive(false);
        NetworkServer.Spawn(temp);
        temp.GetComponent<Equipment>().player = player;
        temp.GetComponent<Equipment>().id = id;
        temp.GetComponent<Equipment>().p_follow = player.netId;
        //player_equipments.Add(temp.GetComponent<Equipment>());
        RPCAddEquipment(player.connectionToClient, temp);
        if (SceneManager.GetActiveScene().name == "Forest")
        {
            all_biome_equipments.Add(temp.GetComponent<Equipment>());
        }

    }

    [Command(requiresAuthority = false)]
    public void CMDSpawnTablet(uint conn)
    {
        Player player = NetworkClient.spawned[conn].GetComponent<Player>();
        int id = FindID(TABLET_NAME); //DO NOT CHANGE THIS EQUIPMENT NAME
        GameObject temp = Instantiate(all_equipments[id].gameObject);

        //temp.SetActive(false);
        NetworkServer.Spawn(temp);
        temp.GetComponent<Equipment>().player = player;

        temp.GetComponent<Equipment>().p_follow = player.netId;
        //player_equipments.Add(temp.GetComponent<Equipment>());
        RPCAddTablet(player.connectionToClient, temp);
    }
    public int FindID(Equipment e)
    {
        string n = e.m_name;
        for (int i = 0; i < all_equipments.Count; i++)
        {
            if (all_equipments[i].m_name == n)
            {
                return i;
            }
        }
        return -1;
    }

    public int FindID(string n)
    {

        for (int i = 0; i < all_equipments.Count; i++)
        {
            if (all_equipments[i].m_name == n)
            {
                return i;
            }
        }
        return -1;
    }
    [TargetRpc]
    public void RPCAddEquipment(NetworkConnection conn, GameObject g)
    {

        player_equipments.Add(g.GetComponent<Equipment>());
    }

    [TargetRpc]
    public void RPCAddTablet(NetworkConnection conn, GameObject g)
    {
        observe_tablet = g.GetComponent<Equipment>();
        observe_tablet.GetComponent<ObserveOS>().InitializeOS();

    }
    #endregion

    #region Save/Load
    //Saves equipment player currently carrying (per player)
    public void SavePlayerCarryEquipment()
    {
        List<Equipment> playerCarryingEquipment = NetworkClient.spawned[playerID].GetComponent<EquipmentManager>().equipments;
        List<string> temp = new List<string>();
        foreach (Equipment e in playerCarryingEquipment)
        {
            temp.Add(e.m_name);
        }
        PlayerEquipmentData save = LoadPlayerEquipmentData();
        save.inventory_equipment = temp;
        SavePlayerEquipmentData(save);
    }
    //Loads in all placed equipment
    public void LoadAllEquipment()
    {
        EquipmentSave data = Load();
        foreach (EquipmentData d in data.data)
        {
            SpawnWorldEquipment(d.id, ToVector3(d.position), ToVector3(d.rotation), d.type);
        }
    }
    public void SaveAllEquipment() //(only host) saves global world
    {
        EquipmentSave data = new EquipmentSave();
        List<EquipmentData> cache = new List<EquipmentData>();

        foreach (Equipment e in all_biome_equipments)
        {
            if (e.IsPickupable())
            {
                EquipmentData d = new EquipmentData();
                d.position = ToVectorThree(e.transform.position);
                d.rotation = ToVectorThree(e.transform.eulerAngles);
                d.id = e.id;
                if (e.placed)
                {
                    d.type = 0;
                }
                else
                {
                    d.type = 1;
                }
                cache.Add(d);
            }
        }
        data.data = cache;

        Save(data);
    }
    public static void Save(EquipmentSave data)
    {
        //No adusting minimum currently

        BinaryFormatter bf = new BinaryFormatter();
        FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Create);

        bf.Serialize(stream, data);
        stream.Close();
    }

    public static EquipmentSave Load()
    {
        if (File.Exists(Application.persistentDataPath + FILENAME))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Open);

            EquipmentSave data = bf.Deserialize(stream) as EquipmentSave;

            stream.Close();

            return data;
        }
        else
        {
            Debug.LogError("Equipment data not found or corrupted, created new data save.");
            EquipmentSave d = new EquipmentSave();
            Save(d);
            return Load();
        }


    }
    #endregion

    #region Helper Functions

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

    #region World Equipment Save & data
    [Serializable]
    public class EquipmentData
    {
        [SerializeReference]
        public VectorThree position;

        [SerializeReference]
        public VectorThree rotation;

        [SerializeField]
        public int id;

        [SerializeField]
        public int type; //0 = placed, 1 = dropped
    }


    [Serializable]
    public class EquipmentSave
    {
        [SerializeReference]
        public List<EquipmentData> data = new List<EquipmentData>();
    }
    #endregion

    #region Per player equipment save & data

    [Serializable]
    public class PlayerEquipmentData
    {
        [SerializeField]
        public List<string> inventory_equipment = new List<string>();
    }

    public static void SavePlayerEquipmentData(PlayerEquipmentData data)
    {
        BinaryFormatter bf = new BinaryFormatter();
        FileStream stream = new FileStream(Application.persistentDataPath + P_FILENAME, FileMode.Create);

        bf.Serialize(stream, data);
        stream.Close();
    }

    public static PlayerEquipmentData LoadPlayerEquipmentData()
    {
        if (File.Exists(Application.persistentDataPath + P_FILENAME))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream stream = new FileStream(Application.persistentDataPath + P_FILENAME, FileMode.Open);

            PlayerEquipmentData data = bf.Deserialize(stream) as PlayerEquipmentData;

            stream.Close();

            return data;
        }
        else
        {
            Debug.LogError("Player Equipment data not found or corrupted, created new data save.");
            PlayerEquipmentData d = new PlayerEquipmentData();
            SavePlayerEquipmentData(d);
            return LoadPlayerEquipmentData();
        }


    }
    #endregion
}
