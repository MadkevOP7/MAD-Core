using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class netObjectDisable : NetworkBehaviour
{
    public GameObject[] objDisable;
    // Start is called before the first frame update
    void Start()
    {
        if (!isLocalPlayer)
        {
            foreach (var c in objDisable)
            {
                c.SetActive(false);
            }
        }
        
    }

   
}
