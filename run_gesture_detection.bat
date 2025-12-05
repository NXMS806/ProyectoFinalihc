@echo off
echo ===============================================
echo    CLEAN OCEAN VR - CONFIGURACION AUTOMATICA
echo ===============================================
echo.

echo [1/4] Verificando Python 3.11.0...
pyenv local 3.11.0
if %errorlevel% neq 0 (
    echo ERROR: Python 3.11.0 no encontrado
    pause
    exit /b 1
)

echo [2/4] Activando entorno virtual...
call .\clean_ocean_vr_env\Scripts\activate.bat
if %errorlevel% neq 0 (
    echo ERROR: No se pudo activar el entorno virtual
    pause
    exit /b 1
)

echo [3/4] Instalando dependencias...
pip install -r requirements.txt
if %errorlevel% neq 0 (
    echo ERROR: Fallo en instalacion de dependencias
    pause
    exit /b 1
)

echo [4/4] Iniciando deteccion de gestos...
echo.
echo ===============================================
echo     GESTOS DISPONIBLES:
echo  - Mano Derecha Arriba: Rotar Derecha
echo  - Mano Izquierda Arriba: Rotar Izquierda  
echo  - Ambas Manos Arriba: Mover Adelante
echo  - Manos Juntas: Mover Atras
echo  - Puno Cerrado: Agarrar/Eliminar
echo ===============================================
echo.
echo Presiona Ctrl+C para detener...
echo.

python movimiento.py

pause