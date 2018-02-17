const char IOTHUB_HOSTNAME[] = "NNRiothub.azure-devices.net";
const char STORAGE_HOSTNAME[] = "nnriothubstorage.blob.core.windows.net";
const char STORAGE_SAS[] = "?sv=2016-05-31&ss=bfqt&srt=sco&sp=rwdlacup&se=2017-10-28T22:04:25Z&st=2017-06-07T14:04:25Z&spr=https&sig=iBGNG49IuBvRrci8OR%2BycoYfaqgcZ7j%2FyfF87x%2BdK8s%3D";
const char STORAGE_CONTAINER_NAME[] = "deviceimages";
const char WEBAPP_HOSTNAME[] = "nnriotwebapps.azurewebsites.net";
const char WEBAPP_FUNCTION_NAME[] = "HttpPOST-processing";
const char WEBAPP_FUNCTION_KEY[] = "JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA==";
const char WEBAPP_FUNCTION_URL[] = "https://nnriotWebApps.azurewebsites.net/api/HttpPOST-processing?code=JYQFydLmQ0hzKFbinoAFesa42n5zdT2JCwKvGBpW6fv4EjMIm4IhfA==";
const char WEBAPP_CMD_BARCODE_CREATED[] = "Barcode created";
const char FIXED_BLOB_NAME[] = "photo.JPEG";
const char THIS_DEVICE_NAME[] = "ArduinoD1_001";
const char THIS_DEVICE_SAS[] = "elB/d4TY5poTH8PpWH88EbqB8FHaGWSVRQ+INnorYPc=";
//const char FIXED_BLOB_PATH[] = "/nnriothubcontainer/ArduinoD1_001/";
// Get next value from the Azure Portal (https://portal.azure.com/#resource/subscriptions/e678a72c-f502-4d57-9066-b6ac1a8dda26/resourceGroups/nnr_iot_resource_group/providers/Microsoft.Storage/storageAccounts/nnriothubstorage/sas)
