using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.CommMessages;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Actors
{
	public class GreatPreviousIterationActor : ReceiveActor
	{
		public const string GreatWorkGreeting = "good job";

		private readonly ILogger _logger;
		private readonly AzureDevOpsSettings _azureDevOpsSettings;
		private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
		private readonly CurrentIterationInfo _currentIteration;

		public GreatPreviousIterationActor(IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions,
			IOptions<List<DevOpsChatUserMap>> devOpsChatUserMapsOptions,
			IOptions<CurrentIterationInfo> currentIterationOptions, ILogger logger)
		{
			_logger = logger;
			_azureDevOpsSettings = azureDevOpsSettingsOptions.Value;
			_devOpsChatUserMaps = devOpsChatUserMapsOptions.Value;
			_currentIteration = currentIterationOptions.Value;

			Receive<IReadOnlyList<JObject>>(HandleIncomingWorkItems);
		}

		private void HandleIncomingWorkItems(IReadOnlyList<JObject> workItems)
		{
			var workItemsByPersons = workItems
				.Where(wi => wi["fields"] is JObject fields &&
				             new List<string>() {"Bug", "User Story"}.Contains(fields["System.WorkItemType"]
					             .Value<string>()) &&
				             fields.ContainsKey("System.AssignedTo")
				).ToLookup(
					wi => wi["fields"]["System.AssignedTo"]["uniqueName"].Value<string>(), t => t);

			if (!workItemsByPersons.Any())
			{
				Sender.Tell(new ActorResponse<IReadOnlyList<string>>(new List<string>(), false));
			}

			var messages = new List<string>();

			foreach (var workItemsByPerson in workItemsByPersons)
			{
				var anyPendingWorkItems = workItemsByPerson.Any(a =>
					!a!["fields"]!["System.State"]!.Value<string>()
						.Equals("Closed", StringComparison.InvariantCultureIgnoreCase));

				if (anyPendingWorkItems)
				{
					continue;
				}

				var userDisplayName = workItemsByPerson.First()["fields"]?["System.CreatedBy"]?["displayName"]
					?.Value<string>();
				var userEmail = workItemsByPerson.Key;
				var devOpsGoogleChatUserMap =
					_devOpsChatUserMaps.SingleOrDefault(t =>
						t.AzureDevOpsEmail.Equals(userEmail, StringComparison.InvariantCultureIgnoreCase));

				var chatDisplayName = devOpsGoogleChatUserMap == null
					? userDisplayName
					: $"<users/{devOpsGoogleChatUserMap.GoogleChatUserId}>";

				_logger.LogInformation(
					"BOARD: Closed everything from the previous sprint by the first day of the current sprint {currentIteration}. Assigned to {userEmail}.",
					_currentIteration.Name, userEmail);

				messages.Add(
					$"{chatDisplayName}, {GreatWorkGreeting}! 👏 You *closed* all of *your previous iteration* work items! 🎉");
			}

			Sender.Tell(new ActorResponse<IReadOnlyList<string>>(messages, true));
		}
	}
}
