using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.UI;
using System;

public class MLoadingManager : MonoBehaviour
{
    
    
    public Slider progress_bar;
   
   
 
    public void UpdateProgressUI(float v)
    {
        progress_bar.value = v;
    }

    
}
