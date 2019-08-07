using Accord.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Icris.MJpegStreamer.Server
{
    class Program
    {
        static List<Stream> mjpegstreams = new List<Stream>();

        static void Main(string[] args)
        {
            //Start camera frame handling
            Thread camThread = new Thread(() =>
            {
                //First webcam found on this device.
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                var device = new VideoCaptureDevice(videoDevices[0].MonikerString);
                device.NewFrame += Device_NewFrame;

                //device.DesiredAverageTimePerFrame = 10000;
                device.Start();
            });
            camThread.SetApartmentState(ApartmentState.STA);
            camThread.Start();


            //Start a http listener to listen for http requests on port 8039.
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:18039/");
            //System.Diagnostics.Process.Start("http://localhost:18039/");
            listener.Start();
            while (true)
            {
                Console.WriteLine("Point your browser to http://localhost:18039 and see your camerafeed appearing.");
                Console.WriteLine("Listening...");
                // Note: The GetContext method blocks while waiting for a request. 
                HttpListenerContext ctx = listener.GetContext();
                var handler = new Thread(c =>
                {
                    try
                    {
                        var context = (HttpListenerContext)c;
                        HttpListenerRequest request = context.Request;
                        HttpListenerResponse response = context.Response;
                        var responseString = "";
                        //Serve the main page
                        if (request.RawUrl == "/" || request.RawUrl == "/index.html")
                        {
                            responseString = File.ReadAllText("index.html");
                            response.ContentType = "text/html";
                            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                            // Get a response stream and write the response to it.
                            response.ContentLength64 = buffer.Length;
                            System.IO.Stream output = response.OutputStream;
                            output.Write(buffer, 0, buffer.Length);
                            // You must close the output stream.
                            output.Close();
                        }
                        else if (request.RawUrl.Contains("mjpeg"))
                        {
                            mjpegstreams.Add(response.OutputStream);
                            return;
                        }
                        else
                        {
                            Console.WriteLine($"Unknown request: {request.RawUrl}");
                            response.StatusCode = (int)HttpStatusCode.NotFound;
                            response.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        //System.Diagnostics.Debugger.Break();
                    }

                });
                handler.Start(ctx);

            }
            listener.Stop();
        }

        private static void Device_NewFrame(object sender, Accord.Video.NewFrameEventArgs eventArgs)
        {
            var bitmap = (Bitmap)eventArgs.Frame.Clone();
            MemoryStream buffert = new MemoryStream();
            bitmap.Save(buffert, System.Drawing.Imaging.ImageFormat.Jpeg);
            byte[] boundary = new ASCIIEncoding().GetBytes("\r\n--myboundary\r\nContent-Type: image/jpeg\r\nContent-Length:" + buffert.Length + "\r\n\r\n");
            var bitmapdata = buffert.ToArray();            
            
            //Console.WriteLine(Convert.ToBase64String(bitmapdata));
            foreach (var mjpegstream in mjpegstreams)
            {
                try
                {
                    mjpegstream.Write(boundary, 0, boundary.Length);
                    mjpegstream.Write(bitmapdata, 0, bitmapdata.Length);
                    mjpegstream.Flush();
                }
                catch (Exception e)
                {
                    //System.Diagnostics.Debugger.Break();
                }
            }
        }
    }
}
