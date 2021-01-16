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
	/// <summary>
	/// During the current sprint/iteration, this actors finds the engineers who has not activated
	/// any of their assigned work items.
	/// </summary>
	public class EngineersWithoutActivatedWorkItemsFinderActor : ReceiveActor
	{
		private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
		private readonly IterationInfo _currentIteration;
		private readonly ILogger _logger;

		public EngineersWithoutActivatedWorkItemsFinderActor(IOptions<List<DevOpsChatUserMap>> devOpsChatUserMapsOptions,
			IOptions<IterationInfo> currentIterationOptions, ILogger logger)
		{
			_devOpsChatUserMaps = devOpsChatUserMapsOptions.Value;
			_logger = logger;
			_currentIteration = currentIterationOptions.Value;
			Receive<IReadOnlyList<JObject>>(HandleIncomingWorkItems);
		}

		private void HandleIncomingWorkItems(IReadOnlyList<JObject> workItems)
		{
			var offendingWorkItems = workItems
				.Where(wi => wi["fields"] is JObject fields &&
				             new List<string>() {"Bug", "User Story"}.Contains(fields["System.WorkItemType"]
					             .Value<string>()) &&
				             fields.ContainsKey("System.AssignedTo")).ToLookup(
					wi => wi["fields"]["System.AssignedTo"]["uniqueName"].Value<string>(), t => t);

			if (!offendingWorkItems.Any())
			{
				Sender.Tell(new ActorResponse<IReadOnlyList<string>>(null, false));
			}

			var messages = new List<string>();

			foreach (var offendingWorkItem in offendingWorkItems)
			{
				// Check if there is any active work item.
				if (offendingWorkItem.Any(a =>
					a["fields"]["System.State"].Value<string>()
						.Equals("Active", StringComparison.InvariantCultureIgnoreCase)))
				{
					continue;
				}

				// Check if there is any new item to activate in the first place.
				// Don't need to send any notification when all of the work items are complete or in resolve state.
				if (!offendingWorkItem.Any(a =>
					a["fields"]["System.State"].Value<string>()
						.Equals("New", StringComparison.InvariantCultureIgnoreCase)))
				{
					continue;
				}

				var userDisplayName = offendingWorkItem.First()["fields"]?["System.CreatedBy"]?["displayName"]
					?.Value<string>();
				var userEmail = offendingWorkItem.Key;
				var devOpsGoogleChatUserMap =
					_devOpsChatUserMaps.SingleOrDefault(t =>
						t.AzureDevOpsEmail.Equals(userEmail, StringComparison.InvariantCultureIgnoreCase));

				var chatDisplayName = devOpsGoogleChatUserMap == null
					? userDisplayName
					: $"<users/{devOpsGoogleChatUserMap.GoogleChatUserId}>";

				_logger.LogInformation(
					"BOARD: Not activated any work item by {userEmail} in {currentIteration}.",
					userEmail, _currentIteration.Name);

				messages.Add(
					$"{chatDisplayName}, *activate the work item* you are working on.");
			}

			Sender.Tell(new ActorResponse<IReadOnlyList<string>>(messages, true));
		}
	}
}
