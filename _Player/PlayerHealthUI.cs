using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.UI;
public class PlayerHealthUI : NetworkBehaviour
{
    public bool isLocalPlayerBool;
    public Player player;
    public float sanity = 100f;
    public Image screen;
    [Range(0, 0.68f)]
    public float oppacity;
    bool isPlayerAlive = true;
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        isLocalPlayerBool = true;
    }
    private void Update()
    {
        #region Setup
        if (!isLocalPlayerBool) return;
        if (!isPlayerAlive) return;
        if (player == null)
        {
            player = GetComponent<Player>();
        }
        sanity = player.sanity;
        #endregion

        //UI change here
        if (sanity >= 95f)
        {
            oppacity = 0f;
        }
        else if (sanity > 50&&sanity<95f)
        {
            oppacity = 0.1978f;
        }
        else if(sanity == 0)
        {
            isPlayerAlive = false;
            StartCoroutine(DeathUIFade());
        }
        else
        {
            oppacity = (100 - sanity) * 0.0046f;
        }
        var tempColor = screen.color;
        tempColor.a = oppacity;
        screen.color = tempColor;
    }

    IEnumerator DeathUIFade()
    {
        yield return new WaitForSeconds(2.68f);
        while (oppacity > 0)
        {
            oppacity -= 0.01f;
            var tempColor = screen.color;
            tempColor.a = oppacity;
            screen.color = tempColor;
            yield return new WaitForSeconds(0.5f);
        }
    }
}
