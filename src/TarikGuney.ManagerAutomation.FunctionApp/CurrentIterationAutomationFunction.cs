using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.DataFlow;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation
{
    public static class CurrentIterationAutomationFunction
    {
        // The function will be executed on Monday through Friday at every 9:30 AM and 3:30 PM
        [FunctionName("CurrentIterationAutomation")]
        public static async Task RunAsync([TimerTrigger("0 30 9,15 * * 1-5")] TimerInfo myTimer,
            ILogger log, ExecutionContext context)
        {
            // Sets the settings model defined as static properties in the class.
            await Config.SetSharedSettings(context);
            Logger.CurrentLogger = log;
            // No need to send anything on the first day of the sprint since it is the planning day,
            // and people most likely won't have much time to keep their work items current.
            if (Config.CurrentIteration.StartDate.Date == DateTime.Now.Date)
            {
                return;
            }

            // If in the last day of the sprint and the time of the day has past after noon
            var lastDayOfIteration = Config.CurrentIteration.FinishDate.Date == DateTime.Now.Date &&
                                     DateTime.Now.TimeOfDay > TimeSpan.FromHours(12);

            var iterationWorkItemsTransformBlock = IterationWorkItemsRetrieverTransform.Block;
            var regularDaysMessageSenderActionBlock = CurrentIterationGoogleChatMessageSenderAction.Block;
            var lastDayMessageSenderActionBlock =
                LastDayOfCurrentIterationGoogleChatMessageSenderAction.Block;

            var estimateWorkItemsTransformBlock = EstimateWorkItemsTransform.Block;
            var descriptiveTitleTransformBlock = DescriptiveTitlesTransform.Block;
            var activateWorkItemTransformBlock = ActivateWorkItemTransform.Block;
            var descriptionTransformBlock = DescriptionTransform.Block;
            var longCodeCompleteTransformBlock = LongCodeCompleteTransform.Block;
            var greatWorkTransformBlock = GreatWorkTransform.Block;
            var stillActiveWorkItemsTransformBlock = StillActiveWorkItemsTransform.Block;

            var broadcastBlock = new BroadcastBlock<List<JObject>>(null);
            // Increase the limit of the batch size after adding another transform block.
            var batchBlock = new BatchBlock<string>(lastDayOfIteration ? 7 : 6);

            // On the last day of the iteration, send a different message indicating the end of the sprint.
            if (lastDayOfIteration)
            {
                broadcastBlock.LinkTo(stillActiveWorkItemsTransformBlock);
                // Adding one more to the batch block, increasing the batch size by one.
                stillActiveWorkItemsTransformBlock.LinkTo(batchBlock);
                batchBlock.LinkTo(lastDayMessageSenderActionBlock);
            }
            else
            {
                batchBlock.LinkTo(regularDaysMessageSenderActionBlock);
            }

            iterationWorkItemsTransformBlock.LinkTo(broadcastBlock);

            broadcastBlock.LinkTo(estimateWorkItemsTransformBlock);
            estimateWorkItemsTransformBlock.LinkTo(batchBlock);

            broadcastBlock.LinkTo(descriptiveTitleTransformBlock);
            descriptiveTitleTransformBlock.LinkTo(batchBlock);

            broadcastBlock.LinkTo(activateWorkItemTransformBlock);
            activateWorkItemTransformBlock.LinkTo(batchBlock);

            broadcastBlock.LinkTo(descriptionTransformBlock);
            descriptionTransformBlock.LinkTo(batchBlock);

            broadcastBlock.LinkTo(longCodeCompleteTransformBlock);
            longCodeCompleteTransformBlock.LinkTo(batchBlock);

            broadcastBlock.LinkTo(greatWorkTransformBlock);
            greatWorkTransformBlock.LinkTo(batchBlock);

            iterationWorkItemsTransformBlock.Post(IterationTimeFrame.Current);
            iterationWorkItemsTransformBlock.Complete();
            await regularDaysMessageSenderActionBlock.Completion;
        }
    }
}
