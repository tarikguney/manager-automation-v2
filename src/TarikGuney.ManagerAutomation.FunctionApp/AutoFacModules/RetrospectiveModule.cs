using Autofac;
using TarikGuney.ManagerAutomation.Actors;
using TarikGuney.ManagerAutomation.IterationWorkItemRetrievers;
using TarikGuney.ManagerAutomation.Managers;
using TarikGuney.ManagerAutomation.MessageSenders;

namespace TarikGuney.ManagerAutomation.AutoFacModules
{
	public class RetrospectiveModule: Module
	{
		protected override void Load(ContainerBuilder builder)
		{
			builder.RegisterType<RetrospectiveGoogleChatMessageSender>()
				.As<IRetrospectiveMessageSender>().SingleInstance();

			builder.RegisterType<AzureDevOpsIterationWorkItemsRetriever>()
				.As<IIterationWorkItemsRetriever>().SingleInstance();

			builder.RegisterType<CurrentIterationGoogleChatMessageSender>()
				.As<ICurrentIterationMessageSender>().SingleInstance();

			builder.RegisterType<EstimateWorkItemsActor>().AsSelf();
			builder.RegisterType<DescriptiveTitleActor>().AsSelf();
			builder.RegisterType<DescriptionActor>().AsSelf();
			builder.RegisterType<LongCodeCompleteActor>().AsSelf();
			builder.RegisterType<OpenWorkItemsActor>().AsSelf();
			builder.RegisterType<GreatPreviousIterationActor>().AsSelf();
			builder.RegisterType<RetrospectiveManager>().AsSelf();
		}
	}
}
