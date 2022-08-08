using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Chunk Dictionary", menuName = "Chunk Dictionary")]
public class ChunkDictionary : ScriptableObject
{
    public Dictionary<string, GameObject> dictionary;

    public void FillChunkDictionary(List<GameObject> list)
    {
        
        dictionary = new Dictionary<string, GameObject>(list.Count);

        foreach (GameObject obj in list)
        {
            dictionary.Add(obj.name, obj);
        }
    }

}
 
