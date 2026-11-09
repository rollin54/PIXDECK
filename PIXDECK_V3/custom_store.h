#pragma once
#include <Arduino.h>
#include <LittleFS.h>
#include "config.h"
#include "settings.h"

// ======================================================================================
// Temas subidos desde la PC (archivos .pxtheme convertidos a lineas de "widgets").
// Se guardan en la flash (LittleFS) como /ct<slot>.txt:
//   linea 1:   <hash>|<nombre>
//   resto:     un widget por linea (ver theme_custom.h)
// Indices de tema: 0..NATIVE_COUNT-1 = temas propios; NATIVE_COUNT + slot = temas subidos.
// ======================================================================================

#define CUSTOM_MAX         8
#define PREVIEW_SLOT       CUSTOM_MAX   // ranura temporal del creador de temas (no se lista ni rota)

static bool g_previewOn = false;
static int  g_previewPrevTheme = 0;
#define CUSTOM_MAX_WIDGETS 96
#define CUSTOM_MAX_BYTES   12000

static bool     g_fsOk = false;
static uint8_t  g_customMask = 0;
static uint32_t g_customHash[CUSTOM_MAX];
static char     g_customName[CUSTOM_MAX][17];
static int      g_customLoaded = -1;      // slot cargado en RAM (theme_custom.h); -1 = ninguno

static String ctPath(int slot) { return String("/ct") + slot + ".txt"; }

static void customScan() {
  g_customMask = 0;
  for (int s = 0; s < CUSTOM_MAX; s++) {
    g_customHash[s] = 0;
    g_customName[s][0] = 0;
    if (!g_fsOk) continue;
    File f = LittleFS.open(ctPath(s), "r");
    if (!f) continue;
    String first = f.readStringUntil('\n');
    f.close();
    int bar = first.indexOf('|');
    if (bar < 0) continue;
    g_customHash[s] = strtoul(first.substring(0, bar).c_str(), nullptr, 10);
    copyStr(g_customName[s], sizeof g_customName[s], first.substring(bar + 1).c_str());
    g_customMask |= (1 << s);
  }
}

static void customInit() {
  g_fsOk = LittleFS.begin(true);   // formatea la particion la primera vez
  customScan();
}

static bool themeValid(int t) {
  if (t < 0) return false;
  if (t < NATIVE_COUNT) return true;
  int s = t - NATIVE_COUNT;
  if (s == PREVIEW_SLOT) return g_previewOn;
  return s < CUSTOM_MAX && (g_customMask & (1 << s));
}

static int themeNext(int t) {
  const int total = NATIVE_COUNT + CUSTOM_MAX;
  if (t >= total) t = 0;   // desde la vista previa vuelve al principio
  for (int i = 1; i <= total; i++) {
    int n = (t + i) % total;
    if (themeValid(n)) return n;
  }
  return 0;
}

static const char* themeName(int t);   // definida en screens.h

// ---------------- subida (comandos U|...) ----------------
static int      s_upSlot = -1;
static uint32_t s_upHash = 0;
static char     s_upName[17];
static String   s_upBuf;
static int      s_upLines = 0;

static void customBegin(int slot, uint32_t hash, const char* name) {
  s_upSlot = slot;
  s_upHash = hash;
  copyStr(s_upName, sizeof s_upName, name);
  s_upBuf = "";
  s_upBuf.reserve(2048);
  s_upLines = 0;
}

static bool customAddLine(int slot, const char* line) {
  if (slot != s_upSlot || s_upLines >= CUSTOM_MAX_WIDGETS) return false;
  if (s_upBuf.length() + strlen(line) + 1 > CUSTOM_MAX_BYTES) return false;
  s_upBuf += line;
  s_upBuf += '\n';
  s_upLines++;
  return true;
}

static bool customEnd(int slot) {
  if (slot != s_upSlot || !g_fsOk) return false;
  File f = LittleFS.open(ctPath(slot), "w");
  if (!f) return false;
  f.printf("%lu|%s\n", (unsigned long)s_upHash, s_upName);
  f.print(s_upBuf);
  f.close();
  s_upBuf = "";
  s_upSlot = -1;
  if (slot < CUSTOM_MAX) customScan();
  if (g_customLoaded == slot) g_customLoaded = -1;   // forzar recarga
  return true;
}

static void customDelete(int slot) {
  if (g_fsOk) LittleFS.remove(ctPath(slot));
  customScan();
  if (g_customLoaded == slot) g_customLoaded = -1;
}
