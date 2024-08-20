using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class ExitMachine : NetworkBehaviour
{
    [Header("Settings")]
    public AudioSource audioSource;
    public AudioClip onInteractClip;
    //For now, everyone can interact with Exit Machine and end the session
    public void Interact()
    {
        CMDInteract();
    }

    [Command(requiresAuthority = false)]
    public void CMDInteract()
    {
        RPCPlayInteractionAudio();
        GameManager.instance.ServerGameOver(true);
    }

    [ClientRpc(includeOwner =  true)]
    void RPCPlayInteractionAudio()
    {
        audioSource.PlayOneShot(onInteractClip);
    }
}
