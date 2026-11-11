#pragma once
#include "gfx.h"

// ======================================================================================
// TEMAS 6-8: familia PIXDECK (el diseno original de PIXDECK_V2 y dos variantes)
//   6 PIXDECK        - el clasico: gauges arriba, barras RAM/VRAM abajo
//   7 PIXDECK RINGS  - anillos dobles grandes (uso por fuera, temperatura por dentro)
//   8 PIXDECK GRAPH  - temperatura con grafica de area + 4 mosaicos con mini anillos
// ======================================================================================

namespace pxd {
constexpr uint16_t HEADER = rgb565(14, 16, 30);
constexpr uint16_t CARD   = rgb565(16, 20, 34);
constexpr uint16_t BORDER = rgb565(38, 46, 74);
constexpr uint16_t CPU    = rgb565(255, 150, 20);
constexpr uint16_t GPU    = rgb565(90, 255, 120);
constexpr uint16_t RAMC   = rgb565(0, 205, 255);
constexpr uint16_t VRAMC  = rgb565(185, 100, 255);
constexpr uint16_t CPU_DK = rgb565(80, 46, 8);
constexpr uint16_t GPU_DK = rgb565(24, 76, 38);
constexpr int CW = 154;

static void header(bool withClock) {
  fr(0, 0, 320, 24, HEADER);
  hl(0, 24, 320, BORDER);
  title(S.name, 8, 12, 200, C_VIOLET, ML_DATUM);

  bool on = linkOnline();
  const char* t = withClock ? clockStr() : (on ? "ONLINE" : "OFFLINE");
  uint16_t col = on ? C_OK : C_HOT;
  int w = tw(t, F_SMALL);
  txt(t, 312, 12, F_SMALL, withClock ? C_TEXT : col, MR_DATUM);
  fc(312 - w - 10, 12, 4, col);
}

static void cardBase(int x, int y, int w, int h, const char* title, uint16_t accent) {
  fsr(x, y, w, h, 9, BORDER, C_BG);
  fsr(x + 1, y + 1, w - 2, h - 2, 8, CARD, BORDER);
  frr(x + 9, y + 7, 3, 11, 1, accent);
  fitTxt(title, x + 17, y + 6, w - 76, accent);
}

static void tempText(int x, int y, float t, const GFXfont* f, uint8_t size, uint8_t datum) {
  char b[12];
  snprintf(b, sizeof b, "%s C", fmt(t));
  txt(b, x, y, f, tempColor(t), datum, size);
}

// ---------------- 6: CLASICO (igual que PIXDECK_V2) ----------------
static void gaugeCard(int x, int y, const char* title, uint16_t accent, float animPct, float load,
                      float temp, const char* topRight) {
  cardBase(x, y, CW, 116, title, accent);
  if (topRight && topRight[0]) txt(topRight, x + CW - 10, y + 6, F_SMALL, C_DIM, TR_DATUM);
  int cx = x + CW / 2, cy = y + 66;
  arc(cx, cy, 44, 36, 60, 300, C_TRACK, CARD, true);
  int p = (int)lroundf(clampPct(animPct));
  if (p > 0) {
    int end = max(66, 60 + 240 * p / 100);
    arc(cx, cy, 44, 36, 60, end, p >= 90 ? C_HOT : accent, CARD, true);
  }
  txt(fmt(load), cx, cy - 4, F_SANS, C_TEXT, MC_DATUM);
  txt("%", cx, cy + 16, F_SMALL, C_DIM, MC_DATUM);
  tempText(cx, y + 101, temp, F_SANS, 1, MC_DATUM);
}

static void barCard(int x, int y, const char* title, uint16_t accent, float animPct, float pctVal,
                    const char* val, const char* unit, const char* caption) {
  cardBase(x, y, CW, 89, title, accent);
  char pb[12];
  snprintf(pb, sizeof pb, "%s %%", fmt(pctVal));
  txt(pb, x + CW - 10, y + 6, F_SMALL, C_DIM, TR_DATUM);
  txt(val, x + 10, y + 26, F_SANS, C_TEXT);
  int w = tw(val, F_SANS);
  txt(unit, x + 14 + w, y + 30, F_SMALL, C_DIM);
  bar(x + 10, y + 54, CW - 20, 12, animPct, animPct >= 90 ? C_HOT : accent, C_TRACK, CARD);
  txt(caption, x + 10, y + 72, F_SMALL, C_DIM);
}

static void classic() {
  cv.fillSprite(C_BG);
  header(false);
  char clk[16] = "";
  if (!isnan(ST.v[V_CPU_CLK])) snprintf(clk, sizeof clk, "%.1f GHz", ST.v[V_CPU_CLK] / 1000.0f);
  gaugeCard(4, 27, S.cpuLabel, CPU, A[V_CPU_L], ST.v[V_CPU_L], ST.v[V_CPU_T], clk);
  gaugeCard(162, 27, S.gpuLabel, GPU, A[V_GPU_L], ST.v[V_GPU_L], ST.v[V_GPU_T], "");

  char v[16], c[24];
  if (isnan(ST.v[V_RAM_USED])) snprintf(v, sizeof v, "--.-"); else snprintf(v, sizeof v, "%.1f", ST.v[V_RAM_USED]);
  if (isnan(ST.v[V_RAM_TOT])) snprintf(c, sizeof c, "TOTAL -- GB"); else snprintf(c, sizeof c, "TOTAL %d GB", (int)lroundf(ST.v[V_RAM_TOT]));
  barCard(4, 147, "RAM", RAMC, A[V_RAM_L], ST.v[V_RAM_L], v, "GB", c);

  // VRAM llega en MB: se muestra en GB para que entren tarjetas de 12, 16, 24 GB o mas
  if (isnan(ST.v[V_VRAM_USED])) snprintf(v, sizeof v, "--.-"); else snprintf(v, sizeof v, "%.1f", ST.v[V_VRAM_USED] / 1024.0f);
  if (isnan(ST.v[V_VRAM_TOT])) snprintf(c, sizeof c, "TOTAL -- GB"); else snprintf(c, sizeof c, "TOTAL %d GB", (int)lroundf(ST.v[V_VRAM_TOT] / 1024.0f));
  barCard(162, 147, "VRAM", VRAMC, vramPctA(), vramPct(), v, "GB", c);
}

// ---------------- 7: RINGS ----------------
static void ringCard(int x, const char* title, uint16_t accent, float loadA, float load, float temp,
                     const char* topRight, float watts) {
  const int y = 27, h = 160;
  cardBase(x, y, CW, h, title, accent);
  if (topRight[0]) txt(topRight, x + CW - 10, y + 6, nullptr, C_DIM, TR_DATUM);
  int cx = x + CW / 2, cy = y + 92;
  // anillo exterior: uso
  arc(cx, cy, 62, 54, 45, 315, C_TRACK, CARD, true);
  float p = clampPct(loadA);
  if (p > 1) arc(cx, cy, 62, 54, 45, 45 + (int)(270 * p / 100), p >= 90 ? C_HOT : accent, CARD, true);
  // anillo interior: temperatura (0-100 C)
  arc(cx, cy, 49, 45, 45, 315, rgb565(24, 28, 46), CARD, true);
  float tp = isnan(temp) ? 0 : constrain(temp, 0.0f, 100.0f);
  if (tp > 1) arc(cx, cy, 49, 45, 45, 45 + (int)(270 * tp / 100), tempColor(temp), CARD, true);

  const char* ts = fmt(temp);
  int w = tw(ts, F_BIG);
  txt(ts, cx - 4, cy - 10, F_BIG, tempColor(temp, C_TEXT), MC_DATUM);
  deg(cx - 4 + w / 2 + 5, cy - 19, 3, accent);
  char b[16];
  snprintf(b, sizeof b, "%s%%", fmt(load));
  txt(b, cx, cy + 17, F_SMALL, accent, MC_DATUM);
  txt("USO", cx, cy + 30, nullptr, C_DIM, MC_DATUM);
  if (!isnan(watts)) {
    snprintf(b, sizeof b, "%.0f W", watts);
    txt(b, cx, cy + 57, F_SMALL, C_DIM, MC_DATUM);
  }
}

static void rings() {
  cv.fillSprite(C_BG);
  header(false);
  char clk[16] = "", gclk[16] = "";
  if (!isnan(ST.v[V_CPU_CLK])) snprintf(clk, sizeof clk, "%.2f GHz", ST.v[V_CPU_CLK] / 1000.0f);
  if (!isnan(ST.v[V_GPU_CLK])) snprintf(gclk, sizeof gclk, "%.0f MHz", ST.v[V_GPU_CLK]);
  ringCard(4, S.cpuLabel, CPU, A[V_CPU_L], ST.v[V_CPU_L], ST.v[V_CPU_T], clk, ST.v[V_CPU_PWR]);
  ringCard(162, S.gpuLabel, GPU, A[V_GPU_L], ST.v[V_GPU_L], ST.v[V_GPU_T], gclk, ST.v[V_GPU_PWR]);

  fsr(4, 191, 312, 46, 9, BORDER, C_BG);
  fsr(5, 192, 310, 44, 8, CARD, BORDER);
  vl(160, 198, 32, BORDER);
  txt("RAM", 14, 198, F_SMALL, RAMC);
  txt(ramStr(), 152, 198, F_SMALL, C_TEXT, TR_DATUM);
  bar(14, 217, 138, 9, A[V_RAM_L], A[V_RAM_L] >= 90 ? C_HOT : RAMC, C_TRACK, CARD);
  txt("VRAM", 170, 198, F_SMALL, VRAMC);
  txt(vramStr(), 306, 198, F_SMALL, C_TEXT, TR_DATUM);
  bar(170, 217, 136, 9, vramPctA(), vramPctA() >= 90 ? C_HOT : VRAMC, C_TRACK, CARD);
}

// ---------------- 8: GRAPH ----------------
static void graphCard(int x, const char* title, uint16_t accent, uint16_t dark, float temp, float load,
                      const Hist& h, const char* topRight) {
  const int y = 27;
  cardBase(x, y, CW, 116, title, accent);
  if (topRight[0]) txt(topRight, x + CW - 10, y + 7, nullptr, C_DIM, TR_DATUM);
  const char* ts = fmt(temp);
  txt(ts, x + 10, y + 26, F_BIG, tempColor(temp, C_TEXT));
  int w = tw(ts, F_BIG);
  deg(x + 15 + w, y + 29, 3, accent);
  txt("C", x + 21 + w, y + 26, F_SMALL, accent);
  char b[16];
  snprintf(b, sizeof b, "%s%%", fmt(load));
  txt(b, x + CW - 10, y + 32, F_SMALL, C_TEXT, TR_DATUM);
  txt("USO", x + CW - 10, y + 46, nullptr, C_DIM, TR_DATUM);
  fr(x + 8, y + 62, CW - 16, 46, rgb565(12, 15, 26));
  for (int i = 1; i < 4; i++) hl(x + 8, y + 62 + i * 46 / 4, CW - 16, rgb565(22, 26, 42));
  area(x + 8, y + 62, CW - 16, 46, h, 20, 100, accent, dark);
}

static void tile(int x, const char* title, uint16_t accent, float pA, const char* center, const char* caption) {
  const int y = 147, w = 75, h = 89;
  fsr(x, y, w, h, 8, BORDER, C_BG);
  fsr(x + 1, y + 1, w - 2, h - 2, 7, CARD, BORDER);
  fitTxt(title, x + 8, y + 6, w - 12, accent);
  int cx = x + w / 2, cy = y + 47;
  arc(cx, cy, 22, 17, 45, 315, C_TRACK, CARD, true);
  if (!isnan(pA) && pA > 1) arc(cx, cy, 22, 17, 45, 45 + (int)(270 * clampPct(pA) / 100), pA >= 90 ? C_HOT : accent, CARD, true);
  if (tw(center, F_SMALL) <= 34) txt(center, cx, cy, F_SMALL, C_TEXT, MC_DATUM);
  else txt(center, cx, cy, nullptr, C_TEXT, MC_DATUM);
  txt(caption, cx, y + 79, nullptr, C_DIM, MC_DATUM);
}

static void graph() {
  cv.fillSprite(C_BG);
  header(true);
  char clk[16] = "", gclk[16] = "";
  if (!isnan(ST.v[V_CPU_CLK])) snprintf(clk, sizeof clk, "%.2fGHz", ST.v[V_CPU_CLK] / 1000.0f);
  if (!isnan(ST.v[V_GPU_CLK])) snprintf(gclk, sizeof gclk, "%.0fMHz", ST.v[V_GPU_CLK]);
  graphCard(4, S.cpuLabel, CPU, CPU_DK, ST.v[V_CPU_T], ST.v[V_CPU_L], hCpuT, clk);
  graphCard(162, S.gpuLabel, GPU, GPU_DK, ST.v[V_GPU_T], ST.v[V_GPU_L], hGpuT, gclk);

  char c1[12], c2[12];
  snprintf(c1, sizeof c1, "%s%%", fmt(ST.v[V_RAM_L]));
  tile(4, "RAM", RAMC, A[V_RAM_L], c1, ramStr());
  snprintf(c2, sizeof c2, "%s%%", fmt(vramPct()));
  tile(83, "VRAM", VRAMC, vramPctA(), c2, vramStr());

  Metric m[2];
  int n = collectMetrics(m, 2);
  const uint16_t cols[2] = {CPU, GPU};
  for (int i = 0; i < 2; i++) {
    int x = 162 + i * 79;
    if (i < n) {
      tile(x, m[i].label, cols[i], m[i].pct, m[i].val, m[i].unit);
    } else {
      tile(x, "EXTRA", cols[i], NAN, "--", "");
    }
  }
}
}  // namespace pxd
