#pragma once
#include "gfx.h"
#include "custom_store.h"

// ======================================================================================
// Interprete de temas subidos. Cada linea es un widget: TIPO,num,num,...[,texto]
//   B,color                                         fondo
//   R,x,y,w,h,radio,color                           rectangulo relleno
//   O,x,y,w,h,radio,color                           rectangulo (borde)
//   C,x,y,r,color,relleno(0/1)                      circulo
//   L,x,y,x2,y2,grosor,color                        linea
//   T,x,y,fuente,tam,alin,color,texto               texto ({name} {cpu} {gpu} {time} {hm} {status})
//   V,x,y,fuente,tam,alin,color,dato,dec,colorTemp,sufijo     valor de un sensor
//   A,x,y,r,grosor,ang0,ang1,dato,max,color,pista,fondo       arco (0 = abajo, horario)
//   G,x,y,w,h,dato,min,max,color                    grafica del historial (cpu/gpu temp/uso)
//   P,x,y,w,h,dato,max,color,pista,fondo            barra de progreso
// Colores en RGB565 (decimal). fuente: 0 GLCD, 1-6 nombres viejos (Sans/Mono/Serif negrita),
//   10-57 las 48 FreeFonts (ver FREEFONTS), 60-64 fuentes numeradas 2/4/6/7/8,
//   70 Horyzen, 71 Techno. "tam" = multiplicador (el Hub elige punto + multiplicador segun la escala).
// alin: 0 TL 1 TC 2 TR 3 ML 4 MC 5 MR 6 BL 7 BC 8 BR. dato: indices V_* de link.h, 17 = VRAM %, 18/19 = VRAM usada/total en GB.
// ======================================================================================

namespace ct {
struct Widget {
  char    t;
  int32_t n[11];
  char    text[32];
};
static Widget  W[CUSTOM_MAX_WIDGETS];
static int     WN = 0;
static uint16_t BGC = C_BG;

static int argCount(char t) {
  switch (t) {
    case 'B': return 1;
    case 'R': case 'O': case 'L': case 'T': return 6;
    case 'C': return 5;
    case 'V': return 9;
    case 'A': return 11;
    case 'G': return 8;
    case 'P': return 9;
  }
  return -1;
}

static bool parse(const char* line, Widget& w) {
  int cnt = argCount(line[0]);
  if (cnt < 0 || line[1] != ',') return false;
  memset(&w, 0, sizeof w);
  w.t = line[0];
  const char* p = line + 2;
  for (int i = 0; i < cnt; i++) {
    char* end;
    w.n[i] = strtol(p, &end, 10);
    p = (*end == ',') ? end + 1 : end;
  }
  if (w.t == 'T' || w.t == 'V') copyStr(w.text, sizeof w.text, p);
  return true;
}

static void load(int slot) {
  WN = 0;
  BGC = C_BG;
  g_customLoaded = slot;
  if (!g_fsOk) return;
  File f = LittleFS.open(ctPath(slot), "r");
  if (!f) return;
  f.readStringUntil('\n');   // hash|nombre
  while (f.available() && WN < CUSTOM_MAX_WIDGETS) {
    String l = f.readStringUntil('\n');
    l.trim();
    if (l.length() && parse(l.c_str(), W[WN])) {
      if (W[WN].t == 'B') BGC = (uint16_t)W[WN].n[0];
      else WN++;
    }
  }
  f.close();
}

static float src(int s, bool anim) {
  if (s >= 0 && s < V_COUNT) return (anim && !isnan(A[s])) ? A[s] : ST.v[s];
  if (s == 17) return anim ? vramPctA() : vramPct();
  if (s == 18 || s == 19) {   // VRAM usada / total en GB (llega en MB)
    float mb = s == 18 ? ((anim && !isnan(A[V_VRAM_USED])) ? A[V_VRAM_USED] : ST.v[V_VRAM_USED]) : ST.v[V_VRAM_TOT];
    return isnan(mb) ? NAN : mb / 1024.0f;
  }
  return NAN;
}
static const Hist* hist(int s) {
  switch (s) {
    case V_CPU_T: return &hCpuT;
    case V_CPU_L: return &hCpuL;
    case V_GPU_T: return &hGpuT;
    case V_GPU_L: return &hGpuL;
  }
  return nullptr;
}
// Las 48 FreeFonts de TFT_eSPI. Codigo de fuente = 10 + familia*16 + estilo*4 + tamano
//   familia: 0 Sans, 1 Serif, 2 Mono   estilo: 0 normal, 1 negrita, 2 cursiva, 3 negrita cursiva
//   tamano: 0 = 9pt, 1 = 12pt, 2 = 18pt, 3 = 24pt
static const GFXfont* const FREEFONTS[48] = {
  &FreeSans9pt7b, &FreeSans12pt7b, &FreeSans18pt7b, &FreeSans24pt7b,
  &FreeSansBold9pt7b, &FreeSansBold12pt7b, &FreeSansBold18pt7b, &FreeSansBold24pt7b,
  &FreeSansOblique9pt7b, &FreeSansOblique12pt7b, &FreeSansOblique18pt7b, &FreeSansOblique24pt7b,
  &FreeSansBoldOblique9pt7b, &FreeSansBoldOblique12pt7b, &FreeSansBoldOblique18pt7b, &FreeSansBoldOblique24pt7b,
  &FreeSerif9pt7b, &FreeSerif12pt7b, &FreeSerif18pt7b, &FreeSerif24pt7b,
  &FreeSerifBold9pt7b, &FreeSerifBold12pt7b, &FreeSerifBold18pt7b, &FreeSerifBold24pt7b,
  &FreeSerifItalic9pt7b, &FreeSerifItalic12pt7b, &FreeSerifItalic18pt7b, &FreeSerifItalic24pt7b,
  &FreeSerifBoldItalic9pt7b, &FreeSerifBoldItalic12pt7b, &FreeSerifBoldItalic18pt7b, &FreeSerifBoldItalic24pt7b,
  &FreeMono9pt7b, &FreeMono12pt7b, &FreeMono18pt7b, &FreeMono24pt7b,
  &FreeMonoBold9pt7b, &FreeMonoBold12pt7b, &FreeMonoBold18pt7b, &FreeMonoBold24pt7b,
  &FreeMonoOblique9pt7b, &FreeMonoOblique12pt7b, &FreeMonoOblique18pt7b, &FreeMonoOblique24pt7b,
  &FreeMonoBoldOblique9pt7b, &FreeMonoBoldOblique12pt7b, &FreeMonoBoldOblique18pt7b, &FreeMonoBoldOblique24pt7b,
};

// Fuentes numeradas de TFT_eSPI (codigos 60..64): 2 = 16px, 4 = 26px, 6 = 48px, 7 = 7 segmentos, 8 = 75px.
// 6, 7 y 8 solo tienen numeros y  : - .
static int numFont(int f) {
  static const int8_t map[5] = {2, 4, 6, 7, 8};
  return (f >= 60 && f <= 64) ? map[f - 60] : 0;
}

static const GFXfont* font(int f);

static void drawText(const char* s, int x, int y, int f, uint16_t col, uint8_t datum, int size) {
  int nf = numFont(f);
  if (!nf) { txt(s, x, y, font(f), col, datum, size); return; }
  if (y + 100 < OY || y - 100 > OY + BAND) return;
  cv.setTextFont(nf);
  cv.setTextSize(size);
  cv.setTextDatum(datum);
  cv.setTextColor(col);
  cv.drawString(s, x, Y(y));
}

static const GFXfont* font(int f) {
  if (f >= 10 && f < 58) return FREEFONTS[f - 10];
  switch (f) {
    case 70: return F_HORYZEN;
    case 71: return F_TITLE;
    case 1: return F_SMALL;
    case 2: return F_BIG;
    case 3: return F_MONO;
    case 4: return F_SERIF;
    case 5: return F_SANS;
    case 6: return F_HUGE;
  }
  return nullptr;
}

static void expand(const char* in, char* out, size_t n) {
  size_t o = 0;
  while (*in && o < n - 1) {
    const char* rep = nullptr;
    int skip = 0;
    if (*in == '{') {
      if (!strncmp(in, "{name}", 6))   { rep = S.name; skip = 6; }
      else if (!strncmp(in, "{cpu}", 5))  { rep = S.cpuLabel; skip = 5; }
      else if (!strncmp(in, "{gpu}", 5))  { rep = S.gpuLabel; skip = 5; }
      else if (!strncmp(in, "{time}", 6)) { rep = clockStr(true); skip = 6; }
      else if (!strncmp(in, "{hm}", 4))   { rep = clockStr(false); skip = 4; }
      else if (!strncmp(in, "{status}", 8)) { rep = linkOnline() ? "ONLINE" : "OFFLINE"; skip = 8; }
    }
    if (rep) {
      while (*rep && o < n - 1) out[o++] = *rep++;
      in += skip;
    } else {
      out[o++] = *in++;
    }
  }
  out[o] = 0;
}

static void draw() {
  int slot = S.theme - NATIVE_COUNT;
  if (g_customLoaded != slot) load(slot);
  cv.fillSprite(BGC);
  if (!WN) {
    txt("TEMA VACIO", 160, 120, F_SMALL, C_DIM, MC_DATUM);
    return;
  }
  char buf[48];
  for (int i = 0; i < WN; i++) {
    const Widget& w = W[i];
    const int32_t* n = w.n;
    switch (w.t) {
      case 'R':
        if (n[4] > 0) fsr(n[0], n[1], n[2], n[3], n[4], n[5], BGC);
        else fr(n[0], n[1], n[2], n[3], n[5]);
        break;
      case 'O':
        if (n[4] > 0) drr(n[0], n[1], n[2], n[3], n[4], n[5]);
        else dr(n[0], n[1], n[2], n[3], n[5]);
        break;
      case 'C':
        if (n[4]) fc(n[0], n[1], n[2], n[3]); else dc(n[0], n[1], n[2], n[3]);
        break;
      case 'L':
        if (n[4] > 1) wl(n[0], n[1], n[2], n[3], n[4], n[5], BGC);
        else ln(n[0], n[1], n[2], n[3], n[5]);
        break;
      case 'T':
        expand(w.text, buf, sizeof buf);
        drawText(buf, n[0], n[1], n[2], n[5], n[4], max(1, (int)n[3]));
        break;
      case 'V': {
        float v = src(n[6], false);
        snprintf(buf, sizeof buf, "%s%s", fmt(v, constrain((int)n[7], 0, 2)), w.text);
        uint16_t col = n[8] ? tempColor(v, n[5]) : (uint16_t)n[5];
        drawText(buf, n[0], n[1], n[2], col, n[4], max(1, (int)n[3]));
        break;
      }
      case 'A': {
        int a0 = constrain((int)n[4], 0, 360), a1 = constrain((int)n[5], 0, 360);
        int thick = max(1, (int)n[3]);
        arc(n[0], n[1], n[2], n[2] - thick, a0, a1, n[9], n[10], true);
        float p = n[7] > 0 ? clampPct(src(n[6], true) * 100.0f / n[7]) : 0;
        int e = a0 + (int)((a1 - a0) * p / 100.0f);
        if (e > a0 + 1) arc(n[0], n[1], n[2], n[2] - thick, a0, e, n[8], n[9], true);
        break;
      }
      case 'G': {
        const Hist* h = hist(n[4]);
        if (h && n[6] > n[5]) spark(n[0], n[1], n[2], n[3], *h, n[5], n[6], n[7]);
        break;
      }
      case 'P': {
        float p = n[5] > 0 ? src(n[4], true) * 100.0f / n[5] : NAN;
        bar(n[0], n[1], n[2], n[3], p, n[6], n[7], n[8]);
        break;
      }
    }
  }
}
}  // namespace ct
