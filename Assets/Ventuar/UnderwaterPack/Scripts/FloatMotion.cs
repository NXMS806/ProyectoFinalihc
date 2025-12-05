using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatMotion : MonoBehaviour
{
    // Amplitud del movimiento (qué tan alto/bajo se mueve)
    public float amplitude = 0.5f;
    // Velocidad del movimiento
    public float frequency = 1f;
    // Para guardar la posición inicial
    private Vector3 startPos;

    void Start()
    {
        // Guardamos la posición inicial del objeto
        startPos = transform.position;
    }

    void Update()
    {
        // Movimiento vertical usando seno
        float newY = startPos.y + Mathf.Sin(Time.time * frequency) * amplitude;

        // Movimiento opcional lateral con coseno (efecto más marino)
        float newX = startPos.x + Mathf.Cos(Time.time * frequency * 0.5f) * (amplitude / 4f);

        // Aplicamos la nueva posición
        transform.position = new Vector3(newX, newY, startPos.z);
    }
}
