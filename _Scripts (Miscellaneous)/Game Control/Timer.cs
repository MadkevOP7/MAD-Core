using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class Timer : NetworkBehaviour
{
    [Header("Timer View")]
    public int min;
    public int sec;
    [SyncVar]
    public float seconds;
    [SyncVar]
    public bool isCountdown;

    [SyncVar]
    public bool isGameOver;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        min = GetMinute();
        sec = GetSeconds();
        #region Countdown Control
        if (isCountdown)
        {
            if (seconds > 0)
            {
                seconds -= Time.deltaTime;
            }
            else
            {
                //Time over
                seconds = 0;
                isCountdown = false;
            }
        }
        #endregion
    }

    #region Time Getters
    public int GetMinute()
    {
        return Mathf.FloorToInt(seconds / 60);
    }

    public int GetSeconds()
    {
        return Mathf.FloorToInt(seconds % 60);
    }
    public bool GetTimeOverState()
    {
        return (seconds == 0) ? true : false;
    }

    public bool GetGameOverState()
    {
        return isGameOver;
    }

    [Command(requiresAuthority = false)]
    public void CMDSetGameOver()
    {
        isGameOver = true;
    }
    #endregion

    public void StartTimer()
    {
        isCountdown = true;
    }
}
