using System.Collections.Generic;
using Akka.Actor;
using Akka.DI.Core;
using TarikGuney.ManagerAutomation.Actors;
using TarikGuney.ManagerAutomation.CommMessages;
using TarikGuney.ManagerAutomation.IterationWorkItemRetrievers;
using TarikGuney.ManagerAutomation.MessageSenders;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Managers
{
	public class ProgressReportManager: ReceiveActor
	{
		private readonly IIterationWorkItemsRetriever _iterationWorkItemsRetriever;
		private readonly IManagersReportMessageSender _managersReportMessageSender;

		public ProgressReportManager(IIterationWorkItemsRetriever iterationWorkItemsRetriever,
			IManagersReportMessageSender managersReportMessageSender
		)
		{
			_iterationWorkItemsRetriever = iterationWorkItemsRetriever;
			_managersReportMessageSender = managersReportMessageSender;

			Receive<StartAnalysisRequest>(StartAnalysis);
		}

		private void StartAnalysis(StartAnalysisRequest request)
		{
			var passedDueWorkItemsActor = Context.ActorOf(Context.DI().Props<PassedDueWorkItemsActor>(),
				"passed-due-work-items-actor");

			var passedDueWorkItemsTask =
				passedDueWorkItemsActor.Ask<ActorResponse<IReadOnlyList<string>>>(
					_iterationWorkItemsRetriever.GetWorkItems(IterationTimeFrame.Current));

			var result = passedDueWorkItemsTask.Result;

			_managersReportMessageSender.SendMessages(result.Content).Wait();

			Context.Stop(Self);
			Sender.Tell(new AnalysisCompleteResponse());
		}
	}
}
