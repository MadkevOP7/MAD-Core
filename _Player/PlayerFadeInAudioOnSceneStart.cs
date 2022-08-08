using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using UnityEngine.Events;

public class PlayerFadeInAudioOnSceneStart : MonoBehaviour
{

    public AudioSource audios;
    

    private void Start()
    {
        StartCoroutine(DelayFootStep());
    }

    IEnumerator DelayFootStep()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(1);
      
        if (audios != null)
        {
            audios.volume = 1;
        }
    }
   
}
