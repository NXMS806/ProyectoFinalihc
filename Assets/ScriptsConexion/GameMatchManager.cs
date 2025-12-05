using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class GameMatchManager : MonoBehaviourPunCallbacks
{
    #region Singleton
    public static GameMatchManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            
            // Asegurar que tenga PhotonView para RPCs
            if (GetComponent<PhotonView>() == null)
            {
                gameObject.AddComponent<PhotonView>();
                Debug.Log("GameMatchManager: PhotonView agregado automaticamente");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region Configuration
    [Header("Configuracion de Partida")]
    [Tooltip("Duración de la partida en segundos (2:30 = 150s)")]
    public float matchDuration = 150f;
    
    [Tooltip("Tecla para iniciar la partida (ambos jugadores conectados)")]
    public KeyCode startMatchKey = KeyCode.Alpha2;
    
    [Tooltip("Duración del countdown inicial (3...2...1...)")]
    public int countdownSeconds = 3;
    #endregion

    #region UI References - Waiting
    [Header("UI - Pantalla de Espera")]
    [Tooltip("Panel que se muestra mientras esperamos jugadores")]
    public GameObject waitingPanel;
    
    [Tooltip("Texto de estado (Esperando jugador... / Presiona 2...)")]
    public Text waitingStatusText;
    
    [Tooltip("Texto que muestra los jugadores conectados")]
    public Text playersConnectedText;
    #endregion

    #region UI References - Countdown
    [Header("UI - Countdown")]
    [Tooltip("Panel del countdown (3...2...1...)")]
    public GameObject countdownPanel;
    
    [Tooltip("Texto grande del número de countdown")]
    public Text countdownNumberText;
    
    [Tooltip("Texto pequeño debajo del número")]
    public Text countdownSubtitleText;
    #endregion

    #region UI References - Match Active
    [Header("UI - Partida Activa")]
    [Tooltip("Panel que muestra timer durante la partida")]
    public GameObject matchPanel;
    
    [Tooltip("Texto del timer (formato MM:SS)")]
    public Text matchTimerText;
    
    [Tooltip("(OPCIONAL) Texto de puntuación del Limpiador - Dejar vacío si usas OceanVRHUD")]
    public Text cleanerScoreText;
    
    [Tooltip("(OPCIONAL) Texto de puntuación del Contaminador - Dejar vacío si usas TrashSpawnerUI")]
    public Text polluterScoreText;
    #endregion

    #region UI References - Results
    [Header("UI - Resultados")]
    [Tooltip("Panel de resultados finales")]
    public GameObject resultsPanel;
    
    [Tooltip("Texto del ganador")]
    public Text winnerText;
    
    [Tooltip("Texto de estadísticas del Limpiador")]
    public Text cleanerFinalText;
    
    [Tooltip("Texto de estadísticas del Contaminador")]
    public Text polluterFinalText;
    
    [Tooltip("Texto de instrucciones (Presiona R para reiniciar)")]
    public Text restartInstructionText;
    #endregion

    #region Audio
    [Header("Audio")]
    [Tooltip("Sonido de countdown (cada número)")]
    public AudioClip countdownBeepSound;
    
    [Tooltip("Sonido de inicio de partida")]
    public AudioClip matchStartSound;
    
    [Tooltip("Sonido de fin de partida")]
    public AudioClip matchEndSound;
    
    [Tooltip("Sonido de victoria")]
    public AudioClip victorySound;
    
    private AudioSource audioSource;
    #endregion

    #region Debug
    [Header("Debug")]
    public bool showDebugLogs = true;
    #endregion

    #region Match State
    public enum MatchState
    {
        WaitingForPlayers,  // Esperando que se conecten 2 jugadores
        ReadyToStart,       // 2 jugadores conectados, esperando tecla 2
        Countdown,          // Countdown 3...2...1...
        Playing,            // Partida activa
        Finished            // Partida terminada, mostrando resultados
    }
    
    [Header("Estado Actual")]
    public MatchState currentState = MatchState.WaitingForPlayers;
    
    private float timeRemaining;
    private int cleanerScore = 0;
    private int polluterSpawnedCount = 0;
    private int initialCleanerTrash = 0;
    
    // Flag para evitar múltiples inicios
    private bool matchStartRequested = false;
    #endregion

    #region Photon Events
    // Evento personalizado para sincronizar inicio de partida
    private const byte START_MATCH_EVENT = 1;
    private const byte COUNTDOWN_EVENT = 2;
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        // Configurar AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Ocultar todos los paneles al inicio
        HideAllPanels();
        
        // Mostrar panel de espera
        if (waitingPanel != null)
            waitingPanel.SetActive(true);
        
        // Suscribirse a eventos de Photon
        PhotonNetwork.NetworkingClient.EventReceived += OnPhotonEvent;
        
        // Verificar estado inicial de jugadores
        UpdatePlayerStatus();
        
        if (showDebugLogs)
        {
            Debug.Log("GameMatchManager iniciado");
            Debug.Log("   Esperando 2 jugadores para habilitar inicio...");
        }
    }

    void OnDestroy()
    {
        // Desuscribirse de eventos
        PhotonNetwork.NetworkingClient.EventReceived -= OnPhotonEvent;
    }

    void Update()
    {
        switch (currentState)
        {
            case MatchState.WaitingForPlayers:
                // Actualizar estado de espera
                UpdatePlayerStatus();
                break;
                
            case MatchState.ReadyToStart:
                // Detectar tecla 2 para iniciar
                if (Input.GetKeyDown(startMatchKey) && !matchStartRequested)
                {
                    RequestMatchStart();
                }
                break;
                
            case MatchState.Playing:
                // Actualizar timer
                UpdateMatchTimer();
                // Actualizar puntuaciones
                UpdateScores();
                break;
                
            case MatchState.Finished:
                // Detectar tecla R para reiniciar (cualquier jugador puede solicitar)
                if (Input.GetKeyDown(KeyCode.R))
                {
                    RequestRestart();
                }
                break;
        }
    }
    #endregion

    #region Player Status
    void UpdatePlayerStatus()
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.CurrentRoom == null)
        {
            SetWaitingText("Conectando a la sala...", "");
            return;
        }
        
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        
        if (playersConnectedText != null)
        {
            playersConnectedText.text = $"Jugadores: {playerCount}/2";
        }
        
        if (playerCount < 2)
        {
            currentState = MatchState.WaitingForPlayers;
            SetWaitingText("Esperando al otro jugador...", $"Jugadores conectados: {playerCount}/2");
        }
        else if (playerCount >= 2 && currentState == MatchState.WaitingForPlayers)
        {
            currentState = MatchState.ReadyToStart;
            SetWaitingText("¡Ambos jugadores listos!", "Presiona [2] para iniciar la partida");
            
            if (showDebugLogs)
                Debug.Log("2 jugadores conectados - Listo para iniciar");
        }
    }
    
    void SetWaitingText(string status, string subtitle)
    {
        if (waitingStatusText != null)
            waitingStatusText.text = status;
        if (playersConnectedText != null && !string.IsNullOrEmpty(subtitle))
            playersConnectedText.text = subtitle;
    }
    #endregion

    #region Match Start
    void RequestMatchStart()
    {
        if (currentState != MatchState.ReadyToStart) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return;
        
        matchStartRequested = true;
        
        if (showDebugLogs)
            Debug.Log("Solicitando inicio de partida...");
        
        // Enviar evento a TODOS los jugadores para iniciar countdown
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        SendOptions sendOptions = new SendOptions { Reliability = true };
        
        PhotonNetwork.RaiseEvent(START_MATCH_EVENT, null, options, sendOptions);
    }
    
    void OnPhotonEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case START_MATCH_EVENT:
                StartCountdown();
                break;
                
            case COUNTDOWN_EVENT:
                int number = (int)photonEvent.CustomData;
                UpdateCountdownDisplay(number);
                break;
        }
    }
    #endregion

    #region Countdown
    void StartCountdown()
    {
        if (currentState == MatchState.Countdown || currentState == MatchState.Playing)
            return;
            
        currentState = MatchState.Countdown;
        
        if (showDebugLogs)
            Debug.Log("Iniciando countdown...");
        
        // Ocultar panel de espera, mostrar countdown
        HideAllPanels();
        if (countdownPanel != null)
            countdownPanel.SetActive(true);
        
        // Solo el MasterClient ejecuta la coroutine de countdown
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(CountdownCoroutine());
        }
    }
    
    IEnumerator CountdownCoroutine()
    {
        for (int i = countdownSeconds; i > 0; i--)
        {
            // Enviar número a todos los clientes
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            SendOptions sendOptions = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(COUNTDOWN_EVENT, i, options, sendOptions);
            
            yield return new WaitForSeconds(1f);
        }
        
        // Enviar 0 para indicar "¡GO!"
        RaiseEventOptions goOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        SendOptions goSendOptions = new SendOptions { Reliability = true };
        PhotonNetwork.RaiseEvent(COUNTDOWN_EVENT, 0, goOptions, goSendOptions);
        
        yield return new WaitForSeconds(1f);
        
        // Iniciar partida via RPC para sincronización
        photonView.RPC("RPC_StartMatch", RpcTarget.All);
    }
    
    void UpdateCountdownDisplay(int number)
    {
        if (countdownNumberText == null) return;
        
        if (number > 0)
        {
            countdownNumberText.text = number.ToString();
            countdownNumberText.color = Color.white;
            
            if (countdownSubtitleText != null)
                countdownSubtitleText.text = "¡Prepárense!";
            
            // Reproducir sonido de beep
            PlaySound(countdownBeepSound);
        }
        else
        {
            countdownNumberText.text = "¡GO!";
            countdownNumberText.color = Color.green;
            
            if (countdownSubtitleText != null)
                countdownSubtitleText.text = "¡A jugar!";
            
            // Reproducir sonido de inicio
            PlaySound(matchStartSound);
        }
        
        if (showDebugLogs)
            Debug.Log($"Countdown: {(number > 0 ? number.ToString() : "GO!")}");
    }
    #endregion

    #region Match Playing
    [PunRPC]
    void RPC_StartMatch()
    {
        currentState = MatchState.Playing;
        timeRemaining = matchDuration;
        matchStartRequested = false;
        
        // Resetear puntuaciones
        cleanerScore = 0;
        polluterSpawnedCount = 0;
        initialCleanerTrash = SimpleTrashCounter.GetTrashCount();
        
        // Cambiar UI
        HideAllPanels();
        if (matchPanel != null)
            matchPanel.SetActive(true);
        
        // Habilitar controles de jugadores
        EnablePlayerControls(true);
        
        if (showDebugLogs)
            Debug.Log($"PARTIDA INICIADA! Duracion: {FormatTime(matchDuration)}");
    }
    
    void UpdateMatchTimer()
    {
        timeRemaining -= Time.deltaTime;
        
        if (matchTimerText != null)
        {
            matchTimerText.text = FormatTime(timeRemaining);
            
            // Cambiar color cuando queda poco tiempo
            if (timeRemaining < 30f)
                matchTimerText.color = Color.red;
            else if (timeRemaining < 60f)
                matchTimerText.color = Color.yellow;
            else
                matchTimerText.color = Color.white;
        }
        
        // Verificar fin de partida
        if (timeRemaining <= 0f)
        {
            EndMatch();
        }
    }
    
    void UpdateScores()
    {
        // Puntuación del Limpiador: basura recogida desde inicio de partida
        cleanerScore = SimpleTrashCounter.GetTrashCount() - initialCleanerTrash;
        
        // Solo actualizar si los textos existen (son opcionales)
        if (cleanerScoreText != null)
            cleanerScoreText.text = $"Limpiador: {cleanerScore}";
        
        if (polluterScoreText != null)
            polluterScoreText.text = $"Contaminador: {polluterSpawnedCount}";
    }
    
    /// <summary>
    /// Llamado por TrashSpawner cuando spawnea basura
    /// </summary>
    public void OnTrashSpawned()
    {
        if (currentState == MatchState.Playing)
        {
            polluterSpawnedCount++;
            
            if (showDebugLogs)
                Debug.Log($"Basura spawneada: {polluterSpawnedCount}");
        }
    }
    #endregion

    #region Match End
    /// <summary>
    /// Llamado cuando el timer llega a 0.
    /// SOLO el MasterClient calcula los scores y envía resultados a todos.
    /// </summary>
    void EndMatch()
    {
        if (currentState == MatchState.Finished) return;
        
        // Deshabilitar controles inmediatamente (localmente)
        EnablePlayerControls(false);
        
        // SOLO el MasterClient calcula y envía los resultados
        if (PhotonNetwork.IsMasterClient)
        {
            // Calcular puntuación final del Limpiador
            int finalCleanerScore = SimpleTrashCounter.GetTrashCount() - initialCleanerTrash;
            
            // Calcular puntuación final del Contaminador
            // = basura que AÚN existe en la escena (spawneada por polluter)
            int finalPolluterScore = CountSurvivingTrash();
            
            if (showDebugLogs)
            {
                Debug.Log($"[MasterClient] Calculando resultados...");
                Debug.Log($"   Limpiador: {finalCleanerScore} recogidas");
                Debug.Log($"   Contaminador: {finalPolluterScore} sobrevivieron");
            }
            
            // Enviar resultados a TODOS los clientes via RPC
            photonView.RPC("RPC_ShowMatchResults", RpcTarget.All, finalCleanerScore, finalPolluterScore);
        }
    }
    
    /// <summary>
    /// RPC: Muestra los resultados finales en TODOS los clientes.
    /// Los scores son calculados por el MasterClient y enviados para garantizar consistencia.
    /// </summary>
    [PunRPC]
    void RPC_ShowMatchResults(int finalCleanerScore, int finalPolluterScore)
    {
        if (currentState == MatchState.Finished) return;
        
        currentState = MatchState.Finished;
        
        // Guardar scores para referencia
        cleanerScore = finalCleanerScore;
        
        // Determinar ganador (misma lógica, pero con scores sincronizados)
        string winner;
        Color winnerColor;
        
        if (finalCleanerScore > finalPolluterScore)
        {
            winner = "GANA EL LIMPIADOR!";
            winnerColor = Color.cyan;
            PlaySound(victorySound);
        }
        else if (finalPolluterScore > finalCleanerScore)
        {
            winner = "GANA EL CONTAMINADOR!";
            winnerColor = new Color(0.6f, 0.3f, 0.1f);
            PlaySound(victorySound);
        }
        else
        {
            winner = "EMPATE!";
            winnerColor = Color.yellow;
        }
        
        // Mostrar resultados
        HideAllPanels();
        if (resultsPanel != null)
            resultsPanel.SetActive(true);
        
        if (winnerText != null)
        {
            winnerText.text = winner;
            winnerText.color = winnerColor;
        }
        
        if (cleanerFinalText != null)
            cleanerFinalText.text = $"Limpiador: {finalCleanerScore} basuras recogidas";
        
        if (polluterFinalText != null)
            polluterFinalText.text = $"Contaminador: {finalPolluterScore} basuras sobrevivieron";
        
        if (restartInstructionText != null)
            restartInstructionText.text = "Presiona [R] para jugar de nuevo";
        
        // Reproducir sonido de fin
        PlaySound(matchEndSound);
        
        if (showDebugLogs)
        {
            Debug.Log("PARTIDA TERMINADA! (Resultados sincronizados)");
            Debug.Log($"   Limpiador: {finalCleanerScore} recogidas");
            Debug.Log($"   Contaminador: {finalPolluterScore} sobrevivieron");
            Debug.Log($"   {winner}");
        }
    }
    
    int CountSurvivingTrash()
    {
        // Contar todos los TrashObject que aún existen en la escena
        // y fueron spawneados por el Contaminador
        TrashObject[] allTrash = FindObjectsOfType<TrashObject>();
        int count = 0;
        
        foreach (TrashObject trash in allTrash)
        {
            // Solo contar basura spawneada por Polluter
            if (trash.spawnedByPolluter)
            {
                count++;
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"Basura sobreviviente: {count}");
        
        return count;
    }
    #endregion

    #region Match Restart
    /// <summary>
    /// Cualquier jugador puede solicitar reinicio presionando R.
    /// La solicitud se envía al MasterClient quien ejecuta el reinicio real.
    /// </summary>
    void RequestRestart()
    {
        if (showDebugLogs)
            Debug.Log("Solicitando reinicio de partida...");
        
        // Enviar solicitud de reinicio via RPC
        // El MasterClient destruirá la basura y luego notificará a todos
        photonView.RPC("RPC_RequestRestart", RpcTarget.MasterClient);
    }
    
    /// <summary>
    /// RPC: Recibido por el MasterClient cuando alguien solicita reinicio.
    /// El MasterClient coordina la destruccion y notifica a todos para reiniciar.
    /// </summary>
    [PunRPC]
    void RPC_RequestRestart()
    {
        // Solo el MasterClient procesa esta solicitud
        if (!PhotonNetwork.IsMasterClient) return;
        
        if (showDebugLogs)
            Debug.Log("[MasterClient] Procesando solicitud de reinicio...");
        
        // Notificar a TODOS los clientes que destruyan su basura y reinicien
        // Cada cliente destruirá la basura de la que es owner
        photonView.RPC("RPC_ExecuteRestart", RpcTarget.All);
    }
    
    /// <summary>
    /// RPC: Ejecuta el reinicio en TODOS los clientes.
    /// Cada cliente destruye la basura de la que es owner.
    /// </summary>
    [PunRPC]
    void RPC_ExecuteRestart()
    {
        if (showDebugLogs)
            Debug.Log("Reiniciando partida (sincronizado)...");
        
        // PRIMERO: Destruir la basura de la que ESTE cliente es owner
        DestroyOwnedTrash();
        
        // Resetear contador del TrashSpawner (UI del contaminador)
        TrashSpawner spawner = FindObjectOfType<TrashSpawner>();
        if (spawner != null)
        {
            spawner.ResetSpawnCount();
        }
        
        // Resetear estado local
        currentState = MatchState.WaitingForPlayers;
        matchStartRequested = false;
        cleanerScore = 0;
        polluterSpawnedCount = 0;
        initialCleanerTrash = 0;
        
        // Resetear contador de basura
        SimpleTrashCounter.ResetCount();
        
        // Mostrar pantalla de espera
        HideAllPanels();
        if (waitingPanel != null)
            waitingPanel.SetActive(true);
        
        // Habilitar controles para el nuevo juego
        EnablePlayerControls(true);
        
        // Verificar jugadores conectados
        UpdatePlayerStatus();
        
        if (showDebugLogs)
            Debug.Log("Partida reiniciada correctamente");
    }
    
    /// <summary>
    /// Destruye toda la basura de la que ESTE cliente es owner.
    /// </summary>
    void DestroyOwnedTrash()
    {
        TrashObject[] allTrash = FindObjectsOfType<TrashObject>();
        int destroyedCount = 0;
        
        foreach (TrashObject trash in allTrash)
        {
            PhotonView pv = trash.GetComponent<PhotonView>();
            
            if (pv != null && pv.IsMine)
            {
                // Solo destruir objetos de los que SOY owner
                if (showDebugLogs)
                    Debug.Log($"Destruyendo MI basura: {trash.name} | ViewID={pv.ViewID}");
                
                PhotonNetwork.Destroy(trash.gameObject);
                destroyedCount++;
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"Destruidas {destroyedCount} basuras propias");
    }
    #endregion

    #region Player Controls
    void EnablePlayerControls(bool enabled)
    {
        // Habilitar/deshabilitar movimiento de jugadores
        // Esto se hace via una variable pública que los scripts consultan
        
        // Para UDP.cs y Movement.cs
        UDP udp = FindObjectOfType<UDP>();
        if (udp != null)
        {
            udp.enabled = enabled;
        }
        
        // Para TrashSpawner
        TrashSpawner spawner = FindObjectOfType<TrashSpawner>();
        if (spawner != null)
        {
            spawner.enabled = enabled;
        }
        
        // NUEVO: Si estamos habilitando controles, reconectar el adaptador UDP
        if (enabled)
        {
            UDPMultiplayerAdapter adapter = FindObjectOfType<UDPMultiplayerAdapter>();
            if (adapter != null)
            {
                adapter.ReconnectToPlayer();
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"Controles de jugador: {(enabled ? "HABILITADOS" : "DESHABILITADOS")}");
    }
    #endregion

    #region Utility Methods
    void HideAllPanels()
    {
        if (waitingPanel != null) waitingPanel.SetActive(false);
        if (countdownPanel != null) countdownPanel.SetActive(false);
        if (matchPanel != null) matchPanel.SetActive(false);
        if (resultsPanel != null) resultsPanel.SetActive(false);
    }
    
    string FormatTime(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return string.Format("{0:00}:{1:00}", mins, secs);
    }
    
    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    #endregion

    #region Photon Callbacks
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (showDebugLogs)
            Debug.Log($"Jugador conectado: {newPlayer.NickName}");
        
        UpdatePlayerStatus();
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (showDebugLogs)
            Debug.LogWarning($"Jugador desconectado: {otherPlayer.NickName}");
        
        if (currentState == MatchState.Playing || currentState == MatchState.Countdown)
        {
            Debug.LogWarning("Jugador desconectado durante la partida");
        }
        
        UpdatePlayerStatus();
    }
    #endregion

    #region Public Methods for External Scripts
    /// <summary>
    /// Verifica si la partida está activa (para otros scripts)
    /// </summary>
    public bool IsMatchActive()
    {
        return currentState == MatchState.Playing;
    }
    
    /// <summary>
    /// Obtiene el tiempo restante de la partida
    /// </summary>
    public float GetTimeRemaining()
    {
        return timeRemaining;
    }
    
    /// <summary>
    /// Obtiene el estado actual de la partida
    /// </summary>
    public MatchState GetMatchState()
    {
        return currentState;
    }
    #endregion
}
