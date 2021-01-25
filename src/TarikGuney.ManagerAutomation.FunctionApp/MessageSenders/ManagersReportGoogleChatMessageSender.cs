using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Options;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.MessageSenders
{
	public class ManagersReportGoogleChatMessageSender : IManagersReportMessageSender
	{
		private readonly IOptions<EngineeringManagerInfo> _managerInfoOptions;

		public ManagersReportGoogleChatMessageSender(IOptions<EngineeringManagerInfo> managerInfoOptions)
		{
			_managerInfoOptions = managerInfoOptions;
		}

		public void SendMessages(IReadOnlyList<string> messages)
		{
			var httpClient = new HttpClient();
			var yesterday = DateTime.Now.Subtract(TimeSpan.FromDays(1)).Date.ToShortDateString();
			var greetings = new[]
			{
				$"Hello <users/{_managerInfoOptions.Value.GoogleChatUserId}>. Here is the report for *yesterday* ({yesterday}) progress",
			};

			var random = new Random();
			var randomGreeting = greetings[random.Next(0, greetings.Length)];

			var finalMessage = messages.All(string.IsNullOrEmpty)
				? "The board is looking good and every thing is on track"
				: string.Join("", messages);

			var chatMessage = new
			{
				text = $"{randomGreeting}:\n\n{finalMessage}"
			};

			httpClient.PostAsJsonAsync(_managerInfoOptions.Value.ManagerRemindersGoogleWebhookUrl,
				chatMessage).Wait();
		}
	}
}
