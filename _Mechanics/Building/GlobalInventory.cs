//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine.UI;
public class GlobalInventory : MonoBehaviour
{
    GlobalBuild globalBuild;
    //Save============================
    private const string FILENAME = "/Inventory_Data.sav";

    //================================
    //Inventory are saved per player, not all on the server. Players play multiplayer but the resources each player has will be all on their account.
    [Header("Storage")]
    public InventoryData inventory = new InventoryData();

    [Header("Runtime")]
    public Dictionary<string, GlobalItem> dictionary = new Dictionary<string, GlobalItem>();
    //Runtime
    
    private GlobalItem _selected = null;
    public GlobalItem selected
    {
        get
        {
            return _selected;
        }
        set
        {
            _selected = value;
        }
    }
    #region Singleton Setup & Initialization
    public static GlobalInventory Instance { get; private set; }
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

        inventory = Load();
        globalBuild = GetComponent<GlobalBuild>();
        InitializeDictionary();
        InitializeInventoryData();
    }
    #endregion

    #region Core Functions
    public void InitializeInventoryData()
    {
        foreach(BuildItem i in globalBuild.global_items)
        {
            AddItem(i.m_name, 0, i.type); //[Remove] Change to 0, 100 is for testing
        }
    }

    void InitializeDictionary()
    {
        foreach(GlobalItem i in inventory.inventory_data)
        {
            dictionary.Add(i.m_name, i);
        }
    }
    public void AddItem(string name, int count, BuildItem.Type type)
    {
        GlobalItem get;
        if(dictionary.TryGetValue(name, out get))
        {
            get.count += count;
            return;
        }
       
        GlobalItem item = new GlobalItem();
        item.m_name = name;
        item.count = count;
        item.type = type;
        inventory.inventory_data.Add(item);

        //Add to each type's list
        switch (type)
        {
            case BuildItem.Type.OFBlockVertical:
                inventory.OFBlocksVertical.Add(item);
                break;

            case BuildItem.Type.OFBlockFlat:
                inventory.OFBlocksFlat.Add(item);
                break;

            case BuildItem.Type.block:
                inventory.Blocks.Add(item);
                break;
        }
    }
    #endregion

    #region Save/Load
    public void SaveInventory()
    {
        Save(inventory);
    }
    //Local Save Load to disk
    public static void Save(InventoryData data)
    {
        //No adusting minimum currently

        BinaryFormatter bf = new BinaryFormatter();
        FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Create);

        bf.Serialize(stream, data);
        stream.Close();
    }

    public static InventoryData Load()
    {
        if (File.Exists(Application.persistentDataPath + FILENAME))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Open);

            InventoryData data = bf.Deserialize(stream) as InventoryData;
            
            stream.Close();

            return data;
        }
        else
        {
            Debug.LogError("Inventory data not found or corrupted, created new data save.");
            InventoryData d = new InventoryData();
            Save(d);
            return Load();
        }
    }
    #endregion
}

[Serializable]
public class GlobalItem
{
    [SerializeField]
    public int count;
    [SerializeField]
    public string m_name;
    [SerializeField]
    public BuildItem.Type type;
}

[Serializable]
public class InventoryData
{
    [SerializeReference]
    public List<GlobalItem> inventory_data = new List<GlobalItem>();

    //Specific types
    [SerializeReference]
    public List<GlobalItem> OFBlocksVertical = new List<GlobalItem>();

    [SerializeReference]
    public List<GlobalItem> OFBlocksFlat = new List<GlobalItem>();

    [SerializeReference]
    public List<GlobalItem> Blocks = new List<GlobalItem>();
}
