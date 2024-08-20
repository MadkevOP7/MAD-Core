using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
/// <summary>
/// Derives from NetworkBehaviour to allow for mechanics where server client communication is required, ie. target forwarding to client display
/// </summary>
public class MultiStackInstance : NetworkBehaviour
{
    Renderer[] mRenderersCache;
    public override void OnStartClient()
    {
        base.OnStartClient();
        mRenderersCache = GetComponentsInChildren<Renderer>();
        BaseTimeframeManager.Instance.OnRefreshLocalPlayerIsPastState += OnLocalPlayerTimeFrameChanged;
        BaseTimeframeManager.Instance.OnLocalPlayerLimenBreakoccured += OnLocalPlayerLimenBreakOccured;
    }
    public void SetClientVisibility(bool visible)
    {
        foreach (Renderer renderer in mRenderersCache)
        {
            renderer.enabled = visible;
        }
    }

    #region Timeframe Callbacks
    private void OnLocalPlayerLimenBreakOccured()
    {
        SetClientVisibility(true);
    }
    private void OnLocalPlayerTimeFrameChanged(bool isInPast)
    {
        //Only visible in present
        SetClientVisibility(!isInPast);
    }
    #endregion
}
