#include <ArduinoJson.h>

#include <VirtualShield.h>
#include <Text.h>
#include <Speech.h>
#include <Recognition.h>

#include <SPI.h>
#include <WiFi101.h>


VirtualShield shield;	          // identify the shield
Text screen = Text(shield);	  // connect the screen
Speech speech = Speech(shield);	  // connect text to speech
Recognition recognition = Recognition(shield);	  // connect speech to text

int queueNum = 0;

int LED_PIN = 8;

///*** WiFi Network Config ***///
char ssid[] = "yourNetwork"; //  your network SSID (name)
char pass[] = "secretPassword";    // your network password (use for WPA, or use as key for WEP)

///*** Azure IoT Hub Config ***///
//see: http://mohanp.com/  for details on getting this right if you are not sure.

char hostname[] = "YourIoTHubName.azure-devices.net";    // host name address for your Azure IoT Hub
char feeduri[] = "/devices/SmartQueueMKR1000/messages/events?api-version=2016-02-03"; //feed URI
char authSAS[] = "YourSharedAccessKey";

///*** Azure IoT Hub Config ***///

int status = WL_IDLE_STATUS;

WiFiSSLClient client;

String message;

void recognitionEvent(ShieldEvent* event) 
{
  if (event->resultId > 0) {
  	digitalWrite(LED_PIN, recognition.recognizedIndex == 1 ? HIGH : LOW);
    screen.printAt(6, "Heard " + String(recognition.recognizedIndex == 1 ? "next" : "stop"));

    if (recognition.recognizedIndex == 1) {
      queueNum++;
      message =  "Currently serving queue #" + String(queueNum);
      screen.printAt(8, message);

      azureHttpRequest(message);
      //Serial.println();
      //Serial.println(message);
    }
    
  	recognition.listenFor("next,stop", false);	    // reset up the recognition after each event
  }
}

// when Bluetooth connects, or the 'Refresh' button is pressed
void refresh(ShieldEvent* event) 
{
  // String message = "Hello Virtual Shields. Say the word 'on' or 'off' to affect the LED";
  String message = "Hello. Say the word 'next' to serve the next queue number.";
        
	screen.clear();
	screen.print(message);
  speech.speak(message); 

	recognition.listenFor("next,stop", false);	// NON-blocking instruction to recognize speech
}

void setup()
{
	pinMode(LED_PIN, OUTPUT);
	pinMode(LED_PIN, LOW);

  //check for the presence of the shield:
  if (WiFi.status() == WL_NO_SHIELD) {
    // don't continue:
    while (true);
  }

  // attempt to connect to Wifi network:
  while (status != WL_CONNECTED) {
    status = WiFi.begin(ssid, pass);
    // wait 10 seconds for connection:
    delay(10000);
  }

  // set up a function to handle recognition events (turns auto-blocking off)
	recognition.setOnEvent(recognitionEvent);	
  shield.setOnRefresh(refresh);

  // begin() communication - you may specify a baud rate here, default is 115200
	shield.begin(9600);

  randomSeed(analogRead(0));
}

void loop()
{
  String response = "";
  char c;
  ///read response if WiFi Client is available
  while (client.available()) {
    c = client.read();
    response.concat(c);
  }
  
	shield.checkSensors();		    // handles Virtual Shield events.
}

// this method makes an HTTPS connection to the Azure IOT Hub Server:
void azureHttpRequest(String content) {

  // close any connection before send a new request.
  // This will free the socket on the WiFi shield
  client.stop();

  String messageId = String(random(300)) + String(millis());
  //Serial.println();
  //Serial.println(messageId);

  String contentType = "text/plain";
  String accept = "application/json";
  
  // if there's a successful connection:
  if (client.connect(hostname, 443)) {
    //make the GET request to the Azure IOT device feed uri
    client.print("POST ");  //Do a POST
    client.print(feeduri);  // On the feedURI
    client.println(" HTTP/1.1"); 
    client.print("Host: "); 
    client.println(hostname);  //with hostname header
    // client.print("Accept: ");  // On the feedURI
    // client.println(accept); 
    client.print("Authorization: ");
    client.println(authSAS);  //Authorization SAS token obtained from Azure IoT device explorer
    client.print("IoTHub-MessageId: "); 
    client.println(messageId);  
    client.println("Connection: close");
    client.print("IoTHub-app-messageType: "); 
    client.println("interactive");  

    client.print("Content-Type: ");
    client.println(contentType);
    client.print("Content-Length: ");
    client.println(content.length());
    client.println();
    client.println(content);

    client.println();

  }
  else {
    // if you couldn't make a connection:
    Serial.println();
    Serial.println("connection failed");
  }

}
