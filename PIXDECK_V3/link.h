#pragma once
#include <Arduino.h>
#include <math.h>
#include "config.h"
#include "settings.h"
#include "pixel_sounds.h"
#include "custom_store.h"

// ================= PROTOCOLO USB (texto, una linea por mensaje, campos con '|') =================
// PC -> ESP32
//   ?                                      -> responde PIXDECK|HELLO|<ver>|<nombre>|<tema>
//   D|HHMMSS|v0|v1|...|v16                 datos (vacio o "-" = sin dato), ver indices abajo
//   N|nombre                               nombre del equipo
//   L|etiquetaCPU|etiquetaGPU
//   X|i|etiqueta|unidad|maximo             dato extra i (0..3); etiqueta vacia = desactivado
//   S|tema|rotarSeg|alertaOn|alertaOff|ambar|sonido   (sonido = 16 + bits: 1 encendido, 2 conexion, 4 tema, 8 alarma)
//   T|tema                                 cambia de tema ya
//   B                                      prueba del buzzer
//   Z|enc|con|tema|alarma|repetirSeg       melodia elegida para cada evento (0 = la primera)
//   Y|evento|melodia                       toca una melodia ya (0 enc, 1 con, 2 tema, 3 alarma)
//   Q                                      lista los temas subidos -> CT|slot|hash|nombre ... CT|END
//   U|slot|BEGIN|hash|nombre               empieza a subir un tema al slot (0..7)
//   U|slot|W|<widget>                      una linea de widget (ver theme_custom.h)
//   U|slot|END                             guarda el tema en la flash
//   U|slot|DEL                             borra el tema del slot
// ESP32 -> PC
//   PIXDECK|HELLO|...   OK|<cmd>   ERR|<cmd>   EVT|THEME|n (se cambio el tema con el boton BOOT)

enum {
  V_CPU_T, V_CPU_L, V_CPU_CLK, V_CPU_PWR,
  V_GPU_T, V_GPU_L, V_GPU_CLK, V_GPU_PWR,
  V_RAM_USED, V_RAM_TOT, V_RAM_L,
  V_VRAM_USED, V_VRAM_TOT,
  V_X0, V_X1, V_X2, V_X3,
  V_COUNT
};

struct PCStats {
  float    v[V_COUNT];
  uint32_t lastOkMs = 0;
  uint8_t  hh = 0, mm = 0, ss = 0;
  bool     hasTime = false;
  PCStats() { for (int i = 0; i < V_COUNT; i++) v[i] = NAN; }
};

// Historial de 64 muestras (una por paquete de datos) para graficas.
struct Hist {
  static constexpr int N = 64;
  float d[N];
  int head = 0, count = 0;
  Hist() { for (int i = 0; i < N; i++) d[i] = 0; }
  void push(float x) {
    if (isnan(x)) x = count ? d[(head + N - 1) % N] : 0;
    d[head] = x;
    head = (head + 1) % N;
    if (count < N) count++;
  }
  float get(int i) const { return d[(head - count + i + N * 2) % N]; }   // 0 = mas viejo
};

static PCStats g_st;
static Hist hCpuT, hGpuT, hCpuL, hGpuL;
static bool g_themeFromPC = false;    // el tema cambio por orden de la PC
static uint32_t g_packets = 0;

static char s_rx[512];
static int  s_rxn = 0;

static int splitFields(char* s, char** f, int maxf) {
  int n = 0;
  f[n++] = s;
  for (char* p = s; *p && n < maxf; p++) {
    if (*p == '|') { *p = 0; f[n++] = p + 1; }
  }
  return n;
}

static float pf(const char* s) {
  if (!s || !*s || (s[0] == '-' && s[1] == 0)) return NAN;
  return atof(s);
}

static void sendHello() {
  Serial.printf("PIXDECK|HELLO|%s|%s|%u\n", FW_VERSION, S.name, S.theme);
}

static void handleLine(char* line) {
  char* f[24];
  int n = splitFields(line, f, 24);
  const char* cmd = f[0];

  if (!strcmp(cmd, "?")) { sendHello(); return; }

  // Subida de temas: U|slot|BEGIN|hash|nombre  U|slot|W|widget  U|slot|END  U|slot|DEL
  if (!strcmp(cmd, "U") && n >= 3) {
    int slot = atoi(f[1]);
    if (slot < 0 || slot > PREVIEW_SLOT) return;
    const char* op = f[2];
    if (!strcmp(op, "W")) {
      // el widget puede traer '|' ya separados: se vuelven a unir
      char w[256] = "";
      for (int i = 3; i < n; i++) {
        if (i > 3) strncat(w, "|", sizeof w - strlen(w) - 1);
        strncat(w, f[i], sizeof w - strlen(w) - 1);
      }
      if (!customAddLine(slot, w)) Serial.printf("ERR|U|%d\n", slot);
      return;   // sin OK por linea para no saturar
    }
    bool ok = true;
    if (!strcmp(op, "BEGIN") && n >= 5) customBegin(slot, strtoul(f[3], nullptr, 10), f[4]);
    else if (!strcmp(op, "END")) ok = customEnd(slot);
    else if (!strcmp(op, "DEL")) {
      customDelete(slot);
      if (S.theme == NATIVE_COUNT + slot) { S.theme = 0; settingsMarkDirty(); g_themeFromPC = true; }
    } else ok = false;
    Serial.printf("%s|U|%d|%s\n", ok ? "OK" : "ERR", slot, op);
    return;
  }
  if (cmd[0] == 0 || cmd[1] != 0) return;   // comandos de una letra; el resto se ignora

  switch (cmd[0]) {
    case 'D': {
      if (n < 2) return;
      const char* t = f[1];
      if (strlen(t) == 6) {
        g_st.hh = (t[0] - '0') * 10 + (t[1] - '0');
        g_st.mm = (t[2] - '0') * 10 + (t[3] - '0');
        g_st.ss = (t[4] - '0') * 10 + (t[5] - '0');
        g_st.hasTime = true;
      }
      for (int i = 0; i < V_COUNT; i++) g_st.v[i] = (i + 2 < n) ? pf(f[i + 2]) : NAN;
      g_st.lastOkMs = millis();
      if (!g_st.lastOkMs) g_st.lastOkMs = 1;
      hCpuT.push(g_st.v[V_CPU_T]);
      hGpuT.push(g_st.v[V_GPU_T]);
      hCpuL.push(g_st.v[V_CPU_L]);
      hGpuL.push(g_st.v[V_GPU_L]);
      g_packets++;
      return;
    }
    case 'N':
      if (n >= 2) { copyStr(S.name, sizeof S.name, f[1]); settingsMarkDirty(); }
      break;
    case 'L':
      if (n >= 3) {
        copyStr(S.cpuLabel, sizeof S.cpuLabel, f[1]);
        copyStr(S.gpuLabel, sizeof S.gpuLabel, f[2]);
        settingsMarkDirty();
      }
      break;
    case 'X':
      if (n >= 5) {
        int i = atoi(f[1]);
        if (i < 0 || i > 3) return;
        copyStr(S.extra[i].label, sizeof S.extra[i].label, f[2]);
        copyStr(S.extra[i].unit, sizeof S.extra[i].unit, f[3]);
        float m = pf(f[4]);
        S.extra[i].maxv = isnan(m) ? 0 : m;
        settingsMarkDirty();
      }
      break;
    case 'S':
      if (n >= 7) {
        int th = atoi(f[1]);
        if (themeValid(th) && th != S.theme) { S.theme = th; g_themeFromPC = true; }
        S.rotateSec = constrain(atoi(f[2]), 0, 3600);
        S.alertOn   = constrain(atoi(f[3]), 30, 110);
        S.alertOff  = constrain(atoi(f[4]), 20, S.alertOn);
        S.warn      = constrain(atoi(f[5]), 20, S.alertOn);
        // sonido: 16..31 = mascara (16 + bits de SND_*); 0/1 = formato viejo (todo apagado / todo encendido)
        {
          int snd = atoi(f[6]);
          S.sound = snd >= 16 ? (snd & SND_ALL) : (snd ? SND_ALL : 0);
        }
        settingsMarkDirty();
      }
      break;
    case 'T':
      if (n >= 2) {
        int th = atoi(f[1]);
        if (themeValid(th)) { S.theme = th; g_themeFromPC = true; settingsMarkDirty(); }
      }
      break;
    case 'B':
      soundTest();
      break;
    case 'Z':   // Z|encendido|conexion|tema|alarma|repetirSeg  -> que melodia usa cada evento
      if (n >= 6) {
        for (int i = 0; i < 4; i++) SNDSEL.sel[i] = (uint8_t)constrain(atoi(f[i + 1]), 0, 31);
        SNDSEL.repeatSec = (uint8_t)constrain(atoi(f[5]), 0, 255);
        settingsMarkDirty();
      }
      break;
    case 'Y':   // Y|evento|melodia  -> la toca ya (boton Probar del Hub), aunque el evento este apagado
      if (n >= 3) playEvent(atoi(f[1]), atoi(f[2]), true);
      break;
    case 'P':   // P|1 = mostrar la vista previa del creador de temas, P|0 = volver al tema anterior
      if (n >= 2 && atoi(f[1])) {
        if (!g_previewOn) g_previewPrevTheme = S.theme;
        g_previewOn = true;
        g_customLoaded = -1;
        S.theme = NATIVE_COUNT + PREVIEW_SLOT;
      } else if (g_previewOn) {
        g_previewOn = false;
        S.theme = themeValid(g_previewPrevTheme) ? g_previewPrevTheme : 0;
      }
      g_themeFromPC = true;
      break;
    case 'Q':
      for (int s = 0; s < CUSTOM_MAX; s++)
        if (g_customMask & (1 << s)) Serial.printf("CT|%d|%lu|%s\n", s, (unsigned long)g_customHash[s], g_customName[s]);
      Serial.println("CT|END");
      return;
    default:
      return;
  }
  Serial.printf("OK|%c\n", cmd[0]);
}

static void linkInit() {
  Serial.setRxBufferSize(2048);   // el render tarda ~50 ms; el buffer guarda lo que llega mientras
  Serial.begin(SERIAL_BAUD);
}

static void linkPoll() {
  while (Serial.available()) {
    char c = (char)Serial.read();
    if (c == '\n' || c == '\r') {
      if (s_rxn) { s_rx[s_rxn] = 0; handleLine(s_rx); s_rxn = 0; }
    } else if (s_rxn < (int)sizeof(s_rx) - 1) {
      s_rx[s_rxn++] = c;
    } else {
      s_rxn = 0;   // linea demasiado larga: se descarta
    }
  }
}

static bool linkOnline() {
  return g_st.lastOkMs && (millis() - g_st.lastOkMs < OFFLINE_AFTER_MS);
}
