using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Tooltip("Name of the scene to load")]
    public string sceneName;

    public void LoadScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
    }
}
