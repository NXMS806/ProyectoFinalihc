using UnityEngine;
using Photon.Pun;

/// <summary>
/// Script de debugging para mostrar informacion de Player2 (temporal).
/// </summary>
public class Player2DebugInfo : MonoBehaviourPunCallbacks
{
    private Vector3 lastPosition;
    private float updateInterval = 0.5f;
    private float nextUpdate = 0f;
    
    void Start()
    {
        if (!photonView.IsMine) return;
        
        lastPosition = transform.position;
        Debug.Log($"� Player2DebugInfo iniciado en: {transform.position}");
    }
    
    void Update()
    {
        if (!photonView.IsMine) return;
        
        // Mostrar información periódicamente
        if (Time.time >= nextUpdate)
        {
            nextUpdate = Time.time + updateInterval;
            
            Vector3 currentPos = transform.position;
            Vector3 movement = currentPos - lastPosition;
            
            if (movement.magnitude > 0.01f)
            {
                Debug.Log($" Player2 Posición: {currentPos}");
                Debug.Log($"   Δ Movimiento: X={movement.x:F3}, Y={movement.y:F3}, Z={movement.z:F3}");
                Debug.Log($"   Magnitud: {movement.magnitude:F3}");
            }
            
            lastPosition = currentPos;
        }
        
        // Detectar input en tiempo real
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        
        if (h != 0 || v != 0)
        {
            Debug.Log($"⌨ Input detectado: H={h:F2}, V={v:F2}");
        }
    }
    
    void OnGUI()
    {
        if (!photonView.IsMine) return;
        
        // Mostrar información en pantalla
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.yellow;
        
        string info = $"Player2 Debug Info:\n";
        info += $"Position: {transform.position}\n";
        info += $"Input H: {Input.GetAxis("Horizontal"):F2}\n";
        info += $"Input V: {Input.GetAxis("Vertical"):F2}\n";
        
        Movement movement = GetComponent<Movement>();
        if (movement != null)
        {
            info += $"isPlayer2: {movement.isPlayer2}\n";
            info += $"useGestureControl: {movement.useGestureControl}\n";
        }
        
        GUI.Label(new Rect(10, 200, 400, 200), info, style);
    }
}
