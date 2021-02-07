using System;
using Akka.Actor;
using Akka.DI.Core;
using Autofac;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using TarikGuney.ManagerAutomation.AutoFacModules;
using TarikGuney.ManagerAutomation.CommMessages;
using TarikGuney.ManagerAutomation.Managers;

namespace TarikGuney.ManagerAutomation
{
	public static class ManagersReportAutomationFunction
	{
		[FunctionName("ManagersReportAutomation")]
		public static void Run([TimerTrigger("0 30 9 * * 1-5")] TimerInfo myTimer,
			ILogger log, ExecutionContext context)
		{
			// Sets the settings model defined as static properties in the class.
			var configModule = new ConfigurationModule(context);

			var containerBuilder = new ContainerBuilder();
			containerBuilder.RegisterModule(configModule);
			containerBuilder.RegisterModule<ManagersReportModule>();
			containerBuilder.RegisterInstance(log).As<ILogger>();
			var container = containerBuilder.Build();

			var actorSystem = ActorSystem.Create("manager-report-actor-system");
			actorSystem.UseAutofac(container);

			var progressReportManager = actorSystem.ActorOf(actorSystem.DI().Props<ProgressReportManager>(),
				"progress-report-manager");

			var result =
				progressReportManager.Ask<AnalysisCompleteResponse>(new StartAnalysisRequest(),
					TimeSpan.FromMinutes(1));

			result.Wait();
		}
	}
}
