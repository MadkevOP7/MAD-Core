using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class SquareItemHolder : MonoBehaviour
{
    [Header("Settings")]
    public bool _selected;
    public Color selectedColor;
    Color startColor;
    public Sprite selected_sprite;
    public Sprite normal_sprite;
    public string m_name;
    public Text count;
    public Image image;
    public Image selection;
    public ObserveOS os;
    public void Select(bool selected)
    {
        _selected = selected;
        if (_selected)
        {
            selection.sprite = selected_sprite;
            selection.color = selectedColor;
            os.selectedItem = this;
            GlobalInventory.Instance.selected = GlobalInventory.Instance.dictionary[m_name];
        }
        else
        {
            os.selectedItem = null;
            GlobalInventory.Instance.selected = null;
            selection.sprite = normal_sprite;
            selection.color = startColor;
        }
    }

    public void UISelect() // for handling button press
    {
        Select(!_selected);
    }
    public bool IsSelected()
    {
        return _selected;
    }

    private void Start()
    {
        startColor = selection.color;
    }
}
