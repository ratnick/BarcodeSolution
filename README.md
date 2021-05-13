# BarcodeSolution
Arduino (WeMOS D1 mini) based barcode reader using cheap camera and Azure services.
Complete setup guide found here.

Contents

1	ArduCAM + Arduino Mini D1	
1.2	Installing OV7670 WITH ARDUCAM REV C+ camera shield with SERIAL DATA interface to Wemos D1 mini	
1.2.1	ArduCAM Rev C+ Pin connections:	
1.2.2	Hardware Circuit Connection btwn D1 and Arducam	
1.2.3	SW setup	
1.2.4	Wiring OV7670 w/o FIFO to the Arducam board	
1.2.5	Basic “is it alive” setup	
1.2.6	Wiring OV7670 FIFO	
1.2.10	Converting RAW to image	

2	Set up Arduino for camera	
2.3	Installing OV7670 NO FIFO camera	
2.3.1	Wiring OV7670 NO FIFO	

2.4	Installing OV7670 WITH FIFO (=AL422) camera	
2.4.1	Basic “is it alive” setup	
2.4.2	Wiring OV7670 FIFO	

2.5	Installing wifi shield ESP-01 ESP-8266	1
2.5.1	Wiring	
2.5.2	Booting	

3	ESP8266 Arduino board (ESP12-E Arduino = D1)	

4	Barcode reader programming	
4.1	Test programs for development	
4.1.1	29/11-16: sending messages to IOT hub from Arduino using Visual Studio:	
4.2	Sending a file over wifi	
