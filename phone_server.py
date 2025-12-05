#!/usr/bin/env python3
import socket
import json
import os
from http.server import HTTPServer, BaseHTTPRequestHandler

def get_local_ip():
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80)) 
        local_ip = s.getsockname()[0]
        s.close()
        return local_ip
    except Exception:
        return "127.0.0.1"

HTTP_PORT = 8080
UNITY_IP = "127.0.0.1"
UNITY_GYRO_PORT = 5006 
LOCAL_IP = get_local_ip() 

udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

print("=" * 60)
print("CLEAN OCEAN VR - PHONE CONTROLLER SERVER")
print("=" * 60)
print(f"Servidor HTTP: puerto {HTTP_PORT}")
print(f"Enviando datos a Unity: {UNITY_IP}:{UNITY_GYRO_PORT}")
print(f"IP local: {LOCAL_IP}")
print()
print("OPCIONES:")
print(f"  Solo Botones: http://{LOCAL_IP}:{HTTP_PORT}/buttons")
print(f"  Giroscopio:   http://{LOCAL_IP}:{HTTP_PORT}/")
print()
print("Presiona Ctrl+C para detener")
print("=" * 60)

class PhoneControllerHandler(BaseHTTPRequestHandler):
    
    def serve_html_file(self, filename):
        try:
            html_path = os.path.join(os.path.dirname(__file__), filename)
            
            with open(html_path, 'r', encoding='utf-8') as f:
                html_content = f.read()

            html_content = html_content.replace(
                'value="192.168.1.100"',
                f'value="{LOCAL_IP}"'
            )
            
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.send_header('Access-Control-Allow-Origin', '*')
            self.end_headers()
            self.wfile.write(html_content.encode('utf-8'))
            
            print(f"{filename} servido a {self.client_address[0]}")
            
        except FileNotFoundError:
            self.send_response(404)
            self.send_header('Content-Type', 'text/html')
            self.end_headers()
            self.wfile.write(f"<h1>404 - {filename} no encontrado</h1>".encode('utf-8'))
        except Exception as e:
            self.send_response(500)
            self.end_headers()
            self.wfile.write(f"Error: {e}".encode('utf-8'))
    
    def do_GET(self):
        if self.path == '/phone_controller.html' or self.path == '/':
            self.serve_html_file('phone_controller.html')
        elif self.path == '/buttons' or self.path == '/phone_buttons_controller.html':
            self.serve_html_file('phone_buttons_controller.html')
        elif self.path == '/api/ip':
            try:
                self.send_response(200)
                self.send_header('Content-Type', 'application/json')
                self.send_header('Access-Control-Allow-Origin', '*')
                self.end_headers()
                
                ip_data = {
                    'ip': LOCAL_IP,
                    'port': HTTP_PORT,
                    'gyroPort': UNITY_GYRO_PORT
                }
                self.wfile.write(json.dumps(ip_data).encode('utf-8'))
            except Exception as e:
                self.send_response(500)
                self.end_headers()
        else:
            self.send_response(404)
            self.send_header('Content-Type', 'text/html')
            self.end_headers()
            self.wfile.write(b"<html><body><h1>404 Not Found</h1></body></html>")
    
    def do_POST(self):
        if self.path == '/gyro':
            try:
                content_length = int(self.headers['Content-Length'])
                post_data = self.rfile.read(content_length)
                
                data = json.loads(post_data.decode('utf-8'))
                
                gyro_x = data.get('x', 0)
                gyro_y = data.get('y', 0)
                gyro_z = data.get('z', 0)
                grab_button = data.get('grabButton', False)
                highlight_button = data.get('highlightButton', False)
                
                unity_data = {
                    'x': gyro_x,
                    'y': gyro_y,
                    'z': gyro_z,
                    'grabButton': grab_button,
                    'highlightButton': highlight_button
                }
                
                json_data = json.dumps(unity_data).encode('utf-8')
                udp_socket.sendto(json_data, (UNITY_IP, UNITY_GYRO_PORT))
                
                self.send_response(200)
                self.send_header('Access-Control-Allow-Origin', '*')
                self.end_headers()
                self.wfile.write(b'OK')
                
            except Exception as e:
                self.send_response(500)
                self.end_headers()
                self.wfile.write(f"Error: {e}".encode('utf-8'))
        else:
            self.send_response(404)
            self.end_headers()
    
    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        self.end_headers()
    
    def log_message(self, format, *args):
        pass

def run_server():
    try:
        server = HTTPServer(('0.0.0.0', HTTP_PORT), PhoneControllerHandler)
        print(f"Servidor iniciado en http://{LOCAL_IP}:{HTTP_PORT}/")
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nServidor detenido")
        udp_socket.close()
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    run_server()
