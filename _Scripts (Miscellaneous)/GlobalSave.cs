using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalSave : MonoBehaviour
{
    #region Singleton Setup & Initialization
    public static GlobalSave Instance { get; private set; }
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
    public void SavePlayerData()
    {
        EquipmentStorage.Instance.SavePlayerCarryEquipment();
    }
    public void SaveGame()
    {
        GlobalInventory.Instance.SaveInventory();
        GlobalBuild.Instance.SaveWorld();
        EquipmentStorage.Instance.SaveAllEquipment();
        SavePlayerData();
        //Save player position to base set position, so if player falls through map they can come back
    }

    public void LoadGame()
    {
        GlobalBuild.Instance.LoadWorld();
        EquipmentStorage.Instance.LoadAllEquipment();

    }
}
