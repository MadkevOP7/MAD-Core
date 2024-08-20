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
    static string TABLET_NAME = "Observer Tablet";
    [Header("Equipments Storage")]
    //[HideInInspector]

    //Store all equipments existing in biome, on server, for data serializations Note: May not work after 10/30/2023 change to syncvar-> Hook -> callback
    //to prevent race condition where rpc receives null GameObject that has just been server spawned.
    public List<Equipment> all_biome_equipments = new List<Equipment>();
    //Each player will initialize their own equipments
    public void InitializePlayerEquipments(NetworkConnection conn, uint id, bool isLocalPlayer, bool isInLobby)
    {

        //Registering moved to OnStartClient() override in BaseNetworkManager
        if (isInLobby)
        {
            //Only enable tablet in lobby
            CMDSpawnTablet(id);
            return;
        }

        playerID = id;
        //Loads player inventory save and allocates equipment to Inventory_equipments list
        List<string> savedLoadout = LoadPlayerEquipmentInventoryData().loadout_equipment;

        //Build equipment cache, then verify if cache count for each equipment > 0 to spawn, else ignore
        Dictionary<string, int> inventoryCache = new Dictionary<string, int>();
        //Build dictionary to compute inventory equipment count
        foreach (var itemString in EquipmentStorage.GetPlayerInventoryEquipment())
        {
            if (inventoryCache.ContainsKey(itemString))
            {
                //Add 1 to it
                inventoryCache[itemString] = inventoryCache[itemString] + 1;
                continue;
            }

            //Not in dictionary, add it
            inventoryCache.Add(itemString, 1);
        }


        //Spawn equipment in loadout from save
        foreach (string n in savedLoadout)
        {
            //Verify that inventory contains this equipment with count > 0
            if (!inventoryCache.ContainsKey(n) || inventoryCache[n] == 0) continue;
            CMDSpawn(FindID(n), id);
        }

        CMDSpawnTablet(id);
    }
    public static EquipmentStorage Instance { get; private set; }

    private void Awake()
    {
        //Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(Instance);
        }

        Instance = this;

    }

    #region World Equipment Functions (For biome, not separete game levels)
    /// <summary>
    /// Spawns a given equipment (by id) into the world
    /// </summary>
    /// <param name="id"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <param name="type"></param>
    [Command(requiresAuthority = false)]
    void SpawnWorldEquipment(int id, Vector3 position, Vector3 rotation, int type)
    {
        GameObject temp = Instantiate(GlobalContainer.GetInstance().globalEquipments[id].gameObject, position, Quaternion.Euler(rotation));

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
        equipment.OnRefreshClient();
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
        //Validate ID
        if (id < 0 || id >= GlobalContainer.GetInstance().globalEquipments.Length)
        {
            Debug.LogError("EquipmentStorage: Invalid CMDSpawn equipment id: " + id);
            return;
        }

        Player player = NetworkClient.spawned[conn].GetComponent<Player>();
        GameObject temp = Instantiate(GlobalContainer.GetInstance().globalEquipments[id].gameObject);
        temp.GetComponent<Equipment>().originPlayer = player.gameObject;

        NetworkServer.Spawn(temp);
        Equipment equipment = temp.GetComponent<Equipment>();
        equipment.id = id;

        //equipment.player = player;
        //equipment.p_follow = player.netId;
        //player_equipments.Add(temp.GetComponent<Equipment>());
        //RPCAddEquipment(player.connectionToClient, temp); 10/30/2023 - Moved to Equipment Syncvar -> Hook -> Callback to address race condition
        if (SceneManager.GetActiveScene().name == "Forest")
        {
            all_biome_equipments.Add(temp.GetComponent<Equipment>());
        }

    }

    [Command(requiresAuthority = false)]
    public void CMDSpawnTablet(uint conn)
    {
        StartCoroutine(DelayedSpawnTablet(conn));
    }

    IEnumerator DelayedSpawnTablet(uint conn) //Fix index -1
    {
        yield return new WaitForEndOfFrame();
        Player player = NetworkClient.spawned[conn].GetComponent<Player>();
        int id = FindID(TABLET_NAME); //DO NOT CHANGE THIS EQUIPMENT NAME
        GameObject temp = Instantiate(GlobalContainer.GetInstance().globalEquipments[id].gameObject);
        temp.GetComponent<Equipment>().originPlayer = player.gameObject;
        NetworkServer.Spawn(temp);
        Equipment equipment = temp.GetComponent<Equipment>();
        equipment.id = id;

        //temp.SetActive(false);
        //Equipment equipment = temp.GetComponent<Equipment>();
        //equipment.player = player;
        //equipment.p_follow = player.netId;
        //player_equipments.Add(temp.GetComponent<Equipment>());
        //RPCAddTablet(player.connectionToClient, temp); //Moved to hook -> callback to EquipmentManager
    }
    public int FindID(Equipment e)
    {
        string n = e.m_name;
        for (int i = 0; i < GlobalContainer.GetInstance().globalEquipments.Length; i++)
        {
            if (GlobalContainer.GetInstance().globalEquipments[i].m_name == n)
            {
                return i;
            }
        }

        Debug.LogError("Equipment: " + e.m_name + " failed to find ID");
        return -1;
    }

    public int FindID(string n)
    {

        for (int i = 0; i < GlobalContainer.GetInstance().globalEquipments.Length; i++)
        {
            if (GlobalContainer.GetInstance().globalEquipments[i].m_name == n)
            {
                return i;
            }
        }
        return -1;
    }



    #endregion

    #region Save/Load
    //Saves equipment player currently carrying (per player)
    /// <summary>
    /// [Warning] We have moved to loadout for level games, forest may need to adjust to it
    /// </summary>
    public void SavePlayerCarryEquipment()
    {
        List<Equipment> playerCarryingEquipment = NetworkClient.spawned[playerID].GetComponent<EquipmentManager>().GetEquipmentList();
        List<string> temp = new List<string>();
        foreach (Equipment e in playerCarryingEquipment)
        {
            temp.Add(e.m_name);
        }
        PlayerEquipmentData save = LoadPlayerEquipmentInventoryData();
        save.loadout_equipment = temp;
        SavePlayerEquipmentInventoryData(save);
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
                if (e.GetIsPlaced())
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
            try
            {
                EquipmentSave data = bf.Deserialize(stream) as EquipmentSave;
                stream.Close();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Error loading world forest equipment data: Binary corruption!\n" + e.Message + "\n" + e.StackTrace);
                FileStream saveBackup = new FileStream(Application.persistentDataPath + "/World_Equipment_ Corrupted Backup " + GetTimestamp() + ".sav", FileMode.Create);
                stream.CopyTo(saveBackup);
                stream.Close();
                saveBackup.Close();
                Debug.LogError("Creating new world equipment save override, backup saved to disk");
                EquipmentSave d = new EquipmentSave();
                Save(d);
                //Probably only have it here for serialization exception, the not found could be triggered during fresh save so we might not wanna kick to error there.
                ErrorAutoDisplay.CreateError("Save corruption: failed to load world equipment data, please do not modify the files as it may corrupt your data. A backup was created for the corrupted file and a new save has been created.");
                return Load();
            }

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
    public class EquipmentSave //For forest owner saving forest equipment data (host only) ie. dropped equipments, placed equipments
    {
        [SerializeReference]
        public List<EquipmentData> data = new List<EquipmentData>();
    }
    #endregion

    #region Per player equipment save & data

    [Serializable]
    public class PlayerEquipmentData //Per player inventory owning equipment
    {
        /// <summary>
        /// This is the player's inventory
        /// </summary>
        [SerializeField]
        public List<string> inventory_equipment = new List<string> { "Phone" };

        //The default load-out contains 1 phone
        [SerializeField]
        public List<string> loadout_equipment = new List<string> { "Phone" };

        [SerializeField]
        public List<string> purchased_characters = new List<string> { LIMENDefine.CHARACTER_STARTER };

        [SerializeField]
        public string current_character = LIMENDefine.CHARACTER_STARTER;

    }

    protected static void SavePlayerEquipmentInventoryData(PlayerEquipmentData data)
    {
        BinaryFormatter bf = new BinaryFormatter();
        FileStream stream = new FileStream(Application.persistentDataPath + P_FILENAME, FileMode.Create);

        bf.Serialize(stream, data);
        stream.Close();
    }

    protected static PlayerEquipmentData LoadPlayerEquipmentInventoryData()
    {
        if (File.Exists(Application.persistentDataPath + P_FILENAME))
        {
            FileStream stream = new FileStream(Application.persistentDataPath + P_FILENAME, FileMode.Open);
            try
            {
                BinaryFormatter bf = new BinaryFormatter();
                PlayerEquipmentData data = bf.Deserialize(stream) as PlayerEquipmentData;

                stream.Close();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Error loading player equipment data: Binary corruption!\n" + e.Message + "\n" + e.StackTrace);
                FileStream saveBackup = new FileStream(Application.persistentDataPath + "/Player_Equipment_ Corrupted Backup " + GetTimestamp() + ".sav", FileMode.Create);
                stream.CopyTo(saveBackup);
                stream.Close();
                saveBackup.Close();
                Debug.LogError("Creating new player equipment save override, backup saved to disk");
                PlayerEquipmentData d = new PlayerEquipmentData();
                SavePlayerEquipmentInventoryData(d);
                //Probably only have it here for serialization exception, the not found could be triggered during fresh save so we might not wanna kick to error there.
                ErrorAutoDisplay.CreateError("Save corruption: failed to load player equipment data, please do not modify the files as it may corrupt your data. A backup was created for the corrupted file and a new save has been created.");
                return LoadPlayerEquipmentInventoryData();
            }
        }
        else
        {
            Debug.LogError("Player Equipment data not found or corrupted, created new data save.");
            PlayerEquipmentData d = new PlayerEquipmentData();
            SavePlayerEquipmentInventoryData(d);
            return LoadPlayerEquipmentInventoryData();
        }
    }
    #endregion

    #region Public Static Calls: 10/19/2023 - Needs to call static methods here to modify player equipment inventory
    /// <summary>
    /// Adds a equipment to player inventory data
    /// </summary>
    /// <param name="equipmentName"></param>
    public static void AddEquipmentToPlayerInventory(string equipmentName)
    {
        PlayerEquipmentData eqData = LoadPlayerEquipmentInventoryData();
        eqData.inventory_equipment.Add(equipmentName);
        SavePlayerEquipmentInventoryData(eqData);
    }

    /// <summary>
    /// Adds an array of equipment to player inventory data
    /// </summary>
    /// <param name="equipmentNames"></param>
    public static void AddEquipmentToPlayerInventory(string[] equipmentNames)
    {
        PlayerEquipmentData eqData = LoadPlayerEquipmentInventoryData();
        eqData.inventory_equipment.AddRange(equipmentNames);
        SavePlayerEquipmentInventoryData(eqData);
    }

    /// <summary>
    /// Adds a list of equipment to player inventory data
    /// </summary>
    /// <param name="equipmentNames"></param>
    public static void AddEquipmentToPlayerInventory(List<string> equipmentNames)
    {
        PlayerEquipmentData eqData = LoadPlayerEquipmentInventoryData();
        eqData.inventory_equipment.AddRange(equipmentNames);
        SavePlayerEquipmentInventoryData(eqData);
    }

    /// <summary>
    /// After successful purchase, add the character to inventory list of owned characters
    /// </summary>
    /// <param name="characterName"></param>
    public static void AddCharacterToPlayerInventory(string characterName)
    {
        PlayerEquipmentData eqData = LoadPlayerEquipmentInventoryData();
        if (!eqData.purchased_characters.Contains(characterName))
        {
            eqData.purchased_characters.Add(characterName);
            SavePlayerEquipmentInventoryData(eqData);
        }
        else
        {
            Debug.LogError("EquipmentStorage: " + characterName + " is already in player inventory, duplicate purchase!");
        }
    }

    /// <summary>
    /// Sets the current saved character, player will load in with this character at start of game
    /// </summary>
    /// <param name="characterName"></param>
    public static void SetCurrentSelectedPlayerCharacter(string characterName)
    {
        PlayerEquipmentData eqData = LoadPlayerEquipmentInventoryData();
        eqData.current_character = characterName;
        SavePlayerEquipmentInventoryData(eqData);
    }

    /// <summary>
    /// Returns the current saved player character
    /// </summary>
    /// <returns></returns>
    public static string GetCurrentSelectedPlayerCharacter()
    {
        return LoadPlayerEquipmentInventoryData().current_character;
    }
    /// <summary>
    /// Returns the list of player purchased characters
    /// </summary>
    /// <returns></returns>
    public static List<string> GetPlayerPurchasedCharacters()
    {
        return LoadPlayerEquipmentInventoryData().purchased_characters;
    }
    /// <summary>
    /// Removes a equipment from player inventory data
    /// </summary>
    /// <param name="equipmentName"></param>
    public static void RemoveEquipmentFromPlayerInventory(string equipmentName)
    {
        PlayerEquipmentData eqData = LoadPlayerEquipmentInventoryData();
        eqData.inventory_equipment.Remove(equipmentName);
        SavePlayerEquipmentInventoryData(eqData);
    }

    /// <summary>
    /// Removes a list of equipment from player inventory data
    /// </summary>
    /// <param name="equipmentNames"></param>
    public static void RemoveEquipmentFromPlayerInventory(List<string> equipmentNames)
    {
        PlayerEquipmentData eqData = LoadPlayerEquipmentInventoryData();

        foreach (var e in equipmentNames)
        {
            eqData.inventory_equipment.Remove(e);
        }
        SavePlayerEquipmentInventoryData(eqData);
    }

    /// <summary>
    /// Returns a List<string> representing saved player inventory equipment list
    /// </summary>
    /// <returns></returns>
    public static List<string> GetPlayerInventoryEquipment()
    {
        return LoadPlayerEquipmentInventoryData().inventory_equipment;
    }

    /// <summary>
    /// Returns a List<string> representing saved player loadout list
    /// </summary>
    /// <returns></returns>
    public static List<string> GetPlayerLoadoutEquipment()
    {
        return LoadPlayerEquipmentInventoryData().loadout_equipment;
    }

    /// <summary>
    /// Sets the player loadout equipment as given List<string> of equipment, then saves player inventory data
    /// </summary>
    /// <returns></returns>
    public static void SetPlayerLoadoutEquipment(List<string> equipment)
    {
        PlayerEquipmentData eqData = LoadPlayerEquipmentInventoryData();
        eqData.loadout_equipment = equipment;
        SavePlayerEquipmentInventoryData(eqData);
    }
    static string GetTimestamp()
    {
        DateTime theTime = DateTime.Now;
        string datetime = theTime.ToString("yyyy-MM-dd\\HH-mm-ss\\Z");
        return datetime;
    }
    #endregion

    #region LIMEN TESTING COMMANDS
    /// <summary>
    /// Testing commmand for spawning an equipment into world and the current player connection is owner
    /// </summary>
    /// <param name="equipmentID"></param>
    public void TSpawnEquipment(int equipmentID)
    {
        CMDSpawn(equipmentID, playerID);
    }
    #endregion
}
