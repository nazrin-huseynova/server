using System.Diagnostics;
using System.Globalization;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Speech.Recognition;
using System.Text;

namespace Client;

public class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "Client - Voice & Image Enabled";
        var ipAddress = IPAddress.Parse("192.168.1.72");
        var port = 27001;
        var ep = new IPEndPoint(ipAddress, port);
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            await socket.ConnectAsync(ep);
            if (socket.Connected)
            {
                Console.WriteLine("Connected successfully");

                _ = Task.Run(() => StartVoiceRecognition(socket));
                _ = Task.Run(() => ReceiveHandler(socket));

                while (true)
                {
                    var input = Console.ReadLine()?.Trim('\"');
                    if (string.IsNullOrEmpty(input)) continue;

                    if (File.Exists(input) && (input.EndsWith(".jpg") || input.EndsWith(".png") || input.EndsWith(".jpeg")))
                    {
                        byte[] imgData = await File.ReadAllBytesAsync(input);
                        await SendData(socket, imgData, 2);
                        Console.WriteLine("[SYSTEM]: Image file detected and sent.");
                    }
                    else
                    {
                        byte[] textData = Encoding.UTF8.GetBytes(input);
                        await SendData(socket, textData, 1);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    static void StartVoiceRecognition(Socket socket)
    {
        try
        {
            var culture = new CultureInfo("en-US");
            using (SpeechRecognitionEngine recognizer = new SpeechRecognitionEngine(culture))
            {
                recognizer.LoadGrammar(new DictationGrammar());
                recognizer.SetInputToDefaultAudioDevice();

                recognizer.SpeechRecognized += async (s, e) =>
                {
                    if (e.Result.Confidence > 0.1)
                    {
                        Console.WriteLine($"\n[VOICE DETECTED]: {e.Result.Text}");
                        byte[] voiceTextData = Encoding.UTF8.GetBytes(e.Result.Text);
                        await SendData(socket, voiceTextData, 1);
                        Console.Write("Your message: ");
                    }
                };

                recognizer.RecognizeAsync(RecognizeMode.Multiple);
                Console.WriteLine("Mic is listening in ENGLISH... (en-US)");

                while (true) { Thread.Sleep(1000); }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("\nSpeech Error: " + ex.Message);
        }
    }

    static async Task SendData(Socket socket, byte[] data, int type)
    {
        try
        {
            byte[] header = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(type), 0, header, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(data.Length), 0, header, 4, 4);

            await socket.SendAsync(header, SocketFlags.None);
            await socket.SendAsync(data, SocketFlags.None);
        }
        catch { }
    }

    static async Task ReceiveHandler(Socket socket)
    {
        try
        {
            while (true)
            {
                byte[] header = new byte[8];
                int hReceived = await socket.ReceiveAsync(header, SocketFlags.None);
                if (hReceived == 0) break;

                int type = BitConverter.ToInt32(header, 0);
                int length = BitConverter.ToInt32(header, 4);

                byte[] data = new byte[length];
                int received = 0;
                while (received < length)
                {
                    int r = await socket.ReceiveAsync(new ArraySegment<byte>(data, received, length - received), SocketFlags.None);
                    if (r == 0) break;
                    received += r;
                }

                switch (type)
                {
                    case 1:
                        string message = Encoding.UTF8.GetString(data);
                        Console.WriteLine($"\n[SERVER]: {message}");
                        break;

                    case 2: 
                        string imgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"incoming_{Guid.NewGuid()}.jpg");
                        await File.WriteAllBytesAsync(imgPath, data);
                        Console.WriteLine($"\n[IMAGE RECEIVED]: {imgPath}");

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
                        catch { Console.WriteLine("Could not open image automatically."); }
                        break;

                    case 3:
                        string sndPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"incoming_{Guid.NewGuid()}.wav");
                        await File.WriteAllBytesAsync(sndPath, data);
                        using (var player = new SoundPlayer(sndPath))
                        {
                            player.Load();
                            player.Play();
                        }
                        break;
                }
                Console.Write("Your message: ");
            }
        }
        catch { Console.WriteLine("\nDisconnected."); }
    }
}
