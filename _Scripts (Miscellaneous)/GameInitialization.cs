using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
public class GameInitialization : MonoBehaviour
{
    public PostProcessVolume p;
    private void Awake()
    {
        #region Post Processing Fix
        SetGrainSizeDifference();
        #endregion    

    }

    void SetGrainSizeDifference()
    {

        int height = Screen.currentResolution.height;
        //Debug.Log("Resolution Height: " + height);
        float val = height / 2160f;
        //Debug.Log("Grain val is: " + val);
        if (p == null)
        {
            p = GameObject.FindGameObjectWithTag("PostProcessing").GetComponent<PostProcessVolume>();
        }
        p.profile.GetSetting<Grain>().size.Override(val);
        //Debug.Log("Grain size done!");
    }
}
