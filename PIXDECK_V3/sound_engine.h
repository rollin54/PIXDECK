#pragma once
#include <Arduino.h>
#include "config.h"

// Secuenciador de notas NO bloqueante: se avanza llamando soundUpdate() en el loop.
struct Note { uint16_t freq; uint16_t ms; };   // freq = 0 => silencio

static const Note* s_seq = nullptr;
static uint8_t s_len = 0, s_idx = 0;
static uint32_t s_end = 0;

inline void initSoundEngine() {
  pinMode(BUZZER_PIN, OUTPUT);
  digitalWrite(BUZZER_PIN, LOW);
}

inline void soundPlay(const Note* seq, uint8_t len) {
  s_seq = seq; s_len = len; s_idx = 0; s_end = 0;
}

inline void soundUpdate() {
  if (!s_seq) return;
  uint32_t now = millis();
  if (s_end && (int32_t)(now - s_end) < 0) return;      // la nota actual sigue sonando
  if (s_idx >= s_len) { noTone(BUZZER_PIN); s_seq = nullptr; return; }
  const Note& n = s_seq[s_idx++];
  if (n.freq) tone(BUZZER_PIN, n.freq); else noTone(BUZZER_PIN);
  s_end = now + n.ms;
}
