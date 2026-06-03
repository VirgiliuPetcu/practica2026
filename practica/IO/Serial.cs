using Practica;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace practica.IO
{
    public class Serial
    {

        private SerialPort port;
        private AsyncReceiver receiver;
        private AsyncWriter writer;


        public void Subscribe(EventHandler<string> handler) => receiver.DataReceived += handler;
        public void Unsubscribe(EventHandler<string> handler) => receiver.DataReceived -= handler;
        public Serial(string _portName, int _baudRate, Parity _parity, int _dataBits, StopBits _stopBits)
        {

            port = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits);
            try
            {
                port.Open();
                ClearSerialBuffer();
                Console.WriteLine($"Port {port.PortName} is open");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening port: {ex.Message}");
                Thread.Sleep(1000);
            }


            port.ReadTimeout = 500;
            port.WriteTimeout = 500;

            receiver = new AsyncReceiver(port);
            writer = new AsyncWriter(port);



        }
        public  void ClearSerialBuffer() {
            if (port != null && port.IsOpen) {
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
            }
        }

        public void Close() {
            if (port.IsOpen) {
                port.Close();
                Console.WriteLine($"Port {port.PortName} is closed");
            }
        }



        public void  ListenToPort() {
            receiver.StartListening();
        }
        public void StopListenToPort() { 
            receiver.StopListening();
        }

        public void SendCommand(string command) {
            writer.SendCommand(command);
        }

     
    }

    public class SerialEventArgs :   EventArgs 
    {
        public string portName;
        public int baudRate;
        public Parity parity;
        public int dataBits;
        public  StopBits stopBits;

        public SerialEventArgs(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            this.portName = portName;
            this.baudRate = baudRate;
            this.parity = parity;
            this.dataBits = dataBits;
            this.stopBits = stopBits;
        }
    }
}
