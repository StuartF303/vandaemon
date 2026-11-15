#include "pwm.h"
#include <math.h>
#include "driver/gpio.h"

static constexpr uint8_t  PWM_RES_BITS = 12;
static constexpr uint16_t PWM_MAX_DUTY = (1u << PWM_RES_BITS) - 1;

static uint16_t s_gamma[256];
static uint8_t  s_level[PWM_CHANNELS] = {0};
static bool     s_gammaOn = true;
static float    s_derate  = 1.0f;
static bool     s_ready   = false;

void pwm_preinit() {
    for (uint8_t i = 0; i < PWM_CHANNELS; i++) {
        gpio_num_t pin = (gpio_num_t)PIN_GATE[i];
        gpio_hold_dis(pin);
        pinMode(PIN_GATE[i], OUTPUT);
        digitalWrite(PIN_GATE[i], LOW);
        // IO3/9/11/13 are all RTC-capable on the S3, so the hold survives a
        // soft reset and the channels stay off through the next ROM boot.
        gpio_hold_en(pin);
    }
}

static void buildGammaTable() {
    // CIE 1931 lightness -> luminance. Perceptually even steps, and a far
    // finer low end than a linear ramp, which is where van lighting lives.
    for (uint16_t i = 0; i < 256; i++) {
        float lstar = (float)i * 100.0f / 255.0f;
        float y = (lstar > 8.0f)
                      ? powf((lstar + 16.0f) / 116.0f, 3.0f)
                      : lstar / 903.3f;
        float duty = y * (float)PWM_MAX_DUTY;
        if (duty < 0.0f) duty = 0.0f;
        if (duty > PWM_MAX_DUTY) duty = PWM_MAX_DUTY;
        s_gamma[i] = (uint16_t)lroundf(duty);
    }
    // A non-zero request must never round down to a dark channel.
    for (uint16_t i = 1; i < 256; i++) {
        if (s_gamma[i] == 0) s_gamma[i] = 1;
    }
}

static void applyChannel(uint8_t ch) {
    if (!s_ready) return;
    uint32_t duty = s_gammaOn
                        ? s_gamma[s_level[ch]]
                        : ((uint32_t)s_level[ch] * PWM_MAX_DUTY) / 255u;
    duty = (uint32_t)lroundf((float)duty * s_derate);
    if (duty > PWM_MAX_DUTY) duty = PWM_MAX_DUTY;
    ledcWrite(PIN_GATE[ch], duty);
}

void pwm_begin(uint16_t freqHz, bool gammaCorrect) {
    s_gammaOn = gammaCorrect;
    buildGammaTable();

    for (uint8_t i = 0; i < PWM_CHANNELS; i++) {
        gpio_hold_dis((gpio_num_t)PIN_GATE[i]);
        ledcAttach(PIN_GATE[i], freqHz, PWM_RES_BITS);
        ledcWrite(PIN_GATE[i], 0);
    }
    s_ready = true;
    for (uint8_t i = 0; i < PWM_CHANNELS; i++) applyChannel(i);
}

void pwm_set(uint8_t channel, uint8_t value) {
    if (channel >= PWM_CHANNELS) return;
    s_level[channel] = value;
    applyChannel(channel);
}

uint8_t pwm_get(uint8_t channel) {
    return channel < PWM_CHANNELS ? s_level[channel] : 0;
}

const uint8_t *pwm_levels() { return s_level; }

void pwm_setAll(uint8_t value) {
    for (uint8_t i = 0; i < PWM_CHANNELS; i++) pwm_set(i, value);
}

void pwm_allOff() { pwm_setAll(0); }

void pwm_setDerate(float factor) {
    if (factor < 0.0f) factor = 0.0f;
    if (factor > 1.0f) factor = 1.0f;
    if (fabsf(factor - s_derate) < 0.001f) return;
    s_derate = factor;
    for (uint8_t i = 0; i < PWM_CHANNELS; i++) applyChannel(i);
}

float pwm_derate() { return s_derate; }
