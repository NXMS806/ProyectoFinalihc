using UnityEngine;
using Photon.Pun;

/// <summary>
/// Objeto de basura que puede ser agarrado y sincronizado por red.
/// </summary>
public class TrashObject : MonoBehaviourPunCallbacks
{
    [Header("Configuración de Basura")]
    [Tooltip("Puntos que otorga este objeto al ser recolectado")]
    public int points = 1;
    
    [Tooltip("Distancia máxima para poder agarrar este objeto")]
    public float grabDistance = 10f;
    
    [Header("Efectos Visuales")]
    [Tooltip("Material cuando el objeto está cerca y se puede agarrar")]
    public Material highlightMaterial;
    
    [Header("Referencias Internas")]
    private Material originalMaterial;
    private Renderer objectRenderer;
    private bool isHighlighted = false;
    private bool isBeingGrabbed = false; // Evitar doble agarre
    private bool forcedHighlight = false; // Sistema de highlight forzado por gesto
    
    // Referencia a PhotonView (agregado para sincronización)
    private PhotonView photonView;
    
    [Header("Sistema de Partida Competitiva")]
    [Tooltip("TRUE si fue spawneado por el Contaminador (Player2)")]
    public bool spawnedByPolluter = false;
    
    void Start()
    {
        // Obtener o agregar PhotonView
        photonView = GetComponent<PhotonView>();
        if (photonView == null)
        {
            photonView = gameObject.AddComponent<PhotonView>();
            Debug.LogWarning($" TrashObject '{name}': PhotonView agregado automáticamente. Recomendado agregarlo manualmente en el prefab.");
        }
        
        // Guardar material original
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
        }
        
        // Buscar material de highlight automáticamente si no está asignado
        if (highlightMaterial == null)
        {
            highlightMaterial = Resources.Load<Material>("HighlightMaterial");
            if (highlightMaterial == null)
            {
                // Crear material de highlight programáticamente
                highlightMaterial = new Material(Shader.Find("Standard"));
                highlightMaterial.color = Color.green;
                highlightMaterial.EnableKeyword("_EMISSION");
                highlightMaterial.SetColor("_EmissionColor", Color.green * 0.5f);
                Debug.Log($"Material de highlight creado automáticamente para {name}");
            }
        }
        
        // Verificar que tiene collider para detección (pero no para colisión)
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }
        // Hacer el collider trigger para evitar colisiones físicas
        col.isTrigger = true;
        
        Debug.Log($" TrashObject '{name}' configurado con sincronización de red");
    }
    
    /// <summary>
    /// Ilumina el objeto cuando está cerca del jugador
    /// Solo funciona si no está en modo forzado
    /// </summary>
    public void HighlightObject()
    {
        // No hacer nada si está en modo forzado
        if (forcedHighlight)
        {
            return;
        }
        
        if (!isHighlighted && highlightMaterial != null && objectRenderer != null)
        {
            objectRenderer.material = highlightMaterial;
            isHighlighted = true;
        }
    }
    
    /// <summary>
    /// Quita la iluminación del objeto
    /// Solo funciona si no está en modo forzado
    /// </summary>
    public void RemoveHighlight()
    {
        // No hacer nada si está en modo forzado
        if (forcedHighlight)
        {
            return;
        }
        
        if (isHighlighted && originalMaterial != null && objectRenderer != null)
        {
            objectRenderer.material = originalMaterial;
            isHighlighted = false;
        }
    }
    
    /// <summary>
    /// Fuerza el estado de highlight (override de proximidad)
    /// Usado por el sistema de gesto thumbs up
    /// SINCRONIZADO EN RED
    /// </summary>
    public void ForceHighlight(bool state)
    {
        // Si estamos en multijugador, sincronizar vía RPC
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            photonView.RPC("RPC_ForceHighlight", RpcTarget.AllBuffered, state);
        }
        else
        {
            // Modo single player: ejecutar directamente
            RPC_ForceHighlight(state);
        }
    }
    
    /// <summary>
    /// RPC: Fuerza el estado de highlight en TODOS los clientes
    /// </summary>
    [PunRPC]
    void RPC_ForceHighlight(bool state)
    {
        forcedHighlight = state;
        
        if (objectRenderer != null)
        {
            if (state)
            {
                // Activar highlight forzado
                if (highlightMaterial != null)
                {
                    objectRenderer.material = highlightMaterial;
                    isHighlighted = true;
                }
            }
            else
            {
                // Desactivar highlight forzado, volver al estado normal
                if (originalMaterial != null)
                {
                    objectRenderer.material = originalMaterial;
                    isHighlighted = false;
                }
            }
        }
    }
    
    /// <summary>
    /// Método llamado cuando el objeto es agarrado
    /// SINCRONIZADO EN RED - Todos los clientes ejecutarán la destrucción
    /// </summary>
    public void GrabObject()
    {
        // Evitar doble agarre
        if (isBeingGrabbed)
        {
            return;
        }
        
        // Si estamos en multijugador, sincronizar vía RPC
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            // Llamar RPC en TODOS los clientes (AllBuffered)
            photonView.RPC("RPC_GrabObject", RpcTarget.AllBuffered);
        }
        else
        {
            // Modo single player: ejecutar directamente
            RPC_GrabObject();
        }
    }
    
    /// <summary>
    /// RPC: Ejecuta la recolección de basura en TODOS los clientes
    /// </summary>
    [PunRPC]
    void RPC_GrabObject()
    {
        // Evitar doble agarre
        if (isBeingGrabbed)
        {
            return;
        }
        
        isBeingGrabbed = true;
        
        // Usar el contador simple
        SimpleTrashCounter.AddTrash(points);
        
        // REPRODUCIR SONIDO DE RECOGER BASURA
        if (OceanAudioSystem.Instance != null)
        {
            OceanAudioSystem.Instance.PlayPickupSound();
        }
        
        // Efecto visual de recolección
        Debug.Log($" ¡Basura '{name}' recolectada! +{points} puntos");
        
        // Cambiar color antes de destruir
        if (objectRenderer != null && highlightMaterial != null)
        {
            objectRenderer.material = highlightMaterial;
        }
        
        // Destruir después de un breve delay
        Invoke("DestroyObject", 0.3f);
    }
    
    void DestroyObject()
    {
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Animación simple de desaparición
    /// </summary>
    System.Collections.IEnumerator DestroyWithAnimation()
    {
        Vector3 originalScale = transform.localScale;
        float timer = 0f;
        float duration = 0.3f;
        
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);
            yield return null;
        }
        
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Obtiene la distancia al jugador
    /// </summary>
    /// <param name="playerPosition">Posición del jugador</param>
    /// <returns>Distancia en unidades</returns>
    public float GetDistanceToPlayer(Vector3 playerPosition)
    {
        return Vector3.Distance(transform.position, playerPosition);
    }
    
    /// <summary>
    /// Verifica si el objeto está dentro del rango de agarre
    /// </summary>
    /// <param name="playerPosition">Posición del jugador</param>
    /// <returns>True si se puede agarrar</returns>
    public bool CanBeGrabbed(Vector3 playerPosition)
    {
        return GetDistanceToPlayer(playerPosition) <= grabDistance;
    }
}