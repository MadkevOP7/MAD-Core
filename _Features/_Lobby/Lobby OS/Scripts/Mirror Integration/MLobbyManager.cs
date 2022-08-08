//PURPOSE: Manager Player to Lobby Manager connection, local handling of lobbies
//Copyright (c) MADKEV Studio, LLC
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MLobbyManager : NetworkBehaviour
{
    public bool isLocalPlayerBool;
    public LobbyManager lm;
    public Transform playerListContent;
    [Header("Buttons")]
    public Button b_ready;
    public Button b_unready;
    public Button b_start_game;
    public GameObject player_ITEM;

    [SerializeField]
    public static List<GameObject> players = new List<GameObject>();
    public static bool allPlayersReady;
    public static int gameResult; //0 = lose, 1 = win
    //Loading
    [Header("Loading")]
    public MLoadingManager ml;
    [SyncVar]
    public float progress;
    [SyncVar()]
    public bool isLoadingScene;
    [SyncVar]
    public bool isHost;

    #region Initial
    private void OnDestroy()
    {
        players.Clear();
    }
    // Update is called once per frame
    void Update()
    {
        if (!isLocalPlayerBool)
        {
            return;
        }
        if (lm == null)
        {
            if (GameObject.FindGameObjectWithTag("LobbyManager") != null)
            {
                lm = GameObject.FindGameObjectWithTag("LobbyManager").GetComponent<LobbyManager>();
            }
        }
        if (isLoadingScene)
        {
            DisplayLevelLoading();
        }
    
    }
    public void GoToLobby()
    {
        lm.GameToLobby();
    }
   
    public void OnGameOverToLobby(int result)
    {
        gameResult = result;
    }
   
    public void ResetGameStat()
    {
        gameResult = -1;
        isLoadingScene = false;
        
    }
    #endregion
    #region Reference and Start/Stop
    public void GetNeededDataFromLobbyManager()
    {
        lm = GameObject.FindGameObjectWithTag("LobbyManager").GetComponent<LobbyManager>();
        playerListContent = lm.Players_List_Content;
        b_ready = lm.ready_button;
        b_unready = lm.unready_button;
        b_start_game = lm.start_game_button;
        player_ITEM = lm.player_Item;
        ml = lm.ml;
    }
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            Debug.Log("Client Started in Lobby");
            GetNeededDataFromLobbyManager();
        }
    }
    public void CalculateHostStatusForSelf()
    {
        if (isLocalPlayer && isServer)
        {
            isHost = true;
        }
        else
        {
            isHost = false;
        }
    }
    public void SetReady(bool ready)
    {
        GetComponent<PlayerStats>().CMDSetReady(ready);
        if (ready)
        {
            b_unready.gameObject.SetActive(true);
            b_ready.gameObject.SetActive(false);
        }
        else
        {
            b_unready.gameObject.SetActive(false);
            b_ready.gameObject.SetActive(true);
        }
        
    }

    [Command]
    public void HostRefreshCanStartGame()
    {
        //If all players are ready, enable start game button
        CMDListRefresh();
        foreach (GameObject p in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (!p.GetComponent<PlayerStats>().isReady)
            {
                b_start_game.interactable = false;
                return;
            }
        }

        b_start_game.interactable = true;
    }
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        //Refresh player stats
        GetComponent<PlayerStats>().RefreshData();
        if (GameObject.FindGameObjectWithTag("LobbyManager") != null)
        {
            lm = GameObject.FindGameObjectWithTag("LobbyManager").GetComponent<LobbyManager>();
        }
        if (lm != null)
        {
            if (lm.connected_lobby)
            {
                lm.is_multiplayer = true;
            }
            else
            {
                lm.is_multiplayer = false;
            }
        }
        else
        {
            Debug.Log("Lobby Manager not found, is game started without lobby?");
        }
       
        CalculateHostStatusForSelf();
        if (SceneManager.GetActiveScene().name == "Lobby")
        {


            
            GetNeededDataFromLobbyManager();
            InitializeHostGUI();
            //Initialize room list
            CMDListRefresh();
            InitializePlayerGUI();
            Debug.Log("Player successfully connected to server lobby");
            
            
        }
        else
        {
            ResetGameStat();
        }
        
    }


    public override void OnStopClient()
    {
        //CMDCallClientRefreshList(); Removed for now to prevent error;

        base.OnStopClient();
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            if (isServer && isLocalPlayer) //Is host
            {
                Debug.Log("Server started in Lobby");
                
            }
        }
        
    }

    #endregion
    public void ManagerBridgeRefresh() //When NetworkManager knows a client disconnects, calls this bridge to refresh lobby
    {
        Debug.Log("Refreshing List and button state because a client has disconnected!");
        CMDListRefresh();
        StartCoroutine(DelayRefreshAfterClientDisconnect());
    }

    IEnumerator DelayRefreshAfterClientDisconnect()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();
        HostRefreshCanStartGame();
    }

    void ClearPlayerList() //Server
    {
        //Race prevention
        if (playerListContent == null)
        {
            GetNeededDataFromLobbyManager();
        }
        //Clear player list
        foreach (PlayerItem t in playerListContent.GetComponentsInChildren<PlayerItem>())
        {
            NetworkServer.Destroy(t.gameObject);
        }
    }

    [Command(requiresAuthority =false)]
    public void CMDListRefresh() //Calls RPC to let each client do local refresh
    {
        RPCListRefresh();
    }

    [ClientRpc]
    public void RPCListRefresh()
    {
        //Local refresh
        //Clear List
        ClearPlayerList();
        foreach(GameObject p in GameObject.FindGameObjectsWithTag("Player"))
        {
            PlayerItem temp = Instantiate(player_ITEM, playerListContent).GetComponent<PlayerItem>();
            PlayerStats s = p.GetComponent<PlayerStats>();
            Debug.Log(s.isReady);
            //Handles ready status as well
            temp.InitializePlayerItem(s.p_name, s.rank, s.isHost, s.isReady);
            
        }
    }


    #region Loading Screen/Host start game
    //Add Startgame Function to MLobby
    public void InitializeHostGUI()
    {
        if (isServer && isLocalPlayer)
        {
            //Add Button Click event
            b_start_game.GetComponent<Button>().onClick.AddListener(delegate { HostStartGame(); });
            b_start_game.interactable = false; 
            Debug.Log("Server: Enabled Start Game Permission");
        }
        
    }

    public void InitializePlayerGUI()
    {
        GetComponent<PlayerStats>().CMDSetReady(false);
        b_start_game.interactable = false; //Prevents non-hosting players from clicking the button
        b_ready.GetComponent<Button>().onClick.AddListener(delegate { SetReady(true); });
        b_unready.GetComponent<Button>().onClick.AddListener(delegate { SetReady(false); });
    }
    public void HostStartGame()
    {
        if (isServer && isLocalPlayer)
        {
            RPCEnableLoadingScreen();
            isLoadingScene = true;
            lm.StartGame("1"); //1 for now, change later when new levels added
        }
    }

    [ClientRpc]
    public void RPCEnableLoadingScreen()
    {
        ml.gameObject.SetActive(true);
    }
    public void DisplayLevelLoading()
    {
       
        progress = lm.GetNetworkManagerSceneChangeProgress();
        
        ClientUpdateLoadingProgress();
    }


    public void ClientUpdateLoadingProgress()
    {
        Debug.Log("Progress ");

        ml.UpdateProgressUI(progress);
    }
    #endregion

}
