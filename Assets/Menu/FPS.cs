using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FPS : MonoBehaviour
{
    TextMeshProUGUI text;
    float time;
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    // Update is called once per frame
    void Update()
    {
        if (time <= 0)
        {
            float vivod = (1f / Time.deltaTime) - ((1f / Time.deltaTime) % 1);
            text.text = vivod +" FPS";
            //Debug.Log(1f / Time.deltaTime);
            time = 1f;
        }
        else
            time -= Time.deltaTime;
    }
}
