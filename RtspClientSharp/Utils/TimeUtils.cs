using System;
using System.Runtime.CompilerServices;

namespace RtspClientSharp.Utils
{
    static class TimeUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTimeOver(int currentTicks, int previousTicks, int interval)
        {
            if (Math.Abs(currentTicks - previousTicks) >= interval)
                return true;

            return false;
        }

        public static DateTime GetDateFromNtpTimestamp(ulong ntp)
        {
            uint seconds = (uint)((ntp >> 32) & 0xFFFFFFFF);
            uint fraction = (uint)(ntp & 0xFFFFFFFF);
            int milliseconds = (int)(fraction / double.MaxValue * 1000);
            DateTime baseDate = new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return baseDate.AddSeconds(seconds).AddMilliseconds(milliseconds);
        }
    }
}