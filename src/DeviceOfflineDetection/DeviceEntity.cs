using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;

namespace DeviceOfflineDetection
{
    public class DeviceEntity
    {
        public string Id { get; set; }
        public DateTime? LastCommunicationDateTime { get; set; }

        private TimeSpan offlineAfter = TimeSpan.FromSeconds(20);
        private readonly ILogger logger;
        private readonly CloudQueue timeoutQueue;
        private readonly IAsyncCollector<SignalRMessage> signalRMessages;

        public DeviceEntity(string id, ILogger logger, CloudQueue timeoutQueue, IAsyncCollector<SignalRMessage> signalRMessages)
        {
            this.Id = id;
            this.logger = logger;
            this.timeoutQueue = timeoutQueue;
            this.signalRMessages = signalRMessages;
        }

        public async Task MessageReceived()
        {
            LastCommunicationDateTime = DateTime.UtcNow;

            var message = new CloudQueueMessage(this.Id);
            await timeoutQueue.AddMessageAsync(message, null, this.offlineAfter, null, null);

            await ReportState("online");
        }

        private async Task ReportState(string state)
        {
            await this.signalRMessages.AddAsync(new SignalRMessage
            {
                Target = "statusChanged",
                Arguments = new[] { new { deviceId = Id, status = state } }
            });
        }

        public async Task DeviceTimeout()
        {
            if (DateTime.UtcNow - LastCommunicationDateTime > offlineAfter)
            {
                await ReportState("offline");
            }
        }

        [FunctionName(nameof(DeviceEntity))]
        public static async Task HandleEntityOperation(
            [EntityTrigger] IDurableEntityContext context,
            [SignalR(HubName = "devicestatus")] IAsyncCollector<SignalRMessage> signalRMessages,
            [Queue("timeoutQueue", Connection = "AzureWebJobsStorage")] CloudQueue timeoutQueue,
            ILogger logger)
        {
            await context.DispatchAsync<DeviceEntity>(context.EntityKey, logger, timeoutQueue, signalRMessages);
        }
    }
}