using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Akka.Actor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.Helpers;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Actors
{
	/// <summary>
	/// At the last of the sprint, this actor identifies the still active work items,
	/// and recommends the engineers ways to move it to the next sprint.
	/// It calculates the due-dates of the work items, and if the work item was activated late in the sprint
	/// and the remaining days were not enough to the finish them, it asks the engineers to move their respective
	/// work items directly to the next sprint.
	/// </summary>
	public class StillActiveWorkItemsActor : ReceiveActor
	{
		private readonly ILogger _logger;
		private readonly AzureDevOpsSettings _azureDevOpsSettings;
		private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
		private readonly IterationInfo _currentIteration;

		public StillActiveWorkItemsActor(IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions,
			IOptions<List<DevOpsChatUserMap>> devOpsChatUserMapsOptions,
			IOptions<IterationInfo> currentIterationOptions, ILogger logger)
		{
			_logger = logger;
			_azureDevOpsSettings = azureDevOpsSettingsOptions.Value;
			_devOpsChatUserMaps = devOpsChatUserMapsOptions.Value;
			_currentIteration = currentIterationOptions.Value;

			Receive<IReadOnlyList<JObject>>(HandleIncomingWorkItems);
		}

		private void HandleIncomingWorkItems(IReadOnlyList<JObject> workItems)
		{
			// Property names that has periods in them won't be parsed by Json.NET as opposed to online JSON Parser tools
			// eg. $.value[?(@.fields['Microsoft.VSTS.Scheduling.StoryPoints'] == null && @.fields['System.AssignedTo'] != null)]
			// Because of that reason, I had to use enumeration below.
			var offendingWorkItems = workItems
				.Where(wi => wi["fields"] is JObject fields &&
				             new List<string> {"Bug", "User Story"}.Contains(fields["System.WorkItemType"]
					             .Value<string>()) &&
				             fields["System.State"].Value<string>().ToLower() == "active" &&
				             fields.ContainsKey("System.AssignedTo")).ToList();

			if (!offendingWorkItems.Any())
			{
				Sender.Tell(new ActorResponse<IReadOnlyList<string>>(null, false));
			}

			var messages = new List<string>();
			var baseUrl =
				$"https://dev.azure.com/{HttpUtility.UrlPathEncode(_azureDevOpsSettings.Organization)}/" +
				$"{HttpUtility.UrlPathEncode(_azureDevOpsSettings.Project)}/_workitems/edit";

			foreach (var offendingWorkItem in offendingWorkItems)
			{
				// Todo Check if there is any user story point assigned. Otherwise, the other reminders will take effect.

				var recommendedActionText = "Make sure the work item is *closed*. " +
				                            $"If you *need more time*, then *create a follow-up* work item, *link it* to the original work, " +
				                            $"*move it* to the appropriate sprint, and *close* the original work item. If you have *not even started working on it*, " +
				                            $"then move it to the appropriate sprint as it is.";


				// Work items might not have story points, and they have to be sized first.
				// If a work item happens to be sized at the end of the sprint, then there is no need to make more assumptions
				// about it, and simply suggest the default recommended message.
				if (offendingWorkItem["fields"] is JObject fieldsJson &&
				    fieldsJson.ContainsKey("Microsoft.VSTS.Scheduling.StoryPoints"))
				{
					var storyPoint = offendingWorkItem["fields"]["Microsoft.VSTS.Scheduling.StoryPoints"]
						.Value<int>();
					var activationDate =
						DateTime.Parse(offendingWorkItem["fields"]["Microsoft.VSTS.Common.ActivatedDate"]
							.Value<string>()).ToLocalTime();

					var dueInDays = IterationHelper.PointsToDays(storyPoint);

					// Local time is assumed for now. Not the best within different time zone cases.
					var assumedActivationDate = activationDate.TimeOfDay >= TimeSpan.FromHours(12)
						? activationDate.Date + TimeSpan.FromDays(1)
						: activationDate.Date;
					var workItemDueDate = assumedActivationDate + dueInDays;
					if (workItemDueDate > _currentIteration.FinishDate.Date)
					{
						recommendedActionText = "Move it to the next iteration.";
					}
				}

				var userDisplayName = offendingWorkItem["fields"]?["System.AssignedTo"]?["displayName"]
					?.Value<string>();
				var userEmail = offendingWorkItem["fields"]?["System.AssignedTo"]?["uniqueName"]?.Value<string>();
				var devOpsGoogleChatUserMap =
					_devOpsChatUserMaps.SingleOrDefault(t =>
						t.AzureDevOpsEmail.Equals(userEmail, StringComparison.InvariantCultureIgnoreCase));
				var workItemTitle = offendingWorkItem["fields"]?["System.Title"]?.Value<string>();
				var workItemId = offendingWorkItem["id"].Value<string>();
				var workItemUrl = $"{baseUrl}/{workItemId}";

				var chatDisplayName = devOpsGoogleChatUserMap == null
					? userDisplayName
					: $"<users/{devOpsGoogleChatUserMap.GoogleChatUserId}>";

				_logger.LogInformation(
					"BOARD: Still in active state. Story \"{workItemId}:{workItemTitle}\". Assigned to {userEmail} in {currentIteration}.",
					workItemId, workItemTitle, userEmail, _currentIteration.Name);

				messages.Add(
					$"{chatDisplayName}, <{workItemUrl}|{workItemTitle}> is *still active*. {recommendedActionText}.");
			}

			Sender.Tell(new ActorResponse<IReadOnlyList<string>>(messages, true));
		}
	}
}
