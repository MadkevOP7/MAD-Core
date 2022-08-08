using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class SpeedHelper : NetworkBehaviour
{
    [SyncVar]
    public float speed;

    public bool isLocalPlayerBool;
    public CharacterController cc;
   

    public override void OnStartLocalPlayer()
    {
        isLocalPlayerBool = true;
        if (cc == null&&GetComponent<CharacterController>() != null)
        {
            cc = GetComponent<CharacterController>();
        }
     
    }

    // Update is called once per frame
    void Update()
    {
        if (!isLocalPlayerBool)
        {
            return;
        }
        if (cc != null)
        {
            CMDSetSpeed(cc.velocity.magnitude);
        }
    }


    [Command(requiresAuthority =false)]
    public void CMDSetSpeed(float s)
    {
        speed = s;
    }
}
