#pragma once
#include <Arduino.h>

// ============ HARDWARE (ESP32 clasico) ============
// Los pines del TFT siguen en el User_Setup.h de TFT_eSPI (no cambian).
#define BUZZER_PIN     25
#define BOOT_BTN_PIN   0        // boton BOOT de la placa: cambia de tema

// ============ ENLACE USB ============
#define SERIAL_BAUD        115200
#define OFFLINE_AFTER_MS   5000  // sin datos por este tiempo => pantalla "esperando PC"
#define FW_VERSION         "3.0"

// ============ RENDER ============
#define FRAME_MS           50    // ~20 fps maximo
#define NATIVE_COUNT       8     // temas incluidos en el firmware (los subidos van despues)

// ============ PALETA BASE (RGB565) ============
constexpr uint16_t rgb565(uint8_t r, uint8_t g, uint8_t b) {
  return ((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3);
}

constexpr uint16_t C_BG      = rgb565(6, 8, 14);
constexpr uint16_t C_TRACK   = rgb565(30, 36, 58);
constexpr uint16_t C_TEXT    = rgb565(235, 240, 255);
constexpr uint16_t C_DIM     = rgb565(120, 130, 165);
constexpr uint16_t C_VIOLET  = 0x780F;
constexpr uint16_t C_OK      = rgb565(70, 225, 130);
constexpr uint16_t C_WARN    = rgb565(255, 195, 40);
constexpr uint16_t C_HOT     = rgb565(255, 60, 60);
