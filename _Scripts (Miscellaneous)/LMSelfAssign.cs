using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LMSelfAssign : MonoBehaviour
{
    public LobbyManager lm;
    private void Awake()
    {
        lm = GameObject.FindGameObjectWithTag("LobbyManager").GetComponent<LobbyManager>();
        lm.i_in = this.GetComponent<Interactable>();
    }
}
