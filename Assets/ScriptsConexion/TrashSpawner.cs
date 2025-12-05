using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class TrashSpawner : MonoBehaviourPunCallbacks
{
    [Header("Prefabs de Basura")]
    [Tooltip("Lista de prefabs de basura que se pueden spawnear (deben estar en Resources/)")]
    public string[] trashPrefabNames = new string[]
    {
        "Barrel",
        "Crate01",
        "Treasure Chest"
    };

    [Header("Configuracion de Spawn")]
    [Tooltip("Tecla para spawnear basura aleatoria cerca de Player1")]
    public KeyCode spawnKey = KeyCode.E;

    [Tooltip("Radio aleatorio alrededor del Player1 donde aparecerá la basura")]
    public float spawnRadius = 5f;

    [Tooltip("Altura mínima de spawn (nivel del suelo)")]
    public float minSpawnHeight = 0f;

    [Tooltip("Altura máxima de spawn")]
    public float maxSpawnHeight = 3f;

    [Header("Escala de Objetos")]
    [Tooltip("Escala que se aplicará a los objetos spawneados (3 = 3 veces más grande)")]
    public float spawnScale = 3f;

    [Header("Anti-Superposicion")]
    [Tooltip("Radio mínimo de separación entre objetos spawneados")]
    public float minSeparationRadius = 3f;
    
    [Tooltip("Máximo de intentos para encontrar posición libre")]
    public int maxPlacementAttempts = 10;
    
    [Tooltip("Radio de búsqueda para posición alternativa")]
    public float alternativePositionRadius = 5f;

    [Header("Click Spawn")]
    [Tooltip("Activar spawn con click del mouse")]
    public bool enableClickSpawn = true;

    [Tooltip("Offset vertical desde la altura del Player1 (puede ser negativo para spawnear abajo)")]
    public float clickSpawnHeightOffset = 0f;

    [Tooltip("Mostrar indicador visual donde aparecerá la basura")]
    public bool showSpawnIndicator = true;

    [Tooltip("Color del indicador de spawn")]
    public Color indicatorColor = Color.green;

    [Tooltip("Distancia máxima del raycast")]
    public float maxRaycastDistance = 1000f;

    [Header("Referencias")]
    [Tooltip("Referencia al Transform del Player1 (Limpiador) - se busca automáticamente")]
    private Transform player1Transform;

    [Tooltip("Cámara del Player2 (StrategyCamera) - se asigna automáticamente o manualmente")]
    public Camera strategyCamera;

    [Header("Sistema de Cooldown")]
    [Tooltip("Tiempo de espera entre spawns (segundos)")]
    public float cooldownTime = 2f;

    [Tooltip("Puede spawnear durante el cooldown con tecla E (más permisivo)")]
    public bool allowKeySpawnDuringCooldown = true;

    [Header("Estadisticas")]
    [Tooltip("Contador de basura total spawneada en la partida")]
    public int totalTrashSpawned = 0;

    [Header("Debug")]
    [Tooltip("Mostrar mensajes de debug en consola")]
    public bool showDebugLogs = true;

    // Variables privadas
    private PhotonView photonView;
    private float lastSpawnTime = -999f; // Tiempo del último spawn
    private bool isInCooldown = false;

    void Awake()
    {
        // Obtener PhotonView
        photonView = GetComponent<PhotonView>();
        
        if (photonView == null)
        {
            Debug.LogError("TrashSpawner requiere PhotonView en el mismo GameObject");
        }
        else
        {
            Debug.Log($"[TrashSpawner] AWAKE - GameObject: {gameObject.name}");
            Debug.Log($"[TrashSpawner] PhotonView encontrado - ViewID: {photonView.ViewID}");
        }
    }

    void Start()
    {
        // Debug adicional en Start cuando ya está instanciado en red
        Debug.Log($"[TrashSpawner] START - IsMine: {photonView.IsMine}, Owner: {photonView.Owner?.NickName ?? "null"}");
        
        if (photonView.IsMine)
        {
            Debug.Log("[TrashSpawner] Este TrashSpawner pertenece al jugador LOCAL (Player2)");
        }
        else
        {
            Debug.Log("[TrashSpawner] Este TrashSpawner pertenece a OTRO jugador (no es local)");
        }
        
        // Buscar Player1 en la escena al iniciar
        FindPlayer1();

        // Buscar cámara estratégica del Player2 (aunque esté desactivada)
        if (strategyCamera == null)
        {
            FindStrategyCamera();
        }
    }

    void Update()
    {
        // Solo el dueño del Player2 puede spawnear
        if (!photonView.IsMine)
            return;

        // NUEVO: Solo permitir spawn si la partida está activa
        if (GameMatchManager.Instance != null && !GameMatchManager.Instance.IsMatchActive())
        {
            // Partida no activa, no permitir spawn
            return;
        }

        // Si no tenemos cámara, intentar buscarla de nuevo
        if (strategyCamera == null || !strategyCamera.gameObject.activeInHierarchy)
        {
            FindStrategyCamera();
        }

        // Actualizar estado de cooldown
        UpdateCooldown();

        // FASE 1: Spawn con tecla E (spawn aleatorio cerca de Player1)
        if (Input.GetKeyDown(spawnKey))
        {
            // Permitir spawn con E incluso durante cooldown (opcional)
            if (allowKeySpawnDuringCooldown || !isInCooldown)
            {
                SpawnTrashNearPlayer1();
            }
            else if (showDebugLogs)
            {
                Debug.Log($" Cooldown activo: {GetRemainingCooldown():F1}s");
            }
        }

        // FASE 2: Spawn con click del mouse (spawn preciso donde se hace clic)
        if (enableClickSpawn && Input.GetMouseButtonDown(0))
        {
            // Click spawning SIEMPRE respeta el cooldown
            if (!isInCooldown)
            {
                SpawnTrashAtMousePosition();
            }
            else if (showDebugLogs)
            {
                Debug.Log($" Cooldown activo: {GetRemainingCooldown():F1}s");
            }
        }

        // FASE 3: PODER ESPECIAL - Click derecho para ensuciar la pantalla del Limpiador
        if (Input.GetMouseButtonDown(1)) // 1 = click derecho
        {
            TryUseDirtyScreenPower();
        }
    }

    void OnDrawGizmos()
    {
        // FASE 2: Mostrar indicador visual de dónde aparecerá la basura
        if (!showSpawnIndicator || strategyCamera == null || player1Transform == null)
            return;

        // Solo mostrar si el Player2 es el dueño
        if (photonView != null && !photonView.IsMine)
            return;

        // Raycast desde la posición del mouse (sin LayerMask para detectar todo)
        Ray ray = strategyCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRaycastDistance))
        {
            // Calcular altura de spawn (altura del Player1 + offset)
            float spawnHeight = player1Transform.position.y + clickSpawnHeightOffset;
            Vector3 spawnPosition = new Vector3(hit.point.x, spawnHeight, hit.point.z);
            
            // Dibujar esfera en la posición de spawn
            Gizmos.color = indicatorColor;
            Gizmos.DrawWireSphere(spawnPosition, 1f);
            
            // Dibujar línea desde el suelo hasta el punto de spawn
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(hit.point, spawnPosition);
            
            // Dibujar cruz en el suelo donde hiciste click
            Gizmos.color = Color.red;
            Gizmos.DrawLine(hit.point + Vector3.left * 0.5f, hit.point + Vector3.right * 0.5f);
            Gizmos.DrawLine(hit.point + Vector3.forward * 0.5f, hit.point + Vector3.back * 0.5f);
        }
    }

    /// <summary>
    /// Busca automáticamente el Player1 en la escena
    /// </summary>
    void FindPlayer1()
    {
        GameObject player1Obj = GameObject.Find("Player1(Clone)");
        
        if (player1Obj != null)
        {
            player1Transform = player1Obj.transform;
            if (showDebugLogs)
                Debug.Log(" Player1 encontrado para spawn de basura");
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning(" Player1 no encontrado. Intentando de nuevo en 1 segundo...");
            
            // Reintentar en 1 segundo
            Invoke("FindPlayer1", 1f);
        }
    }

    /// <summary>
    /// Busca automáticamente la cámara estratégica (StrategyCamera) del Player2
    /// BUSCA AUNQUE ESTÉ DESACTIVADA usando Resources.FindObjectsOfTypeAll
    /// </summary>
    void FindStrategyCamera()
    {
        // Buscar TODAS las cámaras (incluso desactivadas)
        Camera[] allCameras = Resources.FindObjectsOfTypeAll<Camera>();
        
        // Buscar específicamente "StrategyCamera"
        foreach (Camera cam in allCameras)
        {
            if (cam.name == "StrategyCamera")
            {
                strategyCamera = cam;
                if (showDebugLogs)
                    Debug.Log($" StrategyCamera encontrada (activa: {cam.gameObject.activeInHierarchy})");
                return;
            }
        }

        // Si no se encuentra, intentar buscar la cámara activa
        if (Camera.main != null)
        {
            strategyCamera = Camera.main;
            if (showDebugLogs)
                Debug.Log(" Usando Camera.main como fallback");
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning(" StrategyCamera no encontrada. Puedes asignarla manualmente en el Inspector.");
        }
    }

    /// <summary>
    /// FASE 1: Spawner basura cerca del Player1 de forma aleatoria
    /// </summary>
    void SpawnTrashNearPlayer1()
    {
        // Verificar que tenemos Player1
        if (player1Transform == null)
        {
            if (showDebugLogs)
                Debug.LogWarning(" No se puede spawnear: Player1 no encontrado");
            return;
        }

        // Verificar que hay prefabs configurados
        if (trashPrefabNames.Length == 0)
        {
            Debug.LogError(" No hay prefabs de basura configurados");
            return;
        }

        // Elegir un prefab aleatorio
        string randomPrefab = trashPrefabNames[Random.Range(0, trashPrefabNames.Length)];

        // Calcular posición aleatoria cerca del Player1
        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
        randomOffset.y = Random.Range(minSpawnHeight, maxSpawnHeight); // Altura controlada

        Vector3 desiredPosition = player1Transform.position + randomOffset;
        
        // ANTI-SUPERPOSICIÓN: Buscar posición válida sin colisiones
        Vector3 spawnPosition = FindValidSpawnPosition(desiredPosition);

        // Rotación aleatoria para variedad visual
        Quaternion randomRotation = Random.rotation;

        // SPAWN - PhotonNetwork.Instantiate YA sincroniza automáticamente
        // NO usar RPC porque causaba spawn duplicado (una vez por cada cliente)
        InstantiateTrashDirect(randomPrefab, spawnPosition, randomRotation);

        // Registrar spawn para cooldown y estadísticas
        RegisterSpawn();

        if (showDebugLogs)
            Debug.Log($" Spawneando '{randomPrefab}' en {spawnPosition} | Total: {totalTrashSpawned}");
    }

    /// <summary>
    /// Instancia el objeto de basura directamente usando PhotonNetwork.Instantiate.
    /// PhotonNetwork.Instantiate ya sincroniza automaticamente en todos los clientes.
    /// </summary>
    void InstantiateTrashDirect(string prefabName, Vector3 position, Quaternion rotation)
    {
        GameObject trash = null;
        
        if (PhotonNetwork.IsConnected)
        {
            // Usar PhotonNetwork.Instantiate para sincronización
            trash = PhotonNetwork.Instantiate(
                prefabName, 
                position, 
                rotation
            );
        }
        else
        {
            // Modo offline: usar Resources.Load
            GameObject prefab = Resources.Load<GameObject>(prefabName);
            if (prefab != null)
            {
                trash = Instantiate(prefab, position, rotation);
            }
            else
            {
                Debug.LogError($" No se encontró prefab '{prefabName}' en Resources/");
                return;
            }
        }
        
        // Marcar basura como spawneada por Polluter y aplicar escala
        if (trash != null)
        {
            // APLICAR ESCALA al objeto spawneado (localmente)
            trash.transform.localScale = Vector3.one * spawnScale;
            
            PhotonView trashPV = trash.GetComponent<PhotonView>();
            
            if (trashPV != null && PhotonNetwork.IsConnected)
            {
                // SINCRONIZAR escala y spawnedByPolluter en TODOS los clientes
                // Usamos AllBuffered para que nuevos clientes también reciban la info
                photonView.RPC("RPC_SetTrashProperties", RpcTarget.AllBuffered, trashPV.ViewID, spawnScale, true);
                
                if (showDebugLogs)
                    Debug.Log($" Basura sincronizada: ViewID={trashPV.ViewID} | Escala={spawnScale} | spawnedByPolluter=true");
            }
            else
            {
                // Modo offline: marcar directamente
                TrashObject trashComponent = trash.GetComponent<TrashObject>();
                if (trashComponent != null)
                {
                    trashComponent.spawnedByPolluter = true;
                }
            }
            
            // Notificar al GameMatchManager (solo el que spawnea)
            if (GameMatchManager.Instance != null)
            {
                GameMatchManager.Instance.OnTrashSpawned();
            }
        }
    }

    /// <summary>
    /// RPC para sincronizar escala y propiedad spawnedByPolluter en todos los clientes.
    /// </summary>
    [PunRPC]
    void RPC_SetTrashProperties(int viewID, float scale, bool isFromPolluter)
    {
        PhotonView targetView = PhotonView.Find(viewID);
        if (targetView != null)
        {
            // Aplicar escala
            targetView.transform.localScale = Vector3.one * scale;
            
            // Marcar como spawneada por Polluter
            TrashObject trashComponent = targetView.GetComponent<TrashObject>();
            if (trashComponent != null)
            {
                trashComponent.spawnedByPolluter = isFromPolluter;
                
                if (showDebugLogs)
                    Debug.Log($" [RPC] Basura sincronizada: ViewID={viewID} | Escala={scale} | spawnedByPolluter={isFromPolluter}");
            }
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning($" [RPC] No se encontró objeto con ViewID {viewID}");
        }
    }



    /// <summary>
    /// </summary>
    /// <param name="desiredPosition">Posición deseada original</param>
    /// <returns>Posición válida sin superposición</returns>
    Vector3 FindValidSpawnPosition(Vector3 desiredPosition)
    {
        // Intentar primero la posición original
        if (!IsPositionOccupied(desiredPosition))
        {
            return desiredPosition;
        }

        if (showDebugLogs)
            Debug.Log($" Posición original ocupada, buscando alternativa...");

        // BÚSQUEDA EN ANILLOS CONCÉNTRICOS
        // Empezamos cerca y expandimos el radio hasta encontrar espacio libre
        float currentRadius = minSeparationRadius;
        float maxSearchRadius = alternativePositionRadius * 5f; // Expandir hasta 5x el radio inicial
        int pointsPerRing = 8; // Puntos a probar en cada anillo

        while (currentRadius <= maxSearchRadius)
        {
            // Probar puntos equidistantes en el anillo actual
            for (int i = 0; i < pointsPerRing; i++)
            {
                // Calcular ángulo para distribución uniforme
                float angle = (360f / pointsPerRing) * i;
                // Agregar un poco de variación aleatoria al ángulo
                angle += Random.Range(-15f, 15f);
                
                float angleRad = angle * Mathf.Deg2Rad;
                
                Vector3 candidatePosition = new Vector3(
                    desiredPosition.x + Mathf.Cos(angleRad) * currentRadius,
                    desiredPosition.y,
                    desiredPosition.z + Mathf.Sin(angleRad) * currentRadius
                );

                // Verificar si esta posición está libre
                if (!IsPositionOccupied(candidatePosition))
                {
                    if (showDebugLogs)
                        Debug.Log($" Posición libre encontrada en anillo radio={currentRadius:F1}: {candidatePosition}");
                    return candidatePosition;
                }
            }

            // Expandir al siguiente anillo
            currentRadius += minSeparationRadius;
            // Aumentar puntos en anillos más grandes para mejor cobertura
            pointsPerRing += 4;
            
            if (showDebugLogs && currentRadius <= maxSearchRadius)
                Debug.Log($" Anillo radio={currentRadius - minSeparationRadius:F1} lleno, expandiendo a radio={currentRadius:F1}");
        }

        // FALLBACK: Si aún no encontramos, buscar en dirección aleatoria muy lejos
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        float farDistance = maxSearchRadius + minSeparationRadius * 2f;
        Vector3 farPosition = new Vector3(
            desiredPosition.x + randomDir.x * farDistance,
            desiredPosition.y,
            desiredPosition.z + randomDir.y * farDistance
        );

        if (showDebugLogs)
            Debug.LogWarning($" Área muy congestionada. Spawneando lejos en: {farPosition}");

        return farPosition;
    }

    /// <summary>
    /// Verifica si una posición está ocupada por otro objeto de basura
    /// </summary>
    bool IsPositionOccupied(Vector3 position)
    {
        // Usar OverlapSphere para detectar objetos cercanos
        Collider[] nearbyColliders = Physics.OverlapSphere(position, minSeparationRadius);

        foreach (Collider col in nearbyColliders)
        {
            // Verificar si es un objeto de basura (tiene TrashObject o tag "Trash")
            if (col.GetComponent<TrashObject>() != null || col.CompareTag("Trash"))
            {
                return true; // Posición ocupada
            }
        }

        return false; // Posición libre
    }


    /// <summary>
    /// FASE 2: Spawner basura en la posición donde se hace clic con el mouse
    /// Usa raycast para detectar la superficie y spawner a la altura del Player1
    /// </summary>
    void SpawnTrashAtMousePosition()
    {
        // Verificar que tenemos la cámara Y que está activa
        if (strategyCamera == null || !strategyCamera.gameObject.activeInHierarchy)
        {
            // No mostrar warning constante - ya se maneja en Update
            return;
        }

        // Verificar que tenemos Player1
        if (player1Transform == null)
        {
            if (showDebugLogs)
                Debug.LogWarning(" No se puede spawnear: Player1 no encontrado");
            return;
        }

        // Verificar que hay prefabs configurados
        if (trashPrefabNames.Length == 0)
        {
            Debug.LogError(" No hay prefabs de basura configurados");
            return;
        }

        // Crear raycast desde la posición del mouse
        Ray ray = strategyCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (showDebugLogs)
            Debug.Log($" Raycast desde mouse pos: {Input.mousePosition}, Ray: {ray.origin} -> {ray.direction}");

        // Hacer raycast SIN LayerMask para detectar CUALQUIER superficie
        if (Physics.Raycast(ray, out hit, maxRaycastDistance))
        {
            // Elegir un prefab aleatorio
            string randomPrefab = trashPrefabNames[Random.Range(0, trashPrefabNames.Length)];

            // IMPORTANTE: Calcular posición usando la altura del Player1
            // Esto asegura que la basura aparezca a la misma altura que el jugador
            float spawnHeight = player1Transform.position.y + clickSpawnHeightOffset;
            Vector3 desiredPosition = new Vector3(hit.point.x, spawnHeight, hit.point.z);
            
            // ANTI-SUPERPOSICIÓN: Buscar posición válida sin colisiones
            Vector3 spawnPosition = FindValidSpawnPosition(desiredPosition);

            // Rotación aleatoria para variedad visual
            Quaternion randomRotation = Random.rotation;

            // SPAWN - PhotonNetwork.Instantiate YA sincroniza automáticamente
            // NO usar RPC porque causaba spawn duplicado (una vez por cada cliente)
            InstantiateTrashDirect(randomPrefab, spawnPosition, randomRotation);

            // Registrar spawn para cooldown y estadísticas
            RegisterSpawn();

            if (showDebugLogs)
                Debug.Log($" Click spawn SUCCESS: '{randomPrefab}' en {spawnPosition} | Superficie: {hit.collider.name} | Total: {totalTrashSpawned}");
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning($" Click NO detectó superficie | Mouse: {Input.mousePosition} | Camera: {strategyCamera.name}");
        }
    }

    
    /// <summary>
    /// Actualiza el estado del cooldown
    /// </summary>
    void UpdateCooldown()
    {
        if (isInCooldown)
        {
            if (Time.time >= lastSpawnTime + cooldownTime)
            {
                isInCooldown = false;
            }
        }
    }

    /// <summary>
    /// Registra un spawn y activa el cooldown
    /// </summary>
    void RegisterSpawn()
    {
        lastSpawnTime = Time.time;
        isInCooldown = true;
        totalTrashSpawned++;
    }

    /// <summary>
    /// Obtiene el tiempo restante del cooldown
    /// </summary>
    public float GetRemainingCooldown()
    {
        if (!isInCooldown) return 0f;
        float remaining = cooldownTime - (Time.time - lastSpawnTime);
        return Mathf.Max(0f, remaining);
    }

    /// <summary>
    /// Obtiene el progreso del cooldown (0 a 1)
    /// </summary>
    public float GetCooldownProgress()
    {
        if (!isInCooldown) return 1f;
        float elapsed = Time.time - lastSpawnTime;
        return Mathf.Clamp01(elapsed / cooldownTime);
    }

    /// <summary>
    /// Verifica si puede spawnear (para uso externo)
    /// </summary>
    public bool CanSpawn()
    {
        return !isInCooldown;
    }
    
    /// <summary>
    /// Intenta usar el poder de ensuciar pantalla al hacer click derecho.
    /// </summary>
    void TryUseDirtyScreenPower()
    {
        // Verificar si el sistema DirtyScreenEffect existe
        if (DirtyScreenEffect.Instance == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("� DirtyScreenEffect no encontrado en la escena");
            return;
        }
        
        // Intentar activar el efecto (tiene su propio cooldown)
        bool success = DirtyScreenEffect.Instance.TryActivateDirtyEffect();
        
        if (success && showDebugLogs)
        {
            Debug.Log(" ¡Poder de suciedad activado!");
        }
    }

    /// <summary>
    /// Resetea el contador de basura spawneada al reiniciar partida.
    /// </summary>
    public void ResetSpawnCount()
    {
        totalTrashSpawned = 0;
        isInCooldown = false;
        lastSpawnTime = -999f;
        
        if (showDebugLogs)
            Debug.Log(" [TrashSpawner] Contador de spawn reseteado a 0");
    }
}
