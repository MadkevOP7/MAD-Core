//© 2022 by MADKEV, all rights reserved

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChunkData
{
    [SerializeReference]
    public List<ObjectData> data;

    ////Adjacent chunks
    //[SerializeReference]
    //public ChunkData top;
    //[SerializeReference]
    //public ChunkData bottom;
    //[SerializeReference]
    //public ChunkData left;
    //[SerializeReference]
    //public ChunkData right;
    //[SerializeReference]
    //public List<ChunkData> neighbors = new List<ChunkData>();
}

[Serializable]
public class GlobalChunkSave
{
    [SerializeReference]
    public List<ChunkData> save = new List<ChunkData>();
}
