using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//For managing global camera effects
public class CameraManager : MonoBehaviour
{
    [Header("Cameras")]
    public Camera humanCam;
    public Camera ghostCam;
    [Header("Audios")]
    public AudioSource audioSource;
    public AudioClip glitch_audio;

    private void Start()
    {
        
    }
    #region glitch effects
    public void GhostSwitchGlitchCamera()
    {
        
        GlitchCamera(.68f, 0, true);
        GlitchCamera(.68f, 1, false);
    }
    public void GlitchCamera(float duration, int camera, bool playSound) //0=humancam 1=ghostcam
    {
        if (camera == 0)
        {
            humanCam.GetComponent<MobileGlitchCameraShader>().enabled = true;
        }
        else if (camera == 1)
        {
            ghostCam.GetComponent<MobileGlitchCameraShader>().enabled = true;
        }
        if (playSound)
        {
            PlayAudio(glitch_audio);
        }
        StartCoroutine(TimedCameraGlitchDisable(duration, camera));
    }

    public void PlayAudio(AudioClip clip)
    {
        if (audioSource.isPlaying)
        {
            if (audioSource.clip == clip)
            {
                audioSource.Stop();
            }
        }
        audioSource.clip = clip;
        audioSource.Play();
    }
    IEnumerator TimedCameraGlitchDisable(float time, int camera)
    {
        yield return new WaitForSeconds(time);
        if (camera == 0)
        {
            humanCam.GetComponent<MobileGlitchCameraShader>().enabled = false;
        }
        else if (camera == 1)
        {
            ghostCam.GetComponent<MobileGlitchCameraShader>().enabled = false;
        }
      
    }
    #endregion
}
