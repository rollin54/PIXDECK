#pragma once
#include <TFT_eSPI.h>
#include <math.h>
#include "config.h"
#include "settings.h"
#include "link.h"

// ======================================================================================
// Motor de dibujo por "franjas": la pantalla 320x240 se dibuja en un sprite de 320xBAND
// (120 filas => 75 KB). Cada tema dibuja en coordenadas de PANTALLA y el motor lo llama
// una vez por franja; Y() traduce a la franja actual. Asi no hay parpadeo y los temas
// pueden ocupar toda la pantalla sin necesitar 150 KB de RAM.
// ======================================================================================

static TFT_eSPI    tft = TFT_eSPI();
static TFT_eSprite cv  = TFT_eSprite(&tft);
static int OY   = 0;     // fila de pantalla donde empieza la franja actual
static int BAND = 120;   // alto de la franja (se reduce si no hay memoria)

static PCStats  ST;      // copia de los datos para el frame actual
static uint32_t NOW = 0; // millis() del frame actual
static float    A[V_COUNT];   // valores animados (persiguen al real)

// Fuentes propias (opcionales). Si los archivos no estan (version publica), se usan FreeFonts parecidas.
#if __has_include("techno_hideo_bold20pt7b.h")
  #include "techno_hideo_bold20pt7b.h"
  #define F_TITLE (&techno_hideo_bold20pt7b)        // titulo (nombre del equipo) y temas subidos
#else
  #define F_TITLE (&FreeSansBoldOblique18pt7b)
#endif
#if __has_include("HoryzenHeadline_Aug20257pt7b.h")
  #include "HoryzenHeadline_Aug20257pt7b.h"
  #define F_HORYZEN (&HoryzenHeadline_Aug20257pt7b)  // solo para temas subidos
#else
  #define F_HORYZEN (&FreeSansBold9pt7b)
#endif

// Fuentes estandar de TFT_eSPI (FreeFonts, requieren LOAD_GFXFF)
#define F_SMALL (&FreeSansBold9pt7b)     // etiquetas
#define F_BIG   (&FreeSansBold18pt7b)    // numeros grandes
#define F_HUGE  (&FreeSansBold24pt7b)    // numeros gigantes
#define F_MONO  (&FreeMonoBold9pt7b)
#define F_SERIF (&FreeSerifBold12pt7b)
#define F_SANS  (&FreeSansBold12pt7b)

static inline int  Y(int y)            { return y - OY; }
static inline bool vis(int y, int h)   { return y < OY + BAND && y + h > OY; }

static bool gfxInit() {
  tft.init();
  tft.setRotation(1);
  tft.fillScreen(TFT_BLACK);
  cv.setColorDepth(16);
  const int opts[] = {120, 80, 60, 40};
  for (int h : opts) {
    if (cv.createSprite(320, h)) { BAND = h; return true; }
  }
  return false;
}

// ---------------- animacion ----------------
static void animStep() {
  for (int i = 0; i < V_COUNT; i++) {
    float t = ST.v[i];
    if (isnan(t)) continue;
    if (isnan(A[i])) { A[i] = t; continue; }
    A[i] += (t - A[i]) * 0.25f;
    if (fabsf(t - A[i]) < 0.2f) A[i] = t;
  }
}
static void animReset() { for (int i = 0; i < V_COUNT; i++) A[i] = NAN; }

// ---------------- utilidades de formato ----------------
static const char* fmt(float v, int dec = 0) {
  static char buf[10][16];
  static uint8_t k = 0;
  char* b = buf[k++ % 10];
  if (isnan(v)) snprintf(b, 16, "--");
  else snprintf(b, 16, "%.*f", dec, v);
  return b;
}
static float pct(float v, float m) {
  if (isnan(v) || isnan(m) || m <= 0) return NAN;
  return constrain(v * 100.0f / m, 0.0f, 100.0f);
}
static float clampPct(float p) { return isnan(p) ? 0 : constrain(p, 0.0f, 100.0f); }
static float vramPct()  { return pct(ST.v[V_VRAM_USED], ST.v[V_VRAM_TOT]); }
static float vramPctA() { return pct(A[V_VRAM_USED], ST.v[V_VRAM_TOT]); }

static uint16_t tempColor(float t, uint16_t ok = C_OK) {
  if (isnan(t)) return C_DIM;
  if (t >= S.alertOn) return C_HOT;
  if (t >= S.warn) return C_WARN;
  return ok;
}

static const char* clockStr(bool secs = true) {
  static char b[12];
  if (!ST.hasTime) return secs ? "--:--:--" : "--:--";
  if (secs) snprintf(b, sizeof b, "%02u:%02u:%02u", ST.hh, ST.mm, ST.ss);
  else snprintf(b, sizeof b, "%02u:%02u", ST.hh, ST.mm);
  return b;
}

// "12.3/32G" a partir de usado/total en GB
static const char* ramStr() {
  static char b[20];
  float u = ST.v[V_RAM_USED], t = ST.v[V_RAM_TOT];
  if (isnan(u)) snprintf(b, sizeof b, "--");
  else if (isnan(t)) snprintf(b, sizeof b, "%.1fG", u);
  else snprintf(b, sizeof b, "%.1f/%.0fG", u, t);
  return b;
}
// VRAM llega en MB; se muestra en GB
static const char* vramStr() {
  static char b[20];
  float u = ST.v[V_VRAM_USED], t = ST.v[V_VRAM_TOT];
  if (isnan(u)) snprintf(b, sizeof b, "--");
  else if (isnan(t)) snprintf(b, sizeof b, "%.1fG", u / 1024.0f);
  else snprintf(b, sizeof b, "%.1f/%.0fG", u / 1024.0f, t / 1024.0f);
  return b;
}
static const char* ghzStr(float mhz) {
  static char b[2][12];
  static uint8_t k = 0;
  char* o = b[k++ & 1];
  if (isnan(mhz)) snprintf(o, 12, "-- GHz");
  else snprintf(o, 12, "%.2f GHz", mhz / 1000.0f);
  return o;
}

// Lista de metricas "secundarias": extras configurados + potencias + reloj GPU.
struct Metric { char label[12]; char val[12]; char unit[6]; float pct; };
static int collectMetrics(Metric* out, int maxn) {
  int n = 0;
  for (int i = 0; i < 4 && n < maxn; i++) {
    if (!S.extra[i].label[0]) continue;
    Metric& m = out[n++];
    float v = ST.v[V_X0 + i];
    copyStr(m.label, sizeof m.label, S.extra[i].label);
    copyStr(m.unit, sizeof m.unit, S.extra[i].unit);
    snprintf(m.val, sizeof m.val, "%s", fmt(v, (!isnan(v) && fabsf(v) < 10) ? 1 : 0));
    m.pct = pct(v, S.extra[i].maxv);
  }
  struct { const char* l; int idx; const char* u; } base[] = {
    {"CPU PWR", V_CPU_PWR, "W"}, {"GPU PWR", V_GPU_PWR, "W"}, {"GPU CLK", V_GPU_CLK, "MHz"}};
  for (auto& b : base) {
    if (n >= maxn) break;
    if (isnan(ST.v[b.idx])) continue;
    Metric& m = out[n++];
    copyStr(m.label, sizeof m.label, b.l);
    copyStr(m.unit, sizeof m.unit, b.u);
    snprintf(m.val, sizeof m.val, "%s", fmt(ST.v[b.idx]));
    m.pct = NAN;
  }
  return n;
}

// ---------------- primitivas en coordenadas de pantalla ----------------
static void txt(const char* s, int x, int y, const GFXfont* f, uint16_t col,
                uint8_t datum = TL_DATUM, uint8_t size = 1) {
  if (y + 70 < OY || y - 70 > OY + BAND) return;
  if (f) cv.setFreeFont(f); else cv.setTextFont(1);
  cv.setTextSize(size);
  cv.setTextDatum(datum);
  cv.setTextColor(col);
  cv.drawString(s, x, Y(y));
}
static int tw(const char* s, const GFXfont* f, uint8_t size = 1) {
  if (f) cv.setFreeFont(f); else cv.setTextFont(1);
  cv.setTextSize(size);
  return cv.textWidth(s);
}
// Titulo (nombre del equipo) con la fuente Techno; si no entra, usa una mas chica.
static void title(const char* s, int x, int y, int maxW, uint16_t col, uint8_t datum) {
  if (tw(s, F_TITLE) <= maxW) txt(s, x, y, F_TITLE, col, datum);
  else if (tw(s, F_BIG) <= maxW) txt(s, x, y, F_BIG, col, datum);
  else txt(s, x, y, F_SMALL, col, datum);
}

// Texto que pasa a la fuente chica (GLCD 6px) si no entra en maxW, y se recorta si aun asi no entra.
static void fitTxt(const char* s, int x, int y, int maxW, uint16_t col, uint8_t datum = TL_DATUM,
                   const GFXfont* f = F_SMALL) {
  if (tw(s, f) <= maxW) { txt(s, x, y, f, col, datum); return; }
  char b[24];
  copyStr(b, sizeof b, s);
  int maxc = max(1, maxW / 6);
  if ((int)strlen(b) > maxc) b[maxc] = 0;
  bool middle = datum == MC_DATUM || datum == ML_DATUM || datum == MR_DATUM;
  txt(b, x, middle ? y : y + 2, nullptr, col, datum);
}
static void fr(int x, int y, int w, int h, uint16_t c)  { if (vis(y, h)) cv.fillRect(x, Y(y), w, h, c); }
static void dr(int x, int y, int w, int h, uint16_t c)  { if (vis(y, h)) cv.drawRect(x, Y(y), w, h, c); }
static void frr(int x, int y, int w, int h, int r, uint16_t c) { if (vis(y, h)) cv.fillRoundRect(x, Y(y), w, h, r, c); }
static void drr(int x, int y, int w, int h, int r, uint16_t c) { if (vis(y, h)) cv.drawRoundRect(x, Y(y), w, h, r, c); }
static void fsr(int x, int y, int w, int h, int r, uint16_t c, uint16_t bg) {
  if (vis(y, h) && w > 0 && h > 0) cv.fillSmoothRoundRect(x, Y(y), w, h, r, c, bg);
}
static void hl(int x, int y, int w, uint16_t c) { if (vis(y, 1)) cv.drawFastHLine(x, Y(y), w, c); }
static void vl(int x, int y, int h, uint16_t c) { if (vis(y, h)) cv.drawFastVLine(x, Y(y), h, c); }
static void ln(int x0, int y0, int x1, int y1, uint16_t c) {
  if (vis(min(y0, y1), abs(y1 - y0) + 1)) cv.drawLine(x0, Y(y0), x1, Y(y1), c);
}
static void wl(float x0, float y0, float x1, float y1, float w, uint16_t c, uint16_t bg) {
  int ya = (int)fminf(y0, y1) - (int)w, h = (int)fabsf(y1 - y0) + 2 * (int)w + 1;
  if (vis(ya, h)) cv.drawWideLine(x0, y0 - OY, x1, y1 - OY, w, c, bg);
}
static void fc(int x, int y, int r, uint16_t c) { if (vis(y - r, 2 * r + 1)) cv.fillCircle(x, Y(y), r, c); }
static void dc(int x, int y, int r, uint16_t c) { if (vis(y - r, 2 * r + 1)) cv.drawCircle(x, Y(y), r, c); }
static void fsc(int x, int y, int r, uint16_t c, uint16_t bg) {
  if (vis(y - r, 2 * r + 1)) cv.fillSmoothCircle(x, Y(y), r, c, bg);
}
static void fe(int x, int y, int rx, int ry, uint16_t c) { if (vis(y - ry, 2 * ry + 1)) cv.fillEllipse(x, Y(y), rx, ry, c); }
static void ftri(int x0, int y0, int x1, int y1, int x2, int y2, uint16_t c) {
  int a = min(y0, min(y1, y2)), b = max(y0, max(y1, y2));
  if (vis(a, b - a + 1)) cv.fillTriangle(x0, Y(y0), x1, Y(y1), x2, Y(y2), c);
}
// Arco suave. Angulos de TFT_eSPI: 0 = abajo, sentido horario.
static void arc(int cx, int cy, int r, int ir, int a0, int a1, uint16_t c, uint16_t bg, bool round = false) {
  if (a1 <= a0 || !vis(cy - r, 2 * r + 1)) return;
  cv.drawSmoothArc(cx, Y(cy), r, ir, a0, a1, c, bg, round);
}
// Punto sobre un circulo con el mismo sistema de angulos que arc()
static inline float px(float cx, float r, float a) { return cx - sinf(a * DEG_TO_RAD) * r; }
static inline float py(float cy, float r, float a) { return cy + cosf(a * DEG_TO_RAD) * r; }

// Paralelogramo inclinado (estilo anime): la parte de arriba se corre "sk" px a la derecha
static void para(int x, int y, int w, int h, int sk, uint16_t c) {
  ftri(x + sk, y, x + w + sk, y, x, y + h, c);
  ftri(x + w + sk, y, x + w, y + h, x, y + h, c);
}
static void paraOutline(int x, int y, int w, int h, int sk, uint16_t c) {
  ln(x + sk, y, x + w + sk, y, c);
  ln(x + w + sk, y, x + w, y + h, c);
  ln(x + w, y + h, x, y + h, c);
  ln(x, y + h, x + sk, y, c);
}

static void bar(int x, int y, int w, int h, float p, uint16_t col, uint16_t track, uint16_t bg) {
  if (!vis(y, h)) return;
  int r = h / 2;
  cv.fillSmoothRoundRect(x, Y(y), w, h, r, track, bg);
  int fw = (int)(w * clampPct(p) / 100.0f);
  if (fw > 0) {
    if (fw < h) fw = h;
    cv.fillSmoothRoundRect(x, Y(y), fw, h, r, col, track);
  }
}

// Simbolo de grados (las fuentes no lo traen)
static void deg(int x, int y, int r, uint16_t c) { dc(x, y, r, c); if (r > 2) dc(x, y, r - 1, c); }

// Grafica de area (relleno bajo la curva) con el historial
static void area(int x, int y, int w, int h, const Hist& hs, float lo, float hi, uint16_t line, uint16_t fill) {
  if (!vis(y, h) || hs.count < 2) return;
  int n = hs.count;
  float step = (float)w / (Hist::N - 1);
  int x0 = x + w - (int)((n - 1) * step);
  int pxv = 0, pyv = 0;
  for (int i = 0; i < n; i++) {
    float v = constrain(hs.get(i), lo, hi);
    int xx = x0 + (int)(i * step);
    int yy = y + h - 1 - (int)((v - lo) * (h - 1) / (hi - lo));
    if (i) {
      for (int k = pxv; k <= xx; k++) {
        int ky = pyv + (yy - pyv) * (k - pxv) / max(1, xx - pxv);
        vl(k, ky, y + h - ky, fill);
      }
      ln(pxv, pyv, xx, yy, line);
      ln(pxv, pyv - 1, xx, yy - 1, line);
    }
    pxv = xx; pyv = yy;
  }
}

// Grafica de linea con el historial (lo..hi = rango vertical)
static void spark(int x, int y, int w, int h, const Hist& hs, float lo, float hi, uint16_t c) {
  if (!vis(y, h) || hs.count < 2) return;
  int n = hs.count;
  float step = (float)w / (Hist::N - 1);
  int x0 = x + w - (int)((n - 1) * step);
  int pxv = 0, pyv = 0;
  for (int i = 0; i < n; i++) {
    float v = constrain(hs.get(i), lo, hi);
    int xx = x0 + (int)(i * step);
    int yy = y + h - 1 - (int)((v - lo) * (h - 1) / (hi - lo));
    if (i) { ln(pxv, pyv, xx, yy, c); ln(pxv, pyv + 1, xx, yy + 1, c); }
    pxv = xx; pyv = yy;
  }
}
