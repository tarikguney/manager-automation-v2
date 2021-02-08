using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Akka.Actor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.CommMessages;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Actors
{
	/// <summary>
	/// On the first of a new sprint, this actor finds the open work items from the last sprint
	/// and offers suggestions to the owner of the work item on how it should be dealt with.
	/// </summary>
	public class OpenWorkItemsActor : ReceiveActor
	{
		private readonly ILogger _logger;
		private readonly AzureDevOpsSettings _azureDevOpsSettings;
		private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
		private readonly CurrentIterationInfo _currentIteration;

		public OpenWorkItemsActor(IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions,
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
			// Property names that has periods in them won't be parsed by Json.NET as opposed to online JSON Parser tools
			// eg. $.value[?(@.fields['Microsoft.VSTS.Scheduling.StoryPoints'] == null && @.fields['System.AssignedTo'] != null)]
			// Because of that reason, I had to use enumeration below.
			var offendingWorkItems = workItems
				.Where(wi => wi["fields"] is JObject fields &&
				             new List<string> {"Bug", "User Story"}.Contains(fields["System.WorkItemType"]
					             .Value<string>()) &&
				             // Find any open work that is not in closed state.
				             fields["System.State"].Value<string>().ToLower() != "closed" &&
				             fields.ContainsKey("System.AssignedTo")).ToList();

			if (!offendingWorkItems.Any())
			{
				Sender.Tell(new ActorResponse<IReadOnlyList<string>>(new List<string>(), false));
			}

			var messages = new List<string>();

			var baseUrl =
				$"https://dev.azure.com/{HttpUtility.UrlPathEncode(_azureDevOpsSettings.Organization)}/" +
				$"{HttpUtility.UrlPathEncode(_azureDevOpsSettings.Project)}/_workitems/edit";

			foreach (var offendingWorkItem in offendingWorkItems)
			{
				var currentStatus = offendingWorkItem["fields"]["System.State"].Value<string>();
				var recommendedActionText = currentStatus.ToLower() switch
				{
					"new" => "Move it to the current sprint.",
					"resolved" => "If it is verified, close it. Otherwise, get it verified.",
					"pr submitted" =>
						"Make sure the PR is reviewed, merged, and the work is verified by another engineer.",
					_ =>
						"*What is the status of it?* Can you close it or do you need to create a follow-up work item in the current sprint?"
				};

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
					"BOARD: Still open in {currentState} state. Story \"{workItemId}:{workItemTitle}\". Assigned to {userEmail} in {currentIteration}.",
					workItemId, workItemTitle, userEmail, _currentIteration.Name);

				messages.Add(
					$"{chatDisplayName}, <{workItemUrl}|{workItemTitle}> is in *{currentStatus}* state! {recommendedActionText}");
			}

			Sender.Tell(new ActorResponse<IReadOnlyList<string>>(messages, true));
		}
	}
}
