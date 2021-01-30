using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Akka.Actor;
using Autofac;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.AutoFacModules;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation
{
	public static class RetrospectiveAutomationFunction
	{
		[FunctionName("RetrospectiveAutomation")]
		public static async Task RunAsync([TimerTrigger("0 0 10 * * Mon")] TimerInfo myTimer,
			ILogger log, ExecutionContext context)
		{
			// Sets the settings model defined as static properties in the class.
			var configModule = new ConfigurationModule(context);

			var containerBuilder = new ContainerBuilder();
			containerBuilder.RegisterModule(configModule);
			containerBuilder.RegisterModule<RetrospectiveModule>();
			containerBuilder.RegisterInstance(log).As<ILogger>();
			var container = containerBuilder.Build();

			var actorSystem = ActorSystem.Create("retrospective-automation-actor-system");
			actorSystem.UseAutofac(container);

			var currentIteration = container.Resolve<IOptions<CurrentIterationInfo>>();

			// Running this function every Monday and only send reminders if it is the beginning of the sprint.
			if (currentIteration.Value.StartDate.Date != DateTime.Now.Date)
			{
				return;
			}

			/*var iterationWorkItemsTransformBlock = IterationWorkItemsRetrieverTransform.Block;*/
			/*var googleChatMessageSender = PreviousIterationGoogleChatMessageSenderAction.Block;*/

			/*var estimateWorkItemsTransformBlock = EstimateWorkItemsTransform.Block;
			var descriptiveTitleTransformBlock = DescriptiveTitlesTransform.Block;
			var descriptionTransformBlock = DescriptionTransform.Block;
			var longCodeCompleteTransformBlock = LongCodeCompleteTransform.Block;
			var greatPreviousIteration = GreatPreviousIteration.Block;
			var openWorkItemsTransformBlock = OpenWorkItemsTransform.Block;*/

			var broadcastBlock = new BroadcastBlock<List<JObject>>(null);
			// Increase the limit of the batch size after adding another transform block.
			var batchBlock = new BatchBlock<string>(6);

			/*iterationWorkItemsTransformBlock.LinkTo(broadcastBlock);*/

			/*broadcastBlock.LinkTo(estimateWorkItemsTransformBlock);
			estimateWorkItemsTransformBlock.LinkTo(batchBlock);

			broadcastBlock.LinkTo(descriptiveTitleTransformBlock);
			descriptiveTitleTransformBlock.LinkTo(batchBlock);

			broadcastBlock.LinkTo(descriptionTransformBlock);
			descriptionTransformBlock.LinkTo(batchBlock);

			broadcastBlock.LinkTo(longCodeCompleteTransformBlock);
			longCodeCompleteTransformBlock.LinkTo(batchBlock);

			broadcastBlock.LinkTo(greatPreviousIteration);
			greatPreviousIteration.LinkTo(batchBlock);

			broadcastBlock.LinkTo(openWorkItemsTransformBlock);
			openWorkItemsTransformBlock.LinkTo(batchBlock);*/

			/*batchBlock.LinkTo(googleChatMessageSender);

			iterationWorkItemsTransformBlock.Post(IterationTimeFrame.Previous);
			iterationWorkItemsTransformBlock.Complete();
			await googleChatMessageSender.Completion;*/
		}
	}
}
