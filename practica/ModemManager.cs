using practica.Config;
using practica.Entity;
using practica.IO;
using Practica;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace practica
{
    enum State
    {

        BOOTING,
        CONFIG,
        IDLE,
        SMS,
        CALL,
        FAIL
    }
    public class ModemManager
    {

        ATClient client;
        UsersConfiguration UserManager;
        ModemConfig ModemConfiguration;

        private DateTime ultimulSmsPrimit;
        private bool avemSmsInAsteptare;
        private readonly TimeSpan timpDeAsteptare;
        private BackgroundLogger logger;
        private System.Timers.Timer isUpTimer;
        int error_count = 0;
        int fatalError_count = 0;

        State current_state;



        public ModemManager(BackgroundLogger _logger) {
            logger = _logger;


            ModemConfiguration = ConfigManager.LoadConfig();
            if (ModemConfiguration == null)
            {
                Console.WriteLine($"FAILED TO LOAD CONFIGURATION, CHECK IF CONFIG FILE EXISTS AND IS PROPERLY FORMATED");
                logger.Log(LOGINFO.ERROR, $"FAILED,{ConfigManager.filePath} CHECK IF CONFIG FILE EXISTS AND IS PROPERLY FORMATED");
                Environment.Exit(1);
            }
            else {
                logger.Log(LOGINFO.INFO, $"CONFIGURATION File {ConfigManager.filePath} LOADED SUCCESSFULLY ");
            }


            logger.Log(LOGINFO.INFO, $"APPLICATION LOGGING STARTED");
            Console.WriteLine($"CONFIG LOADED COM: {ModemConfiguration.ComPort} BAUDRATE: {ModemConfiguration.BaudRate} DEVICE NAME: {ModemConfiguration.DeviceName}");

            Console.WriteLine($"LOADING USER CONFIGURATION FROM {ModemConfiguration.UserConfigurationPath}");
            string xmlStr = string.IsNullOrEmpty(ModemConfiguration.UserConfigurationPath) ? "Users.xml" : ModemConfiguration.UserConfigurationPath;
            UserManager = new UsersConfiguration(xmlStr);
            if (UserManager.isUserConfigurationLoaded)
            {
                logger.Log(LOGINFO.INFO, $"USER CONFIGURATION LOADED SUCCESSFULLY ->{UserManager.UserCount} Users");
            }
            else {
                logger.Log(LOGINFO.ERROR, $"FAILED TO LOAD USER CONFIGURATION, CHECK IF CONFIG FILE EXISTS AND IS PROPERLY FORMATED");
            }



            timpDeAsteptare = TimeSpan.FromSeconds(3);
            ultimulSmsPrimit = DateTime.MinValue;
            avemSmsInAsteptare = false;
            error_count = 0;
            fatalError_count = 0;


            TimerSetup(60);
            ConnectToModem();


            current_state = State.BOOTING;

        }

        private void TimerSetup(int timeInSeconds) {

            isUpTimer = new System.Timers.Timer(timeInSeconds * 1000);
            isUpTimer.Elapsed += IsModemUpCheck;
            isUpTimer.AutoReset = true;
         
        }
        private async void IsModemUpCheck(object? sender, System.Timers.ElapsedEventArgs e) {
            if (current_state != State.IDLE) {
                return; 
            }
            isUpTimer?.Stop();
            try
            {

                TerminalStatus commandStatus = TerminalStatus.ERROR;
                commandStatus = await PingModem();
                Console.WriteLine("exit");
                if (commandStatus != TerminalStatus.OK)
                {
                    logger.Log(LOGINFO.ERROR, "Lost contact with Modem");
                    current_state = State.FAIL;
                    return;
                }
                TerminalStatus simReady = await CheckIfReady();
                CheckCommandStatus(simReady);
                if (simReady != TerminalStatus.OK)
                {
                    TerminalStatus pinSet = await SetPin(ModemConfiguration.PinCode);
                    CheckCommandStatus(pinSet);
                    if (pinSet != TerminalStatus.OK)
                    {
                        logger.Log(LOGINFO.ERROR, "SIM not ready. Triggering SOFT reset.");
                        current_state = State.FAIL;
                        return;
                    }
                }
                TerminalStatus checkNetwork = await CheckNetworkRegistration(true);
                if (checkNetwork != TerminalStatus.OK)
                {
                    logger.Log(LOGINFO.ERROR, "Network registration failed. Triggering SOFT reset.");
                    current_state = State.FAIL;
                    return;
                }
              
                CheckCommandStatus(checkNetwork);

               


            }
            catch (Exception ex) {
                logger.Log(LOGINFO.ERROR, $"Exception during modem check: {ex.Message}");
                CheckCommandStatus(TerminalStatus.DISCONNECTED);
                current_state = State.FAIL;


            }
            finally
            {
                if (current_state != State.FAIL)
                {
                    isUpTimer?.Start(); 
                }
            }






        }

        public void ConnectToModem() {

            if (client != null) {
                client.ClosePort();
                client = null;
            }
            if (ModemConfiguration != null) {
                try
                {
                    
                    string foundCom = ConfigManager.GetActivePortByName(ModemConfiguration.DeviceName);
                    if (!string.IsNullOrEmpty(foundCom))
                    {
                        client = new ATClient(foundCom, ModemConfiguration.BaudRate, ModemConfiguration.Parity, ModemConfiguration.DataBits, ModemConfiguration.StopBits);
                        ModemConfiguration.ComPort = foundCom;
                        ConfigManager.SaveConfig(ModemConfiguration);
                        client.CallReceived += onCall;
                        client.SmsReceived += OnSmsIndexReceived;

                    }
                    else
                    {
                        Console.WriteLine("CRITICAL ERROR: Device could not be found by name or COM port");
                        logger.Log(LOGINFO.ERROR,"CRITICAL ERROR: Device could not be found by name or COM port");
                       
                    }
                }
                catch(Exception ex)
                {

                    Console.WriteLine($"FAILED TO CONNECT TO {ModemConfiguration.ComPort} from config file");
                    
                    
                    client = new ATClient(ModemConfiguration.ComPort, ModemConfiguration.BaudRate, ModemConfiguration.Parity, ModemConfiguration.DataBits, ModemConfiguration.StopBits);
                    client.CallReceived += onCall;
                    client.SmsReceived += OnSmsIndexReceived;
                     

                }
            }
            else {
                Console.WriteLine("CRITICAL ERROR: MODEM CONFIGURATION IS NULL");
            }
        }
        public async Task<TerminalStatus> PingModem() {

            AtResponse at = await client.SendAsync("AT");
            return at.Status;
        }

        public async Task<TerminalStatus> CheckIfReady() {
            AtResponse response = await client.SendAsync("AT+CPIN?");
            if (response.DataLines.Any(line => line.Contains("READY")))
            {
                Console.WriteLine("SIM IS READY");
                return TerminalStatus.OK;
            }
            if (response.Status == TerminalStatus.CMEERROR) {
                switch (response.ErrorCode) {

                    case 10:
                        logger.Log(LOGINFO.ERROR, "SIM NOT INSERTED");
                        break;
                    case 17:
                        logger.Log(LOGINFO.ERROR, "PIN2 AUTHENTIFICATION FAILURE");
                        break;
                    case 18:
                        logger.Log(LOGINFO.ERROR, "PUK2 AUTHENTIFICATION FAILURE");
                        break;

                }
            }
            return response.Status;
        }

        public async Task<TerminalStatus> EnableCallerIdentification() {
            AtResponse response = await client.SendAsync("AT+CLIP=1");
            if (response.IsOk()) {
                Console.WriteLine("ENABLE CALLER IDENTIFICATION");
                return TerminalStatus.OK;
            }
            return response.Status;
        }

        public async Task<TerminalStatus> HangUpCall() {

            AtResponse response = await client.SendAsync("ATH");

            if (response.IsOk())
            {
                Console.WriteLine("CALL HANG UP");
                return TerminalStatus.OK;
            }
            return response.Status;
        }


        public async Task<TerminalStatus> SetPin(string PIN) {

            AtResponse response = await client.SendAsync("AT+CPIN=" + PIN);
            if (response.IsOk())
            {
                Console.WriteLine("SIM UNLOCKED");
                return TerminalStatus.OK;
            }
            if (response.Status == TerminalStatus.ERROR)
            {
                Console.WriteLine("ERROR AT SETTING PIN");
                logger.Log(LOGINFO.ERROR, "ERROR AT SETTING PIN");
            }
            return response.Status;
        }

        public async Task<TerminalStatus> SendSMS(string number, string text) {
            string new_number = number.Remove(0, 1);
            Console.WriteLine($"newNUmber =  {new_number}");
            if (int.TryParse(ModemConfiguration.CountryFormat, out int country_code))
            {
                Console.WriteLine($"Country code {country_code} ");
                new_number = "+" + country_code.ToString() + new_number;
                AtResponse response = await client.SendAsync($"AT#CMGS=\"{new_number}\",\"{text}\"");
                if (response.DataLines.Any(Line => Line.Contains("#CMGS:")))
                {
                    Console.WriteLine($"SMS TO: {new_number} WITH: {text} Send Succesfully");
                    return TerminalStatus.OK;
                }


            }
            else {
                Console.WriteLine($"Could not parse the number{number}");
            }
            return TerminalStatus.ERROR;

        }

        public async Task<TerminalStatus> ShowTextModeParameters() {
            AtResponse response = await client.SendAsync("AT+CSDH=1");
            if (response.IsOk()) {
                Console.WriteLine("Show text mode Parameters Enabled");
                return TerminalStatus.OK;
            }
            Console.WriteLine("ERR COULD NOT ENABLE text mode parameters");
            return response.Status;

        }
        public async Task<TerminalStatus> setErrorFormating() {
            AtResponse response = await client.SendAsync("AT+CMEE=1");
            Console.WriteLine("FORMATING SET TO 1");
            if (response.IsOk()) {
                return TerminalStatus.OK;
            }
            Console.WriteLine("ERROR FORMATING SET TO 1");
            return response.Status;
        }

        public async Task<TerminalStatus> CheckAntennaSignalQuality() {
            AtResponse response = await client.SendAsync("AT+CSQ");

            if (response.IsOk())
            {
                string? line = response.DataLines.FirstOrDefault(l => l.StartsWith("+CSQ:"));

                if (!string.IsNullOrWhiteSpace(line))
                {

                    int colonIndex = line.IndexOf(':');
                    if (colonIndex != -1)
                    {
                        string dataPart = line.Substring(colonIndex + 1).Trim();
                        string[] measurements = dataPart.Split(',');
                        if (measurements.Length >= 2)
                        {
                            if (int.TryParse(measurements[0].Trim(), out int signalStrengthIndicator) &&
                                int.TryParse(measurements[1].Trim(), out int bitErrorRate))
                            {

                                if (signalStrengthIndicator == 99)
                                {
                                    Console.WriteLine("Signal Strength: Not known or not detectable");
                                }
                                else
                                {
                                    Console.WriteLine($"Signal Strength: {-113 + (signalStrengthIndicator * 2)} dBm");
                                }


                                if (bitErrorRate == 99)
                                {
                                    Console.WriteLine("Bit Error Rate: Not known or not detectable");
                                }
                                else
                                {

                                    Console.WriteLine($"Error rate indicator: {(double)bitErrorRate * 0.2} - {(double)bitErrorRate * 0.4}");
                                }

                                return TerminalStatus.OK;
                            }
                            else
                            {
                                Console.WriteLine("Error: Could not parse signal parameters into integers.");
                            }
                        }
                    }
                }
                Console.WriteLine("Error: +CSQ response format was invalid or missing.");

                return TerminalStatus.ERROR;
            }

            Console.WriteLine("ERROR AT CHECKING ANTENNA SIGNAL QUALITY");
            return response.Status;
        }




        public async Task<TerminalStatus> SetNetworkRegistration() {
            AtResponse response = await client.SendAsync("AT+CREG=1");
            Console.WriteLine("Setting NETWORK REGISTRATION  TO 1");
            if (response.IsOk()) {
                return TerminalStatus.OK;

            }
            Console.WriteLine("ERROR AT SETTING NETWORK REGISTRATION");
            return response.Status;
        }

        public async Task<TerminalStatus> CheckNetworkRegistration(bool silence = false)
        {
            AtResponse response = await client.SendAsync("AT+CREG?");
            if (response.IsOk())
            {
                string? line = response.DataLines.FirstOrDefault(line => line.Contains("+CREG:"));
                if (line != null)
                {
                    string[] components = line.Split(' ');
                    if (components.Length >= 1)
                    {
                        string[] pair = components[1].Trim().Split(',');
                        if (pair.Length >=2) {
                            
                            switch (pair[1])
                            {
                                case "0":
                                    logger.Log(LOGINFO.ERROR, "Not registered, device is not currently searching a new operator to register to");
                                    return TerminalStatus.ERROR;

                                case "1":
                                    logger.Log(LOGINFO.INFO, "Registered, home network");
                                    Console.WriteLine("registered home");
                                    return TerminalStatus.OK;

                                case "2":
                                    logger.Log(LOGINFO.ERROR, "Not registered, but device is currently searching a new operator to register to");
                                    return TerminalStatus.ERROR;

                                case "3":
                                    if (!silence) { 
                                        logger.Log(LOGINFO.ERROR, "Registration denied");
                                    }
                                    return TerminalStatus.ERROR;

                                case "5":
                                    if (!silence)
                                    {
                                        logger.Log(LOGINFO.INFO, "Registered, roaming");
                                    }
                                    Console.WriteLine("registered roaming");
                                    return TerminalStatus.OK;
                                default:
                                    logger.Log(LOGINFO.WARNING, $"Unknown registration status: {pair}");
                                    return TerminalStatus.ERROR;
                            }
                        }

                    }

                }

            }
            logger.Log(LOGINFO.WARNING, "ERROR AT Checking NETWORK REGISTRATION");
            return response.Status;


        }








        public async Task<TerminalStatus> EnableSMStoTextFormat() {
            AtResponse response = await client.SendAsync("AT+CMGF=1");
            if (response.IsOk())
            {
                Console.WriteLine("SET SMS TO TEXT FORMAT");
                return TerminalStatus.OK;
            }
            Console.WriteLine("ERROR AT SETTING SMS TO TEXT FORMAT");
            return response.Status;
        }
        public async Task<TerminalStatus> EnableNewSMSIndications() {
            AtResponse response = await client.SendAsync("AT+CNMI=2,1,0,0,0");
            if (response.IsOk())
            {
                Console.WriteLine("ENABLED NEW SMS INDICATIONS");
                return TerminalStatus.OK;
            }
            Console.WriteLine("ERROR AT ENABLING NEW SMS INDICATIONS");
            return response.Status;
        }

        public async Task<TerminalStatus> DeleteMessageHistory() {
            AtResponse response = await client.SendAsync("AT+CMGD=1,4");
            if (response.IsOk())
            {
                Console.WriteLine("Deleted ALL the messages");
                return TerminalStatus.OK;
            }
            Console.WriteLine($"Could not perform delete operation");
            return response.Status;
        }

        public async Task<TerminalStatus> DeleteMessageByIndex(int index) {
            AtResponse response = await client.SendAsync($"AT+CMGD={index}");
            if (response.IsOk())
            {
                Console.WriteLine($"Deleted message at index {index}");
                return TerminalStatus.OK;
            }
            Console.WriteLine($"Could not delete message at index {index}");
            return response.Status;
        }



        public List<SMSMessage> GetListFormatedSMSFromString(List<string> unfromatedSMS) {
            List<SMSMessage> lst = new List<SMSMessage>();
            for (int i = 0; i < unfromatedSMS.Count; i++) {
                if (unfromatedSMS[i].StartsWith("+CMGL:")) {

                    string header = unfromatedSMS[i];
                    StringBuilder builder = new StringBuilder();
                    i++;

                    while (i < unfromatedSMS.Count && !unfromatedSMS[i].StartsWith("+CMGL:")) {
                        builder.Append(unfromatedSMS[i]);
                        i++;
                    }
                    i--;

                    SMSMessage SMS = new SMSMessage(header, builder.ToString().Trim(), ModemConfiguration.CountryFormat);
                    lst.Add(SMS);
                }

            }
            return lst;
        }

        public List<SMSMessage> GetFormatedSMSFromString(List<string> unfromatedSMS, int index) {
            List<SMSMessage> lst = new List<SMSMessage>();
            for (int i = 0; i < unfromatedSMS.Count; i++)
            {
                if (unfromatedSMS[i].StartsWith("+CMGR:"))
                {

                    string header = unfromatedSMS[i];
                    StringBuilder builder = new StringBuilder();
                    i++;

                    while (i < unfromatedSMS.Count && !unfromatedSMS[i].StartsWith("+CMGR:"))
                    {
                        builder.Append(unfromatedSMS[i]);
                        i++;
                    }
                    i--;

                    SMSMessage SMS = new SMSMessage(index, header, builder.ToString().Trim(), ModemConfiguration.CountryFormat);
                    lst.Add(SMS);
                }

            }
            return lst;

        }

        public async Task<List<SMSMessage>>? ReadMessageList() {
            AtResponse response = await client.SendAsync("AT+CMGL=\"REC UNREAD\"");
            if (response.IsOk()) {
                Console.WriteLine("UNREAD SMS FETCH SUCCESFULL");
                return GetListFormatedSMSFromString(response.DataLines);
            }
            Console.WriteLine("COULD NOT FETCH UNDREAD MESSAGED");
            return null;

        }

        public async Task<TerminalStatus> CheckMessageList() {
            Console.WriteLine("CHECKING MESSAGE LIST");
            List<SMSMessage> lst = await ReadMessageList();
            if (lst == null)
            {
                Console.WriteLine("Problem whenreading messages");
                return TerminalStatus.ERROR;
            }

            ResolveUnreadMessages(lst);

            return TerminalStatus.OK;
        }

        public async void ResolveUnreadMessages(List<SMSMessage> messages) {
            foreach (var message in messages)
            {
                Console.WriteLine($"Resolving message NO:{message.index} | From Number:{message.number} | TIME:{message.Time} | Content: {message.Message}");
                if (message.status == MsgStatus.UNREAD && UserManager.CheckNumber(message.number))
                {

                    if (message.Time != null && message.Time > DateTime.Now.AddMinutes(-5))
                    {
                        if (message.Message == ModemConfiguration.SecretMsg)
                        {
                            string UserMAC = UserManager.GetUserMacAdress(message.number);
                            if (UserMAC != string.Empty)
                            {
                                WakeOnLan.SendWakePacket(UserMAC);
                                Console.WriteLine($"MAGIC PACKET SENT TO {UserMAC} FROM SMS");
                                logger.Log(LOGINFO.SMS_ACCEPTED, $"MessageSMS ACCEPTED INDEX :{message.index} | From Number:{message.number} |Time of msg Receive To modem:{message.Time} | Content: {message.Message} | Started PC WITH MAC {UserMAC}");

                                TerminalStatus sentACK = await SendSMS(message.number, "AWAKE");
                                if (sentACK == TerminalStatus.OK) {
                                    logger.Log(LOGINFO.SMS_ACCEPTED, $"MessageSMS SENT : TO Number:{message.number}  | Content: AWAKE! ");
                                }
                            }
                        }
                        else {
                            Console.WriteLine("Mesaj Respins SecretMsg nu corespunde");
                            logger.Log(LOGINFO.SMS_REJECTED, $"MessageSMS REJECTED INDEX :{message.index} | From Number:{message.number} |Time of msg Receive To modem:{message.Time} | Content: {message.Message}");
                        }
                    }
                    else {
                        Console.WriteLine($"Dropped - 5min  Timeout expired");
                        logger.Log(LOGINFO.SMS_RECEIVED_TIMEOUT_REJECTED, $"MessageSMS TIMEOUT_REJECTED INDEX :{message.index} | From Number:{message.number} |Time of msg Receive To modem:{message.Time} | Content: {message.Message}");
                    }
                }
                else {
                    Console.WriteLine("Message Already read or number format not valid");
                    logger.Log(LOGINFO.SMS_REJECTED, $"MessageSMS REJECTED INDEX :{message.index} | From Number:{message.number} |Time of msg Receive To modem:{message.Time} | Content: {message.Message}");
                }
                await DeleteMessageByIndex(message.index);
                Console.WriteLine($"Resolved, deleting {message.index}");
            }
        }



        public async void onCall(object sender, string line) {
            if (current_state != State.IDLE) return;

            if (line == "RING")
            {
                Console.WriteLine("SOMEONE IS RINGING");
            }
            else if (line.StartsWith("+CLIP:")) {
                Console.WriteLine(line);

                string number = CheckCallFormat(line);

                if (number == string.Empty) {
                    Console.WriteLine("CALL FORMAT NOT ACCEPTED");
                    TerminalStatus response = await HangUpCall();
                    logger.Log(LOGINFO.CALL_REJECTED, $"REJECTING From Number:{number} | Reason: CALL FORMAT NOT ACCEPTED");
                    if (response == TerminalStatus.OK)
                    {
                        return;
                    }
                    else {
                        Console.WriteLine("FAILED TO HANG UP CALL");
                        logger.Log(LOGINFO.WARNING, $"FAILED TO HANG UP CALL From authorized Number:{number}");
                    }


                }
                Console.WriteLine($"Extracted number {number}");
                if (UserManager.CheckNumber(number))
                {
                    string UserMAC = UserManager.GetUserMacAdress(number);

                    if (UserMAC != string.Empty)
                    {
                        WakeOnLan.SendWakePacket(UserMAC);
                        await HangUpCall();

                        await SendSMS(number, "AWAKE");
                        logger.Log(LOGINFO.CALL_ACCEPTED, $"ACCEPTED From Number:{number} | Started PC WITH MAC {UserMAC}");
                        logger.Log(LOGINFO.INFO, $"SENT Confirmation SMS to :{number} | Started PC WITH MAC {UserMAC}");
                        Console.WriteLine($"MAGIC PACKET SENT TO {UserMAC}");
                        return;
                    }
                }

                Console.WriteLine("UNAUTHORIZED NUMBER");
                TerminalStatus response2 = await HangUpCall();
                logger.Log(LOGINFO.CALL_REJECTED, $"Rejecting call From Number:{number} | Reason: NO Authorization");
                if (response2 == TerminalStatus.OK)
                {
                    return;
                }
                else
                {
                    logger.Log(LOGINFO.WARNING, $"FAILED TO HANG UP CALL From Unauthorized Number:{number}");
                    Console.WriteLine("FAILED TO HANG UP CALL");
                }

            }
        }


        private void OnSmsIndexReceived(object? sender, int index) {


            Console.WriteLine("IN SMS STATE");
            Console.WriteLine($"NEW SMS RECEIVED AT INDEX {index}");
            ultimulSmsPrimit = DateTime.Now;
            avemSmsInAsteptare = true;
            logger.Log(LOGINFO.SMS_RECEIVED, $"NEW SMS RECEIVED AT INDEX {index} | Received Time: {ultimulSmsPrimit}");


            /*
            AtResponse response = await client.SendAsync($"AT+CMGR={index}");
            for (int i = 0; i < response.DataLines.Count; i++) {

                Console.WriteLine(response.DataLines[i]);
                
            }
            if ( response.DataLines.Count > 0)
            {

                List<SMSMessage> messages = GetFormatedSMSFromString(response.DataLines,index);
                for (int i = 0; i < messages.Count; i++) { 
                    Console.WriteLine(messages[i].index);
                }
                if (messages != null) { 
                    
                    ResolveUnreadMessages(messages);
                }
            }
            else { 
                Console.WriteLine($"FAILED TO READ SMS AT INDEX {index}");
            }
             */
        }

        public string CheckCallFormat(string line) {
            string[] splitLineForNumber = line.Split('"');
            string[] formatedLine = line.Split(',');
            bool autorize = false;

            if (formatedLine.Length != 6) {
                Console.WriteLine("UNSUPORTED FORMAT FROM CALLER ");

                Console.WriteLine("DROPPING CALL");
                return string.Empty;

            }

            string unformatedNumber = splitLineForNumber[1]; // with country code 
            string type = formatedLine[1];
            string CalledIdentification = formatedLine[5];

            string number = string.Empty;
            string country_code = string.Empty;


            if (CalledIdentification == "0") {
                // add desired country code from settings
                if (type == "129") // Local calling 0040(country code ) + number
                {

                    number = unformatedNumber.Substring(4, unformatedNumber.Length - 4);
                    country_code = unformatedNumber.Substring(0, 4);

                    number = "0" + number;
                    autorize = true;


                }
                else if (type == "145")
                { // international Calling Formata +0040 + number
                    number = unformatedNumber.Substring(5, unformatedNumber.Length - 5);
                    country_code = unformatedNumber.Substring(1, 5);

                    number = "0" + number;
                    autorize = true;
                }
            }
            if (autorize && country_code == ModemConfiguration.CountryFormat)
            {
                return number;
            }

            return string.Empty;

        }


        public async Task<TerminalStatus> Config() {
            TerminalStatus formatted = await setErrorFormating();
            if (formatted != TerminalStatus.OK) {
                return formatted;
            }
      
            TerminalStatus simReady = await CheckIfReady();
            if (simReady != TerminalStatus.OK)
            {
                TerminalStatus pinSet = await SetPin(ModemConfiguration.PinCode);
                if (pinSet != TerminalStatus.OK) {

                    return pinSet;
                }
            }
         
            TerminalStatus callerIdentification = await EnableCallerIdentification();
            if (callerIdentification != TerminalStatus.OK)
            {
                return callerIdentification;
            }
            TerminalStatus SMStoTextFormat = await EnableSMStoTextFormat();
            if (SMStoTextFormat != TerminalStatus.OK)
            {

                return SMStoTextFormat;
            }
            
            TerminalStatus textModeParameters = await ShowTextModeParameters();
            if (textModeParameters != TerminalStatus.OK)
            {
                return textModeParameters;
            }
            TerminalStatus setNetworkRegistrationFormat = await SetNetworkRegistration();
            if (setNetworkRegistrationFormat != TerminalStatus.OK)
            {
                return setNetworkRegistrationFormat;
            }

            TerminalStatus checkNetworkingRegistration = await CheckNetworkRegistration();
            if (checkNetworkingRegistration != TerminalStatus.OK) {

                return checkNetworkingRegistration;
            }

            TerminalStatus antennaSignalQuality = await CheckAntennaSignalQuality();
            if (antennaSignalQuality != TerminalStatus.OK) {
                return antennaSignalQuality;
            }


            //Check  for sms in message box?
            TerminalStatus CheckedMessages = await CheckMessageList();
            if (CheckedMessages != TerminalStatus.OK)
            {

                return CheckedMessages;
            }
            TerminalStatus Deleted = await DeleteMessageHistory();
            if (Deleted != TerminalStatus.OK)
            {
                return Deleted;
            }
            TerminalStatus newSMSIndications = await EnableNewSMSIndications();
            if (newSMSIndications != TerminalStatus.OK)
            {
                return newSMSIndications;
            }


            return TerminalStatus.OK;
        }
        public void CheckCommandStatus(TerminalStatus commandStatus) {

            if (commandStatus == TerminalStatus.OK)
            {
                error_count = 0;
                fatalError_count = 0;

            }
             if (commandStatus == TerminalStatus.TIMEOUT || commandStatus == TerminalStatus.ERROR || commandStatus == TerminalStatus.CMEERROR)
            {

                error_count++;

            }
              if (error_count > 5)
            {
                fatalError_count++;
                current_state = State.FAIL;
            }
              if (fatalError_count > 3 || commandStatus == TerminalStatus.DISCONNECTED)
            {
                current_state = State.FAIL;
            }

        }
        public async Task Run() {

            TerminalStatus commandStatus = TerminalStatus.OK;
            
            while (true) {
                switch (current_state) {
                    case State.BOOTING:
                        if (client != null)
                        {
                            isUpTimer.Stop();
                            client.serialport.ClearSerialBuffer();
                            commandStatus = await PingModem();
                            CheckCommandStatus(commandStatus);
                            if (commandStatus == TerminalStatus.OK)
                            {
                                logger.Log(LOGINFO.BOOTING, $"MODEM BOOTING");
                                current_state = State.CONFIG;
                            }

                        }
                        else {
                            ConnectToModem();
                            await Task.Delay(5000);
                        }
                        break;
                    case State.CONFIG:
                        logger.Log(LOGINFO.CONFIGURING, $"MODEM CONFIGURING"); 
                        commandStatus = await Config();
                        CheckCommandStatus(commandStatus);
                        if (commandStatus == TerminalStatus.OK) {
                            current_state = State.IDLE;
                            logger.Log(LOGINFO.IDLE, $"MODEM CONFIGURED, ENTERING IDLE STATE");
                            isUpTimer.Start();
                        }
                        
                        break;
                    case State.IDLE:
                        if (avemSmsInAsteptare && (DateTime.Now - ultimulSmsPrimit) >= timpDeAsteptare)
                        {
                            current_state = State.SMS;
                        }
                        else
                        {
                        
                            await Task.Delay(1000);
                        }
                        break;
                    case State.SMS:
                        
                         avemSmsInAsteptare = false;
                            
                         await CheckMessageList();
                        
                        current_state = State.IDLE;
                        break;
                    case State.FAIL:
                        if (commandStatus == TerminalStatus.DISCONNECTED)
                        {
                            await client.SendAsync("AT#REBOOT");
                            ConnectToModem();
                            logger.Log(LOGINFO.HARD_RESET, $"MODEM DISCONNECTED, HARD REBOOTING");
                        }
                        else {
                           await client.SendAsync("AT+CFUN=1,1");
                           logger.Log(LOGINFO.SOFT_RESET, $"MODEM NOT RESPONDING, SOFT REBOOTING");
                        }
                        current_state = State.BOOTING;
                        isUpTimer.Stop();
                        await Task.Delay(1000);

                        break;
                }

            }

            //exit while

            await logger.ShutdownAsync();



        }

    }
}
