############  INSTALAR ##############
# paho-mqtt, version 1.6.1
#####################################

import paho.mqtt.client as mqtt
import json
from dronLink.Dron import Dron

usuario = "elies22"
import threading

# esta función sirve para publicar los eventos resultantes de las acciones solicitadas
def publish_event (event):
    global sending_topic, client
    client.publish(sending_topic + '/'+event)


def publish_telemetry_info (telemetry_info):
    # cuando reciba datos de telemetría los publico
    global sending_topic, client
    print("Telemetría recibida:", telemetry_info)
    client.publish(sending_topic + '/telemetryInfo', json.dumps(telemetry_info))

def on_message(cli, userdata, message):
    global  sending_topic, client
    global dron
    # el mensaje que se recibe tiene este formato:
    #    "origen"/autopilotServiceDemo/"command"
    # tengo que averiguar el origen y el command
    splited = message.topic.split("/")
    origin = splited[0] # aqui tengo el nombre de la aplicación que origina la petición
    command = splited[2] # aqui tengo el comando

    sending_topic = "autopilotServiceDemo/" + origin # lo necesitaré para enviar las respuestas

    if command == 'connect':
        #en TERMINAL: mavproxy --master=com5 --out=udp:127.0.0.1:14550 --out=udp:127.0.0.1:14551

        connection_string = 'udp:127.0.0.1:14551'#'COM5' #tcp:127.0.0.1:5763
        baud = 57600 #115200
        dron.connect(connection_string, baud, freq=10)
        publish_event('connected')
        print("connected")

    if command == 'arm_takeOff':
        if dron.state == 'connected':
            altitude = int(message.payload.decode("utf-8")) if message.payload else 5
            print (f'vamos a armar a{altitude}m')
            dron.arm()
            print ('vamos a despegar')
            dron.takeOff(altitude, blocking=False, callback=publish_event, params='flying')

    if command == 'changeAltitude':
        if dron.state == 'flying':
            altitude = int(message.payload.decode("utf-8"))
            dron.change_altitude(altitude)

    if command == 'go':
        if dron.state == 'flying':
            direction = message.payload.decode("utf-8")
            dron.go(direction)

    if command == 'Land':
        if dron.state == 'flying':
            # operación no bloqueante. Cuando acabe publicará el evento correspondiente
            dron.Land(blocking=False, callback=publish_event, params='landed')

    if command == 'RTL':
        if dron.state == 'flying':
            dron.changeNavSpeed(1.0) # para que vuelva a casa despacio
            # operación no bloqueante. Cuando acabe publicará el evento correspondiente
            dron.RTL(blocking=False, callback=publish_event, params='atHome')

    if command == 'startTelemetry':
        # indico qué función va a procesar los datos de telemetría cuando se reciban
        dron.send_telemetry_info(publish_telemetry_info)

    if command == 'stopTelemetry':
        dron.stop_sending_telemetry_info()

    if command == 'changeHeading':
        if dron.state == 'flying':
            heading = float(message.payload.decode("utf-8"))
            threading.Thread(
                target=dron.changeHeading,
                args=(int(heading),),
                daemon=True
            ).start()

    if command == 'changeNavSpeed':
        if dron.state == 'flying':
            speed = float(message.payload.decode("utf-8"))
            dron.changeNavSpeed(float(speed))

    if command == 'goto':
        if dron.state == 'flying':
            data = json.loads(message.payload.decode("utf-8"))
            lat = float(data['lat'])
            lon = float(data['lon'])
            alt = float(data['alt'])
            dron.goto(lat, lon, alt, blocking=False)

def on_connect(client, userdata, flags, rc):
    global connected
    if rc==0:
        print("connected OK Returned code=",rc)
        connected = True
    else:
        print("Bad connection Returned code=",rc)


dron = Dron()

client = mqtt.Client(f"Servicio_{usuario}", transport="websockets")

# me conecto al broker publico y gratuito
broker_address = "dronseetac.upc.edu"
broker_port = 8000
client.username_pw_set("dronsEETAC", "mimara1456.")

client.on_message = on_message
client.on_connect = on_connect
client.connect (broker_address,broker_port)

# me subscribo a todos los mensajes cuyo destino sea este servicio
client.subscribe(f'{usuario}/autopilotServiceDemo/#')
print (f'AutopilotServiceDemo esperando peticiones de {usuario}')
client.loop_forever()

