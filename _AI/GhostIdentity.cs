using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Use for detection, etc
public class GhostIdentity : MonoBehaviour
{
    [Header("View")]
    public string m_name;
    [Header("AI Settings")]
    [Range(1, 5)]
    public int level; //for storing dangerousness 1 = weakest 5 to most dangerous
    [Header("Response Settings")]
    public bool respond_emf = true;
    public bool respond_infrared = true;

}
