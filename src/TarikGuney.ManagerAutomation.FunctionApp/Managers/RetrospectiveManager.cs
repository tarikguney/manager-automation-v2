using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.DI.Core;
using Microsoft.Extensions.Options;
using TarikGuney.ManagerAutomation.Actors;
using TarikGuney.ManagerAutomation.CommMessages;
using TarikGuney.ManagerAutomation.IterationWorkItemRetrievers;
using TarikGuney.ManagerAutomation.MessageSenders;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Managers
{
	public class RetrospectiveManager : ReceiveActor
	{
		private readonly IIterationWorkItemsRetriever _workItemsRetriever;
		private readonly IRetrospectiveMessageSender _retrospectiveMessageSender;
		private readonly IOptions<CurrentIterationInfo> _currentIterationInfoOptions;

		public RetrospectiveManager(IIterationWorkItemsRetriever workItemsRetriever,
			IRetrospectiveMessageSender retrospectiveMessageSender,
			IOptions<CurrentIterationInfo> currentIterationInfoOptions
			)
		{
			_workItemsRetriever = workItemsRetriever;
			_retrospectiveMessageSender = retrospectiveMessageSender;
			_currentIterationInfoOptions = currentIterationInfoOptions;

			Receive<StartAnalysisRequest>(HandleStartAnalysisRequest);
		}

		private void HandleStartAnalysisRequest(StartAnalysisRequest obj)
		{
			// This is supposed to run on Mondays bi-weekly at the starting of the new iteration.
			if (_currentIterationInfoOptions.Value.StartDate.Date != DateTime.Now.Date)
			{
				Context.Stop(Self);
				Sender.Tell(new AnalysisCompleteResponse());
			}

			var currentIterationWorkItems =
				_workItemsRetriever.GetWorkItems(IterationTimeFrame.Previous);

			// Creating the subordinate actors.
			var estimateWorkItemActor =
				Context.ActorOf(Context.DI().Props<EstimateWorkItemsActor>(), "estimate-work-item-actor");
			var descriptiveTitleActor =
				Context.ActorOf(Context.DI().Props<DescriptiveTitleActor>(), "descriptive-title-actor");
			var descriptionActor =
				Context.ActorOf(Context.DI().Props<DescriptionActor>(), "description-actor");
			var longCodeCompleteActor =
				Context.ActorOf(Context.DI().Props<LongCodeCompleteActor>(), "long-code-complete-actor");
			var greatPreviousIterationActor =
				Context.ActorOf(Context.DI().Props<GreatPreviousIterationActor>(), "great-previous-iteration-actor");
			var openWorkItemsActor =
				Context.ActorOf(Context.DI().Props<OpenWorkItemsActor>(), "open-work-items-actor");

			// Running the actors.
			var tasks = new List<Task>();

			var estimateWorkItemTask = estimateWorkItemActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(estimateWorkItemTask);

			var descriptiveTitleTask = descriptiveTitleActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(descriptiveTitleTask);

			var descriptionTask = descriptionActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(descriptionTask);

			var longCodeCompleteTask = longCodeCompleteActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(longCodeCompleteTask);

			var greatPreviousIterationTask = greatPreviousIterationActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(greatPreviousIterationTask);

			var openWorkItemsTask = openWorkItemsActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(openWorkItemsTask);

			// Waiting for all the of the actors to finish their work and return a response back.
			Task.WaitAll(tasks.ToArray());

			// Collecting the results from each actor.
			var messages = new List<string>();
			messages.AddRange(estimateWorkItemTask.Result.Content);
			messages.AddRange(descriptiveTitleTask.Result.Content);
			messages.AddRange(greatPreviousIterationTask.Result.Content);
			messages.AddRange(openWorkItemsTask.Result.Content);
			messages.AddRange(longCodeCompleteTask.Result.Content);

			// Send the messages
			_retrospectiveMessageSender.SendMessages(messages).Wait();

			// Clearing out and exiting.
			Context.Stop(Self);
			Sender.Tell(new AnalysisCompleteResponse());
		}
	}
}
