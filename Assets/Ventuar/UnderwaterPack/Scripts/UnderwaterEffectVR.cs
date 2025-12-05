using UnityEngine;

public class UnderwaterEffectVR : MonoBehaviour
{
    [Header("Ajustes de agua")]
    public Color waterColor = new Color(0.0f, 0.4f, 0.7f, 1f); // Color azul agua
    [Range(0.001f, 0.1f)]
    public float density = 0.04f; // Densidad de la niebla

    private Color originalFogColor;
    private float originalFogDensity;
    private bool originalFog;

    void Start()
    {
        // Guardamos la configuración original del Fog
        originalFog = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;

        // Activamos efecto bajo el agua
        RenderSettings.fog = true;
        RenderSettings.fogColor = waterColor;
        RenderSettings.fogDensity = density;

        // Opcional: ajustar la luz ambiental
        RenderSettings.ambientLight = waterColor * 0.5f;
    }

    void OnDisable()
    {
        // Restauramos la configuración original
        RenderSettings.fog = originalFog;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;
    }
}
