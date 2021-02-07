using System;
using System.Threading.Tasks;
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
	public static class CurrentIterationAutomationFunction
	{
		// The function will be executed on Monday through Friday at every 9:30 AM and 3:30 PM
		[FunctionName("CurrentIterationAutomation")]
		public static async Task RunAsync([TimerTrigger("0 30 9,15 * * 1-5")] TimerInfo myTimer,
			ILogger log, ExecutionContext context)
		{
			// Sets the settings model defined as static properties in the class.
			// No need to send anything on the first day of the sprint since it is the planning day,
			// and people most likely won't have much time to keep their work items current.
			var configModule = new ConfigurationModule(context);

			var builder = new ContainerBuilder();
			builder.RegisterModule(configModule);
			builder.RegisterModule<CurrentIterationModule>();
			builder.RegisterInstance(log).As<ILogger>();
			var container = builder.Build();
			var actorSystem = ActorSystem.Create("current-iteration-system");
			actorSystem.UseAutofac(container);
			var currentIterationManager = actorSystem.ActorOf(actorSystem.DI().Props<CurrentIterationManager>(),
				"current-iteration-manager");

			var result =
				currentIterationManager.Ask<AnalysisCompleteResponse>(new StartAnalysisRequest(),
					TimeSpan.FromMinutes(1));
			result.Wait();
		}
	}
}
