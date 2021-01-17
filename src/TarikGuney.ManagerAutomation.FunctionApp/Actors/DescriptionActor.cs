using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Akka.Actor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Actors
{
	/// <summary>
	/// During the current sprint, this actor finds the work items without any
	/// description.
	/// </summary>
    public class MissingDescriptionFinderActor : ReceiveActor
    {
        private readonly ILogger _logger;
        private readonly AzureDevOpsSettings _azureDevOpsSettings;
        private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
        private readonly IterationInfo _currentIteration;

        public MissingDescriptionFinderActor(IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions,
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
            var offendingWorkItems = workItems
                .Where(wi => wi["fields"] is JObject fields &&
                             new List<string>() {"Bug", "User Story"}.Contains(fields["System.WorkItemType"]
                                 .Value<string>()) &&
                             ((fields["System.WorkItemType"].Value<string>() == "Bug" &&
                               !fields.ContainsKey("Microsoft.VSTS.TCM.ReproSteps")
                              ) ||
                              (fields["System.WorkItemType"].Value<string>() == "User Story" &&
                               !fields.ContainsKey("System.Description")
                              )
                             )).ToList();

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
