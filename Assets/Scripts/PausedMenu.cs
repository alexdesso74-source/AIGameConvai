using System.ComponentModel;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PausedMenu : MonoBehaviour
{
    public GameObject pauseMenu;
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            pauseMenu.SetActive(!pauseMenu.activeSelf);
            Time.timeScale = pauseMenu.activeSelf ? 0 : 1;
        }    
    }
    public void ResumeButton(){
        pauseMenu.SetActive(false);
        Time.timeScale = 1;
    }

    public void MainMenuButton()
    {
        SceneManager.LoadScene(0);
    }

    public void QuitButton()
    {
        Application.Quit();
    }
}
