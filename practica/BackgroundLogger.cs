using CsvHelper;
using practica.Entity;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

public enum LOGINFO
{
    INFO,
    WARNING,
    ERROR,
    SMS_RECEIVED,  
    SMS_ACCEPTED,
    SMS_REJECTED,
    CALL_ACCEPTED,
    CALL_REJECTED,
    SMS_RECEIVED_TIMEOUT_REJECTED,
    BOOTING,
    IDLE,
    HARD_RESET,
    SOFT_RESET,
    CONFIGURING



}   
public class BackgroundLogger
{
    private readonly string _filePath;

    
    private readonly Channel<LogEvent> _logChannel;

    private string previousMsg;
    private readonly Task _backgroundTask;
    public bool fileExists;

    public BackgroundLogger(string filePath)
    {
        _filePath = filePath;
         _logChannel = Channel.CreateUnbounded<LogEvent>();
        previousMsg = string.Empty;
        _backgroundTask = Task.Run(ProcessLogQueueAsync);
        fileExists = File.Exists(_filePath);

    }

  
    public void Log(LOGINFO level, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        if (message != previousMsg) { 
        
            _logChannel.Writer.TryWrite(new LogEvent(timestamp, level.ToString(), message));
            previousMsg = message;
        }
    }


    private async Task ProcessLogQueueAsync()
    {
        
        using (StreamWriter writer = new StreamWriter(_filePath, append: true))
        {
            writer.AutoFlush = true;
            using (CsvWriter csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture)) { 
                 
                await foreach (LogEvent log in _logChannel.Reader.ReadAllAsync())
                {
                    if (!fileExists) {
                        csvWriter.WriteHeader<LogEvent>();
                        await csvWriter.NextRecordAsync();
                    }
                    csvWriter.WriteRecord(log);
                     await csvWriter.NextRecordAsync();
                }
      
            }
        
        }
    }

  
    public async Task ShutdownAsync()
    {
      
        _logChannel.Writer.Complete();
        await _backgroundTask;
    }
}