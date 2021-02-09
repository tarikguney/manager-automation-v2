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
		}

		private void StartAnalysis(StartAnalysisRequest request)
		{
			var firstDayOfSprint = _currentIterationInfoOptions.Value.StartDate.Date == DateTime.Now.Date;
			// There is another function that runs when it is the first day of the sprint!
			if (firstDayOfSprint)
			{
				Context.Stop(Self);
				Sender.Tell(new AnalysisCompleteResponse());
			}

			var lastDayOfSprint = _currentIterationInfoOptions.Value.FinishDate.Date == DateTime.Now.Date;
			var currentIterationWorkItems = _workItemsRetriever.GetWorkItems(IterationTimeFrame.Current);

			// Creating the subordinate actors.
			var estimateWorkItemActor =
				Context.ActorOf(Context.DI().Props<EstimateWorkItemsActor>(), "estimate-work-item-actor");
			var descriptiveTitleActor =
				Context.ActorOf(Context.DI().Props<DescriptiveTitleActor>(), "descriptive-title-actor");
			var activateWorkItemActor =
				Context.ActorOf(Context.DI().Props<ActivateWorkItemActor>(), "activate-work-item-actor");
			var descriptionActor =
				Context.ActorOf(Context.DI().Props<DescriptionActor>(), "description-actor");
			var longCodeCompleteActor =
				Context.ActorOf(Context.DI().Props<LongCodeCompleteActor>(), "long-code-complete-actor");
			var greatWorkActor =
				Context.ActorOf(Context.DI().Props<GreatWorkActor>(), "great-work-actor");
			var stillActiveWorkItemsActor =
				Context.ActorOf(Context.DI().Props<StillActiveWorkItemsActor>(),
					"still-active-work-items-actor");

			// Running the actors.
			var tasks = new List<Task>();

			var estimateWorkItemTask = estimateWorkItemActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(estimateWorkItemTask);

			var descriptiveTitleTask = descriptiveTitleActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(descriptiveTitleTask);

			var activeWorkItemTask = activateWorkItemActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(activeWorkItemTask);

			var descriptionTask = descriptionActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(descriptionTask);

			var longCodeCompleteTask = longCodeCompleteActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(longCodeCompleteTask);

			var greatWorkTask = greatWorkActor
				.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems);
			tasks.Add(greatWorkTask);

			var dummyStillActiveWorkItemsTask = new Task<ActorResponse<IReadOnlyList<string>>>(
				() =>
					new ActorResponse<IReadOnlyList<string>>(new List<string>(), false));
			dummyStillActiveWorkItemsTask.Start();

			var stillActiveWorkItemsTask = lastDayOfSprint
				? stillActiveWorkItemsActor.Ask<ActorResponse<IReadOnlyList<string>>>(currentIterationWorkItems)
				: dummyStillActiveWorkItemsTask;

			tasks.Add(stillActiveWorkItemsTask);

			// Waiting for all the of the actors to finish their work and return a response back.
			Task.WaitAll(tasks.ToArray());

			// Collecting the results from each actor.
			var messages = new List<string>();
			messages.AddRange(estimateWorkItemTask.Result.Content);
			messages.AddRange(descriptiveTitleTask.Result.Content);
			messages.AddRange(greatWorkTask.Result.Content);
			messages.AddRange(activeWorkItemTask.Result.Content);
			messages.AddRange(stillActiveWorkItemsTask.Result.Content);
			messages.AddRange(longCodeCompleteTask.Result.Content);

			// Sending the messages from each actor to the message senders. Using a different message sender if it
			// is the last day of the sprint.
			if (lastDayOfSprint)
			{
				_lastDayOfCurrentIterationMessageSender.SendMessages(messages).Wait();
			}
			else
			{
				_currentIterationMessageSender.SendMessages(messages).Wait();
			}

			Context.Stop(Self);
			// This is required for stopping the program. Check out the Ask<> calls in the function classes.
			Sender.Tell(new AnalysisCompleteResponse());
		}
	}
}
