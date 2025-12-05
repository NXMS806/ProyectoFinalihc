using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TrashSpawnerUI : MonoBehaviour
{
    [Header(" Referencias")]
    [Tooltip("Referencia al TrashSpawner para obtener datos")]
    public TrashSpawner trashSpawner;

    [Header("� UI Elements - Contador")]
    [Tooltip("Texto que muestra el contador de basura spawneada")]
    public TextMeshProUGUI trashCounterText;

    [Tooltip("Formato del texto del contador (use {0} para el número)")]
    public string counterFormat = "Basura Spawneada: {0}";

    [Header("� UI Elements - Cooldown Spawn")]
    [Tooltip("Imagen de relleno para la barra de cooldown (usará Width en lugar de Fill)")]
    public Image cooldownFillBar;

    [Tooltip("RectTransform de la barra de cooldown (se asigna automáticamente)")]
    private RectTransform cooldownFillRect;

    [Tooltip("Ancho máximo de la barra de cooldown")]
    private float maxBarWidth = 200f;

    [Tooltip("Texto que muestra el tiempo restante del cooldown")]
    public TextMeshProUGUI cooldownText;

    [Tooltip("Color cuando puede spawnear")]
    public Color readyColor = Color.green;

    [Tooltip("Color durante el cooldown")]
    public Color cooldownColor = Color.red;

    [Header("� UI Elements - Cooldown Poder Suciedad")]
    [Tooltip("Imagen de relleno para la barra de cooldown del poder de suciedad")]
    public Image dirtyPowerFillBar;

    [Tooltip("RectTransform de la barra de poder suciedad (se asigna automáticamente)")]
    private RectTransform dirtyPowerFillRect;

    [Tooltip("Ancho máximo de la barra de poder suciedad")]
    private float maxDirtyBarWidth = 200f;

    [Tooltip("Texto que muestra el estado del poder de suciedad")]
    public TextMeshProUGUI dirtyPowerText;

    [Tooltip("Color cuando el poder está listo")]
    public Color dirtyReadyColor = new Color(0.6f, 0.3f, 0f); // Marrón/naranja

    [Tooltip("Color durante el cooldown del poder")]
    public Color dirtyCooldownColor = new Color(0.4f, 0.2f, 0f); // Marrón oscuro

    [Header("🎯 Información de Rol")]
    [Tooltip("Texto que muestra el rol del jugador (TextMeshPro)")]
    public TextMeshProUGUI roleText;
    
    [Tooltip("Texto que muestra el objetivo del jugador (TextMeshPro)")]
    public TextMeshProUGUI objectiveText;
    
    [Header("Textos de Rol - Player2 (Contaminador)")]
    [Tooltip("Texto del rol")]
    public string player2RoleText = "🗑️ ROL: CONTAMINADOR";
    
    [Tooltip("Texto del objetivo")]
    [TextArea(2, 4)]
    public string player2ObjectiveText = "Objetivo: Lanza basura y que\nsobreviva hasta el final";
    
    [Header("⚙ Configuración")]
    [Tooltip("Activar/desactivar la UI automáticamente")]
    public bool autoToggle = true;

    [Tooltip("Mostrar solo cuando es Player2")]
    public bool showOnlyForPlayer2 = true;
    
    [Header(" Debug")]
    [Tooltip("Mostrar mensajes de debug detallados")]
    public bool showDetailedDebug = true;
    
    // Variable para controlar si ya se inicializó
    private bool isInitialized = false;
    
    // Referencias a los elementos visuales del panel (para ocultarlos sin desactivar el GO)
    private CanvasGroup canvasGroup;

    void Awake()
    {
        // Obtener o agregar CanvasGroup para controlar visibilidad sin desactivar el GO
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Inicialmente ocultar la UI usando CanvasGroup (NO desactivar el GameObject)
        SetUIVisible(false);
        Debug.Log("� [TrashSpawnerUI] AWAKE - UI oculta con CanvasGroup, GameObject sigue activo para corrutinas");
    }

    void Start()
    {
        Debug.Log(" [TrashSpawnerUI] START - Iniciando búsqueda de TrashSpawner...");
        Debug.Log($" [TrashSpawnerUI] GameObject: {gameObject.name}, Activo: {gameObject.activeSelf}");
        
        // NO desactivar el GameObject, usar CanvasGroup
        // Esperar un frame para que los jugadores se instancien
        StartCoroutine(InitializeUI());
    }

    /// <summary>
    /// Controla la visibilidad de la UI sin desactivar el GameObject
    /// Esto permite que las corrutinas sigan ejecutándose
    /// </summary>
    void SetUIVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        
        if (showDetailedDebug)
        {
            Debug.Log($" [TrashSpawnerUI] SetUIVisible({visible}) - Alpha: {canvasGroup?.alpha}");
        }
    }

    System.Collections.IEnumerator InitializeUI()
    {
        Debug.Log(" [TrashSpawnerUI] InitializeUI - Esperando 2 segundos para que se instancien los jugadores...");
        
        // Esperar 2 segundos para que los jugadores se conecten e instancien
        yield return new WaitForSeconds(2f);
        
        Debug.Log(" [TrashSpawnerUI] InitializeUI - Tiempo de espera completado, buscando TrashSpawner...");

        // Buscar TrashSpawner que pertenezca al jugador local
        if (trashSpawner == null)
        {
            trashSpawner = FindLocalTrashSpawner();
            
            if (trashSpawner == null)
            {
                Debug.LogWarning(" [TrashSpawnerUI] No se encontró TrashSpawner local aún, iniciando reintentos...");
                // Reintentar cada segundo hasta encontrarlo
                StartCoroutine(RetryFindTrashSpawner());
                yield break;
            }
        }

        // Si encontramos un TrashSpawner local, mostrar la UI
        if (trashSpawner != null)
        {
            SetUIVisible(true);
            isInitialized = true;
            
            // Configurar textos de rol y objetivo
            SetupRoleInfo();
            
            Debug.Log("✅ [TrashSpawnerUI] ¡UI ACTIVADA para Player2 local!");
            Debug.Log($"📍 [TrashSpawnerUI] TrashSpawner encontrado en: {trashSpawner.gameObject.name}");
        }
        else
        {
            SetUIVisible(false);
            Debug.Log(" [TrashSpawnerUI] UI oculta (no es Player2 local)");
        }

        // Obtener RectTransform de la barra y guardar el ancho máximo
        if (cooldownFillBar != null)
        {
            cooldownFillRect = cooldownFillBar.GetComponent<RectTransform>();
            if (cooldownFillRect != null)
            {
                maxBarWidth = cooldownFillRect.sizeDelta.x;
                Debug.Log($" [TrashSpawnerUI] Barra de cooldown configurada, ancho máx: {maxBarWidth}");
            }
        }

        // Obtener RectTransform de la barra de poder suciedad
        if (dirtyPowerFillBar != null)
        {
            dirtyPowerFillRect = dirtyPowerFillBar.GetComponent<RectTransform>();
            if (dirtyPowerFillRect != null)
            {
                maxDirtyBarWidth = dirtyPowerFillRect.sizeDelta.x;
                Debug.Log($" [TrashSpawnerUI] Barra de poder suciedad configurada, ancho máx: {maxDirtyBarWidth}");
            }
        }
    }

    /// <summary>
    /// Busca el TrashSpawner que pertenece al jugador local (Player2)
    /// </summary>
    TrashSpawner FindLocalTrashSpawner()
    {
        // Buscar TODOS los TrashSpawners en la escena
        TrashSpawner[] allSpawners = FindObjectsOfType<TrashSpawner>();
        
        Debug.Log($"� [TrashSpawnerUI] FindLocalTrashSpawner - Encontrados {allSpawners.Length} TrashSpawners en escena");
        
        int index = 0;
        foreach (var spawner in allSpawners)
        {
            var photonView = spawner.GetComponent<Photon.Pun.PhotonView>();
            
            if (photonView != null)
            {
                Debug.Log($" [TrashSpawnerUI] Spawner[{index}]: {spawner.gameObject.name} - IsMine: {photonView.IsMine}, ViewID: {photonView.ViewID}, Owner: {photonView.Owner?.NickName ?? "null"}");
                
                if (photonView.IsMine)
                {
                    Debug.Log($" [TrashSpawnerUI] ¡ENCONTRADO! TrashSpawner local del Player2: {spawner.gameObject.name}");
                    return spawner;
                }
            }
            else
            {
                Debug.LogWarning($" [TrashSpawnerUI] Spawner[{index}]: {spawner.gameObject.name} - NO tiene PhotonView!");
            }
            index++;
        }
        
        Debug.Log(" [TrashSpawnerUI] No se encontró TrashSpawner con IsMine=true (este cliente no es Player2)");
        return null;
    }

    System.Collections.IEnumerator RetryFindTrashSpawner()
    {
        int retryCount = 0;
        int maxRetries = 15; // Máximo 15 segundos de reintentos
        
        Debug.Log(" [TrashSpawnerUI] RetryFindTrashSpawner - Iniciando reintentos...");
        
        while (trashSpawner == null && retryCount < maxRetries)
        {
            yield return new WaitForSeconds(1f);
            retryCount++;
            
            Debug.Log($" [TrashSpawnerUI] Reintento {retryCount}/{maxRetries}...");
            
            trashSpawner = FindLocalTrashSpawner();
            
            if (trashSpawner != null)
            {
                Debug.Log($"✅ [TrashSpawnerUI] TrashSpawner local encontrado después de {retryCount} reintentos");
                
                // Mostrar UI para Player2 usando CanvasGroup
                SetUIVisible(true);
                isInitialized = true;
                
                // Configurar textos de rol y objetivo
                SetupRoleInfo();
                
                Debug.Log("🎮 [TrashSpawnerUI] UI VISIBLE para Player2 (Contaminador)");

                // Obtener RectTransform de la barra
                if (cooldownFillBar != null)
                {
                    cooldownFillRect = cooldownFillBar.GetComponent<RectTransform>();
                    if (cooldownFillRect != null)
                    {
                        maxBarWidth = cooldownFillRect.sizeDelta.x;
                        Debug.Log($" [TrashSpawnerUI] Barra configurada, ancho: {maxBarWidth}");
                    }
                }

                // Obtener RectTransform de la barra de poder suciedad
                if (dirtyPowerFillBar != null)
                {
                    dirtyPowerFillRect = dirtyPowerFillBar.GetComponent<RectTransform>();
                    if (dirtyPowerFillRect != null)
                    {
                        maxDirtyBarWidth = dirtyPowerFillRect.sizeDelta.x;
                        Debug.Log($" [TrashSpawnerUI] Barra poder suciedad configurada, ancho: {maxDirtyBarWidth}");
                    }
                }
                
                break;
            }
        }
        
        if (trashSpawner == null)
        {
            Debug.Log(" [TrashSpawnerUI] Después de 15 reintentos: Este jugador NO es Player2, UI permanece oculta");
            SetUIVisible(false);
        }
    }

    void Update()
    {
        if (trashSpawner == null) return;

        UpdateCounterUI();
        UpdateCooldownUI();
        UpdateDirtyPowerUI();
    }

    /// <summary>
    /// Actualiza el contador de basura
    /// </summary>
    void UpdateCounterUI()
    {
        if (trashCounterText != null)
        {
            trashCounterText.text = string.Format(counterFormat, trashSpawner.totalTrashSpawned);
        }
    }

    /// <summary>
    /// Actualiza la barra de cooldown usando WIDTH en lugar de fillAmount
    /// Compatible con Unity 2021 sin Fill Type
    /// </summary>
    void UpdateCooldownUI()
    {
        bool canSpawn = trashSpawner.CanSpawn();
        float progress = trashSpawner.GetCooldownProgress();
        float remaining = trashSpawner.GetRemainingCooldown();

        // Actualizar barra de relleno cambiando el WIDTH
        if (cooldownFillBar != null && cooldownFillRect != null)
        {
            // Cambiar el ancho de la barra según el progreso
            float newWidth = maxBarWidth * progress;
            cooldownFillRect.sizeDelta = new Vector2(newWidth, cooldownFillRect.sizeDelta.y);
            
            // Cambiar color
            cooldownFillBar.color = canSpawn ? readyColor : cooldownColor;
        }

        // Actualizar texto de cooldown
        if (cooldownText != null)
        {
            if (canSpawn)
            {
                cooldownText.text = "¡LISTO!";
                cooldownText.color = readyColor;
            }
            else
            {
                cooldownText.text = $"Espera: {remaining:F1}s";
                cooldownText.color = cooldownColor;
            }
        }
    }

    /// <summary>
    /// Actualiza la barra de cooldown del poder de suciedad.
    /// </summary>
    void UpdateDirtyPowerUI()
    {
        // Verificar si el sistema DirtyScreenEffect existe
        if (DirtyScreenEffect.Instance == null) return;

        bool canUsePower = !DirtyScreenEffect.Instance.IsOnCooldown();
        float progress = DirtyScreenEffect.Instance.GetCooldownProgress();
        float remaining = DirtyScreenEffect.Instance.GetRemainingCooldown();

        // Actualizar barra de relleno cambiando el WIDTH
        if (dirtyPowerFillBar != null && dirtyPowerFillRect != null)
        {
            // Cambiar el ancho de la barra según el progreso
            float newWidth = maxDirtyBarWidth * progress;
            dirtyPowerFillRect.sizeDelta = new Vector2(newWidth, dirtyPowerFillRect.sizeDelta.y);
            
            // Cambiar color
            dirtyPowerFillBar.color = canUsePower ? dirtyReadyColor : dirtyCooldownColor;
        }

        // Actualizar texto del poder
        if (dirtyPowerText != null)
        {
            if (canUsePower)
            {
                dirtyPowerText.text = " ¡LISTO!";
                dirtyPowerText.color = dirtyReadyColor;
            }
            else
            {
                dirtyPowerText.text = $" Espera: {remaining:F1}s";
                dirtyPowerText.color = dirtyCooldownColor;
            }
        }
    }
    
    /// <summary>
    /// Configura los textos de rol y objetivo para Player2 (Contaminador)
    /// </summary>
    void SetupRoleInfo()
    {
        if (roleText != null)
        {
            roleText.text = player2RoleText;
            roleText.color = new Color(1f, 0.5f, 0.2f); // Naranja
        }
        
        if (objectiveText != null)
        {
            objectiveText.text = player2ObjectiveText;
            objectiveText.color = Color.white;
        }
        
        Debug.Log("🎯 [TrashSpawnerUI] Información de rol configurada para CONTAMINADOR");
    }
}
