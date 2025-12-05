using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    public GameObject mainMenu;
    public GameObject demoScene;
    public Camera menuCamera;     

    public void StartGame()
    {
        mainMenu.SetActive(false);

        if (menuCamera != null)
            menuCamera.gameObject.SetActive(false);

        demoScene.SetActive(true);

        Camera gameCamera = demoScene.GetComponentInChildren<Camera>(true);
        if (gameCamera != null)
            gameCamera.gameObject.SetActive(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
