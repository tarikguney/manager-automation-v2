using System;
using Newtonsoft.Json;

namespace TarikGuney.ManagerAutomation.SettingsModels
{
	public class IterationInfo
	{
		[JsonProperty("name")]
		public string Name { get; set; }
		[JsonProperty("id")]
		public string Id { get; set; }
		[JsonProperty("path")]
		public string Path { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime FinishDate { get; set; }
		public IterationTimeFrame TimeFrame { get; set; }
	}
}
