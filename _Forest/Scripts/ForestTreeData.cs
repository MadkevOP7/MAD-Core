using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
[Serializable]
public class ForestTreeData
{
    [NonSerialized]
    public int index; //Index within the ForestRuntimeData list, may be different each calculation

    [SerializeField]
    public int treeID; //Index within the global Tree List, must stay same

    [SerializeField]
    public string treeName;

    [SerializeField]
    public string matrixData;

    [SerializeField]
    public int health;
    [SerializeField]
    public int reward_id;

    //Comp operator overload
    public static bool operator ==(ForestTreeData lhs, ForestTreeData rhs)
    {
        if (lhs.matrixData != rhs.matrixData) return false;
        if (lhs.health != rhs.health) return false;
        return true;
    }
    public static bool operator !=(ForestTreeData lhs, ForestTreeData rhs) => !(lhs == rhs);
}


