using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using Ceras;
using GPUInstancer;
using System;

[CustomEditor(typeof(ForestManager))]
public class ForestManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        ForestManager item = (ForestManager)target;

        if (GUILayout.Button("Generate Forest from Data"))
        {

        }
    }
}

//[ExecuteInEditMode]
public class MADForestHelper : EditorWindow
{
    static string FILENAME = "Default_Forest_Data.sav";
    static string CHUNK_FILENAME = "SM_Forest_Chunk_Data.sav";
    static string path = "Assets/StreamingAssets/";

    static int chunkSize = 100;
    static int gridSize = 7000;
    [MenuItem("MADKEV/MAD Forest Helper")]
    static void Init()
    {
        MADForestHelper window = (MADForestHelper)GetWindow(typeof(MADForestHelper));
    }
    void OnGUI()
    {
        if (Selection.count == 0)
        {
            if (GUILayout.Button("Generate Forest Chunks"))
            {
                GenerateForestChunkData();
            }

            if (GUILayout.Button("Validate Forest Chunk Data"))
            {
                ValidateForestChunkData();
            }
        }
        else
        {
            if (GUILayout.Button("Create Default World Forest File"))
            {
                CreateDefaultForestData();
            }

            if (GUILayout.Button("Validate Forest Data"))
            {
                ValidateForestData();
            }
        }

    }

    public void GenerateForestChunkData()
    {
        ForestData forest = Load();
        int index = 0;
        ForestChunkData chunkData = new ForestChunkData();
        int c = gridSize / chunkSize;
        chunkData.chunks = new ForestChunk[c, c];

        for (int i = 0; i < forest.data.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Generating Chunk Data", "Processing: " + (i + 1).ToString() + "/" + forest.data.Length.ToString(), (i + 1) / (float)forest.data.Length);
            Vector3 position = Matrix4x4FromString(forest.data[i].matrixData).GetColumn(3);
            int cellX = GetXCellIndex(position.x);
            int cellZ = GetZCellIndex(position.z);

            if (chunkData.chunks[cellX, cellZ] == null)
            {
                ForestChunk data = new ForestChunk();
                data.chunkID = index;
                index++;
                chunkData.chunks[cellX, cellZ] = data;
            }
            chunkData.chunks[cellX, cellZ].forestTreeIDs.Add(forest.data[i].treeID);
        }

        SaveChunkData(chunkData);
        EditorUtility.ClearProgressBar();
        Debug.Log("Generated Chunk Data");
        ValidateForestChunkData();
    }
    void ValidateForestChunkData()
    {
        ForestChunkData chunkData = LoadChunkData();
        ForestData forestData = Load();

        if(chunkData == null)
        {
            Debug.LogError("Failed: Couldn't load forest chunk data");
            EditorUtility.ClearProgressBar();
            return;
        }

        if(chunkData.chunks == null)
        {
            Debug.LogError("Failed: Null 2D Array");
            EditorUtility.ClearProgressBar();
            return;
        }
        //Validate Grids
        int c = gridSize / chunkSize;
        //for (int i = 0; i < c; i++)
        //{
        //    EditorUtility.DisplayProgressBar("Validating Chunk Data", "Verifying all chunk existence", (i + 1) / (float)c);
        //    for (int j = 0; j < c; j++)
        //    {
        //        if (chunkData.chunks[i, j] == null)
        //        {
        //            Debug.LogError("Failed at validating chunk data all grids: " + "["+ i + ", " + j + "]");
        //            EditorUtility.ClearProgressBar();
        //            return;
        //        }
        //    }
        //}

        for (int i = 0; i < forestData.data.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Validating Chunk Data", "Processing Tree: " + (i + 1).ToString() + "/" + forestData.data.Length, (i + 1) / (float) forestData.data.Length);
            ForestChunk chunk = GetChunkByPosition(Matrix4x4FromString(forestData.data[i].matrixData).GetColumn(3), chunkData);
            if (chunk == null)
            {
                Debug.LogError("Failed: Chunk data is null");
                EditorUtility.ClearProgressBar();
                return;
            }
            //else
            //{
            //    //Debug.Log("Loaded Chunk ID: " + chunk.chunkID + " Trees count: " + chunk.forestTreeIDs.Count);
            //    foreach(var d in chunk.forestTreeIDs)
            //    {
            //        //Debug.Log("Chunk" + "[" + chunk.chunkID + "] " + "Tree ID: " + d);
            //    }
                
            //}

            bool FoundMatch = false;

            //Check if tree exists in the data
            foreach(var id in chunk.forestTreeIDs)
            {
                if (id == forestData.data[i].treeID)
                {
                    FoundMatch = true;
                    break;
                }
            }

            if (!FoundMatch)
            {
                Debug.LogError("Failed: Tree match not found in chunk data\n" + "Tree ID: " + i + "\n" + "Tree Name: " + forestData.data[i].treeName);
                EditorUtility.ClearProgressBar();
                return;
            }
            
        }

        Debug.Log("Forest Chunk Data Validation: Passed!");
        EditorUtility.ClearProgressBar();
        return;
    }

    public ForestChunk GetChunkByPosition(Vector3 position, ForestChunkData chunkData)
    {
        int xCell = GetXCellIndex(position.x);
        int zCell = GetZCellIndex(position.z);
        Debug.Log("Sucess Get Chunk for: " + xCell + ", " + zCell);
        return chunkData.chunks[xCell, zCell];
    }
    void ValidateForestData()
    {
        ForestData data = Load();
        Trees[] trees = Selection.activeGameObject.GetComponentsInChildren<Trees>();
        int counter = 1;
        //Debug.Log(ValidateTree(trees[0].gameObject, data.data));
        foreach (var t in trees)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Validating Forest Data", "Validate " + counter + "/" + trees.Length, (float)counter / trees.Length))
            {
                Debug.Log("Validation canceled at: " + counter + "/" + trees.Length);
                break;
            }
            if (!ValidateTree(t.transform, data.data))
            {
                Debug.Log("Validate Complate: failed");
                Debug.Log("Failed at #: " + (counter - 1) + " " + t.gameObject.name);
                EditorUtility.ClearProgressBar();
                return;
            }
            counter++;
        }
        EditorUtility.ClearProgressBar();
        Debug.Log("Validate Complate: passed!");

    }
    public int GetXCellIndex(float x)
    {
        return Mathf.FloorToInt((gridSize / chunkSize) - Mathf.Abs(x - gridSize / 2) / (float)chunkSize);
    }

    //Returns the index of cell in the [,] grid given a z position worldpoint
    public int GetZCellIndex(float z)
    {
        return Mathf.FloorToInt((gridSize / chunkSize) - Mathf.Abs(z - gridSize / 2) / (float)chunkSize);
    }
    public bool ValidateTree(Transform tree, ForestTreeData[] treeList)
    {
        //int match = 0;
        foreach (var t in treeList)
        {
            if (t.treeName != tree.gameObject.name) continue;
            //Vector3 tPos = ToVector3(t.position);
            //if (tPos.Equals(tree.position))
            //{
            //    //Found Position Match
            //    if (!ToVector3(t.scale).Equals(tree.localScale))
            //    {
            //        Debug.Log("Failed at: Scale Validation for " + tree.gameObject.name);
            //        Debug.Log("Actual: " + ToVector3(t.scale) + " Data: " + tree.localScale);
            //        return false;
            //    }

            //    if (!ToVector3(t.rotation).Equals(tree.eulerAngles))
            //    {
            //        Debug.Log("Failed at: Rotation Validation for " + tree.gameObject.name);
            //        Debug.Log("Actual: " + ToVector3(t.rotation) + " Data: " + tree.eulerAngles);
            //        return false;
            //    }
            //    return true;
            //}
        }
        //if (match == 1) return true;
        //Debug.LogError("Match found: " + tree.name + " " + match);
        return false;
    }
    public ForestData Load()
    {
        var ceras = new CerasSerializer();
        return ceras.Deserialize<ForestData>(File.ReadAllBytes(path + FILENAME));
    }
    public ForestChunkData LoadChunkData()
    {
        var ceras = new CerasSerializer();
        return ceras.Deserialize<ForestChunkData>(File.ReadAllBytes(path + CHUNK_FILENAME));
    }
    void CreateDefaultForestData()
    {
        Trees[] trees = Selection.activeGameObject.GetComponentsInChildren<Trees>();
        List<ForestTreeData> ForestTreeData = new List<ForestTreeData>();
        ForestData forestData = new ForestData();

        for (int i = 0; i < trees.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Converting to Forest Data", "Processing: " + (i + 1).ToString() + "/" + trees.Length.ToString(), (float)i / trees.Length);
            GameObject Go = trees[i].gameObject;
            Transform t = Go.transform;
            ForestTreeData data = new ForestTreeData();
            data.treeName = Go.name;
            //data.position = new VectorThree(t.position);
            //data.rotation = new VectorThree(t.eulerAngles);
            //data.scale = new VectorThree(t.localScale);

            //Matrix 4x4 data
            data.matrixData = GPUInstancerUtility.Matrix4x4ToString(t.transform.localToWorldMatrix);

            data.health = trees[i].health;
            data.reward_id = trees[i].reward_id;

            ForestTreeData.Add(data);
        }

        forestData.data = ForestTreeData.ToArray();

        //Assign IDs
        for (int i = 0; i < forestData.data.Length; i++)
        {
            forestData.data[i].treeID = i;
        }
        SaveData(forestData);
        EditorUtility.ClearProgressBar();
        Debug.Log("Sucessfully processed");
    }
    public static void SaveData(ForestData data)
    {
        var ceras = new CerasSerializer();
        var bytes = ceras.Serialize(data);
        File.WriteAllBytes(path + FILENAME, bytes);
        Debug.Log("saved forest data");
    }

    public static void SaveChunkData(ForestChunkData data)
    {
        var ceras = new CerasSerializer();
        var bytes = ceras.Serialize(data);
        File.WriteAllBytes(path + CHUNK_FILENAME, bytes);
        Debug.Log("saved chunk data");
    }
    public Vector3 ToVector3(VectorThree vector)
    {
        return new Vector3(vector.x, vector.y, vector.z);
    }

    public Matrix4x4 Matrix4x4FromString(string matrixStr)
    {
        Matrix4x4 matrix4x4 = new Matrix4x4();
        string[] floatStrArray = matrixStr.Split(';');
        for (int i = 0; i < floatStrArray.Length; i++)
        {
            matrix4x4[i / 4, i % 4] = float.Parse(floatStrArray[i], System.Globalization.CultureInfo.InvariantCulture);
        }
        return matrix4x4;
    }
}
