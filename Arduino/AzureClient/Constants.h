const char IOTHUB_HOSTNAME[] = "NNRiothub.azure-devices.net";
const char STORAGE_HOSTNAME[] = "nnriothubstorage.blob.core.windows.net";
// key on next line found via Azure: nnriothubstorage - Shared access signature - Generate SAS
//const char STORAGE_SAS[] = "?sv=2016-05-31&ss=bfqt&srt=sco&sp=rwdlacup&se=2017-10-28T22:04:25Z&st=2017-06-07T14:04:25Z&spr=https&sig=iBGNG49IuBvRrci8OR%2BycoYfaqgcZ7j%2FyfF87x%2BdK8s%3D";
//const char STORAGE_SAS[] = "?sv=2017-04-17&ss=bfqt&srt=sco&sp=rwdlacup&se=2018-11-04T21:38:22Z&st=2017-12-17T13:38:22Z&spr=https&sig=M8FwoaUDp4nDpBhfgUDcQGRUf97MGEGIy3e%2BNvpmJcA%3D";
const char STORAGE_SAS[] = "?sv=2017-11-09&ss=bfqt&srt=sco&sp=rwdlacup&se=2025-11-06T22:37:05Z&st=2018-11-05T14:37:05Z&spr=https&sig=3VzJ2WLeMSTc5slk%2FQa3%2FZQVt3PDXk6CEVvZrXH%2Fz%2BY%3D";
const char STORAGE_CONTAINER_NAME[] = "barcodes-d1-001"; // deviceimages";
const char WEBAPP_HOSTNAME[] = "nnriotwebapps.azurewebsites.net";
const char WEBAPP_FUNCTION_NAME[] = "HttpPOST-processing";
const char WEBAPP_FUNCTION_KEY[] = "JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA==";
const char WEBAPP_FUNCTION_URL[] = "https://nnriotWebApps.azurewebsites.net/api/HttpPOST-processing?code=JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA==";
const char WEBAPP_CMD_BARCODE_CREATED[] = "Barcode created";
const char FIXED_BLOB_NAME[] = "photo.RAW";  // for more advanced cameras like 2640: "photo.JPEG";
const char THIS_DEVICE_NAME[] = ""; // "ArduinoD1_001";
const char THIS_DEVICE_SAS[] = "elB/d4TY5poTH8PpWH88EbqB8FHaGWSVRQ+INnorYPc=";
const char STORAGE_ACCOUNT_CS[] = "DefaultEndpointsProtocol=https;AccountName=nnriothubstorage;AccountKey=Dwho3wxRlVaHbIgoQCuUSU0EjZlCunAdah3+JU7syboA4KCJoDjp+7KGI09rTRRRRSAre++FFR1WRbDFCfpc+g==;";
//const char FIXED_BLOB_PATH[] = "/nnriothubcontainer/ArduinoD1_001/";
// Get next value from the Azure Portal (https://portal.azure.com/#resource/subscriptions/e678a72c-f502-4d57-9066-b6ac1a8dda26/resourceGroups/nnr_iot_resource_group/providers/Microsoft.Storage/storageAccounts/nnriothubstorage/sas)

