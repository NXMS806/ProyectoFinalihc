import cv2
import mediapipe as mp
import socket
import json

UNITY_IP = "127.0.0.1"
UNITY_PORT = 5052

mp_hands = mp.solutions.hands
hands = mp_hands.Hands(static_image_mode=False, max_num_hands=1,
                       min_detection_confidence=0.7, min_tracking_confidence=0.7)
mp_draw = mp.solutions.drawing_utils

cap = cv2.VideoCapture(0)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
width = cap.get(cv2.CAP_PROP_FRAME_WIDTH)
height = cap.get(cv2.CAP_PROP_FRAME_HEIGHT)
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
unity_address = (UNITY_IP, UNITY_PORT)

while cap.isOpened():
    success, image = cap.read()
    if not success: continue

    image = cv2.flip(image, 1)
    rgb_image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    results = hands.process(rgb_image)

    data_to_send = {"x": 0.0, "y": 0.0}

    if results.multi_hand_landmarks:
        hand_landmarks = results.multi_hand_landmarks[0]
        mp_draw.draw_landmarks(image, hand_landmarks, mp_hands.HAND_CONNECTIONS)

        index_tip = hand_landmarks.landmark[8]
        data_to_send["x"] = index_tip.x
        data_to_send["y"] = index_tip.y

        sock.sendto(json.dumps(data_to_send).encode(), unity_address)

    cv2.imshow('Hand Tracking', image)
    if cv2.waitKey(5) & 0xFF == 27: break

cap.release()
cv2.destroyAllWindows()