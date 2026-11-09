#pragma once
#include "gfx.h"

// ======================================================================================
// TEMA 3: AUTOS  -  tablero deportivo: dos relojes con aguja, testigos, marcha y
//                   "computadora de viaje" que va rotando los datos extra
// ======================================================================================

namespace au {
constexpr uint16_t BG     = rgb565(6, 6, 9);
constexpr uint16_t FACE   = rgb565(16, 16, 22);
constexpr uint16_t CHROME = rgb565(120, 124, 134);
constexpr uint16_t TICK   = rgb565(230, 230, 236);
constexpr uint16_t MINOR  = rgb565(110, 110, 120);
constexpr uint16_t RED    = rgb565(255, 40, 30);
constexpr uint16_t NEEDLE = rgb565(255, 70, 20);
constexpr uint16_t ICE    = rgb565(120, 205, 255);
constexpr uint16_t LAMP0  = rgb565(40, 40, 46);
constexpr uint16_t AMBER  = rgb565(255, 160, 0);
constexpr uint16_t GREEN  = rgb565(60, 230, 90);
constexpr uint16_t PANEL  = rgb565(14, 14, 20);

constexpr int SW0 = 50, SW1 = 310;            // barrido de 260 grados, abierto abajo

static void gauge(int cx, int cy, int R, float pA, float temp, const char* label, uint16_t glow) {
  fsc(cx, cy, R, FACE, BG);
  arc(cx, cy, R, R - 3, 0, 360, CHROME, BG);
  // zona roja (85..100 %)
  arc(cx, cy, R - 6, R - 11, SW0 + (SW1 - SW0) * 85 / 100, SW1, RED, FACE);
  // anillo de luz hasta el valor
  float p = clampPct(pA);
  int aEnd = SW0 + (int)((SW1 - SW0) * p / 100.0f);
  if (aEnd > SW0 + 2) arc(cx, cy, R - 4, R - 6, SW0, aEnd, glow, FACE);

  for (int i = 0; i <= 20; i++) {           // marcas: 11 mayores, 10 menores
    float a = SW0 + (SW1 - SW0) * i / 20.0f;
    bool major = (i % 2) == 0;
    float r0 = R - 7, r1 = major ? R - 17 : R - 12;
    if (major) wl(px(cx, r0, a), py(cy, r0, a), px(cx, r1, a), py(cy, r1, a), 1.5f, TICK, FACE);
    else ln(px(cx, r0, a), py(cy, r0, a), px(cx, r1, a), py(cy, r1, a), MINOR);
    if (major) {
      char n[4];
      snprintf(n, sizeof n, "%d", i / 2);
      float rn = R - 26;
      txt(n, (int)px(cx, rn, a), (int)py(cy, rn, a), nullptr, i >= 17 ? RED : TICK, MC_DATUM);
    }
  }
  fitTxt(label, cx, cy - 26, 72, ICE, MC_DATUM);
  txt("x10%", cx, cy - 13, nullptr, MINOR, MC_DATUM);

  // lectura digital de temperatura debajo del eje
  txt(fmt(temp), cx - 4, cy + 46, F_BIG, tempColor(temp, TICK), MC_DATUM);
  deg(cx + 28, cy + 38, 3, ICE);

  // aguja
  float a = SW0 + (SW1 - SW0) * p / 100.0f;
  wl(px(cx, -10, a), py(cy, -10, a), px(cx, R - 12, a), py(cy, R - 12, a), 3, NEEDLE, FACE);
  fsc(cx, cy, 8, rgb565(40, 40, 46), FACE);
  fsc(cx, cy, 4, NEEDLE, rgb565(40, 40, 46));
}

// Testigo luminoso redondo con texto
static void lamp(int x, int y, const char* t, uint16_t c, bool on) {
  fsc(x, y, 9, on ? c : LAMP0, BG);
  txt(t, x, y + 1, nullptr, on ? BG : MINOR, MC_DATUM);
}

// Indicador tipo "combustible" con segmentos
static void fuel(int x, int y, const char* label, const char* val, float p, uint16_t c) {
  frr(x, y, 112, 52, 6, PANEL);
  drr(x, y, 112, 52, 6, rgb565(40, 40, 50));
  txt(label, x + 8, y + 6, F_SMALL, ICE);
  txt(val, x + 104, y + 6, nullptr, TICK, TR_DATUM);
  txt("E", x + 8, y + 30, nullptr, MINOR);
  txt("F", x + 100, y + 30, nullptr, MINOR);
  int on = (int)lroundf(clampPct(p) * 8 / 100.0f);
  for (int i = 0; i < 8; i++) {
    uint16_t col = i < on ? (i >= 7 ? RED : c) : LAMP0;
    fr(x + 18 + i * 10, y + 26 - i, 8, 14 + i, col);   // segmentos que crecen
  }
}

static void draw() {
  cv.fillSprite(BG);
  bool on = linkOnline();
  bool hot = (!isnan(ST.v[V_CPU_T]) && ST.v[V_CPU_T] >= S.alertOn) || (!isnan(ST.v[V_GPU_T]) && ST.v[V_GPU_T] >= S.alertOn);
  bool warm = (!isnan(ST.v[V_CPU_T]) && ST.v[V_CPU_T] >= S.warn) || (!isnan(ST.v[V_GPU_T]) && ST.v[V_GPU_T] >= S.warn);

  // Barra superior: testigos, computadora de viaje y reloj
  lamp(14, 12, "T", AMBER, warm);
  lamp(36, 12, "!", RED, hot && ((NOW / 300) & 1));
  lamp(58, 12, "U", GREEN, on);

  Metric m[6];
  int n = collectMetrics(m, 6);
  frr(76, 2, 166, 20, 4, PANEL);
  if (n) {
    Metric& k = m[(NOW / 3000) % n];
    char b[32];
    snprintf(b, sizeof b, "%s  %s %s", k.label, k.val, k.unit);
    txt(b, 159, 12, F_SMALL, AMBER, MC_DATUM);
  } else {
    txt(S.name, 159, 12, F_SMALL, AMBER, MC_DATUM);
  }
  txt(clockStr(false), 314, 12, F_SMALL, TICK, MR_DATUM);

  gauge(82, 104, 72, A[V_CPU_L], ST.v[V_CPU_T], S.cpuLabel, ICE);
  gauge(238, 104, 72, A[V_GPU_L], ST.v[V_GPU_T], S.gpuLabel, rgb565(255, 120, 40));

  // Panel inferior: RAM, marcha, VRAM
  fuel(4, 184, "RAM", ramStr(), A[V_RAM_L], GREEN);
  fuel(204, 184, "VRAM", vramStr(), vramPctA(), ICE);

  frr(122, 184, 76, 52, 6, PANEL);
  drr(122, 184, 76, 52, 6, rgb565(40, 40, 50));
  txt("GEAR", 160, 191, nullptr, MINOR, TC_DATUM);
  char g[2] = {'N', 0};
  if (!isnan(ST.v[V_CPU_L])) g[0] = '1' + (char)constrain((int)(ST.v[V_CPU_L] / 20), 0, 5);
  txt(g, 160, 202, F_BIG, (g[0] >= '6') ? RED : TICK, TC_DATUM);
}
}  // namespace au
