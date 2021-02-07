using Autofac;
using TarikGuney.ManagerAutomation.Actors;
using TarikGuney.ManagerAutomation.IterationWorkItemRetrievers;
using TarikGuney.ManagerAutomation.MessageSenders;

namespace TarikGuney.ManagerAutomation.AutoFacModules
{
	public class RetrospectiveModule: Module
	{
		protected override void Load(ContainerBuilder builder)
		{
			builder.RegisterType<IRetrospectiveMessageSender>()
				.As<RetrospectiveGoogleChatMessageSender>();

			builder.RegisterType<IIterationWorkItemsRetriever>()
				.As<AzureDevOpsIterationWorkItemsRetriever>().SingleInstance();
			builder.RegisterType<ICurrentIterationMessageSender>()
				.As<CurrentIterationGoogleChatMessageSender>().SingleInstance();
			builder.RegisterType<EstimateWorkItemsActor>().AsSelf();
			builder.RegisterType<DescriptiveTitleActor>().AsSelf();
			builder.RegisterType<DescriptionActor>().AsSelf();
			builder.RegisterType<LongCodeCompleteActor>().AsSelf();
			builder.RegisterType<OpenWorkItemsActor>().AsSelf();
			builder.RegisterType<GreatPreviousIterationActor>().AsSelf();
		}
	}
}
