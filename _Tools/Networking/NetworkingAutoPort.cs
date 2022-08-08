using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using kcp2k;
using System;

public class NetworkingAutoPort : MonoBehaviour
{
    public KcpTransport kcp;
    private void Start()
    {
        kcp.Port = Convert.ToUInt16((UnityEngine.Random.Range(7000, 8000)));
    }
}
