using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable]
public class HandData {
    public float x;
    public float y;
}

public class HandReceiver : MonoBehaviour {
    private UdpClient udpClient;
    private IPEndPoint remoteEP;
    public GameObject cursorObject;
    public float dwellTime = 2.0f;  
    private HandData lastData = new HandData { x = 0f, y = 0f };
    private float dwellTimer = 0f;
    private Button currentHoveredButton = null;
    private Button lastHoveredButton = null;

    void Start() {
        try {
            udpClient = new UdpClient(5052);
            remoteEP = new IPEndPoint(IPAddress.Any, 0);
            StartCoroutine(ReceiveData());
        } catch (System.Exception e) {
            Debug.LogError($"Error al iniciar UDP: {e.Message}");
        }

        if (cursorObject == null) {
            Debug.LogError("cursorObject no está asignado en el Inspector. Por favor, asigna un GameObject para el cursor.");
        }

        if (EventSystem.current == null) {
            Debug.LogError("No se encontró un EventSystem en la escena. Añade un GameObject con el componente EventSystem.");
        }
    }

    IEnumerator ReceiveData() {
        while (true) {
            try {
                if (udpClient.Available > 0) {
                    byte[] data = udpClient.Receive(ref remoteEP);
                    string json = Encoding.UTF8.GetString(data);
                    lastData = JsonUtility.FromJson<HandData>(json);
                    Debug.Log($"Datos recibidos: x={lastData.x}, y={lastData.y}");
                }
            } catch (System.Exception e) {
                Debug.LogWarning($"Error al recibir datos UDP: {e.Message}");
            }
            yield return null;
        }
    }

    void Update() {
        if (lastData.x == 0 && lastData.y == 0) return;

        Vector2 screenPos = new Vector2(lastData.x * Screen.width, (1 - lastData.y) * Screen.height);
        if (cursorObject != null) {
            cursorObject.transform.position = screenPos;
            Debug.Log($"Coordenadas del cursor: {screenPos}");
        } else {
            Debug.LogWarning("cursorObject es null. No se puede mover el cursor.");
            return;
        }

        if (EventSystem.current == null) {
            Debug.LogWarning("EventSystem no está disponible. No se pueden procesar interacciones de UI.");
            return;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        currentHoveredButton = null;
        foreach (var result in results) {
            Button button = result.gameObject.GetComponent<Button>();
            if (button != null && button.interactable) {
                currentHoveredButton = button;
                Debug.Log($"Botón detectado: {result.gameObject.name}");
                break;
            }
        }

        if (currentHoveredButton != lastHoveredButton) {
            dwellTimer = 0f;
        }

        if (lastHoveredButton != null && lastHoveredButton != currentHoveredButton) {
            ExecuteEvents.Execute(lastHoveredButton.gameObject, eventData, ExecuteEvents.pointerExitHandler);
        }
        if (currentHoveredButton != null && currentHoveredButton != lastHoveredButton) {
            ExecuteEvents.Execute(currentHoveredButton.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
        }
        lastHoveredButton = currentHoveredButton;

        if (currentHoveredButton != null) {
            dwellTimer += Time.deltaTime;
            if (dwellTimer >= dwellTime) {
                currentHoveredButton.onClick.Invoke();
                Debug.Log($"Clic activado en botón: {currentHoveredButton.gameObject.name}");
                dwellTimer = 0f;
            }
        } else {
            dwellTimer = 0f;
        }
    }

    void OnDestroy() {
        if (udpClient != null) {
            udpClient.Close();
        }
    }
}