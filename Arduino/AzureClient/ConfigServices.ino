#include <EEPROM.h>

void initCloudConfig() {
	EEPROM.begin(512);
	char* data;
	int length;

	const int BUFFER_SIZE = JSON_OBJECT_SIZE(4) + JSON_ARRAY_SIZE(0);
	StaticJsonBuffer<1000> jsonBuffer;
	int address = 2;

	length = word(EEPROM.read(0), EEPROM.read(1));
	data = new char[length];

	for (address = 2; address < length + 2; address++) {
		data[address - 2] = EEPROM.read(address);
	}
	data[address - 2] = '\0';


	JsonObject& root = jsonBuffer.parseObject(data);
	if (!root.success())
	{
		Serial.println("parseObject() failed");
		return;
	}

  /* Following line should be changed to accomodate for two different hosts: iothub and storage
  cloud.host = GetValue(root["host"]);
  */
//	cloud.deviceSASKey = (char*)GetValue(root["key"]);
//  cloud.deviceId = GetValue(root["id"]);
  cloud.geo = GetValue(root["geo"]);
  

/*  wifiDevice.wifiPairs = root["wifi"]; 
  wifiDevice.ssid = new const char*[wifiDevice.wifiPairs];
  wifiDevice.pwd = new const char*[wifiDevice.wifiPairs];

	for (int i = 0; i < wifiDevice.wifiPairs; i++)
	{
    wifiDevice.ssid[i] = GetValue(root["ssid"][i]);
    wifiDevice.pwd[i] = GetValue(root["pwd"][i]);
	}
	*/
}

void initCloudConfig(
	const char *iothubHostname,
	const char *storageHostname,
	const char *storageSASkey,
	const char *storageContainerName,
	const char *webAppHostName,
	const char *webAppFunctionName,
	const char *webAppFunctionKey,
	const char *deviceId,
	const char *deviceSASKey,
	const char *ssid,
	const char *pwd, 
	const char *geo){

//  initWifiDevice(ssid, pwd);
  cloud.geo = geo;
  
  cloud.iothubHostname = iothubHostname;
  cloud.storageHostname = storageHostname;
  cloud.storageSASkey = storageSASkey;
  cloud.storageContainerName = storageContainerName;
  cloud.webAppHostName = webAppHostName;
  cloud.webAppFunctionName = webAppFunctionName;
  cloud.webAppFunctionKey = webAppFunctionKey;
  cloud.deviceId = deviceId;
  cloud.deviceSASKey = (char *)deviceSASKey;

}


