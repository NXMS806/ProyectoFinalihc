using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestor de niveles con temporizador y condiciones de victoria/derrota.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("Configuración del Nivel 1")]
    [Tooltip("Tiempo límite para completar el nivel (en segundos)")]
    public float levelTime = 90f; // 1:30 minutos
    
    [Tooltip("Número de basuras a recolectar para ganar")]
    public int targetTrash = 5;

    [Header("UI Referencias - LevelPanel")]
    [Tooltip("Panel que muestra timer y progreso durante el nivel")]
    public GameObject levelPanel;
    
    [Tooltip("Texto que muestra el temporizador (formato MM:SS)")]
    public Text timerText;
    
    [Tooltip("Texto que muestra el progreso (ej: 'Basura: 3/5')")]
    public Text objectiveText;

    [Header("UI Referencias - Victoria")]
    [Tooltip("Panel que aparece al completar el nivel exitosamente")]
    public GameObject victoryPanel;
    
    [Tooltip("Texto que muestra las basuras recolectadas en victoria")]
    public Text victoryTrashText;
    
    [Tooltip("Texto que muestra el tiempo final en victoria")]
    public Text victoryTimeText;

    [Header("UI Referencias - Derrota")]
    [Tooltip("Panel que aparece cuando se acaba el tiempo")]
    public GameObject defeatPanel;
    
    [Tooltip("Texto que muestra las basuras recolectadas en derrota")]
    public Text defeatTrashText;

    [Header("Audio (Opcional)")]
    [Tooltip("Clip de audio para victoria (opcional si usas OceanAudioSystem)")]
    public AudioClip victorySound;
    
    [Tooltip("Clip de audio para derrota (opcional si usas OceanAudioSystem)")]
    public AudioClip defeatSound;

    [Header("Debug")]
    [Tooltip("Mostrar mensajes de debug en consola")]
    public bool showDebugLogs = true;

    // Variables privadas
    private bool levelActive = false;
    private float timeRemaining;
    private int trashCollected = 0;
    private int initialTrashCount = 0; // Para resetear correctamente
    private AudioSource localAudioSource;

    void Start()
    {
        // Ocultar todos los paneles al inicio
        if (levelPanel != null) levelPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);

        // Crear AudioSource local si no tenemos OceanAudioSystem
        if (OceanAudioSystem.Instance == null)
        {
            localAudioSource = gameObject.AddComponent<AudioSource>();
            if (showDebugLogs)
                Debug.Log(" OceanAudioSystem no encontrado, usando AudioSource local");
        }

        // Suscribirse al evento de recolección de basura
        SimpleTrashCounter.OnTrashCollected += OnTrashCollected;

        if (showDebugLogs)
        {
            Debug.Log(" LevelManager iniciado");
            Debug.Log($"   Presiona '1' para iniciar Nivel 1");
            Debug.Log($"   Objetivo: Recolectar {targetTrash} basuras en {FormatTime(levelTime)}");
        }
    }

    void OnDestroy()
    {
        // Desuscribirse del evento al destruir
        SimpleTrashCounter.OnTrashCollected -= OnTrashCollected;
    }

    void Update()
    {
        // DETECTAR TECLA '1' PARA INICIAR NIVEL
        if (Input.GetKeyDown(KeyCode.Alpha1) && !levelActive)
        {
            StartLevel();
        }

        // ACTUALIZAR TIMER SI EL NIVEL ESTÁ ACTIVO
        if (levelActive)
        {
            timeRemaining -= Time.deltaTime;
            UpdateTimerUI();

            // VERIFICAR SI SE ACABÓ EL TIEMPO (DERROTA)
            if (timeRemaining <= 0f)
            {
                LevelDefeat();
            }
        }
    }

    /// <summary>
    /// Inicia el Nivel 1
    /// </summary>
    void StartLevel()
    {
        levelActive = true;
        timeRemaining = levelTime;
        trashCollected = 0;

        // Guardar el contador inicial para poder resetear después
        initialTrashCount = SimpleTrashCounter.GetTrashCount();

        // Activar panel del nivel
        if (levelPanel != null)
            levelPanel.SetActive(true);

        // Actualizar UI inicial
        UpdateObjectiveUI();
        UpdateTimerUI();

        // Restaurar time scale (por si venimos de una victoria/derrota anterior)
        Time.timeScale = 1f;

        if (showDebugLogs)
            Debug.Log($" NIVEL 1 INICIADO - Recolecta {targetTrash} basuras en {FormatTime(levelTime)}");
    }

    /// <summary>
    /// Llamado cuando se recolecta basura (evento de SimpleTrashCounter)
    /// </summary>
    void OnTrashCollected(int totalTrash)
    {
        if (!levelActive) return;

        // Calcular basuras recolectadas desde que inició el nivel
        trashCollected = totalTrash - initialTrashCount;

        UpdateObjectiveUI();

        if (showDebugLogs)
            Debug.Log($" Basura recolectada: {trashCollected}/{targetTrash}");

        // VERIFICAR VICTORIA
        if (trashCollected >= targetTrash)
        {
            LevelComplete();
        }
    }

    /// <summary>
    /// Actualiza el texto del temporizador
    /// </summary>
    void UpdateTimerUI()
    {
        if (timerText == null) return;

        // Convertir segundos a formato MM:SS
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);

        timerText.text = string.Format(" Tiempo: {0:00}:{1:00}", minutes, seconds);

        // Cambiar color si queda poco tiempo (ALERTA)
        if (timeRemaining < 30f)
        {
            timerText.color = Color.red;
        }
        else if (timeRemaining < 60f)
        {
            timerText.color = Color.yellow;
        }
        else
        {
            timerText.color = Color.white;
        }
    }

    /// <summary>
    /// Actualiza el texto del objetivo
    /// </summary>
    void UpdateObjectiveUI()
    {
        if (objectiveText == null) return;

        objectiveText.text = $" Basura: {trashCollected}/{targetTrash}";
    }

    /// <summary>
    /// Nivel completado exitosamente (VICTORIA)
    /// </summary>
    void LevelComplete()
    {
        levelActive = false;
        Time.timeScale = 0f; // Pausar juego

        // Ocultar panel de nivel
        if (levelPanel != null)
            levelPanel.SetActive(false);

        // Mostrar panel de victoria
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);

            // Actualizar textos del panel de victoria
            if (victoryTrashText != null)
                victoryTrashText.text = $"Basuras recolectadas: {trashCollected}/{targetTrash}";

            if (victoryTimeText != null)
            {
                float timeUsed = levelTime - timeRemaining;
                victoryTimeText.text = $"Tiempo usado: {FormatTime(timeUsed)}";
            }
        }

        // Reproducir sonido de victoria
        PlayVictorySound();

        if (showDebugLogs)
            Debug.Log($"� ¡NIVEL COMPLETADO! Basuras: {trashCollected}/{targetTrash} | Tiempo restante: {FormatTime(timeRemaining)}");
    }

    /// <summary>
    /// Nivel fallido por tiempo agotado (DERROTA)
    /// </summary>
    void LevelDefeat()
    {
        levelActive = false;
        Time.timeScale = 0f; // Pausar juego

        // Ocultar panel de nivel
        if (levelPanel != null)
            levelPanel.SetActive(false);

        // Mostrar panel de derrota
        if (defeatPanel != null)
        {
            defeatPanel.SetActive(true);

            // Actualizar texto del panel de derrota
            if (defeatTrashText != null)
                defeatTrashText.text = $"Basuras recolectadas: {trashCollected}/{targetTrash}\n¡Te faltaron {targetTrash - trashCollected}!";
        }

        // Reproducir sonido de derrota
        PlayDefeatSound();

        if (showDebugLogs)
            Debug.Log($"⏰ TIEMPO AGOTADO - Nivel fallido | Basuras: {trashCollected}/{targetTrash}");
    }

    /// <summary>
    /// Reproduce sonido de victoria
    /// </summary>
    void PlayVictorySound()
    {
        // Intentar usar OceanAudioSystem primero
        if (OceanAudioSystem.Instance != null)
        {
            OceanAudioSystem.Instance.PlayVictorySound();
        }
        // Si no existe, usar AudioSource local
        else if (localAudioSource != null && victorySound != null)
        {
            localAudioSource.PlayOneShot(victorySound);
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning(" No se puede reproducir sonido de victoria (falta clip o AudioSource)");
        }
    }

    /// <summary>
    /// Reproduce sonido de derrota
    /// </summary>
    void PlayDefeatSound()
    {
        // Intentar usar OceanAudioSystem primero
        if (OceanAudioSystem.Instance != null)
        {
            OceanAudioSystem.Instance.PlayDefeatSound();
        }
        // Si no existe, usar AudioSource local
        else if (localAudioSource != null && defeatSound != null)
        {
            localAudioSource.PlayOneShot(defeatSound);
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning(" No se puede reproducir sonido de derrota (falta clip o AudioSource)");
        }
    }

    /// <summary>
    /// Formatea segundos a formato legible (MM:SS)
    /// </summary>
    string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    // ============================================
    // MÉTODOS PÚBLICOS PARA BOTONES DE UI
    // ============================================

    /// <summary>
    /// Reintentar el nivel (llamado por botón UI)
    /// </summary>
    public void RetryLevel()
    {
        Time.timeScale = 1f; // Restaurar time scale

        // Ocultar paneles
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);

        // Reiniciar nivel
        StartLevel();

        if (showDebugLogs)
            Debug.Log(" Reintentando nivel...");
    }

    /// <summary>
    /// Continuar en modo libre (llamado por botón UI de victoria/derrota)
    /// Cierra los paneles del nivel y restaura el modo de exploración libre
    /// </summary>
    public void ContinueFreeMode()
    {
        levelActive = false;
        Time.timeScale = 1f; // Restaurar time scale

        // Ocultar todos los paneles del nivel
        if (levelPanel != null) levelPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);

        if (showDebugLogs)
            Debug.Log(" Volviendo a modo libre - El usuario puede explorar libremente");
    }

    /// <summary>
    /// Volver al menú principal (opcional - no se usa en victoria/derrota)
    /// Mantiene el método por si lo necesitas en otro contexto
    /// </summary>
    public void BackToMenu()
    {
        Time.timeScale = 1f; // Restaurar time scale antes de cambiar escena
        SceneManager.LoadScene("MainMenu");

        if (showDebugLogs)
            Debug.Log("� Volviendo al menú principal...");
    }

    /// <summary>
    /// Salir del nivel sin completarlo (para modo libre)
    /// </summary>
    public void ExitLevel()
    {
        levelActive = false;
        Time.timeScale = 1f;

        if (levelPanel != null) levelPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);

        if (showDebugLogs)
            Debug.Log("� Saliendo del nivel, volviendo a modo libre");
    }
}
