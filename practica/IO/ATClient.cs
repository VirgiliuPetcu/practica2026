using practica.Entity;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace practica.IO
{

  
    public class ATClient
    {

        public  Serial serialport;
        

        private EventHandler<string>? activeCommandHandler;

        public EventHandler<string>? CallReceived;
        public EventHandler<int>? SmsReceived;



        public ATClient(string comPort, int baudRate ,Parity parity , int dataBits , StopBits stopBits) {
            
            serialport = new Serial(comPort, baudRate, parity, dataBits, stopBits);
            
            serialport.ListenToPort();
            serialport.Subscribe(Router);
            
            
        }
   

        
        



        public void ClosePort() {

            CallReceived = null;
            SmsReceived = null;

            
            if (serialport != null)
            {
                serialport.Close(); 
            }

        } 



        private void Router(object? sender, string line)
        {
           
            if (line.StartsWith("+CMTI:"))
            {
                Console.WriteLine(line);
                string[] parts = line.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int index)){ 
                       SmsReceived?.Invoke(sender, index);
                }
                return;
            }

            else if (line.StartsWith("RING") || line.StartsWith("+CLIP:"))
            {
                CallReceived?.Invoke(sender, line);
                return;
            }

          
            else if (activeCommandHandler != null)
            {
                activeCommandHandler?.Invoke(sender, line);
                return;
            }

           
            else if (!string.IsNullOrWhiteSpace(line))
            {
                Console.WriteLine($"[UNHANDLED MODEM ALERT / DATA]: {line}");
            }
        }

        public async Task<AtResponse> SendAsync(string command, int timeoutMs = 5000) {
            var tcs = new TaskCompletionSource<AtResponse>();
            var dataLines = new List<string>();

            void OnLine(object? s, string line)
            {
           
                if (line == "OK")
                    tcs.TrySetResult(new AtResponse
                    {
                        Status = TerminalStatus.OK,
                        DataLines = dataLines
                    });
                else if (line == "ERROR")
                    tcs.TrySetResult(new AtResponse
                    {
                        Status = TerminalStatus.ERROR,
                        DataLines = dataLines
                    });
                else if (line.StartsWith("+CME ERROR:"))
                    tcs.TrySetResult(new AtResponse
                    {
                        Status = TerminalStatus.CMEERROR,
                        ErrorCode = ParseCode(line),
                        DataLines = dataLines
                    });
                else if (line.StartsWith("+CMS ERROR:"))
                    tcs.TrySetResult(new AtResponse
                    {
                        Status = TerminalStatus.CMSERROR,
                        ErrorCode = ParseCode(line),
                        DataLines = dataLines
                    });
                else
                    dataLines.Add(line);
            }

            activeCommandHandler = OnLine;
            try
            {
                serialport.SendCommand(command);
            }
            catch (InvalidOperationException ex) {
                return new AtResponse { Status = TerminalStatus.DISCONNECTED };
            }

            if (await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)) == tcs.Task)
            {
                activeCommandHandler = null;
                return await tcs.Task;
            }

            activeCommandHandler = null;
            return new AtResponse { Status = TerminalStatus.TIMEOUT };
        }

        private int ParseCode(string line)
        {
            // "+CME ERROR: 10" → 10
            var parts = line.Split(':');
            return parts.Length > 1 && int.TryParse(parts[1].Trim(), out int code)
                ? code : -1;
        }



    }

     
}
