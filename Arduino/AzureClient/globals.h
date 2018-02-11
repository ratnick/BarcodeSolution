const int SPI_CS = 16;		 // SPI Pin on WeMos D1 Mini
const int LED_PIN = D4;
const int PUSHBUTTON_PIN = D8;

enum LedState {
  LED_Off,
  LED_On
};

enum SensorMode {
  None,
  Bmp180Mode,
  DhtShieldMode
};

enum BoardType {
  NodeMCU,
  WeMos,
  SparkfunThing,
  Other
};

enum DisplayMode {
  NoDisplay,
  LedMatrix
};

struct SensorData{
  float temperature;
  float humidity;
  int pressure;
  int light;
};

struct MessageData {
	String blobName;
	String fullBlobName;
	String msgId;
	long blobSize;
	String cameraType;
	int imageWidth;
	int imageHeight;
	String imageType;
};

struct CloudConfig {
  unsigned int publishRateInSeconds = 60; // defaults to once a minute
  // WARNING EXPIRY SET TO 10 YEARS FROM NOW.  
  // Epoch Timestamp Conversion Tool http://www.epochconverter.com/
  // Expires Wed, 22 Jan 2025 00:00:00 GMT.  Todo: add expiry window - eg now plus 2 days...
  // IOT HUB Devices can be excluded by device id/key - expiry window not so relevant
  // EVENT Hubs Devices can only be excluded by policy so a more sensible expiry should be tried and you'd need to device a moving expiry window
  unsigned int sasExpiryDate = 1737504000;  // Expires Wed, 22 Jan 2025 00:00:00 GMT
  const char *iothubHostname;
  const char *storageHostname;
  const char *storageSASkey;
  const char *storageContainerName;
  const char *webAppHostName;
  const char *webAppFunctionName;
  const char *webAppFunctionKey;
  char *deviceSASKey;
  const char *deviceId;
  const char *geo;
  unsigned long lastPublishTime = 0;
  String fullSas;
  String endPoint;
};

struct DeviceConfig {
  int WifiIndex = 0;
  unsigned long LastWifiTime = 0;
  int WiFiConnectAttempts = 0;
  int wifiPairs = 1;
  String ssid;
  String pwd;
  BoardType boardType = Other;            // OperationMode enumeration: NodeMCU, WeMos, SparkfunThing, Other
  SensorMode sensorMode = None;           // OperationMode enumeration: DemoMode (no sensors, fakes data), Bmp180Mode, Dht11Mode
  DisplayMode displayMode = NoDisplay;    // DisplayMode enumeration: NoDisplay or LedMatrix
  unsigned int deepSleepSeconds = 0;      // Number of seconds for the ESP8266 chip to deepsleep for.  GPIO16 needs to be tied to RST to wake from deepSleep http://esp8266.github.io/Arduino/versions/2.0.0/doc/libraries.html
};

enum imageBufferSourceEnum {
	InternalImageBuffer,
	DirectFromCamera
};

