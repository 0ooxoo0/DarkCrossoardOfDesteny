using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ExitForMenu : MonoBehaviour
{
    public GameObject falsee;
    public GameObject truee;
    public MainMenu menu;

    public void Update()
    {
        if (Input.GetKey(KeyCode.Escape))
        {
            truee.SetActive(true);
            menu.save();
            falsee.SetActive(false);
            //this.gameObject.SetActive(false);
        }
    }

}
