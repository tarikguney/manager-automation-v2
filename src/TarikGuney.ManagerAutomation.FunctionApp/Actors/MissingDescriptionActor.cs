using System.Collections.Generic;
using Akka.Actor;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Actors
{
	public class MissingDescriptionActor: ReceiveActor
	{
		private readonly AzureDevOpsSettings _azureDevOpsSettings;
		private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
		private readonly IterationInfo _currentIteration;

		public MissingDescriptionActor(IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions,
			IOptions<List<DevOpsChatUserMap>> devOpsChatUserMapsOptions,
			IOptions<IterationInfo> currentIterationOptions)
		{
			_azureDevOpsSettings = azureDevOpsSettingsOptions.Value;
			_devOpsChatUserMaps = devOpsChatUserMapsOptions.Value;
			_currentIteration = currentIterationOptions.Value;
			
			Receive<List<JObject>>(HandleIncomingWorkItems);
		}

		private void HandleIncomingWorkItems(List<JObject> obj)
		{
			throw new System.NotImplementedException();
		}
	}
}
