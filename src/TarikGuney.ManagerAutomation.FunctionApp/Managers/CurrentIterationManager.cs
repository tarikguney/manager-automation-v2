using System;
using System.ComponentModel.Design;
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
			var activeWorkItemActor =
				Context.ActorOf(Context.DI().Props<ActivateWorkItemActor>(), "activate-work-item-actor");
			var descriptionActor = Context.ActorOf(Context.DI().Props<DescriptionActor>(), "description-actor");
			var longCodeComplete = Context.ActorOf(Context.DI().Props<LongCodeCompleteActor>(), "long-code-complete-actor");
			var greatWorkActor = Context.ActorOf(Context.DI().Props<GreatWorkActor>(), "great-work-actor");
			var stillActiveWorkItemsActor = Context.ActorOf(Context.DI().Props<StillActiveWorkItemsActor>(),
				"still-active-work-items-actor");

			estimateWorkItemActor.Tell(currentIterationWorkItems);
			descriptiveTitleActor.Tell(currentIterationWorkItems);

		}
	}

	public class StartAnalysisRequest
	{
	}
}
