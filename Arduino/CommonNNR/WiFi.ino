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
//TODO: Should be removed from AzureClient library once debuggin is done.

void initWifiDevice(int wifiSet) {   //wifiset always zero at the moment. But in time, it will be used for retries at different wifi ssid

	wifiDevice.wifiPairs = 4;		 // nbr of sets below. If e.g. set to 1, only first set is tried.
	switch (wifiSet) {
	case 2:
		wifiDevice.ssid = "Nik Z5";
		wifiDevice.pwd = "RASMUSSEN";
		break;
	case 1:
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
	case 7:
		wifiDevice.ssid = "TeliaGateway58-98-35-B5-DB-17";
		wifiDevice.pwd = "E5B004E62E";
		break;
	other:
		break;

	}
}

int initWifi() {
	const int WifiTimeoutMilliseconds = 60000;  // 60 seconds
	const int MaxRetriesWithSamePwd = 20;
	int MaxLoopsThroughAll = 5;
	int retry;

	WiFi.mode(WIFI_STA);  // Ensure WiFi in Station/Client Mode
	
	if (WiFi.status() == WL_NO_SHIELD) {
		Serial.println("WiFi shield not present");
		while (true);  // don't continue
	}

	if (WiFi.status() == WL_CONNECTED) {
		return true;
	}
	else {
		while (MaxLoopsThroughAll-- >= 0) {
			wifiDevice.WifiIndex = 0;
			Serial.println("Not connected. Trying all known wifi hotspots.");

			while (++wifiDevice.WifiIndex <= wifiDevice.wifiPairs) {

				if (wifiDevice.LastWifiTime > millis()) { delay(500); }

				initWifiDevice(wifiDevice.WifiIndex);
				Serial.print("initWifi: trying " + wifiDevice.ssid);
				WiFi.begin(wifiDevice.ssid.c_str(), wifiDevice.pwd.c_str());

				// NNR: We should not proceed before we are connected to a wifi
				delay(500);
				retry = 0;
				while (WiFi.status() != WL_CONNECTED && retry++ < MaxRetriesWithSamePwd) {
					delay(500);
					Serial.print(".");
				}

				if (WiFi.status() == WL_CONNECTED) {
					Serial.println(" => connected OK");
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
	int ntpRetryCount = 0;
	while (timeStatus() == timeNotSet && ++ntpRetryCount < 3) { // get NTP time
																 //Serial.println(WiFi.localIP());
		setSyncProvider(getNtpTime);
		setSyncInterval(60 * 60);
	}
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

