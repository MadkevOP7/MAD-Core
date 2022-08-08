using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ResultItem : MonoBehaviour
{
    [Header("Components")]
    public Text p_name;
    public Text rank;
    
    public Image player_image;
    public Image winner_img;


    //Ini
    public void InitializeResultItem(string name, int m_rank)
    {
        SetPlayerName(name);
        SetPlayerRank(m_rank);
       
    }
    public void SetPlayerName(string n)
    {
        p_name.text = n;
    }

    public void SetPlayerRank(int n)
    {
        this.GetComponentInChildren<DelayedTranslateAddString>().add = " " + n.ToString();
    }
    
}
