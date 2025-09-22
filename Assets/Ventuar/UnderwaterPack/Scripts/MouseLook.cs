using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 100f;
    public Transform playerBody; // referencia al XR Origin o al "Player"

    private float xRotation = 0f;

    void Start()
    {
        // Bloquear y ocultar el cursor
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Input del mouse
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Rotación vertical (arriba/abajo), acumulada en xRotation
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // limitar para no girar de más

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotación horizontal (izquierda/derecha) en el cuerpo del jugador
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
