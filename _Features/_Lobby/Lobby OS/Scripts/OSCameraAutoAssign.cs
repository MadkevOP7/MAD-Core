using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OSCameraAutoAssign : MonoBehaviour
{
    private void Update()
    {
        if (this.GetComponent<Canvas>().worldCamera == null)
        {
            this.GetComponent<Canvas>().worldCamera = GameObject.FindGameObjectWithTag("LobbyCam")?.GetComponent<Camera>();
        }
    }
}
