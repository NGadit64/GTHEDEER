using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Nama Scene Game")]
    public string gameSceneName = "Game";

    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void ExitGame()
    {
        Debug.Log("Exit Game");

        // Berfungsi saat game sudah di-build
        Application.Quit();
    }
}