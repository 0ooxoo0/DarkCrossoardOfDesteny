using UnityEngine;
using UnityEngine.SceneManagement;

public class ReloadGame : MonoBehaviour
{
public void Restart()
    {
        SceneManager.LoadScene(0);
    }
}
