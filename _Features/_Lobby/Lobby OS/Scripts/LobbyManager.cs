//PURPOSE: Lobbies interface handling, EOSLobby connection
//Copyright (c) MADKEV Studio, LLC
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using EpicTransport;
using kcp2k;
using UnityEngine.UI;
using Epic.OnlineServices.Lobby;
using UnityEngine.SceneManagement;
public class LobbyManager : EOSLobby
{
    //Handles EOS Transport
    [Header("Network Settings")]
    public Interactable i_in;
    public Manager manager;
    public EosTransport EOSTransport;
    public EOSSDKComponent EOSSDK;
    public KcpTransport KCPTransport;


    [Header("Camera Settings")]
    public Camera mainCam;
    public GameObject lobbyCamHolder;
    private GameObject lobbyCam;
    [SerializeField]
    public bool is_focused;
    [SerializeField]
    public bool camera_moving = false;
    public Vector3 camera_focus_pos = new Vector3(2.701f, 1.488f, 5.481f);
    public Quaternion camera_focus_rot = Quaternion.Euler(0, 90, 0);

    [Header("Audio")]
    public AudioClip a_swoosh;

    [Header("Server List Contents")]
    public Transform Servers_List_Content;
    public GameObject server_Item;
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
    #region Lobby Settings
    private string lobbyName = "Lobby Test";
    private bool showLobbyList = false;
    private bool showPlayerList = false;

    [Header("Main System Windows")]
    public GameObject LOBBY_WINDOW;
    public GameObject MENU_WINDOW;
    [Header("Lobby Windows")]
    public GameObject server_details_window;
    public GameObject lobby_view_window;
    public GameObject create_server_window;
    private List<LobbyDetails> foundLobbies = new List<LobbyDetails>();
    private List<Attribute> lobbyData = new List<Attribute>();

    [Header("Result Windows")]
    public GameObject RESULT_WINDOW;
    public Button Winner_Continue_Button;
    public GameStats game_stats;

    [Header("Loading Screen")]
    public MLoadingManager ml;
    public Button start_game_button;
    #endregion

    //CONTROL//
    public bool isInGame;
    public bool backFromGame;
    static LobbyManager instance; //Singleton
    public bool is_multiplayer;
    public bool connected_lobby;
    void Awake()
    {
        //Singleton method
        if (instance == null)
        {
            //First run, set the instance
            instance = this;
            DontDestroyOnLoad(gameObject);

        }
        else if (instance != this)
        {
            //Instance is not the same as the one we have new one
            Destroy(gameObject);
            
        }
    }
    // Start is called before the first frame update
    public override void Start()
    {
        base.Start();
        SceneManager.sceneLoaded += OnSceneLoaded;
        LocalLeaveLobby();
        SetSingleplayer(); //Singleplayer Lobby
        manager.StartHost();
    }
 
    private void OnDisable()
    {
        LocalLeaveLobby();
    }
    //Determine if is in game or can use lobby
    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        is_focused = false;
        camera_moving = false;
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            isInGame = false;
            if (backFromGame)
            {
                InitializeGameResult();
                InitiliazeOSBackFromGame();
               
            }
        }
        else
        {
            isInGame = true;
        }
    }
    IEnumerator DelayNetworkManagerGetter()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(1);
        
    }
    // Update is called once per frame
    void Update()
    {
        
        if (!isInGame)
        {
            if (i_in.selected && !is_focused)
            {
                if (Input.GetKeyDown(KeyCode.E)) //Input
                {
                    OS_Interact();
                }
            }
            if (is_focused)
            {
                if (Input.GetKeyDown(KeyCode.Tab)) //Input
                {
                    OS_Interact();
                }
            }
        }
        
    }
    public void GameToLobby() //game over, go back to lobby
    {
        manager.LoadGame("Lobby");
    }

    
    public void InitiliazeOSBackFromGame() //Initialize lobby os when user just returned from game
    {
        //Get Game Stats Reference
        if(GameObject.FindGameObjectWithTag("Game Stats") != null)
        {
            game_stats = GameObject.FindGameObjectWithTag("Game Stats").GetComponent<GameStats>();
        }
        MENU_WINDOW.SetActive(false);
        LOBBY_WINDOW.SetActive(true);
        server_details_window.SetActive(true);
        lobby_view_window.SetActive(false);
        create_server_window.SetActive(false);
        RESULT_WINDOW.SetActive(true);
        backFromGame = false;
        GUIJoinLobby();
        RefreshLobbyDetails();
    }

    //Handling XP increase, rewards, etc
    public void InitializeGameResult()
    {
        //Get Game Stats Reference
        if (GameObject.FindGameObjectWithTag("Game Stats") != null)
        {
            game_stats = GameObject.FindGameObjectWithTag("Game Stats").GetComponent<GameStats>();
        }
        game_stats.DisplayResult();

    }
    public void SetSingleplayer()
    {
        //Initiliaze singleplayer lobby
        //LocalLeaveLobby();
       
        EOSTransport.enabled = false;
        EOSSDK.enabled = false;
        //EOSOBJ.SetActive(false); Breaks Transport for some reason
        KCPTransport.enabled = true;
        manager.maxConnections = 1;
        manager.ChangeToKCP();
        NetworkServer.dontListen = true;
        manager.StartHost();
        is_multiplayer = false;
        connected_lobby = false;
    }

    public void SetMultiplayer()
    {
        //Initiliaze multiplayer lobby
        KCPTransport.enabled = false;

        
        EOSTransport.enabled = true; 
        EOSSDK.enabled = true;
        StartCoroutine(SetMultiplayerFix());

    }
    IEnumerator SetMultiplayerFix() //fix player gets destroyed
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForEndOfFrame();
        manager.maxConnections = 100;
        manager.ChangeToEpic();
        yield return new WaitForFixedUpdate();
        yield return new WaitForEndOfFrame();
        NetworkServer.dontListen = false;
        yield return new WaitForFixedUpdate();
        yield return new WaitForEndOfFrame();
        LocalStartLobby();
        //UI Update
        MENU_WINDOW.SetActive(false);
        LOBBY_WINDOW.SetActive(true);
    }

    #region Lobby Camera Focus
    public void OS_Interact()
    {
        if (!is_focused&&!camera_moving)
        {
            FocusCamera();
            camera_moving = true;
        }
        if(is_focused&&!camera_moving)
        {
            UnFocusCamera();
            camera_moving = true;
        }
    }

    public void FocusCamera()
    {
        PreventDuplicateLobbyCam(); //Deletes leftover lobby cameras if player left lobby

        foreach(GameObject g in GameObject.FindGameObjectsWithTag("Player"))
        {
            //Use localplayer not localplayerBool here since it's lobby
            if (g.GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                g.GetComponent<InteractionDetection>().DisablePlayerUI();
                mainCam = g.GetComponent<Player>().mainCamera;
                break;
            }
        }
        //GameObject.FindGameObjectWithTag("Player")?.GetComponent<InteractionDetection>().DisablePlayerUI();
        lobbyCam = Instantiate(lobbyCamHolder);
        //Store original data

        lobbyCam.transform.position = mainCam.transform.position;
        lobbyCam.transform.rotation = mainCam.transform.rotation;
        StartCoroutine(MoveCameraFocus());
  
    }
    
    public void PreventDuplicateLobbyCam()
    {
        foreach(GameObject g in GameObject.FindGameObjectsWithTag("LobbyCam"))
        {
            Destroy(g);
        }
    }
    public void UnFocusCamera()
    {
        foreach (GameObject g in GameObject.FindGameObjectsWithTag("Player"))
        {
            //Use localplayer not localplayerBool here since it's lobby
            if (g.GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                g.GetComponent<InteractionDetection>().EnablePlayerUI();
                
                break;
            }
        }
        //GameObject.FindGameObjectWithTag("Player")?.GetComponent<InteractionDetection>().EnablePlayerUI();
        StartCoroutine(MoveCameraBack());
    }

    IEnumerator MoveCameraFocus()
    {
        GetComponent<AudioSource>().PlayOneShot(a_swoosh);
        while (Vector3.Distance(lobbyCam.transform.position, camera_focus_pos) > 0.01f || Quaternion.Angle(lobbyCam.transform.rotation, camera_focus_rot) > 0.01f)
        {
            lobbyCam.transform.position = Vector3.Lerp(lobbyCam.transform.position, camera_focus_pos, Time.deltaTime * 6f);
            lobbyCam.transform.rotation = Quaternion.Slerp(lobbyCam.transform.rotation, camera_focus_rot, Time.deltaTime * 6f);
            yield return null;
        }
        
        camera_moving = false;
        is_focused = true;
    }

    IEnumerator MoveCameraBack()
    {
        GetComponent<AudioSource>().PlayOneShot(a_swoosh);
        float speed = 5f;
        Vector3 m_p = mainCam.transform.position;
        Quaternion m_r = Quaternion.Euler(mainCam.transform.rotation.x, mainCam.transform.rotation.y, mainCam.transform.rotation.z);
        while (Vector3.Distance(lobbyCam.transform.position, mainCam.transform.position) > 1 || Quaternion.Angle(lobbyCam.transform.rotation, mainCam.transform.rotation) >1)
        {
            lobbyCam.transform.position = Vector3.Lerp(lobbyCam.transform.position, mainCam.transform.position, Time.deltaTime * speed);
            lobbyCam.transform.rotation = Quaternion.Slerp(lobbyCam.transform.rotation, mainCam.transform.rotation, Time.deltaTime * speed);
            speed *= 1.5f;
            yield return null;
        }

        Destroy(lobbyCam);
        
        is_focused = false;
        camera_moving = false;
    }

    #endregion

    #region Lobby Events
    //when the lobby is successfully created, start the host
    private void OnCreateLobbySuccess(List<Attribute> attributes)
    {

        Debug.Log("lobby created");
        is_focused = false;
        camera_moving = false;
        lobbyData = attributes;
        showPlayerList = true;
        showLobbyList = false;
        Debug.Log("Processed lobby data");
        HostSwitchToJoin();
        Debug.Log("Request starting manager Host");
        HostSwitchToJoin();
        manager.StartHost();
        Debug.Log("Manager host started");
        RefreshLobbyDetails();
        
    }

    public void RefreshLobbyDetails()
    {
        Debug.Log("requesting refresh lobby details");

        GUIJoinLobby();
        //Set Invite code, publicity, etc
        Attribute lobby_publicity_attribute = new Attribute();
        ConnectedLobbyDetails.CopyAttributeByKey(new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = AttributeKeys[1] }, out lobby_publicity_attribute);

        if (lobby_publicity_attribute.Data.Value.AsBool == true)
        {
            //Is public server
            public_switch_on.gameObject.SetActive(true);
            public_switch_off.gameObject.SetActive(false);
        }
        else
        {
            //Is public server
            public_switch_off.gameObject.SetActive(true);
            public_switch_on.gameObject.SetActive(false);
        }

        //Update Invite Code
        Attribute lobby_invite_code = new Attribute();
        ConnectedLobbyDetails.CopyAttributeByKey(new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = AttributeKeys[2] }, out lobby_invite_code);
        invite_code.text = lobby_invite_code.Data.Value.AsUtf8;

        RefreshLobbyDetailsPlayerList(); //Moved to MLobbyManager for user add lobby sync
    }

    public void RefreshLobbyDetailsPlayerList()
    {
        /*
        //Clear player list
        foreach(PlayerItem t in Players_List_Content.GetComponentsInChildren<PlayerItem>())
        {
            Destroy(t.gameObject);
        }
        //Fill List
        for(uint i=0; i<ConnectedLobbyDetails.GetMemberCount(new LobbyDetailsGetMemberCountOptions {}); i++)
        {
            PlayerItem p = Instantiate(player_Item, Players_List_Content).GetComponent<PlayerItem>();
            ConnectedLobbyDetails.GetMemberByIndex(new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i });
            p.InitializePlayerItem("kevin", 2, true);
      
        }
        */
    }
    //when the user joined the lobby successfully, set network address and connect
    private void OnJoinLobbySuccess(List<Attribute> attributes)
    {
        connected_lobby = true;
        is_focused = false;
        camera_moving = false;
        RefreshLobbyDetails();
        lobbyData = attributes;
        showPlayerList = true;
        showLobbyList = false;

        
        manager.networkAddress = attributes.Find((x) => x.Data.Key == hostAddressKey).Data.Value.AsUtf8;
        manager.StartClient();
    }

    //callback for FindLobbiesSucceeded
    private void OnFindLobbiesSuccess(List<LobbyDetails> lobbiesFound)
    {
        foundLobbies = lobbiesFound;
        showPlayerList = false;
        showLobbyList = true;
    }

    //when the lobby was left successfully, stop the host/client
    private void OnLeaveLobbySuccess()
    {
        Debug.Log("Left lobby");
        /*
        manager.StopHost();
        manager.StopClient();
        Debug.Log("Successfully left lobby");
        */
    }

    #endregion
    #region Lobby Start/End
    public void LocalStartLobby()
    {
        //subscribe to events
        CreateLobbySucceeded += OnCreateLobbySuccess;
        JoinLobbySucceeded += OnJoinLobbySuccess;
        FindLobbiesSucceeded += OnFindLobbiesSuccess;
        LeaveLobbySucceeded += OnLeaveLobbySuccess;
    }

    public void LocalLeaveLobby()
    {
        //unsubscribe from events
        
        CreateLobbySucceeded -= OnCreateLobbySuccess;
        JoinLobbySucceeded -= OnJoinLobbySuccess;
        FindLobbiesSucceeded -= OnFindLobbiesSuccess;
        LeaveLobbySucceeded -= OnLeaveLobbySuccess;
        
    }
    #endregion
    #region Lobby Creation/Search features
    public void ButtonCreateLobby()
    {
        LeaveLobby();
        //manager.StopHost();

        //Add lobby data ("name, publicity, inviteCode, etc")
        //Genearte 6-digit invite code
        int r = Random.Range(0, 999999);
        string code = r.ToString();
        CreateLobby(4, server_publicity_dropdown.value == 0 ? LobbyPermissionLevel.Publicadvertised : LobbyPermissionLevel.Joinviapresence, false, new AttributeData[] { new AttributeData { Key = AttributeKeys[0], Value = server_name_text.text != "" ? server_name_text.text:"New Lobby" },
        new AttributeData{Key = AttributeKeys[1], Value=server_publicity_dropdown.value == 0 ? true : false }, //Lobby publicity
        new AttributeData{Key = AttributeKeys[2], Value=code}, //Invite code
        }); //With key and attribute index we can pass in information
        Debug.Log("Lobby Creation Request Successful, await final");
    }

    public void GUIJoinLobby() //Displays lobby details
    {
        //Enable and disable windows
        server_details_window.SetActive(true);
        lobby_view_window.SetActive(false);
        create_server_window.SetActive(false);
       
     
   
    }

    #endregion

    #region Server Loading/Main Lobby Server List
    public void DisplayServers()
    {
        FindLobbies();
        //Draw lobbies list
        foreach(LobbyDetails lobby in foundLobbies)
        {
            //get lobby name
            Attribute lobbyNameAttribute = new Attribute();
            lobby.CopyAttributeByKey(new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = AttributeKeys[0] }, out lobbyNameAttribute);
            ServerItem s = Instantiate(server_Item, Servers_List_Content).GetComponent<ServerItem>();
            s.SetUILobbyname(lobbyNameAttribute.Data.Value.AsUtf8);
            s.SetUILobbySize(lobby.GetMemberCount(new LobbyDetailsGetMemberCountOptions { }).ToString() + "/4");
            //Assign join lobby feature (UI click) of each server item
            s.GetComponent<Button>().onClick.AddListener(delegate { HostSwitchToJoin(); });
            s.GetComponent<Button>().onClick.AddListener(delegate { JoinLobby(lobby, AttributeKeys); });
            
        }
    }
    public void HostSwitchToJoin()
    {
        Debug.Log("Request to switch host, await final");
        manager.StopHost();
        Debug.Log("Host stopped");
        manager.StopClient();
        Debug.Log("host switch");
    }
    public void RefreshServerList()
    {
        //clear list
        foreach(ServerItem g in Servers_List_Content.transform.GetComponentsInChildren<ServerItem>())
        {
            Destroy(g.gameObject);
        }
        DisplayServers();
    }
    #endregion

    #region Lobby Details Window
    public void GUILeaveLobby()
    {
        PreventDuplicateLobbyCam();
        connected_lobby = false;
        LeaveLobby();
        manager.StopClient();
        manager.StopHost();
        //Process GUI
        lobby_view_window.SetActive(true);
        server_details_window.SetActive(false);
        create_server_window.SetActive(false);

        manager.autoCreatePlayer = true;
        LOBBY_WINDOW.SetActive(false);
        MENU_WINDOW.SetActive(true);
        SetSingleplayer();
        is_focused = false;
        camera_moving = false;
       

    }
    #endregion

    #region Start Game
    public void StartGame(string level)
    {

        //Call from MLobbyManager
        manager.LoadGame(level);
    }

    //Get NetworkManager Scene Change Progress
    public float GetNetworkManagerSceneChangeProgress()
    {
        return manager.ReturnSceneChangeProgress();
    }
    #endregion
}
