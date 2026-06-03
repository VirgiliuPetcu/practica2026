using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace practica.Entity
{
    public class LogEvent
    {
        public string time { get; set; }
        public string type { get; set; }
        public string Message { get; set; }
        public LogEvent(string _time, string _type, string _message) {
            time = _time;
            type = _type;
            Message = _message;
        }   
    }
}
