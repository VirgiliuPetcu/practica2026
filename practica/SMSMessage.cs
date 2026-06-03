using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Practica
{
    public enum MsgStatus {
        UNREAD,
        READ
    };
    public class SMSMessage
    {

        public string number;
        public DateTime? Time;
        public string Message;
        public MsgStatus status;
        public int index;


        public SMSMessage(string fromLine, string Message ,string NumberPrefix) {

            string[] unformated_line = fromLine.Split('"');
            string[] unfromated_line2 = fromLine.Split(',');
            Console.WriteLine(unformated_line.Length);
            string number = unformated_line[3];

            try
            {
                this.Time = ParseSmsTimestamp(unformated_line[7]);
            }
            catch (InvalidTimeZoneException e) {

                Console.WriteLine($"Invalid format for time : {e.Message}");
                this.Time = null;
            }

            this.index = this.index = int.Parse(unformated_line[0].Remove(0, 7).Trim(','));


            Console.WriteLine($"Format:{number}");
            if (IsValidPhoneNumber(number))
            {
              string country_format = "+" + int.Parse(NumberPrefix).ToString();
                if (country_format == number.Substring(0, country_format.Length))
                {
                    this.number = "0" + number.Remove(0, country_format.Length);
                    Console.WriteLine(number);
                }
            }

          
            if (unformated_line[1].Contains("REC UNREAD"))
            {
                this.status = MsgStatus.UNREAD;
            }
            else
            {
                this.status = MsgStatus.READ;
            }

            this.Message = Message;
          
        }

        public SMSMessage(int index, string fromLine, string Message, string NumberPrefix) {

            string[] unformated_line = fromLine.Split('"');
            string[] unfromated_line2 = fromLine.Split(',');
            Console.WriteLine(unformated_line.Length);
            string number = unformated_line[3];

            try
            {
                this.Time = ParseSmsTimestamp(unformated_line[7]);
            }
            catch (InvalidTimeZoneException e)
            {

                Console.WriteLine($"Invalid format for time : {e.Message}");
                this.Time = null;
            }

            this.index = index;

            Console.WriteLine($"Format:{number}");
            if (IsValidPhoneNumber(number))
            {
                string country_format = "+" + int.Parse(NumberPrefix).ToString();
                if (country_format == number.Substring(0, country_format.Length))
                {
                    this.number = "0" + number.Remove(0, country_format.Length);
                    Console.WriteLine(number);
                }
            }


            if (unformated_line[1].Contains("REC UNREAD"))
            {
                this.status = MsgStatus.UNREAD;
            }
            else
            {
                this.status = MsgStatus.READ;
            }

            this.Message = Message;



            Console.WriteLine("constructed sms with index");
        }

        private DateTime? ParseSmsTimestamp(string TimeStamp) {

            int index = TimeStamp.LastIndexOf("+");
            if (index == -1) { 
                index = TimeStamp.LastIndexOf("-");
            }
            if (index == -1) {
                throw new InvalidTimeZoneException("Invalid time format");
            }
            string timepart = TimeStamp.Substring(0, index);
            try
            {
                DateTime time = DateTime.ParseExact(timepart, "yy/MM/dd,HH:mm:ss", CultureInfo.InvariantCulture);
                return time;
            }
            catch (ArgumentNullException ex)
            {
                Console.WriteLine($"No time to parse {ex.Message}");
            }
            catch (FormatException ex) {
                Console.WriteLine($"Invalid format argument for time : {ex.Message}");

            }
            return null;



        }


        public static bool IsValidPhoneNumber(string phoneNumber)
        {
            // Matches international format with optional +, 1-4 digit country code, and 7-14 remaining digits
            string pattern = @"^\+?[1-9]\d{7,14}$";
            return Regex.IsMatch(phoneNumber, pattern);
        }


        
        
        public bool IsUnread => this.status == MsgStatus.UNREAD;

    }
}
