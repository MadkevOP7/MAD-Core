using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dissonance;
public class VoiceChatHelper : MonoBehaviour
{
    [Header("Components")]
    public VoiceBroadcastTrigger vt;
    //Enable Disable Voice Chat
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            
            vt.ToggleMute();
        }
    }
}
