// This application reads the serial port until the string "*RDY*" is present
// After that, it reads all the bytes 320*240 of a new image sent by Arduino (sketch and instructions can be found in
//   http://www.instructables.com/id/OV7670-Without-FIFO-Very-Simple-Framecapture-With-/?ALLSTEPS)
// 
// By: cesarab

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;


namespace ReadSerialPortWin
{
    public partial class frmPrinc : Form
    {
        private const int WIDTH = 320; //320; //30; //160;
        private const int HEIGHT = 240; //240; //120;
        private int width = 0;
        private int height = 0;
        private int pixelSize = 0; //bytes per pixel
        private int  totalImageSize = 0;
        private int _readingImageSession = 0;
        private bool _readingImage = false;
        private Int32 xx = 0;
        private Int32 yy = 0;
        private Int32 byteCount = 0;

        private SerialPort _mySerialPort { get; set; }

        private enum COLORSPACE { YUV422, RGB565, BAYER_RGB};
        private COLORSPACE ColorSpace = COLORSPACE.YUV422;
        private Size _bitmapSize = new Size(WIDTH, HEIGHT);
        private Bitmap _bitmap;
        public Bitmap MyBitmap
        {
            get
            {
                if (_bitmap == null)
                {
                    _bitmap = new Bitmap(_bitmapSize.Width, _bitmapSize.Height, PixelFormat.Format24bppRgb); //.Format16bppRgb565); // OV7670 raw output format
                }
                return _bitmap;
            }
        }


        public static bool ReadingImage { get; set; }

        public frmPrinc()
        {
            InitializeComponent();
        }

        private bool CheckSerialPorts()
        {
            var names = SerialPort.GetPortNames();
            if (names.Length > 0)
            {
                cboSerialPorts.Items.Clear();
                foreach (var name in names)
                {
                    cboSerialPorts.Items.Add(name);
                }
                cboSerialPorts.SelectedItem = names[0];
                return true;
            }

            return false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!CheckSerialPorts())
            {
                btnStart.Enabled = false;
                lblStatus.Text = "Nothing connected to serial ports";
            }
          
            picImage.Image = MyBitmap;
        }

        private void LblStatus(string text)
        {
            lblStatus.Invoke((MethodInvoker)(() =>
            {
                lblStatus.Text = text;
            }));
        }

        private void PrintBuffer(byte[] buffer, int bytesRead) {
            string str = "";
            for (int i = 0; i < bytesRead; i++)
            {
                //str = str + " [" + i.ToString() + "]=" + buffer[i].ToString();
                str = str + " " + buffer[i].ToString();
                if (i % 16 == 15) { str = str + '\n'; }
            }
            //Trace.WriteLine("Session:" + _readingImageSession.ToString() + "  Buffer read:" + str);
        }

        private byte clamp(double  x, byte x_min, byte x_max)
        {
            if (x < x_min)  { return x_min; }
            if (x > x_max)  { return x_max; }
            return (byte)x;
        }

        private void yvu422_RGB(byte[] yvu, int i, ref Color pix1, ref Color pix2)
        {
            i--;
            byte Y1 = yvu[++i];  //best
            byte Cr = yvu[++i];
            byte Y0 = yvu[++i];  
            byte Cb = yvu[++i];
            double R0 = Y0 + 1.4075 * (Cb - 128);
            double G0 = Y0 - (0.3455 * (Cr - 128)) - (0.7169 * (Cb - 128));
            double B0 = Y0 + (1.7790 * (Cr - 128));
            double R1 = Y1 +                         1.4075 * (Cb - 128);
            double G1 = Y1 - (0.3455 * (Cr - 128)) - (0.7169 * (Cb - 128));
            double B1 = Y1 + (1.7790 * (Cr - 128));

            pix1 = Color.FromArgb(1,  clamp(R0,0,255), clamp(G0,0,255), clamp(B0,0,255));
            pix2 = Color.FromArgb(1,  clamp(R1,0,255), clamp(G1,0,255), clamp(B1,0,255));


            /*Trace.WriteLine("Sess:" + _readingImageSession.ToString() +
                " i=" + i.ToString() + "  yy=" + yy.ToString() +
                " Y0=" + Y0.ToString() + "  Cb=" + Cb.ToString() +
                " Y1=" + Y1.ToString() + "  Cr=" + Cr.ToString() +
                " ---   R=" + R.ToString() + " G=" + G.ToString() +" B=" + B.ToString() );*/
            /*MyBitmap.SetPixel(2 * i, yy, Color.FromArgb(1, Convert.ToByte(R), Convert.ToByte(G), Convert.ToByte(B)));

            R = clamp( Y1 +                         (1.4075 * (Cr - 128)), 0, 255);
            G = clamp( Y1 - (0.3455 * (Cb - 128)) - (0.7169 * (Cr - 128)), 0, 255);
            B = clamp( Y1 + (1.7790 * (Cb - 128)), 0, 255);

            MyBitmap.SetPixel(2 * i + 1, yy, Color.FromArgb(1, Convert.ToByte(R), Convert.ToByte(G), Convert.ToByte(B)));
            */
        }

        private Boolean ReadHeaderFromSerial(SerialPort sp, ref int width, ref int height, ref int pixelsize, ref int totalImageSize)
        {
            // look for header info
            int bytesRead = 0;
            int bytesInBuffer = 0;
            var header_buffer = new byte[100];
            // look for "###" probably indicating first byte in header
            while (sp.ReadChar() != '#') ;
            if ((sp.ReadChar() == '#') && (sp.ReadChar() == '#'))
            {
                if (sp.BytesToRead > 0)
                {
                    bytesInBuffer = sp.BytesToRead;
                    bytesRead = sp.Read(header_buffer, 0, 15);  //15: *RDY* (5bytes) + width (2b) + height (2b) + pixelSize (2b) + totalBytes (4b)
                    PrintBuffer(header_buffer, bytesRead);
                }
                //LblStatus($"bytesRead: bytes={buffer.Length}: {buffer[0]}-{buffer[1]}-{buffer[2]}-{buffer[3]}-{buffer[4]}");

                if (bytesRead > 4 && header_buffer[0] == '*' && header_buffer[1] == 'R' && header_buffer[2] == 'D' && header_buffer[3] == 'Y' && header_buffer[4] == '*')  // Look for *RDY* to sync image start
                {
                    _readingImage = true;
                    Trace.WriteLine("Setting ReadingImage. Session=" + _readingImageSession.ToString() + "  ReadingImage=" + _readingImage.ToString());
                    // reader width, height and size information:
                    width = (int)((byte)(header_buffer[6]) << 8 | (byte)(header_buffer[5]));
                    height = (int)((byte)(header_buffer[8]) << 8 | (byte)(header_buffer[7]));
                    pixelSize = (int)((byte)(header_buffer[10]) << 8 | (byte)(header_buffer[9]));
                    totalImageSize = (int)(
                        (byte)(header_buffer[14]) << 24 | (byte)(header_buffer[13]) << 16 |
                        (byte)(header_buffer[12]) << 8 | (byte)(header_buffer[11]));
                    if ((width != WIDTH) || (height != HEIGHT))
                    {
                        String s = "";
                        for (int i = 0; i < 13; i++)
                        {
                            s = s + " " + i.ToString() + ":" + header_buffer[i].ToString();
                        }
                        Trace.WriteLine("*** DUMP: " + s);
                        Trace.WriteLine("*** WARNING: Height or width inconsistent. \nReceived width=" + width.ToString() + " WIDTH=" + WIDTH.ToString() + " received height=" + height.ToString() + " HEIGHT=" + HEIGHT.ToString() );
                    }
                    bytesRead = 0;
                    header_buffer[0] = 0;
                    byteCount = 0;

                    Trace.WriteLine("*** *RDY* found. Session:" + _readingImageSession.ToString() + " data width:" + width.ToString() + "  data height:" + height.ToString() + "  size:" + totalImageSize.ToString());
                    //LblStatus("Found *RDY*");
                    LblStatus($"Reading image from serial port {cboSerialPorts.SelectedText}...");
                    return true;
                }
            }
            return false;

        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesInBuffer = 0;
            int bytesRead = 0;
            byte[] buffer = new byte[2*WIDTH*HEIGHT];  //2* to accomodate for yvu422 or rgb565 coding (two bytes per pixel)
            byte[] imgBuf = new byte[2*WIDTH*HEIGHT];

            byte R; byte G1; byte G2; byte G; byte B; 
            Color pix1 = Color.Black; Color pix2 = Color.Black;
            Int32 idx = 0;

           _readingImageSession += 1;
            //Trace.WriteLine("START Reading image. Session=" + _readingImageSession.ToString() + "  _readingImage=" + _readingImage.ToString());
            //LblStatus("START");
            try
            {
                //var sw = new Stopwatch();
                //sw.Start();

                var sp = (SerialPort)sender;
                if (!sp.IsOpen)
                {
                    Trace.WriteLine("*** Port already open.  Session:" + _readingImageSession.ToString());
                    return;
                }

                // read the header
                while (!ReadHeaderFromSerial(sp, ref width, ref height, ref pixelSize, ref totalImageSize)) ;
                idx = 0;

                // (continue to) read the image data
                while (idx < totalImageSize)
                {
                    bytesInBuffer = sp.BytesToRead;
                    if (bytesInBuffer > 0)
                    {
                        if (bytesInBuffer > (totalImageSize - idx))
                        {
                            bytesInBuffer = (totalImageSize - idx);
                        }
                        bytesRead = sp.Read(buffer, 0, bytesInBuffer);  // read as many bytes as possible up to max line lenght
                                                                        // copy to right place in imgBuf
                        Buffer.BlockCopy(buffer, 0, imgBuf, idx, bytesRead);
                        idx += bytesRead;
                        //PrintBuffer(buffer, bytesRead);
                        //PrintBuffer(imgBuf, idx);
                    }
                }
                Trace.WriteLine("idx=" + idx.ToString());

                // process the data

                int pixHeight = pixelSize * height; 
                int i = 0;
                int j = 0;
                byte[] rawData = new byte[totalImageSize];
                Buffer.BlockCopy(imgBuf, 0, rawData, 0, totalImageSize);  // then we can keep the result in imgBuf
                switch (ColorSpace)
                {
                    case COLORSPACE.YUV422:

                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x += 2)
                            {
                                i = (y * width * pixelSize) + x * 2;
                                yvu422_RGB(imgBuf, i, ref pix1, ref pix2);
                                MyBitmap.SetPixel( x+1, height - 1 - y, pix1);
                                MyBitmap.SetPixel( x  , height - 1 - y, pix2);
                            }
                        }
                        break;

                    case COLORSPACE.RGB565:
                        /*for (int h = 0; h < height; h++)
                        {
                            int offset = h * pixWidth;
                            for (int w = 0; w < pixWidth; w++)
                            {
                                i = offset + w;
                                j = offset + (pixWidth - 1) - w;
                                if (i < 0 || i >= totalImageSize || j < 0 || j >= totalImageSize)
                                {
                                    Trace.WriteLine("h=" + h + " offset=" + offset + " pixw=" + pixWidth + " i=" + i + " j=" + j);
                                }
                                else
                                {
                                    //Trace.WriteLine("i=" + i.ToString() + " imgBuf[i]=" + imgBuf[i].ToString() + "   j=" + j.ToString() + " rawData[j]=" + rawData[j].ToString()); 
                                    imgBuf[i] = rawData[j];
                                }
                            }
                        }
                        Trace.WriteLine("ConvertImageFileFormat: mirroring done");
                        */
                        i = 0;
                        for (int x = 0; x < width; x++)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                i = (x * height * pixelSize) + y;
                                // R R R R R G G G   G G G B B B B B
                                B = (byte)(imgBuf[i] & 0b11111000);
                                G1 = (byte)(((byte)(imgBuf[i] & 0b00000111)) << 3);
                                G2 = (byte)(((byte)(imgBuf[i+1] & 0b11100000)) >> 5);
                                G = (byte)(G1 & G2);
                                R = (byte)(((byte)(imgBuf[i+1] & 0b00011111)) << 3);
                                MyBitmap.SetPixel(x,y, Color.FromArgb(R, G, B));
                                //MyBitmap.SetPixel(height - 1 - y, width - x - 1, Color.FromArgb(R, G, B));
                            }
                        }
                        break;
                    default:
                        break;
                }

                // display the image
                using (var memoryStream = new MemoryStream())
                {
                    MyBitmap.Save(memoryStream, ImageFormat.Bmp);
                    picImage.Invoke((MethodInvoker)(() =>
                    {
                        picImage.Image = Image.FromStream(memoryStream);
                    }));
                }
                Application.DoEvents();  //allow GUI to catch up
            }

/*            catch (Exception ex)
            {
                LblStatus($"Error: {ex.Message}");
            }
*/            finally
            {
                Trace.WriteLine("***FINISH: Image was read");
                Thread.Sleep(500);
                Application.DoEvents();
                if (!btnStop.Enabled)
                    CloseSerialPort();
                Trace.WriteLine("Releasing readingImage. Session=" + _readingImageSession.ToString() + "  ReadingImage=" + _readingImage.ToString());
                
            }
            
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseSerialPort();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            _mySerialPort = new SerialPort(cboSerialPorts.SelectedItem.ToString())
            //_mySerialPort = new SerialPort("COM1")
            {
                BaudRate = 921600, //115200  ,
                //BaudRate = 250000,
                Parity = Parity.None,
                StopBits = StopBits.One,
                DataBits = 8,
                Handshake = Handshake.None,
                ReadTimeout = 3000  /*,
                DtrEnable = true,
                RtsEnable = true*/

            };
            
            _mySerialPort.Open();
            _mySerialPort.DataReceived += DataReceivedHandler;
            lblStatus.Text = $"Opening {cboSerialPorts.SelectedItem} port...";
            //_mySerialPort.Open();
            lblStatus.Text = $"{cboSerialPorts.SelectedItem} port opened";
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            btnStop.Focus();
        }

        private void CloseSerialPort()
        {
            if (_mySerialPort != null && _mySerialPort.IsOpen)
                _mySerialPort.Close();
            lblStatus.Text = $"{cboSerialPorts.SelectedItem} port closed";
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnStart.Focus();
            if (!ReadingImage) CloseSerialPort();
        }

        private void btnCheckPorts_Click(object sender, EventArgs e)
        {
            if (CheckSerialPorts())
            {
                btnStart.Enabled = true;
            }
            else
            {
                btnStart.Enabled = false;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (dlgSaveFile.ShowDialog(this) == DialogResult.OK)
            {
                MyBitmap.Save(dlgSaveFile.FileName, ImageFormat.Bmp);
            }
        }

        private void rgb565swapped(ref byte[] imgBuf, int width, int height, int pixelsize, int totalImageSize)
        {
            byte[] rawData = new byte[totalImageSize];
            Buffer.BlockCopy(imgBuf, 0, rawData, 0, totalImageSize);  // then we can keep the result in imgBuf

            // Mirror the picture line by line
            long pixWidth = pixelsize * width; 
            long i = 0;
            long j = 0;
            for (long h=0; h<height;h++)
            {
                long offset = h * pixWidth;
                for (long w=0; w<pixWidth;w++)
                {
                    i = offset + w;
                    j = offset + (pixWidth - 1) - w;
                    if (i < 0 || i >= totalImageSize || j < 0 || j >= totalImageSize)
                    {
                        Trace.WriteLine("h=" + h + " offset=" + offset + " pixw=" + pixWidth + " i=" + i + " j=" + j); 
                    } else
                    {
                        imgBuf[i] = rawData[j];
                    }
                }
            }
            Trace.WriteLine("ConvertImageFileFormat: mirroring done");

            // Convert the raw data to bitmap format
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData bData = MyBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, MyBitmap.PixelFormat);

            IntPtr ptr = bData.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(imgBuf, 0, ptr, totalImageSize);
//            byte[] pt = System.Runtime.InteropServices.Marshal.st  PtrToStructure(ptr , typeof( bData.Scan0 ));
//            Buffer.BlockCopy(imgBuf,0, pt, 0, totalImageSize);

            MyBitmap.UnlockBits(bData);

        }



        /*
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
                    //for (int i=0; i<8;  i++)    { log.Info("ConvertImageFileFormat: rawData=" + rawData[i]);   }

                    // Modify original picture (easier here than on Arduino)
                    // Mirror the picture line by line
                    long pixWidth = 2 * size.Width; // 2 bytes per pixel in RGB565
                    long i = 0;
                    long j = 0;
                    for (long h=0; h<size.Height;h++)
                    {
                        long offset = h * pixWidth;
                        for (long w=0; w<pixWidth;w++)
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
                    log.Info("ConvertImageFileFormat: mirroring done");

                    // copy the raw data into the bitmapdata
                    PixelFormat pxFormat = PixelFormat.Format16bppRgb565;  // OV7670 raw output format
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

                    // Dump imagedata
                /*    IntPtr ptr = bData.Scan0;
                    int bytes  = Math.Abs(bData.Stride) * bitmap.Height;
                    byte[] rgbValues = new byte[bytes];
                    // Copy the RGB values into the array (only used for printout).
                    System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);
                    for (int i=0; i<5;  i++)    { log.Info("ConvertImageFileFormat: rgbValues=" + rgbValues[i]);   }
                */
        // Encode to JPEG
        //create an encoder parameter for the image quality
        /*           EncoderParameter qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
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

                   //dump memStream
                   //   outStream.Position = 0;
                   //   for (int i=0; i<5;  i++)    { log.Info("ConvertImageFileFormat: DUMP=" + outStream.ReadByte());   }

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

           */

        private void DataReceivedHandlerORG(object sender, SerialDataReceivedEventArgs e)
        {
            //byte R; byte G; byte B; //byte Cb; byte Y0; byte Cr; byte Y1;
            //UInt16 Ri; UInt16 Gi1; UInt16 Gi2; UInt16 Gi; UInt16 Bi; 

            int bytesInBuffer = 0;
            int bytesRead = 0;
            var header_buffer = new byte[100];
            var buffer = new byte[2*WIDTH];  //2* to accomodate for yvu422 or rgb565 coding (two bytes per pixel)
            Color pix1 = Color.Black; Color pix2 = Color.Black;

           _readingImageSession += 1;
            //Trace.WriteLine("START Reading image. Session=" + _readingImageSession.ToString() + "  _readingImage=" + _readingImage.ToString());
            //LblStatus("START");
            try
            {
                //var sw = new Stopwatch();
                //sw.Start();

                var sp = (SerialPort)sender;
                if (!sp.IsOpen) {
                    Trace.WriteLine("*** Port already open.  Session:" + _readingImageSession.ToString());
                    return;
                }

                if (!_readingImage) {
                    // look for header info
                    bytesRead = 0;
                    // look for "###" probably indicating first byte in header
                    while (sp.ReadChar() != '#') ;
                    if ((sp.ReadChar() == '#') && (sp.ReadChar() == '#'))
                    {
                        if (sp.BytesToRead > 0)
                        {
                            bytesInBuffer = sp.BytesToRead;
                            bytesRead = sp.Read(header_buffer, 0, 15);  //15: *RDY* (5bytes) + width (2b) + height (2b) + pixelSize (2b) + totalBytes (4b)
                            PrintBuffer(header_buffer, bytesRead);
                        }
                        //LblStatus($"bytesRead: bytes={buffer.Length}: {buffer[0]}-{buffer[1]}-{buffer[2]}-{buffer[3]}-{buffer[4]}");

                        if (bytesRead > 4 && header_buffer[0] == '*' && header_buffer[1] == 'R' && header_buffer[2] == 'D' && header_buffer[3] == 'Y' && header_buffer[4] == '*')  // Look for *RDY* to sync image start
                        {
                            _readingImage = true;
                            Trace.WriteLine("Setting ReadingImage. Session=" + _readingImageSession.ToString() + "  ReadingImage=" + _readingImage.ToString());
                            // reader width, height and size information:
                            width = (int)((byte)(header_buffer[6]) << 8 | (byte)(header_buffer[5]));
                            height = (int)((byte)(header_buffer[8]) << 8 | (byte)(header_buffer[7]));
                            pixelSize = (int)((byte)(header_buffer[10]) << 8 | (byte)(header_buffer[9]));
                            totalImageSize = (int)(
                                (byte)(header_buffer[14]) << 24 | (byte)(header_buffer[13]) << 16 |
                                (byte)(header_buffer[12]) << 8 | (byte)(header_buffer[11]));
                            if ((width != WIDTH) || (height != HEIGHT))
                            {
                                String s = "";
                                for (int i = 0; i < 13; i++)
                                {
                                    s = s + " " + i.ToString() + ":" + header_buffer[i].ToString();
                                }
                                Trace.WriteLine("*** DUMP: " + s);
                                Trace.WriteLine("*** ERROR: Height or width inconsistent. height=" + height.ToString() + " HEIGHT=" + HEIGHT.ToString() + " width=" + width.ToString() + " WIDTH=" + WIDTH.ToString());
                            }
                            bytesRead = 0;
                            header_buffer[0] = 0;
                            byteCount = 0;
                            Trace.WriteLine("*** *RDY* found. Session:" + _readingImageSession.ToString() + " width:" + width.ToString() + "  height:" + height.ToString() + "  size:" + totalImageSize.ToString());
                            //LblStatus("Found *RDY*");
                            LblStatus($"Reading image from serial port {cboSerialPorts.SelectedText}...");
                        }
                    }
                } else
                {
                    // (continue to) read the image data
                    //Application.DoEvents();  //allow GUI to catch up
                    if (!sp.IsOpen)
                        return;
                    while (yy < height)
                    {
                        //Application.DoEvents();  //allow GUI to catch up
                        while (xx < width)
                        {
                            if (sp.BytesToRead > 0)
                            {
                                int maxBytesToRead = (width - xx);
                                bytesInBuffer = sp.BytesToRead;
                                bytesRead = sp.Read(buffer, 0, maxBytesToRead);  // read as many bytes as possible up to max line lenght
                                xx += bytesRead;
                                //PrintBuffer(buffer, bytesRead);
                                //Trace.WriteLine("read:" +  " xx=" + xx.ToString() + "  yy=" + yy.ToString() + "  Received bytes=" + bytesRead.ToString());
                            }
                        }
                        if (xx == width )  // if end of line, reset counters to read next line. This is needed to ensure re-entrance 
                        {
                            //PrintBuffer(buffer, bytesRead);
                            //Trace.WriteLine("*** BEFORE MyBitmap.SetPixel:" +  " i=" + i.ToString() + "  xx=" + xx.ToString() + "  yy=" + yy.ToString() + "  Received bytes=" + bytesRead.ToString());

                            //MyBitmap.SetPixel(i, yy, Color.FromArgb(1, buffer[i], buffer[i], buffer[i]));

                            int i;
                            Color c;
                            switch (ColorSpace)
                            {
                                case COLORSPACE.YUV422:
                                    for (i = 0; i < width/4; i += 2)  // 4 bytes pr 2 pixels
                                    {
                                        yvu422_RGB(buffer, i*4, ref pix1, ref pix2);
                                        MyBitmap.SetPixel(i, yy, pix1);
                                        MyBitmap.SetPixel(i + 1, yy, pix2);
                                        if (i == 80-1)
                                        { 
                                            /*Trace.WriteLine("Sess:" + _readingImageSession.ToString() +
                                            " i=" + i.ToString() + "  yy=" + yy.ToString() +
                                            " Y0=" + Y0.ToString() + "  Cb=" + Cb.ToString() +
                                            " Y1=" + Y1.ToString() + "  Cr=" + Cr.ToString() +
                                            " ---   R=" + R.ToString() + " G=" + G.ToString() + " B=" + B.ToString());*/
                                        }
                                    }
                                    break;
                                case COLORSPACE.RGB565:

                                    //HERTIL: lav det her om til et kald i main_yvu og brug algoritmen nedenfor (er taget fra Azure funktionen)

                                    for (i = 0; i < width; i += 1)  // 2 bytes pr 1 pixels
                                    {
                                        /*buffer[i*2] = 0;
                                        buffer[i*2+1] = 128;
                                        /*R = (byte) ( (int)(buffer[2*i]) >> 3);
                                        G = (byte) (((buffer[2*i] & 0x07)<<3) + (buffer[2*i+1] >> 5));
                                        B = (byte) ((int) (buffer[2*i+1] & 0x1F));
                                        */

                                        // R R R R R G G G   G G G B B B B B
                                        /*Ri =  (byte) ((UInt16) (buffer[2*i]) >> 3);
                                               //Ri =  (byte) ((UInt16) 8 * Ri );
                                        Gi1 = (byte) ((UInt16) (buffer[2*i]) & 0b00000111);
                                        Gi1 = (byte)((UInt16)8 * Gi1);
                                        Gi2 = (byte) ((UInt16)    (buffer[2*i+1]) >> 5);
                                        Gi =  (byte) (Gi1 + Gi2);
                                        Bi =  (byte) ((UInt16) (buffer[2*i+1]) & 0b00011111);
                                        R = (byte) Ri;
                                        G = (byte) Gi;
                                        B = (byte) Bi;


                                        /*R = (byte) (( buffer[2*i+1] >> 3)<<3);
                                        G = (byte) (((buffer[2*i+1] & 0x07)<<3) + (buffer[2*i] >> 5));
                                        B = (byte) ( buffer[2*i] & 0x1F);
                                        

                                        R = buffer[i*2];
                                        G = buffer[i*2];
                                        B = buffer[i*2];
                                        

                                        // generate test image:
                                        /*if (i < width)
                                        {
                                            R = (byte)((8 * i + 8 * yy) % 255);
                                            G = (byte)((4 * i + 3 * yy) % 255);
                                            B = (byte)((2 * i + 6 * yy) % 255);
                                        }*/

                                        //rgb565swapped(imgBuf, 2*i, ref pix1);  // 2 bytes per pixel
                                        MyBitmap.SetPixel(i, yy, pix1);
                                        //c = Color.FromArgb(R, G, B);
                                        //c = Color.FromArgb(R, G, B);
                                        //MyBitmap.SetPixel(i, yy, c);
                                        //Trace.WriteLine("*** i=" + i.ToString() + " yy=" + yy.ToString() + " c=" + c.Name + " RGB=" + R.ToString() + " " + G.ToString() + " " + B.ToString());
                                    }
                                    break;
                            }
                            yy += 1;
                            //Trace.WriteLine("*** Line read. Session:" + _readingImageSession.ToString() + " xx=" + xx.ToString() + "  yy=" + yy.ToString() + "  bytesRead=" + bytesRead.ToString());
                            xx = 0;
                        }
                    }
                    if ((yy==height) && (xx == 0) && _readingImage)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            MyBitmap.Save(memoryStream, ImageFormat.Bmp);

                            picImage.Invoke((MethodInvoker)(() =>
                            {
                                picImage.Image = Image.FromStream(memoryStream);
                            }));
                        }
                        Trace.WriteLine("*** FINISH Session:" + _readingImageSession.ToString() + " xx=" + xx.ToString() + "  yy=" + yy.ToString() + "  Received bytes=" + byteCount.ToString());
                        Application.DoEvents();  //allow GUI to catch up
                        _readingImage = false;
                        yy = 0;
                    }
                }    
            }
            catch (Exception ex)
            {
                LblStatus($"Error: {ex.Message}");
            }
            finally
            {
                if ( (byteCount >= totalImageSize) && (byteCount > 0)) {
                    //sw.Stop();
                    //LblStatus($"Image was read. Time taken: {sw.ElapsedMilliseconds} ms");
                    Trace.WriteLine("***FINISH: Image was read");
                    Thread.Sleep(500);
                    Application.DoEvents();
                    if (!btnStop.Enabled)
                        CloseSerialPort();
                    //Trace.WriteLine("Session:" + _readingImageSession.ToString() + " *** Clear serial buffer");
                    //sp.ReadExisting();
                    _readingImage = false;
                    Trace.WriteLine("Releasing readingImage. Session=" + _readingImageSession.ToString() + "  ReadingImage=" + _readingImage.ToString());
                }
            }
        }

        private void DataReceivedHandlerSIMPLE(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesInBuffer = 0;
            int bytesRead = 0;
            var header_buffer = new byte[100];
            var buffer = new byte[2*WIDTH];  //2* to accomodate for yvu422 or rgb565 coding (two bytes per pixel)
            Color pix1 = Color.Black; Color pix2 = Color.Black;


           _readingImageSession += 1;
            //Trace.WriteLine("START Reading image. Session=" + _readingImageSession.ToString() + "  _readingImage=" + _readingImage.ToString());
            //LblStatus("START");
            try
            {
                //var sw = new Stopwatch();
                //sw.Start();

                var sp = (SerialPort)sender;
                if (!sp.IsOpen) {
                    Trace.WriteLine("*** Port already open.  Session:" + _readingImageSession.ToString());
                    return;
                }

                while (true) {

                    // look for header info
                    bytesRead = 0;
                    if (sp.BytesToRead > 0)
                    {
                        bytesInBuffer = sp.BytesToRead;
                        bytesRead = sp.Read(header_buffer, 0, 99);   
                        PrintBuffer(header_buffer, bytesRead);
                    }
                }    
            }
            catch (Exception ex)
            {
                LblStatus($"Error: {ex.Message}");
            }
            finally
            {
                if (byteCount >= totalImageSize) {
                    //sw.Stop();
                    //LblStatus($"Image was read. Time taken: {sw.ElapsedMilliseconds} ms");
                    Trace.WriteLine("***FINISH: Image was read");
                    Thread.Sleep(500);
                    Application.DoEvents();
                    if (!btnStop.Enabled)
                        CloseSerialPort();
                    //Trace.WriteLine("Session:" + _readingImageSession.ToString() + " *** Clear serial buffer");
                    //sp.ReadExisting();
                    _readingImage = false;
                    Trace.WriteLine("Releasing readingImage. Session=" + _readingImageSession.ToString() + "  ReadingImage=" + _readingImage.ToString());
                }
            }
        }


    }
}
