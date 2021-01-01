using System;
using System.Linq;

namespace TarikGuney.ManagerAutomation.Helpers
{
    public static class DateDiffHelper
    {
        public static int CalculateWeekendDays(DateTime startDate, DateTime endDate)
        {
            var daysDiff = (int) (endDate.Date - startDate.Date).TotalDays;
            var weekendDaysCount = Enumerable.Range(0, daysDiff + 1)
                .Select(a => startDate.Add(TimeSpan.FromDays(a)))
                .Count(a => a.DayOfWeek == DayOfWeek.Saturday || a.DayOfWeek == DayOfWeek.Sunday);
            return weekendDaysCount;
        }
    }
}