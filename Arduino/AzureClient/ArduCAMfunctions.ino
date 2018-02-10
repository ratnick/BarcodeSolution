#include <ArduCAM.h>		// added to enable Arducam interface. REMEMBER TO CONFIGURE memorysaver.h in the ArduCAM library
#include <SPI.h>			// added to enable Arducam interface
#include "memorysaver.h"

//SPI_CS defined in globals.h
//const int SPI_CS = 16;		 // SPI Pin on WeMos D1 Mini
#define MAX_FRAME_LENGTH 64  // default: 256

#if defined (OV2640_CAM)
	ArduCAM myCAM(OV2640, SPI_CS);
#elif defined (OV7670_CAM)
	ArduCAM myCAM(OV7670, SPI_CS);
#endif

void initArduCAM() {
	uint8_t vid = 0, pid = 0;
	uint8_t temp = 0;

	//Serial.println(F("initArduCAM - begin"));

	#if defined (OV7670_CAM)
		//Serial.println(F("initArduCAM - OV7670_CAM defined"));
	#endif
	#if defined (OV2640_CAM)
		Serial.println(F("initArduCAM - OV2640_CAM defined"));
	#endif

	// initialize SPI:
	Wire.begin();
	pinMode(SPI_CS, OUTPUT);
	SPI.begin();
	SPI.setFrequency(4000000); //4MHz
	while (1) {
		//Check if the ArduCAM SPI bus is OK
		//Serial.print("Check SPI interface on pin ");
		//Serial.print(SPI_CS);
		myCAM.write_reg(ARDUCHIP_TEST1, 0x55);
		temp = myCAM.read_reg(ARDUCHIP_TEST1);
		if (temp != 0x55) {
			Serial.println(F("SPI interface Error! TERMINATING."));
			while(true); continue;
		}
		else {
			//Serial.println(F(" ... SPI interface OK."));
			break;
		}
	}

	//Check if the camera module type is OV7660
	myCAM.rdSensorReg8_8(0x0A, &vid);
	myCAM.rdSensorReg8_8(0x0B, &pid);
	if ((vid != 0x76) || (pid != 0x73)) {
		Serial.printf("\nCan't find OV7660 module! TERMINATING. vid=0x%x, pid=0x%x", vid, pid);
		while (true); 
	} 

	// sent to Azure in HTTP call
	msgData.imageWidth =  YCROP_END - YCROP_START + 1;
	msgData.imageHeight = XCROP_END - XCROP_START + 1; 
	msgData.imageType = IMAGE_TYPE;

#if defined (OV2640_CAM)
	Serial.print(F("Check for OV2640 module: "));
	Wire.begin();
//	while (1) {
		//Check if the camera module type is OV2640
		myCAM.wrSensorReg8_8(0xff, 0x01);
		myCAM.rdSensorReg8_8(OV2640_CHIPID_HIGH, &vid);
		myCAM.rdSensorReg8_8(OV2640_CHIPID_LOW, &pid);
		if ((vid != 0x26) && ((pid != 0x41) || (pid != 0x42))) {
			Serial.println(F("Can't find OV2640 module!"));
			delay(1000); 
//			continue;
		}
		else {
			Serial.println(F("OV2640 detected.")); 
//			break;
		}
//	}case OV7670 x2
#endif
//-		myCAM.set_format(BMP);

		myCAM.set_format(JPEG);
		myCAM.InitCAM();

//	myCAM.set_format(JPEG);  // note: this does NOT apply to OV7670. It always produces RAW.
#if defined (OV7670_CAM)
		setCamBit(0x12, BIT7, 1);  // SCCB register reset
//		myCAM.set_format(BMP);  // 320 x 240 RAW RGB565 (big endian = swapped bytes) 

	//setCamBit(0x0C, BIT3, 1);  // Scale enable
//	setCamBit(0x12, BIT1, 1);  // Color bar

					
//	setCamBit(0x12, BIT2, 0); setCamBit(0x12, BIT0, 0); // YUV 
//	setCamBit(0x12, BIT2, 0); setCamBit(0x12, BIT0, 1); // Bayer Raw
//	setCamBit(0x12, BIT2, 1); setCamBit(0x12, BIT0, 1); // Processed Bayer
//		setCamBit(0x12, BIT2, 1); setCamBit(0x12, BIT0, 0); // Raw RGB 
//		setCamBit(0x40, BIT4, 0); // Raw RGB (in combination with above)

//	setCamBit(0x13, BIT2, 1);  // AGC enable/disable
//	setCamBit(0x3A, BIT4, 1);  // Fixed output

//	setCamByte(0x70, 0b10001000); // Grayscale test pattern + scaling



#endif
//	myCAM.InitCAM();

#if defined (OV2640_CAM)
	myCAM.OV2640_set_JPEG_size(OV2640_320x240); delay(1000);
	//myCAM.OV2640_set_JPEG_size(OV2640_1600x1200); delay(1000);
#endif
}

int getSetBitLocation(byte b) {
	int i = 0;
	while (!((b >> i++) & 0x01)) { ; }
	return i-1;
}

void setCamBit(byte addr, byte bit, byte setVal) {
	byte orgVal; 
	byte newVal;
	byte checkVal; 
	byte dummy;
	byte tmp=0;

	dummy = myCAM.rdSensorReg8_8(addr, &orgVal);
	if (setVal) {
		newVal = orgVal | bit;
	}
	else {
		tmp = (byte) (255 - bit);
		newVal = orgVal & tmp;
	}
	myCAM.wrSensorReg8_8(addr, newVal);
	dummy = myCAM.rdSensorReg8_8(addr, &checkVal);

	Serial.printf(" SET BIT %i  at addr 0x%x", getSetBitLocation(bit), addr);
	Serial.printf("  tmp= %x", tmp);
	Serial.printf("  setVal= %x", setVal);
	Serial.printf("  orgVal= %x ", orgVal);
	Serial.printf(" newVal= %x ", newVal);
	Serial.printf(" checkVal= %x \n", checkVal);

//	uint8_t read_reg(uint8_t addr);
//	void write_reg(uint8_t addr, uint8_t data);

}

void setCamByte(byte addr, byte val) {
	byte orgVal;
	byte dummy;
	byte checkVal;
	dummy = myCAM.rdSensorReg8_8(addr, &orgVal);
	myCAM.wrSensorReg8_8(addr, val);
	//myCAM.write_reg(addr, val);  // TODO: Don't really know which one to use. None of them seems to work
	dummy = myCAM.rdSensorReg8_8(addr, &checkVal);

	Serial.printf(" SET BYTE at addr 0x%x", addr);
	Serial.print(" setVal= "); Serial.print(val,BIN);
	Serial.print(" orgVal= "); Serial.print(orgVal, BIN);
	Serial.print(" checkVal= "); Serial.println(checkVal, BIN);
}

void takePicture(long &nbrOfBytes, short &finalWidth, short &finalHeight, short &pixelSize) {

	myCAM.set_format(JPEG);
	//-myCAM.set_format(BMP);
	myCAM.InitCAM();
	//-setCamBit(0x12, BIT7, 1);  // SCCB register reset
	// Working setup (next 2 lines):
	setCamBit(0x12, BIT2, 0); setCamBit(0x12, BIT0, 0); // YUV 
	setCamBit(0x40, BIT4, 0); // Raw RGB (in combination with above)
	setCamBit(0x13, BIT2, 1);  // AGC enable/disable

	// Set up special effects on cam
	//setCamByte(0x70, 0b10001000); // Grayscale test pattern + scaling
	//setCamBit(0x12, BIT2, 0); setCamBit(0x12, BIT0, 0); // YUV 
	//setCamBit(0x12, BIT2, 1); setCamBit(0x12, BIT0, 0); // Raw RGB 
	//setCamBit(0x40, BIT4, 0); // Raw RGB (in combination with above)
	//setCamBit(0x13, BIT2, 1);  // AGC enable/disable
	//setCamBit(0x12, BIT1, 1); // Color bar enable
	//setCamBit(0x70, BIT7, 1); setCamBit(0x71, BIT7, 0); // 8-bar color bar
	//setCamByte(0x01, 0x00); // AWB blue channel gain (0 - FF)
	//setCamByte(0x02, 0x00); // AWB red channel gain (0 - FF)

	//Flush the FIFO
	myCAM.flush_fifo();
	//Clear the capture done flag
	myCAM.clear_fifo_flag();
	//Start capture
	myCAM.start_capture();
	//Serial.println(F("take picture."));
	while (!myCAM.get_bit(ARDUCHIP_TRIG, CAP_DONE_MASK));
	//Serial.println(F("Capture Done."));
	nbrOfBytes = myCAM.read_fifo_length();
	#ifdef DEBUG_PRINT
	Serial.print(F("\nTake picture. Size reported from camera is: "));
	Serial.println(nbrOfBytes, DEC);
	#endif

	// we know we are going to crop the image later, so we reduce the number know. 
	//nbrOfBytes = (nbrOfBytes * (YCROP_END - YCROP_START + 1)) / IMAGE_HEIGHT;
	finalWidth = (XCROP_END - XCROP_START + 1);
	finalHeight = (YCROP_END - YCROP_START + 1);
	pixelSize = BYTES_PER_PIXEL;
	nbrOfBytes = finalWidth  * finalHeight * BYTES_PER_PIXEL;
	#ifdef DEBUG_PRINT
	Serial.print(F("After cropping, it is: "));
	Serial.println(nbrOfBytes, DEC);
	#endif


	// prepare camera for burst readout
	myCAM.CS_LOW();
	myCAM.set_fifo_burst();
}

void readPicFromCamToImageBuffer(byte *buf, long nbrOfBytes) {
	int i = 0;
	uint32_t length = nbrOfBytes;

	if (length >= MAX_FIFO_SIZE)
	{
		Serial.println(F("readPicFromCamToImageBuffer - Over size. STOPPING"));
		while (true);
		return;
	}
	if (length == 0) //0 kb
	{
		Serial.println(F("readPicFromCamToImageBuffer - Size is 0. STOPPING"));
		while (true);
		return;
	}
	if (length >= MAX_IMAGE_SIZE)
	{
		Serial.println(F("readPicFromCamToImageBuffer - Size is larger than MAX_IMAGE_SIZE.  STOPPING"));
		while (true);
		return;
	}

	//Read image data from FIFO into buf
	i = 0;
	while (length-- > 0)
	{
		buf[i++] = SPI.transfer(0x00);
//		if (i % 50 == 0) {
//			Serial.print(i-1); Serial.print("=");; Serial.print((int)buf[i-1]); Serial.print(" ");
//		}
	}
	#ifdef DEBUG_PRINT
	Serial.print(i - 1); Serial.print("=");; Serial.print((int)buf[i - 1]); Serial.print(" ");
	Serial.println(F("Image saved to imageBuffer!"));
	#endif
}

int readChunkFromCamToBuffer(byte *buf, long nbrOfBytes) {

	int i = 0;
	if (nbrOfBytes > 0) {
		uint32_t length = nbrOfBytes;

		while (length-- > 0)
		{
			buf[i] = SPI.transfer(0x00);
			i++;
		}
	}
	//Serial.println(F("Chunk read from camera FIFO"));
	return i;
}

void readAndDiscardFirstByteFromCam() {
	// Don't know why, but to keep the JPEG/JFIF spec, the first byte needs to go. (https://www.fastgraph.com/help/jpeg_header_format.html)
	char tmp = SPI.transfer(0x00);
}

void flushCamFIFO() {
	myCAM.flush_fifo();
}

void closeCamera() {
	myCAM.CS_HIGH();
}
