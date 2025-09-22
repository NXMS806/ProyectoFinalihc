using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cajaflotante : MonoBehaviour
{
    public float amplitudY = 0.5f;    
    public float velocidadY = 2f;     

    public float amplitudRot = 5f;    
    public float velocidadRot = 1.5f; 

    private Vector3 posicionInicial;

    void Start()
    {
        posicionInicial = transform.position;
    }

    void Update()
    {
        float nuevaY = posicionInicial.y + Mathf.Sin(Time.time * velocidadY) * amplitudY;

        float rotX = Mathf.Sin(Time.time * velocidadRot) * amplitudRot;
        float rotZ = Mathf.Cos(Time.time * velocidadRot * 0.8f) * amplitudRot;

        transform.position = new Vector3(transform.position.x, nuevaY, transform.position.z);
        transform.rotation = Quaternion.Euler(rotX, 0f, rotZ);
    }
}