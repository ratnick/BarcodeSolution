#include <WiFiUDP.h>
#include <WiFiServer.h>
#include <WiFiClientSecure.h>
#include <WiFiClient.h>
#include <ESP8266WiFiType.h>
#include <ESP8266WiFiSTA.h>
#include <ESP8266WiFiScan.h>
//#include <ESP8266WiFiMulti.h>
#include <ESP8266WiFiGeneric.h>
#include <ESP8266WiFiAP.h>
#include <ESP8266WiFi.h>
#include <ESP8266WebServer.h>
#include <String.h>
//TODO: Should be removed from AzureClient library once debuggin is done.

void initWifiDevice(int wifiSet) {   //wifiset always zero at the moment. But in time, it will be used for retries at different wifi ssid

	wifiDevice.wifiPairs = 1;		 // nbr of sets below. If e.g. set to 1, only first set is tried.
	switch (wifiSet) {
	case 1:
		wifiDevice.ssid = "Nik Z5";
		wifiDevice.pwd = "RASMUSSEN";
		break;
	case 2:
		wifiDevice.ssid = "nohrTDC";
		wifiDevice.pwd = "RASMUSSEN";
		break;
	case 3:
		wifiDevice.ssid = "nohrTDC_5GHz";
		wifiDevice.pwd = "RASMUSSEN";
		break;
	case 4:
		wifiDevice.ssid = "nohrTDC_2GEXT";
		wifiDevice.pwd = "RASMUSSEN";
		break;
	case 5:
		wifiDevice.ssid = "nnr router";
		wifiDevice.pwd = "RASMUSSEN";
		break;
	case 6:
		wifiDevice.ssid = "NikSession";
		wifiDevice.pwd = "rasmussen";
		break;
	other:
		break;

	}
}

boolean IsAlreadyConnected() {

	wifiDevice.WifiIndex = 0;
	initWifiDevice(wifiDevice.WifiIndex);

	String lastSSID = String(WiFi.SSID());
	String curSSID = String(wifiDevice.ssid);

	Serial.printf("\n CheckIfAlreadyConnected\n Current SSID = '%s'    Last SSID = '%s'    Continue without reconnecting? ", curSSID.c_str(), lastSSID.c_str());

	if (lastSSID != "" && lastSSID.equals(curSSID)) {
		Serial.println("TRUE");
		return true;
	}
	else {
		Serial.println("FALSE");
		return false;
	}
}

int initWifi() {

	const int WifiTimeoutMilliseconds = 60000;  // 60 seconds
	const int MaxRetriesWithSamePwd = 40;
	int MaxLoopsThroughAll = 5;
	int retry;

	if (WiFi.status() == WL_NO_SHIELD) {
		Serial.println("WiFi shield not present");
		while (true);  // don't continue
	}

	if (IsAlreadyConnected()) {
		return true;   // has not yet proven to work.
	}

	if (WiFi.status() == WL_CONNECTED) {
		Serial.println("WiFi already connected (WL_CONNECTED)");
		return true;
	}
	else {
		
		WiFi.mode(WIFI_STA);  // Ensure WiFi in Station/Client Mode
		#ifdef DEBUG_PRINT
			listNetworks();
		#endif

		while (MaxLoopsThroughAll-- >= 0) {
			wifiDevice.WifiIndex = 0;
			Serial.println("Not connected. Trying all known wifi hotspots.");

			while (++wifiDevice.WifiIndex <= wifiDevice.wifiPairs) {

				if (wifiDevice.LastWifiTime > millis()) { delay(500); }

				initWifiDevice(wifiDevice.WifiIndex);
				Serial.print("initWifi: trying " + wifiDevice.ssid);
				if (WiFi.status() != WL_CONNECTED) {
					WiFi.disconnect();
					WiFi.begin(wifiDevice.ssid.c_str(), wifiDevice.pwd.c_str());
					Serial.printf("\nTry: %s / %s ", wifiDevice.ssid.c_str(), wifiDevice.pwd.c_str());
				}

				// NNR: We should not proceed before we are connected to a wifi
				retry = 0;
				while (WiFi.status() != WL_CONNECTED && retry++ < MaxRetriesWithSamePwd) {
					delay(500);
					Serial.print(WiFi.status());
				}

				if (WiFi.status() == WL_CONNECTED) {
					PrintIPAddress();
					Serial.println(" => connected OK");
					WiFi.setAutoConnect(true);
						
					return true;
				}
				else {
					Serial.println(" could not connect");
					wifiDevice.WiFiConnectAttempts++;
					wifiDevice.LastWifiTime = millis() + WifiTimeoutMilliseconds;
					//if (wifiDevice.WifiIndex++ > wifiDevice.wifiPairs) { wifiDevice.WifiIndex = 1; }
				}
			}
		}
	}

	Serial.println("*** Could not connect to Wifi at all. Try 1) power cycling. 2) look if your SSID is defined in the list.");
	while (true);  // don't continue

}

int initWifiDEBUG() {

	WiFi.mode(WIFI_STA);  // Ensure WiFi in Station/Client Mode 
	listNetworks();

	while (WiFi.status() != WL_CONNECTED) {
		//WiFi.mode(WIFI_STA);  // Ensure WiFi in Station/Client Mode
		//WiFi.mode(WIFI_OFF);  // https://github.com/esp8266/Arduino/issues/2702

		if (WiFi.status() != WL_CONNECTED) {
					
			// https://github.com/esp8266/Arduino/issues/2702
			// make sure WiFi is off until just before trying to connect.
			//WiFi.mode(WIFI_OFF);  // https://github.com/esp8266/Arduino/issues/2702
			//WiFi.disconnect(); 

			Serial.print("Try to connect:");
			WiFi.begin("nohrTDC", "RASMUSSEN");
		} 

		while (WiFi.status() != WL_CONNECTED) {
			delay(500);
			Serial.print(".");
		}

	}
	PrintIPAddress();
	return true;

}

void listNetworks() {
  // scan for nearby networks:
  Serial.println("** Scan Networks **");
  int numSsid = WiFi.scanNetworks();
  if (numSsid == -1) {
    Serial.println("Couldn't get a wifi connection");
    while (true);
  }

  // print the list of networks seen:
  Serial.print("number of available networks:");
  Serial.println(numSsid);

  // print the network number and name for each network found:
  for (int thisNet = 0; thisNet < numSsid; thisNet++) {
    Serial.print(thisNet);
    Serial.print(") ");
    Serial.print(WiFi.SSID(thisNet));
    Serial.print("\tSignal: ");
    Serial.println(WiFi.RSSI(thisNet));

  }
}

void PrintIPAddress() {

	int ipAddress;
	byte ipQuads[4];

	ipAddress = WiFi.localIP();
	ipQuads[0] = (byte)(ipAddress & 0xFF);;
	ipQuads[1] = (byte)((ipAddress >> 8) & 0xFF);
	ipQuads[2] = (byte)((ipAddress >> 16) & 0xFF);
	ipQuads[3] = (byte)((ipAddress >> 24) & 0xFF);

	//print the local IP address
	Serial.println("Connected with ip address: " + String(ipQuads[0]) + "." + String(ipQuads[1]) + "." + String(ipQuads[2]) + "." + String(ipQuads[3]));

}

void getCurrentTime() {
	setSyncProvider(getNtpTime);
	setSyncInterval(60 * 60);
	Serial.println(GetISODateTime());
}


// For setting up hotspot:
/* Set these to your desired credentials. */
const char *ssid = "NNR D1";
const char *password = "RASMUSSEN";
ESP8266WebServer server(80);

void initWifiAccesspoint() {
	delay(1000);
	Serial.begin(115200);
	Serial.println();
	Serial.print("Configuring access point...");
	/* You can remove the password parameter if you want the AP to be open. */
	WiFi.softAP(ssid, password);

	IPAddress myIP = WiFi.softAPIP();
	Serial.print("AP IP address: ");
	Serial.println(myIP);
	server.on("/", handleRoot);
	server.begin();
	Serial.println("HTTP server started");
}

void StartWifiAccesspoint() {
	server.handleClient();
}

/* Just a little test message.  Go to http://192.168.4.1 in a web browser
* connected to this access point to see it.
*/

void handleRoot() {
	server.send(200, "text/html", "<h1>You are connected</h1>");
}

/*
void getCurrentTimeORG() {
	int ntpRetryCount = 0;
	while (timeStatus() == timeNotSet && ++ntpRetryCount < 6) { // get NTP time
																//Serial.println(WiFi.localIP());
		setSyncProvider(getNtpTime);
		setSyncInterval(60 * 60);
	}
}
*/
