using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using EpicTransport;
using kcp2k;
using UnityEngine.UI;
using Epic.OnlineServices.Lobby;
using UnityEngine.SceneManagement;

public class LobbyReferenceContainer : MonoBehaviour
{
    //Handles EOS Transport
    [Header("Network Settings")]
    public Interactable i_in;


    [Header("Server List Contents")]
    public Transform Servers_List_Content;
    [Header("Server Details Contents")]
    public Transform Players_List_Content;
    public GameObject player_Item;
    public Image public_switch_on;
    public Image public_switch_off;
    public Button ready_button;
    public Button unready_button;

    public Text invite_code;

    [Header("Server Creation Settings")]
    public Text server_name_text;
    public Dropdown server_publicity_dropdown;
    public Button create_server_button;



    [Header("Main System Windows")]
    public GameObject LOBBY_WINDOW;
    public GameObject MENU_WINDOW;
    [Header("Lobby Windows")]
    public GameObject server_details_window;
    public GameObject lobby_view_window;
    public GameObject create_server_window;

    [Header("Result Windows")]
    public GameObject RESULT_WINDOW;
    public Button Winner_Continue_Button;
    public GameObject result_item;
    LobbyManager lm;

    [Header("Loading Screen")]
    public MLoadingManager ml;
    public Button start_game_button;
    private void Awake()
    {
        lm = GameObject.FindGameObjectWithTag("LobbyManager").GetComponent<LobbyManager>();
        lm.i_in = i_in;
        
        lm.Servers_List_Content = Servers_List_Content;
        lm.Players_List_Content = Players_List_Content;
        lm.player_Item = player_Item;
        lm.public_switch_on = public_switch_on;
        lm.public_switch_off = public_switch_off;
        lm.ready_button = ready_button;
        lm.unready_button = unready_button;
        lm.invite_code = invite_code;
        lm.server_name_text = server_name_text;
        lm.server_publicity_dropdown = server_publicity_dropdown;
        lm.create_server_button = create_server_button;
        lm.LOBBY_WINDOW = LOBBY_WINDOW;
        lm.MENU_WINDOW = MENU_WINDOW;
        lm.server_details_window = server_details_window;
        lm.lobby_view_window = lobby_view_window;
        lm.create_server_window = create_server_window;
        lm.RESULT_WINDOW = RESULT_WINDOW;
        lm.Winner_Continue_Button = Winner_Continue_Button;
        lm.ml = ml;
        lm.start_game_button = start_game_button;
    }

    //Wrapper functions
    public void SetMultiplayer()
    {
        lm.SetMultiplayer();
    }

    public void RefreshServerList()
    {
        lm.RefreshServerList();
    }

   

    public void GUILeaveLobby()
    {
        lm.GUILeaveLobby();
    }
    public void ButtonCreateLobby()
    {
        lm.ButtonCreateLobby();
        
    }
}
