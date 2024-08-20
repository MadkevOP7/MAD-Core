using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Lean.Localization;
public class HostManagerUI : MonoBehaviour
{
    [Header("Components")]
    public Sprite s_icon;
    public Sprite k_icon;
    public GameObject pObj;
    public Image hmImg;
    public Slider s_progress;
    public Text display;
    public bool isHost;
    public string s;
    public void InitiliazeHMUI(bool is_host)
    {
        isHost = is_host;
        if (isHost)
        {
            hmImg.sprite = k_icon;
            s = Lean.Localization.LeanLocalization.GetTranslationText("Repair Progress");
            display.text = s;
        }
        else
        {
            hmImg.sprite = s_icon;
            s = Lean.Localization.LeanLocalization.GetTranslationText("Hacking Progress");
            display.text = s;
        }
    }

    public void DisplayProgressBar(float progress, float maxHealth)
    {
        if (!pObj.activeInHierarchy)
        {
            pObj.SetActive(true);
        }

        s_progress.value = progress/maxHealth;  
    }

    public void HideProgressBar()
    {
        if (pObj.activeInHierarchy)
        {
            pObj.SetActive(false);
        }
    }
}
