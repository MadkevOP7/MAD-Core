using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
public class GameManager : NetworkBehaviour
{
    
    [SyncVar]
    public int win_state = -1; //-1 game running, 0 killer win, 1 survivor win, 2 (minimum player) everyone in game wins
    [SyncVar]
    public int connectedPlayers;
    [SyncVar]
    public int survivorsCount;
    public bool is_server;
    [Header("Loading")]
    public GameObject loadingScreenOBJ;

    [Command(requiresAuthority = false)]
    public void SetPlayerCount(int count)
    {
        connectedPlayers = count;
        
    }

    [Command(requiresAuthority = false)]
    public void SetSurvivorCount(int count)
    {
        survivorsCount = count;
        if (count <= 2 && SceneManager.GetActiveScene().name != "Forest")
        {
            Debug.Log("Fog phase starting");
            GetComponent<FogManager>().ServerEnableFog(true);
        }
    }

    [Command(requiresAuthority = false)]
    public void SetWinState(int state)
    {
        win_state = state;
        Debug.Log("Win State: " + win_state);
        if (win_state != -1)
        {
            //ServerGameOver(); //[TEMP] for testing, uncomment for release
        }
    }

    public void ServerGameOver()
    {
        Debug.Log("Game Over!");
        StartCoroutine(TimedReturnToLobby());
    }


    IEnumerator TimedReturnToLobby()
    {
        yield return new WaitForSeconds(6f);
        RPCSpawnLoadingScreen();
        yield return new WaitForSeconds(3f); //Allow screen fading, finish killing animation, etc.
        if (GameObject.FindGameObjectWithTag("LobbyManager") != null)
        {
            GameObject.FindGameObjectWithTag("LobbyManager").GetComponent<LobbyManager>().GameToLobby();
        }
        else
        {
            GameObject.FindGameObjectWithTag("NetworkManager").GetComponent<Manager>().LoadGame("Lobby"); //Go back to lobby

            Debug.LogWarning("Lobby Manager wasn't found, probably launched through game tests, save data not written");
        }
    }

    [ClientRpc]
    public void RPCSpawnLoadingScreen()
    {
        //EOS lobby doesn't cotain RPC ability, so here we flag each client's lobbymanager backfromgame to true
        if (GameObject.FindGameObjectWithTag("LobbyManager") != null)
        {
            GameObject.FindGameObjectWithTag("LobbyManager").GetComponent<LobbyManager>().backFromGame = true;
        }
        else
        {
            Debug.Log("No lobby manager found, started directly from game scene?");
        }
        Instantiate(loadingScreenOBJ);

    }
}
