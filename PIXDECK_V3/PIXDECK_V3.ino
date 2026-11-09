// =====================================================================================
//  PIXDECK V3 - monitor de PC por USB (sin WiFi)
//  Recibe los datos desde "PixDeck Hub" (programa de Windows con LibreHardwareMonitor).
//  Boton BOOT de la placa: cambia de tema.
// =====================================================================================
#include "config.h"
#include "settings.h"
#include "sound_engine.h"
#include "pixel_sounds.h"
#include "link.h"
#include "gfx.h"
#include "screens.h"

enum Mode { MODE_LOADING, MODE_WAITING, MODE_DASH };
static Mode mode = MODE_LOADING;
static uint32_t bootMs = 0, lastFrame = 0, themeSince = 0;
static bool alertCpu = false, alertGpu = false;
static uint8_t lastTheme = 255;

// ---------------- alertas con histeresis ----------------
static uint32_t lastAlarmAt = 0;

static void checkAlerts() {
  const float ct = ST.v[V_CPU_T], gt = ST.v[V_GPU_T];
  if (!isnan(gt)) {
    if (gt >= S.alertOn && !alertGpu) { alertGpu = true; soundAlert(); raiseAlert(1, (int)gt); lastAlarmAt = millis(); }
    else if (gt < S.alertOff) alertGpu = false;
  }
  if (!isnan(ct)) {
    if (ct >= S.alertOn && !alertCpu) { alertCpu = true; soundAlert(); raiseAlert(0, (int)ct); lastAlarmAt = millis(); }
    else if (ct < S.alertOff) alertCpu = false;
  }
  // Repetir la alarma mientras siga caliente (si se eligio en el Hub)
  if (SNDSEL.repeatSec && (alertCpu || alertGpu) && millis() - lastAlarmAt >= (uint32_t)SNDSEL.repeatSec * 1000UL) {
    lastAlarmAt = millis();
    soundAlert();
    bool cpuHot = alertCpu && !isnan(ct) && (isnan(gt) || ct >= gt);
    raiseAlert(cpuHot ? 0 : 1, (int)(cpuHot ? ct : gt));
  }
}

// ---------------- cambio de tema ----------------
static void setTheme(uint8_t t, bool notifyPC) {
  S.theme = themeValid(t) ? t : 0;
  if (notifyPC) settingsMarkDirty();   // la rotacion automatica no escribe en la flash
  if (notifyPC) Serial.printf("EVT|THEME|%u\n", S.theme);
}

static void onThemeShown() {
  if (S.theme == lastTheme) return;
  lastTheme = S.theme;
  themeSince = millis();
  NOW = millis();
  if (mode == MODE_DASH) { showToast(themeName(S.theme)); soundTheme(); }
}

// ---------------- boton BOOT ----------------
static void buttonPoll() {
  static bool last = true;
  static uint32_t changedAt = 0;
  bool v = digitalRead(BOOT_BTN_PIN);
  if (v != last && millis() - changedAt > 40) {
    changedAt = millis();
    last = v;
    if (!v) setTheme(themeNext(S.theme), true);   // al presionar
  }
}

void setup() {
  linkInit();
  settingsLoad();
  initSoundEngine();
  pinMode(BOOT_BTN_PIN, INPUT_PULLUP);

  // El sprite se reserva primero, con la memoria todavia sin fragmentar.
  if (!gfxInit()) {
    tft.setTextColor(TFT_RED);
    tft.drawString("SIN MEMORIA", 20, 100, 1);
    while (true) delay(1000);
  }
  customInit();                          // temas subidos guardados en la flash
  if (!themeValid(S.theme)) S.theme = 0;
  animReset();
  bootMs = millis();
  soundBoot();
  sendHello();   // por si la PC ya estaba escuchando
}

void loop() {
  linkPoll();
  soundUpdate();
  buttonPoll();
  settingsAutosave();

  uint32_t now = millis();
  if (now - lastFrame < FRAME_MS) return;
  lastFrame = now;
  NOW = now;
  ST = g_st;
  bool online = linkOnline();

  // ===== CARGA =====
  if (mode == MODE_LOADING) {
    float target = online ? 100.0f : 60.0f;
    g_loadPct += (target - g_loadPct) * 0.07f;
    g_loadMsg = online ? "LISTO" : (now - bootMs < 1200 ? "INICIANDO" : "ESPERANDO PC");
    renderScreen(drawLoading, false);
    if (online && g_loadPct >= 97.0f) {
      mode = MODE_DASH;
      animReset();
      soundOnline();
      lastTheme = 255;
    } else if (!online && now - bootMs > 3500) {
      mode = MODE_WAITING;
    }
    return;
  }

  // ===== ESPERANDO PC =====
  if (mode == MODE_WAITING) {
    if (online) {
      mode = MODE_DASH;
      animReset();
      soundOnline();
      lastTheme = 255;
    } else {
      renderScreen(drawWaiting, false);
      return;
    }
  }

  // ===== DASHBOARD =====
  if (!online) { mode = MODE_WAITING; return; }

  if (g_themeFromPC) g_themeFromPC = false;
  if (g_previewOn && S.theme != NATIVE_COUNT + PREVIEW_SLOT) g_previewOn = false;   // se cambio de tema
  if (S.rotateSec && !g_previewOn && now - themeSince >= (uint32_t)S.rotateSec * 1000UL) setTheme(themeNext(S.theme), false);
  onThemeShown();

  animStep();
  checkAlerts();
  renderScreen(drawCurrentTheme, true);
}
