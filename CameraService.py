###########  INSTALAR #########################
# opencv-python
# aiortc
###############################################


import asyncio
import cv2
from aiortc import RTCPeerConnection, RTCSessionDescription, VideoStreamTrack
from aiortc.contrib.signaling import TcpSocketSignaling
from av import VideoFrame
import fractions


class CustomVideoStreamTrack(VideoStreamTrack):
    def __init__(self, camera_id):
        super().__init__()
        print ("Preparando la cámara ...")
        self.cap = cv2.VideoCapture(camera_id)
        self.frame_count = 0

    async def recv(self):
        self.frame_count += 1
        print(f"Sending frame {self.frame_count}")
        ret, frame = self.cap.read()
        if not ret:
            print("Failed to read frame from camera")
            return None
        frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        video_frame = VideoFrame.from_ndarray(frame, format="rgb24")
        video_frame.pts = self.frame_count
        video_frame.time_base = fractions.Fraction(1, 30)  # Use fractions for time_base


        video_frame = VideoFrame.from_ndarray(frame, format="rgb24")
        video_frame.pts = self.frame_count
        video_frame.time_base = fractions.Fraction(1, 30)  # Use fractions for time_base
        return video_frame

async def setup_webrtc_and_run(ip_address, port, camera_id):
    # aquí le digo el puerto donde se abre el websocket para ponerse de acuerdo con el cliente
    signaling = TcpSocketSignaling(ip_address, port)
    # Creamos la estructura que se necesita para establecer el canal de comunicación via WebRTC
    pc = RTCPeerConnection()
    video_sender = CustomVideoStreamTrack(camera_id)
    pc.addTrack(video_sender) # aqui le decimos de dónde sale el stream de video

    try:
        print ("Esperando clientes")
        await signaling.connect()

        print ("Preparo la oferta. En cuanto se conecte el cliente se la envío")
        offer = await pc.createOffer()
        await pc.setLocalDescription(offer)
        await signaling.send(pc.localDescription)
        print ("Ciente conectado. Envio oferta y espero respuesta")
        while True:
            obj = await signaling.receive()
            if isinstance(obj, RTCSessionDescription):
                await pc.setRemoteDescription(obj)
                print("Respuesta recibida. Trasmisión en marcha")
            elif obj is None:
                print("Fallo en la coordinación")
                break
        print("Cierro conexión")
    finally:
        await pc.close()

async def main():
    # En este caso es el emisor el que actua de servidor, exponiendo el canal de comunicación
    # en el puerto 9999
    ip_address = "0.0.0.0"
    port = 9999
    camera_id = 0  # Hay que cambiar eso en el caso de capturar el video que viene del dron
    await setup_webrtc_and_run(ip_address, port, camera_id)

if __name__ == "__main__":
    asyncio.run(main())