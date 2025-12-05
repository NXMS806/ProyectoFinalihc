using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

/// <summary>
/// Efecto visual de suciedad en pantalla del Limpiador, controlado por el Contaminador.
/// </summary>
public class DirtyScreenEffect : MonoBehaviourPunCallbacks
{
    #region Singleton
    public static DirtyScreenEffect Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region Configuration
    [Header(" Configuración del Efecto")]
    [Tooltip("Duración del efecto de suciedad en segundos")]
    public float effectDuration = 4f;
    
    [Tooltip("Tiempo de fade in (aparición gradual)")]
    public float fadeInTime = 0.3f;
    
    [Tooltip("Tiempo de fade out (desaparición gradual)")]
    public float fadeOutTime = 1f;
    
    [Tooltip("Opacidad máxima del efecto (0-1)")]
    [Range(0f, 1f)]
    public float maxOpacity = 0.8f;
    
    [Header(" Cooldown")]
    [Tooltip("Tiempo de espera entre usos del poder")]
    public float cooldownTime = 10f;
    
    [Header(" Referencias UI")]
    [Tooltip("Image del overlay de suciedad (debe estar en el Canvas del Limpiador)")]
    public Image dirtyOverlayImage;
    
    [Tooltip("(Opcional) Imagen alternativa 2 para variedad")]
    public Image dirtyOverlayImage2;
    
    [Header(" Audio")]
    [Tooltip("Sonido cuando se activa el efecto (splash/suciedad)")]
    public AudioClip dirtySplashSound;
    
    [Tooltip("Sonido cuando el efecto termina")]
    public AudioClip cleanSound;
    
    private AudioSource audioSource;
    
    [Header(" Debug")]
    public bool showDebugLogs = true;
    #endregion

    #region State
    private bool isEffectActive = false;
    private float lastUseTime = -999f;
    private Coroutine activeEffectCoroutine;
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
        
        // Asegurar que el overlay empiece invisible
        if (dirtyOverlayImage != null)
        {
            SetImageAlpha(dirtyOverlayImage, 0f);
            dirtyOverlayImage.gameObject.SetActive(false);
        }
        
        if (dirtyOverlayImage2 != null)
        {
            SetImageAlpha(dirtyOverlayImage2, 0f);
            dirtyOverlayImage2.gameObject.SetActive(false);
        }
        
        if (showDebugLogs)
            Debug.Log("� DirtyScreenEffect inicializado");
    }
    #endregion

    #region Public Methods (Called by TrashSpawner)
    /// <summary>
    /// Intenta activar el efecto de suciedad en el Limpiador.
    /// Llamado por el Contaminador cuando hace click derecho.
    /// </summary>
    /// <returns>True si se activó, False si está en cooldown</returns>
    public bool TryActivateDirtyEffect()
    {
        // Verificar cooldown
        if (IsOnCooldown())
        {
            if (showDebugLogs)
                Debug.Log($"� Poder en cooldown: {GetRemainingCooldown():F1}s restantes");
            return false;
        }
        
        // Registrar uso
        lastUseTime = Time.time;
        
        if (showDebugLogs)
            Debug.Log("� ¡Activando poder de suciedad!");
        
        // Enviar RPC al Limpiador (Player1)
        // El Limpiador es el MasterClient (primer jugador en conectar)
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_ApplyDirtyEffect", RpcTarget.Others);
        }
        else
        {
            // Modo offline: aplicar localmente (para testing)
            RPC_ApplyDirtyEffect();
        }
        
        return true;
    }
    
    /// <summary>
    /// Verifica si el poder está en cooldown
    /// </summary>
    public bool IsOnCooldown()
    {
        return (Time.time - lastUseTime) < cooldownTime;
    }
    
    /// <summary>
    /// Obtiene el tiempo restante del cooldown
    /// </summary>
    public float GetRemainingCooldown()
    {
        float elapsed = Time.time - lastUseTime;
        return Mathf.Max(0f, cooldownTime - elapsed);
    }
    
    /// <summary>
    /// Obtiene el progreso del cooldown (0 = recién usado, 1 = listo)
    /// </summary>
    public float GetCooldownProgress()
    {
        if (!IsOnCooldown()) return 1f;
        float elapsed = Time.time - lastUseTime;
        return Mathf.Clamp01(elapsed / cooldownTime);
    }
    #endregion

    #region RPC Methods
    /// <summary>
    /// RPC: Aplica el efecto de suciedad en la pantalla del receptor.
    /// Solo el Limpiador (Player1) debería mostrar el efecto.
    /// </summary>
    [PunRPC]
    void RPC_ApplyDirtyEffect()
    {
        // Solo aplicar si SOY el Limpiador (Player1)
        // El Limpiador es el MasterClient o el primer jugador
        // Verificamos si tenemos el overlay configurado (solo el Limpiador debería tenerlo)
        if (dirtyOverlayImage == null)
        {
            if (showDebugLogs)
                Debug.Log("� No soy el Limpiador o no tengo overlay configurado - ignorando");
            return;
        }
        
        if (showDebugLogs)
            Debug.Log("� ¡Recibido efecto de suciedad! Aplicando...");
        
        // Si hay un efecto activo, cancelarlo
        if (activeEffectCoroutine != null)
        {
            StopCoroutine(activeEffectCoroutine);
        }
        
        // Iniciar nuevo efecto
        activeEffectCoroutine = StartCoroutine(DirtyEffectCoroutine());
    }
    #endregion

    #region Effect Coroutine
    /// <summary>
    /// Coroutine que maneja el efecto completo: fade in → mantener → fade out
    /// </summary>
    IEnumerator DirtyEffectCoroutine()
    {
        isEffectActive = true;
        
        // Seleccionar overlay (aleatorio si hay 2)
        Image activeOverlay = dirtyOverlayImage;
        if (dirtyOverlayImage2 != null && Random.value > 0.5f)
        {
            activeOverlay = dirtyOverlayImage2;
        }
        
        // Activar y preparar
        activeOverlay.gameObject.SetActive(true);
        SetImageAlpha(activeOverlay, 0f);
        
        // Reproducir sonido de splash
        PlaySound(dirtySplashSound);
        
        // FASE 1: FADE IN 
        float elapsed = 0f;
        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, maxOpacity, elapsed / fadeInTime);
            SetImageAlpha(activeOverlay, alpha);
            yield return null;
        }
        SetImageAlpha(activeOverlay, maxOpacity);
        
        if (showDebugLogs)
            Debug.Log("� Efecto de suciedad activo");
        
        // FASE 2: MANTENER 
        yield return new WaitForSeconds(effectDuration);
        
        // FASE 3: FADE OUT
        elapsed = 0f;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(maxOpacity, 0f, elapsed / fadeOutTime);
            SetImageAlpha(activeOverlay, alpha);
            yield return null;
        }
        SetImageAlpha(activeOverlay, 0f);
        
        // Desactivar overlay
        activeOverlay.gameObject.SetActive(false);
        
        // Reproducir sonido de limpieza
        PlaySound(cleanSound);
        
        isEffectActive = false;
        activeEffectCoroutine = null;
        
        if (showDebugLogs)
            Debug.Log("� Efecto de suciedad terminado");
    }
    #endregion

    #region Utility Methods
    void SetImageAlpha(Image image, float alpha)
    {
        if (image != null)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
    
    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    #endregion

    #region Public Getters (For UI)
    /// <summary>
    /// Verifica si el efecto está actualmente activo
    /// </summary>
    public bool IsEffectActive()
    {
        return isEffectActive;
    }
    #endregion
}
