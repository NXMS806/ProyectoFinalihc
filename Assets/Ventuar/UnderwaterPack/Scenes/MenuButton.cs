using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButton : MonoBehaviour
{
    public void IniciarJuego()
    {
        SceneManager.LoadScene("Juego");
    }

    public void IrATutorial()
    {
        SceneManager.LoadScene("Tutorial");
    }

    public void Salir()
    {
        Application.Quit();
        Debug.Log("Juego cerrado.");
    }
}
