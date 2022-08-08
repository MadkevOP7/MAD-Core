using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lean.Localization;
using UnityEngine.UI;
public class DelayedTranslateAddString : MonoBehaviour
{
    //For fixing UI translated text such as Rank: delay to add Rank:1
    public string add;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(DelayAdd());
    }


    IEnumerator DelayAdd()
    {
        yield return new WaitForEndOfFrame();
        this.GetComponent<LeanLocalizedText>().enabled = false;
        this.GetComponent<Text>().text = this.GetComponent<Text>().text + add;
    }
}
