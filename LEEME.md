# PIXDECK V3 · por USB (sin WiFi)

- `PIXDECK_V3/` es el firmware del ESP32 (pantalla ILI9341 con la librería TFT_eSPI).
- `PixDeckHub/` es el programa de Windows (C# .NET 8 + LibreHardwareMonitorLib).
- `docs/User_Setup.h` es la configuración de TFT_eSPI que usa PIXDECK (pines del ESP32).

## 1. Subir el firmware
1. **Configura TFT_eSPI (obligatorio).** Reemplaza `Documentos\Arduino\libraries\TFT_eSPI\User_Setup.h` por `docs/User_Setup.h`; antes guarda una copia del original. La librería viene de fábrica con pines de ESP8266 (NodeMCU) y con ellos la pantalla queda en blanco.
   - Pines del archivo: MISO 19, MOSI 23, SCLK 18, CS 5, DC 2, RST 4.
   - Driver ILI9341 a 27 MHz, sin touch.
   - Si actualizas la librería, vuelve a copiarlo.
2. Abre `PIXDECK_V3/PIXDECK_V3.ino` en el IDE de Arduino.
3. Elige la placa **ESP32 Dev Module** (core esp32 3.x) y súbelo. El aviso `TOUCH_CS pin not defined` es normal.
4. El buzzer va en GPIO 25. El botón **BOOT** de la placa cambia de tema.
5. **Cierra el Monitor Serie** del IDE: el puerto COM solo lo puede usar un programa a la vez.

Sin IDE: usa `PIXDECK_firmware_x.x.x_ESP32.bin` del Release en https://espressif.github.io/esptool-js/, en la dirección **0x0**. Ya trae esta configuración.

## 2. Programa de la PC

### Con el instalador (recomendado)
Ejecuta `dist\PixDeckHub_Setup_3.0.0.exe`:
- **Qué hace:** instala en `Archivos de programa\PixDeck Hub`, crea el acceso en el menú Inicio (y en el escritorio, si quieres) y aparece en *Configuración › Aplicaciones*.
- **Inicio con Windows:** una opción del propio instalador, activada por defecto.
- **Para actualizar:** vuelve a ejecutar un instalador nuevo encima; se conserva tu configuración.
- **Para generar el instalador después de cambiar código:** `.\build_installer.ps1`.
- **Requisito:** .NET 8 Desktop Runtime. Si falta, Windows avisa y ofrece descargarlo.

### Compilarlo a mano
Compílalo:

    cd PixDeckHub
    dotnet build -c Release

El ejecutable queda en `PixDeckHub\bin\Release\net8.0-windows\PixDeckHub.exe`. Pide permisos de administrador porque LibreHardwareMonitor los necesita para leer las temperaturas del CPU.

- **Si lo abres a mano:** ves la pantalla de bienvenida y luego el panel con **SETTINGS** y **UI**.
- **Si arranca con Windows:** se activa en Settings, General, "Iniciar con Windows". Corre en segundo plano, sin ventana, y solo deja el icono en la bandeja.
- **Al cerrar la ventana**, el programa sigue en la bandeja. Para cerrarlo del todo: clic derecho en el icono y **Salir**.
- **La primera vez** detecta solo los sensores. Puedes cambiarlos en Settings, Sensores.
- **Lo que configures** se guarda en `%APPDATA%\PixDeckHub\config.json`. El equipo también guarda su nombre, su tema y sus alertas en la flash.

## Temas
1. **ELECTRÓNICA:** chips QFP, osciloscopio con el historial de uso, multímetro con los datos extra y barras LED.
2. **INFORMÁTICA:** terminal estilo `top`, con barras de texto, tabla de sensores y cursor parpadeante.
3. **AUTOS:** dos relojes con aguja, testigos, computadora de viaje que va rotando los datos extra y "marcha" según el uso del CPU.
4. **ANIME:** paneles inclinados, números gigantes, líneas de impacto al 80% de uso o más y barras HP/MP.
5. **PELÍCULAS:** cinta en movimiento, marquesina con focos que corren, boletos para CPU y GPU, y créditos.
6. **PIXDECK:** el diseño original de PIXDECK V2, con gauges arriba y RAM/VRAM abajo.
7. **PIXDECK RINGS:** anillos dobles grandes, con el uso por fuera y la temperatura por dentro.
8. **PIXDECK GRAPH:** temperatura con gráfica de área y 4 mosaicos con mini anillos.

## Subir temas (.pxtheme)
En **UI**, el botón **Subir tema...** acepta un archivo `.pxtheme`. El programa lo valida, lo guarda en `%APPDATA%\PixDeckHub\temas` y lo sube al equipo, que lo guarda en su flash.

- Caben hasta **8 temas subidos**.
- **Al reconectar**, el programa compara los temas y vuelve a subir solo los que cambiaron.
- **Para borrar uno**, ponlo en pantalla y usa **Eliminar tema subido**.
- **De ejemplo** tienes `temas/neon_minimal.pxtheme`.

### Formato (base para el futuro editor de temas)
El archivo es un JSON con `name`, `author`, `description`, `background` y `widgets` (máximo 96). Se aceptan comentarios `//`. La pantalla mide 320x240 y los colores van como `#RRGGBB`.

| type | campos |
|---|---|
| `rect` | x, y, w, h, radius, color, outline (true = solo borde) |
| `circle` | x, y, r, color, fill |
| `line` | x, y, x2, y2, width, color |
| `text` | x, y, text, font, size, align, color. El texto admite `{name}` `{cpu}` `{gpu}` `{time}` `{hm}` `{status}` |
| `value` | x, y, source, decimals, suffix, font, size, align, color, tempColor (se pinta ámbar/rojo según las alertas) |
| `arc` | x, y, r, thickness, start, end (grados: 0 = abajo, en sentido horario), source, max, color, track |
| `bar` | x, y, w, h, source, max, color, track |
| `graph` | x, y, w, h, source (cpu_temp, cpu_load, gpu_temp, gpu_load), min, max, color |

- **`font`:** el estilo de letra. El tamaño se elige aparte, con `size`.
  - **Sans, Serif y Mono:** `sans-regular`, `sans-bold`, `sans-italic`, `sans-bolditalic` (igual con `serif-...` y `mono-...`). `size` va de 1 a 7: 1-4 son 9/12/18/24 pt nítidos y 5-7 son ampliaciones (se ven pixeladas).
  - **Tus fuentes:** `horyzen` (`size` 1-4) y `techno` (`size` 1-3).
  - **`seg7`:** 7 segmentos de 48 px, `size` 1-2. Solo trae números y `: - .`.
  - **`glcd`:** pixel 6 px, `size` 1-4.
  - **Nombres de versiones anteriores** (`small`, `big`, `sans18b`...) se convierten solos al abrir el archivo.
- **`align`:** `TL` `TC` `TR` `ML` `MC` `MR` `BL` `BC` `BR`.
- **`source`:** `cpu_temp` `cpu_load` `cpu_clock` `cpu_power` `gpu_temp` `gpu_load` `gpu_clock` `gpu_power` `ram_used` `ram_total` `ram_load` `vram_used` `vram_total` `vram_load` `extra1`..`extra4`.

## Si algo falla
- **Se queda en "ESPERANDO PC":** revisa que el programa esté abierto y que el Monitor Serie del IDE esté cerrado. En Settings, General, prueba a elegir el puerto COM a mano.
- **La CPU marca "--":** ejecútalo como administrador. Si sigue igual, instala el driver PawnIO, que es el que usa LibreHardwareMonitor 0.9.6.
- **Un dato no aparece:** en Settings, Sensores, elige el sensor correcto. A la derecha ves su valor en vivo.

## Protocolo (115200 baudios, una línea por mensaje)
    PC  -> ESP32:  ?   D|HHMMSS|v0..v16   N|nombre   L|cpu|gpu   X|i|etiqueta|unidad|max
                   S|tema|rotarSeg|alertaOn|alertaOff|ambar|sonido   T|tema   B   Q
                   U|slot|BEGIN|hash|nombre   U|slot|W|widget   U|slot|END   U|slot|DEL
    ESP32 -> PC :  PIXDECK|HELLO|ver|nombre|tema   OK|x   EVT|THEME|n   CT|slot|hash|nombre ... CT|END

Temas: 0-7 vienen en el firmware; 8 + slot son los subidos.

Orden de los valores en `D`: cpuT, cpuLoad, cpuMHz, cpuW, gpuT, gpuLoad, gpuMHz, gpuW, ramUsadaGB, ramTotalGB, ramLoad, vramUsadaMB, vramTotalMB, extra0..extra3. Un campo vacío significa "sin dato".
