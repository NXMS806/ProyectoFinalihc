#!/usr/bin/env python3
"""
Test de detección automática de IP
"""

import socket

def get_local_ip():
    """Obtener la IP local de la PC"""
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        local_ip = s.getsockname()[0]
        s.close()
        return local_ip
    except Exception:
        return "127.0.0.1"

if __name__ == "__main__":
    print("=" * 60)
    print("TEST DE DETECCIÓN AUTOMÁTICA DE IP")
    print("=" * 60)
    
    ip = get_local_ip()
    
    print(f"\nIP detectada: {ip}\n")
    
    if ip == "127.0.0.1":
        print("WARNING: Se detectó localhost (127.0.0.1)")
        print("   Esto significa que no hay conexión de red activa.")
        print("   Verifica tu conexión WiFi.")
    else:
        print(f"Todo OK! Esta es tu IP local: {ip}")
        print(f"\nEn tu celular, abre:")
        print(f"   http://{ip}:8080/")
    
    print("\n" + "=" * 60)
