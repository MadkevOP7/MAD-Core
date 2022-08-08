using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Not currently used
public class GlobalReference : MonoBehaviour
{
    public static GlobalReference Instance { get; private set; }
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

    //[Header("References")]
    //public bool isLocalPlayer;

    #region Public Functions
    //public void SetTabletOSState(bool _isLocalPlayer)
    //{
    //    isLocalPlayer = _isLocalPlayer;
    //    EquipmentStorage.Instance.observe_tablet.GetComponent<ObserveOS>().InitializeOSType(_isLocalPlayer);
    //}
    #endregion
}
