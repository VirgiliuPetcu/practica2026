using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace practica.IO
{
    public class AsyncWriter
    {

        private SerialPort serialPort;
        private string CRLF;

        public AsyncWriter(SerialPort _serialPort) {
            CRLF = "\r\n";
            serialPort = _serialPort;
            

        }

        public void SendCommand(string command) {

            if (!serialPort.IsOpen) {
                Console.WriteLine($"Cannot write to closed port");
                throw new InvalidOperationException("Serial port is not open.");
                
            }
            try
            {
                serialPort.Write(command + CRLF);
                Console.WriteLine($"Sent {command}");
                serialPort.WriteTimeout = 500;
                
            }
            catch (TimeoutException)
            {
                Console.WriteLine($"Timeout {serialPort.WriteTimeout}");
                serialPort.WriteTimeout += 500; 
                SendCommand(command);
            }
            catch (InvalidOperationException ex) {
                Console.WriteLine($"Error sending data: {ex.Message}");

            }
        
     

        }
            



    }
}
