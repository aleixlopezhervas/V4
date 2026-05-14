import asyncio
import threading
import cv2
import numpy as np
import torch
from http.server import BaseHTTPRequestHandler, HTTPServer
from aiortc import RTCPeerConnection, RTCSessionDescription, MediaStreamTrack
from aiortc.contrib.signaling import TcpSocketSignaling
import paho.mqtt.client as mqtt
import json

latest_frame = None
frame_lock = threading.Lock()
current_object_ids = []  # lista de objetos a detectar
detection_lock = threading.Lock()

# ── YOLO ────────────────────────────────────────────────────
model = None

def load_model():
    global model
    print("Cargando modelo YOLO...")
    model = torch.hub.load('ultralytics/yolov5', 'yolov5s', pretrained=True)
    model.eval()
    print("Modelo cargado.")

def detect_objects(frame):
    global model, current_object_ids
    if model is None or not current_object_ids:
        return frame

    img_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = model(img_rgb)

    with detection_lock:
        ids = list(current_object_ids)

    for *box, conf, cls in results.xyxy[0]:
        if int(cls.item()) in ids:
            x1, y1, x2, y2 = map(int, box)
            label = model.names[int(cls.item())]
            cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 2)
            cv2.putText(frame, f"{label} {conf:.2f}", (x1, y1 - 10),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)
    return frame

# ── MQTT ────────────────────────────────────────────────────
usuario = "elies22"

def on_mqtt_message(cli, userdata, message):
    global current_object_ids
    command = message.topic.split("/")[-1]

    if command == "startDetection":
        data = json.loads(message.payload.decode("utf-8"))
        with detection_lock:
            current_object_ids = data["objectIds"]
        print(f"Detectando objetos: {current_object_ids}")

    if command == "stopDetection":
        with detection_lock:
            current_object_ids = []
        print("Detección detenida.")

def start_mqtt():
    client = mqtt.Client(f"VideoReceiver_{usuario}", transport="websockets")
    client.username_pw_set("dronsEETAC", "mimara1456.")
    client.on_message = on_mqtt_message
    client.connect("dronseetac.upc.edu", 8000)
    client.subscribe(f"{usuario}/autopilotServiceDemo/#")
    client.loop_forever()

# ── MJPEG ───────────────────────────────────────────────────
class MJPEGHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args): pass

    def do_GET(self):
        self.send_response(200)
        self.send_header('Content-Type', 'multipart/x-mixed-replace; boundary=frame')
        self.end_headers()
        import time
        while True:
            with frame_lock:
                frame = latest_frame.copy() if latest_frame is not None else None
            if frame is not None:
                frame = detect_objects(frame)  # ✅ detección sobre el frame
                _, jpeg = cv2.imencode('.jpg', frame, [cv2.IMWRITE_JPEG_QUALITY, 50])
                data = jpeg.tobytes()
                try:
                    self.wfile.write(b'--frame\r\nContent-Type: image/jpeg\r\n\r\n')
                    self.wfile.write(data)
                    self.wfile.write(b'\r\n')
                except:
                    break
            else:
                time.sleep(0.03)

def start_mjpeg_server():
    server = HTTPServer(('localhost', 8888), MJPEGHandler)
    server.serve_forever()

# ── WebRTC ──────────────────────────────────────────────────
class VideoReceiver:
    async def handle_track(self, track):
        global latest_frame
        while True:
            try:
                frame = await asyncio.wait_for(track.recv(), timeout=5.0)
                img = frame.to_ndarray(format="bgr24")
                img = cv2.flip(img, 1)
                img = cv2.resize(img, (640, 360))
                with frame_lock:
                    latest_frame = img
            except asyncio.TimeoutError:
                print("Timeout esperando frame")
            except Exception as ex:
                print(f"Error: {ex}"); break

async def run():
    IP_server = "localhost"
    signaling = TcpSocketSignaling(IP_server, 9999)
    pc = RTCPeerConnection()
    receiver = VideoReceiver()

    @pc.on("track")
    def on_track(track):
        if isinstance(track, MediaStreamTrack):
            asyncio.ensure_future(receiver.handle_track(track))

    await signaling.connect()
    offer = await signaling.receive()
    await pc.setRemoteDescription(offer)
    answer = await pc.createAnswer()
    await pc.setLocalDescription(answer)
    await signaling.send(pc.localDescription)
    print("Stream WebRTC recibido. MJPEG en http://localhost:8888")
    await asyncio.sleep(3600)

if __name__ == "__main__":
    threading.Thread(target=load_model, daemon=True).start()
    threading.Thread(target=start_mjpeg_server, daemon=True).start()
    threading.Thread(target=start_mqtt, daemon=True).start()
    asyncio.run(run())