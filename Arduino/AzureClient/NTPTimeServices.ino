#include <WiFiUdp.h>

static WiFiUDP Udp;
unsigned int localPort = 2390; // 8888;  // local port to listen for UDP packets
const int timeZone = 0; 
char isoTime[30];

const int NTP_PACKET_SIZE = 48; // NTP time is in the first 48 bytes of message
byte packetBuffer[NTP_PACKET_SIZE]; //buffer to hold incoming & outgoing packets

time_t getNtpTime()
{
	boolean time_found = false;
	time_t utcTime;
	int outerLoopcnt = 0;
	int innerLoopcnt = 0;

	Udp.begin(localPort);  // call once

	while (!time_found && (++outerLoopcnt < 5)) {

		Serial.printf("\nRequest NTP time. Round %d ", outerLoopcnt);
		sendNTPpacket(timeServer); // send an NTP packet to a time server
								   // wait to see if a reply is available
		delay(1000);

		innerLoopcnt = 0;
		while (!time_found && (++innerLoopcnt < 16)) {
			if (Udp.parsePacket()) {
				Serial.println("- UDP packet received");
				// We've received a packet, read the data from it
				Udp.read(packetBuffer, NTP_PACKET_SIZE); // read the packet into the buffer
				//UTCtesting(packetBuffer);
				time_found = true;
				return ConvertToTime(packetBuffer);
			}
			else {
				delay(1000);
				Serial.print("x");
			}
		}
	}
	Serial.printf("\n\n*** Gave up on retrieving NTC time!!\n\n");
	return 0; 
}

time_t ConvertToTime(byte packetBuffer[]) {
	unsigned long secsSince1900;
	// convert four bytes starting at location 40 to a long integer
	secsSince1900 = (unsigned long)packetBuffer[40] << 24;
	secsSince1900 |= (unsigned long)packetBuffer[41] << 16;
	secsSince1900 |= (unsigned long)packetBuffer[42] << 8;
	secsSince1900 |= (unsigned long)packetBuffer[43];
	return secsSince1900 - 2208988800UL + timeZone * SECS_PER_HOUR;
}

void UTCtesting(byte packetBuffer[]) {

	// for testing and printouts
	// combine the four bytes (two words) into a long integer
	// this is NTP time (seconds since Jan 1 1900):

	//the timestamp starts at byte 40 of the received packet and is four bytes,
	// or two words, long. First, esxtract the two words:
	unsigned long highWord = word(packetBuffer[40], packetBuffer[41]);
	unsigned long lowWord = word(packetBuffer[42], packetBuffer[43]);

	unsigned long secsSince1900 = highWord << 16 | lowWord;
	Serial.print("Seconds since Jan 1 1900 = ");
	Serial.println(secsSince1900);

	// now convert NTP time into everyday time:
	Serial.print("Unix time = ");
	// Unix time starts on Jan 1 1970. In seconds, that's 2208988800:
	const unsigned long seventyYears = 2208988800UL;
	// subtract seventy years:
	unsigned long epoch = secsSince1900 - seventyYears;
	// print Unix time:
	Serial.println(epoch);


	// print the hour, minute and second:
	Serial.print("The UTC time is ");       // UTC is the time at Greenwich Meridian (GMT)
	Serial.print((epoch % 86400L) / 3600); // print the hour (86400 equals secs per day)
	Serial.print(':');
	if (((epoch % 3600) / 60) < 10) {
	// In the first 10 minutes of each hour, we'll want a leading '0'
	Serial.print('0');
	}
	Serial.print((epoch % 3600) / 60); // print the minute (3600 equals secs per minute)
	Serial.print(':');
	if ((epoch % 60) < 10) {
	// In the first 10 seconds of each minute, we'll want a leading '0'
	Serial.print('0');
	}
	Serial.println(epoch % 60); // print the second
}



// send an NTP request to the time server at the given address
//void sendNTPpacket(IPAddress &address)
void sendNTPpacket(const char *host)
{
  // set all bytes in the buffer to 0
  memset(packetBuffer, 0, NTP_PACKET_SIZE);
  // Initialize values needed to form NTP request
  // (see URL above for details on the packets)
  packetBuffer[0] = 0b11100011;   // LI, Version, Mode
  packetBuffer[1] = 0;     // Stratum, or type of clock
  packetBuffer[2] = 6;     // Polling Interval
  packetBuffer[3] = 0xEC;  // Peer Clock Precision
  // 8 bytes of zero for Root Delay & Root Dispersion
  packetBuffer[12]  = 49;
  packetBuffer[13]  = 0x4E;
  packetBuffer[14]  = 49;
  packetBuffer[15]  = 52;
  // all NTP fields have been given values, now
  // you can send a packet requesting a timestamp:                 
  Udp.beginPacket(host, 123); //NTP requests are to port 123
  Udp.write(packetBuffer, NTP_PACKET_SIZE);
  Udp.endPacket();
}

char* GetISODateTime() {
	sprintf(isoTime, "ISO UTC time: %4d-%02d-%02dT%02d:%02d:%02d", year(), month(), day(), hour(), minute(), second());
	return isoTime;
}

