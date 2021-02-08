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
	public static class RetrospectiveAutomationFunction
	{
		[FunctionName("RetrospectiveAutomation")]
		public static void Run([TimerTrigger("0 0 10 * * Mon")] TimerInfo myTimer,
			ILogger log, ExecutionContext context)
		{
			// Sets the settings model defined as static properties in the class.
			var configModule = new ConfigurationModule(context);

			var containerBuilder = new ContainerBuilder();
			containerBuilder.RegisterModule(configModule);
			containerBuilder.RegisterModule<RetrospectiveModule>();
			containerBuilder.RegisterInstance(log).As<ILogger>();
			var container = containerBuilder.Build();

			var actorSystem = ActorSystem.Create("retrospective-automation-actor-system");
			actorSystem.UseAutofac(container);
			var retrospectiveManager =
				actorSystem.ActorOf(actorSystem.DI().Props<RetrospectiveManager>(), "retrospective-manager");
			var result = retrospectiveManager.Ask(new StartAnalysisRequest(),
				TimeSpan.FromMinutes(1));
			result.Wait();
		}
	}
}
