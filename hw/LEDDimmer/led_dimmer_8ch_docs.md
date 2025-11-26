# 8-Channel LED Dimmer - Circuit Documentation

## Overview
ESP32-based 4/8-channel PWM LED dimmer with WS2812 status indicator and 2 button inputs.
Same PCB can be populated for 4 or 8 channels.

## Pin Assignments

| Function       | ESP32 GPIO | Board        | Notes                    |
|----------------|------------|--------------|--------------------------|
| PWM Channel 1  | GPIO25     | 4-ch & 8-ch  | LEDC Channel 0           |
| PWM Channel 2  | GPIO26     | 4-ch & 8-ch  | LEDC Channel 1           |
| PWM Channel 3  | GPIO27     | 4-ch & 8-ch  | LEDC Channel 2           |
| PWM Channel 4  | GPIO14     | 4-ch & 8-ch  | LEDC Channel 3           |
| PWM Channel 5  | GPIO4      | 8-ch only    | LEDC Channel 4           |
| PWM Channel 6  | GPIO5      | 8-ch only    | LEDC Channel 5           |
| PWM Channel 7  | GPIO18     | 8-ch only    | LEDC Channel 6           |
| PWM Channel 8  | GPIO19     | 8-ch only    | LEDC Channel 7           |
| WS2812 Data    | GPIO16     | Both         | RMT peripheral           |
| Button 1       | GPIO32     | Both         | Active LOW, 10K pull-up  |
| Button 2       | GPIO33     | Both         | Active LOW, 10K pull-up  |

## Board Variants

### 4-Channel Build
- Populate Q1-Q4, R1-R4, R9-R12, J2-J5
- Leave Q5-Q8, R5-R8, R13-R16, J8-J11 unpopulated
- Set `NUM_CHANNELS 4` in firmware

### 8-Channel Build  
- Populate all MOSFETs, resistors, and connectors
- Set `NUM_CHANNELS 8` in firmware

## Circuit Description

### MOSFET Driver Stage (x8)
```
                         VIN ──┬── LED+ (from strip)
                               │
GPIO ──[100R]──┬──[GATE]     [DRAIN]
               │               │
             [10K]          LED- (from strip)
               │               │
              GND          [SOURCE]
                               │
                              GND
```

## Bill of Materials

| Ref      | Value        | Package   | 4-ch | 8-ch | Notes                |
|----------|--------------|-----------|------|------|----------------------|
| U1       | ESP32-WROOM  | Module    | 1    | 1    | Or DevKit board      |
| U2       | AMS1117-3.3  | SOT-223   | 1    | 1    | 3.3V LDO regulator   |
| Q1-Q4    | IRLZ44N      | TO-220    | 4    | 4    | Logic-level N-MOSFET |
| Q5-Q8    | IRLZ44N      | TO-220    | -    | 4    | 8-ch only            |
| D1       | WS2812B      | 5050      | 1    | 1    | Status LED           |
| R1-R4    | 100Ω         | 0805      | 4    | 4    | Gate resistors       |
| R5-R8    | 100Ω         | 0805      | -    | 4    | 8-ch only            |
| R9-R12   | 10KΩ         | 0805      | 4    | 4    | Gate pull-downs      |
| R13-R16  | 10KΩ         | 0805      | -    | 4    | 8-ch only            |
| R17-R18  | 10KΩ         | 0805      | 2    | 2    | Button pull-ups      |
| C1,C3    | 100nF        | 0805      | 2    | 2    | Decoupling           |
| C2       | 10µF         | 0805      | 1    | 1    | Bulk decoupling      |
| J1       | 2-pos screw  | 5.08mm    | 1    | 1    | Power input          |
| J2-J5    | 2-pos screw  | 5.08mm    | 4    | 4    | LED outputs CH1-4    |
| J6-J7    | 2-pos header | 2.54mm    | 2    | 2    | Button connectors    |
| J8-J11   | 2-pos screw  | 5.08mm    | -    | 4    | LED outputs CH5-8    |

### Totals
| Component    | 4-Channel | 8-Channel |
|--------------|-----------|-----------|
| MOSFETs      | 4         | 8         |
| 100Ω         | 4         | 8         |
| 10KΩ         | 6         | 10        |
| Screw terms  | 5         | 9         |

## Firmware Configuration

Edit the single `#define` at the top of `led_dimmer.ino`:

```cpp
// For 4-channel board:
#define NUM_CHANNELS    4

// For 8-channel board:
#define NUM_CHANNELS    8
```

The firmware auto-configures PWM channels based on this setting.

## Serial Commands (Optional)

Uncomment the serial handler in the code for debug/control:

| Command     | Action                          |
|-------------|---------------------------------|
| `CH1:128`   | Set channel 1 to 50%            |
| `CH5:255`   | Set channel 5 to 100%           |
| `ALL:0`     | All channels off                |
| `ALL:64`    | All channels to 25%             |

## Design Notes

1. **4-ch vs 8-ch**: Same PCB design, just populate extra components for 8-ch
2. **GPIO Selection**: Avoided bootstrap pins (GPIO0, 2, 12) for PWM outputs
3. **PWM Frequency**: 5kHz default, increase to 20kHz if visible flicker
4. **Current**: Each channel rated 1.4A, IRLZ44N can handle 47A continuous
5. **Heat**: Negligible at 1.4A with IRLZ44N's 22mΩ Rds(on): P = I²R = 43mW

## Expansion Options

If you need 12 channels in future, these GPIOs are still available:
- GPIO17 (spare)
- GPIO21, 22, 23 (I2C pins, usable for PWM)

Would give you 11 total PWM + 1 spare, or 12 PWM if you use internal pull-ups for buttons.
