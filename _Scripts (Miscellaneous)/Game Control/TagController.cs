using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class TagController : NetworkBehaviour
{
    /*
    //TAG GAME//
    Timer time;
    [Header("Game Settings")]

    public int chaser_amount;

    public int runner_amount;

    public int player_amount;

    [Header("Debug")]

    [SyncVar]
    public int player_count;

    public readonly SyncList<GameObject> players = new SyncList<GameObject>();
    public List<NetworkIdentity> chasers;
    public List<NetworkIdentity> runners;
  
    //----------------------
    private void Start()
    {
        time = GameObject.FindGameObjectWithTag("Game Manager").GetComponent<Timer>();
        StartCoroutine(StartTimed());
    }

    IEnumerator StartTimed()
    {
        yield return new WaitForSeconds(1f);
        OnGameStart();
    }
    #region Player Container Clear Functions
    
    public void ClearPlayers()
    {
        players.Clear();
    }


    public void ClearChasers()
    {
        chasers.Clear();
    }


    public void ClearRunners()
    {
        runners.Clear();
    }
    #endregion

    #region Player Removal Functions

    public void RemovePlayer(NetworkIdentity p)
    {
        players.Remove(p);
        if (chasers.Contains(p))
        {
            RemoveChaser(p);
        }
        if (runners.Contains(p))
        {
            RemoveRunner(p);
        }
        player_count--;
    }

    public void RemoveChaser(NetworkIdentity p)
    {
        chasers.Remove(p);
    }

    public void RemoveRunner(NetworkIdentity p)
    {
        runners.Remove(p);
    }
    
    #endregion

    #region Player Adding Functions

    
    public void AddPlayer(GameObject p)
    {
        p.GetComponent<NetworkIdentity>().AssignClientAuthority(this.gameObject.GetComponent<NetworkIdentity>().connectionToClient);
        CMDAddPlayer(p);
    }

    [Command(requiresAuthority =false)]
    void CMDAddPlayer(GameObject player)
    {
        players.Add(player);
        player_count++;
    }
    
    public void AddChaser(NetworkIdentity p)
    {
        Debug.Log("adding chaser");

        chasers.Add(p); //Add to chasers list
        if (runners.Contains(p)) //Remove from runners list
        {
            runners.Remove(p);
        }
        p.GetComponent<Player>().setChaser(); //Setting Player state on Player script
    }

    public void AddRunner(NetworkIdentity p)
    {
        Debug.Log("adding runner");
        runners.Add(p);
        if (chasers.Contains(p))
        {
            chasers.Remove(p);
        }
        p.GetComponent<Player>().setRunner();
    }
    
    #endregion

    #region Global Control

    public void ResetGame()
    {

        //ClearChasers();
        //ClearRunners();
        //ClearPlayers();
    }

    public void OnGameStart() //This is the function that starts the game (setting up players, determine who is possessed, etc)
    {
        time.StartTimer();
        DetermineChaser(chaser_amount);
        //FillRunnersStart();
    }
    #endregion

    #region On Game Start

    public void FillRunnersStart()
    {
      
        foreach (GameObject i in players)
        {
            if (!chasers.Contains(i.GetComponent<NetworkIdentity>()))
            {
                AddRunner(i.GetComponent<NetworkIdentity>());
            }
        }
  
    }

    public void DetermineChaser(int amount)
    {
        //Test set all to runner
        for(int i=0; i<players.Count; i++)
        {
            SetRemotePlayerState(players[i], 0);

        }
       
        for (int i = 0; i < amount; i++)
        {
            int random = UnityEngine.Random.Range(0, players.Count);
            Debug.Log("Random Selected Index: " + random);
            SetRemotePlayerState(players[i].GetComponent<NetworkIdentity>().connectionToClient.identity.gameObject, chaser_amount);
        }
      
    }

    [Command(requiresAuthority = false)]
    public void SetRemotePlayerState(GameObject conn, int s)
    {
        
        RPCSetState(conn.GetComponent<NetworkIdentity>().connectionToClient, s);
    }

    [TargetRpc]
    public void RPCSetState(NetworkConnection conn, int state)
    {
        if (state == 0)
        {
            GetComponent<Player>().setRunner();
        }
        else if (state == 1)
        {
            GetComponent<Player>().setChaser();

        }
    }
    #endregion
    */
}
