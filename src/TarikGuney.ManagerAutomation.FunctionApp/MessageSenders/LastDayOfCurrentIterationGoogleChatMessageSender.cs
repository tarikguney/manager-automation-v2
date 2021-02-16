using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.MessageSenders
{
	public class LastDayOfCurrentIterationGoogleChatMessageSender : ILastDayOfCurrentIterationMessageSender
	{
		private readonly IOptions<GoogleChatSettings> _googleChatSettingsOptions;
		private readonly IOptions<CurrentIterationInfo> _currentIterationInfoOptions;

		public LastDayOfCurrentIterationGoogleChatMessageSender(IOptions<GoogleChatSettings> googleChatSettingsOptions,
			IOptions<CurrentIterationInfo> currentIterationInfoOptions)
		{
			_googleChatSettingsOptions = googleChatSettingsOptions;
			_currentIterationInfoOptions = currentIterationInfoOptions;
		}

		public async Task SendMessages(IReadOnlyList<string> messages)
		{
			var allCompleted = messages == null || !messages.Any();

			var httpClient = new HttpClient();

			var greetings = new[]
			{
				$"Hello there team 👋, this is *the last day* of our current sprint ({_currentIterationInfoOptions.Value.Name})."
			};

			var actionMessage = allCompleted
				? "\n\nAnd, *GREAT WORK* <users/all>! 👏🎉👏🎉 *All of the work items are closed* from this sprint! " +
				  "Have a wonderful weekend and I will see you all next week!"
				: "*Unfortunately*, there are some remaining work. Please complete the actions below *before the end of the day*:\n\n";

			var random = new Random();
			var randomGreeting = greetings[random.Next(0, greetings.Length)];

			var chatMessage = new
			{
				text = $"{randomGreeting} {actionMessage}" + string.Join("\n\n", messages)
			};

			await httpClient.PostAsJsonAsync(_googleChatSettingsOptions.Value.WebhookUrl, chatMessage);
		}
	}
}
