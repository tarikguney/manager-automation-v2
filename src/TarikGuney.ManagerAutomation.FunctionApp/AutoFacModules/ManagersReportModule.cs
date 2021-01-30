using Autofac;
using TarikGuney.ManagerAutomation.Actors;
using TarikGuney.ManagerAutomation.IterationWorkItemRetrievers;
using TarikGuney.ManagerAutomation.MessageSenders;

namespace TarikGuney.ManagerAutomation.AutoFacModules
{
	public class ManagersReportModule: Module
	{
		protected override void Load(ContainerBuilder builder)
		{
			builder.RegisterType<IIterationWorkItemsRetriever>()
				.As<AzureDevOpsIterationWorkItemsRetriever>().SingleInstance();
			builder.RegisterType<IManagersReportMessageSender>()
				.As<ManagersReportGoogleChatMessageSender>().SingleInstance();
			builder.RegisterType<PassedDueWorkItemsActor>().AsSelf();
		}
	}
}
