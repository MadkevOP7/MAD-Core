using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Mirror;
using Steamworks;
//Local function MLobby will handle sync!
public class PlayerStats : NetworkBehaviour
{
    [SyncVar]
    public string p_name = "Kevin";
    [SyncVar]
    public int rank = 1;

    //For lobby list
    [SyncVar(hook =nameof(HookCallMLobbyRefreshAgain))]
    public bool isReady;
    [SyncVar]
    public bool isHost;
    public SaveData dataStorage = new SaveData();
    #region Load/Save
    private const string FILENAME = "/Game_Data.sav";

    public void HookCallMLobbyRefreshAgain(bool oldVal, bool newVal)
    {
        GetComponent<MLobbyManager>().HostRefreshCanStartGame();
        GetComponent<MLobbyManager>().CMDListRefresh();
    }
    public void RefreshData()
    {
        string name = SteamManager.Initialized ? SteamFriends.GetPersonaName(): "No Name";
        LoadGame();
        CMDRefresh(name, dataStorage.rank);
 
    }
    [Command(requiresAuthority =false)]
    public void CMDSetReady(bool ready)
    {
        isReady = ready;
    }
    [Command(requiresAuthority = false)]
    public void CMDRefresh(string n, int r)
    {
        p_name = n;
        rank = r;
        isHost = isLocalPlayer && isServer;
    }
    public static SaveData Load()
    {
        if (File.Exists(Application.persistentDataPath + FILENAME))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Open);

            SaveData data = bf.Deserialize(stream) as SaveData;

            stream.Close();

            return data;
        }
        else
        {
            Debug.LogError("File not found.");
            return null;
        }
    }

    //Save Load Game
    public void LoadGame()
    {
        if (Load() == null)
        {
            Debug.LogWarning("Recreating Save");
            SaveData newSave = new SaveData();
            newSave.player_name = "No Name";
            newSave.rank = 1;
            newSave.money = 100;
            newSave.xp = 0;
            Save(newSave);
        }
        dataStorage = Load();
        Debug.Log("XP: " + dataStorage.xp);
    }
    //Local Save Load to disk
    public static void Save(SaveData data)
    {
        //No adusting minimum currently

        BinaryFormatter bf = new BinaryFormatter();
        FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Create);

        bf.Serialize(stream, data);
        stream.Close();
    }
    #endregion
}
