using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PageButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject fromUI;
    [SerializeField] private GameObject toUI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        button.onClick.AddListener(() =>
        {
            fromUI.SetActive(false);
            toUI.SetActive(true);
        });
    }
    
}
