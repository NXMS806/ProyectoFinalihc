using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Photon.Pun;

[System.Serializable]
public class PhoneButtonData
{
    public float x; // Gyro X (ignorado pero mantenido para compatibilidad)
    public float y; // Gyro Y (ignorado)
    public float z; // Gyro Z (ignorado)
    public bool grabButton;      // Botón táctil para agarrar
    public bool highlightButton; // Botón táctil para highlight
}

public class PhoneButtonReceiver : MonoBehaviourPunCallbacks
{
    #region Configuration
    [Header("� Network Configuration")]
    [Tooltip("Puerto UDP para recibir datos del celular")]
    public int phonePort = 5006;
    
    [Header(" Button Settings")]
    [Tooltip("Activar/desactivar recepción de botones del celular")]
    public bool enablePhoneButtons = true;
    
    [Tooltip("Tiempo mínimo entre activaciones del mismo botón (anti-spam)")]
    public float buttonCooldown = 0.3f;
    
    [Header(" Debug")]
    [Tooltip("Mostrar mensajes de debug en consola")]
    public bool showDebugInfo = true;
    
    [Tooltip("Mostrar panel de debug en pantalla")]
    public bool showDebugPanel = false;
    #endregion

    #region Private Fields
    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning = false;
    private PhoneButtonData currentData = new PhoneButtonData();
    
    // Referencias al sistema principal
    private UDP mainUDPController;
    
    // Control de cooldown para botones
    private float lastGrabTime = -999f;
    private float lastHighlightTime = -999f;
    
    // Estado anterior para detectar cambios (press/release)
    private bool wasGrabPressed = false;
    private bool wasHighlightPressed = false;
    
    // Para detectar desconexión
    private DateTime lastPacketTime = DateTime.MinValue;
    private const double DISCONNECT_TIMEOUT = 3.0; // segundos
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        // Solo el Player1 (Limpiador) debe usar el control por celular
    }

    void Start()
    {
        // Buscar el controlador UDP principal
        mainUDPController = FindObjectOfType<UDP>();
        
        if (mainUDPController == null)
        {
            Debug.LogWarning(" [PhoneButtonReceiver] No se encontró componente UDP en la escena.");
            Debug.LogWarning("   Los botones del celular NO funcionarán.");
            Debug.LogWarning("   SOLUCIÓN: Asegúrate de que haya un GameObject con el script UDP.cs");
            return;
        }
        
        Debug.Log($" [PhoneButtonReceiver] UDP encontrado en: {mainUDPController.gameObject.name}");
        
        // Iniciar receptor UDP
        StartPhoneReceiver();
        
        Debug.Log(" [PhoneButtonReceiver] Sistema de botones del celular iniciado");
        Debug.Log($"   Puerto: {phonePort}");
        Debug.Log($"   Botones: {(enablePhoneButtons ? " Activos" : " Desactivados")}");
    }

    void Update()
    {
        if (!enablePhoneButtons) return;
        if (mainUDPController == null) return;
        
        // Procesar botones del celular
        ProcessPhoneButtons();
    }

    void OnDestroy()
    {
        StopPhoneReceiver();
    }

    void OnApplicationQuit()
    {
        StopPhoneReceiver();
    }
    #endregion

    #region UDP Receiver
    void StartPhoneReceiver()
    {
        try
        {
            udpClient = new UdpClient(phonePort);
            isRunning = true;
            
            receiveThread = new Thread(ReceivePhoneData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
            
            Debug.Log($" [PhoneButtonReceiver] Receptor UDP iniciado en puerto {phonePort}");
        }
        catch (Exception e)
        {
            Debug.LogError($" [PhoneButtonReceiver] Error iniciando receptor: {e.Message}");
        }
    }

    void StopPhoneReceiver()
    {
        isRunning = false;
        
        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch { }
            udpClient = null;
        }
        
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(1000);
        }
        
        Debug.Log("� [PhoneButtonReceiver] Receptor UDP cerrado");
    }

    void ReceivePhoneData()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, phonePort);
        
        while (isRunning && udpClient != null)
        {
            try
            {
                udpClient.Client.ReceiveTimeout = 1000;
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(data).Trim();
                
                if (!string.IsNullOrEmpty(json))
                {
                    PhoneButtonData newData = JsonUtility.FromJson<PhoneButtonData>(json);
                    if (newData != null)
                    {
                        currentData = newData;
                        lastPacketTime = DateTime.UtcNow;
                        
                        if (showDebugInfo && (newData.grabButton || newData.highlightButton))
                        {
                            Debug.Log($" [PhoneButtonReceiver] Grab={newData.grabButton}, Highlight={newData.highlightButton}");
                        }
                    }
                }
            }
            catch (SocketException se)
            {
                if (se.SocketErrorCode != SocketError.TimedOut)
                {
                    if (showDebugInfo)
                        Debug.LogWarning($" [PhoneButtonReceiver] SocketException: {se.Message}");
                }
                Thread.Sleep(10);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception e)
            {
                if (showDebugInfo)
                    Debug.LogError($" [PhoneButtonReceiver] Error: {e.Message}");
                Thread.Sleep(100);
            }
        }
    }
    #endregion

    #region Button Processing
    void ProcessPhoneButtons()
    {
        // Verificar si hay datos recientes
        if (!HasRecentData()) return;
        
        // Detectar PRESS del botón Grab (transición de false a true)
        if (currentData.grabButton && !wasGrabPressed)
        {
            if (Time.time - lastGrabTime > buttonCooldown)
            {
                lastGrabTime = Time.time;
                OnGrabButtonPressed();
            }
        }
        
        // Detectar PRESS del botón Highlight (transición de false a true)
        if (currentData.highlightButton && !wasHighlightPressed)
        {
            if (Time.time - lastHighlightTime > buttonCooldown)
            {
                lastHighlightTime = Time.time;
                OnHighlightButtonPressed();
            }
        }
        
        // Actualizar estado anterior
        wasGrabPressed = currentData.grabButton;
        wasHighlightPressed = currentData.highlightButton;
        
        // Enviar estado continuo al sistema UDP (para compatibilidad)
        if (mainUDPController != null)
        {
            mainUDPController.SetPhoneButtonState(currentData.grabButton, currentData.highlightButton);
        }
    }

    void OnGrabButtonPressed()
    {
        if (showDebugInfo)
            Debug.Log("✊ [PhoneButtonReceiver] ¡Botón GRAB presionado!");
        
        // El sistema UDP ya maneja la lógica de grab
        // Solo necesitamos asegurarnos de que el estado se envíe
    }

    void OnHighlightButtonPressed()
    {
        if (showDebugInfo)
            Debug.Log(" [PhoneButtonReceiver] ¡Botón HIGHLIGHT presionado!");
        
        // El sistema UDP ya maneja la lógica de highlight
    }
    #endregion

    #region Utility Methods
    bool HasRecentData()
    {
        if (lastPacketTime == DateTime.MinValue) return false;
        double seconds = (DateTime.UtcNow - lastPacketTime).TotalSeconds;
        return seconds < DISCONNECT_TIMEOUT;
    }

    public bool IsConnected()
    {
        return HasRecentData();
    }

    public PhoneButtonData GetCurrentData()
    {
        return currentData;
    }
    #endregion

    #region Debug GUI
    void OnGUI()
    {
        if (!showDebugPanel) return;
        
        float panelWidth = 280f;
        float panelHeight = 150f;
        float padding = 10f;
        
        Rect panelRect = new Rect(
            Screen.width - panelWidth - padding,
            padding,
            panelWidth,
            panelHeight
        );
        
        GUI.Box(panelRect, " Control Celular");
        
        float y = padding + 30f;
        float lineHeight = 22f;
        
        // Estado de conexión
        bool connected = IsConnected();
        GUI.color = connected ? Color.green : Color.red;
        GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight),
            $"Estado: {(connected ? " Conectado" : " Desconectado")}");
        y += lineHeight;
        GUI.color = Color.white;
        
        // Botón Grab
        GUI.color = currentData.grabButton ? Color.green : Color.gray;
        GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight),
            $"Botón Grab: {(currentData.grabButton ? "PRESIONADO" : "Inactivo")}");
        y += lineHeight;
        
        // Botón Highlight
        GUI.color = currentData.highlightButton ? Color.green : Color.gray;
        GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight),
            $"Botón Highlight: {(currentData.highlightButton ? "PRESIONADO" : "Inactivo")}");
        y += lineHeight;
        
        GUI.color = Color.white;
        
        // Puerto
        GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight),
            $"Puerto UDP: {phonePort}");
    }
    #endregion
}
