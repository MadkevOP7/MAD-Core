using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
[Serializable]
public class SaveData
{
    // Game Data //
    public string player_name;
    public int xp;
    public int money;
    public int rank;
    public int kills;
    public int hostMachine_destroys;
}
//Handles storing data as well, and writing to steam cloud
//Saves to a file called GameStats.mk, 
public class GameStats : MonoBehaviour
{
    [Header("Stats")]
    public bool isWinner;
    public int kills;
    public int destroys;
    private ResultWindowDisplay resultWindow;
    public SaveData dataStorage = new SaveData();
    public bool canStart;
    // Start is called before the first frame update
    void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        DontDestroyOnLoad(this.gameObject); //To store update once game to lobby
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    IEnumerator DelayStart()
    {
        while (!canStart)
        {
            yield return null;
        }

        Display();
    }
    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            canStart = true;
        }
    }

    private void Display()
    {
        canStart = false;
        //Get display window reference
        resultWindow = GameObject.FindGameObjectWithTag("Result Window").GetComponent<ResultWindowDisplay>();
        //Calculate Results
        int r_xp = isWinner ? 1450 : 750; //Add xp reward, 1450 for winner and 750 for regular
        int baseMoney = 25;
        int winMoney = isWinner ? 175 : 0;
        int kill_bonus = 25 * kills;
        int destroy_bonus = 25 * destroys;
        int totalPayment = baseMoney + winMoney + kill_bonus + destroy_bonus;
        //Write to Save
        LoadGame();
        dataStorage.money += totalPayment;
        dataStorage.kills += kills;
        dataStorage.hostMachine_destroys += destroys;
        //Calculate rank and XP
        int x = dataStorage.xp + r_xp;
        int rankIncrease = x / 1000;
        int leftOverXP = x % 1000;
        dataStorage.rank += rankIncrease;
        dataStorage.xp = leftOverXP;
        Save(dataStorage);

        resultWindow.Display(r_xp, baseMoney, winMoney, kill_bonus, destroy_bonus, totalPayment);

        //Reload
        LoadGame();
    }
    public void DisplayResult()
    {
        StartCoroutine(DelayStart());
    }

    private const string FILENAME = "/Game_Data.sav";
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

   
}
