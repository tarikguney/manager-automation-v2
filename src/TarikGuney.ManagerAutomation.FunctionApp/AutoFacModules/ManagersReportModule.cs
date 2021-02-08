using Autofac;
using TarikGuney.ManagerAutomation.Actors;
using TarikGuney.ManagerAutomation.IterationWorkItemRetrievers;
using TarikGuney.ManagerAutomation.Managers;
using TarikGuney.ManagerAutomation.MessageSenders;

namespace TarikGuney.ManagerAutomation.AutoFacModules
{
	public class ManagersReportModule: Module
	{
		protected override void Load(ContainerBuilder builder)
		{
			builder.RegisterType<AzureDevOpsIterationWorkItemsRetriever>()
				.As<IIterationWorkItemsRetriever>().SingleInstance();

			builder.RegisterType<ManagersReportGoogleChatMessageSender>()
				.As<IManagersReportMessageSender>().SingleInstance();

			builder.RegisterType<PassedDueWorkItemsActor>().AsSelf();

			builder.RegisterType<ProgressReportManager>().AsSelf();
		}
	}
}
