// Set-up low-level interrupts for most accurate BPM math.
#define USE_ARDUINO_INTERRUPTS true
#include <PulseSensorPlayground.h>

// Define variables
PulseSensorPlayground pulseSensor;
int nPulsePin = A0;
int nLEDPin = 13;
int nThreshold = 675;

//-----------------------------------------------------------------------------
//                                  BeatSync
//                                  --------
//
// General  : This code is a part of the BeatSync project and is written
//            for Arduino Nano with a bluetooth adapter and a hearbeat sensor.
//
// Input    : User lays their finger on the hearbeat sensor. Sensor then
//            calculates the user's BPM based on the difference between the
//            impulses that go above the given threshold.
//
// Output   : Program takes the calculated BPM and transmits it as a byte
//            over bluetooth.
//
//-----------------------------------------------------------------------------
// Programmer : Vlad Poberezhny
// Date : 03.04.2018
//-----------------------------------------------------------------------------
void setup()
{
  // Begin serial at 9600 baud
  // Bluetooth adapter can only transmit at 9600 baud or lower
  Serial.begin(9600);

  // Set-up the pulse sensor
  pulseSensor.analogInput(nPulsePin);
  pulseSensor.blinkOnPulse(nLEDPin);
  pulseSensor.setThreshold(nThreshold);

  // Begin reading
  pulseSensor.begin();
}

void loop()
{
  // If heartbeat was detected
  if (pulseSensor.sawStartOfBeat())
  {
    // Convert to BPM, transmit calculation as byte
    Serial.write(pulseSensor.getBeatsPerMinute());
  }

  // Small delay is a good practice
  delay(10);
}
