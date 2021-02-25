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
	/// During the current sprint, this actor finds the work items without any
	/// description.
	/// </summary>
    public class DescriptionActor : ReceiveActor
    {
        private readonly ILogger _logger;
        private readonly AzureDevOpsSettings _azureDevOpsSettings;
        private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
        private readonly CurrentIterationInfo _currentIteration;

        public DescriptionActor(IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions,
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
	        // This finds the user stories and bugs that do not have description.
	        // Azure DevOps does not return the Description field at all when it is empty.
	        // Therefore, we are checking if the fields exist at all in the API response.
            var offendingWorkItems = workItems
                .Where(wi => wi["fields"] is JObject fields &&
                             new List<string>() {"Bug", "User Story"}.Contains(fields["System.WorkItemType"]
                                 .Value<string>()) &&
                             ((fields["System.WorkItemType"].Value<string>().ToLower() == "bug" &&
                                // This field might be coming from the old version of Azure DevOps
								!fields.ContainsKey("Microsoft.VSTS.TCM.ReproSteps") &&
								// Looks like the following is the description field for the bugs in the later versions of Azure DevOps
								!fields.ContainsKey("System.Description")) ||
                              (fields["System.WorkItemType"].Value<string>().ToLower() == "user story" &&
								!fields.ContainsKey("System.Description"))
                             )).ToList();

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
                var userDisplayName = offendingWorkItem["fields"]?["System.CreatedBy"]?["displayName"]
                    ?.Value<string>();
                var userEmail = offendingWorkItem["fields"]?["System.CreatedBy"]?["uniqueName"]?.Value<string>();
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
                    "BOARD: Missing description for \"{workItemId}:{workItemTitle}\". Assigned to {userEmail} in {currentIteration}.",
                    workItemId, workItemTitle, userEmail, _currentIteration.Name);

                messages.Add(
                    $"{chatDisplayName}, add a *description* to <{workItemUrl}|{workItemTitle}>.");
            }

            Sender.Tell(new ActorResponse<IReadOnlyList<string>>(messages, true));
        }
    }
}
