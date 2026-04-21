using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TextLanguage : MonoBehaviour
{
    public string language;
    TextMeshProUGUI text;

    public string textRu;
    public string textEng;

    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    // Update is called once per frame
    void Update()
    {
        language = PlayerPrefs.GetString("Language");

        if(language == "" || language == "Eng")
        {
            text.text = textEng;
        }
        else if(language == "Ru")
        {
            text.text = textRu;
        }
    }
}
