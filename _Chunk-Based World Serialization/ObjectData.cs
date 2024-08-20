//© 2022 by MADKEV, all rights reserved

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ObjectData
{
    [SerializeField]
    public int id;
    //Should name that will reference to pool
    [SerializeField]
    public string m_name;

    //Transform data
    [SerializeReference]
    public VectorThree position;
    [SerializeReference]
    public VectorThree rotation;
    [SerializeReference]
    public VectorThree scale;

    //Tree (Custom data)
    [SerializeField]
    public int health;
    [SerializeField]
    public int reward_id;
}


