#include "control.h"
#include "board_pins.h"
#include "pwm.h"
#include "store.h"
#include "net.h"

static_assert(PWM_CHANNEL_COUNT == PWM_CHANNELS, "settings.h and board_pins.h disagree on the channel count");

static void stage() {
    store_stageLevels(pwm_levels(), PWM_CHANNELS);
    store_markLevelsDirty();
}

void ctl_setLevel(uint8_t ch, uint8_t level) {
    if (ch >= PWM_CHANNELS) return;
    pwm_set(ch, level);
    stage();
    net_publishChannelState(ch);
}

void ctl_setAll(uint8_t level) {
    pwm_setAll(level);
    stage();
    for (uint8_t i = 0; i < PWM_CHANNELS; i++) net_publishChannelState(i);
}

void ctl_allOff() { ctl_setAll(0); }
