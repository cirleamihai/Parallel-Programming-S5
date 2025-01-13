using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Lab_4
{
    class EventDrivenDownloader
    {
        public static async Task GetAsync(string host, string path, string locationFileName)
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                Console.WriteLine("Connecting to the server...");

                try
                {
                    IPHostEntry hostEntry = await Dns.GetHostEntryAsync(host);
                    IPAddress ipAddress = hostEntry.AddressList[1];  // Prefer IPv4 address
                    IPEndPoint endPoint = new IPEndPoint(ipAddress, 80);

                    // Connect asynchronously using a wrapped Task
                    await ConnectAsync(socket, endPoint);
                    Console.WriteLine("Connected! Sending HTTP request...");

                    string request = $"GET {path} HTTP/1.1\r\n" +
                                     $"Host: {host}\r\n" +
                                     "User-Agent: SimpleSocketClient/1.0\r\n" +
                                     "Accept: */*\r\n" +
                                     "Connection: close\r\n\r\n";

                    byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                    await SendAsync(socket, requestBytes); // Send request

                    Console.WriteLine("Receiving HTTP response...");
                    byte[] buffer = new byte[1024];
                    StringBuilder httpResponse = new StringBuilder();

                    int bytesRead;
                    while ((bytesRead = await ReceiveAsync(socket, buffer)) > 0)
                    {
                        httpResponse.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                    }

                    int contentLength = ParseContentLength(httpResponse.ToString());
                    DownloadHttp(locationFileName, httpResponse.ToString(), contentLength);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}\n {ex.StackTrace}");
                }
            }
        }

        public static int ParseContentLength(string httpResponse)
        {
            try
            {
                string contentLengthHeader = "Content-Length: ";
                string contentLength = httpResponse.Split(contentLengthHeader)[1].Split("\r\n")[0];
                return int.Parse(contentLength);
            }
            catch (Exception)
            {
                throw new Exception("Content-Length header not found in HTTP response");
            }
        }

        public static void DownloadHttp(string fileName, string httpResponse, int contentLength)
        {
            Console.WriteLine($"Saving the HTTP response to {fileName}...");
            using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
            {
                string bodyResponse = httpResponse.Split("\r\n\r\n")[1];
                byte[] bodyBytes = Encoding.ASCII.GetBytes(bodyResponse);

                if (bodyBytes.Length != contentLength)
                {
                    throw new Exception("Content-Length header does not match the body length");
                }

                fileStream.Write(bodyBytes, 0, bodyBytes.Length);
            }
            Console.WriteLine("Download complete!");
        }

        static async Task Main(string[] args)
        {
            Task task1 = GetAsync("www.example.com", "/", "example1.html");
            Task task2 = GetAsync("www.example.com", "/", "example2.html");

            await Task.WhenAll(task1, task2);

            Console.WriteLine("All downloads completed! Press any key to exit...");
            Console.ReadLine();
        }

        // APM to TAP wrapper for Socket.Connect
        private static Task ConnectAsync(Socket socket, EndPoint endPoint)
        {
            var tcs = new TaskCompletionSource<bool>();
            socket.BeginConnect(endPoint, ar =>
            {
                try
                {
                    socket.EndConnect(ar); // End connection
                    tcs.SetResult(true);   // Set task as completed successfully
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);  // Propagate the exception to the task
                }
            }, null);
            return tcs.Task; // Return the task
        }

        // APM to TAP wrapper for Socket.Send
        private static Task SendAsync(Socket socket, byte[] buffer)
        {
            var tcs = new TaskCompletionSource<int>();
            socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, ar =>
            {
                try
                {
                    int bytesSent = socket.EndSend(ar); // End send
                    tcs.SetResult(bytesSent);           // Set task result with bytes sent
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);  // Propagate the exception
                }
            }, null);
            return tcs.Task; // Return the task
        }

        // APM to TAP wrapper for Socket.Receive
        private static Task<int> ReceiveAsync(Socket socket, byte[] buffer)
        {
            var tcs = new TaskCompletionSource<int>();
            socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ar =>
            {
                try
                {
                    int bytesReceived = socket.EndReceive(ar); // End receive
                    tcs.SetResult(bytesReceived);             // Set task result with bytes received
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);  // Propagate the exception
                }
            }, null);
            return tcs.Task; // Return the task
        }
    }
}