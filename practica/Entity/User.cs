using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace practica.Entity
{

    [XmlRoot("User")]
    public class User
    {
        [XmlElement("Name")]
        public string Name { get; set; }
        [XmlElement("Number")]
        public string Number { get; set; }
        [XmlElement("MAC")]   
        public string MAC { get; set; }

        public User() { }
        public User(string name, string number, string mac) {
            Name = name;
            Number = number;
            MAC = mac;
        }
    }
}
