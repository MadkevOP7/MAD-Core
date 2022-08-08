using UnityEngine;
using UnityEditor;

public class EditorLightControl : MonoBehaviour
{
    [MenuItem("Window/Dim Lights")]
    private static void FindProblemMesh()
    {
        foreach(var light in FindObjectsOfType<Light>())
        {
           // light.intensity -= .18f;
            // light.range -= .86f;
            //Debug.Log(light.gameObject.name + "Reduced");
           
        }
        Debug.Log("Reduced!");
    }
}
