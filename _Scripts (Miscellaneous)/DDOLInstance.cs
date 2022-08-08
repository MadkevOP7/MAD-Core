using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DDOLInstance : MonoBehaviour
{
    private static GameObject instance;
    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);

        if (instance == null)
        {
            instance = this.gameObject;
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
}
