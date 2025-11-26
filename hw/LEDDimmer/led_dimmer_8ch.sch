<?xml version="1.0" encoding="utf-8"?>
<!DOCTYPE eagle SYSTEM "eagle.dtd">
<eagle version="9.6.2">
<drawing>
<settings>
<setting alwaysvectorfont="no"/>
<setting verticaltext="up"/>
</settings>
<grid distance="0.1" unitdist="inch" unit="inch" style="lines" multiple="1" display="no" altdistance="0.01" altunitdist="inch" altunit="inch"/>
<layers>
<layer number="91" name="Nets" color="2" fill="1" visible="yes" active="yes"/>
<layer number="92" name="Busses" color="1" fill="1" visible="yes" active="yes"/>
<layer number="93" name="Pins" color="2" fill="1" visible="no" active="yes"/>
<layer number="94" name="Symbols" color="4" fill="1" visible="yes" active="yes"/>
<layer number="95" name="Names" color="7" fill="1" visible="yes" active="yes"/>
<layer number="96" name="Values" color="7" fill="1" visible="yes" active="yes"/>
<layer number="97" name="Info" color="7" fill="1" visible="yes" active="yes"/>
<layer number="98" name="Guide" color="6" fill="1" visible="yes" active="yes"/>
</layers>
<schematic xreflabel="%F%N/%S.%C%R" xrefpart="/%S.%C%R">
<description>8-Channel LED Dimmer - ESP32 PWM Controller</description>
<libraries>
<library name="led_dimmer">
<packages>
<package name="ESP32-WROOM-32">
<description>ESP32-WROOM-32 Module</description>
<wire x1="-9" y1="12.75" x2="9" y2="12.75" width="0.127" layer="21"/>
<wire x1="9" y1="12.75" x2="9" y2="-12.75" width="0.127" layer="21"/>
<wire x1="9" y1="-12.75" x2="-9" y2="-12.75" width="0.127" layer="21"/>
<wire x1="-9" y1="-12.75" x2="-9" y2="12.75" width="0.127" layer="21"/>
</package>
<package name="TO220">
<description>TO-220 Package</description>
<wire x1="-5.08" y1="1.905" x2="5.08" y2="1.905" width="0.127" layer="21"/>
<wire x1="5.08" y1="1.905" x2="5.08" y2="-1.905" width="0.127" layer="21"/>
<wire x1="5.08" y1="-1.905" x2="-5.08" y2="-1.905" width="0.127" layer="21"/>
<wire x1="-5.08" y1="-1.905" x2="-5.08" y2="1.905" width="0.127" layer="21"/>
<pad name="G" x="-2.54" y="0" drill="1.0"/>
<pad name="D" x="0" y="0" drill="1.0"/>
<pad name="S" x="2.54" y="0" drill="1.0"/>
</package>
<package name="0805">
<description>0805 SMD</description>
<smd name="1" x="-0.95" y="0" dx="1.3" dy="1.5" layer="1"/>
<smd name="2" x="0.95" y="0" dx="1.3" dy="1.5" layer="1"/>
</package>
<package name="SOT223">
<description>SOT-223</description>
<smd name="1" x="-2.3" y="-3.15" dx="0.9" dy="1.5" layer="1"/>
<smd name="2" x="0" y="-3.15" dx="0.9" dy="1.5" layer="1"/>
<smd name="3" x="2.3" y="-3.15" dx="0.9" dy="1.5" layer="1"/>
<smd name="4" x="0" y="3.15" dx="3.5" dy="1.5" layer="1"/>
</package>
<package name="5050-LED">
<description>WS2812B 5050</description>
<smd name="1" x="-2.45" y="-1.65" dx="1.5" dy="0.9" layer="1"/>
<smd name="2" x="-2.45" y="1.65" dx="1.5" dy="0.9" layer="1"/>
<smd name="3" x="2.45" y="1.65" dx="1.5" dy="0.9" layer="1"/>
<smd name="4" x="2.45" y="-1.65" dx="1.5" dy="0.9" layer="1"/>
</package>
<package name="SCREWTERMINAL-2">
<description>2-pos Screw Terminal 5.08mm</description>
<pad name="1" x="-2.54" y="0" drill="1.2"/>
<pad name="2" x="2.54" y="0" drill="1.2"/>
</package>
</packages>
<symbols>
<symbol name="ESP32-WROOM-32">
<wire x1="-17.78" y1="35.56" x2="17.78" y2="35.56" width="0.254" layer="94"/>
<wire x1="17.78" y1="35.56" x2="17.78" y2="-35.56" width="0.254" layer="94"/>
<wire x1="17.78" y1="-35.56" x2="-17.78" y2="-35.56" width="0.254" layer="94"/>
<wire x1="-17.78" y1="-35.56" x2="-17.78" y2="35.56" width="0.254" layer="94"/>
<pin name="3V3" x="-22.86" y="33.02" length="middle" direction="pwr"/>
<pin name="EN" x="-22.86" y="30.48" length="middle" direction="in"/>
<pin name="GPIO36" x="-22.86" y="27.94" length="middle"/>
<pin name="GPIO39" x="-22.86" y="25.4" length="middle"/>
<pin name="GPIO34" x="-22.86" y="22.86" length="middle"/>
<pin name="GPIO35" x="-22.86" y="20.32" length="middle"/>
<pin name="GPIO32" x="-22.86" y="17.78" length="middle"/>
<pin name="GPIO33" x="-22.86" y="15.24" length="middle"/>
<pin name="GPIO25" x="-22.86" y="12.7" length="middle"/>
<pin name="GPIO26" x="-22.86" y="10.16" length="middle"/>
<pin name="GPIO27" x="-22.86" y="7.62" length="middle"/>
<pin name="GPIO14" x="-22.86" y="5.08" length="middle"/>
<pin name="GPIO12" x="-22.86" y="2.54" length="middle"/>
<pin name="GND@1" x="-22.86" y="0" length="middle" direction="pwr"/>
<pin name="GPIO13" x="-22.86" y="-2.54" length="middle"/>
<pin name="GPIO4" x="22.86" y="33.02" length="middle" rot="R180"/>
<pin name="GPIO16" x="22.86" y="30.48" length="middle" rot="R180"/>
<pin name="GPIO17" x="22.86" y="27.94" length="middle" rot="R180"/>
<pin name="GPIO5" x="22.86" y="25.4" length="middle" rot="R180"/>
<pin name="GPIO18" x="22.86" y="22.86" length="middle" rot="R180"/>
<pin name="GPIO19" x="22.86" y="20.32" length="middle" rot="R180"/>
<pin name="GPIO21" x="22.86" y="17.78" length="middle" rot="R180"/>
<pin name="GPIO22" x="22.86" y="12.7" length="middle" rot="R180"/>
<pin name="GPIO23" x="22.86" y="10.16" length="middle" rot="R180"/>
<pin name="GPIO0" x="22.86" y="5.08" length="middle" rot="R180"/>
<pin name="GPIO2" x="22.86" y="2.54" length="middle" rot="R180"/>
<pin name="GPIO15" x="22.86" y="0" length="middle" rot="R180"/>
<pin name="GND@2" x="0" y="-40.64" length="middle" direction="pwr" rot="R90"/>
<text x="-17.78" y="36.83" size="1.778" layer="95">&gt;NAME</text>
<text x="-17.78" y="-38.1" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="NMOS">
<wire x1="-2.54" y1="-2.54" x2="-2.54" y2="2.54" width="0.254" layer="94"/>
<wire x1="0" y1="2.54" x2="0" y2="1.27" width="0.254" layer="94"/>
<wire x1="0" y1="1.27" x2="0" y2="-1.27" width="0.254" layer="94"/>
<wire x1="0" y1="-1.27" x2="0" y2="-2.54" width="0.254" layer="94"/>
<wire x1="0" y1="2.54" x2="2.54" y2="2.54" width="0.254" layer="94"/>
<wire x1="2.54" y1="2.54" x2="2.54" y2="-2.54" width="0.254" layer="94"/>
<wire x1="2.54" y1="-2.54" x2="0" y2="-2.54" width="0.254" layer="94"/>
<wire x1="0" y1="1.27" x2="2.54" y2="0" width="0.254" layer="94"/>
<wire x1="2.54" y1="0" x2="0" y2="-1.27" width="0.254" layer="94"/>
<pin name="G" x="-5.08" y="0" length="short" direction="pas"/>
<pin name="D" x="2.54" y="5.08" length="short" direction="pas" rot="R270"/>
<pin name="S" x="2.54" y="-5.08" length="short" direction="pas" rot="R90"/>
<text x="5.08" y="2.54" size="1.778" layer="95">&gt;NAME</text>
<text x="5.08" y="0" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="R">
<wire x1="-2.54" y1="0.889" x2="2.54" y2="0.889" width="0.254" layer="94"/>
<wire x1="2.54" y1="0.889" x2="2.54" y2="-0.889" width="0.254" layer="94"/>
<wire x1="2.54" y1="-0.889" x2="-2.54" y2="-0.889" width="0.254" layer="94"/>
<wire x1="-2.54" y1="-0.889" x2="-2.54" y2="0.889" width="0.254" layer="94"/>
<pin name="1" x="-5.08" y="0" length="short" direction="pas"/>
<pin name="2" x="5.08" y="0" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="1.524" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-3.048" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="C">
<wire x1="0" y1="2.54" x2="0" y2="0.508" width="0.254" layer="94"/>
<wire x1="0" y1="-2.54" x2="0" y2="-0.508" width="0.254" layer="94"/>
<rectangle x1="-2.032" y1="0.254" x2="2.032" y2="0.762" layer="94"/>
<rectangle x1="-2.032" y1="-0.762" x2="2.032" y2="-0.254" layer="94"/>
<pin name="1" x="0" y="5.08" length="short" direction="pas" rot="R270"/>
<pin name="2" x="0" y="-5.08" length="short" direction="pas" rot="R90"/>
<text x="2.54" y="1.27" size="1.778" layer="95">&gt;NAME</text>
<text x="2.54" y="-2.54" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="AMS1117">
<wire x1="-7.62" y1="5.08" x2="7.62" y2="5.08" width="0.254" layer="94"/>
<wire x1="7.62" y1="5.08" x2="7.62" y2="-5.08" width="0.254" layer="94"/>
<wire x1="7.62" y1="-5.08" x2="-7.62" y2="-5.08" width="0.254" layer="94"/>
<wire x1="-7.62" y1="-5.08" x2="-7.62" y2="5.08" width="0.254" layer="94"/>
<pin name="VIN" x="-12.7" y="2.54" length="middle" direction="pwr"/>
<pin name="GND" x="0" y="-10.16" length="middle" direction="pwr" rot="R90"/>
<pin name="VOUT" x="12.7" y="2.54" length="middle" direction="pwr" rot="R180"/>
<text x="-7.62" y="6.35" size="1.778" layer="95">&gt;NAME</text>
<text x="-7.62" y="-12.7" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="WS2812B">
<wire x1="-7.62" y1="7.62" x2="7.62" y2="7.62" width="0.254" layer="94"/>
<wire x1="7.62" y1="7.62" x2="7.62" y2="-7.62" width="0.254" layer="94"/>
<wire x1="7.62" y1="-7.62" x2="-7.62" y2="-7.62" width="0.254" layer="94"/>
<wire x1="-7.62" y1="-7.62" x2="-7.62" y2="7.62" width="0.254" layer="94"/>
<pin name="VDD" x="0" y="12.7" length="middle" direction="pwr" rot="R270"/>
<pin name="DIN" x="-12.7" y="0" length="middle" direction="in"/>
<pin name="GND" x="0" y="-12.7" length="middle" direction="pwr" rot="R90"/>
<pin name="DOUT" x="12.7" y="0" length="middle" direction="out" rot="R180"/>
<text x="-7.62" y="8.89" size="1.778" layer="95">&gt;NAME</text>
<text x="-7.62" y="-10.16" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="CONN-2">
<wire x1="-2.54" y1="5.08" x2="5.08" y2="5.08" width="0.254" layer="94"/>
<wire x1="5.08" y1="5.08" x2="5.08" y2="-5.08" width="0.254" layer="94"/>
<wire x1="5.08" y1="-5.08" x2="-2.54" y2="-5.08" width="0.254" layer="94"/>
<wire x1="-2.54" y1="-5.08" x2="-2.54" y2="5.08" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="2.54" length="middle"/>
<pin name="2" x="-7.62" y="-2.54" length="middle"/>
<text x="-2.54" y="6.35" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-7.62" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
</symbols>
<devicesets>
<deviceset name="ESP32-WROOM-32" prefix="U">
<gates>
<gate name="G$1" symbol="ESP32-WROOM-32" x="0" y="0"/>
</gates>
<devices>
<device name="" package="ESP32-WROOM-32">
<connects>
<connect gate="G$1" pin="3V3" pad="1"/>
<connect gate="G$1" pin="EN" pad="2"/>
</connects>
<technologies>
<technology name=""/>
</technologies>
</device>
</devices>
</deviceset>
<deviceset name="IRLZ44N" prefix="Q">
<description>Logic Level N-Channel MOSFET</description>
<gates>
<gate name="G$1" symbol="NMOS" x="0" y="0"/>
</gates>
<devices>
<device name="" package="TO220">
<connects>
<connect gate="G$1" pin="G" pad="G"/>
<connect gate="G$1" pin="D" pad="D"/>
<connect gate="G$1" pin="S" pad="S"/>
</connects>
<technologies>
<technology name=""/>
</technologies>
</device>
</devices>
</deviceset>
<deviceset name="R" prefix="R" uservalue="yes">
<description>Resistor</description>
<gates>
<gate name="G$1" symbol="R" x="0" y="0"/>
</gates>
<devices>
<device name="0805" package="0805">
<connects>
<connect gate="G$1" pin="1" pad="1"/>
<connect gate="G$1" pin="2" pad="2"/>
</connects>
<technologies>
<technology name=""/>
</technologies>
</device>
</devices>
</deviceset>
<deviceset name="C" prefix="C" uservalue="yes">
<description>Capacitor</description>
<gates>
<gate name="G$1" symbol="C" x="0" y="0"/>
</gates>
<devices>
<device name="0805" package="0805">
<connects>
<connect gate="G$1" pin="1" pad="1"/>
<connect gate="G$1" pin="2" pad="2"/>
</connects>
<technologies>
<technology name=""/>
</technologies>
</device>
</devices>
</deviceset>
<deviceset name="AMS1117-3.3" prefix="U">
<description>3.3V LDO Regulator</description>
<gates>
<gate name="G$1" symbol="AMS1117" x="0" y="0"/>
</gates>
<devices>
<device name="" package="SOT223">
<connects>
<connect gate="G$1" pin="VIN" pad="3"/>
<connect gate="G$1" pin="GND" pad="1"/>
<connect gate="G$1" pin="VOUT" pad="2 4"/>
</connects>
<technologies>
<technology name=""/>
</technologies>
</device>
</devices>
</deviceset>
<deviceset name="WS2812B" prefix="D">
<description>Addressable RGB LED</description>
<gates>
<gate name="G$1" symbol="WS2812B" x="0" y="0"/>
</gates>
<devices>
<device name="" package="5050-LED">
<connects>
<connect gate="G$1" pin="VDD" pad="1"/>
<connect gate="G$1" pin="DOUT" pad="2"/>
<connect gate="G$1" pin="GND" pad="3"/>
<connect gate="G$1" pin="DIN" pad="4"/>
</connects>
<technologies>
<technology name=""/>
</technologies>
</device>
</devices>
</deviceset>
<deviceset name="SCREW-2" prefix="J">
<description>2-Position Screw Terminal</description>
<gates>
<gate name="G$1" symbol="CONN-2" x="0" y="0"/>
</gates>
<devices>
<device name="" package="SCREWTERMINAL-2">
<connects>
<connect gate="G$1" pin="1" pad="1"/>
<connect gate="G$1" pin="2" pad="2"/>
</connects>
<technologies>
<technology name=""/>
</technologies>
</device>
</devices>
</deviceset>
</devicesets>
</library>
</libraries>
<parts>
<!-- Power Input -->
<part name="J1" library="led_dimmer" deviceset="SCREW-2" device="" value="PWR_IN"/>

<!-- ESP32 Module -->
<part name="U1" library="led_dimmer" deviceset="ESP32-WROOM-32" device="" value="ESP32-WROOM-32"/>

<!-- Voltage Regulator -->
<part name="U2" library="led_dimmer" deviceset="AMS1117-3.3" device="" value="AMS1117-3.3"/>

<!-- Decoupling Capacitors -->
<part name="C1" library="led_dimmer" deviceset="C" device="0805" value="100nF"/>
<part name="C2" library="led_dimmer" deviceset="C" device="0805" value="10uF"/>

<!-- Status LED -->
<part name="D1" library="led_dimmer" deviceset="WS2812B" device="" value="WS2812B"/>
<part name="C3" library="led_dimmer" deviceset="C" device="0805" value="100nF"/>

<!-- Channel 1 - GPIO25 -->
<part name="Q1" library="led_dimmer" deviceset="IRLZ44N" device="" value="IRLZ44N"/>
<part name="R1" library="led_dimmer" deviceset="R" device="0805" value="100R"/>
<part name="R9" library="led_dimmer" deviceset="R" device="0805" value="10K"/>
<part name="J2" library="led_dimmer" deviceset="SCREW-2" device="" value="LED_CH1"/>

<!-- Channel 2 - GPIO26 -->
<part name="Q2" library="led_dimmer" deviceset="IRLZ44N" device="" value="IRLZ44N"/>
<part name="R2" library="led_dimmer" deviceset="R" device="0805" value="100R"/>
<part name="R10" library="led_dimmer" deviceset="R" device="0805" value="10K"/>
<part name="J3" library="led_dimmer" deviceset="SCREW-2" device="" value="LED_CH2"/>

<!-- Channel 3 - GPIO27 -->
<part name="Q3" library="led_dimmer" deviceset="IRLZ44N" device="" value="IRLZ44N"/>
<part name="R3" library="led_dimmer" deviceset="R" device="0805" value="100R"/>
<part name="R11" library="led_dimmer" deviceset="R" device="0805" value="10K"/>
<part name="J4" library="led_dimmer" deviceset="SCREW-2" device="" value="LED_CH3"/>

<!-- Channel 4 - GPIO14 -->
<part name="Q4" library="led_dimmer" deviceset="IRLZ44N" device="" value="IRLZ44N"/>
<part name="R4" library="led_dimmer" deviceset="R" device="0805" value="100R"/>
<part name="R12" library="led_dimmer" deviceset="R" device="0805" value="10K"/>
<part name="J5" library="led_dimmer" deviceset="SCREW-2" device="" value="LED_CH4"/>

<!-- Channel 5 - GPIO4 (8-ch only) -->
<part name="Q5" library="led_dimmer" deviceset="IRLZ44N" device="" value="IRLZ44N"/>
<part name="R5" library="led_dimmer" deviceset="R" device="0805" value="100R"/>
<part name="R13" library="led_dimmer" deviceset="R" device="0805" value="10K"/>
<part name="J8" library="led_dimmer" deviceset="SCREW-2" device="" value="LED_CH5"/>

<!-- Channel 6 - GPIO5 (8-ch only) -->
<part name="Q6" library="led_dimmer" deviceset="IRLZ44N" device="" value="IRLZ44N"/>
<part name="R6" library="led_dimmer" deviceset="R" device="0805" value="100R"/>
<part name="R14" library="led_dimmer" deviceset="R" device="0805" value="10K"/>
<part name="J9" library="led_dimmer" deviceset="SCREW-2" device="" value="LED_CH6"/>

<!-- Channel 7 - GPIO18 (8-ch only) -->
<part name="Q7" library="led_dimmer" deviceset="IRLZ44N" device="" value="IRLZ44N"/>
<part name="R7" library="led_dimmer" deviceset="R" device="0805" value="100R"/>
<part name="R15" library="led_dimmer" deviceset="R" device="0805" value="10K"/>
<part name="J10" library="led_dimmer" deviceset="SCREW-2" device="" value="LED_CH7"/>

<!-- Channel 8 - GPIO19 (8-ch only) -->
<part name="Q8" library="led_dimmer" deviceset="IRLZ44N" device="" value="IRLZ44N"/>
<part name="R8" library="led_dimmer" deviceset="R" device="0805" value="100R"/>
<part name="R16" library="led_dimmer" deviceset="R" device="0805" value="10K"/>
<part name="J11" library="led_dimmer" deviceset="SCREW-2" device="" value="LED_CH8"/>

<!-- Button Inputs -->
<part name="R17" library="led_dimmer" deviceset="R" device="0805" value="10K"/>
<part name="R18" library="led_dimmer" deviceset="R" device="0805" value="10K"/>
<part name="J6" library="led_dimmer" deviceset="SCREW-2" device="" value="BTN1"/>
<part name="J7" library="led_dimmer" deviceset="SCREW-2" device="" value="BTN2"/>
</parts>
<sheets>
<sheet>
<plain>
<text x="25.4" y="175.26" size="2.54" layer="97">POWER INPUT 12-24V</text>
<text x="88.9" y="175.26" size="2.54" layer="97">ESP32 CONTROLLER</text>
<text x="45.72" y="132.08" size="2.54" layer="97">3.3V REGULATOR</text>
<text x="25.4" y="96.52" size="2.54" layer="97">STATUS LED - GPIO16</text>
<text x="25.4" y="55.88" size="2.54" layer="97">BUTTON INPUTS</text>
<text x="170.18" y="175.26" size="2.54" layer="97">PWM CHANNELS 1-4</text>
<text x="259.08" y="175.26" size="2.54" layer="97">PWM CHANNELS 5-8 (8-ch only)</text>
<text x="170.18" y="165.1" size="1.778" layer="97">CH1: GPIO25</text>
<text x="170.18" y="127" size="1.778" layer="97">CH2: GPIO26</text>
<text x="170.18" y="88.9" size="1.778" layer="97">CH3: GPIO27</text>
<text x="170.18" y="50.8" size="1.778" layer="97">CH4: GPIO14</text>
<text x="259.08" y="165.1" size="1.778" layer="97">CH5: GPIO4</text>
<text x="259.08" y="127" size="1.778" layer="97">CH6: GPIO5</text>
<text x="259.08" y="88.9" size="1.778" layer="97">CH7: GPIO18</text>
<text x="259.08" y="50.8" size="1.778" layer="97">CH8: GPIO19</text>
<frame x1="0" y1="0" x2="350.52" y2="256.54" columns="8" rows="5" layer="94">
<segment><pinref part="FRAME1" gate="G$1" pin="1"/></segment>
</frame>
<text x="271.78" y="7.62" size="3.81" layer="94">8-Channel LED Dimmer</text>
<text x="271.78" y="2.54" size="2.54" layer="94">Rev 2.0</text>
</plain>
<instances>
<!-- Power Input -->
<instance part="J1" gate="G$1" x="33.02" y="165.1"/>

<!-- ESP32 -->
<instance part="U1" gate="G$1" x="106.68" y="139.7"/>

<!-- Regulator -->
<instance part="U2" gate="G$1" x="55.88" y="121.92"/>
<instance part="C1" gate="G$1" x="43.18" y="111.76"/>
<instance part="C2" gate="G$1" x="68.58" y="111.76"/>

<!-- Status LED -->
<instance part="D1" gate="G$1" x="38.1" y="81.28"/>
<instance part="C3" gate="G$1" x="53.34" y="76.2"/>

<!-- Channel 1 -->
<instance part="R1" gate="G$1" x="175.26" y="157.48"/>
<instance part="R9" gate="G$1" x="187.96" y="149.86" rot="R90"/>
<instance part="Q1" gate="G$1" x="195.58" y="157.48"/>
<instance part="J2" gate="G$1" x="215.9" y="157.48"/>

<!-- Channel 2 -->
<instance part="R2" gate="G$1" x="175.26" y="119.38"/>
<instance part="R10" gate="G$1" x="187.96" y="111.76" rot="R90"/>
<instance part="Q2" gate="G$1" x="195.58" y="119.38"/>
<instance part="J3" gate="G$1" x="215.9" y="119.38"/>

<!-- Channel 3 -->
<instance part="R3" gate="G$1" x="175.26" y="81.28"/>
<instance part="R11" gate="G$1" x="187.96" y="73.66" rot="R90"/>
<instance part="Q3" gate="G$1" x="195.58" y="81.28"/>
<instance part="J4" gate="G$1" x="215.9" y="81.28"/>

<!-- Channel 4 -->
<instance part="R4" gate="G$1" x="175.26" y="43.18"/>
<instance part="R12" gate="G$1" x="187.96" y="35.56" rot="R90"/>
<instance part="Q4" gate="G$1" x="195.58" y="43.18"/>
<instance part="J5" gate="G$1" x="215.9" y="43.18"/>

<!-- Channel 5 -->
<instance part="R5" gate="G$1" x="264.16" y="157.48"/>
<instance part="R13" gate="G$1" x="276.86" y="149.86" rot="R90"/>
<instance part="Q5" gate="G$1" x="284.48" y="157.48"/>
<instance part="J8" gate="G$1" x="304.8" y="157.48"/>

<!-- Channel 6 -->
<instance part="R6" gate="G$1" x="264.16" y="119.38"/>
<instance part="R14" gate="G$1" x="276.86" y="111.76" rot="R90"/>
<instance part="Q6" gate="G$1" x="284.48" y="119.38"/>
<instance part="J9" gate="G$1" x="304.8" y="119.38"/>

<!-- Channel 7 -->
<instance part="R7" gate="G$1" x="264.16" y="81.28"/>
<instance part="R15" gate="G$1" x="276.86" y="73.66" rot="R90"/>
<instance part="Q7" gate="G$1" x="284.48" y="81.28"/>
<instance part="J10" gate="G$1" x="304.8" y="81.28"/>

<!-- Channel 8 -->
<instance part="R8" gate="G$1" x="264.16" y="43.18"/>
<instance part="R16" gate="G$1" x="276.86" y="35.56" rot="R90"/>
<instance part="Q8" gate="G$1" x="284.48" y="43.18"/>
<instance part="J11" gate="G$1" x="304.8" y="43.18"/>

<!-- Button Inputs -->
<instance part="R17" gate="G$1" x="33.02" y="45.72" rot="R90"/>
<instance part="J6" gate="G$1" x="33.02" y="30.48"/>
<instance part="R18" gate="G$1" x="50.8" y="45.72" rot="R90"/>
<instance part="J7" gate="G$1" x="50.8" y="30.48"/>
</instances>
<busses>
</busses>
<nets>
<net name="GND" class="0">
<segment>
<pinref part="J1" gate="G$1" pin="2"/>
<wire x1="25.4" y1="162.56" x2="20.32" y2="162.56" width="0.1524" layer="91"/>
<label x="15.24" y="162.56" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="U1" gate="G$1" pin="GND@2"/>
<wire x1="106.68" y1="99.06" x2="106.68" y2="93.98" width="0.1524" layer="91"/>
<label x="106.68" y="88.9" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="U2" gate="G$1" pin="GND"/>
<wire x1="55.88" y1="111.76" x2="55.88" y2="106.68" width="0.1524" layer="91"/>
<label x="55.88" y="101.6" size="1.778" layer="95"/>
</segment>
</net>
<net name="VIN" class="0">
<segment>
<pinref part="J1" gate="G$1" pin="1"/>
<wire x1="25.4" y1="167.64" x2="20.32" y2="167.64" width="0.1524" layer="91"/>
<label x="15.24" y="167.64" size="1.778" layer="95"/>
</segment>
</net>
<net name="3V3" class="0">
<segment>
<pinref part="U2" gate="G$1" pin="VOUT"/>
<wire x1="68.58" y1="124.46" x2="73.66" y2="124.46" width="0.1524" layer="91"/>
<label x="76.2" y="124.46" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="U1" gate="G$1" pin="3V3"/>
<wire x1="83.82" y1="172.72" x2="78.74" y2="172.72" width="0.1524" layer="91"/>
<label x="73.66" y="172.72" size="1.778" layer="95"/>
</segment>
</net>
<net name="PWM_CH1" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO25"/>
<wire x1="83.82" y1="152.4" x2="78.74" y2="152.4" width="0.1524" layer="91"/>
<label x="68.58" y="152.4" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="R1" gate="G$1" pin="1"/>
<wire x1="170.18" y1="157.48" x2="165.1" y2="157.48" width="0.1524" layer="91"/>
<label x="154.94" y="157.48" size="1.778" layer="95"/>
</segment>
</net>
<net name="PWM_CH2" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO26"/>
<wire x1="83.82" y1="149.86" x2="78.74" y2="149.86" width="0.1524" layer="91"/>
<label x="68.58" y="149.86" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="R2" gate="G$1" pin="1"/>
<wire x1="170.18" y1="119.38" x2="165.1" y2="119.38" width="0.1524" layer="91"/>
<label x="154.94" y="119.38" size="1.778" layer="95"/>
</segment>
</net>
<net name="PWM_CH3" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO27"/>
<wire x1="83.82" y1="147.32" x2="78.74" y2="147.32" width="0.1524" layer="91"/>
<label x="68.58" y="147.32" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="R3" gate="G$1" pin="1"/>
<wire x1="170.18" y1="81.28" x2="165.1" y2="81.28" width="0.1524" layer="91"/>
<label x="154.94" y="81.28" size="1.778" layer="95"/>
</segment>
</net>
<net name="PWM_CH4" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO14"/>
<wire x1="83.82" y1="144.78" x2="78.74" y2="144.78" width="0.1524" layer="91"/>
<label x="68.58" y="144.78" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="R4" gate="G$1" pin="1"/>
<wire x1="170.18" y1="43.18" x2="165.1" y2="43.18" width="0.1524" layer="91"/>
<label x="154.94" y="43.18" size="1.778" layer="95"/>
</segment>
</net>
<net name="PWM_CH5" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO4"/>
<wire x1="129.54" y1="172.72" x2="134.62" y2="172.72" width="0.1524" layer="91"/>
<label x="137.16" y="172.72" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="R5" gate="G$1" pin="1"/>
<wire x1="259.08" y1="157.48" x2="254" y2="157.48" width="0.1524" layer="91"/>
<label x="243.84" y="157.48" size="1.778" layer="95"/>
</segment>
</net>
<net name="PWM_CH6" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO5"/>
<wire x1="129.54" y1="165.1" x2="134.62" y2="165.1" width="0.1524" layer="91"/>
<label x="137.16" y="165.1" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="R6" gate="G$1" pin="1"/>
<wire x1="259.08" y1="119.38" x2="254" y2="119.38" width="0.1524" layer="91"/>
<label x="243.84" y="119.38" size="1.778" layer="95"/>
</segment>
</net>
<net name="PWM_CH7" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO18"/>
<wire x1="129.54" y1="162.56" x2="134.62" y2="162.56" width="0.1524" layer="91"/>
<label x="137.16" y="162.56" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="R7" gate="G$1" pin="1"/>
<wire x1="259.08" y1="81.28" x2="254" y2="81.28" width="0.1524" layer="91"/>
<label x="243.84" y="81.28" size="1.778" layer="95"/>
</segment>
</net>
<net name="PWM_CH8" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO19"/>
<wire x1="129.54" y1="160.02" x2="134.62" y2="160.02" width="0.1524" layer="91"/>
<label x="137.16" y="160.02" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="R8" gate="G$1" pin="1"/>
<wire x1="259.08" y1="43.18" x2="254" y2="43.18" width="0.1524" layer="91"/>
<label x="243.84" y="43.18" size="1.778" layer="95"/>
</segment>
</net>
<net name="WS2812_DIN" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO16"/>
<wire x1="129.54" y1="170.18" x2="134.62" y2="170.18" width="0.1524" layer="91"/>
<label x="137.16" y="170.18" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="D1" gate="G$1" pin="DIN"/>
<wire x1="25.4" y1="81.28" x2="20.32" y2="81.28" width="0.1524" layer="91"/>
<label x="10.16" y="81.28" size="1.778" layer="95"/>
</segment>
</net>
<net name="BTN1" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO32"/>
<wire x1="83.82" y1="157.48" x2="78.74" y2="157.48" width="0.1524" layer="91"/>
<label x="68.58" y="157.48" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="R17" gate="G$1" pin="1"/>
<pinref part="J6" gate="G$1" pin="1"/>
<wire x1="33.02" y1="40.64" x2="33.02" y2="33.02" width="0.1524" layer="91"/>
<label x="35.56" y="38.1" size="1.778" layer="95"/>
</segment>
</net>
<net name="BTN2" class="0">
<segment>
<pinref part="U1" gate="G$1" pin="GPIO33"/>
<wire x1="83.82" y1="154.94" x2="78.74" y2="154.94" width="0.1524" layer="91"/>
<label x="68.58" y="154.94" size="1.778" layer="95"/>
</segment>
<segment>
<pinref part="R18" gate="G$1" pin="1"/>
<pinref part="J7" gate="G$1" pin="1"/>
<wire x1="50.8" y1="40.64" x2="50.8" y2="33.02" width="0.1524" layer="91"/>
<label x="53.34" y="38.1" size="1.778" layer="95"/>
</segment>
</net>
</nets>
</sheet>
</sheets>
</schematic>
</drawing>
</eagle>
