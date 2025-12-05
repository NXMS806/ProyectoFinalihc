using UnityEngine;
using Photon.Pun;

public class CameraRoleManager : MonoBehaviour
{
    [Header("Cámaras del Juego")]
    [Tooltip("Cámara 3D para el Jugador 1 (Limpiador)")]
    public Camera mainCamera3D;
    
    [Tooltip("Cámara 2D ortográfica para el Jugador 2 (Contaminador)")]
    public Camera strategyCamera2D;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    void Start()
    {
        // Desactivar AMBAS cámaras inicialmente
        if (mainCamera3D != null)
            mainCamera3D.enabled = false;
            
        if (strategyCamera2D != null)
            strategyCamera2D.gameObject.SetActive(false);
        
        if (showDebugLogs)
            Debug.Log("CameraRoleManager: Esperando asignacion de rol...");
        
        Invoke("ConfigureCameras", 1f);
    }
    
    void ConfigureCameras()
    {
        // Modo single player
        if (!PhotonNetwork.IsConnected)
        {
            if (mainCamera3D != null)
            {
                mainCamera3D.enabled = true;
                
                if (showDebugLogs)
                    Debug.Log("Single Player - Main Camera 3D activada");
            }
            return;
        }
        
        // Buscar jugador local
        PhotonView[] allPlayers = FindObjectsOfType<PhotonView>();
        bool foundLocalPlayer = false;
        
        foreach (PhotonView pv in allPlayers)
        {
            if (pv.IsMine)
            {
                if (pv.gameObject.name.Contains("Player1"))
                {
                    ActivateCamera3D();
                    foundLocalPlayer = true;
                    break;
                }
                else if (pv.gameObject.name.Contains("Player2"))
                {
                    ActivateCamera2D();
                    foundLocalPlayer = true;
                    break;
                }
            }
        }
        
        if (!foundLocalPlayer && showDebugLogs)
        {
            Debug.LogWarning("Jugador no encontrado, reintentando...");
            Invoke("ConfigureCameras", 1f);
        }
    }
    
    void ActivateCamera3D()
    {
        if (mainCamera3D != null)
        {
            mainCamera3D.enabled = true;
            
            // Activar AudioListener de esta cámara
            AudioListener listener3D = mainCamera3D.GetComponent<AudioListener>();
            if (listener3D != null)
                listener3D.enabled = true;
            
            if (showDebugLogs)
                Debug.Log("LIMPIADOR - Main Camera 3D ACTIVADA");
        }
        
        // Desactivar cámara 2D
        if (strategyCamera2D != null)
        {
            strategyCamera2D.enabled = false;
            
            AudioListener listener2D = strategyCamera2D.GetComponent<AudioListener>();
            if (listener2D != null)
                listener2D.enabled = false;
        }
    }
    
    void ActivateCamera2D()
    {
        if (strategyCamera2D != null)
        {
            strategyCamera2D.gameObject.SetActive(true);
            strategyCamera2D.enabled = true;
            
            // Activar AudioListener de esta cámara
            AudioListener listener2D = strategyCamera2D.GetComponent<AudioListener>();
            if (listener2D != null)
                listener2D.enabled = true;
            
            if (showDebugLogs)
                Debug.Log("CONTAMINADOR - Strategy Camera 2D ACTIVADA");
        }
        
        // Desactivar cámara 3D
        if (mainCamera3D != null)
        {
            mainCamera3D.enabled = false;
            
            AudioListener listener3D = mainCamera3D.GetComponent<AudioListener>();
            if (listener3D != null)
                listener3D.enabled = false;
        }
    }
}

