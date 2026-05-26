using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public GameObject titleScreen;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void StartGame(int type)
    {
        //var convaiCharacter = GameObject.Find("ConvaiCharacter");
        //convaiCharacter.SetActive(true);
        SceneManager.LoadScene(type);
        titleScreen.SetActive(false);
    }
    
    public void QuitButton()
    {
        Application.Quit();
    }
}
