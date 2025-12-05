using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;

public class NetworkReconnectionManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [Tooltip("Panel que se muestra cuando alguien se desconecta")]
    public GameObject reconnectionPanel;
    
    [Tooltip("Texto del mensaje principal")]
    public Text statusText;
    
    [Tooltip("Texto del countdown")]
    public Text timerText;
    
    [Tooltip("Botón para volver al menú (cuando timeout)")]
    public Button menuButton;
    
    [Header("Reconnection Settings")]
    [Tooltip("Tiempo máximo de espera para reconexión (5 minutos = 300 segundos)")]
    public float maxWaitTime = 300f; 
    
    [Tooltip("Intervalo de intento de reconexión automática (cada X segundos)")]
    public float retryInterval = 5f; // Cada 5 segundos intenta reconectar
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    // Estado interno
    private float waitTimer = 0f;
    private float retryTimer = 0f;
    private bool isWaitingForReconnection = false;
    private bool isDisconnectedFromInternet = false;
    private string disconnectedPlayerName = "";
    private string lastRoomName = "";
    
    void Start()
    {

        
        // 1. Activar reconexión automática
        PhotonNetwork.AutomaticallySyncScene = true;
        
        // 2. Configurar timeouts más largos para esperar reconexión
        PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 10000; // 10 segundos
        
        // 3. Guardar nombre de sala actual
        if (PhotonNetwork.InRoom)
        {
            lastRoomName = PhotonNetwork.CurrentRoom.Name;
            if (showDebugLogs)
                Debug.Log($"� Sala guardada: {lastRoomName}");
        }
        
        // Ocultar panel inicialmente
        if (reconnectionPanel != null)
            reconnectionPanel.SetActive(false);
            
        // Configurar botón de menú
        if (menuButton != null)
        {
            menuButton.onClick.AddListener(OnMenuButtonClicked);
            menuButton.gameObject.SetActive(false); // Oculto al inicio
        }
        
        if (showDebugLogs)
        {
            Debug.Log("� NetworkReconnectionManager inicializado");
            Debug.Log($" Tiempo máximo de espera: {maxWaitTime} segundos ({maxWaitTime/60f} minutos)");
            Debug.Log($" Intervalo de reintento: {retryInterval} segundos");
        }
    }
    
    void Update()
    {
 
        if (isWaitingForReconnection)
        {
            // Actualizar timer principal (usar unscaledDeltaTime porque el juego está pausado)
            waitTimer -= Time.unscaledDeltaTime;
            
            // Actualizar UI del countdown
            UpdateTimerUI();
            
            // Timer de reintentos automáticos
            retryTimer -= Time.unscaledDeltaTime;
            
            if (retryTimer <= 0)
            {
                // Intentar reconectar automáticamente
                AttemptReconnection();
                retryTimer = retryInterval; // Resetear para próximo intento
            }
            
            // Verificar si se agotó el tiempo
            if (waitTimer <= 0)
            {
                OnReconnectionTimeout();
            }
        }
        
  
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsConnected)
        {
            // YO perdí conexión (no el otro jugador)
            if (!isDisconnectedFromInternet)
            {
                OnILostConnection();
            }
        }
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        disconnectedPlayerName = otherPlayer.NickName;
        
        if (showDebugLogs)
            Debug.LogWarning($" {disconnectedPlayerName} se desconectó de la sala");
        
        // PAUSAR EL JUEGO
        Time.timeScale = 0f;
        
        // Mostrar UI de espera
        ShowWaitingUI($"{disconnectedPlayerName} perdió la conexión");
        
        // Iniciar countdown y reintentos
        StartWaitingForReconnection();
    }
    
 
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (isWaitingForReconnection)
        {
            if (showDebugLogs)
                Debug.Log($" {newPlayer.NickName} SE RECONECTÓ!");
            
            // REANUDAR JUEGO
            Time.timeScale = 1f;
            
            // Ocultar UI
            HideReconnectionUI();
            
            // Mostrar mensaje de bienvenida
            ShowTemporaryMessage($" {newPlayer.NickName} volvió al juego", Color.green);
            
            // Resetear estado
            isWaitingForReconnection = false;
            isDisconnectedFromInternet = false;
        }
    }
    
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        if (showDebugLogs)
            Debug.LogWarning($" Desconectado del servidor. Causa: {cause}");
        
        if (cause == DisconnectCause.DisconnectByClientLogic ||
            cause == DisconnectCause.DisconnectByServerLogic ||
            cause == DisconnectCause.Exception ||
            cause == DisconnectCause.ExceptionOnConnect ||
            cause == DisconnectCause.ServerTimeout ||
            cause == DisconnectCause.ClientTimeout)
        {
            OnILostConnection();
        }
    }
    
    
    public override void OnConnectedToMaster()
    {
        if (isDisconnectedFromInternet && !string.IsNullOrEmpty(lastRoomName))
        {
            if (showDebugLogs)
                Debug.Log($" Reconectado al Master Server. Intentando volver a sala: {lastRoomName}");
            
            // Intentar volver a la sala anterior
            PhotonNetwork.RejoinRoom(lastRoomName);
        }
    }
    
   
    public override void OnJoinedRoom()
    {
        lastRoomName = PhotonNetwork.CurrentRoom.Name;
        
        if (showDebugLogs)
            Debug.Log($"� Sala guardada: {lastRoomName}");
        
        // Si acabamos de reconectar
        if (isDisconnectedFromInternet)
        {
            if (showDebugLogs)
                Debug.Log($" ¡RECONEXIÓN EXITOSA! Volví a la sala: {PhotonNetwork.CurrentRoom.Name}");
            
            // Reanudar juego
            Time.timeScale = 1f;
            
            // Ocultar UI
            HideReconnectionUI();
            
            // Mostrar mensaje de éxito
            ShowTemporaryMessage(" ¡Reconexión exitosa!", Color.green);
            
            // Resetear estado
            isWaitingForReconnection = false;
            isDisconnectedFromInternet = false;
        }
    }
    
    
    void OnILostConnection()
    {
        isDisconnectedFromInternet = true;
        
        if (showDebugLogs)
            Debug.LogWarning(" YO perdí la conexión a internet");
        
        // PAUSAR JUEGO
        Time.timeScale = 0f;
        
        // Mostrar UI
        ShowWaitingUI("Perdiste la conexión a internet");
        
        // Iniciar countdown
        StartWaitingForReconnection();
    }
    
    
    void StartWaitingForReconnection()
    {
        isWaitingForReconnection = true;
        waitTimer = maxWaitTime;
        retryTimer = retryInterval;
    }
    
    
    void AttemptReconnection()
    {
        if (showDebugLogs)
            Debug.Log(" Intentando reconectar...");
        
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ReconnectAndRejoin();
        }
        else if (PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.CurrentRoom.PlayerCount == 2)
            {
                if (showDebugLogs)
                    Debug.Log(" ¡Ambos jugadores reconectados!");
                
                Time.timeScale = 1f;
                HideReconnectionUI();
                isWaitingForReconnection = false;
                isDisconnectedFromInternet = false;
            }
        }
    }
    
    
    void OnReconnectionTimeout()
    {
        isWaitingForReconnection = false;
        
        if (showDebugLogs)
            Debug.LogError(" Tiempo de reconexión agotado (5 minutos)");
        
        if (statusText != null)
        {
            statusText.text = " No se pudo reconectar\n\n" +
                             "Tiempo de espera agotado (5 minutos)\n\n" +
                             "¿Volver al menú principal?";
        }
        
        // Mostrar botón de menú
        if (menuButton != null)
            menuButton.gameObject.SetActive(true);
    }
    
    
    void ShowWaitingUI(string reason)
    {
        if (reconnectionPanel != null)
        {
            reconnectionPanel.SetActive(true);
            
            if (statusText != null)
            {
                statusText.text = $" {reason}\n\n" +
                                 $"Esperando reconexión...\n\n" +
                                 $"(El juego se reanudará automáticamente)";
            }
        }
    }
    
    
    
    void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(waitTimer / 60f);
            int seconds = Mathf.FloorToInt(waitTimer % 60f);
            
            timerText.text = $" {minutes:00}:{seconds:00}";
            
            // Cambiar color según tiempo restante
            if (waitTimer <= 30)
                timerText.color = Color.red;
            else if (waitTimer <= 60)
                timerText.color = Color.yellow;
            else
                timerText.color = Color.white;
        }
    }
    
    
    void HideReconnectionUI()
    {
        if (reconnectionPanel != null)
            reconnectionPanel.SetActive(false);
            
        if (menuButton != null)
            menuButton.gameObject.SetActive(false);
    }
    
    
    
    void ShowTemporaryMessage(string message, Color color)
    {
        if (showDebugLogs)
            Debug.Log($"� {message}");
        
    }
    
    
    
    void OnMenuButtonClicked()
    {
        if (showDebugLogs)
            Debug.Log("� Volviendo al menú principal...");
        
        // Reanudar tiempo
        Time.timeScale = 1f;
        
        // Desconectar de Photon
        PhotonNetwork.LeaveRoom();
        PhotonNetwork.Disconnect();
        
        // Cargar escena de menú
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
