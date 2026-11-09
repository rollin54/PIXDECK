# Software de terceros

PIXDECK usa estas librerías. Cada una conserva su propia licencia.

| Componente | Uso | Licencia |
|---|---|---|
| [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | lectura de sensores (Windows) | MPL-2.0 |
| [System.IO.Ports](https://github.com/dotnet/runtime) | puerto USB / serie | MIT |
| [.NET](https://github.com/dotnet/runtime) / Windows Forms | programa de Windows | MIT |
| [Arduino core para ESP32](https://github.com/espressif/arduino-esp32) | firmware | LGPL-2.1 |
| [TFT_eSPI](https://github.com/Bodmer/TFT_eSPI) | pantalla | FreeBSD / MIT (ver la librería) |
| Adafruit GFX FreeFonts (GNU FreeFont) | fuentes del firmware | GPL con excepción de fuentes |

El firmware se publica como código abierto en este repositorio, así que cualquiera puede recompilarlo con su propia versión del core de ESP32 (como pide la LGPL).

LibreHardwareMonitorLib se usa sin modificar, como paquete NuGet. Su código fuente está en el enlace de arriba.
