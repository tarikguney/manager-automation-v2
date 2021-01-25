using System.Collections.Generic;

namespace TarikGuney.ManagerAutomation.MessageSenders
{
	public interface IMessageSender
	{
		void SendMessages(IReadOnlyList<string> messages);
	}

	class GoogleChatMessageSender : IMessageSender
	{
		public void SendMessages(IReadOnlyList<string> messages)
		{

		}
	}
}
