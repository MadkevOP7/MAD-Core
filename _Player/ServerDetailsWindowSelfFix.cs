using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//Local fixes ready button and unready button activation status once player exits current lobby, to reset GUI
public class ServerDetailsWindowSelfFix : MonoBehaviour
{
    [Header("References")]
    public Button readyBtn;
    public Button unreadyBtn;
    

    private void OnDisable()
    {
        readyBtn.gameObject.SetActive(true);
        unreadyBtn.gameObject.SetActive(false);
    }
}
