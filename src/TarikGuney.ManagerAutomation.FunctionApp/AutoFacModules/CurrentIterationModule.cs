using Autofac;
using TarikGuney.ManagerAutomation.Actors;
using TarikGuney.ManagerAutomation.IterationWorkItemRetrievers;
using TarikGuney.ManagerAutomation.Managers;
using TarikGuney.ManagerAutomation.MessageSenders;

namespace TarikGuney.ManagerAutomation.AutoFacModules
{
	public class CurrentIterationModule : Module
	{
		protected override void Load(ContainerBuilder builder)
		{
			builder.RegisterType<AzureDevOpsIterationWorkItemsRetriever>()
				.As<IIterationWorkItemsRetriever>().SingleInstance();
			builder.RegisterType<CurrentIterationGoogleChatMessageSender>()
				.As<ICurrentIterationMessageSender>().SingleInstance();
			builder.RegisterType<LastDayOfCurrentIterationGoogleChatMessageSender>()
				.As<ILastDayOfCurrentIterationMessageSender>()
				.SingleInstance();
			builder.RegisterType<EstimateWorkItemsActor>().AsSelf();
			builder.RegisterType<DescriptiveTitleActor>().AsSelf();
			builder.RegisterType<ActivateWorkItemActor>().AsSelf();
			builder.RegisterType<DescriptionActor>().AsSelf();
			builder.RegisterType<LongCodeCompleteActor>().AsSelf();
			builder.RegisterType<GreatWorkActor>().AsSelf();
			builder.RegisterType<StillActiveWorkItemsActor>().AsSelf();
			builder.RegisterType<PendingPullRequestsActor>().AsSelf();
			builder.RegisterType<AzureDevOpsAllPullRequestsRetriever>().As<IPullRequestsRetriever>();
			builder.RegisterType<CurrentIterationManager>().AsSelf();
		}
	}
}
