#pragma once
#include <Arduino.h>

#if ESP_ARDUINO_VERSION_MAJOR < 3
#error "Arduino-ESP32 3.x required (ledcAttach 3-arg API). See platformio.ini."
#endif

// =============================================================================
// VANDIMMER-4CH+2A  Rev A  --  ESP32-S3-WROOM-1U-N16 pin map
// =============================================================================
// Extracted from the exported netlist on 2026-09-04, NOT from any design
// document. The spec's section 7 pin table is the ORIGINAL plan and is wrong --
// the GPIO map changed twice during layout. Re-derive before trusting:
//
//   kicad-cli sch export netlist --format kicadsexpr \
//       -o vd.net hw/VDDimmer/VANDIMMER-4CH2A.kicad_sch
//
// then read U1's nodes. Board as ordered = git tag `ordered-revA` (37c12ab).
//
//   GPIO  Pad  Net           Function
//   IO3   15   GATE_IN1      PWM channel 1
//   IO9   17   GATE_IN2      PWM channel 2
//   IO11  19   GATE_IN3      PWM channel 3
//   IO13  21   GATE_IN4      PWM channel 4
//   IO21  23   ADDR1_DIN     addressable strip 1 data
//   IO47  24   ADDR2_DIN     addressable strip 2 data
//   IO48  25   ADDR_CLK      shared APA102 clock -- UNUSABLE, see below
//   IO45  26   STATUS_DIN    on-board WS2812B status LED (D7)
//   IO40  33   BTN1          J11, active low
//   IO39  32   BTN2          J12, active low
//   IO10  18   I2C_SCL       expansion header J13
//   IO12  20   I2C_SDA       expansion header J13
//   IO42  35   UART1_TX      J14
//   IO41  34   UART1_RX      J14
//   IO1   39   VIN_SENSE     ADC, 100k/10k divider off VIN_PROT
//   IO2   38   NTC_SENSE     ADC, 10k pull-up to +3V3 over 10k B3950 NTC
//   IO0   27   BOOT_IO0      strapping, broken out on J13.6
//   EN     3   EN_MCU        reset, broken out on J13.5
// =============================================================================

// --- PWM channels ------------------------------------------------------------
// LOW-SIDE N-FETs (20N06) behind U4, a 74HCT125 running from +5V.
// GPIO HIGH => buffer output HIGH => gate driven => channel ON.
//
// SAFETY: U4's four /OE pins (1, 4, 10, 13) are all tied to GND, so the buffer
// is permanently enabled. GATE_IN1..4 have no pulldown -- R5..R8 (10k) sit on
// the gate side and cannot fight a push-pull buffer output. While the ESP32 is
// in reset these four pins float, so a channel can glitch ON for the duration
// of the ROM boot window. pwm_preinit() must therefore be the FIRST thing
// setup() calls, and it latches the pads with gpio_hold_en() so the glitch
// cannot recur on a soft reset. A brief flash at COLD power-on is expected and
// is not a fault.
static constexpr uint8_t PWM_CHANNELS = 4;
static constexpr uint8_t PIN_GATE[PWM_CHANNELS] = { 3, 9, 11, 13 };

// --- Addressable outputs -----------------------------------------------------
// Data is level-shifted to 5 V by U5 (74HCT125) through 47R series resistors.
static constexpr uint8_t PIN_ADDR1_DIN  = 21;
static constexpr uint8_t PIN_ADDR2_DIN  = 47;
static constexpr uint8_t PIN_STATUS_DIN = 45;   // D7, on-board status pixel

// ADDR_CLK is a provision only and is deliberately never configured.
// R11/R12 (47R) are DNP, so U5.8 reaches neither J7.3 nor J8.3 -- the outputs
// are WS2812-style single-wire (data only). Note also that R11 and R12 are both
// fed from the SAME buffer output, so even fully fitted this is ONE clock
// shared by both strips, not two independent ones: two APA102 strips would have
// to be clocked in lockstep.
static constexpr uint8_t PIN_ADDR_CLK = 48;     // do not drive

// --- Buttons -----------------------------------------------------------------
// R43/R44 are external 10k pull-ups to +3V3; J11.2/J12.2 go to GND.
// Use INPUT, not INPUT_PULLUP.
static constexpr uint8_t PIN_BTN1 = 40;
static constexpr uint8_t PIN_BTN2 = 39;

// --- Analogue sense ----------------------------------------------------------
static constexpr uint8_t PIN_VIN_SENSE = 1;     // ADC1_CH0
static constexpr uint8_t PIN_NTC_SENSE = 2;     // ADC1_CH1

// R27 100k (VIN_PROT -> VIN_SENSE) / R28 10k (VIN_SENSE -> GND) => divide by 11
static constexpr float VIN_DIVIDER_RATIO = 11.0f;

// R29 10k 1% pull-up to +3V3, RT1 10k B3950 NTC to GND.
// The divider is ratiometric to the 3V3 rail while the ADC reference is
// internal, so rail error shows up directly as temperature error.
static constexpr float NTC_SERIES_OHMS  = 10000.0f;
static constexpr float NTC_NOMINAL_OHMS = 10000.0f;
static constexpr float NTC_NOMINAL_K    = 298.15f;   // 25 C
static constexpr float NTC_BETA         = 3950.0f;
static constexpr float NTC_RAIL_MV      = 3300.0f;

// --- Expansion (not driven by this firmware) ---------------------------------
static constexpr uint8_t PIN_I2C_SCL  = 10;
static constexpr uint8_t PIN_I2C_SDA  = 12;
static constexpr uint8_t PIN_UART1_TX = 42;
static constexpr uint8_t PIN_UART1_RX = 41;
