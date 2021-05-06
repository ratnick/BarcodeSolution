#r "Microsoft.WindowsAzure.Storage"
#r "System.Drawing"
#r "System.Data"
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

// For imaging operations
using System.Drawing;               // for bitmaps
using System.Drawing.Imaging;       // for bitmapdata
using System.Data.Common;
//using System.Media;               // for jpeg encoder
using System.Runtime;
using System.Runtime.InteropServices;  // for byte array copying

// copy of "Constants.h" in AzureClient.ino 
static string IOTHUB_HOSTNAME = "NNRiothub.azure-devices.net";
static string STORAGE_HOSTNAME = "nnriothubstorage.blob.core.windows.net";
static string STORAGE_SAS = "?sv=2016-05-31&ss=bfqt&srt=sco&sp=rwdlacup&se=2017-10-28T22:04:25Z&st=2017-06-07T14:04:25Z&spr=https&sig=iBGNG49IuBvRrci8OR%2BycoYfaqgcZ7j%2FyfF87x%2BdK8s%3D";
static string STORAGE_CONTAINER_NAME = "deviceimages";
static string WEBAPP_HOSTNAME = "nnriotwebapps.azurewebsites.net";
static string WEBAPP_FUNCTION_NAME = "HttpPOST-processing";
static string WEBAPP_FUNCTION_KEY = "JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA==";
static string WEBAPP_FUNCTION_URL = "https://nnriotWebApps.azurewebsites.net/api/HttpPOST-processing?code=JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA==";
static string WEBAPP_CMD_BARCODE_CREATED = "Barcode created";
static string FIXED_BLOB_NAME = "photo.RAW";  // for more advanced cameras like 2640: "photo.JPEG";
static string THIS_DEVICE_NAME = "ArduinoD1_001";
static string THIS_DEVICE_SAS = "elB/d4TY5poTH8PpWH88EbqB8FHaGWSVRQ+INnorYPc=";
static string STORAGE_ACCOUNT_CS = "DefaultEndpointsProtocol=https;AccountName=nnriothubstorage;AccountKey=Dwho3wxRlVaHbIgoQCuUSU0EjZlCunAdah3+JU7syboA4KCJoDjp+7KGI09rTRRRRSAre++FFR1WRbDFCfpc+g==;";

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

private static byte clamp(double  x, byte x_min, byte x_max)
{
    if (x < x_min)  { return x_min; }
    if (x > x_max)  { return x_max; }
    return (byte)x;
}

private static void yvu422_RGB(byte[] yvu, int i, ref Color pix1, ref Color pix2)
{
    
    i--;
    byte Y1 = yvu[++i];  //best
    byte Cr = yvu[++i];
    byte Y0 = yvu[++i];  
    byte Cb = yvu[++i];
    double B0 = Y0 + 1.4075 * (Cb - 128);
    double G0 = Y0 - (0.3455 * (Cr - 128)) - (0.7169 * (Cb - 128));
    double R0 = Y0 + (1.7790 * (Cr - 128));
    double B1 = Y1 +                         1.4075 * (Cb - 128);
    double G1 = Y1 - (0.3455 * (Cr - 128)) - (0.7169 * (Cb - 128));
    double R1 = Y1 + (1.7790 * (Cr - 128));

    pix1 = Color.FromArgb(1,  clamp(R0,0,255), clamp(G0,0,255), clamp(B0,0,255));
    pix2 = Color.FromArgb(1,  clamp(R1,0,255), clamp(G1,0,255), clamp(B1,0,255));
}

private static void DumpBinaryData(byte[] buf, long nbrOfBytes, TraceWriter log) {

	long i = 0;
    string s = "";

    s = s + "\ndumpBinaryData: " + nbrOfBytes.ToString() + " bytes  \n";

	while (i < nbrOfBytes)
	{
        s = s + buf[i++].ToString("X") + " ";
        if (i % 8 == 0)
        {
            s = s + "\n";
        }
	}
    //s = s + "\n";
    log.Info(s);
}

public static CloudBlob ConvertImageFileFormat(CloudBlob blob, int imageWidth, int imageHeight, TraceWriter log)
{
    // define an image (bitmap) with the right characteristics matching the input given in the HTTP request
    Size size = new Size(imageWidth, imageHeight);
    //Size size = new Size(320, 240);
    //Size size = new Size(640, 480);

    // read the blob length
    blob.FetchAttributes(); 
    long rawfileLength = blob.Properties.Length;
    log.Info("ConvertImageFileFormat: rawfileLength=" + rawfileLength + " width=" + size.Width + " height=" + size.Height);

    // read the raw blob data into a byte array (imageData)
    byte[] rawData = new byte[rawfileLength];
    byte[] modifData = new byte[rawfileLength];
    blob.DownloadToByteArray(rawData,0);
    log.Info("ConvertImageFileFormat: downloaded blob");
    //for (int i=0; i<rawfileLength;  i++)    { log.Info("ConvertImageFileFormat: rawData=" + rawData[i]);   }

    // Modify original picture (easier here than on Arduino)

    /* 180211 New algorithm: 
    //DumpBinaryData(rawData, rawfileLength, log); // use only with very small images


    /*180210-    */
    PixelFormat pxFormat = PixelFormat.Format16bppRgb565;  
    var bitmap = new Bitmap(size.Width, size.Height, pxFormat);
    Color pix1 = Color.Black; Color pix2 = Color.Black;
    const int pixelSize = 2; // 2 bytes per pixel (YUV422 and RGB565)
    int pixWidth = pixelSize * size.Width; // 2 bytes per pixel in RGB565
    //int pixHeight = pixelSize * imageHeight;
    //int totalImageSize = pixelSize * imageHeight * imageWidth;
    int i = 0;
    int j = 0;

    // Mirror the picture line by line
    
    //System.IntPtr modifDataIptr = &modifData[];
    //modifDataIptr= Marshal.AllocHGlobal(rawfileLength);
    //System.Runtime.InteropServices.Marshal.Copy(rawData, 0, modifDataIptr, rawfileLength);
    for (int h=0; h<size.Height;h++)
    {
        int offset = h * pixWidth;
        for (int w=0; w<pixWidth;w++)
        {
            i = offset + w;
            j = offset + (pixWidth - 1) - w;
            if (i < 0 || i >= rawfileLength || j < 0 || j >= rawfileLength)
            {
                log.Info("h=" + h + " offset=" + offset + " pixw=" + pixWidth + " i=" + i + " j=" + j); 
            } else
            {
                modifData[i] = rawData[j];
            }
        }
    }
    for (i = 0; i< rawfileLength; i++) {
        rawData[i] = modifData[i];
    }
    log.Info("ConvertImageFileFormat: mirroring done");
    

    // copy the raw data into the bitmapdata

    
    //180210+ begin
    //Buffer.BlockCopy(modifData, 0, rawData, 0, totalImageSize);  // then we can keep the result in modifData
    for (int y = 0; y < imageHeight; y++)
    {
        for (int x = 0; x < imageWidth; x += 2)
        {
            i = (y * imageWidth * pixelSize) + x * 2;
            yvu422_RGB(rawData, i, ref pix1, ref pix2);
            bitmap.SetPixel( x+1, imageHeight - 1 - y, pix1);
            bitmap.SetPixel( x  , imageHeight - 1 - y, pix2);
        }
    }
    //180210+ end  */
    
    /*180210- ORG Algorithm
    PixelFormat pxFormat = PixelFormat.Format24bppRgb; 
    var bitmap = new Bitmap(size.Width, size.Height, pxFormat);
    
    Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
    BitmapData bData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);

    IntPtr ptr = bData.Scan0;
    int len = modifData.Length;
//    IntPtr modifDataIptr = modifData;
    
    log.Info("ConvertImageFileFormat: modifData.Length=" + modifData.Length + " bitmap.Width=" + bitmap.Width + " bitmap.Height=" + bitmap.Height + " ptr=" + ptr + " bdata.Stride=" + bData.Stride);
    System.Runtime.InteropServices.Marshal.Copy(modifData, 0, ptr, len);
    //System.Runtime.InteropServices.Marshal.Copy(modifData, ptr, 0, modifData.Length);
    log.Info("ConvertImageFileFormat: x5");
    bitmap.UnlockBits(bData);
    log.Info("ConvertImageFileFormat: x6");
    log.Info("ConvertImageFileFormat: (bData.Stride * bData.Height)=" + bData.Stride + "*" + bData.Height + "= " + (bData.Stride * bData.Height) + " Should equal size of blob");
    /**/

    // Dump imagedata
    /*180210- 
    //IntPtr ptr = bData.Scan0;
    int bytes  = Math.Abs(bData.Stride) * bitmap.Height;
    byte[] rgbValues = new byte[bytes];
    // Copy the RGB values into the array (only used for printout).
    System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);
    //for (int i=0; i<5;  i++)    { log.Info("ConvertImageFileFormat: rgbValues=" + rgbValues[i]);   }
    /**/


    // 180211: Untouched from here

    // Encode to JPEG
    //create an encoder parameter for the image quality
    EncoderParameter qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
    //get the jpeg codec
    ImageCodecInfo imgCodec = GetEncoderInfo("image/jpeg");
    //create a collection of all parameters that we will pass to the encoder
    EncoderParameters encoderParams = new EncoderParameters(1);
    //set the quality parameter for the codec
    encoderParams.Param[0] = qualityParam;

    // copy the bitmapdata to a new memorystream in the desired file format
    MemoryStream outStream = new MemoryStream();
    outStream.Position = 0;
    bitmap.Save(outStream, imgCodec, encoderParams); 

    log.Info("ConvertImageFileFormat: memStream data: " + outStream.Length + " pos: " + outStream.Position );

    // Retrieve storage account from connection string.
    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(STORAGE_ACCOUNT_CS);
    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
    CloudBlobContainer container = blobClient.GetContainerReference(STORAGE_CONTAINER_NAME);
    log.Info("ConvertImageFileFormat: x8");

    string newFilename = blob.Name + ".jpg";
    CloudBlockBlob newBlob = container.GetBlockBlobReference(newFilename);
    outStream.Position = 0;
    newBlob.UploadFromStream(outStream);
    log.Info("ConvertImageFileFormat: JPG generated:" + newFilename);

    // and also save to BMP for debugging
    outStream.Position = 0;
    bitmap.Save(outStream, System.Drawing.Imaging.ImageFormat.Bmp);
    string bmpFilename = blob.Name + ".bmp";
    CloudBlockBlob bmpBlob = container.GetBlockBlobReference(bmpFilename);
    outStream.Position = 0;
    bmpBlob.UploadFromStream(outStream);
    log.Info("ConvertImageFileFormat: BMP generated:" + bmpFilename );

    return newBlob;
}

private static ImageCodecInfo GetEncoderInfo(String mimeType)
{
    int j;
    ImageCodecInfo[] encoders;
    encoders = ImageCodecInfo.GetImageEncoders();
    for(j = 0; j < encoders.Length; ++j)
    {
        if(encoders[j].MimeType == mimeType)
            return encoders[j];
    }
    return null;
}

public static async Task<HttpResponseMessage> Run(HttpRequestMessage request, TraceWriter log)
{
    MemoryStream imageData = new MemoryStream();

    log.Info("C# HTTP trigger function processed a request.");

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
    int imageWidth = data.imageWidth;
    int imageHeight = data.imageHeight;

    //string connectionString = AmbientConnectionStringProvider.Instance.GetConnectionString(ConnectionStringNames.Storage); 
    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(STORAGE_ACCOUNT_CS);
    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
    CloudBlobContainer container = blobClient.GetContainerReference(containerName);
    log.Info("X1"); 

    container.CreateIfNotExists();
    log.Info("blobPathAndName=" + blobPathAndName); 
    CloudBlob blob = container.GetBlobReference(blobPathAndName);

    if (BlobExistsOnCloud(blobClient, containerName, blobPathAndName)) {
        log.Info("BLOB EXISTS"); 
        CloudBlob convertedBlob = ConvertImageFileFormat(blob, imageWidth, imageHeight, log);

        // recognize converted blob

        // Get SAS string to access the blob (is this needed at all? I think I set global read access to all blobs ion the container. TBD)
        string sas = convertedBlob.GetSharedAccessSignature(
            new SharedAccessBlobPolicy() { 
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessStartTime = DateTime.Now.AddMinutes(-261),
                SharedAccessExpiryTime = DateTime.Now.AddMinutes(260) 
            });
        log.Info("RecognizeBlob: convertedBlob.Name=" + convertedBlob.Name + " containerName=" + containerName + " Uri=" + convertedBlob.Uri);
        //log.Info("RecognizeBlob: BLOB SAS:" + sas);
        string blobURL = "https://nnriothubstorage.blob.core.windows.net/" + containerName + "/" + convertedBlob.Name;
        //string blobURL = "https://nnriothubstorage.blob.core.windows.net/" + convertedBlob.Container + "/" + convertedBlob.Name + sas;
        //næste linie er ren testlinie. Erstat med rigtig blob.
        //blobURL = "https://nnriothubstorage.blob.core.windows.net/input/ArduinoD1_001/barcodecomparison.jpg" + sas;
        //blobURL = "https://nnriothubstorage.blob.core.windows.net/input/ArduinoD1_001/barcodecomparison.jpg?sv=2015-12-11&sr=b&sig=IJ%2F4QXDOxbVL940laFJAq6yEOJ2vYIL1UrnovNVzj7w%3D&st=2017-06-16T11%3A24%3A57Z&se=2017-06-16T12%3A23%3A57Z&sp=r";
        //log.Info("blobUrl = " + blobURL);

        // Retrieve response from Haven OnDemand server
        string havenURL = "http://api.havenondemand.com/1/api/sync/recognizebarcodes/v1?apikey=16b5dc6f-94f6-40eb-be96-62b7d49d7dc7&url=" + blobURL;
        log.Info("havenURL = " + havenURL);
        HttpClient client = new HttpClient();
        var uri = new Uri(havenURL);
        Stream respStream = await client.GetStreamAsync(uri);
        ReadResponseFromHaven(respStream, ref barcodeStr, ref barcodetype, log);
        log.Info("barcode=" + barcodeStr + ". Type:" + barcodetype);

        // Recognize barcode using Aspose (now replaced by Haven OnDemand):
        //barcodeStr = ReadBarcodeFromExternalImageURL.Run(blobURL, log);

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

public static bool BlobExistsOnCloud(CloudBlobClient client, string containerName, string key)
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

public static async Task NOTUSEDMyAsyncMethod(string blobUrl, TraceWriter log)
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
