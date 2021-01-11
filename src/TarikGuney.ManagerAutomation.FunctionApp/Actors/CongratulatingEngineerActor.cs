using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Actors
{
	public class CongratulatingEngineerActor : ReceiveActor
	{
		private readonly EngineeringManagerInfo _engineeringManagerInfo;
		private readonly ILogger _logger;
		private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
		private readonly IterationInfo _currentIteration;

		public CongratulatingEngineerActor(
			IOptions<List<DevOpsChatUserMap>> devOpsChatUserMapsOptions,
			IOptions<EngineeringManagerInfo> engineeringManagerInfoOptions,
			IOptions<IterationInfo> currentIterationOptions, ILogger logger)
		{
			_engineeringManagerInfo = engineeringManagerInfoOptions.Value;
			_logger = logger;
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
				             fields.ContainsKey("System.AssignedTo") &&
				             // Excluding the engineering manager from the congratulation list to prevent self-compliments.
				             !fields!["System.AssignedTo"]!["uniqueName"]!.Value<string>().Equals(
					             _engineeringManagerInfo.AzureDevOpsEmail,
					             StringComparison.InvariantCultureIgnoreCase)
				).ToLookup(
					wi => wi["fields"]["System.AssignedTo"]["uniqueName"].Value<string>(), t => t);

			if (!workItemsByPersons.Any())
			{
				Sender.Tell(new ActorResponse<IReadOnlyList<string>>(null, false));
			}

			var messages = new List<string>();

			foreach (var workItemsByPerson in workItemsByPersons)
			{
				// Check if there is any active work item.
				if (!workItemsByPerson.All(a =>
					a!["fields"]!["System.State"]!.Value<string>()
						.Equals("Closed", StringComparison.InvariantCultureIgnoreCase)))
				{
					continue;
				}

				// To prevent congratulating people multiple times for the same reasons, we are checking
				// if they completed all of their work items recent enough, which is about two days ago to be safe.
				if (!workItemsByPerson.Any(a =>
				{
					var closedDate =
						DateTime.Parse(a["fields"]["Microsoft.VSTS.Common.ClosedDate"].Value<string>()).ToLocalTime()
							.Date;
					return closedDate > DateTime.Now.Date.Subtract(TimeSpan.FromDays(2));
				}))
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
					"BOARD: Closed everything in the current sprint {currentIteration}. Assigned to {userEmail}.",
					_currentIteration.Name, userEmail);

				messages.Add(
					$"{chatDisplayName}, great work 👏👏👏! You *closed* all of your work items! 🎉");
			}

			Sender.Tell(new ActorResponse<IReadOnlyList<string>>(messages, false));
		}
	}
}
