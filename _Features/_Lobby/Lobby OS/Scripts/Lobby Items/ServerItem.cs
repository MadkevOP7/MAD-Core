using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ServerItem : MonoBehaviour
{
    [Header("References")]
    public Text server_name;
    public Text server_size;

    public void SetUILobbySize(string input)
    {
        server_size.text = input;
    }

    public void SetUILobbyname(string input)
    {
        server_name.text = input;
    }
}
