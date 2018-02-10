#include <command.h>
#include <ESP8266WiFi.h>
#include <Wire.h>
#include <TimeLib.h>        // http://playground.arduino.cc/code/time - installed via library manager
#include "globals.h"        // global structures and enums used by the applocation
#include <time.h>
#include "Constants.h"

// Workflow
#define DOWNLOAD_PICTURE_FROM_WEB false
#define TAKE_AND_UPLOAD_PICTURE_TO_BLOB true
#define CALL_BARCODERECOGNITION_WEBJOB true
//#define USE_WIFI  // If undefined, the serial port is used instead
//#define DEBUG_PRINT

// The Arduino device itself
DeviceConfig wifiDevice;

const char timeServer[] = "0.dk.pool.ntp.org"; // Danish NTP Server 
WiFiClient wifiClient;

// Image storage globals
MessageData msgData;
#define IMAGE_WIDTH 320
#define XCROP_START 1	// PIXELS starting with 1, MUST BE UN-EQUAL
#define XCROP_END 50	// PIXELS, MUST BE EQUAL
#define IMAGE_HEIGHT 240
#define YCROP_START 1	// PIXELS starting with 1, MUST BE UN-EQUAL
#define YCROP_END 50	// PIXELS, MUST BE EQUAL

#define BYTES_PER_PIXEL 2 //both RGB565 and YUV422
static String IMAGE_TYPE = "YUV422";


// Enable below if you want to read the whole image from the camera into an internal buffer. This limits the image size.
// Disable if you want to read directly from camera to http request without internal buffering. Works for all image sizes.
//#define USE_IMAGE_BUFFER 
#ifdef USE_IMAGE_BUFFER
	#define MAX_IMAGE_SIZE 10000 // if compile problem "Error linking for board WeMos D1 R2 & mini" reduce this number to reduce memory footprint
	imageBufferSourceEnum imageBufferSource = InternalImageBuffer;
	byte imageBuffer[MAX_IMAGE_SIZE]; //nnr char imageBuffer[10000];
#else
	#define MAX_IMAGE_SIZE  (YCROP_END-YCROP_START+1)*(XCROP_END-XCROP_START+1)   //10000 // if compile problem "Error linking for board WeMos D1 R2 & mini" reduce this number to reduce memory footprint
	imageBufferSourceEnum imageBufferSource = DirectFromCamera;
	byte imageBuffer[1]; //nnr char imageBuffer[10000];
#endif

/*#elif USE_WIFI == false
	#define MAX_IMAGE_SIZE  (YCROP_END-YCROP_START+1)*(XCROP_END-XCROP_START+1)   //10000 // if compile problem "Error linking for board WeMos D1 R2 & mini" reduce this number to reduce memory footprint
	imageBufferSourceEnum imageBufferSource = DirectFromCamera;
#else
	#define MAX_IMAGE_SIZE 1 
	imageBufferSourceEnum imageBufferSource = DirectFromCamera;
#endif
byte imageBuffer[MAX_IMAGE_SIZE]; //nnr char imageBuffer[10000];
*/



// Azure Cloud globals
CloudConfig cloud;


void initDeviceConfig() {
	wifiDevice.boardType = Other;            // BoardType enumeration: NodeMCU, WeMos, SparkfunThing, Other (defaults to Other). This determines pin number of the onboard LED for wifi and publish status. Other means no LED status 
	wifiDevice.deepSleepSeconds = 0;         // if greater than zero with call ESP8266 deep sleep (default is 0 disabled). GPIO16 needs to be tied to RST to wake from deepSleep. Causes a reset, execution restarts from beginning of sketch
}

void setup() {

	String wifiname;
	String wifipwd;

	Serial.begin(921600);
	#ifdef DEBUG_PRINT
	Serial.println("AzureClient START");
	#endif


	initFlashLED();
	LED_Flashes(5, 25);
	initArduCAM();
	delay(100);
	initDeviceConfig();
	azureCloudConfig(wifiname, wifipwd);
#ifdef USE_WIFI
	initWifi();
#endif
	//initialiseAzure(); // https://msdn.microsoft.com/en-us/library/azure/dn790664.aspx  
}

int bytesWritten = 0;
void loop() {
	int published = false;
	int dataSize = 0;	
	boolean success = false;
	long bytesRead = 0;
	short finalWidth = 0;
	short finalHeight = 0;
	short pixelSize = 0;

#ifdef USE_WIFI
	getCurrentTime();
#endif 

	if (TAKE_AND_UPLOAD_PICTURE_TO_BLOB) {
		if (DOWNLOAD_PICTURE_FROM_WEB) {
			// This snippet gets image from http server somewhere (for testing)
			/*success = readReplyFromServer(imageBuffer, msgData.blobSize);
			while (!getImageData()) {
				// keep trying
				Serial.println("ERROR: Problem fetching test image. Terminating");
				delay(2000);
			} */
		} else {
			LED_ON();
			takePicture(msgData.blobSize, finalWidth, finalHeight, pixelSize);  // note that this number indicates the size of the CROPPED image
			LED_OFF();
		}
		if (imageBufferSource == InternalImageBuffer) {
			LED_ON();
			readPicFromCamToImageBuffer(imageBuffer, msgData.blobSize);
			LED_OFF();
			//dumpImageData(imageBuffer, msgData.blobSize);
			//dumpBinaryData(imageBuffer, msgData.blobSize);
		}
		else {
			// do nothing. The pic will be read directly from the camera from transmitDataOnWifi called by UploadToBlobOnAzure
		}
	}
	else {
		Serial.println("NO PHOTO TAKEN (enable compile flag to do this)");
	}
	msgData.blobName = FIXED_BLOB_NAME; 
	msgData.fullBlobName = "/" + String(cloud.storageContainerName) + "/" + String(cloud.deviceId) + "/" + String(msgData.blobName); // code39ex1.jpg";// " / input / " + String(cloud.deviceId) + " / input / " + String(msgData.blobName);

#ifdef USE_WIFI
	if (WiFi.status() == WL_CONNECTED) {
		//Serial.println("WiFi.status() == WL_CONNECTED");

		if (TAKE_AND_UPLOAD_PICTURE_TO_BLOB) {
			if (UploadToBlobOnAzure(imageBufferSource, imageBuffer)) {
				Serial.println("Published Blob to Azure.");
			}
			else {
				Serial.println("ERROR: Could not publish Blob to Azure.");
			}
			delay(5000); // Cloud needs a little time to settle
		}

		if (CALL_BARCODERECOGNITION_WEBJOB) {
			if (SendDeviceToCloudHttpFunctionRequest(imageBufferSource, imageBuffer)) {
				//Serial.println("Sent HTTPPOST Trigger request to Azure: SUCCESS");
			}
			else {
				Serial.println("Sent HTTPPOST Trigger request to Azure: ERROR or no recognition");
			}
		}

		/*		if (wifiDevice.deepSleepSeconds > 0) {
					ESP.deepSleep(1000000 * wifiDevice.deepSleepSeconds, WAKE_RF_DEFAULT); // GPIO16 needs to be tied to RST to wake from deepSleep. Execute restarts from beginning of sketch
				}
				else {
					delay(cloud.publishRateInSeconds * 1000);  // limit publishing rate
				}
		*/
	}
	else {
		initWifi();
		delay(250);
	}

	LED_OFF();    // turn the LED off by making the voltage LOW
	delay(10000); Serial.print("#");
	//TODO: Enaable last three lines. in production, we want to take only one picture.
	//Serial.println("Stopping.");
	//closeCamera();
	//while (true) { delay(10000); Serial.print("#"); } //endless loop. We're done
#else
	// send pic on serial port
	#ifdef DEBUG_PRINT
	Serial.println("Sending data on serial port");
	#endif
	//readPicFromCamToImageBuffer(imageBuffer, msgData.blobSize);
	//readAndDiscardFirstByteFromCam();
	//takePicture(msgData.blobSize, finalWidth, finalHeight, pixelSize);  // note that this number indicates the size of the CROPPED image
	transmitHeaderOnSerial(msgData.blobSize, finalWidth, finalHeight, pixelSize);
	bytesWritten += transmitDataOnWifiOrSerial(msgData.blobSize, imageBufferSource, imageBuffer);   // NOTE: imageBuffer is only assigned if USE_IMAGE_BUFFER is defined
	#ifdef DEBUG_PRINT
		Serial.printf("|\n\n TOTAL SENT %d bytes", bytesWritten);
	#endif
	delay(1000);
	flushCamFIFO();
#endif
}

void dumpImageData(byte *buf, long nbrOfBytes) {

	long length = 0;
	Serial.printf("\dumpImageData: %d bytes \n", length);
	while (length++ < nbrOfBytes)
	{
		if (length % 50 == 0) {
			Serial.print(length-1); Serial.print("=");; Serial.print((int)buf[length-1]); Serial.print(" ");
		}
	}
}

void dumpBinaryData(byte *buf, long nbrOfBytes) {

	long i = 0;
	Serial.printf("\ndumpBinaryData: %d bytes  ", nbrOfBytes);

	while (i < nbrOfBytes)
	{
		Serial.printf("%x ", (byte)buf[i]);
		i++;
		if (i % 10 == 0) { Serial.printf("i=%d  \n", i); }
	}
	Serial.println("");
}

void transmitHeaderOnSerial(long nbrOfBytes, short finalWidth, short finalHeight, short pixelSize) {

	long i = 0;
	byte h[10]; // header: 3 x short + 1 x long

	Serial.printf("###*RDY*");

	h[0] = (finalWidth >> (8 * 0)) & 0xff;  //byte 0 of wg
	h[1] = (finalWidth >> (8 * 1)) & 0xff;  //byte 1 of wg
	h[2] = (finalHeight >> (8 * 0)) & 0xff;
	h[3] = (finalHeight >> (8 * 1)) & 0xff;
	h[4] = (pixelSize >> (8 * 0)) & 0xff;
	h[5] = (pixelSize >> (8 * 1)) & 0xff;

	h[6] = (nbrOfBytes >> (8 * 0)) & 0xff;
	h[7] = (nbrOfBytes >> (8 * 1)) & 0xff;
	h[8] = (nbrOfBytes >> (8 * 2)) & 0xff;
	h[9] = (nbrOfBytes >> (8 * 3)) & 0xff;

#ifdef DEBUG_PRINT
	Serial.printf("|");
#endif
	Serial.write(h, sizeof(h));
#ifdef DEBUG_PRINT
	Serial.printf("|");
#endif

}

void transmitHeaderOnSerial(byte *buf, long nbrOfBytes) {

	Serial.write(buf, nbrOfBytes);

}

int transmitDataOnSerialDELETE(int payloadSize, imageBufferSourceEnum imageBufferSource, byte *buf) {

	int bytesWritten = 0;
	int bytesRead = 0;
	int chunkRead = 0;
	int chunkStart = 0;
	int chunkEnd = 0;
	int nextChunk = 0;
	//const int chunkSize = 2000;
	const int chunkSize = BYTES_PER_PIXEL*(YCROP_END - YCROP_START + 1);   // one line's uncropped pixels (2 bytes per pixel)
	uint8_t buffer[chunkSize];
	uint8_t discardbuffer[BYTES_PER_PIXEL*IMAGE_WIDTH];

	readAndDiscardFirstByteFromCam();

	// This is tricky: The payload size is the size of the CROPPED image. If no cropping, then payloadsize is the same as full image size

	// read initial cropped VERTICAL lines from camera and discard them
	for (int line = 1; line < YCROP_START; line++) {
		nextChunk = BYTES_PER_PIXEL * IMAGE_WIDTH;
		chunkRead = readChunkFromCamToBuffer(discardbuffer, nextChunk);
		bytesRead += chunkRead;
		//Serial.printf("\nFIRST: (start:%d, end:%d, payload:%d, written:%d, read:%d, chunkSize=%d) DONE\n", chunkStart, chunkEnd, payloadSize, bytesWritten, bytesRead, chunkSize);
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
			memcpy(buffer, &imageBuffer[chunkStart], (chunkEnd - chunkStart + 1) * sizeof(char));
		}
		else {
			// read directly from camera and transmit over wifi
			// read from vertical line start to crop start and discard it
			nextChunk = BYTES_PER_PIXEL*(XCROP_START - 1);
			chunkRead = readChunkFromCamToBuffer(discardbuffer, nextChunk);
			bytesRead += chunkRead;
			//Serial.printf("\n1.   nextChunk=%d, chunkRead=%d, bytesRead=%d", nextChunk, chunkRead, bytesRead);
			// read from crop start to crop end and buffer it
			nextChunk = (chunkEnd - chunkStart);
			chunkRead = readChunkFromCamToBuffer(buffer, nextChunk);
			bytesRead += chunkRead;
			//Serial.printf("\n2.   nextChunk=%d, chunkRead=%d, bytesRead=%d", nextChunk, chunkRead, bytesRead);
			// read from crop end to line end and discard it
			nextChunk = BYTES_PER_PIXEL*(IMAGE_WIDTH - XCROP_END);
			chunkRead = readChunkFromCamToBuffer(discardbuffer, nextChunk);
			bytesRead += chunkRead;
			//Serial.printf("\n3.   nextChunk=%d, chunkRead=%d, bytesRead=%d", nextChunk, chunkRead, bytesRead);
		}
		//dumpBinaryData(buffer, 10);
		int len = chunkEnd - chunkStart;
		if (len > 128) {
			bytesWritten += Serial.write(buffer, len/2);
			delay(20);
			bytesWritten += Serial.write(&buffer[(len/2)], len/2);
			delay(20);
		}
		else {
			bytesWritten += Serial.write(buffer, len);
		}
		chunkStart += chunkSize;
		//Serial.printf("\nMID: (start:%d, end:%d, payload:%d, written:%d, read:%d, chunkSize=%d) DONE\n", chunkStart, chunkEnd, payloadSize, bytesWritten, bytesRead, chunkSize);
	}

	// read last cropped lines from camera and discard them
	// NOTE: 1 line extra is removed, since it seems like the camera reports one additional line ???
	for (int line = YCROP_END; line < IMAGE_HEIGHT+1; line++) {
		nextChunk = BYTES_PER_PIXEL * IMAGE_WIDTH;
		chunkRead = readChunkFromCamToBuffer(discardbuffer, nextChunk);
		bytesRead += chunkRead;
		//Serial.printf("\nLAST: (start:%d, end:%d, payload:%d, written:%d, read:%d, chunkSize=%d) DONE\n", chunkStart, chunkEnd, payloadSize, bytesWritten, bytesRead, chunkSize);
	}

//DO	Serial.printf("\nEND: (payloadSize:%d, bytesWritten:%d, bytesRead:%d) DONE\n", payloadSize, bytesWritten, bytesRead);
	return bytesWritten;
}

