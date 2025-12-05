using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

/// <summary>
/// HUD del Player1 que muestra basura recolectada, puntos y estado.
/// </summary>
public class OceanVRHUD : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("Texto que muestra la cantidad de basura recolectada")]
    public Text trashCountText;
    
    [Tooltip("Texto que muestra los puntos acumulados")]
    public Text pointsText;
    
    [Tooltip("Texto que muestra el estado del highlight")]
    public Text highlightStatusText;
    
    [Header("🎯 Información de Rol")]
    [Tooltip("Texto que muestra el rol del jugador")]
    public Text roleText;
    
    [Tooltip("Texto que muestra el objetivo del jugador")]
    public Text objectiveText;

    [Header("Configuración de Textos")]
    [Tooltip("Prefijo para el contador de basura")]
    public string trashPrefix = "Basura: ";
    
    [Tooltip("Prefijo para los puntos")]
    public string pointsPrefix = "Puntos: ";
    
    [Tooltip("Prefijo para el estado del highlight")]
    public string highlightPrefix = "Highlight: ";
    
    [Header("Textos de Rol - Player1 (Limpiador)")]
    [Tooltip("Texto del rol")]
    public string player1RoleText = "🧹 ROL: LIMPIADOR";
    
    [Tooltip("Texto del objetivo")]
    [TextArea(2, 4)]
    public string player1ObjectiveText = "Objetivo: Recoge toda la basura\nantes de que termine el tiempo";

    [Header("Configuración de Actualización")]
    [Tooltip("Intervalo de actualización en segundos")]
    public float updateInterval = 0.1f;
    
    [Header(" Configuración de Rol")]
    [Tooltip("Solo mostrar para Player1 (Limpiador)")]
    public bool showOnlyForPlayer1 = true;

    // Referencias internas
    private UDP udpController;
    private bool isPlayer1 = false;
    private bool isInitialized = false;

    void Start()
    {
        Debug.Log("� [OceanVRHUD] START - Iniciando...");
        
        // Inicialmente OCULTAR todos los textos hasta verificar el rol
        SetAllTextsVisible(false);
        
        // Buscar el controlador UDP para obtener estado del highlight
        udpController = FindObjectOfType<UDP>();
        
        if (udpController == null)
        {
            Debug.LogWarning(" OceanVRHUD: No se encontró UDP controller.");
        }

        // Iniciar verificación de rol
        StartCoroutine(InitializeForRole());
    }

    /// <summary>
    /// Oculta o muestra todos los textos de la UI
    /// </summary>
    void SetAllTextsVisible(bool visible)
    {
        if (trashCountText != null)
            trashCountText.gameObject.SetActive(visible);
        
        // pointsText QUITADO - Los puntos son redundantes, lo que importa es la basura recogida
        // if (pointsText != null)
        //     pointsText.gameObject.SetActive(visible);
            
        if (highlightStatusText != null)
            highlightStatusText.gameObject.SetActive(visible);
        
        // Mostrar textos de rol y objetivo (TextMeshPro)
        if (roleText != null)
            roleText.gameObject.SetActive(visible);
        
        if (objectiveText != null)
            objectiveText.gameObject.SetActive(visible);
        
        // También ocultar el panel padre si existe
        Transform parent = transform.parent;
        if (parent != null && parent.name.Contains("Panel"))
        {
            parent.gameObject.SetActive(visible);
        }
        
        Debug.Log($"🎮 [OceanVRHUD] SetAllTextsVisible({visible})");
    }

    /// <summary>
    /// Determina si este cliente es Player1 (Limpiador) y muestra/oculta la UI
    /// </summary>
    IEnumerator InitializeForRole()
    {
        // Esperar a que los jugadores se conecten
        yield return new WaitForSeconds(3f);
        
        Debug.Log("🎮 [OceanVRHUD] Verificando rol del jugador...");
        
        if (showOnlyForPlayer1)
        {
            isPlayer1 = CheckIfLocalPlayerIsPlayer1();
            
            if (isPlayer1)
            {
                SetAllTextsVisible(true);
                isInitialized = true;
                
                // Configurar textos de rol y objetivo
                SetupRoleInfo();
                
                Debug.Log("✅ [OceanVRHUD] Este cliente es PLAYER1 (Limpiador) - UI VISIBLE");
                
                // Iniciar actualización continua
                StartCoroutine(UpdateHUDCoroutine());
            }
            else
            {
                SetAllTextsVisible(false);
                Debug.Log(" [OceanVRHUD] Este cliente es PLAYER2 - UI OCULTA permanentemente");
            }
        }
        else
        {
            // Si no hay filtro por rol, mostrar siempre
            SetAllTextsVisible(true);
            isInitialized = true;
            StartCoroutine(UpdateHUDCoroutine());
        }
    }

    /// <summary>
    /// Verifica si el jugador local es Player1 (Limpiador)
    /// Player1 = NO tiene TrashSpawner con IsMine
    /// Player2 = SÍ tiene TrashSpawner con IsMine
    /// </summary>
    bool CheckIfLocalPlayerIsPlayer1()
    {
        // Buscar todos los GameObjects que tengan un componente llamado "TrashSpawner"
        // Usamos este método para evitar problemas de referencias entre carpetas
        MonoBehaviour[] allBehaviours = FindObjectsOfType<MonoBehaviour>();
        
        int trashSpawnerCount = 0;
        
        foreach (var behaviour in allBehaviours)
        {
            // Verificar si el componente es un TrashSpawner por nombre de tipo
            if (behaviour.GetType().Name == "TrashSpawner")
            {
                trashSpawnerCount++;
                var photonView = behaviour.GetComponent<PhotonView>();
                
                if (photonView != null && photonView.IsMine)
                {
                    // Si encontramos un TrashSpawner que es nuestro, somos Player2
                    Debug.Log($"� [OceanVRHUD] Encontrado TrashSpawner local en {behaviour.gameObject.name} - Este cliente es PLAYER2");
                    return false;
                }
            }
        }
        
        Debug.Log($"� [OceanVRHUD] Encontrados {trashSpawnerCount} TrashSpawners, ninguno es local - Este cliente es PLAYER1");
        return true;
    }

    /// <summary>
    /// Coroutine que actualiza el HUD periódicamente
    /// </summary>
    IEnumerator UpdateHUDCoroutine()
    {
        while (true)
        {
            UpdateHUD();
            yield return new WaitForSeconds(updateInterval);
        }
    }

    /// <summary>
    /// Actualiza todos los elementos del HUD
    /// </summary>
    void UpdateHUD()
    {
        UpdateTrashCount();
        // UpdatePoints(); // QUITADO - Los puntos son redundantes, lo que importa es la basura recogida
        UpdateHighlightStatus();
    }

    /// <summary>
    /// Actualiza el texto de basura recolectada
    /// </summary>
    void UpdateTrashCount()
    {
        if (trashCountText != null)
        {
            int trashCount = SimpleTrashCounter.GetTrashCount();
            trashCountText.text = trashPrefix + trashCount.ToString();
        }
    }

    /// <summary>
    /// Actualiza el texto de puntos acumulados
    /// </summary>
    void UpdatePoints()
    {
        if (pointsText != null)
        {
            int points = SimpleTrashCounter.GetTotalPoints();
            pointsText.text = pointsPrefix + points.ToString();
        }
    }

    /// <summary>
    /// Actualiza el texto del estado del highlight
    /// </summary>
    void UpdateHighlightStatus()
    {
        if (highlightStatusText != null)
        {
            // Si no tenemos referencia al UDP, intentar buscarlo de nuevo
            if (udpController == null)
            {
                udpController = FindObjectOfType<UDP>();
            }
            
            if (udpController != null)
            {
                bool isHighlightActive = udpController.IsHighlightModeActive();
                
                // Cambiar texto según estado
                string statusText = isHighlightActive ? "ACTIVO" : "APAGADO";
                highlightStatusText.text = highlightPrefix + statusText;
                
                // Cambiar color según estado (verde activo, gris apagado)
                highlightStatusText.color = isHighlightActive ? Color.green : Color.gray;
            }
            else
            {
                // Si no encontramos UDP, mostrar como desconocido
                highlightStatusText.text = highlightPrefix + "N/A";
                highlightStatusText.color = Color.yellow;
            }
        }
    }

    /// <summary>
    /// Método público para forzar actualización manual si es necesario
    /// </summary>
    public void ForceUpdate()
    {
        UpdateHUD();
    }
    
    /// <summary>
    /// Configura los textos de rol y objetivo para Player1 (Limpiador)
    /// </summary>
    void SetupRoleInfo()
    {
        Color cyanColor = new Color(0.2f, 0.8f, 1f); // Azul claro/cyan
        
        // Configurar Role Text (TextMeshPro)
        if (roleText != null)
        {
            roleText.text = player1RoleText;
            roleText.color = cyanColor;
        }
        
        // Configurar Objective Text (TextMeshPro)
        if (objectiveText != null)
        {
            objectiveText.text = player1ObjectiveText;
            objectiveText.color = Color.white;
        }
        
        Debug.Log("🎯 [OceanVRHUD] Información de rol configurada para LIMPIADOR");
    }
}
