using UnityEngine;
using Photon.Pun;

/// <summary>
/// Conecta el sistema UDP con multijugador de Photon.
/// </summary>
public class UDPMultiplayerAdapter : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Se auto-detecta. Si no, asignar manualmente.")]
    public UDP udpController;
    
    [Tooltip("Se auto-detecta. Si no, asignar manualmente.")]
    public PhoneGyroReceiver gyroController;
    
    [Header("Configuración de Búsqueda")]
    [Tooltip("Cuántas veces intentar buscar al jugador")]
    public int maxRetries = 10;
    
    [Tooltip("Segundos entre cada reintento")]
    public float retryInterval = 0.5f;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    private bool isMultiplayerMode = false;
    private Transform localPlayerTransform;
    private int currentRetry = 0;
    private bool playerFound = false;
    
    void Start()
    {
        // Auto-detectar controladores si no están asignados
        if (udpController == null)
        {
            udpController = GetComponent<UDP>();
        }
        
        if (gyroController == null)
        {
            gyroController = GetComponent<PhoneGyroReceiver>();
        }
        
        // Verificar si estamos en modo multijugador
        isMultiplayerMode = PhotonNetwork.IsConnected;
        
        if (isMultiplayerMode)
        {
            if (showDebugLogs)
            {
                Debug.Log("🎮 UDPMultiplayerAdapter: Modo MULTIJUGADOR detectado");
            }
            
            // Esperar un poco para que Photon instancie el jugador, luego buscar
            currentRetry = 0;
            playerFound = false;
            Invoke("ConnectToLocalPlayer", 0.5f);
        }
        else
        {
            if (showDebugLogs)
            {
                Debug.Log(" UDPMultiplayerAdapter: Modo SINGLE PLAYER - No se requiere adaptación");
            }
        }
    }
    
    /// <summary>
    /// Reconecta el sistema UDP con el jugador local.
    /// Llamar después de reiniciar la partida para asegurar que las referencias estén correctas.
    /// </summary>
    public void ReconnectToPlayer()
    {
        if (showDebugLogs)
        {
            Debug.Log("🔄 UDPMultiplayerAdapter: Reconectando al jugador local...");
        }
        
        // Verificar si ya tenemos la referencia del jugador
        if (localPlayerTransform != null && localPlayerTransform.gameObject != null)
        {
            // Reconectar UDP
            if (udpController != null)
            {
                udpController.objectToMove = localPlayerTransform;
                Debug.Log($"✅ UDP.cs reconectado a: {localPlayerTransform.name}");
            }
            
            // Reconectar Gyro
            if (gyroController != null)
            {
                Camera playerCamera = localPlayerTransform.GetComponentInChildren<Camera>();
                if (playerCamera != null)
                {
                    gyroController.cameraTransform = playerCamera.transform;
                    Debug.Log($"✅ PhoneGyroReceiver.cs reconectado a: {playerCamera.name}");
                }
            }
        }
        else
        {
            // El jugador puede haber sido destruido, buscar de nuevo
            currentRetry = 0;
            playerFound = false;
            ConnectToLocalPlayer();
        }
    }
    
    /// <summary>
    /// Verifica si las conexiones UDP están activas y funcionales.
    /// </summary>
    public bool IsConnected()
    {
        return playerFound && 
               localPlayerTransform != null && 
               udpController != null && 
               udpController.objectToMove != null;
    }
    
    void ConnectToLocalPlayer()
    {
        if (playerFound) return; // Ya encontramos al jugador
        
        currentRetry++;
        
        if (showDebugLogs)
        {
            Debug.Log($" UDPMultiplayerAdapter: Buscando Player1... (intento {currentRetry}/{maxRetries})");
        }
        
        // Buscar todos los objetos con PhotonView en la escena
        PhotonView[] allPlayers = FindObjectsOfType<PhotonView>();
        
        if (showDebugLogs)
        {
            Debug.Log($"   Encontrados {allPlayers.Length} objetos con PhotonView");
        }
        
        foreach (PhotonView pv in allPlayers)
        {
            // Debug: mostrar todos los objetos encontrados
            if (showDebugLogs && currentRetry == 1)
            {
                Debug.Log($"   - {pv.gameObject.name} | IsMine={pv.IsMine}");
            }
            
            // Encontrar el jugador que pertenece a ESTE cliente
            // Y que sea el Jugador 1 (Limpiador con gestos)
            // Buscar "Player1" o "Cleaner" en el nombre
            bool isPlayer1 = pv.gameObject.name.Contains("Player1") || 
                             pv.gameObject.name.Contains("Cleaner") ||
                             pv.gameObject.name.Contains("Limpiador");
            
            if (pv.IsMine && isPlayer1)
            {
                localPlayerTransform = pv.transform;
                playerFound = true;
                
                // CONECTAR UDP.cs con el jugador local
                if (udpController != null)
                {
                    udpController.objectToMove = localPlayerTransform;
                    Debug.Log($" UDP.cs ahora controla: {pv.gameObject.name}");
                    Debug.Log($"    Los botones del teléfono ahora funcionarán para este jugador");
                }
                
                // CONECTAR PhoneGyroReceiver.cs con la cámara del jugador
                if (gyroController != null)
                {
                    Camera playerCamera = localPlayerTransform.GetComponentInChildren<Camera>();
                    
                    if (playerCamera != null)
                    {
                        gyroController.cameraTransform = playerCamera.transform;
                        Debug.Log($" PhoneGyroReceiver.cs ahora controla: {playerCamera.name}");
                    }
                    else
                    {
                        Debug.LogWarning(" No se encontró cámara en el jugador local");
                    }
                }
                
                return; // Encontramos al jugador, salir
            }
        }
        
        // Si no encontramos Player1, verificar si somos Player2
        foreach (PhotonView pv in allPlayers)
        {
            bool isPlayer2 = pv.gameObject.name.Contains("Player2") || 
                             pv.gameObject.name.Contains("Pollut") ||
                             pv.gameObject.name.Contains("Contaminador");
            
            if (pv.IsMine && isPlayer2)
            {
                playerFound = true; // Marcar como encontrado para no seguir buscando
                Debug.Log($" Eres el CONTAMINADOR (Player2): {pv.gameObject.name}");
                Debug.Log($"   Los botones del teléfono NO aplican para este rol.");
                Debug.Log($"   Usa el ratón y teclado para jugar.");
                return;
            }
        }
        
        // Si llegamos aquí, no se encontró ningún jugador
        if (currentRetry < maxRetries)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($" Jugador no encontrado aún, reintentando en {retryInterval}s...");
            }
            Invoke("ConnectToLocalPlayer", retryInterval);
        }
        else
        {
            Debug.LogError(" UDPMultiplayerAdapter: No se encontró jugador local después de todos los intentos");
            Debug.LogError("   Verifica que:");
            Debug.LogError("   1. El prefab se llame 'Player1' o contenga 'Cleaner'");
            Debug.LogError("   2. Estés conectado a Photon correctamente");
            Debug.LogError("   3. Seas el PRIMER jugador en la sala (Player1)");
        }
    }
    
    void OnDestroy()
    {
        // Cancelar cualquier Invoke pendiente
        CancelInvoke();
        
        // Limpiar referencias al salir
        if (udpController != null && udpController.objectToMove == localPlayerTransform)
        {
            udpController.objectToMove = null;
        }
        
        if (gyroController != null && localPlayerTransform != null)
        {
            Camera playerCamera = localPlayerTransform.GetComponentInChildren<Camera>();
            if (playerCamera != null && gyroController.cameraTransform == playerCamera.transform)
            {
                gyroController.cameraTransform = null;
            }
        }
    }
}
