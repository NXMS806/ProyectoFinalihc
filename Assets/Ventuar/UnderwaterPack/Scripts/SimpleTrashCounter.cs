using System;
using UnityEngine;

/// <summary>
/// Contador de basura recolectada con eventos.
/// </summary>
public class SimpleTrashCounter : MonoBehaviour
{
    [Header("Configuración Simple")]
    public int totalTrash = 0;
    public int targetTrash = 10;
    
    // NUEVO: Evento que se dispara cuando se recolecta basura
    public static event Action<int> OnTrashCollected;
    
    private static SimpleTrashCounter instance;
    
    void Awake()
    {
        // Singleton pattern para evitar duplicados
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        Debug.Log(" SimpleTrashCounter iniciado - Sistema de basura activo");
        ShowStatus();
    }
    
    public static void AddTrash(int points = 1)
    {
        if (instance != null)
        {
            instance.totalTrash += points;
            instance.ShowStatus();
            
            // NUEVO: Invocar evento para notificar a LevelManager
            OnTrashCollected?.Invoke(instance.totalTrash);
            
            if (instance.totalTrash >= instance.targetTrash)
            {
                Debug.Log("� ¡Meta alcanzada! ¡Océano limpio!");
            }
        }
        else
        {
            Debug.Log($" Basura recolectada: +{points} (sin contador)");
        }
    }
    
    void ShowStatus()
    {
        Debug.Log($" Basura Total: {totalTrash}/{targetTrash}");
    }
    
    // OnGUI desactivado - Ahora usamos OceanVRHUD.cs para mostrar el HUD
    /*
    void OnGUI()
    {
        // UI simple usando OnGUI (más confiable)
        GUI.color = Color.white;
        GUI.skin.label.fontSize = 24;
        GUI.Label(new Rect(10, 10, 300, 50), $"Basura: {totalTrash}/{targetTrash}");
    }
    */
    
    /// <summary>
    /// Metodos publicos para el HUD.
    /// </summary>
    public static int GetTrashCount()
    {
        return instance != null ? instance.totalTrash : 0;
    }
    
    public static int GetTotalPoints()
    {
        // Cada objeto vale 30 puntos
        return instance != null ? instance.totalTrash * 30 : 0;
    }
    
    /// <summary>
    /// Resetea el contador de basura (para reiniciar niveles).
    /// </summary>
    public static void ResetCount()
    {
        if (instance != null)
        {
            instance.totalTrash = 0;
            Debug.Log(" Contador de basura reseteado a 0");
        }
    }
}
