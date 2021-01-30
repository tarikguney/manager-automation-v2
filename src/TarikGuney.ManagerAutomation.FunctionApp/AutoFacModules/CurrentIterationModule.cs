using Autofac;
using TarikGuney.ManagerAutomation.Actors;
using TarikGuney.ManagerAutomation.IterationWorkItemRetrievers;
using TarikGuney.ManagerAutomation.MessageSenders;

namespace TarikGuney.ManagerAutomation.AutoFacModules
{
	public class CurrentIterationModule : Module
	{
		protected override void Load(ContainerBuilder builder)
		{
			builder.RegisterType<IIterationWorkItemsRetriever>()
				.As<AzureDevOpsIterationWorkItemsRetriever>().SingleInstance();
			builder.RegisterType<ICurrentIterationMessageSender>()
				.As<CurrentIterationGoogleChatMessageSender>().SingleInstance();
			builder.RegisterType<ILastDayOfCurrentIterationMessageSender>()
				.As<LastDayOfCurrentIterationGoogleChatMessageSender>()
				.SingleInstance();
			builder.RegisterType<EstimateWorkItemsActor>().AsSelf();
			builder.RegisterType<DescriptiveTitleActor>().AsSelf();
			builder.RegisterType<ActivateWorkItemActor>().AsSelf();
			builder.RegisterType<DescriptionActor>().AsSelf();
			builder.RegisterType<LongCodeCompleteActor>().AsSelf();
			builder.RegisterType<GreatWorkActor>().AsSelf();
			builder.RegisterType<StillActiveWorkItemsActor>().AsSelf();
		}
	}
}
