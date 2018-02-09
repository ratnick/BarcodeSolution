
void initFlashLED(){
	pinMode(LED_PIN, OUTPUT);
}

void LED_ON()
{
	digitalWrite(LED_PIN, HIGH);
}

void LED_OFF()
{
	digitalWrite(LED_PIN, LOW);
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