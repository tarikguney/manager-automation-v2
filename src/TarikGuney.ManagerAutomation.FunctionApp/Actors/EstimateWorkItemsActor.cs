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
	/// During the current sprint, this actor finds the work items that do not have any estimation
	/// or story points.
	/// </summary>
    public class EstimateWorkItemsActor : ReceiveActor
    {
        private readonly ILogger _logger;
        private readonly AzureDevOpsSettings _azureDevOpsSettings;
        private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
        private readonly CurrentIterationInfo _currentIteration;

        public EstimateWorkItemsActor(ILogger logger,
            IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions,
            IOptions<List<DevOpsChatUserMap>> devOpsChatUserMapOptions, IOptions<CurrentIterationInfo> currentIterationOptions)
        {
            _logger = logger;
            _azureDevOpsSettings = azureDevOpsSettingsOptions.Value;
            _devOpsChatUserMaps = devOpsChatUserMapOptions.Value;
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
                             new List<string>() {"Bug", "User Story"}.Contains(fields["System.WorkItemType"]
                                 .Value<string>()) &&
                             !fields.ContainsKey("Microsoft.VSTS.Scheduling.StoryPoints") &&
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
                    "BOARD: Missing story point for \"{workItemId}:{workItemTitle}\". Assigned to {userEmail} in {currentIteration}.",
                    workItemId, workItemTitle, userEmail, _currentIteration.Name);

                messages.Add(
                    $"{chatDisplayName}, *estimate* the story point of <{workItemUrl}|{workItemTitle}>.");
            }

            Sender.Tell(new ActorResponse<IReadOnlyList<string>>(messages, true));
        }
    }
}
