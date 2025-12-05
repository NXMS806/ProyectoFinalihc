using UnityEngine;
using Photon.Pun;

public class CameraFollowPlayer : MonoBehaviour
{
    [Header("Configuración de Seguimiento")]
    [Tooltip("Distancia de la cámara respecto al jugador (atrás y arriba)")]
    public Vector3 offset = new Vector3(0, 5, -10);
    
    [Tooltip("Suavizado del movimiento (más alto = más suave)")]
    [Range(0.1f, 10f)]
    public float smoothSpeed = 5f;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    // Referencias privadas
    private Transform targetPlayer;
    private bool isFollowing = false;
    
    void Start()
    {
        if (showDebugLogs)
        {
            Debug.Log("CameraFollowPlayer: Inicializando seguimiento...");
        }
        
        // Esperar 2 segundos para que Photon instancie los jugadores
        Invoke("FindLocalPlayer", 2f);
    }
    
    void FindLocalPlayer()
    {
        // Solo en multijugador
        if (!PhotonNetwork.IsConnected)
        {
            if (showDebugLogs)
            {
                Debug.Log("CameraFollowPlayer: Modo single player - No se requiere seguimiento");
            }
            return;
        }
        
        // Buscar todos los jugadores en la escena
        PhotonView[] allPlayers = FindObjectsOfType<PhotonView>();
        
        foreach (PhotonView pv in allPlayers)
        {
            // Encontrar el jugador que es MÍO
            if (pv.IsMine)
            {
                string playerName = pv.gameObject.name;
                int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
                
                // SOLUCIÓN DEFINITIVA: Detectar Player1 por nombre primero
                if (playerName.Contains("Player1"))
                {
                    // SOY PLAYER1 - Necesito seguimiento de cámara
                    targetPlayer = pv.transform;
                    isFollowing = true;
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"CameraFollowPlayer: Player1 detectado. Siguiendo jugador.");
                    }
                    return;
                }
                else if (playerName.Contains("Player2"))
                {
                    // SOY PLAYER2 - NO necesito seguimiento (cámara ortográfica fija)
                    if (showDebugLogs)
                    {
                        Debug.Log($"CameraFollowPlayer: Player2 detectado (CONTAMINADOR) - No requiere seguimiento 3D");
                    }
                    return;
                }
                // Fallback por ActorNumber
                else if (actorNumber == 1)
                {
                    // BACKUP: Soy primer jugador - seguir
                    targetPlayer = pv.transform;
                    isFollowing = true;
                    
                    if (showDebugLogs)
                    {
                        Debug.LogWarning($"CameraFollowPlayer: Usando ActorNumber {actorNumber}. Siguiendo jugador.");
                    }
                    return;
                }
                else if (actorNumber == 2)
                {
                    // BACKUP: Soy segundo jugador - NO seguir
                    if (showDebugLogs)
                    {
                        Debug.Log($"CameraFollowPlayer: ActorNumber {actorNumber} (CONTAMINADOR) - No seguimiento");
                    }
                    return;
                }
            }
        }
        
        // Si no se encontró, reintentar (solo mostrar warning cada 3 segundos)
        if (!isFollowing)
        {
            if (showDebugLogs && Time.frameCount % 180 == 0)
            {
                Debug.LogWarning("CameraFollowPlayer: Buscando Player1... (reintentar en 1s)");
            }
            
            Invoke("FindLocalPlayer", 1f);
        }
    }
    
    void LateUpdate()
    {
        // Solo seguir si tenemos un objetivo y estamos en modo seguimiento
        if (!isFollowing || targetPlayer == null)
            return;
        
        // Calcular posición deseada
        Vector3 desiredPosition = targetPlayer.position + targetPlayer.TransformDirection(offset);
        
        // Interpolar suavemente hacia la posición deseada
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // Aplicar la nueva posición
        transform.position = smoothedPosition;
        
        // Mirar hacia el jugador
        transform.LookAt(targetPlayer.position + Vector3.up * 2f); // Mirar un poco arriba del jugador
    }
    
    /// <summary>
    /// Permite cambiar el objetivo manualmente (útil para debugging)
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        targetPlayer = newTarget;
        isFollowing = (newTarget != null);
        
        if (showDebugLogs)
        {
            Debug.Log($"CameraFollowPlayer: Nuevo objetivo asignado: {newTarget?.name ?? "None"}");
        }
    }
    
    /// <summary>
    /// Detiene el seguimiento
    /// </summary>
    public void StopFollowing()
    {
        isFollowing = false;
        targetPlayer = null;
        
        if (showDebugLogs)
        {
            Debug.Log("CameraFollowPlayer: Seguimiento detenido");
        }
    }
}
