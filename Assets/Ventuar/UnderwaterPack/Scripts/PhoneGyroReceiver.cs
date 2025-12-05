using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receptor de datos del giroscopio del celular via UDP.
/// </summary>
[System.Serializable]
public class GyroData
{
    public float x; // Pitch (arriba/abajo)
    public float y; // Yaw (izquierda/derecha)  
    public float z; // Roll (inclinar lados)
    public bool grabButton;      // Botón táctil para agarrar
    public bool highlightButton; // Botón táctil para highlight
}

public class PhoneGyroReceiver : MonoBehaviour
{
    [Header("Network Configuration")]
    [Tooltip("Puerto UDP para recibir datos del giroscopio")]
    public int gyroPort = 5006; // Puerto diferente al de gestos (5005)
    
    [Header("Camera Control Settings")]
    [Tooltip("Transform de la cámara a controlar (normalmente Main Camera)")]
    public Transform cameraTransform;
    
    [Tooltip("Usar la inclinación (Roll Z) del celular para girar izquierda/derecha (Yaw)")]
    public bool yawFromRoll = true;
    
    [Tooltip("Invertir dirección de giro si al inclinar a la derecha gira al revés")]
    // Invertir por defecto para que la cámara se mueva en sentido opuesto
    // al movimiento del teléfono (si el teléfono va a la derecha, la cámara irá a la izquierda).
    public bool invertYaw = true;
    
    [Tooltip("Forzar que la cámara no rote en el eje Z (roll) – siempre 0°")]
    public bool lockRollZ = true;
    
    [Tooltip("Sensibilidad del giroscopio (más alto = más sensible)")]
    [Range(1f, 500f)]
    public float sensitivity = 35f; // Más baja para un giro controlado
    
    [Tooltip("Zona muerta: ignora movimientos menores a este valor (mayor = más estable)")]
    [Range(0f, 10f)]
    public float deadzone = 0.2f; // Muy baja para no bloquear inclinaciones
    
    [Tooltip("Curva de aceleración: movimientos lentos = control fino, rápidos = más sensibilidad")]
    [Range(1f, 3f)]
    public float accelerationCurve = 1.5f; // NUEVA: Control exponencial
    
    [Tooltip("Suavizado del movimiento (más alto = más suave)")]
    [Range(1f, 10f)]
    public float smoothing = 6f; // Un poco menos para mejor respuesta
    
    [Header("Rotation Limits")]
    [Tooltip("Límite máximo de inclinación hacia arriba/abajo (evita volteretas)")]
    [Range(45f, 89f)]
    public float maxPitch = 85f;
    
    [Header("System Control")]
    [Tooltip("Activar/desactivar control por giroscopio")]
    public bool enableGyroControl = true;
    
    [Tooltip("Activar/desactivar botones táctiles del celular")]
    public bool enablePhoneButtons = true;
    
    [Tooltip("Aplicar la rotación en LateUpdate (ayuda si otros scripts sobreescriben la cámara en Update)")]
    public bool applyInLateUpdate = true;
    
    [Header("Debug")]
    [Tooltip("Mostrar panel de debug en pantalla (desactivar para producción)")]
    public bool showDebugInfo = false; // DESACTIVADO para no molestar
    
    // Private fields
    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning = false;
    private GyroData currentGyro = new GyroData();
    private Vector3 targetRotation;
    private Vector3 currentRotation;
    private float debugYawInputAbs = 0f; // Para panel de debug
    private float yawRate = 0f; // deg/s filtrada
    
    // Calibración de cero para el roll (teléfono acostado, pantalla arriba)
    private float rollZeroOffset = 0f;
    private bool rollCalibrated = false;

    // Selección de eje para yaw por si el navegador mapea distinto
    public enum AxisSource { X, Y, Z }
    [Header("Axis Mapping")]
    [Tooltip("Eje del dato del celular a usar para girar izquierda/derecha")] 
    public AxisSource yawAxis = AxisSource.Z;
    
    // Variables de integración (no se usa mapping absoluto)
    
    // Sistema de detección de desconexión por tiempo (no por frames)
    private System.DateTime lastPacketAt = System.DateTime.MinValue;
    private const double DISCONNECT_SECONDS = 2.0; // sin datos => desconectado
    public float yawResponse = 10f; // rapidez de seguimiento de la velocidad objetivo
    
    // Referencia al sistema UDP principal para enviar botones
    private UDP mainUDPController;

    void Start()
    {
        // Auto-asignar cámara si no está asignada
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main?.transform;
            if (cameraTransform == null)
            {
                Debug.LogError(" PhoneGyroReceiver: No se encontró Main Camera");
                return;
            }
        }
        
        // Buscar el controlador UDP principal
        mainUDPController = FindObjectOfType<UDP>();
        if (mainUDPController == null)
        {
            Debug.LogWarning(" PhoneGyroReceiver: No se encontró componente UDP en la escena.");
            Debug.LogWarning("   Los botones del celular NO funcionarán.");
            Debug.LogWarning("   SOLUCIÓN: Asegúrate de que haya un GameObject con el script UDP.cs en la escena.");
        }
        else
        {
            Debug.Log($" PhoneGyroReceiver encontró UDP en: {mainUDPController.gameObject.name}");
        }
        
        // Inicializar rotación actual
        currentRotation = cameraTransform.eulerAngles;
        targetRotation = currentRotation;
        
        // Iniciar receptor UDP
        StartGyroReceiver();
        
        Debug.Log(" PhoneGyroReceiver iniciado correctamente");
        Debug.Log($"   Puerto giroscopio: {gyroPort}");
        Debug.Log($"   Control de cámara: {(enableGyroControl ? " Activo" : " Desactivado")}");
        Debug.Log($"   Botones táctiles: {(enablePhoneButtons ? " Activo" : " Desactivado")}");
    }

    void StartGyroReceiver()
    {
        try
        {
            udpClient = new UdpClient(gyroPort);
            isRunning = true;
            
            receiveThread = new Thread(ReceiveGyroData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
            
            Debug.Log($" Receptor de giroscopio iniciado en puerto {gyroPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($" Error iniciando receptor de giroscopio: {e.Message}");
        }
    }

    void ReceiveGyroData()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, gyroPort);
        
        while (isRunning && udpClient != null)
        {
            try
            {
                if (udpClient != null)
                {
                    udpClient.Client.ReceiveTimeout = 1000; // Timeout de 1 segundo
                    byte[] data = udpClient.Receive(ref remoteEndPoint);
                    string json = Encoding.UTF8.GetString(data).Trim();
                    
                    if (!string.IsNullOrEmpty(json))
                    {
                        GyroData newData = JsonUtility.FromJson<GyroData>(json);
                        if (newData != null)
                        {
                            currentGyro = newData;
                            lastPacketAt = System.DateTime.UtcNow; // marcar hora de llegada
                            
                            // DEBUG: Mostrar datos recibidos
                            if (showDebugInfo)
                            {
                                Debug.Log($" DATOS RECIBIDOS: X={newData.x:F2}, Y={newData.y:F2}, Z={newData.z:F2}, Grab={newData.grabButton}, Highlight={newData.highlightButton}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($" JSON recibido pero no se pudo parsear: {json}");
                        }
                    }
                }
            }
            catch (SocketException se)
            {
                // Timeout normal, continuar
                if (showDebugInfo && se.SocketErrorCode != SocketError.TimedOut)
                {
                    Debug.LogWarning($" SocketException: {se.SocketErrorCode} - {se.Message}");
                }
                Thread.Sleep(10);
            }
            catch (ObjectDisposedException)
            {
                // UDP client fue cerrado, salir del loop
                Debug.Log("� UDP Client cerrado - terminando ReceiveGyroData");
                break;
            }
            catch (Exception e)
            {
                if (showDebugInfo)
                {
                    Debug.LogError($" Error recibiendo datos del giroscopio: {e.GetType().Name} - {e.Message}");
                }
                Thread.Sleep(100);
            }
        }
    }

    void Update()
    {
        // No usamos el contador por frames; UpdateCameraRotation consultará el tiempo desde el último paquete
        
        // Control de cámara con giroscopio (si no usamos LateUpdate)
        if (!applyInLateUpdate)
        {
            if (enableGyroControl && cameraTransform != null)
            {
                UpdateCameraRotation();
            }
        }
        
        // Procesar botones táctiles del celular
        if (enablePhoneButtons && mainUDPController != null)
        {
            ProcessPhoneButtons();
        }
    }

    void LateUpdate()
    {
        if (applyInLateUpdate && enableGyroControl && cameraTransform != null)
        {
            UpdateCameraRotation();
        }
    }

    void UpdateCameraRotation()
    {
        // Usar el eje seleccionado (por defecto Z/Roll) para controlar el YAW
        float yawInput = yawAxis == AxisSource.X ? currentGyro.x : yawAxis == AxisSource.Y ? currentGyro.y : currentGyro.z;
        
        // Si no hay datos recientes, anular entrada para evitar saltos
        double since = (lastPacketAt == System.DateTime.MinValue) ? double.MaxValue : (System.DateTime.UtcNow - lastPacketAt).TotalSeconds;
        bool hasRecentData = since < 0.25; // 250 ms
        if (!hasRecentData)
        {
            yawInput = 0f;
        }
        if (invertYaw) yawInput = -yawInput;

        // Deadzone para evitar jitter
        float gyroMagnitude = Mathf.Abs(yawInput);
        if (gyroMagnitude < deadzone) yawInput = 0f;
        debugYawInputAbs = Mathf.Abs(yawInput);

    // Seguir una velocidad objetivo suavemente (filtro de primer orden)
    float targetYawRate = yawInput * sensitivity; // deg/s
    yawRate = Mathf.Lerp(yawRate, targetYawRate, Mathf.Clamp01(yawResponse * Time.deltaTime));
        
    // Cálculo de delta yaw estable
    float deltaYaw = yawRate * Time.deltaTime;

        // Tomar la rotación actual de la cámara
        Vector3 camEuler = cameraTransform.eulerAngles;

    // Nuevo yaw: aplicar directamente el incremento para asegurar respuesta
    float newYaw = camEuler.y + deltaYaw;

        // Mantener pitch, bloquear roll Z
    float newPitch = camEuler.x; // No modificamos pitch
        float newRoll = 0f;

        cameraTransform.rotation = Quaternion.Euler(newPitch, newYaw, lockRollZ ? 0f : camEuler.z);
        
        if (showDebugInfo && (Mathf.Abs(yawInput) > deadzone || !hasRecentData))
        {
            Debug.Log($"� Apply Yaw: input={yawInput:F3} rate={yawRate:F2} deg/s sens={sensitivity:F1} dt={Time.deltaTime:F3} recent={(float)since:F3}s → yaw={newYaw:F1}");
        }
    }

    void ProcessPhoneButtons()
    {
        // Notificar al sistema principal sobre los botones del celular
        // Esto se integra con el sistema existente de gestos
        mainUDPController.SetPhoneButtonState(currentGyro.grabButton, currentGyro.highlightButton);
        
        // DEBUG: Mostrar cuando se presionan botones
        if (showDebugInfo && (currentGyro.grabButton || currentGyro.highlightButton))
        {
            Debug.Log($"� BOTONES CELULAR: Grab={currentGyro.grabButton}, Highlight={currentGyro.highlightButton}");
        }
    }

    /// <summary>
    /// Recalibrar la orientación del giroscopio
    /// Útil si el usuario cambia de posición
    /// </summary>
    public void RecalibrateGyro()
    {
        currentRotation = cameraTransform.eulerAngles;
        targetRotation = currentRotation;
        // Recalibrar también el cero del roll (teléfono acostado, pantalla arriba)
        rollZeroOffset = currentGyro.z;
        rollCalibrated = true;
    Debug.Log($" Giroscopio recalibrado | rollZeroOffset={rollZeroOffset:F2}");
    }

    /// <summary>
    /// Obtener el estado actual del giroscopio (para debug)
    /// </summary>
    public GyroData GetCurrentGyroData()
    {
        return currentGyro;
    }

    void OnGUI()
    {
        if (showDebugInfo && enableGyroControl)
        {
            // Panel de debug en esquina superior derecha
            GUI.color = Color.white;
            GUI.skin.box.fontSize = 16;
            GUI.skin.label.fontSize = 14;
            
            float panelWidth = 340f;
            float panelHeight = 230f; // Aumentado para nuevas métricas
            float padding = 10f;
            
            Rect panelRect = new Rect(
                Screen.width - panelWidth - padding,
                padding,
                panelWidth,
                panelHeight
            );
            
            GUI.Box(panelRect, " Control del Celular (MEJORADO)");
            
            float y = padding + 30f;
            float lineHeight = 25f;
            
            // Calcular magnitud para mostrar (señal real usada para yaw)
            float gyroMag = debugYawInputAbs;
            
            GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight), 
                $"Gyro X (Pitch): {currentGyro.x:F2}");
            y += lineHeight;
            
            GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight), 
                $"Gyro Y (Yaw): {currentGyro.y:F2}");
            y += lineHeight;
            
            GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight), 
                $"Gyro Z (Roll): {currentGyro.z:F2}");
            y += lineHeight;

            // Mostrar eje usado para Yaw
            GUI.color = Color.cyan;
            GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight),
                $"Yaw Axis: {yawAxis}");
            GUI.color = Color.white;
            y += lineHeight;
            
            // NUEVO: Mostrar magnitud y deadzone
            GUI.color = gyroMag < deadzone ? Color.yellow : Color.green;
            GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight), 
                $"Magnitud: {gyroMag:F1} (Deadzone: {deadzone:F0})");
            y += lineHeight;
            GUI.color = Color.white;
            
            GUI.color = currentGyro.grabButton ? Color.green : Color.gray;
            GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight), 
                $"Botón Agarrar: {(currentGyro.grabButton ? "PRESIONADO" : "Inactivo")}");
            y += lineHeight;
            
            GUI.color = currentGyro.highlightButton ? Color.green : Color.gray;
            GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight), 
                $"Botón Highlight: {(currentGyro.highlightButton ? "PRESIONADO" : "Inactivo")}");
            y += lineHeight;
            
            // NUEVO: Mostrar configuración activa
            GUI.color = Color.cyan;
            GUI.Label(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight), 
                $"Sens: {sensitivity:F0} | Suavizado: {smoothing:F1}");
            GUI.color = Color.white;
            y += lineHeight;

            // Slider para ajustar sensibilidad en vivo
            float newSens = GUI.HorizontalSlider(new Rect(panelRect.x + 10, y, panelWidth - 20, 20f), sensitivity, 1f, 200f);
            if (Mathf.Abs(newSens - sensitivity) > 0.001f)
            {
                sensitivity = newSens;
            }
            y += lineHeight;

            // Botones rápidos de prueba: cambiar eje, invertir, recalibrar
            if (GUI.Button(new Rect(panelRect.x + 10, y, (panelWidth-30)/2, lineHeight), "Toggle Axis"))
            {
                yawAxis = (AxisSource)(((int)yawAxis + 1) % 3);
            }
            // Mostrar el estado actual de la inversión de yaw
            if (GUI.Button(new Rect(panelRect.x + 20 + (panelWidth-30)/2, y, (panelWidth-30)/2, lineHeight), invertYaw ? "Invert: ON" : "Invert: OFF"))
            {
                invertYaw = !invertYaw;
            }
            y += lineHeight + 5f;

            if (GUI.Button(new Rect(panelRect.x + 10, y, panelWidth - 20, lineHeight), "Recalibrar (Roll Zero)"))
            {
                RecalibrateGyro();
            }
        }
    }

    void OnApplicationQuit()
    {
        StopGyroReceiver();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            StopGyroReceiver();
        }
    }

    void StopGyroReceiver()
    {
        isRunning = false;
        
        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch (Exception e)
            {
                Debug.Log($"Error cerrando UDP del giroscopio: {e.Message}");
            }
            udpClient = null;
        }
        
        if (receiveThread != null && receiveThread.IsAlive)
        {
            if (!receiveThread.Join(1000)) // Timeout de 1 segundo
            {
                receiveThread.Abort();
            }
        }
        
        Debug.Log(" PhoneGyroReceiver detenido");
    }
}
