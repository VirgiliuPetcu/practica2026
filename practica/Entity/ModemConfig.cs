using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace practica.Entity
{
    [XmlRoot("DeviceConfiguration")]
    public class ModemConfig
    {
        [XmlElement("DeviceName")]

        public string DeviceName { get; set; } = "";
        [XmlElement("UserConfigurationPath")]
        public string UserConfigurationPath { get; set; } = "Users.xml";
        [XmlElement("ComPort")]
        public string ComPort { get; set; } = "COM9";
        [XmlElement("BaudRate")]
        public int BaudRate { get; set; } = 115200;
        [XmlElement("Parity")]


        public System.IO.Ports.Parity Parity { get; set; } = System.IO.Ports.Parity.None;
        [XmlElement("DataBits")]
        public int DataBits { get; set; } = 8;
        [XmlElement("StopBits")]
        public System.IO.Ports.StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;
        [XmlElement("CountryFormat")]
        public string CountryFormat { get; set; } = "";
        [XmlElement("PinCode")]
        public string PinCode { get; set; } = "";
        [XmlElement("PukCode")]
        public string PukCode { get; set; } = "";

        [XmlElement("SecretMsg")]
        public string SecretMsg { get; set; } = "";

        [XmlElement("LogFileName")]
        public string LogFileName { get; set; } = "";
    }
}
