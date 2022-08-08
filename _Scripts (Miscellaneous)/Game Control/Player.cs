using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using TMPro;
using System.Linq;
using UnityEngine.SceneManagement;
using PlayMaker;
//using GPUInstancer;
public class Player : NetworkBehaviour
{
    #region Settings
    [Header("Game Configuration")]
    [SyncVar]
    public int connectedPlayers;
    //Player type
    [SyncVar(hook =nameof(HookKillerTagChange))]
    public bool isKiller;
    [ShowInInspector()]
    public static int killer_amount = 1;

    [Header("Player Configuration")]
    [SyncVar]
    public int sanity;
   
    public Character currentCharacter;
    #region UI Settings
    [Header("UI")]
    public Camera mainCamera;
    public GameObject player_UX;
    public TextMeshProUGUI timer_text;
    public TextMeshProUGUI playerState;
    [Header("Controls")]
    [Space()]
    public Color p_red;
    public Color p_green;
    public Color p_grey;
    public string p_trail = "On Trial";
    public string p_killer = "Observer";
    public string p_survivor = "Gatekeeper";
    public string p_win = "Victory";
    public string p_lose = "Eliminated";
  
    [Header("Game Manager")]
    [SyncVar(hook =nameof(EnableDeathRagdoll))]
    public bool is_alive = true;
    public float deathDuration = 2f; //Lower than 2 may cut off death audio before it finishes playing
    private bool final_death; // when true cuts to ragdoll
    public GameObject gm;
    private GameManager game_manager;
    public Timer time;

    [Header("Ghost Integration")]
    [SyncVar]
    public bool isGhost; //killer only



    public static List<GameObject> players = new List<GameObject>();
    public static List<GameObject> survivors = new List<GameObject>();
    public static List<GameObject> killers = new List<GameObject>();
    public bool isLocalPlayerBool;
    #endregion

    //Private status
    private bool isServerStarted;
    private bool isClientStarted;
    #endregion
    [Header("Ragdoll")]
    public Animator animator;
    public Animator camera_animator;
    public Transform AnimatedBody;
    //Debug Death Types
    [Header("Death")]
    private GameObject lockHeadCamHolder;
    private bool deathCamZoom;
    private bool finalDeathDone;
    public GameObject deathGhost;
    public Transform head;
    private bool lockheadDeath;
    private Transform lockHeadPos;
    private Transform lockHeadLook;
    public AudioSource deathAudioSource;
    [Header("Game Stats (Local)")]
    [HideInInspector]
    public GameStats gameStats;
    public GameObject gameStatsPrefab;
    private bool finalizedGameOver;
    #region Initialization
    //TEMP
    private bool self_healing;
    public void DebugList()
    {
        if (isServer && isLocalPlayerBool)
        {
            Debug.Log("[List] Players:");
            for (int i = 0; i < players.Count; i++)
            {
                Debug.Log("Players[" + i + "]: " + players[i].name);
            }

            Debug.Log("[List] Survivors:");
            for (int i = 0; i < survivors.Count; i++)
            {
                Debug.Log("Survivors[" + i + "]: " + survivors[i].name);
            }

            Debug.Log("[List] Killers:");
            for (int i = 0; i < killers.Count; i++)
            {
                Debug.Log("Killers[" + i + "]: " + killers[i].name);
            }
        }

    }
    public override void OnStartServer()
    {
        if (!isServerStarted)
        {
            base.OnStartServer();
            if (SceneManager.GetActiveScene().name != "Lobby"&&SceneManager.GetActiveScene().name != "Forest")
            {
                if (isServer && isLocalPlayer)
                {
                    Debug.Log("Is Host Server...");
                    StartCoroutine(TimedServerInitializeCaller());
                }
            }
            isServerStarted = true;
        }
        
        
    }
    public override void OnStartLocalPlayer()
    {
        //GPU Instancer initialize camera
        //GPUInstancerAPI.SetCamera(mainCamera);
        if (!isLocalPlayerBool)
        {
            base.OnStartLocalPlayer();
            CMDSetRagdollState(false);
            gm = GameObject.FindGameObjectWithTag("Game Manager");
            
            //Enable Player Functions (Audio listenr, etc)
            player_UX.SetActive(false); //UX Handle
            mainCamera.GetComponent<AudioListener>().enabled = true;
            if (isLocalPlayer)
            {
                //Remove temp objects such as level camera, etc
                foreach (GameObject i in GameObject.FindGameObjectsWithTag("Temp"))
                {
                    Destroy(i);
                }
            }
            if (SceneManager.GetActiveScene().name == "Forest")
            {
                //Camera clear flag
                mainCamera.farClipPlane = 100;
                //Fog back clear flag color
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = Color.white;
                game_manager = GameObject.FindGameObjectWithTag("Game Manager").GetComponent<GameManager>();
                AddPlayer(this.gameObject); //Presever this for keeping track of players on server
                SetSurvivor(); //New change to initialize self as survivor, killer fully AI
            }
            else if (SceneManager.GetActiveScene().name != "Lobby")
            {
                Debug.Log("local Game Start");
                game_manager = GameObject.FindGameObjectWithTag("Game Manager").GetComponent<GameManager>();
                //Remove Local Gamestats once new game starts
                foreach (GameObject s in GameObject.FindGameObjectsWithTag("Game Stats"))
                {
                    Destroy(s);
                }
                if (gameStats != null)
                {
                    Destroy(gameStats.gameObject);
                }
                //Allocate a new game stats instance
                gameStats = Instantiate(gameStatsPrefab).GetComponent<GameStats>();
                ResetLists(); //Game mode reset
                player_UX.SetActive(true); //UX Handle On Game Tag
                playerState.text = p_trail;
                playerState.color = p_grey;

                time = gm?.GetComponent<Timer>();
                AddPlayer(this.gameObject); //Presever this for keeping track of players on server
                SetSurvivor(); //New change to initialize self as survivor, killer fully AI

            }
            isLocalPlayerBool = true;
        }

    }

    public override void OnStartClient()
    {
        if (!isClientStarted)
        {
            base.OnStartClient();
            gm = GameObject.FindGameObjectWithTag("Game Manager");
            if (SceneManager.GetActiveScene().name == "Forest")
            {
                //Add to Object pool transform to be tracked
                //ObjectPool.Instance.players.Add(transform);
            }
            isClientStarted = true;
        }
        
    }
    IEnumerator TimedServerInitializeCaller()
    {
        yield return new WaitForSeconds(12);
        InitializePlayerToServer();
    }

    //Server Initialization//
    void InitializePlayerToServer()
    {
        Debug.Log("Initializing players to server...");
        time.StartTimer();
        //DetermineKiller(killer_amount);
        //FillSurvivors();
        if (gm == null)
        {
            gm = GameObject.FindGameObjectWithTag("Game Manager");
        }
        //gm.GetComponent<HostMachineManager>().CMDSetDestroyCount(survivors.Count);
        RefreshConnectedPlayerCount();
        RefreshSurvivorsState();
    }

    public void DetermineKiller(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            Debug.Log("[Selector] Players count: " + players.Count);
            int random = UnityEngine.Random.Range(0, players.Count);
            Debug.Log("Random Selected Index: " + random);
            if (killers.Contains(players[random]) == false)
            {
                Debug.Log("Server sending request to add killer: " + players[random].name);
                ServerAddKiller(players[random]);
            }

        }
       
    }

    public void ResetLists()
    {
        players.Clear();
        killers.Clear();
        survivors.Clear();
    }

    //Server side adding/removing
    void ServerAddSurvivor(GameObject g)
    {
        if (killers.Contains(g))
        {
            killers.Remove(g);
        }
        if (!survivors.Contains(g))
        {
            survivors.Add(g);
        }
        g.gameObject.GetComponent<Player>().SetSurvivor();
        DebugList();
    }

    
    void ServerAddKiller(GameObject g)
    {
        Debug.Log("Received request to add chaser: " + g.name);
        if (survivors.Contains(g))
        {
            survivors.Remove(g);
        }
        if (!killers.Contains(g))
        {
            killers.Add(g);
        }
        g.gameObject.GetComponent<Player>().SetKiller();
        DebugList();

    } 
    
    //Client side adding/removing
    [Command]
    void AddPlayer(GameObject g)
    {
        players.Add(g);
        RPCOnAdded();
    }

    [TargetRpc]
    void RPCOnAdded()
    {
        Debug.Log("Successfully joined player pool");
    }

    #endregion
    // Update is called once per frame
    void Update()
    {
        if (SceneManager.GetActiveScene().name != "Lobby"&& SceneManager.GetActiveScene().name != "Forest")
        {
            if (!finalizedGameOver)
            {
                if (!isLocalPlayerBool) return;
                #region UX Update
                if (timer_text != null || time.isCountdown)
                {
                    timer_text.text = string.Format("{0:0}:{1:00}", time.GetMinute(), time.GetSeconds());
                }
                if (game_manager == null)
                {
                    game_manager = GameObject.FindGameObjectWithTag("Game Manager").GetComponent<GameManager>();
                }
                if (time.GetTimeOverState() || time.GetGameOverState()||game_manager.win_state!=-1)
                {
                    OnGameOver();
                    finalizedGameOver = true;
                }
                #endregion
            }

        }
        if (final_death&&!finalDeathDone)
        {
            if (!finalDeathDone)
            {
                var c = GetComponentInChildren<FixedJoint>();
                if (c != null)
                {
                    Destroy(c);
                }
                //Final death stuff here
                deathCamZoom = false;
                lockheadDeath = false;
                if (mainCamera != null)
                {
                    mainCamera.gameObject.SetActive(false);
                }
                finalDeathDone = true;
            }
            
        }

        #region Death Update Calls
        if (lockheadDeath)
        {
          
            mainCamera.transform.LookAt(lockHeadLook.transform);
            if (lockHeadCamHolder == null)
            {
                lockHeadCamHolder = new GameObject("Lockhead Holder");
                lockHeadCamHolder.transform.position = transform.position;
                mainCamera.transform.SetParent(lockHeadCamHolder.transform, true);
            }
            lockHeadCamHolder.transform.position = lockHeadPos.position;
        }
        #endregion
       

    }

    #region Game State

    [Command(requiresAuthority = false)]
    public void RefreshConnectedPlayerCount()
    {
        connectedPlayers = 0;
        foreach(GameObject g in players)
        {
            if (g == null)
            {
                players.Remove(g);
            }
            else
            {
                connectedPlayers++;
            }
        }
        if (gm == null)
        {
            gm = GameObject.FindGameObjectWithTag("Game Manager");
        }
        gm.GetComponent<GameManager>().SetPlayerCount(connectedPlayers);
        
    }

    [Command(requiresAuthority =false)]
    public void RefreshSurvivorsState() //Calculates if all survivors are dead
    {
        RefreshConnectedPlayerCount();
        int c = 0;
        bool allSurvivorsDead = true;
        foreach(GameObject p in players)
        {
            if (!p.GetComponent<Player>().isKiller)
            {
                c++;
                if (p.GetComponent<Player>().is_alive)
                {
                    allSurvivorsDead = false;
                }
            }
        }
        if (gm == null)
        {
            gm = GameObject.FindGameObjectWithTag("Game Manager");
        }
        gm.GetComponent<GameManager>().SetSurvivorCount(c);
        if (allSurvivorsDead)
        {
            gm.GetComponent<GameManager>().SetWinState(0); //Killer wins when all survivors are dead
        }
    }

    #endregion
    #region Server Side Filling
    public void FillSurvivors() //Fill survivors list (host only)
    {
        foreach (GameObject i in players)
        {
            if (!killers.Contains(i))
            {
                ServerAddSurvivor(i);
            }
        }
    }
    #endregion

    #region Killer/Survivor State Control
    public void HookKillerTagChange(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            gameObject.tag = "Killer";
        }
    }
    //[TargetRpc]
    public void setSurvivorState()
    {
        if (SceneManager.GetActiveScene().name != "Forest")
        {
            playerState.text = p_survivor;
            playerState.color = p_green;

            GetComponent<HostManagerUI>().InitiliazeHMUI(false);
            GetComponent<EquipmentManager>().canUse = true;
            //Debug.Log(gameObject.name + "'s state: " + isKiller);
        }
        else
        {
            //Forest biome initialization
            GetComponent<EquipmentManager>().canUse = true;
        }



    }
    [TargetRpc]
    public void setKillerState()
    {
        playerState.text = p_killer;
        playerState.color = p_red;
        GetComponent<HostManagerUI>().InitiliazeHMUI(true);
 
        Debug.Log(gameObject.name + "'s state: " + isKiller);
        RequestHMOutlineEnable();
        GetComponent<EquipmentManager>().canUse = true;
        GetComponent<GhostController>().isKiller = true;
    }

   
    public void RequestHMOutlineEnable()
    {
        gm.GetComponent<HostMachineManager>().EnableOutlines(this.gameObject);
    }


    public void SetKiller()
    {
        setKillerState();
        isKiller = true;
    }
    
    public void SetSurvivor()
    {
        setSurvivorState();
        isKiller = false;
    }

    #region Win/Lose State

    public void OnGameOver()
    {
        //GetComponent<MLobbyManager>().GoToLobby(); moved to GameManger to call
        //Use game manager win state

        //Update Local Game Stats Winning
        if (game_manager.win_state == 0)
        {
            //Killer wins
            if (isKiller)
            {
                gameStats.isWinner = true;
            }
            else
            {
                gameStats.isWinner = false;
            }
        }
        else if (game_manager.win_state == 1)
        {
            //Survivor wins
            if (isKiller)
            {
                gameStats.isWinner = false;
            }
            else
            {
                gameStats.isWinner = true;
            }
        }
        else if(game_manager.win_state ==2)
        {
            //Everyone wins
            gameStats.isWinner = true;
        }

        //Update Local UI
        if (gameStats.isWinner)
        {
            playerState.text = p_win;
            playerState.color = p_green;
        }
        else
        {
            playerState.text = p_lose;
            playerState.color = p_red;
        }

        //Close off all interactions and wait for transport
        GetComponent<InteractionDetection>().enabled = false;
        //Xp from kills and host machine repairs update runtime
    }

  
    #endregion
    #endregion


    #region On Attack
    [Command(requiresAuthority = false)]
    public void OnSanityHit(int damage)
    {
        sanity -= damage;
        if (sanity <= 0&&is_alive)
        {
            is_alive = false;
            sanity = 0;
            SelfDeath();
            RefreshSurvivorsState();
        }
        if (!self_healing)
        {
            StartCoroutine(AutoHeal());
        }
    }

    IEnumerator AutoHeal()
    {
        self_healing = true;
        float temp_health = sanity;
        yield return new WaitForSeconds(25f);
        if (sanity == temp_health)
        {
            while (sanity < 95f)
            {
                sanity += 1;
                yield return new WaitForSeconds(2f);
            }
        }
        self_healing = false;
    }
    #endregion

    #region Death & Effects
    //Ragdoll
    public void EnableDeathRagdoll(bool oldVal, bool newVal) //hook
    {
        if (!newVal)
        {
            //GetComponent<NetworkTransform>().enabled = false;
            if (GetComponent<NetworkTransformChild>() != null)
            {
                //GetComponent<NetworkTransformChild>().enabled = false;
            }
            GetComponent<CharacterController>().enabled = false;
            gameObject.tag = "Died";
            
            LocalRagdollDeath();
        }
    }
    [TargetRpc]
    public void SelfDeath()
    {
        EquipmentManager em = GetComponent<EquipmentManager>();
        em.DropAllEquipments();
        em.canUse = false;
        GetComponent<TerrorZone>().m_heartbeat.volume = 0; //Stop heartbeat
        deathAudioSource.clip = GetComponent<AudioContainer>().death_audio;
        deathAudioSource.Play();
        DisablePlayerControl();
        
        
        StartCoroutine(TimedDeathAmount());
    }
    IEnumerator TimedDeathAmount()
    {
        yield return new WaitForSeconds(deathDuration);
        //Death stuff
        //Spawn death ghost
        GameObject d = Instantiate(deathGhost, gameObject.transform.position, gameObject.transform.rotation);
        GetComponent<InteractionDetection>().cam = d.GetComponentInChildren<Camera>();
        foreach (AudioSource a in GetComponentsInChildren<AudioSource>())
        {
            a.volume = 0f;
        }
        final_death = true; //cut off effects and switch to ragdoll, spawn new ghost player
    }
    //Brings character to the killer's hand position for tearing
    [Command(requiresAuthority =false)]
    public void DeathLockHang(uint lockPosID)
    {
        
        DisablePlayerControl();
        LockTransform(lockPosID);
    }

    [TargetRpc]
    public void DisablePlayerControl()
    {
        //disable fsm
        foreach (PlayMakerFSM p in GetComponentsInChildren<PlayMakerFSM>())
        {
            p.enabled = false;
        }
        GetComponent<PlayerMouseLook>().enabled = false;
        
    }

    [TargetRpc]
    public void LockTransform(uint pos)
    {
        lockHeadPos = NetworkClient.spawned[pos].GetComponent<GhostHelper>().hand;
        lockHeadLook = NetworkClient.spawned[pos].GetComponent<GhostHelper>().head;
        lockheadDeath = true;

        //Move camera up for slender
        Vector3 p = mainCamera.transform.position;

        mainCamera.transform.position = p + new Vector3(0, -0.15f, 0);
        deathCamZoom = true;
    }

    #endregion
    #region Ragdoll
    [Command(requiresAuthority =false)]
    public void CMDSetRagdollState(bool enable)
    {
        if (enable)
        {
            RPCEnableRagdoll();
        }
        else
        {
            RPCDisableRagdoll();
        }
    }
    [ClientRpc]
    void RPCDisableRagdoll()
    {
        setRigidbodyState(true);
        setColliderState(false);
        AnimatedBody.GetComponent<Animator>().enabled = true;
    }
    [ClientRpc]
    void RPCEnableRagdoll()
    {
        setRigidbodyState(false);
        setColliderState(true);
        AnimatedBody.GetComponent<Animator>().enabled = false;
    }

    //Local Hook
    private void LocalRagdollDeath()
    {
        GetComponent<NetworkAnimator>().enabled = false;
        AnimatedBody.GetComponent<Animator>().enabled = false;
        setRigidbodyState(false);
        setColliderState(true);
    }
    void setRigidbodyState(bool state)
    {

        Rigidbody[] rigidbodies = AnimatedBody.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rigidbody in rigidbodies)
        {
            rigidbody.isKinematic = state;
            rigidbody.useGravity = !state;
            rigidbody.detectCollisions = !state;
        }

        CharacterJoint[] j = AnimatedBody.GetComponentsInChildren<CharacterJoint>();
        foreach(CharacterJoint cj in j)
        {
            cj.enableCollision = !state;
        }

    }

    void setColliderState(bool state)
    {

        Collider[] colliders = AnimatedBody.GetComponentsInChildren<Collider>();

        foreach (Collider collider in colliders)
        {
            collider.enabled = state;
        }

    }

    #endregion

    #region Player / Ghost event interactions
    [Command(requiresAuthority = false)]
    public void OnBeingChasedByAI(bool chased, GameObject player, uint ghost)
    {
        RPCBeingChasedByAI(player.GetComponent<NetworkIdentity>().connectionToClient, ghost, chased);
    }

    [TargetRpc]
    public void RPCBeingChasedByAI(NetworkConnection player, uint ghost, bool chased) //invoked on the local player client
    {
        if (chased)
        {
            GetComponent<TerrorZone>().AddGhost(ghost);
        }
        else
        {
            GetComponent<TerrorZone>().RemoveGhost(ghost);
        }
    }
    #endregion

    #region Animation and Effects
    public void SetAnimatorSpeed(float speed)
    {
        animator.speed = speed;
    }
    //Only localplayer for allowing use of tablet, animation still show for clients
    public void FreezeCameraAnimator()
    {
        camera_animator.speed = 0;
    }

    //Only localplayer for allowing use of tablet, animation still show for clients
    public void UnFreezeCameraAnimator()
    {
        camera_animator.speed = 1;
    }
    #endregion
}
