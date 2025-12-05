using UnityEngine;
using Photon.Pun;

public class StrategyCameraFollow : MonoBehaviour
{
    [Header(" Configuración de Cámara Ortográfica")]
    [Tooltip("Tamaño ortográfico inicial (zoom). Menor = más cerca, Mayor = más lejos")]
    [Range(10f, 150f)]
    public float orthographicSize = 40f;
    
    [Tooltip("Altura de la cámara sobre el jugador (Y offset)")]
    public float offsetY = 80f;
    
    [Tooltip("Suavizado del movimiento (más alto = más suave)")]
    [Range(0.1f, 10f)]
    public float smoothSpeed = 8f;
    
    [Tooltip("Rotación de la cámara (mirando hacia abajo)")]
    public Vector3 cameraRotation = new Vector3(90, 0, 0);
    
    [Header("� Configuración de Zoom")]
    [Tooltip("Velocidad del zoom con scroll del ratón")]
    public float zoomSpeed = 10f;
    
    [Tooltip("Zoom mínimo (más cercano)")]
    public float minZoom = 15f;
    
    [Tooltip("Zoom máximo (más lejano)")]
    public float maxZoom = 100f;
    
    [Header(" Ocultar Player2 Local")]
    [Tooltip("Ocultar el modelo del Player2 local para que no tape la vista")]
    public bool hideLocalPlayer = true;
    
    [Header(" Debug")]
    public bool showDebugLogs = true;
    
    // Referencias privadas
    private Transform targetPlayer;
    private bool isFollowing = false;
    private Camera cam;
    private GameObject localPlayerModel;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        
        if (cam == null)
        {
            Debug.LogError(" StrategyCameraFollow: No se encontró componente Camera!");
            return;
        }
        
        // Configurar cámara ortográfica básica
        cam.orthographic = true;
        cam.orthographicSize = orthographicSize;
        
        if (showDebugLogs)
        {
            Debug.Log(" StrategyCameraFollow: Inicializando seguimiento 2D...");
        }
        
        // Establecer rotación fija (mirando hacia abajo)
        transform.rotation = Quaternion.Euler(cameraRotation);
        
        // Esperar 2 segundos para que Photon instancie los jugadores
        Invoke("FindLocalPlayer2", 2f);
    }
    
    void FindLocalPlayer2()
    {
        // Solo en multijugador
        if (!PhotonNetwork.IsConnected)
        {
            if (showDebugLogs)
            {
                Debug.Log(" StrategyCameraFollow: Modo single player - No se requiere seguimiento");
            }
            return;
        }
        
        // Buscar todos los jugadores en la escena
        PhotonView[] allPlayers = FindObjectsOfType<PhotonView>();
        
        foreach (PhotonView pv in allPlayers)
        {
            if (pv.IsMine)
            {
                string playerName = pv.gameObject.name;
                int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
                
                // DETECTAR PLAYER2 por nombre
                if (playerName.Contains("Player2"))
                {
                    targetPlayer = pv.transform;
                    localPlayerModel = pv.gameObject;
                    isFollowing = true;
                    
                    // Ocultar modelo local si está activado
                    if (hideLocalPlayer)
                    {
                        HideLocalPlayerModel();
                    }
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($" StrategyCameraFollow: Player2 detectado. Siguiendo jugador en vista 2D.");
                    }
                    return;
                }
                else if (playerName.Contains("Player1"))
                {
                    if (showDebugLogs)
                    {
                        Debug.Log($" StrategyCameraFollow: Player1 detectado - No requiere seguimiento 2D");
                    }
                    return;
                }
                // Fallback por ActorNumber
                else if (actorNumber == 2)
                {
                    targetPlayer = pv.transform;
                    localPlayerModel = pv.gameObject;
                    isFollowing = true;
                    
                    if (hideLocalPlayer)
                    {
                        HideLocalPlayerModel();
                    }
                    
                    if (showDebugLogs)
                    {
                        Debug.LogWarning($" StrategyCameraFollow: Usando ActorNumber {actorNumber}. Siguiendo jugador.");
                    }
                    return;
                }
                else if (actorNumber == 1)
                {
                    if (showDebugLogs)
                    {
                        Debug.Log($" StrategyCameraFollow: ActorNumber {actorNumber} (LIMPIADOR) - No seguimiento 2D");
                    }
                    return;
                }
            }
        }
        
        // Si no se encontró, reintentar
        if (!isFollowing)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning(" StrategyCameraFollow: Buscando Player2... (reintentar en 1s)");
            }
            Invoke("FindLocalPlayer2", 1f);
        }
    }
    
    void LateUpdate()
    {
        if (!isFollowing || targetPlayer == null)
            return;
        
        // Calcular posición deseada (directamente arriba del jugador)
        Vector3 desiredPosition = new Vector3(
            targetPlayer.position.x,
            targetPlayer.position.y + offsetY,
            targetPlayer.position.z
        );
        
        // Interpolar suavemente hacia la posición deseada
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // Aplicar la nueva posición
        transform.position = smoothedPosition;
        
        // Mantener rotación fija mirando hacia abajo
        transform.rotation = Quaternion.Euler(cameraRotation);
    }
    
    void Update()
    {
        // ZOOM con rueda del ratón
        if (isFollowing && cam != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                orthographicSize -= scroll * zoomSpeed;
                orthographicSize = Mathf.Clamp(orthographicSize, minZoom, maxZoom);
                cam.orthographicSize = orthographicSize;
                
                if (showDebugLogs)
                    Debug.Log($"� Zoom: {orthographicSize:F1}");
            }
        }
    }
    
    /// <summary>
    /// Oculta el modelo visual del Player2 local para que no tape la vista de la cámara.
    /// </summary>
    void HideLocalPlayerModel()
    {
        if (localPlayerModel == null) return;
        
        // Ocultar MeshRenderers
        MeshRenderer[] renderers = localPlayerModel.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            renderer.enabled = false;
        }
        
        // Ocultar SkinnedMeshRenderers
        SkinnedMeshRenderer[] skinnedRenderers = localPlayerModel.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            renderer.enabled = false;
        }
        
        if (showDebugLogs)
            Debug.Log($"� Player2 local ocultado - {renderers.Length + skinnedRenderers.Length} renderers desactivados");
    }
    
    /// <summary>
    /// Muestra el modelo del Player2 local
    /// </summary>
    public void ShowLocalPlayerModel()
    {
        if (localPlayerModel == null) return;
        
        MeshRenderer[] renderers = localPlayerModel.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            renderer.enabled = true;
        }
        
        SkinnedMeshRenderer[] skinnedRenderers = localPlayerModel.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            renderer.enabled = true;
        }
        
        if (showDebugLogs)
            Debug.Log($"� Player2 local visible de nuevo");
    }
    
    /// <summary>
    /// Permite cambiar el objetivo manualmente
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        targetPlayer = newTarget;
        isFollowing = (newTarget != null);
    }
    
    /// <summary>
    /// Detiene el seguimiento
    /// </summary>
    public void StopFollowing()
    {
        isFollowing = false;
        targetPlayer = null;
    }
    
    /// <summary>
    /// Ajusta el zoom programáticamente
    /// </summary>
    public void SetZoom(float newSize)
    {
        orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
        if (cam != null)
        {
            cam.orthographicSize = orthographicSize;
        }
    }
}
