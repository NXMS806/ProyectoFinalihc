using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMov : MonoBehaviour
{
    public float speed = 3.0f;       // Velocidad de movimiento
    public float gravity = -9.81f;   // Gravedad
    public float jumpHeight = 1.0f;  // Altura de salto

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Input de movimiento en X/Z (WASD o flechas)
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // Movimiento relativo a la orientación del jugador
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        controller.Move(move * speed * Time.deltaTime);

        // Aplicar gravedad
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // mantiene pegado al suelo
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Salto con espacio
        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
}
