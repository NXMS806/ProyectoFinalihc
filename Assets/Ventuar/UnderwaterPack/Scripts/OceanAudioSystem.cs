using UnityEngine;

public class OceanAudioSystem : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioSource ambientSource;
    
    [Header("Audio Clips - Gameplay")]
    public AudioClip pickupTrash;
    public AudioClip highlightOn;
    public AudioClip highlightOff;
    
    [Header("Audio Clips - Niveles")]
    [Tooltip("Sonido cuando se completa un nivel exitosamente")]
    public AudioClip victorySound;
    
    [Tooltip("Sonido cuando se pierde un nivel (tiempo agotado)")]
    public AudioClip defeatSound;
    
    [Header("Audio Clips - Ambiente")]
    public AudioClip oceanAmbient;
    public AudioClip underwaterMusic;
    
    [Header("Audio Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float ambientVolume = 0.6f;
    
    public static OceanAudioSystem Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
        
        if (ambientSource == null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
        }
    }
    
    void Start()
    {
        PlayAmbient();
        if (underwaterMusic != null) PlayMusic();
    }
    
    void Update()
    {
        if (musicSource != null) musicSource.volume = musicVolume * masterVolume;
        if (sfxSource != null) sfxSource.volume = sfxVolume * masterVolume;
        if (ambientSource != null) ambientSource.volume = ambientVolume * masterVolume;
    }
    
    public void PlayPickupSound()
    {
        if (pickupTrash != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(pickupTrash);
            Debug.Log(" Sonido: Basura recogida");
        }
    }
    
    public void PlayHighlightOnSound()
    {
        if (highlightOn != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(highlightOn);
            Debug.Log(" Sonido: Highlight activado");
        }
    }
    
    public void PlayHighlightOffSound()
    {
        if (highlightOff != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(highlightOff);
            Debug.Log(" Sonido: Highlight desactivado");
        }
    }
    
    void PlayMusic()
    {
        if (underwaterMusic != null && musicSource != null && !musicSource.isPlaying)
        {
            musicSource.clip = underwaterMusic;
            musicSource.Play();
            Debug.Log("� Música iniciada");
        }
    }
    
    void PlayAmbient()
    {
        if (oceanAmbient != null && ambientSource != null && !ambientSource.isPlaying)
        {
            ambientSource.clip = oceanAmbient;
            ambientSource.Play();
            Debug.Log(" Ambiente oceánico iniciado");
        }
    }
    
    // ============================================
    // METODOS PARA SISTEMA DE NIVELES
    // ============================================
    
    /// <summary>
    /// Reproduce sonido de victoria al completar un nivel
    /// </summary>
    public void PlayVictorySound()
    {
        if (victorySound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(victorySound);
            Debug.Log("� Sonido: ¡Victoria!");
        }
        else
        {
            Debug.LogWarning(" No se puede reproducir sonido de victoria: clip no asignado");
        }
    }
    
    /// <summary>
    /// Reproduce sonido de derrota al perder un nivel
    /// </summary>
    public void PlayDefeatSound()
    {
        if (defeatSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(defeatSound);
            Debug.Log("� Sonido: Derrota");
        }
        else
        {
            Debug.LogWarning(" No se puede reproducir sonido de derrota: clip no asignado");
        }
    }
}
