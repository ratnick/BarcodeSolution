// Include next line in WebApp job on Azure server
//#r "Microsoft.WindowsAzure.Storage"
// *******************************************
//HttpPOST-processing in nnriotWebApps 
// *******************************************

using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage; // Namespace for CloudStorageAccount
using Microsoft.WindowsAzure.Storage.Blob; // Namespace for Blob storage types 
using Microsoft.WindowsAzure.Storage.Queue;
//using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Net;
using System.IO; 


// For Aspose 
using Com.Aspose.Barcode.Api;
using Com.Aspose.Barcode.Model;
using System.Collections.Generic;
using System.Configuration;

// For Haven OnDemand barcode service
using System.Net.Http; 
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HttpPOST_processing2
{
    // To learn more about Microsoft Azure WebJobs SDK, please see https://go.microsoft.com/fwlink/?LinkID=320976
    class Program
    {
        // Please set the following connection strings in app.config for this WebJob to run:
        // AzureWebJobsDashboard and AzureWebJobsStorage
        static void Main()
        {
            var config = new JobHostConfiguration();

            if (config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
            }

            var host = new JobHost();
            // The following code ensures that the WebJob will be running continuously
            host.RunAndBlock();
        }
    }
}



public static object DeserializeFromStream(Stream stream)
{
    var serializer = new JsonSerializer();

    using (var sr = new StreamReader(stream))
    using (var jsonTextReader = new JsonTextReader(sr))
    {
        return serializer.Deserialize(jsonTextReader);
    }
}

public static void ReadResponseFromHaven(Stream respStream, ref string barcodeStr, ref string barcodetype, TraceWriter log)
{
    log.Info("ReadResponseFromHaven: start");
    
    // For testing
//    StreamReader reader = new StreamReader(respStream);
//    string text = reader.ReadToEnd();
//    log.Info("ReadResponseFromHaven: respStream=" + text);
    // until here

    dynamic fullRespJson = DeserializeFromStream(respStream);
    log.Info("ReadResponseFromHaven: fullRespJson=" + fullRespJson);

    barcodeStr = (string)fullRespJson.SelectToken("barcode[0].text");
    barcodetype = (string)fullRespJson.SelectToken("barcode[0].barcode_type");
    //log.Info("barcode=" + barcodeStr + ". Type:" + barcodetype);
    return;
}

public static async Task<HttpResponseMessage> Run(HttpRequestMessage request, TraceWriter log)
{
    MemoryStream imageData = new MemoryStream();

    //log.Info("C# HTTP trigger function processed a request.");

    // parse query parameter   
    string msgType = request.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "msgType", true) == 0)
        .Value;

    // Get request body 
    dynamic data = await request.Content.ReadAsAsync<object>();

    // Set name to query string or body data  
    msgType = msgType ?? data?.msgType; 

    log.Info("BEGIN: msgType=" + msgType);
    log.Info("HTTP body (=data):" + data);

    string barcodeStr = "barcode not assigned";
    string barcodetype = "n/a";
    string deviceId = data.deviceId;
    string blobName = data.BlobName;
    string containerName = data.BlobContainer; //"nnriothubcontainer";
    string blobPathAndName = data.BlobPath + "/" + blobName; //pathName + blobName;

    //string connectionString = AmbientConnectionStringProvider.Instance.GetConnectionString(ConnectionStringNames.Storage); 
    string connectionString = "DefaultEndpointsProtocol=https;AccountName=nnriothubstorage;AccountKey=Dwho3wxRlVaHbIgoQCuUSU0EjZlCunAdah3+JU7syboA4KCJoDjp+7KGI09rTRRRRSAre++FFR1WRbDFCfpc+g==;EndpointSuffix=core.windows.net";
    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
    CloudBlobContainer container = blobClient.GetContainerReference(containerName);
    log.Info("X1"); 

    container.CreateIfNotExists();
    log.Info("blobPathAndName=" + blobPathAndName); 
//    CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
    CloudBlob blob = container.GetBlobReference(blobPathAndName);

    if (BlobExistsOnCloud(blobClient, containerName, blobPathAndName)) {
        log.Info("BLOB EXISTS"); 
        // Get SAS string to access the blob
        string sas = blob.GetSharedAccessSignature(
            new SharedAccessBlobPolicy() { 
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessStartTime = DateTime.Now.AddMinutes(-261),
                SharedAccessExpiryTime = DateTime.Now.AddMinutes(260) 
            });
        log.Info("Blob info: Name=" + blob.Name + " Container=" + blob.Container + " Uri=" + blob.Uri);
        log.Info("BLOB SAS:" + sas);

        // HERTIL: næste linie er ren testlinie. Erstat med rigtig blob.
        string blobURL = "https://nnriothubstorage.blob.core.windows.net/" + containerName + "/" + blobPathAndName + sas;

        //blobURL = "https://nnriothubstorage.blob.core.windows.net/input/ArduinoD1_001/barcodecomparison.jpg" + sas;
        //blobURL = "https://nnriothubstorage.blob.core.windows.net/input/ArduinoD1_001/barcodecomparison.jpg?sv=2015-12-11&sr=b&sig=IJ%2F4QXDOxbVL940laFJAq6yEOJ2vYIL1UrnovNVzj7w%3D&st=2017-06-16T11%3A24%3A57Z&se=2017-06-16T12%3A23%3A57Z&sp=r";

        log.Info("blobUrl = " + blobURL);

        // Recognize barcode using Aspose:
        //barcodeStr = ReadBarcodeFromExternalImageURL.Run(blobURL, log);

        // Retrieve response from Haven OnDemand server

        string havenURL = "http://api.havenondemand.com/1/api/sync/recognizebarcodes/v1?apikey=16b5dc6f-94f6-40eb-be96-62b7d49d7dc7&url=" + blobURL;
        //havenURL = "http://api.havenondemand.com/1/api/sync/recognizebarcodes/v1?apikey=16b5dc6f-94f6-40eb-be96-62b7d49d7dc7&url=http://help.accusoft.com/SAAS/pcc-for-acs/images/Code39.jpg";
        log.Info("havenURL = " + havenURL);
        HttpClient client = new HttpClient();
        var uri = new Uri(havenURL);
        Stream respStream = await client.GetStreamAsync(uri);
        ReadResponseFromHaven(respStream, ref barcodeStr, ref barcodetype, log);
        log.Info("barcode=" + barcodeStr + ". Type:" + barcodetype);

    } else {
        log.Info("BLOB NOT FOUND");
    } 

    return msgType == null
        ? request.CreateResponse(HttpStatusCode.BadRequest, "HTTP parameter must contain msgType=<type> on the query string or in the request body")
        : request.CreateResponse(HttpStatusCode.OK, "Hello " + deviceId + " blob size: " + imageData.Length + " Barcode: " + barcodeStr);
}

//========================================================
class Common
{
    public static string APP_SID = "fca8d3dc-a821-493c-8417-740de1131a79";
    public static string APP_KEY = "06dcc50d89436afc82eee57b4856df67";
    public static string FOLDER = " ";
    public static string STORAGE = "";
    public static string BASEPATH = "http://api.aspose.cloud/v1.1";
    // public static string BASEPATH = "http://localhost:8080/v1.1";
    public static string OUTFOLDER = "C://temp/";
}

class ReadBarcodeFromExternalImageURL
{
    public static string Run(string blobUrl, TraceWriter log)
    {

        // Instantiate Aspose BarCode Cloud API SDK
        BarcodeApi barcodeApi = new BarcodeApi(Common.APP_KEY, Common.APP_SID, Common.BASEPATH);
        String barcodeStr = "(n/a)";

        // The barcode type. If this parameter is empty, autodetection of all supported types is used.
        String barcodetype = ""; //Code39Standard";  //leave empty in production. In Aspose evaluation version, only Code39 is possible.

        // Set mode for checksum validation during recognition 
        String checksumValidation = "Default";

        // Set if FNC symbol stripping should be performed
        bool stripFnc = false;

        // Set recognition of rotated barcode
        int rotationAngle = 0;

        //Set the image file url 
        String url = blobUrl; //"http://www.barcoding.com/images/Barcodes/code93.gif";
        //url = "https://www.nationwidebarcode.com/wp-content/uploads/2011/09/code39.jpg";
        byte[] file = null;

        log.Info("Try barcode URL: " + url);
        log.Info("Parameters: barcodetype=" + barcodetype + " checksumValidation=" + checksumValidation + " stripFnc=" + stripFnc +
            " rotationAngle=" + rotationAngle + " url=" + url + " file=" + file);

        BarcodeResponseList apiResponse;
        try
                {
                    // Invoke Aspose.BarCode Cloud SDK API to read barcode from external image URL
                    //BarcodeResponseList apiResponse = barcodeApi.PostBarcodeRecognizeFromUrlorContent(barcodetype, checksumValidation, stripFnc, rotationAngle, url, file);
                    apiResponse = barcodeApi.PostBarcodeRecognizeFromUrlorContent(barcodetype, checksumValidation, stripFnc, rotationAngle, url, file);

                    if (apiResponse != null)
                    {
                        foreach (Barcode barcode in apiResponse.Barcodes)
                        {
                            log.Info("Barcode text: " + barcode.BarcodeValue + "\nType: " + barcode.BarcodeType);
                            barcodeStr = barcode.BarcodeValue;
                        }
                        log.Info("Read Barcode from External Image URL, Done!");
                    }
                }
                catch (Exception ex)
                {
                    log.Info("error from barcode service:" + ex.Message + "\n" + ex.StackTrace);
                    barcodeStr = "Can't read barcode: " + ex.Message ; 
                }

        return barcodeStr;
        // ExEnd:1
    }
}
 

public static bool BlobExistsOnCloud(CloudBlobClient client, 
    string containerName, string key)
{
     return client.GetContainerReference(containerName)
                  .GetBlockBlobReference(key)
                  .Exists();  
}

// called as:   RunAsyncNOTUSED(MyAsyncMethodNOTUSED(blobURL, log),log);
private static void RunAsyncNOTUSED(Task task, TraceWriter log)
{
    task.ContinueWith(t =>
    {
        //ILog log = ServiceLocator.Current.GetInstance<ILog>();
        log.Error("Unexpected Error", t.Exception);
    }, TaskContinuationOptions.OnlyOnFaulted);
}

public static async Task MyAsyncMethodNOTUSED(string blobUrl, TraceWriter log)
{

    String barcodeStr = "(n/a)";
    String barcodetype = "";
    String url = "http://help.accusoft.com/SAAS/pcc-for-acs/images/Code39.jpg";
    url = "http://api.havenondemand.com/1/api/sync/recognizebarcodes/v1?apikey=16b5dc6f-94f6-40eb-be96-62b7d49d7dc7&url=http://help.accusoft.com/SAAS/pcc-for-acs/images/Code39.jpg";
    log.Info("MyAsyncMethod: Try barcode URL: " + url);


    //static string _address = "http://maps.googleapis.com/maps/api/staticmap?center=Redmond,WA&zoom=14&size=400x400&sensor=false";
    HttpClient client = new HttpClient();

    /*// Method 1:
    // Send asynchronous request
    HttpResponseMessage response = await client.GetAsync(url);
    // Check that response was successful or throw exception
    response.EnsureSuccessStatusCode();
    log.Info("response.Content=" + response.Content);
*/    // End Method 1


/*    // Method 2 (works):
    Task<string> getStringTask = client.GetStringAsync(url);
    // You can do work here that doesn't rely on the string from GetStringAsync.
    //DoIndependentWork();
    // The await operator suspends AccessTheWebAsync. 
    //  - AccessTheWebAsync can't continue until getStringTask is complete. 
    //  - Meanwhile, control returns to the caller of AccessTheWebAsync. 
    //  - Control resumes here when getStringTask is complete.  
    //  - The await operator then retrieves the string result from getStringTask. 
    string urlContents = await getStringTask;
    log.Info("urlContents=" + urlContents);
    // End Method 2
*/

    // Method 3:
    var uri = new Uri(url);
    Stream respStream = await client.GetStreamAsync(uri);
    dynamic fullRespJson = DeserializeFromStream(respStream);
    log.Info("MyAsyncMethod full respJson=" + fullRespJson);

    barcodeStr = (string)fullRespJson.SelectToken("barcode[0].text");
    barcodetype = (string)fullRespJson.SelectToken("barcode[0].barcode_type");
    log.Info("barcode=" + barcodeStr + ". Type:" + barcodetype);


    // Read response asynchronously and save asynchronously to file
    /*using (FileStream fileStream = new FileStream("output.png", FileMode.Create, FileAccess.Write, FileShare.None))
    {
        await response.Content.CopyToAsync(fileStream);
    }

    Process process = new Process();
    process.StartInfo.FileName = "output.png";
    process.Start();
    */

    //return barcodeStr;
    // ExEnd:1
    return;
}
