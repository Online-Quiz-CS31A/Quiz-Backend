#include <Adafruit_Fingerprint.h>
#include <HardwareSerial.h>

// Set up Fingerprint Sensor on ESP32 Serial2 (RX2 = GPIO 16, TX2 = GPIO 17)
HardwareSerial mySerial(2);
#define FINGERPRINT_RX 16
#define FINGERPRINT_TX 17
Adafruit_Fingerprint finger = Adafruit_Fingerprint(&mySerial);

void setup() {
  Serial.begin(115200);
  while (!Serial); 
  delay(100);
  
  // Use the pins you defined (16 and 17)
  mySerial.begin(57600, SERIAL_8N1, FINGERPRINT_RX, FINGERPRINT_TX); 
  
  if (finger.verifyPassword()) {
    Serial.println("Found fingerprint sensor!");
  } else {
    Serial.println("Did not find fingerprint sensor :(");
    while (1) { delay(1); }
  }

  Serial.println("Commands:");
  Serial.println("Type 'E <ID>' to Enroll (e.g., E 5)");
  Serial.println("Type 'V <ID>' to Verify (e.g., V 5)");
}

void loop() {
  if (Serial.available() > 0) {
    char command = Serial.read();
    if (command == '\n' || command == '\r' || command == ' ') return; 

    int id = Serial.parseInt(); 

    if (id <= 0 || id > 127) {
      Serial.println("Invalid ID. Please use 1-127.");
      return;
    }

    if (command == 'E' || command == 'e') {
      handleEnroll(id);
    } 
    else if (command == 'V' || command == 'v') {
      handleVerify(id);
    }
  }
}

// --- PHASE 1: ENROLLMENT ---
void handleEnroll(int slotId) {
  Serial.println("----------------------------------");
  Serial.print("ENROLLING TO SLOT: "); Serial.println(slotId);
  
  // Scan 1
  Serial.println("Place finger...");
  while (finger.getImage() != FINGERPRINT_OK);
  finger.image2Tz(1);
  
  Serial.println("Remove finger...");
  delay(2000);
  while (finger.getImage() != FINGERPRINT_NOFINGER);
  
  // Scan 2
  Serial.println("Place same finger again...");
  while (finger.getImage() != FINGERPRINT_OK);
  finger.image2Tz(2);

  // Create Model and Save
  if (finger.createModel() == FINGERPRINT_OK) {
    if (finger.storeModel(slotId) == FINGERPRINT_OK) {
      Serial.println("SUCCESS: Fingerprint saved!");
    } else {
      Serial.println("ERROR: Could not save to slot.");
    }
  } else {
    Serial.println("ERROR: Prints did not match.");
  }
  Serial.println("----------------------------------");
}

// --- PHASE 2: VERIFICATION (FIXED) ---
void handleVerify(int targetId) {
  Serial.println("----------------------------------");
  Serial.print("VERIFYING AGAINST SLOT: "); Serial.println(targetId);
  Serial.println("Waiting for finger...");

  while (finger.getImage() != FINGERPRINT_OK);
  
  // Convert image to features in Buffer 1
  if (finger.image2Tz(1) != FINGERPRINT_OK) {
    Serial.println("Conversion Error.");
    return;
  }

  // Use the name the compiler suggested: fingerSearch()
  // This searches the sensor memory for a match to Buffer 1
  int p = finger.fingerSearch(); 
  
  if (p == FINGERPRINT_OK) {
    // If the ID found by the sensor matches our Target ID from the "Backend"
    if (finger.fingerID == targetId) {
      Serial.print("ACCESS GRANTED! Match confirmed for ID #");
      Serial.println(finger.fingerID);
    } else {
      Serial.print("ACCESS DENIED! Wrong finger. (Found ID #");
      Serial.print(finger.fingerID);
      Serial.println(" instead)");
    }
  } else {
    Serial.println("ACCESS DENIED! No match found in memory.");
  }
  Serial.println("----------------------------------");
}