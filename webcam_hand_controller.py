"""Send webcam hand gestures to the Unity incident commander on localhost:5005."""
import argparse
import json
import math
import socket
import time


def distance(a, b):
    return math.hypot(a.x - b.x, a.y - b.y)


def classify(landmarks):
    wrist = landmarks[0]
    thumb = landmarks[4]
    index = landmarks[8]
    if distance(thumb, index) < 0.045:
        return "pinch"

    fingers = ((8, 6), (12, 10), (16, 14), (20, 18))
    extended = [
        distance(landmarks[tip], wrist) > distance(landmarks[pip], wrist) * 1.15
        for tip, pip in fingers
    ]
    if all(extended):
        return "open_palm"
    if extended[0] and not any(extended[1:]):
        return "point"
    return "idle"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--camera", type=int, default=0, help="OpenCV camera index")
    parser.add_argument("--port", type=int, default=5005, help="Unity UDP port")
    parser.add_argument("--preview", action="store_true", help="Show webcam preview")
    args = parser.parse_args()

    try:
        import cv2
        import mediapipe as mp
    except ImportError as error:
        raise SystemExit(
            "Install webcam dependencies first: pip install -r requirements-handtracking.txt"
        ) from error

    if not hasattr(mp, "solutions"):
        raise SystemExit("This script needs the MediaPipe solutions.hands API.")
    camera = cv2.VideoCapture(args.camera)
    if not camera.isOpened():
        raise SystemExit(f"Cannot open camera {args.camera}")
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    endpoint = ("127.0.0.1", args.port)
    next_send = 0.0
    try:
        with mp.solutions.hands.Hands(
            max_num_hands=1,
            model_complexity=0,
            min_detection_confidence=0.55,
            min_tracking_confidence=0.5,
        ) as hands:
            while True:
                ok, frame = camera.read()
                if not ok:
                    break
                frame = cv2.flip(frame, 1)
                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                result = hands.process(rgb)
                now = time.monotonic()
                if result.multi_hand_landmarks and now >= next_send:
                    landmarks = result.multi_hand_landmarks[0].landmark
                    tip = landmarks[8]
                    payload = {
                        "x": round(max(0.0, min(1.0, tip.x)), 4),
                        "y": round(max(0.0, min(1.0, tip.y)), 4),
                        "gesture": classify(landmarks),
                    }
                    sock.sendto(json.dumps(payload).encode("utf-8"), endpoint)
                    next_send = now + 1.0 / 30.0
                    if args.preview:
                        cv2.putText(
                            frame, payload["gesture"], (20, 40),
                            cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 255), 2,
                        )
                if args.preview:
                    cv2.imshow("ResQ hand control - press Q to quit", frame)
                    if cv2.waitKey(1) & 0xFF == ord("q"):
                        break
    except KeyboardInterrupt:
        pass
    finally:
        camera.release()
        sock.close()
        if args.preview:
            cv2.destroyAllWindows()


if __name__ == "__main__":
    main()

