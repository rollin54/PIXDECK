#pragma once
#include "sound_engine.h"
#include "settings.h"

// ======================================================================================
// Biblioteca de sonidos. Cada evento tiene varias melodias; el Hub elige cual (SNDSEL.sel[ev]).
// El ORDEN de cada tabla tiene que coincidir con SoundCatalog en el Hub (AppConfig.cs).
// Nota {frecuencia Hz, duracion ms}; frecuencia 0 = silencio.
// ======================================================================================

enum { EV_BOOT = 0, EV_CONN = 1, EV_THEME = 2, EV_ALARM = 3 };

// ---------------- encendido ----------------
static const Note B_CLASICO[] = { {523,70},{659,70},{784,70},{1047,120} };
static const Note B_RETRO[]   = { {392,60},{0,15},{523,60},{0,15},{659,60},{0,15},{784,60},{0,15},{1047,90},{0,30},{784,60},{1047,160} };
static const Note B_SCIFI[]   = { {400,25},{500,25},{620,25},{760,25},{920,25},{1100,25},{1300,25},{1520,25},{1760,25},{2000,60},{0,40},{2000,30},{0,30},{2400,90} };
static const Note B_ARCADE[]  = { {1047,50},{0,20},{1319,50},{0,20},{1568,50},{0,20},{2093,120},{0,40},{1568,60},{2093,160} };
static const Note B_FANFARE[] = { {523,120},{0,30},{523,90},{0,20},{523,90},{0,20},{698,300},{0,40},{880,160},{698,120},{1047,350} };
static const Note B_BEEP[]    = { {1800,120} };

// ---------------- conexion con la PC ----------------
static const Note C_DOBLE[]   = { {800,80},{0,20},{1200,100} };
static const Note C_ASC[]     = { {660,60},{880,60},{1175,60},{1568,120} };
static const Note C_BURBUJA[] = { {900,30},{1300,30},{1700,30},{0,40},{1200,30},{1600,40} };
static const Note C_BLIP[]    = { {2000,40} };

// ---------------- cambio de tema ----------------
static const Note T_CLIC[]    = { {1500,25},{0,15},{2200,35} };
static const Note T_SWOOSH[]  = { {2500,15},{2100,15},{1700,15},{1300,15},{1000,20} };
static const Note T_TICTAC[]  = { {3000,10},{0,80},{2200,10} };
static const Note T_BLIP2[]   = { {1800,30},{0,40},{1800,30} };

// ---------------- alarma de temperatura ----------------
static const Note A_BITONO[] = {
  {2000,90},{0,10},{1200,90},{0,10}, {2000,90},{0,10},{1200,90},{0,10}, {2000,90},{0,10},{1200,90},{0,10},
  {2000,90},{0,10},{1200,90},{0,10}, {2000,90},{0,10},{1200,90},{0,10}, {2000,90},{0,10},{1200,90},{0,10} };
static const Note A_SIRENA[] = {
  {600,30},{800,30},{1000,30},{1200,30},{1400,30},{1600,60},{1400,30},{1200,30},{1000,30},{800,30},{600,60},
  {600,30},{800,30},{1000,30},{1200,30},{1400,30},{1600,60},{1400,30},{1200,30},{1000,30},{800,30},{600,60},
  {600,30},{800,30},{1000,30},{1200,30},{1400,30},{1600,60},{1400,30},{1200,30},{1000,30},{800,30},{600,60} };
static const Note A_URGENTE[] = {
  {2500,60},{0,40},{2500,60},{0,40},{2500,60},{0,40},{2500,60},{0,140},
  {2500,60},{0,40},{2500,60},{0,40},{2500,60},{0,40},{2500,60},{0,140} };
static const Note A_SOS[] = {   // ... --- ...
  {1800,100},{0,100},{1800,100},{0,100},{1800,100},{0,300},
  {1800,300},{0,100},{1800,300},{0,100},{1800,300},{0,300},
  {1800,100},{0,100},{1800,100},{0,100},{1800,100} };
static const Note A_SUAVE[] = { {880,200},{0,150},{660,300},{0,400},{880,200},{0,150},{660,300} };

// ---------------- tablas ----------------
struct Melody { const Note* n; uint8_t len; };
#define MEL(a) { a, (uint8_t)(sizeof(a) / sizeof(Note)) }

static const Melody MEL_BOOT[]  = { MEL(B_CLASICO), MEL(B_RETRO), MEL(B_SCIFI), MEL(B_ARCADE), MEL(B_FANFARE), MEL(B_BEEP) };
static const Melody MEL_CONN[]  = { MEL(C_DOBLE), MEL(C_ASC), MEL(C_BURBUJA), MEL(C_BLIP) };
static const Melody MEL_THEME[] = { MEL(T_CLIC), MEL(T_SWOOSH), MEL(T_TICTAC), MEL(T_BLIP2) };
static const Melody MEL_ALARM[] = { MEL(A_BITONO), MEL(A_SIRENA), MEL(A_URGENTE), MEL(A_SOS), MEL(A_SUAVE) };

static const Melody* const MEL_TABLE[4] = { MEL_BOOT, MEL_CONN, MEL_THEME, MEL_ALARM };
static const uint8_t MEL_COUNT[4] = {
  sizeof(MEL_BOOT) / sizeof(Melody), sizeof(MEL_CONN) / sizeof(Melody),
  sizeof(MEL_THEME) / sizeof(Melody), sizeof(MEL_ALARM) / sizeof(Melody) };

// force = suena aunque el evento este desactivado (boton "Probar" del Hub)
static void playEvent(int ev, int idx, bool force) {
  if (ev < 0 || ev > 3) return;
  if (!force && !(S.sound & (1 << ev))) return;
  if (idx < 0 || idx >= MEL_COUNT[ev]) idx = 0;
  const Melody& m = MEL_TABLE[ev][idx];
  soundPlay(m.n, m.len);
}

static const Note SND_TEST[] = { {660,90},{0,20},{880,90},{0,20},{1320,140} };

inline void soundBoot()   { playEvent(EV_BOOT,  SNDSEL.sel[EV_BOOT],  false); }
inline void soundOnline() { playEvent(EV_CONN,  SNDSEL.sel[EV_CONN],  false); }
inline void soundTheme()  { playEvent(EV_THEME, SNDSEL.sel[EV_THEME], false); }
inline void soundAlert()  { playEvent(EV_ALARM, SNDSEL.sel[EV_ALARM], false); }
inline void soundTest()   { soundPlay(SND_TEST, sizeof(SND_TEST) / sizeof(Note)); }   // siempre suena
