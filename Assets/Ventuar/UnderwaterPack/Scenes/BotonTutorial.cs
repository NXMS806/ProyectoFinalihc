using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BotonTutorial : MonoBehaviour
{
    public int indiceImagen;

    public void MostrarImagen()
    {
        ControladorTutorial.instancia.MostrarSoloUna(indiceImagen);
    }
}
