using System;

namespace TarikGuney.ManagerAutomation.Helpers
{
    public static class IterationHelper
    {
        public static TimeSpan PointsToDays(int storyPoint)
        {
            return storyPoint switch
            {
                1 => TimeSpan.FromDays(1),
                2 => TimeSpan.FromDays(3),
                3 => TimeSpan.FromDays(5),
                _ => TimeSpan.FromDays(10)
            };
        }
    }
}
