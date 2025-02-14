using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;

namespace DeviceOfflineDetection
{
    public static class DeviceOfflineDetectionFunctions
    { 
        [FunctionName(nameof(QueueTrigger))]
        public static async Task QueueTrigger(
            [QueueTrigger("device-messages", Connection = "AzureWebJobsStorage")] CloudQueueMessage message,
            [DurableClient] IDurableEntityClient durableEntityClient,
            ILogger log)
        {
            log.LogInformation($"Receiving message for device {message.AsString}");

            var entity = new EntityId(nameof(DeviceEntity), message.AsString);
            await durableEntityClient.SignalEntityAsync(entity, nameof(DeviceEntity.MessageReceived));
        }

        [FunctionName(nameof(HandleOfflineMessage))]
        public static async Task HandleOfflineMessage(
            [DurableClient] IDurableEntityClient durableEntityClient,
            [QueueTrigger("timeoutQueue", Connection = "AzureWebJobsStorage")]CloudQueueMessage message, 
            ILogger log
            )
        {
            var deviceId = message.AsString;

            var entity = new EntityId(nameof(DeviceEntity), deviceId);
            await durableEntityClient.SignalEntityAsync(entity, nameof(DeviceEntity.DeviceTimeout));
            
            log.LogInformation($"Device ${deviceId} if now offline");
            log.LogMetric("offline", 1);
        }

        [FunctionName(nameof(GetStatus))]
        public static async Task<IActionResult> GetStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpTriggerArgs args,
            [DurableClient] IDurableEntityClient durableEntityClient)
        {
            var entity = new EntityId(nameof(DeviceEntity), args.DeviceId);
            var device = await durableEntityClient.ReadEntityStateAsync<DeviceEntity>(entity);

            return new OkObjectResult(device);
        }
    }
}