#pragma once
#include "gfx.h"

// ======================================================================================
// TEMA 1: ELECTRONICA  -  placa PCB, chips QFP, osciloscopio, multimetro y barras LED
// ======================================================================================

namespace el {
constexpr uint16_t PCB     = rgb565(8, 44, 26);
constexpr uint16_t PCB_DK  = rgb565(4, 28, 16);
constexpr uint16_t TRACE   = rgb565(22, 92, 52);
constexpr uint16_t PAD     = rgb565(205, 165, 70);
constexpr uint16_t SILK    = rgb565(225, 235, 225);
constexpr uint16_t CHIP    = rgb565(20, 20, 24);
constexpr uint16_t CHIP_TX = rgb565(170, 170, 180);
constexpr uint16_t SCOPE   = rgb565(4, 18, 12);
constexpr uint16_t GRID    = rgb565(22, 64, 38);
constexpr uint16_t CH1     = rgb565(255, 225, 40);
constexpr uint16_t CH2     = rgb565(40, 220, 255);
constexpr uint16_t BEZEL   = rgb565(48, 52, 58);
constexpr uint16_t DMM     = rgb565(235, 185, 20);
constexpr uint16_t LCD     = rgb565(150, 172, 140);
constexpr uint16_t LCD_TX  = rgb565(20, 34, 22);
constexpr uint16_t LED_G   = rgb565(60, 255, 90),  LED_G0 = rgb565(14, 56, 24);
constexpr uint16_t LED_A   = rgb565(255, 170, 0),  LED_A0 = rgb565(64, 44, 6);
constexpr uint16_t LED_R   = rgb565(255, 45, 45),  LED_R0 = rgb565(64, 12, 12);

static void via(int x, int y) { fc(x, y, 4, PAD); fc(x, y, 2, PCB_DK); }

static void traces() {
  // Pistas que salen de los chips hacia los bordes, con codos a 45 grados
  for (int i = 0; i < 7; i++) {
    int y = 40 + i * 12;
    hl(0, y, 10, TRACE);
    hl(0, y + 1, 10, TRACE);
    hl(310, y, 10, TRACE);
    hl(310, y + 1, 10, TRACE);
  }
  for (int i = 0; i < 4; i++) {
    int y = 46 + i * 20;
    ln(150, y, 158, y + 8, TRACE);
    ln(151, y, 159, y + 8, TRACE);
    hl(158, y + 8, 12, TRACE);
    hl(158, y + 9, 12, TRACE);
  }
  via(160, 36);
  via(160, 116);
  vl(160, 40, 72, TRACE);
  via(204, 128);
  via(110, 128);
  hl(114, 128, 86, TRACE);
}

// Chip QFP con datos
static void chip(int x, int y, const char* label, const char* ref, uint16_t accent,
                 float temp, float load, float loadA, const char* line2) {
  const int w = 122, h = 92;
  for (int i = 0; i < 7; i++) {           // pines laterales
    fr(x - 6, y + 10 + i * 12, 6, 4, PAD);
    fr(x + w, y + 10 + i * 12, 6, 4, PAD);
  }
  for (int i = 0; i < 8; i++) {           // pines arriba/abajo
    fr(x + 10 + i * 14, y - 5, 4, 5, PAD);
    fr(x + 10 + i * 14, y + h, 4, 5, PAD);
  }
  frr(x, y, w, h, 4, CHIP);
  drr(x, y, w, h, 4, rgb565(50, 50, 58));
  fc(x + 9, y + 9, 3, rgb565(60, 60, 68));        // marca del pin 1

  fitTxt(label, x + 18, y + 5, w - 40, accent);
  txt(ref, x + w - 8, y + 5, nullptr, CHIP_TX, TR_DATUM);

  txt(fmt(temp), x + 88, y + 22, F_BIG, tempColor(temp, SILK), TR_DATUM);
  deg(x + 95, y + 25, 3, accent);
  txt("C", x + 101, y + 22, F_SMALL, accent);

  char b[16];
  snprintf(b, sizeof b, "%s%%", fmt(load));
  txt("LOAD", x + 8, y + 52, F_SMALL, CHIP_TX);
  txt(b, x + w - 8, y + 52, F_SMALL, SILK, TR_DATUM);
  txt(line2, x + w - 8, y + 66, nullptr, CHIP_TX, TR_DATUM);
  txt("CLK", x + 8, y + 66, nullptr, CHIP_TX);

  // 10 LEDs de carga
  int on = (int)lroundf(clampPct(loadA) / 10.0f);
  for (int i = 0; i < 10; i++) {
    uint16_t c = i < 6 ? (i < on ? LED_G : LED_G0) : i < 8 ? (i < on ? LED_A : LED_A0) : (i < on ? LED_R : LED_R0);
    fc(x + 12 + i * 11, y + 83, 3, c);
  }
}

static void scope(int x, int y, int w, int h) {
  frr(x, y, w, h, 5, BEZEL);
  int sx = x + 4, sy = y + 4, sw = w - 8, sh = h - 8;
  fr(sx, sy, sw, sh, SCOPE);
  for (int i = 1; i < 8; i++)                    // reticula punteada
    for (int yy = sy + 2; yy < sy + sh; yy += 4) fr(sx + i * sw / 8, yy, 1, 1, GRID);
  for (int i = 1; i < 4; i++)
    for (int xx = sx + 2; xx < sx + sw; xx += 4) fr(xx, sy + i * sh / 4, 1, 1, GRID);
  hl(sx, sy + sh / 2, sw, rgb565(30, 90, 52));

  spark(sx + 2, sy + 4, sw - 4, sh - 8, hCpuL, 0, 100, CH1);
  spark(sx + 2, sy + 4, sw - 4, sh - 8, hGpuL, 0, 100, CH2);

  int sweep = sx + (int)((NOW / 12) % sw);       // barrido del haz
  vl(sweep, sy, sh, rgb565(40, 120, 70));

  txt("CH1 CPU", sx + 4, sy + 3, nullptr, CH1);
  txt("CH2 GPU", sx + 52, sy + 3, nullptr, CH2);
  txt("20%/div", sx + sw - 4, sy + sh - 10, nullptr, rgb565(90, 160, 110), TR_DATUM);
}

static void multimeter(int x, int y, int w, int h) {
  frr(x, y, w, h, 8, DMM);
  frr(x + 6, y + 6, w - 12, h - 18, 3, LCD);
  txt("DMM", x + w / 2, y + h - 11, nullptr, rgb565(60, 40, 0), TC_DATUM);
  Metric m[3];
  int n = collectMetrics(m, 3);
  if (!n) {
    txt("SIN", x + w / 2, y + 16, F_SMALL, LCD_TX, TC_DATUM);
    txt("EXTRAS", x + w / 2, y + 30, F_SMALL, LCD_TX, TC_DATUM);
    return;
  }
  for (int i = 0; i < n; i++) {
    int yy = y + 10 + i * 16;
    char b[20];
    snprintf(b, sizeof b, "%s%s", m[i].val, m[i].unit);
    txt(m[i].label, x + 10, yy, nullptr, LCD_TX);
    txt(b, x + w - 10, yy + 7, nullptr, LCD_TX, TR_DATUM);
  }
}

static void ledBar(int x, int y, const char* label, const char* val, float p) {
  txt(label, x, y, F_SMALL, SILK);
  txt(val, x + 150, y, F_SMALL, PAD, TR_DATUM);
  int on = (int)lroundf(clampPct(p) * 11 / 100.0f);
  for (int i = 0; i < 11; i++) {
    uint16_t c = i < 7 ? (i < on ? LED_G : LED_G0) : i < 9 ? (i < on ? LED_A : LED_A0) : (i < on ? LED_R : LED_R0);
    fr(x + i * 14, y + 14, 11, 9, c);
  }
}

static void draw() {
  cv.fillSprite(PCB);
  traces();

  // Header serigrafiado
  fr(0, 0, 320, 20, PCB_DK);
  txt(S.name, 8, 5, F_SMALL, SILK);
  bool on = linkOnline();
  fc(250, 10, 4, on ? LED_G : LED_R);
  txt("PWR", 240, 6, nullptr, SILK, TR_DATUM);
  txt(clockStr(), 312, 6, nullptr, SILK, TR_DATUM);

  chip(22, 30, S.cpuLabel, "U1", CH1, ST.v[V_CPU_T], ST.v[V_CPU_L], A[V_CPU_L], ghzStr(ST.v[V_CPU_CLK]));
  char g[16];
  if (isnan(ST.v[V_GPU_CLK])) snprintf(g, sizeof g, "-- MHz");
  else snprintf(g, sizeof g, "%.0f MHz", ST.v[V_GPU_CLK]);
  chip(176, 30, S.gpuLabel, "U2", CH2, ST.v[V_GPU_T], ST.v[V_GPU_L], A[V_GPU_L], g);

  scope(6, 132, 196, 74);
  multimeter(208, 132, 106, 74);

  ledBar(8, 210, "RAM", ramStr(), A[V_RAM_L]);
  ledBar(164, 210, "VRAM", vramStr(), vramPctA());
}
}  // namespace el
