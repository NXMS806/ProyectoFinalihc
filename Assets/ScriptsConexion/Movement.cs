using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Movement : MonoBehaviourPunCallbacks
{
    [Header("Movement Settings")]
    public float velocidad = 5;
    public float rotationSpeed = 10f;

    [Header("Rango Multijugador")]
    public float maxDistanciaDeP1 = 30f;
    private Transform player1Transform;

    [Header("Control Mode")]
    [Tooltip("Si es TRUE, usa gestos/celular (UDP). Si es FALSE, usa WASD (para testing)")]
    public bool useGestureControl = true; 
    
    [Header("Player2 Settings")]
    [Tooltip("TRUE si este es Player2 (movimiento 2D). Se detecta automáticamente.")]
    public bool isPlayer2 = false;
    
    private UDP gestureController;
    private PhoneGyroReceiver gyroController;
    
    void Start()
    {
        if (!photonView.IsMine) return;
        
        string playerName = gameObject.name;
        Debug.Log($" Movement Start: GameObject name = '{playerName}'");
        
        if (playerName.Contains("Player2"))
        {
            isPlayer2 = true;
            useGestureControl = false; 
            Debug.Log(" Movement: Player2 detectado - Movimiento 2D activado");
        }
        else if (playerName.Contains("Player1"))
        {
            isPlayer2 = false;
            Debug.Log(" Movement: Player1 detectado - Movimiento 3D activado");
        }
        
        gestureController = FindObjectOfType<UDP>();
        gyroController = FindObjectOfType<PhoneGyroReceiver>();
        
        if (useGestureControl)
        {
            if (gestureController == null && gyroController == null)
            {
                Debug.LogWarning(" Movement: No se encontró UDP/Gyro, cambiando a WASD...");
                useGestureControl = false;
            }
            else
            {
                Debug.Log(" Movement: Control por gestos/celular activado");
            }
        }
        else
        {
            Debug.Log(" Movement: Control por teclado WASD activado");
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;
        
        if (useGestureControl)
        {
            HandleGestureMovement();
        }
        else
        {
            HandleKeyboardMovement();
        }
    }
    
    void BuscarPlayer1()
    {
        Movement[] allPlayers = FindObjectsOfType<Movement>();

        foreach (Movement m in allPlayers)
        {
            if (m.gameObject.name.Contains("Player1"))
            {
                player1Transform = m.transform;
                Debug.Log(" P1 Encontrado por NOMBRE: " + m.gameObject.name);
                break;
            }
        }
    }

    void HandleKeyboardMovement()
    {
        float movHorizontal = Input.GetAxis("Horizontal");
        float movVertical = Input.GetAxis("Vertical");

        Vector3 desplazamiento;

        if (isPlayer2)
        {
            if (movHorizontal == 0 && movVertical == 0) return;

            Vector3 targetPos = transform.position;
            targetPos.x += movHorizontal * velocidad * Time.deltaTime;
            targetPos.z += movVertical * velocidad * Time.deltaTime;

            if (player1Transform == null) BuscarPlayer1();

            if (player1Transform != null)
            {
                float limiteReal = maxDistanciaDeP1;
                if (limiteReal <= 0.1f) limiteReal = 30f; 

                Vector3 p1Flat = player1Transform.position;
                p1Flat.y = 0;

                Vector3 targetFlat = targetPos;
                targetFlat.y = 0;

                Vector3 directionFlat = targetFlat - p1Flat;
                float currentDist = directionFlat.magnitude;

                if (currentDist > limiteReal)
                {
                    Vector3 clampedFlat = p1Flat + (directionFlat.normalized * limiteReal);

                    targetPos.x = clampedFlat.x;
                    targetPos.z = clampedFlat.z;
                }
            }

            transform.position = targetPos;
        }
        else
        {
            desplazamiento = new Vector3(movHorizontal, 0, movVertical) * velocidad * Time.deltaTime;
            transform.Translate(desplazamiento);
        }
    }

    private void OnDrawGizmos()
    {
        if (player1Transform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(player1Transform.position, maxDistanciaDeP1);
        }
    }

    void HandleGestureMovement()
    {
       
    }
}