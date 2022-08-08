using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
public class ObserveOS : NetworkBehaviour
{
    public bool isLocalPlayerBool;
    public enum Tabs
    {
        OFBlocksVertical,
        OFBlocksFlat,
        Blocks
    }
    private Tabs _currentTab;
    public Tabs currentTab
    {
        get { return _currentTab; }
        set
        {
            _currentTab = value;
            RefreshCurrentTab();
        }
    }
    [Header("Component References")]
    public Canvas canvas;
    public GameObject clientCover;
    [Header("1/4 Chunks Vertical")]
    public Transform OFVerticalParent; //for 1/4 size chunks preview tab, vertical ones..
    [Header("UI Element Prefabs")]
    public SquareItemHolder squareItemHolder;

    //Runtime Item Selection
    private SquareItemHolder _selectedItem;
    public SquareItemHolder selectedItem
    {
        get
        {
            return _selectedItem;
        }
        set
        {
            if (_selectedItem != null && value != null)
            {
                _selectedItem.Select(false);
            }
            _selectedItem = value;
        }
    }
    #region Initialization Functions
    private void Awake()
    {
        //Set default mode
        canvas.gameObject.SetActive(false);
        clientCover.SetActive(true);
    }
    public void InitializeOS()
    {
        isLocalPlayerBool = true;
        CMDRefreshOSClients();
    }
    [Command(requiresAuthority = false)]
    void CMDRefreshOSClients()
    {
        RPCRefreshOSClients();
    }
    [ClientRpc(includeOwner = false)]
    void RPCRefreshOSClients()
    {
        if (isLocalPlayerBool)
        {
            Destroy(clientCover.gameObject);
            canvas.gameObject.SetActive(true);
        }
        else
        {
            clientCover.SetActive(true);
            Destroy(canvas.gameObject);
            Destroy(this);
        }
       
    }
    public void AssignCamera(Camera cam)
    {
        canvas.worldCamera = cam;
    }
    #endregion

    #region Core Functions
    public void Launch(Camera cam)
    {
        AssignCamera(cam);
        currentTab = Tabs.OFBlocksVertical; //For now, change later according to Tab selected
    }

    public void Close()
    {
        //Implement closing...
    }
    public void RefreshCurrentTab()
    {
        switch (currentTab)
        {
            case Tabs.OFBlocksVertical:
                //Clear UI
                foreach(SquareItemHolder i in OFVerticalParent.GetComponentsInChildren<SquareItemHolder>())
                {
                    Destroy(i.gameObject);
                }
                foreach(GlobalItem item in GlobalInventory.Instance.inventory.OFBlocksVertical)
                {
                    SquareItemHolder holder = Instantiate(squareItemHolder, OFVerticalParent, false);
                    holder.m_name = item.m_name;
                    holder.count.text = item.count.ToString();
                    holder.image.sprite = GlobalBuild.Instance.previews[item.m_name];
                    if(item.count == 0)
                    {
                        holder.GetComponent<Button>().interactable = false;
                    }
                    else
                    {
                        holder.GetComponent<Button>().interactable = true;
                    }
                    holder.os = this;
                    if(GlobalInventory.Instance.selected == item)
                    {
                        holder.Select(true);
                    }
                }

                break;
        }
    }
    #endregion
}
