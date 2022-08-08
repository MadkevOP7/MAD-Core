using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelLoadingUI : MonoBehaviour
{
    [Header("References")]
    public GameObject logo;
    // Start is called before the first frame update
    void OnEnable()
    {
        StartCoroutine(TimedEnable());
    }
    IEnumerator TimedEnable()
    {
        yield return new WaitForSeconds(1.2f);
        logo.gameObject.SetActive(true);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
