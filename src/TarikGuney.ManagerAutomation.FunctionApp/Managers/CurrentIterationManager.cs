using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.DI.Core;
using Microsoft.Extensions.Options;
using TarikGuney.ManagerAutomation.Actors;
using TarikGuney.ManagerAutomation.IterationWorkItemRetrievers;
using TarikGuney.ManagerAutomation.MessageSenders;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Managers
{
	public class CurrentIterationManager : ReceiveActor
	{
		private readonly ICurrentIterationMessageSender _currentIterationMessageSender;
		private readonly IIterationWorkItemsRetriever _workItemsRetriever;
		private readonly ILastDayOfCurrentIterationMessageSender _lastDayOfCurrentIterationMessageSender;
		private readonly IOptions<CurrentIterationInfo> _currentIterationInfoOptions;

		public CurrentIterationManager(ICurrentIterationMessageSender currentIterationMessageSender,
			IIterationWorkItemsRetriever workItemsRetriever,
			ILastDayOfCurrentIterationMessageSender lastDayOfCurrentIterationMessageSender,
			IOptions<CurrentIterationInfo> currentIterationInfoOptions
		)
		{
			_currentIterationMessageSender = currentIterationMessageSender;
			_workItemsRetriever = workItemsRetriever;
			_lastDayOfCurrentIterationMessageSender = lastDayOfCurrentIterationMessageSender;
			_currentIterationInfoOptions = currentIterationInfoOptions;

			Receive<StartAnalysisRequest>(StartAnalysis);
			Receive<ActorResponse<IReadOnlyList<string>>>(HandleSubordinatesResponses);
		}

		private void HandleSubordinatesResponses(ActorResponse<IReadOnlyList<string>> response)
		{
			/* todo Handle the subordinate response
				1. Count the number of responses received
				2. Check if today's is the lsat day of the sprint, then count the number differently
				3. If everything is received, then
					a. Either form another message and send it to self.
					b. Start sending the messages here directly
				4. Choose the last day or regular days message sender
				5. Push the messages to the message sender.
				6. Stop self, which will terminate the child actors too.
				Question: Will the actors run to the completion or do I need to wait for the response
					in the program.cs file?
			 */
		}

		private void StartAnalysis(StartAnalysisRequest request)
		{
			if (_currentIterationInfoOptions.Value.StartDate.Date == DateTime.Now.Date)
			{
				Context.Stop(Self);
				return;
			}

			var currentIterationWorkItems = _workItemsRetriever.GetWorkItems(IterationTimeFrame.Current);

			var estimateWorkItemActor =
				Context.ActorOf(Context.DI().Props<EstimateWorkItemsActor>(), "estimate-work-item-actor");
			var descriptiveTitleActor =
				Context.ActorOf(Context.DI().Props<DescriptiveTitleActor>(), "descriptive-title-actor");
			var activateWorkItemActor =
				Context.ActorOf(Context.DI().Props<ActivateWorkItemActor>(), "activate-work-item-actor");
			var descriptionActor = Context.ActorOf(Context.DI().Props<DescriptionActor>(), "description-actor");
			var longCodeComplete = Context.ActorOf(Context.DI().Props<LongCodeCompleteActor>(), "long-code-complete-actor");
			var greatWorkActor = Context.ActorOf(Context.DI().Props<GreatWorkActor>(), "great-work-actor");

			var stillActiveWorkItemsActor = Context.ActorOf(Context.DI().Props<StillActiveWorkItemsActor>(),
				"still-active-work-items-actor");

			estimateWorkItemActor.Tell(currentIterationWorkItems);
			descriptiveTitleActor.Tell(currentIterationWorkItems);
			activateWorkItemActor.Tell(currentIterationWorkItems);
			descriptionActor.Tell(currentIterationWorkItems);
			longCodeComplete.Tell(currentIterationWorkItems);
			greatWorkActor.Tell(currentIterationWorkItems);

			if (_currentIterationInfoOptions.Value.FinishDate.Date == DateTime.Now.Date)
			{
				stillActiveWorkItemsActor.Tell(currentIterationWorkItems);
			}
		}
	}

	public class StartAnalysisRequest
	{
	}
}
