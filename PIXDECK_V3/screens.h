#pragma once
#include "gfx.h"
#include "theme_electronica.h"
#include "theme_informatica.h"
#include "theme_autos.h"
#include "theme_anime.h"
#include "theme_peliculas.h"
#include "theme_pixdeck.h"
#include "theme_custom.h"

typedef void (*DrawFn)();
static const DrawFn THEMES[NATIVE_COUNT] = {
  el::draw, inf::draw, au::draw, an::draw, pe::draw, pxd::classic, pxd::rings, pxd::graph };
static const char* const THEME_NAMES[NATIVE_COUNT] = {
  "ELECTRONICA", "INFORMATICA", "AUTOS", "ANIME", "PELICULAS", "PIXDECK", "PIXDECK RINGS", "PIXDECK GRAPH" };

static const char* themeName(int t) {
  if (t >= 0 && t < NATIVE_COUNT) return THEME_NAMES[t];
  int s = t - NATIVE_COUNT;
  if (s == PREVIEW_SLOT) return "VISTA PREVIA";
  if (s >= 0 && s < CUSTOM_MAX && g_customName[s][0]) return g_customName[s];
  return "?";
}
static void drawCurrentTheme() {
  if (S.theme < NATIVE_COUNT) THEMES[S.theme]();
  else ct::draw();
}

// ---------------- estado de overlays ----------------
static int      g_alertSrc = -1;     // 0 = CPU, 1 = GPU
static int      g_alertTemp = 0;
static uint32_t g_alertUntil = 0;
static uint32_t g_toastUntil = 0;
static char     g_toast[24] = "";

static void showToast(const char* t, uint32_t ms = 1400) {
  copyStr(g_toast, sizeof g_toast, t);
  g_toastUntil = NOW + ms;
}
static void raiseAlert(int src, int temp) {
  g_alertSrc = src;
  g_alertTemp = temp;
  g_alertUntil = millis() + 6000;
}

static void overlays() {
  bool alertOn = g_alertSrc >= 0 && (int32_t)(NOW - g_alertUntil) < 0;
  if (alertOn && ((NOW / 400) & 1)) {
    for (int i = 0; i < 3; i++) dr(i, i, 320 - 2 * i, 240 - 2 * i, C_HOT);
    frr(60, 200, 200, 30, 6, C_HOT);
    char m[32];
    snprintf(m, sizeof m, "ALERTA %s %d C", g_alertSrc == 0 ? "CPU" : "GPU", g_alertTemp);
    txt(m, 160, 215, F_SMALL, C_TEXT, MC_DATUM);
  }
  if ((int32_t)(NOW - g_toastUntil) < 0) {
    fsr(50, 98, 220, 44, 10, rgb565(10, 12, 24), rgb565(10, 12, 24));
    drr(50, 98, 220, 44, 10, C_VIOLET);
    txt(g_toast, 160, 121, F_SANS, C_TEXT, MC_DATUM);
  }
}

// ---------------- render por franjas ----------------
static void renderScreen(DrawFn fn, bool withOverlays) {
  for (OY = 0; OY < 240; OY += BAND) {
    fn();
    if (withOverlays) overlays();
    cv.pushSprite(0, OY);
  }
  OY = 0;
}

// ---------------- pantalla de carga (estilo PIXDECK_V2) ----------------
static float g_loadPct = 0;
static const char* g_loadMsg = "INICIANDO";

static void drawLoading() {
  cv.fillSprite(C_BG);
  title(S.name, 160, 96, 300, C_VIOLET, MC_DATUM);
  txt("L O A D I N G", 160, 140, F_SMALL, C_DIM, MC_DATUM);
  bar(40, 188, 240, 12, g_loadPct, C_VIOLET, C_TRACK, C_BG);
  txt(g_loadMsg, 160, 218, F_SMALL, C_DIM, MC_DATUM);
  txt("v" FW_VERSION " USB", 316, 4, nullptr, C_TRACK, TR_DATUM);
}

// ---------------- esperando a la PC ----------------
static void drawWaiting() {
  cv.fillSprite(C_BG);
  title(S.name, 160, 70, 300, C_VIOLET, MC_DATUM);

  int a = (int)((NOW / 4) % 360);        // spinner
  arc(160, 130, 22, 17, 0, 360, C_TRACK, C_BG);
  int a1 = a + 90;
  if (a1 <= 360) arc(160, 130, 22, 17, a, a1, C_VIOLET, C_TRACK, true);
  else { arc(160, 130, 22, 17, a, 360, C_VIOLET, C_TRACK); arc(160, 130, 22, 17, 0, a1 - 360, C_VIOLET, C_TRACK); }

  char m[24] = "ESPERANDO PC";
  int dots = (NOW / 400) % 4;
  for (int i = 0; i < dots; i++) strcat(m, ".");
  txt(m, 160, 178, F_SMALL, C_TEXT, MC_DATUM);
  txt("Abre PixDeck Hub en la PC o conecta el cable USB", 160, 198, nullptr, C_DIM, MC_DATUM);

  char t[56];
  snprintf(t, sizeof t, "TEMA: %s   BOOT = CAMBIAR", themeName(S.theme));
  txt(t, 160, 226, nullptr, C_TRACK, MC_DATUM);
}
