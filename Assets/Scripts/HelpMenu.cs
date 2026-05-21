using UnityEngine;

public class HelpMenu : MonoBehaviour
{
    public GameObject pauseMenu;
    public GameObject container;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9) && !pauseMenu.activeSelf)
        {
            container.SetActive(!container.activeSelf);
            
        }

        if (pauseMenu.activeSelf && container.activeSelf)
        {
            container.SetActive(!container.activeSelf);
        }
    }
}
