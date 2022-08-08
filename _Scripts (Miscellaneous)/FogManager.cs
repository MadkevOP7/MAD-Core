using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class FogManager : NetworkBehaviour
{
    [Header("Settings")]
    public AudioSource m_failure;
    public float fog_fading_speed;
    [SyncVar(hook =nameof(HookFogIntensity))]
    public float intensity;

    private bool in_operation; //Prevent duplicate changing fog

    [Command(requiresAuthority = false)]
    public void ServerEnableFog(bool enable)
    {
        if (!in_operation)
        {
            if (enable)
            {
                StartCoroutine(TimedEnableFog());
                RPCPlayFailureAudio();
            }
            else
            {
                StartCoroutine(TimedDisableFog());
            }
            in_operation = true;
        }
        
    }

    IEnumerator TimedEnableFog()
    {
        do
        {
            intensity += fog_fading_speed * Time.deltaTime;
            yield return null;
        } while (intensity < 0.28f);
    }

    IEnumerator TimedDisableFog()
    {
        do
        {
            intensity -= fog_fading_speed * Time.deltaTime;
            yield return null;
        } while (intensity > 0f);
    }
    public void HookFogIntensity(float oldVal, float newVal)
    {
        RenderSettings.fogDensity = newVal;
    }

    [ClientRpc]
    public void RPCPlayFailureAudio() //Announcement, gate failure protocal
    {
        m_failure.Play();
    }
}
