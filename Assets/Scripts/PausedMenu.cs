using System;
using System.Collections;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PausedMenu : MonoBehaviour
{
    public GameObject container;
    // Update is called once per frame

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            
            if (container.activeSelf)
            {
                StartCoroutine(LockCursorNextFrame());
            }
            container.SetActive(!container.activeSelf);
            Time.timeScale = container.activeSelf ? 0 : 1;
            
        }
    }
    public void ResumeButton(){
        container.SetActive(false);
        Time.timeScale = 1;
        StartCoroutine(LockCursorNextFrame());
    }

    public void MainMenuButton()
    {
        SceneManager.LoadScene(0);
    }

    public void QuitButton()
    {
        Application.Quit();
    }
    
    private IEnumerator LockCursorNextFrame()
    {
        yield return null; // attend 1 frame
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
