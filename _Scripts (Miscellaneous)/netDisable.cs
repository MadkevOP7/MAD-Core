using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class netDisable : NetworkBehaviour
{
    public List<PlayMakerFSM> ignores = new List<PlayMakerFSM>();
    // Start is called before the first frame update
    void Start()
    {
        if (!isLocalPlayer)
        {
            DisableAllFSM();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void DisableAllFSM()
    {
        foreach (var component in GetComponents<PlayMakerFSM>())
        {
            if (ignores.Contains(component) == false)
            {
                component.enabled = false;
            }
        }
        foreach (var component in GetComponentsInChildren<PlayMakerFSM>())
        {
            if (ignores.Contains(component) == false)
            {
                component.enabled = false;

            }
        }
    }
}
