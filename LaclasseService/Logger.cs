using System;
using System.IO;
using System.Net.Mail;

namespace Laclasse
{
    public enum LogLevel
    {
        Debug,
        Info,
        Error,
        Alert
    }

    public class Logger
    {
        readonly LogSetup logSetup;
        readonly MailSetup mailSetup;
        readonly object requestWriteLock = new object();
        readonly object errorWriteLock = new object();

        public Logger(LogSetup logSetup, MailSetup mailSetup)
        {
            this.logSetup = logSetup;
            this.mailSetup = mailSetup;
        }

        public void LogRequest(string message)
        {
            if (logSetup.requestLogFile != null)
                lock (requestWriteLock)
                    using (var writer = File.AppendText(logSetup.requestLogFile))
                        writer.WriteLine($"{DateTime.Now.ToString("s")} {message}");
            else
                Console.WriteLine(message);
        }

        public void Log(LogLevel level, string message)
        {
            // send email alert
            if (logSetup.alertEmail != null && level == LogLevel.Alert)
            {
                using (var smtpClient = new SmtpClient(mailSetup.server.host, mailSetup.server.port))
                {
                    var mailMessage = new MailMessage(mailSetup.from, logSetup.alertEmail, "[Laclasse-alert]", message);
                    smtpClient.Send(mailMessage);
                }
            }
            if (logSetup.errorLogFile != null)
                lock (errorWriteLock)
                    using (var writer = File.AppendText(logSetup.errorLogFile))
                        writer.WriteLine($"{DateTime.Now.ToString("s")} [{level.ToString()}] " + message.Replace("\n", "\\n"));
            else
                Console.WriteLine(message);
        }
    }
}
