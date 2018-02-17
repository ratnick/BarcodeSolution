using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;

namespace SimulatedDevice
{
    class Program
    {
        // Must be a copy from .../Arduino/AzureClient/Constants.h
        static string IOTHUB_HOSTNAME = "NNRiothub.azure-devices.net";
        static string STORAGE_HOSTNAME = "nnriothubstorage.blob.core.windows.net";
        static string STORAGE_SAS = "?sv=2016-05-31&ss=bfqt&srt=sco&sp=rwdlacup&se=2017-10-28T22:04:25Z&st=2017-06-07T14:04:25Z&spr=https&sig=iBGNG49IuBvRrci8OR%2BycoYfaqgcZ7j%2FyfF87x%2BdK8s%3D";
        static string STORAGE_CONTAINER_NAME = "deviceimages";
        static string WEBAPP_HOSTNAME = "nnriotwebapps.azurewebsites.net";
        static string WEBAPP_FUNCTION_NAME = "HttpPOST-processing";
        static string WEBAPP_FUNCTION_KEY = "JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA==";
        static string WEBAPP_FUNCTION_URL = "https://nnriotWebApps.azurewebsites.net/api/HttpPOST-processing?code=JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA==";
        static string WEBAPP_CMD_BARCODE_CREATED = "Barcode created";
        static string FIXED_BLOB_NAME = "photoSIMULATED.JPEG";
        static string THIS_DEVICE_NAME = "ArduinoD1_001";
        static string THIS_DEVICE_SAS = "elB/d4TY5poTH8PpWH88EbqB8FHaGWSVRQ+INnorYPc=";


        static DeviceClient deviceClient;
        static string iotHubUri = IOTHUB_HOSTNAME; 
        static string deviceName = THIS_DEVICE_NAME; 
        static string deviceKey = THIS_DEVICE_SAS; // Azure->NNRiothub->Device explorer->ArduinoD1_001 This code is unique for the device
        //static string fileName = FIXED_BLOB_NAME; 
        static string blobName = FIXED_BLOB_NAME; 




        private static async void SendToBlobAsync(string fileName)
        {
            
            Console.WriteLine("SendToBlobAsync: Uploading blob: {0}", fileName);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            using (var sourceData = new FileStream(@"barcode.jpg", FileMode.Open))
            {
                await deviceClient.UploadToBlobAsync(fileName, sourceData);
            }

            watch.Stop();
            Console.WriteLine("SendToBlobAsync: Time to upload blob: {0}ms\n", watch.ElapsedMilliseconds);
        }

        public void WebRequestinJson(string url, string postData)
        {
            StreamWriter requestWriter;

            var webRequest = System.Net.WebRequest.Create(url) as HttpWebRequest;
            if (webRequest != null)
            {
                webRequest.Method = "POST";
                webRequest.ServicePoint.Expect100Continue = false;
                webRequest.Timeout = 20000;

                webRequest.ContentType = "application/json";
                //POST the data.
                using (requestWriter = new StreamWriter(webRequest.GetRequestStream()))
                {
                    requestWriter.Write(postData);
                }
            }
        }

        private static void SendDeviceToCloudHttpFunctionRequest()
        {
            var telemetryDataPoint = new
                {
                deviceId = deviceName,
                msgType = WEBAPP_CMD_BARCODE_CREATED, //"Barcode created",
                MessageId = "12345",
                UTC = "2017-01-08T10:45:09",
                BlobContainer = STORAGE_CONTAINER_NAME, //"nnriothubcontainer",
                BlobPath = deviceName,
                BlobName = blobName,
                BlobSize = 9567,
                WiFiconnects = 1,
                ESPmemory = 7824,
                Counter = 1
            };

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
                        Console.WriteLine("{0} > HTTP POST: {1}", DateTime.Now, messageString);
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
        }

        private static async void OLD_SendDeviceToCloudMessagesAsync()
        {
            double avgWindSpeed = 10; // m/s
            Random rand = new Random();

            //while (true)
            //{
            double currentWindSpeed = avgWindSpeed + rand.NextDouble() * 4 - 2;

            var telemetryDataPoint = new
            {
                deviceId = deviceName,
                //windSpeed = currentWindSpeed,
                messageType = "Barcode created",
                MessageId = "12345",
                UTC = "2017-01-08T10:45:09",
                FullBlobName = "/input/ArduinoD1_001/test.jpg",
                BlobName = "test.jpg",
                BlobSize = 9567,
                WiFiconnects = 1,
                ESPmemory = 7824,
                Counter = 1
            };

            var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
            string levelValue;

            if (rand.NextDouble() > 0.7)
            {
                messageString = "This is a critical message";
                levelValue = "critical";
            }
            else
            {
                levelValue = "normal";
            }

            var message = new Message(Encoding.ASCII.GetBytes(messageString));
            message.Properties.Add("level", levelValue);

            await deviceClient.SendEventAsync(message);
            Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);

            //Task.Delay(10000).Wait();
            //Console.ReadLine();

            //}
        }

        static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("Simulated device\n");
                deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey(deviceName, deviceKey));
                SendToBlobAsync(blobName);
                Task.Delay(1000).Wait();  // the clouds needs time to settle.
                
                SendDeviceToCloudHttpFunctionRequest();
                Console.WriteLine("Press return to send another blob+message");
                Console.ReadLine();

            }
        }
        
    }
}
