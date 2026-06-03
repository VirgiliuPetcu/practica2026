using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace practica.Entity
{

    public enum TerminalStatus { 
    
        OK,
        ERROR,
        CMEERROR,
        CMSERROR,
        TIMEOUT,
        DISCONNECTED

    } 

    public class AtResponse
    {
        public TerminalStatus Status { get; set; }
        public int ErrorCode { get; set; }
        public List<string> DataLines { get; set; } = new();

        public bool IsOk() { 
            return Status == TerminalStatus.OK;
        }
    }
}
