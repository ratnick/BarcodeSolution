#include <LogLib.h>
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
#define USE_WIFI  // If undefined, the serial port is used instead
//#define DEBUG_PRINT

// To use serial port for output of picture for testing:
// - undefine USE_WIFI   
#define USE_WIFI	true // Use either Wifi to send to Azure or serial port to send to ReadSerialPort

//#define NO_AZURE_OR_CAMERA // don't connect to Azure at all. Used for debugging
//#define DEBUG_PRINT  // don't combine with transmitting over serial port

// The Arduino device itself
DeviceConfig wifiDevice;

//const char timeServer[] = "0.dk.pool.ntp.org"; // Danish NTP Server 
const char timeServer[] = "pool.ntp.org"; // NTP Server pool
WiFiClient wifiClient;

// Image storage globals
MessageData msgData;
#define IMAGE_WIDTH  320	// MUST CORRESPOND TO THE RAW READ-OUT FORMAT FROM THE CAMERA
#define IMAGE_HEIGHT 240	// MUST CORRESPOND TO THE RAW READ-OUT FORMAT FROM THE CAMERA

// Used for on-board cropping (fish eye):
#define XCROP_START  81							// PIXELS starting with 1, MUST BE UN-EVEN. 33
#define XCROP_END    260						// PIXELS, MUST BE EVEN
#define YCROP_START  115//91							// PIXELS starting with 1, MUST BE UN-EVEN  101
#define YCROP_END    174//130						// PIXELS, MUST BE EVEN

/*
#define XCROP_START  1 //23	// PIXELS starting with 1, MUST BE UN-EVEN. 
#define XCROP_END    320	// PIXELS, MUST BE EVEN
#define YCROP_START  1//101	// PIXELS starting with 1, MUST BE UN-EVEN
#define YCROP_END    240	// PIXELS, MUST BE EVEN
*/

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


/* Overall algorithm:

	Setup:
		Init camera + Azure (but not wifi)
	Loop
		Take picture
		Init wifi (if not already done)
		Transmit picture and wait for response from Azure

*/

void setup() {

	String wifiname;
	String wifipwd;

	Serial.begin(921600);
	InitDebugLevel(2);  // 0 => print essentials, 2 => fairly much debug
	LogLine(0, __FUNCTION__, "START");


	initFlashLED();
	initPushButton();
	// only initi here if we are debugging. Otherwise wait until the connection is actually needed (in the main loop)
	//  InitWifiAndNTC();

	#ifndef NO_AZURE_OR_CAMERA
	LED_Flashes(5, 50);
	initArduCAM();
	delay(100);
	//initDeviceConfig();
	azureCloudConfig();
	#endif

	initDeviceConfig();
	//initialiseAzure(); // https://msdn.microsoft.com/en-us/library/azure/dn790664.aspx  
}

void InitWifiAndNTC() {
	#ifdef USE_WIFI
		LED_Flashes(2, 300);
		ArduCamEnterLowPower();
		delay(500);
		initWifi();
		getCurrentTime();
		LogLine(0, __FUNCTION__, "");
		LED_Flashes(3, 300);
		ArduCAMLeaveLowPower();
	#endif

	#ifndef NO_AZURE_OR_CAMERA
		initArduCAM();  // TODO: is it correct to initalize cam here?
	#endif
}

void initDeviceConfig() {
	wifiDevice.boardType = Other;            // BoardType enumeration: NodeMCU, WeMos, SparkfunThing, Other (defaults to Other). This determines pin number of the onboard LED for wifi and publish status. Other means no LED status 
	wifiDevice.deepSleepSeconds = 0;         // if greater than zero with call ESP8266 deep sleep (default is 0 disabled). GPIO16 needs to be tied to RST to wake from deepSleep. Causes a reset, execution restarts from beginning of sketch
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
			flushCamFIFO();
			LogLine(0, __FUNCTION__, "***** >> Ready to shoot. ");
			LED_Flashes(1, 300);
			waitUntilButtonPushed();
			LED_ON();
			takePicture(msgData.blobSize, finalWidth, finalHeight, pixelSize);  // note that this number indicates the size of the CROPPED image
			LED_OFF();
		}
		if (imageBufferSource == InternalImageBuffer) {
			LED_ON();
			readPicFromCamToImageBuffer(imageBuffer, msgData.blobSize);
			LED_OFF();
			//dumpImageData(imageBuffer, msgData.blobSize);
			//dumpBinaryData(imageBuffer, msgData.blobSize, "imageBuffer");
		}
		else {
			// do nothing. This is the normal case: The pic will be read directly from the camera from transmitDataOnWifi called by UploadToBlobOnAzure
		}
	}
	else {
		Serial.println("NO PHOTO TAKEN (enable compile flag to do this)");
	}
	msgData.blobName = FIXED_BLOB_NAME;
	// msgData.fullBlobName = "/" + String(cloud.storageContainerName) + "/" + String(cloud.deviceId) + "/" + String(msgData.blobName); // code39ex1.jpg";// " / input / " + String(cloud.deviceId) + " / input / " + String(msgData.blobName);
	msgData.fullBlobName = "/" + String(cloud.storageContainerName) + "/" + String(msgData.blobName); // code39ex1.jpg";// " / input / " + String(cloud.deviceId) + " / input / " + String(msgData.blobName);

#ifdef USE_WIFI
	if (WiFi.status() != WL_CONNECTED) {
		InitWifiAndNTC();
	}
	if (WiFi.status() == WL_CONNECTED) {
		LogLine(0, __FUNCTION__, "WiFi.status() == WL_CONNECTED");

		if (TAKE_AND_UPLOAD_PICTURE_TO_BLOB) {
			if (published=UploadToBlobOnAzure(imageBufferSource, imageBuffer)) {
				//dumpBinaryData(imageBuffer, msgData.blobSize, "imageBuffer");  //NB: Only works if image is NOT taken from camera but downloaded
				LED_Flashes(4, 300);
			}
			else {
				Serial.println("ERROR: Could not publish Blob to Azure.");
				LED_Flashes(30, 50);
			}
			delay(000); // Cloud needs a little time to settle. TODO: Does it really? 5 secs?
		}

		if (published && CALL_BARCODERECOGNITION_WEBJOB) {
			if (SendDeviceToCloudHttpFunctionRequest(imageBufferSource, imageBuffer)) {
				Serial.println("Recognized barcode: SUCCESS");
				LED_Flashes(3, 1500);
			}
			else {
				Serial.println("Did not recognize barcode: ERROR or no recognition");
				LED_Flashes(30, 50);
			}
		}
	}
#else
	// send pic on serial port
	LogLine(5, "Sending data on serial port");
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

void dumpBinaryData(byte *buf, long nbrOfBytes, String s) {

	long i = 0;
	Serial.printf("\ndumpBinaryData %d bytes of: ", nbrOfBytes);
	Serial.println(s);

	while (i < nbrOfBytes)
	{
		Serial.printf("%x ", (byte)buf[i++]);
		if (i % 8 == 0) { Serial.printf("\n"); }
	}
	//Serial.println("");
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


