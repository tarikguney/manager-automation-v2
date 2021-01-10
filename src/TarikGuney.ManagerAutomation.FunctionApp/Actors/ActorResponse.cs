namespace TarikGuney.ManagerAutomation.Actors
{
	public class ActorResponse<T>
	{
		public T Content { get; }
		public bool Success { get; }

		public ActorResponse(T content, bool success)
		{
			Content = content;
			Success = success;
		}
	}
}
