using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Lean.Localization;
public class LobbyGameStats : MonoBehaviour
{
    public SaveData dataStorage = new SaveData();
    [Header("Reference:")]
    public Text rank;
    public Text exp_needed;
    public Text money;
    public bool isLoading;
    // Start is called before the first frame update
    void Start()
    {
        RefreshStats();
    }

    private void OnEnable()
    {
        RefreshStats();
    }

    private void OnDisable()
    {
        isLoading = false;
    }
    private const string FILENAME = "/Game_Data.sav";

    public void RefreshStats()
    {
        if (!isLoading)
        {
            StartCoroutine(Refresh());
        }
    }

    IEnumerator Refresh()
    {
        isLoading = true;
        //Enable Lean Text for updating localization
        foreach (LeanLocalizedText l in GetComponentsInChildren<LeanLocalizedText>())
        {
            l.enabled = true;
        }
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();
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
        //Disable Lean Text else text won't change
        foreach (LeanLocalizedText l in GetComponentsInChildren<LeanLocalizedText>())
        {
            l.enabled = false;
        }
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.1f);
        dataStorage = Load();
        rank.text = rank.text + " " + dataStorage.rank;
        exp_needed.text = exp_needed.text + " " + (1000 - dataStorage.xp)+"XP";
        money.text = money.text + " " + "$"+ dataStorage.money;
        isLoading = false;
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
  
}
