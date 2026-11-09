#pragma once
#include "gfx.h"

// ======================================================================================
// TEMA 4: ANIME  -  paneles inclinados estilo manga, numeros gigantes, lineas de
//                   velocidad y barras tipo HP/MP de RPG
// ======================================================================================

namespace an {
constexpr uint16_t BG      = rgb565(14, 10, 34);
constexpr uint16_t STRIPE  = rgb565(22, 16, 50);
constexpr uint16_t PANEL   = rgb565(30, 22, 64);
constexpr uint16_t MAGENTA = rgb565(255, 40, 140);
constexpr uint16_t CYAN    = rgb565(0, 230, 255);
constexpr uint16_t YELLOW  = rgb565(255, 230, 0);
constexpr uint16_t WHITE   = rgb565(255, 255, 255);
constexpr uint16_t PINK    = rgb565(255, 175, 205);
constexpr uint16_t PINK_DK = rgb565(235, 110, 160);
constexpr uint16_t HP      = rgb565(70, 240, 110);
constexpr uint16_t MP      = rgb565(70, 150, 255);
constexpr uint16_t TRACK   = rgb565(44, 34, 84);

static void panel(int x, int y, const char* label, uint16_t accent, float temp, float load, float loadA) {
  const int w = 140, h = 112, sk = 10;
  para(x, y, w, h, sk, PANEL);
  paraOutline(x, y, w, h, sk, accent);
  paraOutline(x + 1, y, w, h, sk, accent);
  int lw = min(tw(label, F_SMALL), 108);           // etiqueta: el fondo se adapta al texto
  if (tw(label, F_SMALL) > 108) lw = min(tw(label, nullptr), 108);
  para(x + 12, y + 5, max(50, lw + 14), 17, 4, accent);
  fitTxt(label, x + 18, y + 7, 108, BG);

  int cx = x + 76, cy = y + 54;
  float p = clampPct(loadA);
  if (p >= 80) {                                    // lineas de impacto cuando va a tope
    float rot = (NOW / 60) % 360;
    for (int i = 0; i < 18; i++) {
      float a = rot + i * 20;
      ln((int)px(cx, 34, a), (int)py(cy, 30, a), (int)px(cx, 62, a), (int)py(cy, 52, a), YELLOW);
    }
  }
  bool big = isnan(temp) || temp < 100;
  const char* ts = fmt(temp);
  const GFXfont* tf = big ? F_HUGE : F_BIG;
  txt(ts, cx - 6, cy - 2, tf, tempColor(temp, WHITE), MC_DATUM);
  int dx = cx - 6 + tw(ts, tf) / 2 + 8;
  deg(dx, cy - 16, 4, accent);
  deg(dx, cy - 16, 3, accent);

  // barra segmentada inclinada
  int on = (int)lroundf(p / 10.0f);
  for (int i = 0; i < 10; i++) para(x + 10 + i * 12, y + 84, 9, 10, 4, i < on ? (i >= 8 ? MAGENTA : accent) : TRACK);
  char b[16];
  snprintf(b, sizeof b, "%s%%", fmt(load));
  txt(b, x + w - 4, y + 98, F_SMALL, WHITE, TR_DATUM);
  txt("LOAD", x + 10, y + 98, F_SMALL, accent);
}

static void hpBar(int y, const char* tag, uint16_t c, float p, const char* val) {
  para(4, y, 50, 16, 5, c);
  txt(tag, 12, y + 3, F_SMALL, BG);
  int x = 62, w = 170;
  fr(x - 1, y + 1, w + 2, 14, WHITE);
  fr(x, y + 2, w, 12, TRACK);
  int fw = (int)(w * clampPct(p) / 100.0f);
  fr(x, y + 2, fw, 12, c);
  fr(x, y + 2, fw, 3, (uint16_t)(c | 0x4208));   // brillo superior
  txt(val, 316, y + 3, F_SMALL, WHITE, TR_DATUM);
}

static void draw() {
  cv.fillSprite(BG);
  int off = (NOW / 40) % 24;                        // franjas diagonales en movimiento
  for (int x = -240 + off; x < 340; x += 24) {
    ln(x, 0, x + 120, 240, STRIPE);
    ln(x + 1, 0, x + 121, 240, STRIPE);
  }

  // Banner superior
  para(-12, 4, 236, 30, 14, MAGENTA);
  if (tw(S.name, F_BIG) <= 205) txt(S.name, 12, 8, F_BIG, WHITE);
  else if (tw(S.name, F_SANS) <= 205) txt(S.name, 12, 10, F_SANS, WHITE);
  else txt(S.name, 12, 14, F_SMALL, WHITE);
  float cl = ST.v[V_CPU_L];
  const char* mode = isnan(cl) ? "OFF" : cl >= 80 ? "TURBO!" : cl >= 25 ? "NORMAL" : "CHILL";
  para(230, 8, 82, 22, 8, YELLOW);
  txt(mode, 274, 13, F_SMALL, BG, TC_DATUM);

  panel(6, 42, S.cpuLabel, CYAN, ST.v[V_CPU_T], ST.v[V_CPU_L], A[V_CPU_L]);
  panel(164, 42, S.gpuLabel, MAGENTA, ST.v[V_GPU_T], ST.v[V_GPU_L], A[V_GPU_L]);

  hpBar(164, "RAM", HP, A[V_RAM_L], ramStr());
  hpBar(188, "VRAM", MP, vramPctA(), vramStr());

  // Linea inferior: extras + reloj
  Metric m[2];
  int n = collectMetrics(m, 2);
  char b[48] = "";
  for (int i = 0; i < n; i++) {
    char one[24];
    snprintf(one, sizeof one, "%s%s %s%s", i ? "  " : "", m[i].label, m[i].val, m[i].unit);
    strncat(b, one, sizeof b - strlen(b) - 1);
  }
  txt(b, 8, 218, F_SMALL, PINK);
  txt(clockStr(false), 314, 218, F_SMALL, YELLOW, TR_DATUM);
}
}  // namespace an
