using practica.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

using System.Xml.Serialization;

namespace practica.Config
{
    public class ConfigManager
    {

        public static readonly string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration.xml");
        

        
        public static ModemConfig LoadConfig()
        {

            Console.WriteLine(filePath);
            try
            {
                if (File.Exists(filePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(ModemConfig));
                    using (FileStream fs = new FileStream(filePath, FileMode.Open))
                    {
                        
                        return (ModemConfig)serializer.Deserialize(fs);
                    }
                }
                else
                {
                    Console.WriteLine($"Configuration file not found at {filePath}. Creating default configuration.");
                    ModemConfig defaultConfig = new ModemConfig();
                    SaveConfig(defaultConfig);
                    return defaultConfig;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                return new ModemConfig(); // Return default config on error
            }
        }


        public static string GetActivePortByName(string targetDeviceName)
        {

            if (string.IsNullOrWhiteSpace(targetDeviceName))
            {
                Console.WriteLine("Error: Target device name is missing or null in configuration.");
                return string.Empty;
            }
            string query = "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'";

            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject device in searcher.Get())
                {
                    string friendlyName = device["Name"]?.ToString();
                    Console.WriteLine($"Found device: {friendlyName}");

                    if (!string.IsNullOrEmpty(friendlyName) &&
                        friendlyName.Contains(targetDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Found device: {friendlyName}");

                       
                        Match match = Regex.Match(friendlyName, @"\((COM\d+)\)");
                        if (match.Success)
                        {
                            Console.WriteLine($"Extracted COM port: {match.Groups[1].Value}");
                            string comPort = match.Groups[1].Value;
                             return comPort;
                            
                          
                        }
                    }
                }
            }
            Console.WriteLine($"Device with name containing '{targetDeviceName}' not found.");  
            return string.Empty ; // Not found or not available
        }

        public static void SaveConfig(ModemConfig config)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ModemConfig));
                using (FileStream fs = new FileStream(filePath, FileMode.Create))
                {
                    serializer.Serialize(fs, config);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
            }
        }
    }
}
