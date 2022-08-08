//© 2022 by MADKEV Studio, all rights reserved
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalWaypoint : MonoBehaviour
{
    #region Singleton Setup & Initialization
    public static GlobalWaypoint Instance { get; private set; }
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

    [Header("Groups")]
    public WaypointGroup[] groups;
}
