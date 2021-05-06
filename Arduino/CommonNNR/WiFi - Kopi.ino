void initWifiCredentials(String &wifiname, String &wifipwd) {
	WiFi.mode(WIFI_STA);  // Ensure WiFi in Station/Client Mode

	if (AT_HOME) {
		wifiname = "nohrTDC";
		wifipwd = "RASMUSSEN";
	}
	else if (XPERIAHOTSPOT) {
		wifiname = "Nik Z5";
		wifipwd = "RASMUSSEN";
	}
	else {
		wifiname = "TeliaGateway58-98-35-B5-DB-17";
		wifipwd = "E5B004E62E";
	}
	Serial.print("initWifiCredentials: wifiname=");
	Serial.println(wifiname);
}

void initWifiDevice(
	const char *ssid,
	const char *pwd) {
	wifiDevice.wifiPairs = 1;
	wifiDevice.ssid = new const char*[wifiDevice.wifiPairs];
	wifiDevice.pwd = new const char*[wifiDevice.wifiPairs];
	wifiDevice.ssid[0] = ssid;
	wifiDevice.pwd[0] = pwd;
}


int initWifi(){
  const int WifiTimeoutMilliseconds = 60000;  // 60 seconds
  const int MaxRetriesWithSamePwd = 10;
  int retry;

//  Serial.print("initWifi:  WiFi.status="); 
//  Serial.println(WiFi.status());

  // check for the presence of the shield:
  if (WiFi.status() == WL_NO_SHIELD) {
	  Serial.println("WiFi shield not present");
	  // don't continue:
	  while (true);
  }

  if (WiFi.status() == WL_CONNECTED) {
	  return true;
  }
  else {

	  Serial.println("Not connected. Trying ");
//	  Serial.println(String(wifiDevice.WifiIndex));
//	  Serial.println(String(wifiDevice.ssid[0]));
	  if (wifiDevice.LastWifiTime > millis()) {
		  delay(500);
		  return true;
	  }

	  if (wifiDevice.WifiIndex >= wifiDevice.wifiPairs) { wifiDevice.WifiIndex = 0; }

	  Serial.println("initWifi: trying " + String(wifiDevice.ssid[wifiDevice.WifiIndex]));

	  WiFi.begin(wifiDevice.ssid[wifiDevice.WifiIndex], wifiDevice.pwd[wifiDevice.WifiIndex]);

	  // NNR: We should not proceed before we are connected to a wifi
	  delay(500);
	  if (WiFi.status() != WL_CONNECTED) {
		  retry = 0;
		  Serial.print("waiting for wifi connection on " + String(wifiDevice.ssid[wifiDevice.WifiIndex]) );
		  while (WiFi.status() != WL_CONNECTED && retry++ < MaxRetriesWithSamePwd) {
			  delay(500);
			  Serial.print(".");
		  }
	  }

	  wifiDevice.WiFiConnectAttempts++;
	  wifiDevice.LastWifiTime = millis() + WifiTimeoutMilliseconds;

	  wifiDevice.WifiIndex++;  //increment wifi indexready for the next ssid/pwd pair in case the current wifi pair dont connect
  }
  //delay(2000);
  Serial.println("Wifi connected");
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
	while (timeStatus() == timeNotSet && ++ntpRetryCount < 10) { // get NTP time
																 //Serial.println(WiFi.localIP());
		setSyncProvider(getNtpTime);
		setSyncInterval(60 * 60);
	}
}