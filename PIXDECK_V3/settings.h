#pragma once
#include <Arduino.h>
#include <Preferences.h>
#include "config.h"

// Configuracion que manda el programa de la PC. Se guarda en la flash (NVS)
// para que el equipo arranque con su nombre y su tema aunque la PC no este.

struct ExtraCfg {
  char  label[12];   // "" = desactivado
  char  unit[6];
  float maxv;        // para barras; 0 = sin barra
};

struct Settings {
  uint32_t magic;
  char     name[17];
  char     cpuLabel[17];
  char     gpuLabel[17];
  uint8_t  theme;
  uint16_t rotateSec;  // 0 = sin rotacion automatica
  uint8_t  alertOn;    // C: dispara la alerta
  uint8_t  alertOff;   // C: rearma la alerta (histeresis)
  uint8_t  warn;       // C: color ambar
  uint8_t  sound;      // mascara: SND_BOOT_BIT | SND_CONN_BIT | SND_THEME_BIT | SND_ALARM_BIT
  ExtraCfg extra[4];
};

static constexpr uint32_t SETTINGS_MAGIC = 0x50584433;  // "PXD3"

// Sonidos que se pueden apagar por separado
#define SND_BOOT_BIT   1   // encendido
#define SND_CONN_BIT   2   // conexion con la PC
#define SND_THEME_BIT  4   // cambio de tema
#define SND_ALARM_BIT  8   // alarma de temperatura
#define SND_ALL        15

static Settings S;

// Que melodia suena en cada evento (indice en las tablas de pixel_sounds.h) y repeticion de la alarma.
// Se guarda aparte ("snd") para no invalidar la configuracion ya guardada.
struct SoundSel {
  uint8_t sel[4];      // 0 encendido, 1 conexion, 2 cambio de tema, 3 alarma
  uint8_t repeatSec;   // 0 = la alarma suena una vez; si no, se repite mientras siga caliente
};
static SoundSel SNDSEL = {{0, 0, 0, 0}, 0};

static bool     s_dirty = false;
static uint32_t s_dirtyAt = 0;

static void copyStr(char* dst, size_t n, const char* src) {
  size_t i = 0;
  for (; src && src[i] && i < n - 1; i++) {
    char c = src[i];
    dst[i] = (c >= 0x20 && c <= 0x7E) ? c : '?';   // las fuentes solo traen ASCII
  }
  dst[i] = 0;
}

static void settingsDefaults() {
  memset(&S, 0, sizeof S);
  S.magic = SETTINGS_MAGIC;
  copyStr(S.name, sizeof S.name, "PIXDECK");
  copyStr(S.cpuLabel, sizeof S.cpuLabel, "CPU");
  copyStr(S.gpuLabel, sizeof S.gpuLabel, "GPU");
  S.theme = 0;
  S.rotateSec = 0;
  S.alertOn = 70;
  S.alertOff = 55;
  S.warn = 60;
  S.sound = SND_ALL;
}

static void settingsLoad() {
  Preferences p;
  p.begin("pixdeck", true);
  size_t n = p.getBytesLength("cfg");
  bool ok = false;
  if (n == sizeof(Settings)) {
    p.getBytes("cfg", &S, sizeof S);
    ok = (S.magic == SETTINGS_MAGIC);
  }
  if (p.getBytesLength("snd") == sizeof(SoundSel)) p.getBytes("snd", &SNDSEL, sizeof SNDSEL);
  p.end();
  if (!ok) settingsDefaults();   // el tema se valida despues de leer los temas subidos
  else if (S.sound == 1) S.sound = SND_ALL;   // version anterior: 1 = todos los sonidos
}

// Se guarda con retraso para juntar varios comandos seguidos en una sola escritura.
static void settingsMarkDirty() {
  s_dirty = true;
  s_dirtyAt = millis();
}

static void settingsAutosave() {
  if (!s_dirty || millis() - s_dirtyAt < 1500) return;
  s_dirty = false;
  Preferences p;
  p.begin("pixdeck", false);
  Settings old;
  bool same = p.getBytesLength("cfg") == sizeof old &&
              p.getBytes("cfg", &old, sizeof old) == sizeof old &&
              memcmp(&old, &S, sizeof S) == 0;
  if (!same) p.putBytes("cfg", &S, sizeof S);
  SoundSel oldS;
  bool sameS = p.getBytesLength("snd") == sizeof oldS &&
               p.getBytes("snd", &oldS, sizeof oldS) == sizeof oldS &&
               memcmp(&oldS, &SNDSEL, sizeof SNDSEL) == 0;
  if (!sameS) p.putBytes("snd", &SNDSEL, sizeof SNDSEL);
  p.end();
}
