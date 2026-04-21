using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Speech.Synthesis;
using System.Diagnostics; 

public class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "Server";
        var ipAddress = IPAddress.Parse("192.168.1.72");
        var port = 27001;

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var ep = new IPEndPoint(ipAddress, port);

        try
        {
            socket.Bind(ep);
            socket.Listen(20);
            Console.WriteLine($"Server is listening on {ipAddress}:{port}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bind Error: {ex.Message}");
            return;
        }

        while (true)
        {
            var client = await socket.AcceptAsync();
            Console.WriteLine($"\n[CONNECTED]: {client.RemoteEndPoint}");
            _ = Task.Run(() => HandleClient(client));
        }
    }

    static async Task HandleClient(Socket client)
    {
        try
        {
            while (true)
            {
                byte[] header = new byte[8];
                int hReceived = await client.ReceiveAsync(header, SocketFlags.None);
                if (hReceived == 0) break;

                int type = BitConverter.ToInt32(header, 0);
                int length = BitConverter.ToInt32(header, 4);

                byte[] data = new byte[length];
                int received = 0;
                while (received < length)
                {
                    int r = await client.ReceiveAsync(new ArraySegment<byte>(data, received, length - received), SocketFlags.None);
                    if (r == 0) break;
                    received += r;
                }
                if (type == 1)
                {
                    string message = Encoding.UTF8.GetString(data);
                    Console.WriteLine($"[{client.RemoteEndPoint}]: {message}");

                    try
                    {
                        using (SpeechSynthesizer synth = new SpeechSynthesizer())
                        {
                            synth.SetOutputToDefaultAudioDevice();
                            synth.Speak(message);
                        }
                    }
                    catch (Exception ex) { Console.WriteLine("Speech Error: " + ex.Message); }

                    if (message.ToLower() == "exit") break;
                }

                else if (type == 2)
                {
                    string imgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"received_{Guid.NewGuid()}.jpg");
                    await File.WriteAllBytesAsync(imgPath, data);
                    Console.WriteLine($"[IMAGE RECEIVED]: Saved as {imgPath}");

                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = imgPath,
                            UseShellExecute = true,
                            Verb = "open"
                        };
                        Process.Start(psi);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Image Open Error: " + ex.Message);
                    }
                }
            }
        }
        catch
        {
            Console.WriteLine($"\n[DISCONNECTED]: {client.RemoteEndPoint}");
        }
        finally
        {
            client.Close();
        }
    }
}


