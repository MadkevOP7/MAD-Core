using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostCamHelper : MonoBehaviour
{
    //Disabled for now for bug fix
    public Camera humanCam;
    private void OnEnable()
    {
        //humanCam.gameObject.SetActive(false);
    }
}
