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
static int BYTES_PER_PIXEL = 2; // 2 bytes per pixel (YUV422 and RGB565)
static bool COLOR_CONVERSION = true;


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

private static void DumpBinaryData(byte[] buf, long nbrOfBytes, TraceWriter log, string title) {

	long i = 0; 
    string s;

    if (nbrOfBytes > 32) {nbrOfBytes = 32;}

    s = "\ndumpBinaryData: " + nbrOfBytes.ToString() + " bytes of " + title + "\n";

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

static unsafe void SwapBytesPerLine(byte[] inOutData, int imageWidth, int imageHeight, TraceWriter log )
{
    int bytesPerLine = BYTES_PER_PIXEL * imageWidth;
    int imgSize = imageHeight * bytesPerLine;
    int i; int h; int w; 

    byte[] tmpData = new byte[imgSize];
    Buffer.BlockCopy(inOutData, 0, tmpData, 0, imgSize); 

    // Swap the bytes within each vertical line
    for (w=0; w<imageWidth; w++)
    {
        for (h=0; h<imageHeight*BYTES_PER_PIXEL; h+=2)
        {
            i = (w*imageHeight*BYTES_PER_PIXEL) + h;
            inOutData[i] = tmpData[i+1];
            inOutData[i+1] = tmpData[i];
        }
    }
    DumpBinaryData(inOutData, imgSize, log, "After SwapBytesPerLine "); // use only with very small images
}

static unsafe void ReverseBytesPerLine(byte[] inOutData, int imageWidth, int imageHeight, TraceWriter log)
{
    int bytesPerLine = BYTES_PER_PIXEL * imageWidth;
    int imgSize = imageHeight * bytesPerLine;
    int i; int h; int w; int j;

    byte[] tmpData = new byte[imgSize];
    Buffer.BlockCopy(inOutData, 0, tmpData, 0, imgSize); 

    // Mirror the picture line by line (vertical lines)
    for (w=0; w<imageWidth; w++)
    {
        j = (w+1)*imageHeight*BYTES_PER_PIXEL-1;
        for (h=0; h<imageHeight*BYTES_PER_PIXEL; h++)
        {
            i = (w*imageHeight*BYTES_PER_PIXEL) + h;
            inOutData[i] = tmpData[j--];
        }
    }
    DumpBinaryData(inOutData, imgSize, log, "After ReverseBytesPerLine "); 
}

private static byte clamp(double  x, byte x_min, byte x_max)
{
    if (x < x_min)  { return x_min; }
    if (x > x_max)  { return x_max; }
    return (byte)x;
}

static byte asByte(int value)
{
    //return (byte)value;
    if (value > 255)
        return 255;
    else if (value < 0)
        return 0;
    else
        return (byte)value;
}

static unsafe void PixelYUV2RGB_BYTE(byte[] yvu, int i, ref byte r0, ref byte g0, ref byte b0, ref byte r1, ref byte g1, ref byte b1)
{
    i--;
    byte u = yvu[++i];
    byte y0 = yvu[++i];  
    byte v = yvu[++i]; 
    byte y1 = yvu[++i];  //best

    int C = y0 - 16;
    int D = u - 128;
    int E = v - 128;

    b0 = asByte((298 * C + 409 * E + 128) >> 8);
    g0 = asByte((298 * C - 100 * D - 208 * E + 128) >> 8);
    r0 = asByte((298 * C + 516 * D + 128) >> 8);

    C = y1 - 16;

    b1 = asByte((298 * C + 409 * E + 128) >> 8);
    g1 = asByte((298 * C - 100 * D - 208 * E + 128) >> 8);
    r1 = asByte((298 * C + 516 * D + 128) >> 8);
}

static unsafe void PixelYUV2RGB(byte[] yvu, int i, ref byte r0, ref byte g0, ref byte b0, ref byte r1, ref byte g1, ref byte b1)
{
    i--;
    byte y0 = (byte) (yvu[++i]) ;  //best
    byte u = yvu[++i]; 
    byte y1 = (byte) (yvu[++i]);   
    byte v = yvu[++i];

    if (COLOR_CONVERSION) { 
        b0 = clamp((y0 + 2.0321*u)             ,0,255);
        g0 = clamp((y0 - 0.3946*u - 0.58060*v) ,0,255);
        r0 = clamp((y0 + 1.1398*v)             ,0,255);

        b1 = clamp((y1 + 2.0321*u)             ,0,255);
        g1 = clamp((y1 - 0.3946*u - 0.58060*v) ,0,255);
        r1 = clamp((y1 + 1.1398*v)             ,0,255);
    } else {
        r0 = y0; g0 = y0; b0 = y0; 
        r1 = y1; g1 = y1; b1 = y1; 
    }
}

private static void yvu422_RGB(byte[] yvu, int i, ref Color pix1, ref Color pix2)
{
    i--;
    byte Y0 = yvu[++i];  
    byte Cb = yvu[++i];
    byte Y1 = yvu[++i];  //best
    byte Cr = yvu[++i]; 
    double R0 = Y0 + 1.4075 * (Cb - 128);
    double G0 = Y0 - (0.3455 * (Cr - 128)) - (0.7169 * (Cb - 128));
    double B0 = Y0 + (1.7790 * (Cr - 128));
    double R1 = Y1 +                         1.4075 * (Cb - 128);
    double G1 = Y1 - (0.3455 * (Cr - 128)) - (0.7169 * (Cb - 128));
    double B1 = Y1 + (1.7790 * (Cr - 128));

    pix1 = Color.FromArgb(1,  clamp(R0,0,255), clamp(G0,0,255), clamp(B0,0,255));
    pix2 = Color.FromArgb(1,  clamp(R1,0,255), clamp(G1,0,255), clamp(B1,0,255));
}

private static void yvu422_RGB_Direct(byte[] yvu, int i, ref byte r0, ref byte g0, ref byte b0, ref byte r1, ref byte g1, ref byte b1)
{
    
    i--;
    byte Y0 = yvu[++i];  
    byte Cb = yvu[++i];
    byte Y1 = yvu[++i];  //best
    byte Cr = yvu[++i]; 
    double R0 = Y0 + 1.4075 * (Cb - 128);
    double G0 = Y0 - (0.3455 * (Cr - 128)) - (0.7169 * (Cb - 128));
    double B0 = Y0 + (1.7790 * (Cr - 128));
    double R1 = Y1 +                         1.4075 * (Cb - 128);
    double G1 = Y1 - (0.3455 * (Cr - 128)) - (0.7169 * (Cb - 128));
    double B1 = Y1 + (1.7790 * (Cr - 128));

    r0 = clamp(R0, 0, 255); g0 = clamp(G0, 0, 255); b0 = clamp(B0,0,255);
    r1 = clamp(R1, 0, 255); g1 = clamp(G1, 0, 255); b1 = clamp(B1,0,255);
}

static unsafe void ConvertYVUtoRGB(byte[] inOutData, int imageWidth, int imageHeight, ref Bitmap bitmap, TraceWriter log) {
    log.Info("imageHeight=" + imageHeight.ToString() + " imageWidth=" + imageWidth.ToString() );

    int bytesPerLine = BYTES_PER_PIXEL * imageWidth;
    int imgSize = imageHeight * bytesPerLine;
    byte r0=0; byte g0=0; byte b0=0; byte r1=0;  byte g1=0;  byte b1=0;
    int i; int j; 
    byte[] tmpData = new byte[imgSize];
    Buffer.BlockCopy(inOutData, 0, tmpData, 0, imgSize); 

    j = 0;
    for (int y = 0; y < imageHeight; y++)
    {
        for (int x = 0; x < imageWidth; x += 2)
        {
            i = (y * imageWidth * BYTES_PER_PIXEL) + x * 2;
            //if (i % 100 == 0)  { log.Info("i=" + i.ToString() + " y=" + y.ToString() + " x=" + x.ToString()); }

            yvu422_RGB_Direct(tmpData, i, ref r0, ref g0, ref b0, ref r1, ref g1, ref b1);

            if (COLOR_CONVERSION) {
                inOutData[j++] = r0; inOutData[j++] = g0; inOutData[j++] = b0; inOutData[j++] = r1; inOutData[j++] = g1; inOutData[j++] = b1; 
                bitmap.SetPixel( x, imageHeight - 1 - y, Color.FromArgb(1,r0,g0,b0));
                bitmap.SetPixel( x+1  , imageHeight - 1 - y, Color.FromArgb(1,r1,g1,b1));
            } else {
                // next line WORKS for B/W: 
                PixelYUV2RGB(tmpData, i, ref r0, ref g0, ref b0, ref r1, ref g1, ref b1);
                inOutData[j++] = r0; inOutData[j++] = g0; inOutData[j++] = b0; inOutData[j++] = r1; inOutData[j++] = g1; inOutData[j++] = b1; 
                bitmap.SetPixel( x+1, imageHeight - 1 - y, Color.FromArgb(1,r0,g0,b0));
                bitmap.SetPixel( x  , imageHeight - 1 - y, Color.FromArgb(1,r1,g1,b1));
            }
        }
    }
    DumpBinaryData(inOutData, imgSize, log, "inOutData after ConvertYVUtoRGB"); 
}


public static void CopyArrayToBitmap(byte[] inOutData, long inputImageSize, ref Bitmap bitmap, TraceWriter log) {
    Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
    BitmapData bData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);

    IntPtr ptr = bData.Scan0;
    int len = (int) (inputImageSize/2)*3;
    log.Info("CopyArrayToBitmap: inOutData.Length=" + inOutData.Length + " bitmap.Width=" + bitmap.Width + " bitmap.Height=" + bitmap.Height + " ptr=" + ptr + " bdata.Stride=" + bData.Stride);
    System.Runtime.InteropServices.Marshal.Copy(inOutData, 0, ptr, len);
    log.Info("CopyArrayToBitmap: x5");
    bitmap.UnlockBits(bData);
    log.Info("CopyArrayToBitmap: x6");
    if ((bData.Stride * bData.Height) != inputImageSize)
    {
        log.Info("ConvertImageFileFormat WARNING: (bData.Stride * bData.Height)=" + bData.Stride + "*" + bData.Height + "= " + (bData.Stride * bData.Height) + " Should equal size of blob");
    }

}

public static CloudBlob ConvertImageFileFormat(CloudBlob blob, int imageWidth, int imageHeight, TraceWriter log)
{
    // define an image (bitmap) with the right characteristics matching the input given in the HTTP request
    Size size = new Size(imageWidth, imageHeight);
    PixelFormat pxFormat = PixelFormat.Format24bppRgb; 
    Bitmap bitmap = new Bitmap(size.Width, size.Height, pxFormat);

    // read the blob length
    blob.FetchAttributes(); 
    long rawfileLength = imageWidth * imageHeight *2; // 180212 blob.Properties.Length;
    log.Info("ConvertImageFileFormat: rawfileLength=" + rawfileLength + " width=" + size.Width + " height=" + size.Height);
    
    // read the raw blob data into a byte array (imageData)
    byte[] rawData = new byte[rawfileLength*2];
    blob.DownloadToByteArray(rawData,0);
    log.Info("ConvertImageFileFormat: downloaded blob");

    // Convert format of picture (easier here than on Arduino)
    //DumpBinaryData(rawData, rawfileLength, log, "raw before any conversion"); // use only with very small images
    //SwapBytesPerLine(rawData, imageWidth, imageHeight, log);
    //ReverseBytesPerLine(rawData, imageWidth, imageHeight, log);

    ConvertYVUtoRGB(rawData, imageWidth, imageHeight, ref bitmap, log);
    //CopyArrayToBitmap(rawData, rawfileLength, ref bitmap, log);

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

    if (false)
    {
        log.Info("NOTE %%%%% DEBUG MODE ###");

        TestReverseBytesPerLine(log);

        return request.CreateResponse(HttpStatusCode.OK, "NOTE %%%%% AZURE WEBJOB IN DEBUG MODE");
    }
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

    if (false) {  // true=use the image raw.RAW as input instead of input from Arduino
        blobName = "raw.RAW"; //180212  TODO    data.BlobName;
        imageWidth = 288; 
        imageHeight = 50; 
    }

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

static void TestReverseBytesPerLine(TraceWriter log)
{
    /*  IN       OUT
        0  8     7  F
        1  9     6  E
        2  A     5  D
        3  B     4  C
        4  C     3  B
        5  D     2  A
        6  E     1  9
        7  F     0  8
    */

    byte[] tmpData = new byte[16];
    for (int i = 0; i < 16; i++) tmpData[i] = (byte) i;
    ReverseBytesPerLine(tmpData, 2, 4, log);
}

static void TestSwapBytesPerLine(TraceWriter log)
{
    /*  IN       OUT
        0  8     1  9
        1  9     0  8
        2  A     3  B
        3  B     2  A
        4  C     5  D
        5  D     4  C
        6  E     7  F
        7  F     6  E
    */

    byte[] tmpData = new byte[16];
    for (int i = 0; i < 16; i++) tmpData[i] = (byte) i;
    SwapBytesPerLine(tmpData, 4, 2, log);
}

