//© 2022 by MADKEV, all rights reserved

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RuntimeChunk : MonoBehaviour
{
    //Data handling===================
    public int chunk_id;
    //
    public List<RuntimeChunk> neighbors = new List<RuntimeChunk>();
    //Adjacent chunks
    public RuntimeChunk top;
    public RuntimeChunk bottom;
    public RuntimeChunk left;
    public RuntimeChunk right;

    //
    public List<GameObject> allocated = new List<GameObject>();
    public int pool_id = -1;

    //Bounds==========================
    public float Xmin, Xmax, Zmin, ZMax;
    [Header("Testing and Debug")]
    public bool allocate;
    public bool isActive;
    private void Start()
    {
        if (allocate)
        {
            LoadChunk();
        }
    }
    private void Awake()
    {
        InitializeBounds();
    }
    public void InitializeBounds()
    {
        Bounds b = GetComponent<Collider>().bounds;
        Xmin = b.min.x;
        Xmax = b.max.x;
        Zmin = b.min.z;
        ZMax = b.max.z;
    }
    public void LoadChunk()
    {
        if (isActive) return;
        allocated = ObjectPool.Instance.AllocatePool(chunk_id, true);
        isActive = true;
    }
  
    public void OffloadChunk()
    {
        if (!isActive) return;
        if (allocated == null || allocated.Count == 0) return;
        ObjectPool.Instance.DeAllocatePool(allocated);
        isActive = false;

    }
    #region Data Getters

    #endregion
}
