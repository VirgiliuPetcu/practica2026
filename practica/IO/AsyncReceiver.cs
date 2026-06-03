using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace practica.IO
{
    public  class AsyncReceiver
    {

        private SerialPort serialPort;
        private bool listen;
        private string CRLF;

        public event EventHandler<string>? DataReceived;


        public AsyncReceiver(SerialPort _serial) {
            serialPort = _serial;
            listen = false;
            CRLF = "\r\n";
            serialPort.DataReceived += OnDataReceive;
            

           

        }

        public void StartListening() {
            listen = true;
            
            Console.WriteLine("Started listening for serial data...");
        }

        public void StopListening()
        {
            listen = false;
            Console.WriteLine("Stopped listening for serial data...");
        }



        private void OnDataReceive(object sender,SerialDataReceivedEventArgs e ) {
            if (!listen)
            {
                return;
            }

            try
            {
                if (serialPort.BytesToRead > 0)
                {
                    string payload = serialPort.ReadTo(CRLF);
                    if (!string.IsNullOrEmpty(payload))
                    {

                        DataReceived?.Invoke(this, payload);

                    }
                }

            }
            catch (TimeoutException ex) { 
                Console.WriteLine($"Timeout while reading data: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in async data reception: {ex.Message}");
            }


        }



    }
};
