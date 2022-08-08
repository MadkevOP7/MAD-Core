//© 2022 by MADKEV, all rights reserved

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEngine;

public class ChunkCreator : EditorWindow
{
    private const string FILENAME = "/Chunk_Data.sav";

    #region Window
    [MenuItem("MADKEV/Chunk Creator")]
    static void Init()
    {
        ChunkCreator window = (ChunkCreator)GetWindow(typeof(ChunkCreator));
        window.Show();
    }
    //ChunkAPI c = new ChunkAPI();
    void OnGUI()
    {
        if (Selection.count == 0 || Selection.activeGameObject.transform == null) return;

        selection = Selection.activeGameObject.transform;
        if (GUILayout.Button("Generate"))
        {
            GenerateChunksForAllObjects();
        }
        if (GUILayout.Button("Generate Debug Chunks"))
        {
            GenerateDebugChunks();
        }
        if (GUILayout.Button("Greate Debug 3D Boundary"))
        {
            List<Transform> t = new List<Transform>();
            Create3DBondary(out t);
        }
        if (GUILayout.Button("Clear All"))
        {
            ClearAllSaves();
        }
    }

    #endregion
    //============================================
    static float FLOAT_MIN = float.MinValue;
    static float FLOAT_MAX = float.MaxValue;
    public ChunkDictionary cd;
    public Transform selection;
    bool is_waiting_for_asset;
    //============================================
    //Boundary
    Bounding bound = new Bounding();
    public Vector3 b_center;
    bool drawBound;
    static float gridSize = 64f;
    //============================================
    static string path = "Assets/ChunkData/";
    public List<BoxCollider> bounds = new List<BoxCollider>();

    //Global Data=================================
    public GlobalChunkSave save = new GlobalChunkSave();
    public List<List<RuntimeChunk>> global_chunk_data = new List<List<RuntimeChunk>>();
    public List<ObjectData> global_object_data = new List<ObjectData>();
    //============================================
    #region Core
    public void GenerateDebugChunks()
    {
        global_chunk_data.Clear();
        List<Transform> list = new List<Transform>();
        Bounding mainGrid = Create3DBondary(out list);
        GameObject p = new GameObject("Global Chunks");
        //Array to list

        //Calculate each grid
        //int x_count = Mathf.CeilToInt(GetBoundXSize(mainGrid) / gridSize);
        //int z_count = Mathf.CeilToInt(GetBoundZSize(mainGrid) / gridSize);
        //int total = x_count + z_count;
        //float x = mainGrid.xMin;
        //float z = mainGrid.zMin;
        //float xMax = mainGrid.xMax;
        //float zMax = mainGrid.zMax;

        for (float x = mainGrid.xMin; x <= mainGrid.xMax; x += gridSize)
        {
            //z = mainGrid.zMin;
            List<ChunkData> z_chunk_data = new List<ChunkData>();
            for (float z = mainGrid.zMin; z < mainGrid.zMax; z += gridSize)
            {
                float total = Mathf.Abs(mainGrid.xMax * mainGrid.zMax);
                float current_progress = Mathf.Abs(x * z);
                float progress = CalculateProgress(current_progress, total);
                EditorUtility.DisplayProgressBar("Generating Chunks", "Processing chunk " + current_progress + "/" + total, progress);
                //Create each chunk grid, starts at 3D bound xmin and zmin
                Bounding g = Create3DGrid(x, (x + gridSize), z, (z + gridSize), mainGrid.yMin, mainGrid.yMax);

                Vector3 pos = GetCenter(g);
                string n = "Chunk " + x + " " + z;
                //Get center
                GameObject m_c = DrawColliderBounds(g, pos, n);
                RuntimeChunk chunk = m_c.AddComponent<RuntimeChunk>();
                m_c.tag = "RuntimeChunk";
                m_c.transform.SetParent(p.transform, true);

            }

        }
        EditorUtility.ClearProgressBar();
        Debug.Log("Completed Debug Chunk Generation");
    }
    public void GenerateChunksForAllObjects()
    {
        global_chunk_data.Clear();
        global_object_data.Clear();
        ClearAllSaves();
        List<Transform> list = new List<Transform>();

        Bounding mainGrid = Create3DBondary(out list);
        GameObject p = new GameObject("Global Chunks");
        //Array to list

        //Calculate each grid
        //int x_count = Mathf.CeilToInt(GetBoundXSize(mainGrid) / gridSize);
        //int z_count = Mathf.CeilToInt(GetBoundZSize(mainGrid) / gridSize);
        //int total = x_count + z_count;
        //float x = mainGrid.xMin;
        //float z = mainGrid.zMin;
        //float xMax = mainGrid.xMax;
        //float zMax = mainGrid.zMax;
        List<ChunkData> save_data = new List<ChunkData>();
        for (float x = mainGrid.xMin; x < mainGrid.xMax; x += gridSize)
        {
            //z = mainGrid.zMin;
            List<RuntimeChunk> z_chunk_data = new List<RuntimeChunk>();
            for (float z = mainGrid.zMin; z < mainGrid.zMax; z += gridSize)
            {

                float total = Mathf.Abs(mainGrid.xMax * mainGrid.zMax);
                float current_progress = Mathf.Abs(x * z);
                float progress = CalculateProgress(current_progress, total);
                EditorUtility.DisplayProgressBar("Generating Chunks", "Processing chunk " + current_progress + "/" + total, progress);
                //Create each chunk grid, starts at 3D bound xmin and zmin
                Bounding g = Create3DGrid(x, (x + gridSize), z, (z + gridSize), mainGrid.yMin, mainGrid.yMax);

                Vector3 pos = GetCenter(g);
                string n = "Chunk " + x + " " + z;
                //Get center
                GameObject m_c = DrawColliderBounds(g, pos, n);
                RuntimeChunk chunk = m_c.AddComponent<RuntimeChunk>();
                m_c.transform.SetParent(p.transform, true);
                m_c.tag = "RuntimeChunk";
                //Initialize ChunkData asset
                ChunkData chunkData = new ChunkData();
                //AssetDatabase.CreateAsset(chunkData, path + "Chunks/" + n + ".asset");
                //chunk.data = chunkData;
                save_data.Add(chunkData);
                chunk.chunk_id = save_data.IndexOf(chunkData);
                //Chunkdata storage
                List<ObjectData> obj_data = new List<ObjectData>();
                //Calculate objects that are in this grid
                int id = 0;
                for (int k = list.Count - 1; k >= 0; --k)
                {
                    if (IsInBound2D(list[k], g))
                    {
                        Trees tree = list[k].GetComponent<Trees>();
                        //Create Object data asset and write data
                        ObjectData objData = new ObjectData();
                        //AssetDatabase.CreateAsset(objData, path + "Items/" + k + ".asset");
                        global_object_data.Add(objData);
                        objData.position = ToVectorThree(list[k].position);
                        objData.rotation = ToVectorThree(list[k].eulerAngles);
                        objData.scale = ToVectorThree(list[k].localScale);
                        objData.health = tree.health;
                        objData.reward_id = tree.reward_id;
                        objData.m_name = list[k].gameObject.name;
                        objData.id = id;
                        id++;
                        obj_data.Add(objData);
                        DestroyImmediate(list[k].gameObject);
                        list.RemoveAt(k);
                        //AssetDatabase.SaveAssets();
                    }
                }
                chunkData.data = obj_data;
                z_chunk_data.Add(chunk);
                //AssetDatabase.SaveAssets();

            }
            global_chunk_data.Add(z_chunk_data);

        }
        save.save = save_data;
        EditorUtility.ClearProgressBar();
        AssignNeighboringChunks();
        Debug.Log("Completed chunk generation, proceeeding to write asset data to disk");
        Save(save);
    }

    #endregion

    #region Grid Generation
    public Bounding Create2DGrid(float x1, float x2, float z1, float z2)
    {
        Bounding b = new Bounding();
        b.xMin = x1;
        b.xMax = x2;
        b.zMin = z1;
        b.zMax = z2;
        return b;
    }

    public Bounding Create3DGrid(float x1, float x2, float z1, float z2, float y1, float y2)
    {
        Bounding b = new Bounding();
        b.xMin = x1;
        b.xMax = x2;
        b.zMin = z1;
        b.zMax = z2;
        b.yMin = y1;
        b.yMax = y2;
        return b;
    }

    //Calculates the overall volume to be divided into grids
    public Bounding Create3DBondary(out List<Transform> t_list)
    {
        ClearSceneChunks();
        bound = new Bounding();
        List<Transform> transforms = new List<Transform>();
        Trees[] trees = selection.GetComponentsInChildren<Trees>();
        for (int i = 0; i < trees.Length; i++)
        {

            Transform t = trees[i].transform;
            transforms.Add(t);
            float progress = CalculateProgress(i, trees.Length);
            EditorUtility.DisplayProgressBar("Calculating Bound", "Processing " + t.name + " " + (i + 1) + "/" + trees.Length, progress);
            if (t != selection)
            {

                //Calculate mins
                if (t.position.x < bound.xMin)
                {
                    bound.xMin = t.position.x;
                }
                if (t.position.y < bound.yMin)
                {
                    bound.yMin = t.position.y;
                }
                if (t.position.z < bound.zMin)
                {
                    bound.zMin = t.position.z;
                }

                //Calculate maxs
                if (t.position.x > bound.xMax)
                {
                    bound.xMax = t.position.x;
                }
                if (t.position.y > bound.yMax)
                {
                    bound.yMax = t.position.y;
                }
                if (t.position.z > bound.zMax)
                {
                    bound.zMax = t.position.z;
                }
            }

        }

        //Round max up, round min down
        bound.xMax = Mathf.Ceil(bound.xMax);
        bound.yMax = Mathf.Ceil(bound.yMax);
        bound.zMax = Mathf.Ceil(bound.zMax);
        bound.xMin = Mathf.Floor(bound.xMin);
        bound.yMin = Mathf.Floor(bound.yMin);
        bound.zMin = Mathf.Floor(bound.zMin);
        //Draws bound for parent central bound
        Vector3 center = GetCenter(bound);

        //DEBUG=======================================================
        DrawColliderBounds(bound, center, "Chunk Grid Overview");
        GameObject c = new GameObject("Center Debug");
        c.transform.position = center;
        //============================================================
        EditorUtility.ClearProgressBar();
        t_list = transforms;
        return bound;
    }

    #endregion

    #region Object Calculation 

    //Returns the center position for a given bound
    public Vector3 GetCenter(Bounding b)
    {
        float x = (b.xMax + b.xMin) / 2;
        float y = (b.yMax + b.yMin) / 2;
        float z = (b.zMax + b.zMin) / 2;
        return new Vector3(x, y, z);
    }
    //Calculates if an object is in the bounding, ignores height
    public bool IsInBound2D(Transform t, Bounding b)
    {
        float x = t.position.x;
        float z = t.position.z;
        if (x >= b.xMin && x <= b.xMax && z >= b.zMin && z <= b.zMax)
        {
            return true;
        }
        return false;
    }

    public float GetBoundXSize(Bounding b)
    {
        return b.xMax - b.xMin;
    }

    public float GetBoundZSize(Bounding b)
    {
        return b.zMax - b.zMin;
    }

    public float GetBoundYSize(Bounding b)
    {
        return b.yMax - b.yMin;
    }
    #endregion

    #region Debug Draw Functions
    public class Bounding
    {
        public float xMax = FLOAT_MIN;
        public float yMax = FLOAT_MIN;
        public float zMax = FLOAT_MIN;
        public float xMin = FLOAT_MAX;
        public float yMin = FLOAT_MAX;
        public float zMin = FLOAT_MAX;

    }
    GameObject DrawColliderBounds(Bounding b, Vector3 center, string name)
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temp.name = name;
        //Remove not needed comp
        DestroyImmediate(temp.GetComponent<MeshRenderer>());
        DestroyImmediate(temp.GetComponent<MeshFilter>());

        if (temp.GetComponent<BoxCollider>() == null)
        {
            temp.AddComponent<BoxCollider>();
        }
        BoxCollider bc = temp.GetComponent<BoxCollider>();
        bounds.Add(bc);
        bc.isTrigger = true;
        //Center bound obj
        temp.transform.position = center;

        //Calculate length, width, height
        float length = b.xMax - b.xMin;
        float width = b.zMax - b.zMin;
        float height = b.yMax - b.yMin;

        //Apply to bound obj
        temp.transform.localScale = new Vector3(length, height, width);
        return temp;

    }

    GameObject DrawColliderBounds(Bounding b, Transform center, string name)
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temp.name = name;
        //Remove not needed comp
        DestroyImmediate(temp.GetComponent<MeshRenderer>());
        DestroyImmediate(temp.GetComponent<MeshFilter>());

        if (temp.GetComponent<BoxCollider>() == null)
        {
            temp.AddComponent<BoxCollider>();
        }
        BoxCollider bc = temp.GetComponent<BoxCollider>();
        bounds.Add(bc);
        bc.isTrigger = true;
        //Center bound obj
        temp.transform.position = center.position;

        //Calculate length, width, height
        float length = b.xMax - b.xMin;
        float width = b.zMax - b.zMin;
        float height = b.yMax - b.yMin;

        //Apply to bound obj
        temp.transform.localScale = new Vector3(length, height, width);
        return temp;

    }
    void OnDrawGizmos()
    {
        if (drawBound)
        {
            // bottom
            Bounding b = bound;
            var p1 = new Vector3(b.xMin, b.yMin, b.zMin);
            var p2 = new Vector3(b.xMax, b.yMin, b.zMin);
            var p3 = new Vector3(b.xMax, b.yMin, b.zMax);
            var p4 = new Vector3(b.xMin, b.yMin, b.zMax);

            Handles.DrawLine(p1, p2);
            Handles.DrawLine(p2, p3);
            Handles.DrawLine(p3, p4);
            Handles.DrawLine(p4, p1);


            // top
            var p5 = new Vector3(b.xMin, b.yMax, b.zMin);
            var p6 = new Vector3(b.xMax, b.yMax, b.zMin);
            var p7 = new Vector3(b.xMax, b.yMax, b.zMax);
            var p8 = new Vector3(b.xMin, b.yMax, b.zMax);

            Handles.DrawLine(p5, p6);
            Handles.DrawLine(p6, p7);
            Handles.DrawLine(p7, p8);
            Handles.DrawLine(p8, p5);

            // sides
            Handles.DrawLine(p1, p5);
            Handles.DrawLine(p2, p6);
            Handles.DrawLine(p3, p7);
            Handles.DrawLine(p4, p8);
        }

    }
    void DrawDebugBounds(Bounding b, Color col, float delay = 0)
    {
        // bottom
        var p1 = new Vector3(b.xMin, b.yMin, b.zMin);
        var p2 = new Vector3(b.xMax, b.yMin, b.zMin);
        var p3 = new Vector3(b.xMax, b.yMin, b.zMax);
        var p4 = new Vector3(b.xMin, b.yMin, b.zMax);

        Debug.DrawLine(p1, p2, col, delay);
        Debug.DrawLine(p2, p3, col, delay);
        Debug.DrawLine(p3, p4, col, delay);
        Debug.DrawLine(p4, p1, col, delay);

        // top
        var p5 = new Vector3(b.xMin, b.yMax, b.zMin);
        var p6 = new Vector3(b.xMax, b.yMax, b.zMin);
        var p7 = new Vector3(b.xMax, b.yMax, b.zMax);
        var p8 = new Vector3(b.xMin, b.yMax, b.zMax);

        Debug.DrawLine(p5, p6, col, delay);
        Debug.DrawLine(p6, p7, col, delay);
        Debug.DrawLine(p7, p8, col, delay);
        Debug.DrawLine(p8, p5, col, delay);

        // sides
        Debug.DrawLine(p1, p5, col, delay);
        Debug.DrawLine(p2, p6, col, delay);
        Debug.DrawLine(p3, p7, col, delay);
        Debug.DrawLine(p4, p8, col, delay);
    }
    #endregion

    #region Extra Helper Utility
    public VectorThree ToVectorThree(Vector3 v)
    {
        float x = v.x;
        float y = v.y;
        float z = v.z;
        VectorThree vector = new VectorThree();
        vector.x = x;
        vector.y = y;
        vector.z = z;
        return vector;
    }
    public float GetNestedListCount(List<List<RuntimeChunk>> data)
    {
        float count = 0;
        for (int i = 0; i < data.Count; i++)
        {
            count += data[i].Count;
        }
        return count;
    }
    public float CalculateProgress(float a, float b)
    {
        if (b == 0)
        {
            return 0f;
        }
        if (a >= b)
        {
            return 1.0f;
        }
        return a / b;
    }

    public List<Transform> ToTransformList(Trees[] t)
    {
        List<Transform> transforms = new List<Transform>();
        foreach (Trees tree in t)
        {
            transforms.Add(tree.transform);
        }
        return transforms;
    }
    #endregion

    #region Chunk Management Functions
    public void AssignNeighboringChunks()
    {
        float total = GetNestedListCount(global_chunk_data);
        for (int x = 0; x < global_chunk_data.Count; x++)
        {
            for (int z = 0; z < global_chunk_data[x].Count; z++)
            {
                float c = (x + 1) * (z + 1);
                float progress = CalculateProgress(c, total);
                EditorUtility.DisplayProgressBar("Creating Chunk Neighboring", "Processing chunk " + c + "/" + total, progress);
                RuntimeChunk current = global_chunk_data[x][z];
                current.neighbors.Clear();
                //Calculate top, bottom, left, right chunk to assign to current one
                //Top data
                if ((z + 1) < global_chunk_data[x].Count && global_chunk_data[x][z + 1] != null)
                {
                    current.top = global_chunk_data[x][z + 1];
                    current.neighbors.Add(global_chunk_data[x][z + 1]);
                }

                //Bottom data
                if ((z - 1) >= 0 && (z - 1) < global_chunk_data[x].Count && global_chunk_data[x][z - 1] != null)
                {
                    current.bottom = global_chunk_data[x][z - 1];
                    current.neighbors.Add(global_chunk_data[x][z - 1]);
                }

                //Left data
                if ((x - 1) >= 0 && (x - 1) < global_chunk_data.Count && z < global_chunk_data[x - 1].Count && global_chunk_data[x - 1][z] != null)
                {
                    current.left = global_chunk_data[x - 1][z];
                    current.neighbors.Add(global_chunk_data[x - 1][z]);
                }

                //Right data
                if ((x + 1) < global_chunk_data.Count && z < global_chunk_data[x + 1].Count && global_chunk_data[x + 1][z] != null)
                {
                    current.right = global_chunk_data[x + 1][z];
                    current.neighbors.Add(global_chunk_data[x + 1][z]);
                }

            }
        }
        AssignNeighborCorners();
    }

    public void AssignNeighborCorners()
    {
        float total = GetNestedListCount(global_chunk_data);
        for (int x = 0; x < global_chunk_data.Count; x++)
        {
            for (int z = 0; z < global_chunk_data[x].Count; z++)
            {
                float c = (x + 1) * (z + 1);
                float progress = CalculateProgress(c, total);
                EditorUtility.DisplayProgressBar("Creating Chunk Corners", "Processing chunk " + c + "/" + total, progress);
                RuntimeChunk current = global_chunk_data[x][z];
                if (current.top != null)
                {
                    if (current.top.left != null)
                    {
                        current.neighbors.Add(current.top.left);
                    }

                    if (current.top.right != null)
                    {
                        current.neighbors.Add(current.top.right);
                    }
                }
                if (current.bottom != null)
                {
                    if (current.bottom.left != null)
                    {
                        current.neighbors.Add(current.bottom.left);
                    }

                    if (current.bottom.right != null)
                    {
                        current.neighbors.Add(current.bottom.right);
                    }
                }

                //Extra ring=======================================
                //Top
                if (current?.top?.top != null)
                {
                    current?.neighbors?.Add(current?.top?.top);
                    if (current?.top?.top?.left != null)
                    {
                        current?.neighbors?.Add(current?.top?.top?.left);
                        if (current?.top?.top?.left?.left != null)
                        {
                            current?.neighbors?.Add(current?.top?.top?.left?.left);
                        }
                    }
                    if (current?.top?.top?.right != null)
                    {
                        current?.neighbors?.Add(current?.top?.top?.right);
                        if (current?.top?.top?.right?.right != null)
                        {
                            current?.neighbors?.Add(current?.top?.top?.right?.right);
                        }
                    }
                }
                //Bottom
                if (current?.bottom?.bottom != null)
                {
                    current?.neighbors?.Add(current?.bottom?.bottom);
                    if (current?.bottom?.bottom?.left != null)
                    {
                        current?.neighbors?.Add(current?.bottom?.bottom?.left);
                        if (current?.bottom?.bottom?.left?.left != null)
                        {
                            current?.neighbors?.Add(current?.bottom?.bottom?.left?.left);
                        }
                    }
                    if (current?.bottom?.bottom?.right != null)
                    {
                        current?.neighbors?.Add(current?.bottom?.bottom?.right);
                        if (current?.bottom?.bottom?.right?.right != null)
                        {
                            current?.neighbors?.Add(current?.bottom?.bottom?.right?.right);
                        }
                    }
                }

                //Middle left & right
                if (current?.top?.left?.left != null)
                {
                    current?.neighbors?.Add(current?.top?.left?.left);
                }
                if (current?.top?.right?.right != null)
                {
                    current?.neighbors?.Add(current?.top?.right?.right);
                }

                if (current?.left?.left != null)
                {
                    current?.neighbors?.Add(current?.left?.left);
                }
                if (current?.right?.right != null)
                {
                    current?.neighbors?.Add(current?.right?.right);
                }

                if (current?.bottom?.left?.left != null)
                {
                    current?.neighbors?.Add(current?.bottom?.left?.left);
                }
                if (current?.bottom?.right?.right != null)
                {
                    current?.neighbors?.Add(current?.bottom?.right?.right);
                }

                //==================================================
            }
        }
        EditorUtility.ClearProgressBar();
        Debug.Log("Finished processing neighboring chunks");
    }
    public void ClearSceneChunks()
    {
        foreach (BoxCollider b in bounds)
        {
            if (b != null)
            {
                DestroyImmediate(b.gameObject);
            }
        }
        bounds.Clear();
    }
    #endregion

    #region Asset Saving and Deleting Functions
    public void ClearAllSaves()
    {
        is_waiting_for_asset = true;
        string[] paths = { "Assets/ChunkData/Chunks/", "Assets/ChunkData/Items/" };
        List<string> failed = new List<string>();
        if (AssetDatabase.DeleteAssets(paths, failed))
        {
            Debug.Log("All chunk data removed");
        }
        AssetDatabase.CreateFolder("Assets/ChunkData", "Chunks");
        AssetDatabase.CreateFolder("Assets/ChunkData", "Items");
        Debug.Log("Chunk Data clean up completed");
        is_waiting_for_asset = false;
    }

    public void WriteDataToDisk()
    {
        string object_path = "Assets/ChunkData/Items/";
        string chunk_path = "Assets/ChunkData/Chunks/";

        //Check folder exist
        if (!AssetDatabase.IsValidFolder("Assets/ChunkData/Items"))
        {
            AssetDatabase.CreateFolder("Assets/ChunkData", "Items");
        }
        if (!AssetDatabase.IsValidFolder("Assets/ChunkData/Chunks"))
        {
            AssetDatabase.CreateFolder("Assets/ChunkData", "Chunks");
        }
        try
        {
            //Start asset writing stream
            AssetDatabase.StartAssetEditing();

            //Create Object assets
            for (int i = 0; i < global_object_data.Count; i++)
            {
                //AssetDatabase.CreateAsset(global_object_data[i], object_path + i + ".asset");
            }

            //Create Chunk assets
            for (int i = 0; i < global_chunk_data.Count; i++)
            {
                for (int k = 0; k < global_chunk_data[i].Count; k++)
                {
                    //AssetDatabase.CreateAsset(global_chunk_data[i][k], chunk_path + i + " " + k + ".asset");
                }
            }
        }
        finally
        {
            //Close asset writing stream
            AssetDatabase.StopAssetEditing();
        }
    }
    #endregion

    #region Saving and Loading New
    //Local Save Load to disk
    public static void Save(GlobalChunkSave data)
    {

        BinaryFormatter bf = new BinaryFormatter();
        FileStream stream = new FileStream(Application.persistentDataPath + FILENAME, FileMode.Create);

        bf.Serialize(stream, data);
        stream.Close();
        Debug.Log("saved chunk data");
    }

    #endregion
}
