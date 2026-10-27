using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Axona_WebSocket_Service
{
    internal class KL1vRequest
    {
        public string port { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public DateTime timestamp { get; set; }
    }

    internal class KL1vResponse
    {
        public string? type { get; set; } = string.Empty;
        public DateTime timestamp { get; set; }
        public string version { get; set; } = string.Empty;
        public int errorCode { get; set; }
        public string errorDescription { get; set; } = string.Empty;
        public int? deviceStatus { get; set; }
        public int? deviceError { get; set; }
        public int? pillsCounted { get; set; }
        public int? trayStatus { get; set; }
        public int? pourIndicator { get; set; }
        public int? specialMode { get; set; }
        public bool? inventoryMode { get; set; }
        public bool? partialTabDetected { get; set; }
        public int? vialSize { get; set; }
        public bool? keyPadStatus { get; set; }
    }



    internal class KL1v : WebSocketBehavior
    {
        private readonly string stxStr = ((char)2).ToString();
        private readonly string etxStr = ((char)3).ToString();

        private SerialPort _serialPort = new SerialPort();

        private void OpenPort()
        {
            Console.WriteLine("Serial port status: " + _serialPort.IsOpen);

            if (!_serialPort.IsOpen)
            {
                // Basic configuration configurations
                _serialPort.BaudRate = 14400;
                _serialPort.DataBits = 8;
                _serialPort.Parity = Parity.None;
                _serialPort.StopBits = StopBits.One;
                _serialPort.Handshake = Handshake.None;

                // Set timeouts to prevent the UI thread from locking up completely
                _serialPort.ReadTimeout = 500;
                _serialPort.WriteTimeout = 500;

                _serialPort.Open();

                Console.WriteLine("Connected to serial port " + _serialPort.PortName);

            }

        }

        private void ClosePort()
        {
            Console.WriteLine("Serial port status: " + _serialPort.IsOpen);

            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }


        private bool CalcBCC(string KLCmd, out string BCC)
        {
            int sumCheck = 0; for (int i = 0; i < KLCmd.Length; i++) { sumCheck ^= KLCmd[i]; }
            string calcStr = sumCheck.ToString("X2"); // two-digit uppercase hex
            BCC = calcStr;
            return true;
        }

        private void SendRequest(string request)
        {
            Console.WriteLine("Sending message: " + request);

            request = stxStr + request + etxStr;
            CalcBCC(request, out string BCC);
            request += BCC;
            _serialPort.Write(request);
        }

        private string GetResponse()
        {
            string response = "";

            int totalBytesRead = 0;
            byte[] bytes = new byte[100];
            Array.Clear(bytes, 0, bytes.Length);
            int count = _serialPort.Read(bytes, 0, 100);

            while (count > 0)
            {
                totalBytesRead += count;
                //                Console.WriteLine("Read " + count.ToString() + " bytes");
                //                Console.WriteLine(string.Join(", ", bytes));

                if (totalBytesRead >= 3 && bytes[totalBytesRead - 3] == etxStr[0]) // Check for ETX character
                {
                    //                    Console.WriteLine("ETX found at position " + (totalBytesRead - 3).ToString());
                    break; // Exit the loop if ETX is found
                }

                count = _serialPort.Read(bytes, totalBytesRead, 100 - totalBytesRead);

            }

            //            Console.WriteLine("Read " + totalBytesRead.ToString() + "bytes");
            //            Console.WriteLine(string.Join(", ", bytes));

            response = Encoding.UTF8.GetString(bytes, 1, totalBytesRead - 4);
            Console.WriteLine("Received response: " + response);

            return response;
        }

        protected override void OnClose(CloseEventArgs e)
        {
            ClosePort();
            base.OnClose(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            Console.WriteLine("Serial ports available");
            Console.WriteLine(string.Join(", ", ports));

            var request = JsonSerializer.Deserialize<KL1vRequest>(e.Data);
            Console.WriteLine(request?.ToString());

            KL1vResponse response = new KL1vResponse
            {
                type = request?.type,
                timestamp = DateTime.Now,
                errorCode = 0,
                errorDescription = ""
            };

            try
            {
                if (request?.port != null && request.port != "" && _serialPort.PortName != request.port)
                {
                    _serialPort.PortName = request.port;
                }

                OpenPort();

                if (request?.type.Equals("Hello") == true)
                {
                    string helloMsg = "H";
                    SendRequest(helloMsg);
                    string machineResponse = GetResponse();
                    response.version = machineResponse.Substring(3, machineResponse.Length - 3);

                }
                else if (request?.type.Equals("Zero") == true)
                {
                    string zeroMsg = "Z";
                    SendRequest(zeroMsg);
                    string machineResponse = GetResponse();
                }
                else if (request?.type.Equals("Status") == true)
                {
                    string statusMsg = "S";
                    SendRequest(statusMsg);
                    string machineResponse = GetResponse();

                    response.deviceStatus = Convert.ToInt32(machineResponse.Substring(1, 1));
                    response.deviceError = Convert.ToInt32(machineResponse.Substring(2, 1));
                    response.pillsCounted = Convert.ToInt32(machineResponse.Substring(3, 4));
                    response.trayStatus = Convert.ToInt32(machineResponse.Substring(7, 1));
                    response.pourIndicator = Convert.ToInt32(machineResponse.Substring(8, 1));
                    response.specialMode = Convert.ToInt32(machineResponse.Substring(9, 1));
                    response.inventoryMode = Convert.ToInt32(machineResponse.Substring(10, 1)) != 0;
                    response.partialTabDetected = Convert.ToInt32(machineResponse.Substring(11, 1)) != 0;
                    response.vialSize = Convert.ToInt32(machineResponse.Substring(12, 3));
                    response.keyPadStatus = Convert.ToInt32(machineResponse.Substring(15, 1)) != 0;

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                response.errorCode = 1;
                response.errorDescription = ex.Message;
                ClosePort();
            }

            Console.WriteLine("Received: {0}", e.Data);
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            Send(JsonSerializer.Serialize<KL1vResponse>(response, options));
            Console.WriteLine("Sent: {0}", JsonSerializer.Serialize<KL1vResponse>(response));
        }
    }
}
