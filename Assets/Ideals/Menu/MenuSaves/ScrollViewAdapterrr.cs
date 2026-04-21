using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;

public class ScrollViewAdapterrr : MonoBehaviour {
    public RectTransform prefarb;
    int i = 1;
    public InputField countText;
    public TextMeshProUGUI ButtonText;
    public RectTransform content;
    public GameObject[] saveButton;
    public static bool Activeee = false;

    private void Start()
    {
        //PlayerPrefs.DeleteKey("KolvoSohroneniy");
        if (PlayerPrefs.HasKey("KolvoSohroneniy"))
        {

        }
        else
        {
            PlayerPrefs.SetInt("KolvoSohroneniy", 0);
        }
        if (PlayerPrefs.GetString("Language") == "Ru")
            ButtonText.text = "Создать сохранение";
        if (PlayerPrefs.GetString("Language") == "En")
            ButtonText.text = "Create Save";
        int modelsCount = 0;
        string b = "" + PlayerPrefs.GetInt("KolvoSohroneniy");
        int.TryParse(b, out modelsCount);
        StartCoroutine(GetItems(modelsCount, results => OnReceivedModels(results)));
        //PlayerPrefs.SetInt("KolvoSohroneniy", saveButton.Length);
    }

    private void Update()
    {
            if (saveButton.Length != 0)
        {
            if (!PlayerPrefs.HasKey((saveButton.Length - 1) + "SaveName"))
            {
                Activeee = true;
                //Debug.Log((saveButton.Length - 1) + "  True : " + (PlayerPrefs.GetString((saveButton.Length - 1) + "SaveName")));
            }
            else
            {
                Activeee = false;
                //Debug.Log((saveButton.Length - 1) + "  False : " + (PlayerPrefs.GetString((saveButton.Length - 1) + "SaveName")));
            }
        }
    }

    public void UpdateItems ()
    {
        if (countText.text != "")
        {
            if (PlayerPrefs.GetString("Language") == "Ru")
                ButtonText.text = "Создать сохранение";
            if (PlayerPrefs.GetString("Language") == "En")
                ButtonText.text = "Create Save";
            int modelsCount = 0;
            string b = "1";
            int.TryParse(b, out modelsCount);
            StartCoroutine(GetItems(modelsCount, results => OnReceivedModels(results)));
            PlayerPrefs.SetInt("KolvoSohroneniy", PlayerPrefs.GetInt("KolvoSohroneniy") + 1);
            PlayerPrefs.Save();
        }
        if(countText.text == "")
        {
            if(PlayerPrefs.GetString("Language") == "Ru")
            ButtonText.text = "Введите название сохранения";
            if(PlayerPrefs.GetString("Language") == "En")
            ButtonText.text = "Name save";
        }
    }

    void OnReceivedModels (TestItemModel[] models)
    {
        foreach (Transform child in content)
        {
            //Destroy(child.gameObject);
        }

        foreach (var model in models)
        {
            var instance = GameObject.Instantiate(prefarb.gameObject) as GameObject;
            instance.transform.SetParent(content, false);
            InitializeItemView(instance, model);
            Array.Resize(ref saveButton, saveButton.Length+1);
            saveButton[saveButton.Length-1] = instance;
        }
    }

    void InitializeItemView (GameObject viewGameObject, TestItemModel model)
    {
        TestItemView view = new TestItemView(viewGameObject.transform);
        view.titleText.text = model.title;
        //view.clickButton.GetComponentInChildren<Text>().text = model.buttonText;
        view.clickButton.onClick.AddListener(
            ()=> 
            {
                Debug.Log(view.titleText.text + " is clicked!");
            }
        );
    }

    IEnumerator GetItems (int count, System.Action<TestItemModel[]> callback)
    {
        yield return new WaitForSeconds(1f);
        var results = new TestItemModel[count];
        for (int i = 0; i < count; i++)
        {
            results[i] = new TestItemModel();
            results[i].title = countText.text;
            //results[i].buttonText = "Button " + i;
        }

        callback(results);
    }

    public class TestItemView
    {
        public TextMeshProUGUI titleText;
        public Button clickButton;

        public TestItemView (Transform rootView)
        {
            titleText = rootView.Find("TitleText").GetComponent<TextMeshProUGUI>();
            clickButton = rootView.Find("ClickButton").GetComponent<Button>();
        }
    }

    public class TestItemModel
    {
        public string title;
        public string buttonText;
    }

    public void SaveNameSaveTrue(TextMeshProUGUI textt)
    {
        PlayerPrefs.SetInt(textt.text, 1);
    } 
    public void SaveNameSaveFalse(TextMeshProUGUI textt)
    {
        PlayerPrefs.SetInt(textt.text, 0);
    }
    public void LoadNameSave(Toggle toggle)
    {
    }
    public void Munys()
    {
        for(int i = 0; i < saveButton.Length; i++)
        saveButton[i].GetComponent<SaveAndLoadButton>().Munys();
    }
    public void Exit()
    {
            File.Delete(Application.persistentDataPath + "/SaveSaveMenu0__" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
        for (int f = 0; f<saveButton.Length; f++)
        {
            if(saveButton[f] == null)
            {
                PlayerPrefs.SetInt("KolvoSohroneniy", PlayerPrefs.GetInt("KolvoSohroneniy") - 1);
            }
        }
    }
    public static void Delete(int Number)
    {
        File.Delete(Application.persistentDataPath + "/SaveSaveMenu" + Number + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
    }
}