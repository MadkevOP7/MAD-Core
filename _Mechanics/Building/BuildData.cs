using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
[Serializable]
public class BuildData
{
    [SerializeField]
    public string m_name; //For all woods, the name must contain Wood as Inventory uses common contained to sort into groups

    //Transform Data=================
    [HideInInspector]
    [SerializeReference]
    public VectorThree position;
    [HideInInspector]
    [SerializeReference]
    public VectorThree rotation;
    [SerializeField]
    public int health = 100; //Lives on server but not syncvar
    [SerializeField]
    public bool isPickupMode = false;
}

[Serializable]
public class WorldData
{
    [SerializeReference]
    public List<BuildData> build_data = new List<BuildData>();
}
