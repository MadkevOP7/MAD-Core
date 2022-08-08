using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LobbyListOnEnableRefresh : MonoBehaviour
{
    [SerializeField]
    public LobbyManager lm;

    private void OnEnable()
    {
        lm.RefreshServerList();
    }
}
