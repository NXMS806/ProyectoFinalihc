using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControladorTutorial : MonoBehaviour
{
    public static ControladorTutorial instancia;

    [Header("Imágenes del tutorial")]
    public GameObject[] imagenes;

    void Awake()
    {
        instancia = this;
    }

    public void MostrarSoloUna(int indice)
    {
        for (int i = 0; i < imagenes.Length; i++)
        {
            imagenes[i].SetActive(i == indice);
        }
    }

    public void OcultarTodo()
    {
        foreach (GameObject img in imagenes)
        {
            img.SetActive(false);
        }
    }

}
