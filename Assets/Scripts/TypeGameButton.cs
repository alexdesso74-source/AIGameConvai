using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TypeGameButton : MonoBehaviour
{
    private Button button;
    private GameManager gameManager;
    public int typeGame;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameObject.Find("Game Manager").GetComponent<GameManager>();
        button = GetComponent<Button>();
        button.onClick.AddListener(SetTypeGame);
    }

    void SetTypeGame()
    {
        Debug.Log(button.gameObject.name + " was clicked");
        gameManager.StartGame( typeGame);
    }
}
