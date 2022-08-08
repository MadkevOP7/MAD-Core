using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelCamera : MonoBehaviour
{
    public GameObject joingingCanvas;
    private void Awake()
    {
        joingingCanvas.SetActive(true);
    }
}
