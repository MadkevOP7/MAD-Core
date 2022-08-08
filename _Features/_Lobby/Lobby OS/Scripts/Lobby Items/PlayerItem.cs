using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Lean.Localization;
public class PlayerItem : MonoBehaviour
{
    [Header("Components")]
    public Text p_name;
    public Text rank;
    public Text status_ready; //for localisation
    public Text status_unready;
    public Image player_image;
    public Image host_img;
    public bool is_host;

    //Ini
    public void InitializePlayerItem(string name, int m_rank, bool isHost, bool isReady)
    {
        SetPlayerName(name);
        SetPlayerRank(m_rank);
        SetPlayerIsHost(isHost);
        SetReadyState(isReady);
    }
    public void SetPlayerName(string n)
    {
        p_name.text = n;
    }

    public void SetReadyState(bool isready)
    {
        if (isready)
        {
            status_ready.gameObject.SetActive(true);
            status_unready.gameObject.SetActive(false);
        }
        else
        {
            status_ready.gameObject.SetActive(false);
            status_unready.gameObject.SetActive(true);
        }
    }
    public void SetPlayerRank(int n)
    {
        this.GetComponentInChildren<DelayedTranslateAddString>().add = " " + n.ToString();
    }
    public void SetPlayerIsHost(bool n)
    {
        if (n)
        {
            is_host = true;
            host_img.enabled = true;
        }
        else
        {
            is_host = false;
            host_img.enabled = false;
        }
    }

   
}
