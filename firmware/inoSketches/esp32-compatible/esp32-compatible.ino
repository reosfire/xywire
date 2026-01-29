#include "FastLED.h"
#include <WiFi.h>
#include <WiFiUdp.h>
#include <Preferences.h>
#include <BluetoothSerial.h>

#define NUM_LEDS 197
#define PIN 4

#define WIFI_CONNECTION_RETR

String ssid;
String password;

CRGB leds[NUM_LEDS];
Preferences preferences;
BluetoothSerial SerialBT;

WiFiUDP udp;
bool udpStarted = false;
unsigned int localUdpPort = 25565; // Port to listen on

void setup() {
  FastLED.addLeds<WS2812B, PIN, GRB>(leds, NUM_LEDS).setCorrection(TypicalLEDStrip);
  FastLED.setBrightness(100);

  WiFi.mode(WIFI_STA);
  SerialBT.begin("ChristmasLights");

  preferences.begin("wifi-config", false);
  String storedSSID = preferences.getString("ssid", "");
  String storedPassword = preferences.getString("password", "");
  
  if (storedSSID.length() > 0) {
    ssid = storedSSID;
    password = storedPassword;
    WiFi.begin(ssid.c_str(), password.c_str());
    udp.begin(localUdpPort);
  }
}

char packet[1 + 4 + 3 * NUM_LEDS]; // 1 byte for type 4 bytes for generation + (3 * NUM_LEDS) max data
int indexInPacket = 0;

char wifiReader() {
  return packet[indexInPacket++];
}

void wifiWriter(char* chars, size_t size) {
  udp.beginPacket(udp.remoteIP(), udp.remotePort());
  udp.write((uint8_t*)chars, size);
  udp.endPacket();
}

char btReader() {
  return SerialBT.read();
}

void btWriter(char* chars, size_t size) {
  SerialBT.write((uint8_t*)chars, size);
}

void loop() {
  if (WiFi.status() == WL_CONNECTED) {
    int packetSize = udp.parsePacket();
    if (packetSize > 0) {
      udp.read(packet, packetSize);
      indexInPacket = 0;

      handlePacket(&wifiReader, &wifiWriter);
    }
  }

  if (SerialBT.available() > 0) {
    handlePacket(&btReader, &btWriter);
  }

  FastLED.show();
}

void handlePacket(char (*packetReader)(), void (*responseWriter)(char*, size_t)) {
  char type = packetReader();
  switch (type) {
    case 1: // Brightness Packet
      onSetBrightnessPacket(packetReader, responseWriter);
      break;
    case 2: // Data Packet
      onDataPacket(packetReader);
      break;
    case 3: // Clear Packet
      onClearPacket(responseWriter);
      break;
    case 4: // WiFi Config Packet
      onWiFiConfigPacket(packetReader, responseWriter);
      break;
  }
}

unsigned int lastAcceptedGeneration = 0;

void onDataPacket(char (*packetReader)()) {
  unsigned int generation = getGeneration(packetReader);
  if (generation < lastAcceptedGeneration) return;

  for (int i = 0; i < NUM_LEDS; i++) {
    char a = packetReader();
    char b = packetReader();
    char c = packetReader();
    leds[i] = CRGB(a, b, c);
  }

  lastAcceptedGeneration = generation;
}

void onSetBrightnessPacket(char (*packetReader)(), void (*responseWriter)(char*, size_t)) {
  FastLED.setBrightness(packetReader());
  sendAck(responseWriter);
}

void onClearPacket(void (*responseWriter)(char*, size_t)) {
  fill_solid(leds, NUM_LEDS, CRGB(0, 0, 0));
  lastAcceptedGeneration = 0;
  sendAck(responseWriter);
}

void sendAck(void (*responseWriter)(char*, size_t)) {
  responseWriter((char[]){255}, 1);
}

void onWiFiConfigPacket(char (*packetReader)(), void (*responseWriter)(char*, size_t)) {
  String newSSID = getString(packetReader);
  String newPassword = getString(packetReader);
  
  // Store credentials persistently
  preferences.putString("ssid", newSSID);
  preferences.putString("password", newPassword);
  
  ssid = newSSID;
  password = newPassword;
  
  sendAck(responseWriter);
  
  // Reconnect to WiFi with new credentials
  WiFi.disconnect();
  WiFi.begin(ssid.c_str(), password.c_str());
  udp.begin(localUdpPort);
}

String getString(char (*packetReader)()) {
  String result = "";
  char c;

  while (true) {
      c = packetReader();
      if (c == '\0') {
          break;
      }
      result += c;
  }

  return result;
}

unsigned int getGeneration(char (*packetReader)()) {
    return static_cast<unsigned int>(packetReader()) |
           (static_cast<unsigned int>(packetReader()) << 8) |
           (static_cast<unsigned int>(packetReader()) << 16) |
           (static_cast<unsigned int>(packetReader()) << 24);
}
