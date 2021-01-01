using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks.Dataflow;

namespace TarikGuney.ManagerAutomation.DataFlow
{
	public static class CurrentIterationGoogleChatMessageSenderAction
	{
		public static ActionBlock<string[]> Block => new ActionBlock<string[]>(async messages =>
		{
			var allCompleted = messages.All(string.IsNullOrWhiteSpace);

			var httpClient = new HttpClient();
			object chatMessage;

			if (allCompleted)
			{
				chatMessage = new
				{
					text = "*GREAT WORK* <users/all>! Everything is up-to-date. Keep it up!"
				};
			}
			else
			{
				var workRequiredGreetings = new[]
				{
					"Hello there team 👋, please complete the requested actions below *ASAP*",
					"Team, please complete the requested actions below *ASAP*",
					"It looks like the current sprint board needs more work ☹, please complete the following actions *ASAP*",
					"Hey you! Yes, you... 😎 It looks like you need to take care of a couple of things below *ASAP* 👇",
					"Hello earthlings 👽, sending you an encrypted message: શક્ય તેટલી વહેલી તકે નીચેની ક્રિયાઓ પૂર્ણ કરો"
				};

				var random = new Random();
				var randomGreeting = workRequiredGreetings[random.Next(0, workRequiredGreetings.Length)];

				chatMessage = new
				{
					text = $"{randomGreeting}:\n\n" + string.Join("", messages)
				};
			}

			await httpClient.PostAsJsonAsync(Config.GoogleChatSettings.WebhookUrl, chatMessage);
		});
	}
}
