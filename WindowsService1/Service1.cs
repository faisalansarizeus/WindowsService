using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;

namespace WindowsService1
{
    public partial class Service1 : ServiceBase
    {
        static string ServiceBusConnectionString = ConfigurationManager.AppSettings["ServiceBusConnectionString"].ToString();
        static string QueueName = ConfigurationManager.AppSettings["QueueName"].ToString();
        static IQueueClient queueClient;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        protected override void OnStop()
        {
            queueClient.CloseAsync().GetAwaiter().GetResult();
        }


        public void onDebug()
        {
            OnStart(null);
        }

        static async Task MainAsync()
        {
            queueClient = new QueueClient(ServiceBusConnectionString, QueueName);

            if(ConfigurationManager.AppSettings["LogEnable"].ToString() == "True")
            { 
                WriteToFile("======================================================");
                WriteToFile("Press ENTER key to exit after receiving all the messages.");
                WriteToFile("======================================================");
            }

            // Register QueueClient's MessageHandler and receive messages in a loop
            RegisterOnMessageHandlerAndReceiveMessages();

            //Console.ReadKey();


        }

        static void RegisterOnMessageHandlerAndReceiveMessages()
        {
            // Configure the MessageHandler Options in terms of exception handling, number of concurrent messages to deliver etc.
            var messageHandlerOptions = new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                // Maximum number of Concurrent calls to the callback `ProcessMessagesAsync`, set to 1 for simplicity.
                // Set it according to how many messages the application wants to process in parallel.
                MaxConcurrentCalls = 1,

                // Indicates whether MessagePump should automatically complete the messages after returning from User Callback.
                // False below indicates the Complete will be handled by the User Callback as in `ProcessMessagesAsync` below.
                AutoComplete = false
            };

            // Register the function that will process messages
            queueClient.RegisterMessageHandler(ProcessMessagesAsync, messageHandlerOptions);
        }

        static async Task ProcessMessagesAsync(Message message, CancellationToken token)
        {
            // Process the message
            if (ConfigurationManager.AppSettings["LogEnable"].ToString() == "True")
            {
                WriteToFile($"Received message: SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{Encoding.UTF8.GetString(message.Body)}");
            }

            using (DLSyncService.DatabaseSyncServiceSoapClient client = new DLSyncService.DatabaseSyncServiceSoapClient())
            {
                var result = client.ReceiveMessage(Encoding.UTF8.GetString(message.Body), message.SystemProperties.SequenceNumber, message.Label);
                WriteToFile($"Received Query: {result}");
            }

            // Complete the message so that it is not received again.
            // This can be done only if the queueClient is created in ReceiveMode.PeekLock mode (which is default).
            await queueClient.CompleteAsync(message.SystemProperties.LockToken);

            // Note: Use the cancellationToken passed as necessary to determine if the queueClient has already been closed.
            // If queueClient has already been Closed, you may chose to not call CompleteAsync() or AbandonAsync() etc. calls 
            // to avoid unnecessary exceptions.
        }


        static Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            if (ConfigurationManager.AppSettings["LogEnable"].ToString() == "True")
            {
                WriteToFile($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");
            }
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
            if (ConfigurationManager.AppSettings["LogEnable"].ToString() == "True")
            {
                WriteToFile("Exception context for troubleshooting:");
                WriteToFile($"- Endpoint: {context.Endpoint}");
                WriteToFile($"- Entity Path: {context.EntityPath}");
                WriteToFile($"- Executing Action: {context.Action}");
            }
            return Task.CompletedTask;
        }


        static void WriteToFile(string Message)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
            {
                // Create a file to write to.   
                using (StreamWriter sw = File.CreateText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
        }
    }
}
