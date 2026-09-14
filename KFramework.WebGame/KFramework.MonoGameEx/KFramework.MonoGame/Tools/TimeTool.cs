using System;

namespace KFramework.MonoGame
{
	public static class TimeTool
	{
		public static string GetFormatStringByTimeSpan(TimeSpan mTimeSpan)
		{
			return mTimeSpan.ToString(@"hh\:mm\:ss");
		}

		public static string GetFormatStringByDateTime(DateTime mDateTime)
		{
			return mDateTime.ToString(@"yyyy/MM/dd HH:mm:ss");
		}

		public static TimeSpan GetTimeSpanFromDateString(string timeStr)
		{
			string dateFormatStr = "g";
			TimeSpan mTimeSpan = TimeSpan.ParseExact(timeStr, dateFormatStr, System.Globalization.CultureInfo.InvariantCulture);
			return mTimeSpan;
		}

		public static DateTime GetLocalTimeFromDateString(string timeStr)
		{
			string dateFormatStr = "yyyy/MM/dd HH:mm:ss";
			DateTime beginTime = DateTime.ParseExact(timeStr, dateFormatStr, System.Globalization.CultureInfo.InvariantCulture);
			return beginTime;
		}

		public static UInt64 GetTimeStampFromDateString(string timeStr)
		{
			DateTime beginTime = GetLocalTimeFromDateString(timeStr);
			return GetTimeStampFromLocalTime(beginTime);
		}

		//------------------------------------------------------------------------------------------
		public static UInt64 GetNowTimeStamp()
		{
			System.DateTime utcTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.Now, TimeZoneInfo.Local);
			return GetTimeStampFromUTCTime(utcTime);
		}

		public static UInt64 GetTimeStampFromLocalTime(DateTime nLocalTime)
		{
			System.DateTime utcTime = TimeZoneInfo.ConvertTimeToUtc(nLocalTime, TimeZoneInfo.Local);
			return GetTimeStampFromUTCTime(utcTime);
		}

		public static UInt64 GetTimeStampFromUTCTime(DateTime utcTime)
		{
			TimeSpan ts = utcTime - new DateTime(1970, 1, 1, 0, 0, 0);
			return (UInt64)ts.TotalSeconds;
		}

		public static DateTime GetUTCTimeFromTimeStamp(UInt64 nTimeStamp)
		{
			DateTime dateTimeStart = new DateTime(1970, 1, 1, 0, 0, 0);
			return dateTimeStart.AddSeconds(nTimeStamp);
		}

		public static DateTime GetLocalTimeFromTimeStamp(UInt64 mTimeStamp)
		{
			DateTime utcTime = GetUTCTimeFromTimeStamp(mTimeStamp);
			return TimeZoneInfo.ConvertTimeFromUtc(utcTime, TimeZoneInfo.Local);
		}

		public static DateTime GetLocalTimeFromUTCTime(DateTime utcTime)
		{
			return TimeZoneInfo.ConvertTimeFromUtc(utcTime, TimeZoneInfo.Local);
		}

		public static DateTime GetUtcTimeFromLocalTime(DateTime LocalTime)
		{
			return TimeZoneInfo.ConvertTimeToUtc(LocalTime, TimeZoneInfo.Utc);
		}
	}
}

