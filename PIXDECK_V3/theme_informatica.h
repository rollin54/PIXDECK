#pragma once
#include "gfx.h"

// ======================================================================================
// TEMA 2: INFORMATICA  -  terminal tipo "top/htop" con barras de caracteres y cursor
// ======================================================================================

namespace inf {
constexpr uint16_t BG     = rgb565(6, 9, 10);
constexpr uint16_t SCAN   = rgb565(11, 16, 16);
constexpr uint16_t BAR    = rgb565(32, 36, 44);
constexpr uint16_t GREEN  = rgb565(80, 255, 120);
constexpr uint16_t GDIM   = rgb565(36, 110, 58);
constexpr uint16_t AMBER  = rgb565(255, 180, 40);
constexpr uint16_t CYAN   = rgb565(90, 210, 255);
constexpr uint16_t RED    = rgb565(255, 70, 70);
constexpr uint16_t WHITE  = rgb565(205, 220, 210);
constexpr int CW = 11;          // ancho de caracter de FreeMonoBold9pt
constexpr int BAR_CH = 12;      // caracteres de la barra

// CPU[||||||||    ] 54.2%
static void meter(int y, const char* name, float p, const char* val) {
  txt(name, 6, y, F_MONO, CYAN);
  txt("[", 6 + 3 * CW, y, F_MONO, WHITE);
  int n = (int)lroundf(clampPct(p) * BAR_CH / 100.0f);
  char one[2] = {'|', 0};
  for (int i = 0; i < BAR_CH; i++) {
    int x = 6 + (4 + i) * CW;
    if (i < n) {
      uint16_t c = i < BAR_CH * 6 / 10 ? GREEN : i < BAR_CH * 85 / 100 ? AMBER : RED;
      txt(one, x, y, F_MONO, c);
    } else {
      fr(x + 4, y + 6, 2, 2, GDIM);     // punto de relleno
    }
  }
  txt("]", 6 + (4 + BAR_CH) * CW, y, F_MONO, WHITE);
  txt(val, 314, y, F_MONO, WHITE, TR_DATUM);
}

static void draw() {
  cv.fillSprite(BG);
  for (int y = OY; y < OY + BAND; y += 3) hl(0, y, 320, SCAN);   // lineas de CRT

  // Barra de ventana
  fr(0, 0, 320, 18, BAR);
  fc(10, 9, 4, RED);
  fc(23, 9, 4, AMBER);
  fc(36, 9, 4, GREEN);
  char title[40];
  snprintf(title, sizeof title, "root@%s: ~ (top)", S.name);
  txt(title, 170, 5, nullptr, WHITE, TC_DATUM);

  char b[20];
  snprintf(b, sizeof b, "%s%%", fmt(ST.v[V_CPU_L], 1));
  meter(24, "CPU", A[V_CPU_L], b);
  snprintf(b, sizeof b, "%s%%", fmt(ST.v[V_GPU_L], 1));
  meter(44, "GPU", A[V_GPU_L], b);
  meter(64, "MEM", A[V_RAM_L], ramStr());
  meter(84, "VRM", vramPctA(), vramStr());

  for (int x = 6; x < 314; x += 6) hl(x, 105, 3, GDIM);

  // Tabla de sensores
  txt(S.cpuLabel, 100, 110, nullptr, CYAN);
  txt(S.gpuLabel, 214, 110, nullptr, CYAN);

  txt("TEMP", 6, 122, F_MONO, AMBER);
  snprintf(b, sizeof b, "%sC", fmt(ST.v[V_CPU_T]));
  txt(b, 100, 122, F_MONO, tempColor(ST.v[V_CPU_T], GREEN));
  snprintf(b, sizeof b, "%sC", fmt(ST.v[V_GPU_T]));
  txt(b, 214, 122, F_MONO, tempColor(ST.v[V_GPU_T], GREEN));

  txt("CLK", 6, 142, F_MONO, AMBER);
  snprintf(b, sizeof b, "%sG", fmt(isnan(ST.v[V_CPU_CLK]) ? NAN : ST.v[V_CPU_CLK] / 1000.0f, 2));
  txt(b, 100, 142, F_MONO, GREEN);
  snprintf(b, sizeof b, "%sM", fmt(ST.v[V_GPU_CLK]));
  txt(b, 214, 142, F_MONO, GREEN);

  txt("PWR", 6, 162, F_MONO, AMBER);
  snprintf(b, sizeof b, "%sW", fmt(ST.v[V_CPU_PWR]));
  txt(b, 100, 162, F_MONO, GREEN);
  snprintf(b, sizeof b, "%sW", fmt(ST.v[V_GPU_PWR]));
  txt(b, 214, 162, F_MONO, GREEN);

  // Extras como salida de comando; si no hay, grafica de temperaturas
  int nx = 0;
  for (int i = 0; i < 4 && nx < 2; i++) {
    if (!S.extra[i].label[0]) continue;
    float v = ST.v[V_X0 + i];
    char l[40];
    snprintf(l, sizeof l, "> %-8s %s %s", S.extra[i].label, fmt(v, (!isnan(v) && fabsf(v) < 10) ? 1 : 0), S.extra[i].unit);
    txt(l, 6, 182 + nx * 18, F_MONO, WHITE);
    nx++;
  }
  if (!nx) {
    dr(6, 182, 308, 34, GDIM);
    spark(8, 184, 304, 30, hCpuT, 20, 100, AMBER);
    spark(8, 184, 304, 30, hGpuT, 20, 100, CYAN);
    txt("temp cpu/gpu", 310, 185, nullptr, GDIM, TR_DATUM);
  }

  // Prompt con cursor parpadeante y reloj
  char prompt[32];
  snprintf(prompt, sizeof prompt, "%.9s:~$", S.name);
  for (char* p = prompt; *p; p++) *p = tolower(*p);
  txt(prompt, 6, 222, F_MONO, GREEN);
  int cx = 6 + tw(prompt, F_MONO) + 6;
  if ((NOW / 500) & 1) fr(cx, 222, 9, 14, GREEN);
  txt(clockStr(), 314, 222, F_MONO, GDIM, TR_DATUM);
}
}  // namespace inf
