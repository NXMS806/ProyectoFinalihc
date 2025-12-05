using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

[System.Serializable]
public class PoseData
{
    // Gestos de manos (Sistema original)
    public bool RightHandUp;
    public bool LeftHandUp;
    public bool BothHandsUp;
    public bool PalmsTogetherPraying;  // Manos juntas - FUNCIONA BIEN
    public bool Grab;
    public bool ThumbsUp;  // Nuevo gesto para iluminar basura
    
    // Botones del celular (control dual)
    // Estos campos son opcionales y permiten control dual
    public bool PhoneGrab;      // Botón táctil para agarrar (alternativa a Grab)
    public bool PhoneHighlight; // Botón táctil para highlight (alternativa a ThumbsUp)
}

[System.Serializable]
public class GestureSettings
{
    [Header("Movement Settings")]
    [Tooltip("Velocidad de movimiento adelante/atrás")]
    public float moveSpeed = 12f;
    
    [Tooltip("Velocidad de rotación derecha/izquierda (grados por segundo)")]
    public float rotationSpeed = 200f;
    
    [Tooltip("Distancia máxima para agarrar objetos")]
    public float grabDistance = 5f;
    
    [Header("Fine Tuning")]
    [Tooltip("Multiplicador para movimiento hacia adelante")]
    public float forwardMultiplier = 3f;
    
    [Tooltip("Multiplicador para movimiento hacia atrás")]
    public float backwardMultiplier = 2f;
    
    [Header("Debug")]
    public bool showDebugLogs = false;
    public bool showGestureStatus = true;
}

public class UDP : MonoBehaviour
{
    [Header("Network Configuration")]
    public int port = 5005;
    
    [Header("VR Object Control")]
    public Transform objectToMove;
    
    [Header("Gesture Settings")]
    public GestureSettings gestureSettings = new GestureSettings();
    
    [Header("Grab Cooldown")]
    [Tooltip("Tiempo en segundos entre agarres (evita detección múltiple)")]
    public float grabCooldownTime = 1.0f;
    
    [Header("Highlight Toggle")]
    [Tooltip("Tiempo de cooldown para toggle de highlight (evita toggle accidental)")]
    public float highlightCooldownTime = 0.5f;
    
    // Private fields
    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile string lastMessage = "";
    private PoseData currentPoseData = new PoseData();
    
    // Sistema de highlight por gesto
    private bool highlightModeActive = false;
    private bool lastThumbsUpState = false;
    
    // Sistema dual: Celular + Gestos
    private bool phoneGrabButton = false;
    private bool phoneHighlightButton = false;
    private bool lastPhoneHighlightState = false; 

    void Start()
    {
        Debug.Log($" Clean Ocean VR - Starting UDP receiver on port {port}");
        
        try
        {
            udpClient = new UdpClient(port);
            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
            
            Debug.Log(" UDP receiver started successfully");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($" Failed to start UDP receiver: {ex.Message}");
        }
    }

    void ReceiveData()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, port);
        while (udpClient != null)
        {
            try
            {
                if (udpClient != null)
                {
                    udpClient.Client.ReceiveTimeout = 1000; // Timeout de 1 segundo
                    byte[] data = udpClient.Receive(ref remoteEndPoint);
                    string message = Encoding.UTF8.GetString(data).Trim();
                    if (!string.IsNullOrEmpty(message))
                    {
                        lastMessage = message;
                    }
                }
            }
            catch (System.ObjectDisposedException)
            {
                // UDP client was disposed, exit thread cleanly
                break;
            }
            catch (System.Net.Sockets.SocketException)
            {
                // Timeout o conexión cancelada - normal al cerrar
                Thread.Sleep(100);
            }
            catch (System.Exception ex)
            {
                if (gestureSettings != null && gestureSettings.showDebugLogs)
                {
                    Debug.LogWarning($"UDP Receive Error: {ex.Message}");
                }
                Thread.Sleep(100);
            }
        }
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(lastMessage) && objectToMove != null)
        {
            try
            {
                PoseData newData = JsonUtility.FromJson<PoseData>(lastMessage);
                
                // Update current pose data
                currentPoseData = newData;
                
                // Apply movements based on gestures
                ApplyGestureMovement(newData);
                
                // Handle grab actions
                HandleGrabAction(newData);
                
                // Handle highlight toggle (nuevo sistema)
                HandleHighlightToggle(newData);
                
                // Update trash highlighting (sistema existente de proximidad)
                UpdateTrashHighlighting();
                
                // Debug logging if enabled
                if (gestureSettings.showDebugLogs)
                {
                    LogGestureStatus(newData);
                }
            }
            catch (System.Exception ex)
            {
                if (gestureSettings.showDebugLogs)
                {
                    Debug.LogWarning($"Error deserializing gesture data: {ex.Message}");
                }
            }
        }
    }
    
    void ApplyGestureMovement(PoseData data)
    {
        // Rotation controls - ROTACIÓN DIRECTA Y RÁPIDA (sin Time.deltaTime para más velocidad)
        if (data.LeftHandUp && !data.RightHandUp)
        {
            // Rotación instantánea por frame - mucho más rápido
            objectToMove.Rotate(Vector3.up, -gestureSettings.rotationSpeed * 0.05f);
        }
        
        if (data.RightHandUp && !data.LeftHandUp)
        {
            // Rotación instantánea por frame - mucho más rápido
            objectToMove.Rotate(Vector3.up, gestureSettings.rotationSpeed * 0.05f);
        }
        
        // Movement controls - adelante más notorio, atrás más controlado
        if (data.BothHandsUp)
        {
            float forwardSpeed = gestureSettings.moveSpeed * gestureSettings.forwardMultiplier;
            objectToMove.Translate(Vector3.forward * forwardSpeed * Time.deltaTime);
        }
        
        // Movimiento hacia atrás - Manos juntas
        if (data.PalmsTogetherPraying)
        {
            float backwardSpeed = gestureSettings.moveSpeed * gestureSettings.backwardMultiplier;
            objectToMove.Translate(Vector3.back * backwardSpeed * Time.deltaTime);
        }
    }
    
    // Variables para cooldown del agarre
    private float lastGrabTime = 0f;
    
    void HandleGrabAction(PoseData data)
    {
        // Sistema dual: Acepta GESTO (puno cerrado) OR BOTON DEL CELULAR
        bool grabTriggered = data.Grab || phoneGrabButton;
        
        if (grabTriggered && Time.time > lastGrabTime + grabCooldownTime)
        {
            // Buscar objetos GrabbableObject (sistema anterior)
            GrabbableObject nearest = FindClosestGrabbable();
            if (nearest != null)
            {
                Destroy(nearest.gameObject);
                lastGrabTime = Time.time; // Registrar tiempo de agarre
                if (gestureSettings.showDebugLogs)
                {
                    Debug.Log($"� Grabbed and destroyed: {nearest.name}");
                }
                return; // Si encontró GrabbableObject, no buscar más
            }
            
            // Buscar objetos TrashObject (sistema nuevo)
            TrashObject nearestTrash = FindClosestTrash();
            if (nearestTrash != null)
            {
                nearestTrash.GrabObject();
                lastGrabTime = Time.time; // Registrar tiempo de agarre
                if (gestureSettings.showDebugLogs)
                {
                    Debug.Log($" Trash collected: {nearestTrash.name}");
                }
            }
        }
    }
    
    void LogGestureStatus(PoseData data)
    {
        string status = $"Gestures - Right: {data.RightHandUp}, Left: {data.LeftHandUp}, Both: {data.BothHandsUp}, Pray: {data.PalmsTogetherPraying}, Grab: {data.Grab}";
        Debug.Log(status);
    }

    GrabbableObject FindClosestGrabbable()
    {
        GrabbableObject[] all = GameObject.FindObjectsOfType<GrabbableObject>();
        GrabbableObject nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var grabbable in all)
        {
            float distance = Vector3.Distance(objectToMove.position, grabbable.transform.position);
            if (distance < minDist && distance < gestureSettings.grabDistance)
            {
                minDist = distance;
                nearest = grabbable;
            }
        }
        
        return nearest;
    }
    
    // Public method to get current gesture state (useful for UI or other scripts)
    public PoseData GetCurrentGestureState()
    {
        return currentPoseData;
    }
    
    /// <summary>
    /// Encuentra el objeto de basura más cercano (nuevo sistema)
    /// </summary>
    TrashObject FindClosestTrash()
    {
        TrashObject[] allTrash = GameObject.FindObjectsOfType<TrashObject>();
        TrashObject nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var trash in allTrash)
        {
            if (trash.CanBeGrabbed(objectToMove.position))
            {
                float distance = trash.GetDistanceToPlayer(objectToMove.position);
                if (distance < minDist)
                {
                    minDist = distance;
                    nearest = trash;
                }
            }
        }
        
        return nearest;
    }
    
    
    // Variables para cooldown del highlight toggle
    private float lastHighlightToggleTime = 0f;
    
    /// <summary>
    /// Maneja el toggle del modo de iluminacion por gesto (dual: gesto o boton celular).
    /// </summary>
    void HandleHighlightToggle(PoseData data)
    {
        // SISTEMA DUAL: Gesto thumbs up OR botón del celular
        bool highlightTriggered = data.ThumbsUp || phoneHighlightButton;
        bool lastHighlightState = lastThumbsUpState || lastPhoneHighlightState;
        
        // Detectar flanco de subida (cambio de false a true)
        if (highlightTriggered && !lastHighlightState && Time.time > lastHighlightToggleTime + highlightCooldownTime)
        {
            // Toggle del modo
            highlightModeActive = !highlightModeActive;
            lastHighlightToggleTime = Time.time;
        
            //  REPRODUCIR SONIDO DE HIGHLIGHT
            
            if (OceanAudioSystem.Instance != null)
            {
                if (highlightModeActive)
                {
                    OceanAudioSystem.Instance.PlayHighlightOnSound();
                }
                else
                {
                    OceanAudioSystem.Instance.PlayHighlightOffSound();
                }
            }
            
            
            // Aplicar el nuevo estado a todos los objetos
            ToggleAllTrashHighlight(highlightModeActive);
            
            if (gestureSettings.showDebugLogs)
            {
                Debug.Log($" Highlight Mode: {(highlightModeActive ? "ACTIVADO" : "DESACTIVADO")}");
            }
        }
        
        lastThumbsUpState = data.ThumbsUp;
        lastPhoneHighlightState = phoneHighlightButton;
    }
    
    /// <summary>
    /// Activa o desactiva la iluminación forzada de todos los objetos de basura
    /// </summary>
    void ToggleAllTrashHighlight(bool state)
    {
        TrashObject[] allTrash = GameObject.FindObjectsOfType<TrashObject>();
        
        foreach (TrashObject trash in allTrash)
        {
            trash.ForceHighlight(state);
        }
        
        if (gestureSettings.showGestureStatus)
        {
            Debug.Log($" Highlight forzado {(state ? "ACTIVADO" : "DESACTIVADO")} en {allTrash.Length} objetos");
        }
    }
    
    /// <summary>
    /// Sistema de highlighting para objetos cercanos (mejorado)
    /// Solo funciona cuando el modo forzado NO está activo
    /// </summary>
    void UpdateTrashHighlighting()
    {
        // Si el modo forzado está activo, no hacer nada aquí
        // El highlight está controlado por ForceHighlight
        if (highlightModeActive)
        {
            return;
        }
        
        TrashObject[] allTrash = GameObject.FindObjectsOfType<TrashObject>();
        
        foreach (var trash in allTrash)
        {
            float distance = trash.GetDistanceToPlayer(objectToMove.position);
            
            if (distance <= trash.grabDistance)
            {
                trash.HighlightObject();
                
                // Debug opcional para ver la distancia
                if (gestureSettings.showDebugLogs)
                {
                    Debug.Log($"Objeto '{trash.name}' cerca - Distancia: {distance:F1}");
                }
            }
            else
            {
                trash.RemoveHighlight();
            }
        }
    }

    void OnDestroy()
    {
        StopUDPReceiver();
    }
    
    void StopUDPReceiver()
    {
        // Close UDP client first to stop the receiving thread
        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
                udpClient.Dispose();
                udpClient = null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error closing UDP client: {ex.Message}");
            }
        }
        
        // Then wait for thread to finish
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(1000); // Wait up to 1 second for thread to finish
            receiveThread = null;
        }
    }
    
    /// <summary>
    /// Reinicia el receptor UDP si fue cerrado previamente.
    /// Llamado automáticamente cuando el script se habilita de nuevo.
    /// </summary>
    void RestartUDPReceiver()
    {
        // Solo reiniciar si el cliente fue cerrado
        if (udpClient == null)
        {
            Debug.Log($"🔄 UDP: Reiniciando receptor en puerto {port}...");
            
            try
            {
                udpClient = new UdpClient(port);
                receiveThread = new Thread(ReceiveData);
                receiveThread.IsBackground = true;
                receiveThread.Start();
                
                Debug.Log("✅ UDP receptor reiniciado correctamente");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Error al reiniciar UDP receiver: {ex.Message}");
            }
        }
    }

    void OnApplicationQuit()
    {
        StopUDPReceiver();
    }
    
    void OnDisable()
    {
        // YA NO cerramos el socket aquí para evitar perder la conexión
        // durante el reinicio de partida. Solo lo cerramos en OnDestroy/OnApplicationQuit.
        Debug.Log("⏸️ UDP: Script deshabilitado (socket permanece activo)");
    }
    
    void OnEnable()
    {
        // Si el script se habilita y el socket está cerrado, reiniciarlo
        if (udpClient == null && Application.isPlaying)
        {
            RestartUDPReceiver();
        }
        else
        {
            Debug.Log("▶️ UDP: Script habilitado (socket ya activo)");
        }
    }
    
    /// <summary>
    /// Método público para que el HUD pueda verificar el estado del highlight
    /// </summary>
    /// <returns>True si el modo highlight está activo, false en caso contrario</returns>
    public bool IsHighlightModeActive()
    {
        return highlightModeActive;
    }
    
    /// <summary>
    /// Actualiza el estado de los botones del celular (sistema dual).
    /// </summary>
    /// <param name="grabButton">Estado del botón de agarrar del celular</param>
    /// <param name="highlightButton">Estado del botón de highlight del celular</param>
    public void SetPhoneButtonState(bool grabButton, bool highlightButton)
    {
        phoneGrabButton = grabButton;
        phoneHighlightButton = highlightButton;
        
        // DEBUG: Mostrar cuando cambian los botones
        if (grabButton || highlightButton)
        {
            Debug.Log($" UDP recibió botones: Grab={grabButton}, Highlight={highlightButton}");
        }
    }
}
