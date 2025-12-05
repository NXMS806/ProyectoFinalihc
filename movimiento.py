import cv2
import mediapipe as mp
import sys
import socket
import json
import math
import time
from collections import defaultdict
from config import *

print(f"Iniciando sistema de gestos...")
print(f"Conectando a Unity en {UNITY_IP}:{UNITY_PORT}")
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

mp_pose = mp.solutions.pose
mp_hands = mp.solutions.hands
mp_drawing = mp.solutions.drawing_utils

pose = mp_pose.Pose(
    min_detection_confidence=MIN_DETECTION_CONFIDENCE, 
    min_tracking_confidence=MIN_TRACKING_CONFIDENCE
)
hands = mp_hands.Hands(
    static_image_mode=False,
    max_num_hands=2,
    min_detection_confidence=MIN_DETECTION_CONFIDENCE,
    min_tracking_confidence=MIN_TRACKING_CONFIDENCE
)

def angle_between(a, b, c):
    """Devuelve el ángulo en grados en el punto b (a-b-c)."""
    ax, ay = a.x - b.x, a.y - b.y
    cx, cy = c.x - b.x, c.y - b.y
    dot = ax * cx + ay * cy
    mag_a = math.hypot(ax, ay)
    mag_c = math.hypot(cx, cy)
    if mag_a * mag_c == 0:
        return 0.0
    cosang = max(-1.0, min(1.0, dot / (mag_a * mag_c)))
    return math.degrees(math.acos(cosang))

counters = defaultdict(int)

def update_action_state(name, cond):
    """
    name: string action
    cond: boolean si la condición en este frame se cumple
    devuelve: boolean (activo)
    """
    if name == "Grab":
        required_frames = GRAB_DEBOUNCE_FRAMES
    elif name == "ThumbsUp":
        required_frames = HIGHLIGHT_DEBOUNCE_FRAMES
    else:
        required_frames = DEBOUNCE_FRAMES
    
    if cond:
        counters[name] = min(counters[name] + 1, required_frames)
    else:
        counters[name] = max(counters[name] - 1, 0)
    return counters[name] >= required_frames

print(f"Configurando camara {CAMERA_INDEX}...")
cap = cv2.VideoCapture(CAMERA_INDEX)
if not cap.isOpened():
    print("Error: No se pudo abrir la camara.")
    print("Sugerencias:")
    print("  - Verificar permisos de camara")
    print("  - Probar con otro indice (1, 2, etc.)")
    print("  - Cerrar otras aplicaciones que usen la camara")
    sys.exit(1)

cap.set(cv2.CAP_PROP_FRAME_WIDTH, CAMERA_WIDTH)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, CAMERA_HEIGHT)
cap.set(cv2.CAP_PROP_FPS, CAMERA_FPS)

print("Camara configurada correctamente")
print(f"Resolucion: {CAMERA_WIDTH}x{CAMERA_HEIGHT} @ {CAMERA_FPS}fps")
print("Gestos disponibles:")
print("  - Mano Derecha: Rotar Derecha")
print("  - Mano Izquierda: Rotar Izquierda") 
print("  - Ambas Manos: Mover Adelante")
print("  - Manos Juntas: Mover Atras")
print("  - Puno Cerrado: Agarrar")
print("  - Pulgar Arriba: Iluminar Basura")
print("Sistema iniciado. Presiona 'q' para salir.")

frame_count = 0
last_sent_data = None

while cap.isOpened():
    ret, frame = cap.read()
    if not ret:
        break
    
    frame_count += 1
    if REDUCE_CPU_USAGE and frame_count % FRAME_SKIP != 0:
        continue

    image = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    image.flags.writeable = False

    res_pose = pose.process(image)
    res_hands = hands.process(image)

    image.flags.writeable = True
    frame_h, frame_w = frame.shape[:2]

    cand = {
        "RightHandUp": False,
        "LeftHandUp": False,
        "PalmsTogetherPraying": False,
        "BothHandsUp": False,
        "Grab": False,
        "ThumbsUp": False
    }

    if res_pose.pose_landmarks:
        lm = res_pose.pose_landmarks.landmark

        nose = lm[mp_pose.PoseLandmark.NOSE]
        left_sh = lm[mp_pose.PoseLandmark.LEFT_SHOULDER]
        right_sh = lm[mp_pose.PoseLandmark.RIGHT_SHOULDER]
        left_el = lm[mp_pose.PoseLandmark.LEFT_ELBOW]
        right_el = lm[mp_pose.PoseLandmark.RIGHT_ELBOW]
        left_wr = lm[mp_pose.PoseLandmark.LEFT_WRIST]
        right_wr = lm[mp_pose.PoseLandmark.RIGHT_WRIST]

        mid_sh_x = (left_sh.x + right_sh.x) / 2.0
        mid_sh_y = (left_sh.y + right_sh.y) / 2.0

        if right_wr.y < right_sh.y - HAND_UP_THRESHOLD:
            cand["RightHandUp"] = True

        if left_wr.y < left_sh.y - HAND_UP_THRESHOLD:
            cand["LeftHandUp"] = True

        if (right_wr.y < right_sh.y - HAND_UP_THRESHOLD) and (left_wr.y < left_sh.y - HAND_UP_THRESHOLD):
            cand["BothHandsUp"] = True

        hands_distance = math.hypot(left_wr.x - right_wr.x, left_wr.y - right_wr.y)
        
        if hands_distance < HANDS_TOGETHER_THRESHOLD:
            cand["PalmsTogetherPraying"] = True

        if SHOW_LANDMARKS:
            mp_drawing.draw_landmarks(
                frame, res_pose.pose_landmarks, mp_pose.POSE_CONNECTIONS,
                mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=2),
                mp_drawing.DrawingSpec(color=(0, 255, 255), thickness=2)
            )

    if res_hands.multi_hand_landmarks:
        for idx, hand_landmarks in enumerate(res_hands.multi_hand_landmarks):
            handedness = res_hands.multi_handedness[idx].classification[0].label
            
            thumb_tip = hand_landmarks.landmark[mp_hands.HandLandmark.THUMB_TIP]
            thumb_mcp = hand_landmarks.landmark[mp_hands.HandLandmark.THUMB_MCP]
            thumb_ip = hand_landmarks.landmark[mp_hands.HandLandmark.THUMB_IP]
            index_tip = hand_landmarks.landmark[mp_hands.HandLandmark.INDEX_FINGER_TIP]
            index_mcp = hand_landmarks.landmark[mp_hands.HandLandmark.INDEX_FINGER_MCP]
            middle_tip = hand_landmarks.landmark[mp_hands.HandLandmark.MIDDLE_FINGER_TIP]
            middle_mcp = hand_landmarks.landmark[mp_hands.HandLandmark.MIDDLE_FINGER_MCP]
            ring_tip = hand_landmarks.landmark[mp_hands.HandLandmark.RING_FINGER_TIP]
            ring_mcp = hand_landmarks.landmark[mp_hands.HandLandmark.RING_FINGER_MCP]
            pinky_tip = hand_landmarks.landmark[mp_hands.HandLandmark.PINKY_TIP]
            pinky_mcp = hand_landmarks.landmark[mp_hands.HandLandmark.PINKY_MCP]
            wrist = hand_landmarks.landmark[mp_hands.HandLandmark.WRIST]
            
            thumb_extended_up = thumb_tip.y < thumb_mcp.y - 0.05
            
            index_folded = index_tip.y > index_mcp.y
            middle_folded = middle_tip.y > middle_mcp.y
            ring_folded = ring_tip.y > ring_mcp.y
            pinky_folded = pinky_tip.y > pinky_mcp.y
            
            if thumb_extended_up and index_folded and middle_folded and ring_folded and pinky_folded:
                cand["ThumbsUp"] = True
            
            fingers_closed = 0
            

            if index_tip.y > index_mcp.y:
                fingers_closed += 1
            
            if middle_tip.y > middle_mcp.y:
                fingers_closed += 1
                
            if ring_tip.y > ring_mcp.y:
                fingers_closed += 1
                
            if pinky_tip.y > pinky_mcp.y:
                fingers_closed += 1
            
            if abs(thumb_tip.x - thumb_mcp.x) < 0.05:
                fingers_closed += 1
            
            if fingers_closed >= GRAB_FINGERS_THRESHOLD and not cand["ThumbsUp"]:
                cand["Grab"] = True

            if SHOW_LANDMARKS:
                mp_drawing.draw_landmarks(
                    frame, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                    mp_drawing.DrawingSpec(color=(255, 0, 0), thickness=2, circle_radius=2),
                    mp_drawing.DrawingSpec(color=(255, 255, 0), thickness=2)
                )

    actions_active = {}
    for name in cand:
        actions_active[name] = update_action_state(name, cand[name])

    if actions_active != last_sent_data:
        payload = json.dumps(actions_active).encode("utf-8")
        try:
            sock.sendto(payload, (UNITY_IP, UNITY_PORT))
            last_sent_data = actions_active.copy()
        except Exception as e:
            if SHOW_DEBUG_INFO:
                print(f"Error enviando UDP: {e}")

    if SHOW_DEBUG_INFO:
        overlay = frame.copy()
        cv2.rectangle(overlay, (5, 5), (450, 180), (0, 0, 0), -1)
        cv2.addWeighted(overlay, 0.7, frame, 0.3, 0, frame)
        
        cv2.putText(frame, "CLEAN OCEAN VR - GESTOS", (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 255), 2)
        
        y = 50
        gesture_names = {
            "RightHandUp": "Mano Derecha -> Rotar Derecha",
            "LeftHandUp": "Mano Izquierda -> Rotar Izquierda", 
            "BothHandsUp": "Ambas Manos -> Adelante",
            "PalmsTogetherPraying": "Manos Juntas -> Atras",
            "Grab": "Puno Cerrado -> Agarrar",
            "ThumbsUp": "Pulgar Arriba -> Iluminar Basura"
        }
        
        for k, v in actions_active.items():
            color = (0, 255, 0) if v else (80, 80, 80)
            status = "ACTIVO" if v else "inactivo"
            display_name = gesture_names.get(k, k)
            cv2.putText(frame, f"{display_name}: {status}", (10, y), cv2.FONT_HERSHEY_SIMPLEX, 0.45, color, 1)
            y += 25

    cv2.imshow(DEBUG_WINDOW_NAME, frame)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
sock.close()