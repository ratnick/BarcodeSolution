
void initFlashLED(){
	pinMode(LED_PIN, OUTPUT);
}

void LED_ON()
{
	digitalWrite(LED_PIN, LOW);
}

void LED_OFF()
{
	digitalWrite(LED_PIN, HIGH);
}

void LED_Flashes(int count, int blinkDelayMs)
{
	for (int i = 0; i < count; i++) {
		LED_ON();
		delay(blinkDelayMs);
		LED_OFF();
		delay(blinkDelayMs);
	}
}

void initPushButton() {
	pinMode(PUSHBUTTON_PIN, INPUT);
}

void waitUntilButtonPushed() {
	Serial.print("\nWait for button to be pressed");
	while (!digitalRead(PUSHBUTTON_PIN)) {
		delay(50);
	}
	Serial.println("...done");
}

