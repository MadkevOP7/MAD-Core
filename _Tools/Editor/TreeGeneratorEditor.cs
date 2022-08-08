using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
// Replaces Unity terrain trees with prefab GameObject.
// http://answers.unity3d.com/questions/723266/converting-all-terrain-trees-to-gameobjects.html
[ExecuteInEditMode]
public class TreeGeneratorEditor : EditorWindow
{
    [Header("References")]
    public Terrain[] selection;

    [Header("Runtime")]
    public int groupNumber = 0;
    GameObject _parent;
    //============================================
    [MenuItem("MADKEV/Tree Generator")]
    static void Init()
    {
        TreeGeneratorEditor window = (TreeGeneratorEditor)GetWindow(typeof(TreeGeneratorEditor));
    }
    void OnGUI()
    {
        if (Selection.count == 0)
            return;
        selection = Selection.activeGameObject.GetComponentsInChildren<Terrain>();
        if (GUILayout.Button("Generate Chunk Dictionary"))
        {
            ConvertToDictionary();
        }
        if (GUILayout.Button("Convert to Tree Groups"))
        {
            Convert();
        }
        if (GUILayout.Button("Clear generated trees"))
        {
            Clear();
        }
    }
    //============================================
    public void Convert()
    {

        float y_deviation = -0.55994f;
        _parent = GameObject.Find("Converted Trees Map");
        if (_parent == null)
        {
            _parent = new GameObject("Converted Trees Map");
        }
        for (int i = 0; i < selection.Length; i++)
        {
            Terrain _terrain = selection[i];
            TerrainData data = _terrain.terrainData;
            float width = data.size.x;
            float height = data.size.z;
            float y = data.size.y;
            // Create parent
            string group_id = "Converted Tree Group " + i;
            GameObject parent = GameObject.Find(group_id);
            if (parent == null)
            {
                parent = new GameObject(group_id);
            }
            // Create trees
            foreach (TreeInstance tree in data.treeInstances)
            {
               
                if (tree.prototypeIndex >= data.treePrototypes.Length)
                    continue;
                string prefab_name = data.treePrototypes[tree.prototypeIndex].prefab.name;
                if (prefab_name.ToLower().Contains("bush"))
                {
                    Debug.Log("Skipped bush");
                    continue;
                }
                var _tree = Resources.Load(prefab_name);
                bool found = false;

                if (_tree != null)
                {
                    found = true;
                }
              
                if (!found)
                {
                    Debug.LogError("Prefab name not found in Resources: " + prefab_name);
                    continue;
                }
               
                Vector3 position = new Vector3(
                    tree.position.x * width,
                    (tree.position.y * y) + y_deviation,
                    tree.position.z * height) + _terrain.transform.position;
                GameObject go = PrefabUtility.InstantiatePrefab(_tree) as GameObject;
                go.transform.position = position;
                //Randomness rotation and scale
                go.transform.rotation = Quaternion.Euler(Random.Range(0f, 360f) * Vector3.up);
                float scale = Random.Range(0.68f, 1);

                //go = Instantiate(_tree, position, Quaternion.Euler(0f, Mathf.Rad2Deg * tree.rotation, 0f), parent.transform) as GameObject;
                go.transform.localScale = scale * Vector3.one;
                if (go.GetComponent<MeshRenderer>() != null)
                {
                    go.GetComponent<MeshRenderer>().enabled = true;
                }
                if (go.GetComponent<Trees>() == null)
                {
                    go.AddComponent<Trees>();
                }
                go.transform.SetParent(parent.transform);

            }
            Debug.Log("Completed: " + (i + 1) + "/" + (selection.Length - 1));
            parent.transform.SetParent(_parent.transform);
        }

    }

    public void ConvertToDictionary()
    {
        //Recreate a new instance each time
        GameObject temp = GameObject.Find("Object Pool");
        if (temp != null)
        {
            DestroyImmediate(temp.gameObject);
        }
        ObjectPool pool = new GameObject("Object Pool").AddComponent<ObjectPool>();
        List<GameObject> objects = new List<GameObject>();
        for (int i = 0; i < selection.Length; i++)
        {
            Terrain _terrain = selection[i];
            TerrainData data = _terrain.terrainData;

            // Create trees
            foreach (TreeInstance tree in data.treeInstances)
            {
                if (tree.prototypeIndex >= data.treePrototypes.Length)
                    continue;

                string prefab_name = data.treePrototypes[tree.prototypeIndex].prefab.name;
                if (prefab_name.ToLower().Contains("bush"))
                {
                    Debug.Log("Skipped bush");
                    continue;
                }
                var _tree = Resources.Load(prefab_name) as GameObject;
                bool found = false;

                if (_tree != null)
                {
                    found = true;
                }

                if (!found)
                {
                    Debug.LogError("Prefab name not found in Resources: " + prefab_name);
                    continue;
                }
                
                if (!objects.Contains(_tree))
                {
                    objects.Add(_tree);
                }
            }
        }
        pool.poolObjects = objects;

        Debug.Log("Chunk Dictionary creation completed! Count: " + pool.poolObjects.Count);
    }
    public void Clear()
    {
        DestroyImmediate(GameObject.Find("Converted Trees Map"));
    }
}
