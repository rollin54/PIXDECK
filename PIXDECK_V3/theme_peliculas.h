#pragma once
#include "gfx.h"

// ======================================================================================
// TEMA 5: PELICULAS  -  cine clasico: cinta de pelicula en movimiento, marquesina con
//                       focos que corren, boletos para CPU/GPU y "creditos" abajo
// ======================================================================================

namespace pe {
constexpr uint16_t BG      = rgb565(22, 4, 8);
constexpr uint16_t VELVET  = rgb565(96, 10, 22);
constexpr uint16_t VELV_DK = rgb565(52, 4, 12);
constexpr uint16_t GOLD    = rgb565(235, 185, 60);
constexpr uint16_t GOLD_DK = rgb565(120, 90, 30);
constexpr uint16_t BULB_ON = rgb565(255, 244, 190);
constexpr uint16_t BULB_OF = rgb565(90, 66, 26);
constexpr uint16_t FILM    = rgb565(12, 12, 12);
constexpr uint16_t HOLE    = rgb565(205, 195, 175);
constexpr uint16_t TICKET  = rgb565(246, 232, 198);
constexpr uint16_t T_RED   = rgb565(170, 22, 34);
constexpr uint16_t T_INK   = rgb565(60, 30, 26);
constexpr uint16_t WHITE   = rgb565(240, 236, 228);

static void filmStrip(int y, int dir) {
  fr(0, y, 320, 16, FILM);
  int off = (int)((NOW / 30) % 16);
  if (dir < 0) off = 16 - off;
  for (int x = -16 + off; x < 320; x += 16) frr(x + 4, y + 4, 8, 8, 2, HOLE);
}

static void curtains() {
  for (int x = 0; x < 16; x += 4) {
    fr(x, 16, 2, 208, VELVET);
    fr(x + 2, 16, 2, 208, VELV_DK);
    fr(304 + x, 16, 2, 208, VELVET);
    fr(306 + x, 16, 2, 208, VELV_DK);
  }
}

static void marquee() {
  const int x = 18, y = 20, w = 284, h = 50;
  frr(x, y, w, h, 6, VELV_DK);
  drr(x, y, w, h, 6, GOLD);
  drr(x + 1, y + 1, w - 2, h - 2, 5, GOLD_DK);
  int step = (int)(NOW / 140);
  int k = 0;
  for (int bx = x + 10; bx < x + w - 6; bx += 12, k++) {     // focos arriba y abajo
    fc(bx, y + 5, 2, ((k + step) % 3 == 0) ? BULB_ON : BULB_OF);
    fc(bx, y + h - 6, 2, ((k + 1000 - step) % 3 == 0) ? BULB_ON : BULB_OF);
  }
  txt(S.name, 160, y + 21, F_SERIF, GOLD, MC_DATUM);
  char b[40];
  snprintf(b, sizeof b, "EN CARTELERA  %s", clockStr(false));
  txt(b, 160, y + 36, nullptr, WHITE, TC_DATUM);
}

static void ticket(int x, int y, const char* label, int sala, float temp, float load, float loadA, const char* line) {
  const int w = 138, h = 94, stub = 30;
  frr(x, y, w, h, 6, TICKET);
  fc(x, y + h / 2, 7, BG);                         // muescas
  fc(x + w - 1, y + h / 2, 7, BG);
  for (int yy = y + 6; yy < y + h - 6; yy += 6) fr(x + w - stub, yy, 2, 3, GOLD_DK);   // perforado

  dr(x + 4, y + 4, w - stub - 8, h - 8, T_RED);
  fitTxt(label, x + 9, y + 9, 96, T_RED);
  txt(fmt(temp), x + 9, y + 26, F_BIG, isnan(temp) || temp < S.warn ? T_INK : T_RED);
  int tx = x + 9 + tw(fmt(temp), F_BIG) + 5;
  deg(tx, y + 29, 3, T_RED);
  txt("C", tx + 6, y + 26, F_SMALL, T_RED);

  char b[16];
  snprintf(b, sizeof b, "USO %s%%", fmt(load));
  txt(b, x + 9, y + 54, F_SMALL, T_INK);
  txt(line, x + 9, y + 67, nullptr, T_INK);
  bar(x + 9, y + 78, w - stub - 20, 6, loadA, T_RED, rgb565(220, 200, 160), TICKET);

  // talon
  int sx = x + w - stub + 3;
  txt("SALA", sx + stub / 2 - 2, y + 18, nullptr, T_INK, TC_DATUM);
  char s[3] = {(char)('0' + sala), 0};
  txt(s, sx + stub / 2 - 2, y + 32, F_SERIF, T_RED, TC_DATUM);
  txt("ADMIT", sx + stub / 2 - 2, y + 62, nullptr, T_INK, TC_DATUM);
  txt("ONE", sx + stub / 2 - 2, y + 72, nullptr, T_INK, TC_DATUM);
}

static void credit(int y, const char* label, float p, const char* val) {
  txt(label, 22, y, F_SMALL, GOLD);
  bar(76, y + 2, 140, 8, p, GOLD, VELV_DK, BG);
  txt(val, 300, y, F_SMALL, WHITE, TR_DATUM);
}

static void draw() {
  cv.fillSprite(BG);
  filmStrip(0, 1);
  filmStrip(224, -1);
  curtains();
  marquee();

  ticket(18, 76, S.cpuLabel, 1, ST.v[V_CPU_T], ST.v[V_CPU_L], A[V_CPU_L], ghzStr(ST.v[V_CPU_CLK]));
  char g[16];
  if (isnan(ST.v[V_GPU_CLK])) snprintf(g, sizeof g, "-- MHz");
  else snprintf(g, sizeof g, "%.0f MHz", ST.v[V_GPU_CLK]);
  ticket(164, 76, S.gpuLabel, 2, ST.v[V_GPU_T], ST.v[V_GPU_L], A[V_GPU_L], g);

  credit(176, "RAM", A[V_RAM_L], ramStr());
  credit(194, "VRAM", vramPctA(), vramStr());

  Metric m[3];
  int n = collectMetrics(m, 3);
  char b[64] = "";
  for (int i = 0; i < n; i++) {
    char one[24];
    snprintf(one, sizeof one, "%s%s %s%s", i ? " - " : "", m[i].label, m[i].val, m[i].unit);
    strncat(b, one, sizeof b - strlen(b) - 1);
  }
  if (n) txt(b, 160, 212, nullptr, GOLD_DK, TC_DATUM);
}
}  // namespace pe
