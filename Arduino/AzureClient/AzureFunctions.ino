#include <WiFiClientSecure.h>
#include <ArduinoJson.h>    // https://github.com/bblanchon/ArduinoJson - installed via library manager
#include "sha256.h"
#include "Base64.h"

WiFiClientSecure tlsClient;

// Azure IoT Hub Settings
const char* TARGET_URL = "/devices/";
const char* IOT_HUB_END_POINT = "/messages/events?api-version=2015-08-15-preview";

// Azure Event Hub settings
const char* EVENT_HUB_END_POINT = "/ehdevices/publishers/nodemcu/messages";
//String httpRequest = "..............................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................";

int sendCount = 0;
char buffer[512];

// *** Function for initializing the connection to the cloud
void azureCloudConfig(String &wifiname, String &wifipwd) {
	cloud.publishRateInSeconds = 90;     // limits publishing rate to specified seconds (default is 90 seconds).  Connectivity problems may result if number too small eg 2
	cloud.sasExpiryDate = 1737504000;    // Expires Wed, 22 Jan 2025 00:00:00 GMT (defaults to Expires Wed, 22 Jan 2025 00:00:00 GMT)

    //  initCloudConfig();  // alternative for production is to read config from EEPROM
	initCloudConfig(
		IOTHUB_HOSTNAME,
		STORAGE_HOSTNAME,
		STORAGE_SAS,
		STORAGE_CONTAINER_NAME,
		WEBAPP_HOSTNAME,
		WEBAPP_FUNCTION_NAME,
		WEBAPP_FUNCTION_KEY,
		THIS_DEVICE_NAME,
		THIS_DEVICE_SAS,
		wifiname.c_str(),
		wifipwd.c_str(),
		"Copenhagen");
}

void initialiseIotHub() {
	String url = urlEncode(cloud.iothubHostname) + urlEncode(TARGET_URL) + (String)cloud.deviceId;
	cloud.endPoint = (String)TARGET_URL + (String)cloud.deviceId + (String)IOT_HUB_END_POINT;
	cloud.fullSas = createIotHubSas(cloud.deviceSASKey, url);
}

void initialiseEventHub() {
	String url = urlEncode("https://") + urlEncode(cloud.iothubHostname) + urlEncode(EVENT_HUB_END_POINT);
	cloud.endPoint = EVENT_HUB_END_POINT;
	cloud.fullSas = createEventHubSas(cloud.deviceSASKey, url);
}

void initialiseStorageHub() {
	String url = urlEncode(cloud.storageHostname) + urlEncode(TARGET_URL) + (String)cloud.deviceId;
	cloud.endPoint = (String)TARGET_URL + (String)cloud.deviceId + (String)IOT_HUB_END_POINT;
	cloud.fullSas = createStorageHubSas(cloud.deviceSASKey, url);
}

int connectToAzure(const char *host) {  // true=OK; false=error
	delay(500); // give network connection a moment to settle
				//Serial.printf("Connecting to %s \n", host);
	if (WiFi.status() != WL_CONNECTED) { return false; }
	//Serial.printf("connectToAzure: Connecting to %s \n", host);
	if (!tlsClient.connect(host, 443)) {      // Use WiFiClientSecure class to create TLS connection
		Serial.print("Host connection failed.  WiFi IP Address: ");
		Serial.println(WiFi.localIP());
		delay(2000);
		return false;
	}
	else {
		//Serial.printf("Connected to %s \n", host);
		yield(); // give firmware some time 
				 //    delay(250); // give network connection a moment to settle
		return true;
	}
}

// *** Functions for creating SAS keys
String createIotHubSas(char *key, String url) {
	String stringToSign = url + "\n" + cloud.sasExpiryDate;

	// START: Create signature
	// https://raw.githubusercontent.com/adamvr/arduino-base64/master/examples/base64/base64.ino

	int keyLength = strlen(key);

	int decodedKeyLength = base64_dec_len(key, keyLength);
	char decodedKey[decodedKeyLength];  //allocate char array big enough for the base64 decoded key

	base64_decode(decodedKey, key, keyLength);  //decode key

	Sha256.initHmac((const uint8_t*)decodedKey, decodedKeyLength);
	Sha256.print(stringToSign);
	char* sign = (char*)Sha256.resultHmac();
	// END: Create signature

	// START: Get base64 of signature
	int encodedSignLen = base64_enc_len(HASH_LENGTH);
	char encodedSign[encodedSignLen];
	base64_encode(encodedSign, sign, HASH_LENGTH);

	// SharedAccessSignature
	return "sr=" + url + "&sig=" + urlEncode(encodedSign) + "&se=" + cloud.sasExpiryDate;
	// END: create SAS  
}

String createEventHubSas(char *key, String url) {
	// START: Create SAS  
	// https://azure.microsoft.com/en-us/documentation/articles/service-bus-sas-overview/
	// Where to get seconds since the epoch: local service, SNTP, RTC

	String stringToSign = url + "\n" + cloud.sasExpiryDate;

	// START: Create signature
	Sha256.initHmac((const uint8_t*)key, 44);
	Sha256.print(stringToSign);

	char* sign = (char*)Sha256.resultHmac();
	int signLen = 32;
	// END: Create signature

	// START: Get base64 of signature
	int encodedSignLen = base64_enc_len(signLen);
	char encodedSign[encodedSignLen];
	base64_encode(encodedSign, sign, signLen);
	// END: Get base64 of signature

	// SharedAccessSignature
	return "sr=" + url + "&sig=" + urlEncode(encodedSign) + "&se=" + cloud.sasExpiryDate + "&skn=" + cloud.deviceId;
	// END: create SAS
}

String createStorageHubSas(char *key, String url) {   // Based on createIotHubSas
	String stringToSign = url + "\n" + cloud.sasExpiryDate;

	int keyLength = strlen(key);

	int decodedKeyLength = base64_dec_len(key, keyLength);
	char decodedKey[decodedKeyLength];  //allocate char array big enough for the base64 decoded key

	base64_decode(decodedKey, key, keyLength);  //decode key

	Sha256.initHmac((const uint8_t*)decodedKey, decodedKeyLength);
	Sha256.print(stringToSign);
	char* sign = (char*)Sha256.resultHmac();
	// END: Create signature

	// START: Get base64 of signature
	int encodedSignLen = base64_enc_len(HASH_LENGTH);
	char encodedSign[encodedSignLen];
	base64_encode(encodedSign, sign, HASH_LENGTH);

	// SharedAccessSignature
	return "sr=" + url + "&sig=" + urlEncode(encodedSign) + "&se=" + cloud.sasExpiryDate;
	// END: create SAS  
}

// *** Function to transmit the data to Azure
int transmitHeaderOnWifi(String httpRequest) {

	int bytesWritten;

//	Serial.println("TRANSMIT HTTP header to Azure:");
	bytesWritten = tlsClient.print(httpRequest);
	Serial.print("transmitHeaderOnWifi: HTTP command: \n" + httpRequest + "===");
	//Serial.printf("\nTotal of %d bytes in header. \nDATA TRANSMIT BEGIN:", bytesWritten);

	return bytesWritten;
}

int transmitDataOnWifiOrSerial(int payloadSize, imageBufferSourceEnum imageBufferSource, byte *buf) {

	int bytesWritten = 0;
	int bytesRead = 0;
	int chunkRead = 0;
	int chunkStart = 0;
	int chunkEnd = 0;
	int nextChunk = 0;
	//const int chunkSize = 2000;
	const int chunkSize = BYTES_PER_PIXEL*(XCROP_END - XCROP_START + 1);   // one line's uncropped pixels (2 bytes per pixel)
	uint8_t buffer[chunkSize];
	uint8_t discardbuffer[BYTES_PER_PIXEL*IMAGE_WIDTH];

	readAndDiscardFirstByteFromCam();

	// This is tricky: The payload size is the size of the CROPPED image. If no cropping, then payloadsize is the same as full image size

	// read initial cropped lines from camera and discard them
	for (int line = 1; line < YCROP_START; line++) {
		nextChunk = BYTES_PER_PIXEL * IMAGE_WIDTH;
		chunkRead = readChunkFromCamToBuffer(discardbuffer, nextChunk);
		bytesRead += chunkRead;
		#ifdef DEBUG_PRINT
			Serial.printf("\nFIRST: (start:%d, end:%d, payload:%d, written:%d, read:%d, chunkSize=%d) DONE\n", chunkStart, chunkEnd, payloadSize, bytesWritten, bytesRead, chunkSize);
		#endif
	}

	int l;
	while (chunkEnd < payloadSize) {
		if ((chunkStart + chunkSize) <= payloadSize) {   // more than one chunk to go
			chunkEnd += chunkSize;
		}
		else {											// last chunk
			chunkEnd = payloadSize;
		}
		if (imageBufferSource == InternalImageBuffer) {
			memcpy(buffer, &imageBuffer[chunkStart], (chunkEnd - chunkStart + 1) * sizeof(char)); //NNR Jeg tror rækkefølgen på argumenterne er forkert!!!
		}
		else {
			// read directly from camera and transmit over wifi
			// read from line start to crop start and discard it
			nextChunk = BYTES_PER_PIXEL*(XCROP_START - 1);
			chunkRead = readChunkFromCamToBuffer(discardbuffer, nextChunk);
			bytesRead += chunkRead;
			//Serial.printf("\n1.   nextChunk=%d, chunkRead=%d, bytesRead=%d", nextChunk, chunkRead, bytesRead);

			// read from crop start to crop end and buffer it
			nextChunk = (chunkEnd - chunkStart);
			chunkRead = readChunkFromCamToBuffer(buffer, nextChunk);
			bytesRead += chunkRead;
			//Serial.printf("\n2.   chunkStart=%d, nextChunk=%d, chunkRead=%d, bytesRead=%d", chunkStart, nextChunk, chunkRead, bytesRead);

			// read from crop end to line end and discard it
			nextChunk = BYTES_PER_PIXEL*(IMAGE_WIDTH - XCROP_END);
			chunkRead = readChunkFromCamToBuffer(discardbuffer, nextChunk);
			bytesRead += chunkRead;
			//Serial.printf("\n3.   nextChunk=%d, chunkRead=%d, bytesRead=%d", nextChunk, chunkRead, bytesRead);
		}
		int len = chunkEnd - chunkStart;
		#ifdef USE_WIFI
			bytesWritten += tlsClient.write(buffer, len);
			//dumpBinaryData(buffer, len, "buffer transmitted over wifi");
#else
			if (len > 128) {
				bytesWritten += Serial.write(buffer, len / 2);
				delay(20);
				bytesWritten += Serial.write(&buffer[(len / 2)], len / 2);
				delay(20);
			}
			else {
				bytesWritten += Serial.write(buffer, len);
			}
		#endif
		#ifdef DEBUG_PRINT
			//Serial.printf("|\n\n SENT %d bytes", bytesWritten);
			dumpBinaryData(buffer, len);
		#endif

		chunkStart += chunkSize;
		//Serial.printf("\nMID: (start:%d, end:%d, payload:%d, written:%d, read:%d, chunkSize=%d) DONE\n", chunkStart, chunkEnd, payloadSize, bytesWritten, bytesRead, chunkSize);
	}

	// read last cropped lines from camera and discard them
	// NOTE: 1 line extra is removed, since it seems like the camera reports one additional line ???
	for (int line = YCROP_END; line < IMAGE_HEIGHT + 1; line++) {
		nextChunk = BYTES_PER_PIXEL * IMAGE_WIDTH;
		chunkRead = readChunkFromCamToBuffer(discardbuffer, nextChunk);
		bytesRead += chunkRead;
	}
	#ifdef DEBUG_PRINT
		Serial.printf("\nLAST: (start:%d, end:%d, payload:%d, written:%d, read:%d, chunkSize=%d) DONE\n", chunkStart, chunkEnd, payloadSize, bytesWritten, bytesRead, chunkSize);
	#endif

	//DO	Serial.printf("\nEND: (payloadSize:%d, bytesWritten:%d, bytesRead:%d) DONE\n", payloadSize, bytesWritten, bytesRead);
	//ensure camera buffer is empty
	flushCamFIFO();
	return bytesWritten;
}

int transmitDataOnWifiDELETE(int payloadSize, imageBufferSourceEnum imageBufferSource, byte *buf) {

	int bytesWritten = 0;
	int bytesRead = 0;
	int chunkRead = 0;
	int chunkStart = 0;
	int chunkEnd = 0;
	int nextChunk = 0;
	//const int chunkSize = 2000;
	const int chunkSize = BYTES_PER_PIXEL*(YCROP_END - YCROP_START + 1);   // one line's uncropped pixels (2 bytes per pixel)
	uint8_t buffer[chunkSize];
	uint8_t discardbuffer[BYTES_PER_PIXEL*IMAGE_HEIGHT ];

	readAndDiscardFirstByteFromCam();
	
	// This is tricky: The payload size is the size of the CROPPED image. If no cropping, then payloadsize is the same as full image size

	while (chunkEnd < payloadSize) {

		if ((chunkStart + chunkSize) <= payloadSize) {   // more than one chunk to go
			chunkEnd += chunkSize;
		}
		else {											// last chunk
			chunkEnd = payloadSize;
		}
		if (imageBufferSource == InternalImageBuffer) {
			memcpy(buffer, &imageBuffer[chunkStart], (chunkEnd - chunkStart) * sizeof(char));
		}
		else {
			// read directly from camera and transmit over wifi
			// read from line start to crop start and discard it
			readChunkFromCamToBuffer(discardbuffer, BYTES_PER_PIXEL*(YCROP_START - 1));
			// read from crop start to crop end and buffer it
			readChunkFromCamToBuffer(buffer, (chunkEnd - chunkStart));
			// read from crop end to line end and discard it
			readChunkFromCamToBuffer(discardbuffer, BYTES_PER_PIXEL*(IMAGE_HEIGHT - YCROP_END));
		}
		//Serial.printf("\n(start:%d, end:%d, payload:%d, written:%d) Sending", chunkStart, chunkEnd, payloadSize, bytesWritten);
		//dumpBinaryData(buffer, 10);
		bytesWritten += tlsClient.write(buffer, (chunkEnd - chunkStart));
		chunkStart += chunkSize;
		//		Serial.printf("\n(start:%d, end:%d, payload:%d, written:%d) BEGIN", chunkStart, chunkEnd, payloadSize, bytesWritten);
	}
	Serial.printf("\n(start:%d, end:%d, payload:%d, written:%d) DONE\n", chunkStart, chunkEnd, payloadSize, bytesWritten);
	return bytesWritten;
}

boolean ConnectAndFlushHost(const char *host) {

	if (!tlsClient.connected()) {
		if (!connectToAzure(host)) {
			Serial.printf("not connected A: %s\n", host); return false;
		}
	}
	if (connectToAzure(host) && tlsClient.connected()) { tlsClient.flush(); }
	else {
		Serial.printf("not connected B: %s\n", host); return false;
	}

}

// *** Functions to build HTTP call and transmit to the Blob
int UploadToBlobOnAzure(imageBufferSourceEnum imageBufferSource, byte *buf) {
	String httpRequestBody = "";
	int bytesWritten = 0;
	boolean success;
	const char *host;

	initialiseStorageHub();
	host = cloud.storageHostname;

	//Serial.println("UploadToBlobOnAzure: START");
	//NNR: Is this really needed every time? Check up later.
	if (!tlsClient.connected()) { if (!connectToAzure(host)) { return false; } }

	//Serial.printf("UploadToBlobOnAzure: connect to Azure %s\n", host);
	if (connectToAzure(host) && tlsClient.connected()) {tlsClient.flush();}	else { return false; }

	// Build HTTP msg and send it
	httpRequestBody = buildHttpRequestBlob(serializeDataBlob(msgData));
	//Serial.printf("UploadToBlobOnAzure: HTTP request: %s\n", httpRequestBody.c_str());
	bytesWritten = transmitHeaderOnWifi(httpRequestBody);
	//bytesWritten = transmitDataOnWifiDELETE(msgData.blobSize, imageBufferSource, buf);
	bytesWritten = transmitDataOnWifiOrSerial(msgData.blobSize, imageBufferSource, buf);

	//Serial.printf("UploadToBlobOnAzure: Read reply from server\n");
	String response = "";
	String chunk = "";
	int limit = 1;
	do {
		if (tlsClient.connected()) {
			yield();
			chunk = tlsClient.readStringUntil('\n');
			response += chunk + "\n";
		}
	} while (chunk.length() > 0 && ++limit < 100);

	// Interprete the result to success or error
	if ((response.substring(9, 12) == "201") || (response.substring(9, 12) == "204")) { success = true; }
	else { success = false; }

	// Print debug info in case of error
	if (success) {
		Serial.printf("\npublishToAzure: SUCCESS.  %s response code: %s Full blobname: %s\n", host, response.substring(9, 12).c_str(), msgData.fullBlobName.c_str());
	}
	else {
		Serial.printf("\nERROR in publish to Azure server %s. Server response code: ", host);
		if (response.length() > 12) { Serial.println(response.substring(9, 12)); }
		else { Serial.println("unknown"); }
		Serial.print("\nHTTP command: \n" + httpRequestBody + "\n===\n");
		Serial.println("\nServer response: ");
		Serial.println(response);
		Serial.println("===");
		/*Serial.printf("UploadToBlobOnAzure: Bytes sent in header+data: %d\n", bytesWritten);
		Serial.printf("UploadToBlobOnAzure: ESP Free heap memory (bytes): %d\n", ESP.getFreeHeap());
		Serial.printf("UploadToBlobOnAzure: Message count: %d\n", sendCount);
		Serial.printf("UploadToBlobOnAzure: Response chunks (limit): %d\n", limit);
		Serial.println("===");*/
	}
	return (int)success;
}

String buildHttpRequestBlob(String data) {
	return "PUT https://" + String(cloud.storageHostname) + msgData.fullBlobName + cloud.storageSASkey + " HTTP/1.1\n" +
		"x-ms-version: 2016-05-31\r\n" +
		"Host: " + cloud.storageHostname + "\r\n" +
		"Content-Length: " + msgData.blobSize + "\r\n" +
		"Content-Type: text/plain; charset=UTF-8\r\n" + 
		"x-ms-blob-type: BlockBlob\r\n" +
		"\n";
}

String serializeDataBlob(MessageData data) {
	StaticJsonBuffer<JSON_OBJECT_SIZE(16)> jsonBuffer;  //  allow for a few extra json fields that actually being used at the moment
	JsonObject& root = jsonBuffer.createObject();

	root["deviceId"] = cloud.deviceId;
	root["MessageId"] = msgData.msgId;
	root["UTC"] = GetISODateTime();
	root["Full blob name"] = msgData.fullBlobName;
	root["blob name"] = msgData.blobName;
	root["blobSize"] = msgData.blobSize;

	//instrumentation
	root["WiFiconnects"] = wifiDevice.WiFiConnectAttempts;
	root["ESPmemory"] = ESP.getFreeHeap();
	root["Counter"] = ++sendCount;

	root.printTo(buffer, sizeof(buffer));

	//Serial.printf("serializeDataBlob: JSON: \n");
	//Serial.println((String)buffer);

	return (String)buffer;
}

// *** Functions to build HTTP call and trigger the WebApp
int SendDeviceToCloudHttpFunctionRequest(imageBufferSourceEnum imageBufferSource, byte *buf) {
	//String httpRequest = "..............................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................";
	String httpRequest = "";
	String httpRequestBody = "";
	int bytesWritten = 0;
	boolean success = true;
	
	String tmp;
	const char *host;
	const char *sURL;

	host = cloud.webAppHostName; // "nnriotwebapps.azurewebsites.net"; //cloud.iothubHostname;
	tmp = String("https://") + cloud.webAppHostName + "/api/" + cloud.webAppFunctionName + "?code=" + cloud.webAppFunctionKey;
	sURL = tmp.c_str();

	//Serial.println("SendDeviceToCloudHttpFunctionRequest: START ");
	ConnectAndFlushHost(host);

	//Serial.printf("SendDeviceToCloudHttpFunctionRequest: Build HTTP msg and send it\n");
	// Build HTTP msg and send it
	httpRequestBody = serializeDataWebApp(msgData);
	httpRequest = buildHttpRequestWebApp(httpRequestBody);
	//Serial.print("\nHTTP command was: \n" + httpRequest + "\n");
	bytesWritten = transmitHeaderOnWifi(httpRequest);

//	tlsClient.println("GET https://nnriotwebapps.azurewebsites.net/api/HttpPOST-processing?code=JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA== HTTP/1.1");
//	tlsClient.println("Host: nnriotwebapps.azurewebsites.net");
//	tlsClient.println("Connection: close");
	tlsClient.println();
	
	//Serial.printf("SendDeviceToCloudHttpFunctionRequest: Read reply from server\n");
	String response = "";
	String chunk = "";
	int limit = 1;
	do {
		if (tlsClient.connected()) {
			yield();
			chunk = tlsClient.readStringUntil('\n');
			response += chunk + "\n";
		}
		else {
			Serial.printf("not connected C");
		}
	} while (chunk.length() > 0 && ++limit < 100);

	// Interprete the result to success or error
	if (response.substring(9, 12) == "200") { success = true; }
	else { success = false; }

	// Print debug info in case of error
	if (success) {
		Serial.printf("\SendDeviceToCloudHttpFunctionRequest: SUCCESS. %s responded: %s\n", host, response.substring(9, 12).c_str());
		Serial.println(response);
	}
	else {
		Serial.print("\nERROR when sending HTTP request to Webfunction HttpPOST-processing (to process barcode image): \nHTTP command was: \n" + httpRequest + "\n");
		Serial.printf("\nSendDeviceToCloudHttpFunctionRequest %s. Server response code: ", host);
		if (response.length() > 12) { 
			Serial.println(response.substring(9, 12)); 
		} else { 
			Serial.printf("No or very short answer from host\n"); 
		}
		Serial.print("\nHTTP command was: \n" + httpRequest + "\n");
		Serial.println("===");
		Serial.println("\nServer response was: ");
		Serial.println(response);
		Serial.println("===");
		Serial.printf("SendDeviceToCloudHttpFunctionRequest: Bytes sent in header+data: %d\n", bytesWritten);
/*		Serial.printf("SendDeviceToCloudHttpFunctionRequest: ESP Free heap memory (bytes): %d\n", ESP.getFreeHeap());
		Serial.printf("SendDeviceToCloudHttpFunctionRequest: Message count: %d\n", sendCount);
		Serial.printf("SendDeviceToCloudHttpFunctionRequest: Response chunks (limit): %d\n", limit);
		Serial.println("===");
*/	}

	return (int)success;
}

String buildHttpRequestWebApp(String data) {
	return String("POST /api/") + cloud.webAppFunctionName + "?code=" + cloud.webAppFunctionKey +
		" HTTP/1.1\n" +
		"Host: " + cloud.webAppHostName + "\n" +
		"Content-Type: application/json\n" +
		"Content-Length: " + data.length() + "\n" +   // length must include the surrounding brackets
		"Connection: Keep-Alive\n" +
		"\n" +
		data +
		"";
//      httpRequest = "POST " + String(cloud.webAppFunctionURL) + " HTTP/1.1" +
//		return String("POST /api/HttpPOST-processing?code=JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA==") +
//		" HTTP/1.1\n" +
//		"Host: " + "nnriotwebapps.azurewebsites.net" + "\n"+
//
//		"\nAccept: */*" +
//		"\nAccept-Encoding: gzip, deflate" +
//		"\nUser-Agent: runscope/0.1" + 
//		"Content-Type: application/json\n" +
//		"Content-Length: " + data.length() + "\n"+   // length must include the surrounding brackets
//		"Connection: Keep-Alive\n" +
//		"\n" + 
//		data + 
//		""	;
		//	Serial.printf("\buildHttpRequestWebApp: Full HTTP request:\n%s", httpRequestFull.c_str());
}

String serializeDataWebApp(MessageData msgData) {
	StaticJsonBuffer<JSON_OBJECT_SIZE(16)> jsonBuffer;  //  allow for a few extra json fields that actually being used at the moment
	JsonObject& root = jsonBuffer.createObject();

	root["deviceId"] = cloud.deviceId;
	root["msgType"] = WEBAPP_CMD_BARCODE_CREATED;
	root["MessageId"] = "12345";
	root["UTC"] = "2017-01-08T10:45:09";
	root["BlobContainer"] = cloud.storageContainerName; 
	root["BlobPath"] = cloud.deviceId;
	root["BlobName"] = msgData.blobName;
	root["BlobSize"] = msgData.blobSize;  
	root["imageWidth"] = msgData.imageWidth;
	root["imageHeight"] = msgData.imageHeight;
	root["imageType"] = msgData.imageType;
	root["WiFiconnects"] = 1;
	root["ESPmemory"] = 7824;
	root["Counter"] = 1;

/*	msgData.fullBlobName = "/input/" + String(cloud.deviceId) + "/input/" + String(msgData.blobName);
	//Serial.printf("\n%s\n", msgData.fullBlobName.c_str());

	root["deviceId"] = cloud.deviceId;
	root["msgType"] = "Barcode created";
	root["MessageId"] = "12345"; // msgData.msgId;
	root["UTC"] = GetISODateTime();
	root["FullBlobName"] = "/nnriothubcontainer/ArduinoD1_001/input/imageFromRealDevice.jpg"; //msgData.fullBlobName;
	root["BlobName"] = "input/" + msgData.blobName;
	root["BlobSize"] = msgData.blobSize;
	//instrumentation
/*	root["WiFiconnects"] = wifiDevice.WiFiConnectAttempts;
	root["ESPmemory"] = ESP.getFreeHeap();
	root["Counter"] = ++sendCount;
*/
	root.printTo(buffer, sizeof(buffer));
	return (String)buffer;
}



// *** Unused functions
String buildHttpRequestWebAppTEST_WORKS(String data) {
	// PURELY TEST
	return String("POST /api/HttpSimpleTest?code=2ZhGue1npjZToZQXwiXOTIEqbNNXshvAs6cy/kVJ/i8HaTRixOaUmg== HTTP/1.1\n") +
		"Host: nnriotwebapps.azurewebsites.net\n" +
		"Content-Type: application/json\n" +
		"Content-Length: 42\n" +
		"Cache-Control: no-cache\n" +
		"Connection: Keep-Alive\n" +
		"\n" +
		"{\"name\":\"Nikolaj\", \"parm2\":\"parm 2 value\"}" +
		"";
}

String buildHttpRequestWebAppTESTDATA(String data) {
	// PURELY TEST
	return String("POST /api/HttpSimpleTest?code=2ZhGue1npjZToZQXwiXOTIEqbNNXshvAs6cy/kVJ/i8HaTRixOaUmg== HTTP/1.1\n") +
		"Host: nnriotwebapps.azurewebsites.net\n" +
		"Content-Type: application/json\n" +
		"Content-Length: " + data.length() + "\n" +   // length must include the surrounding brackets
		"Cache-Control: no-cache\n" +
		"Connection: Keep-Alive\n" +
		"\n" +
		data +
		"";
}

String buildBlobHttpRequestWORKS(String data) {
	String tmp = ""; // cloud.host;

	String blobSAS = "?sv=2015-12-11&ss=bfqt&srt=sco&sp=rwdlacup&se=2016-12-31T01:09:00Z&st=2016-12-29T17:09:00Z&spr=https&sig=phhz%2Fx%2BGAWz94Lg6j8nMUKbCUZ1KiHBF1mMOZ4IKEjU%3D";

	//Serial.println(cloud.fullSas);
	return	String("PUT https://") + String(cloud.storageHostname) + "/" + String(cloud.storageContainerName) + "/yessir.txt?sv=2015-12-11&ss=bfqt&srt=sco&sp=rwdlacup&se=2016-12-31T01:09:00Z&st=2016-12-29T17:09:00Z&spr=https&sig=phhz%2Fx%2BGAWz94Lg6j8nMUKbCUZ1KiHBF1mMOZ4IKEjU%3D" + tmp + " HTTP/1.1\r\n" +
		"x-ms-version: 2016-05-31\r\n" +
		"Host: " + String(cloud.storageHostname) + "\r\n"
		"Content-Length: " + data.length() + "\r\n" +
		"x-ms-blob-type: BlockBlob\r\n" +
		"\r\n" + data + "\r\n";
/*	return	"PUT https://nnriothubstorage.blob.core.windows.net/nnriothubcontainer/yessir.txt?sv=2015-12-11&ss=bfqt&srt=sco&sp=rwdlacup&se=2016-12-31T01:09:00Z&st=2016-12-29T17:09:00Z&spr=https&sig=phhz%2Fx%2BGAWz94Lg6j8nMUKbCUZ1KiHBF1mMOZ4IKEjU%3D" + tmp + " HTTP/1.1\r\n" +
		"x-ms-version: 2016-05-31\r\n" +
		"Host: nnriothubstorage.blob.core.windows.net\r\n"
		"Content-Length: " + data.length() + "\r\n" +
		"x-ms-blob-type: BlockBlob\r\n" +
		"\r\n" + data + "\r\n";
*/
}

// Code for igniting the HttpPOST-processing request:
/*String serializeHttpPOSTTrigger() {
	StaticJsonBuffer<JSON_OBJECT_SIZE(16)> jsonBuffer;  //  allow for a few extra json fields that actually being used at the moment
	JsonObject& root = jsonBuffer.createObject();

	root["deviceId"] = cloud.deviceId;
	root["msgType"] = "Barcode created";
	root["MessageId"] = msgData.msgId;
	root["UTC"] = "2017-01-08T10:45:09";
	root["FullBlobName"] = msgData.fullBlobName
	root["BlobName"] = msgData.blobName;
	root["BlobSize"] = msgData.blobSize;
	root["WiFiconnects"] = 1;
	root["ESPmemory"] = 7824;
	root["Counter"] = 1;


	//old stuff:

	root["Device ID"] = cloud.deviceId;
	root["MessageID"] = msgData.msgId;
	root["UTC"] = GetISODateTime();
	root["Full blob name"] = msgData.fullBlobName;
	root["blob name"] = msgData.blobName;
	root["blobSize"] = msgData.blobSize;

	//instrumentation
	root["WiFi connects"] = wifiDevice.WiFiConnectAttempts;
	root["ESP memory"] = ESP.getFreeHeap();
	root["Counter"] = ++sendCount;

	root.printTo(buffer, sizeof(buffer));

	return (String)buffer;
}


int SendDeviceToCloudHttpFunctionRequest() {

	String httpRequest = "";

	httpRequest = buildHttpRequest(serializeData(msgData));


	var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
	var message = new Message(Encoding.ASCII.GetBytes(messageString));

	using (var client = new HttpClient())
	{

		string sURL;
		// Here we define which WebJob function in Azure we will call.
		// check Documentation section under "Integrate" in Axure portal function definition. Function (code) key found under Manage section
		// original function: 
		//sURL = "https://nnriothubprocessimagea.azurewebsites.net/api/HttpTriggerCSharp1?code=CjsO/EzhtUBMgRosqjhCvFxmDN7k0xU1DfJGEGs00tBBqaiXNewn5A==";
		// new function online: 
		sURL = "https://nnriotWebApps.azurewebsites.net/api/HttpPOST-processing?code=JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA==";
		// local debug
		//sURL = "http://localhost:7071/api/HttpPOST-processing";
		//sURL = "http://localhost:7071/api/HttpTriggerBatch";

		StreamWriter requestWriter;

		var webRequest = System.Net.WebRequest.Create(sURL) as HttpWebRequest;
		if (webRequest != null)
		{
			webRequest.Method = "POST";
			webRequest.ServicePoint.Expect100Continue = false;
			webRequest.Timeout = 20000;

			webRequest.ContentType = "application/json";
			//POST the data.
			using (requestWriter = new StreamWriter(webRequest.GetRequestStream()))
			{
				requestWriter.Write(messageString);
			}
		}
		string ret = string.Empty;
		try
		{
			HttpWebResponse resp = (HttpWebResponse)webRequest.GetResponse();
			Stream resStream = resp.GetResponseStream();
			StreamReader reader = new StreamReader(resStream);
			ret = reader.ReadToEnd();
			Console.WriteLine("{0} > HTTP reply: {1}", DateTime.Now, ret);
		}
		catch
		{
			//                    (Exception e)
			Console.WriteLine("{0} > NO HTTP reply", DateTime.Now);
		}
	}


	return true;
}*/