
using System;

namespace hds{

	public class TimeUtils{
		
		public static byte[] getUnixTime(){
			TimeSpan ts = (DateTime.UtcNow - new DateTime(1970,1,1,0,0,0));
			UInt32 unixTime = (UInt32)ts.TotalSeconds;
			byte[] result = NumericalUtils.uint32ToByteArray(unixTime,1);
			return result;
		}
		
		public static UInt32 getUnixTimeUint32(){
			TimeSpan ts = (DateTime.UtcNow - new DateTime(1970,1,1,0,0,0));
			UInt32 unixTime = (UInt32)ts.TotalSeconds;
			return unixTime;
		}
		
		public static byte[] getCurrentTime(){
			TimeSpan ts = (DateTime.UtcNow - new DateTime(1970,1,1,0,0,0));
			UInt32 unixTime = (UInt32)ts.TotalSeconds;
			byte[] result = NumericalUtils.uint32ToByteArray(unixTime,1);
			return result;
		}

        private static readonly System.Diagnostics.Stopwatch SimClock = System.Diagnostics.Stopwatch.StartNew();

        /// <summary>
        /// Server sim time in seconds since the server started. Retail sent small uptime values
        /// here (e.g. 0x47967b67 = 76,918.8 s). This used to send Unix seconds / 1000 (about
        /// 1,794,055 "kiloseconds"), which barely advances; the real client stopped acting on
        /// server RPCs mid-session right at one of those timed packets.
        /// </summary>
        public static float GetSimTimeSeconds()
        {
            return (float)SimClock.Elapsed.TotalSeconds;
        }

        public static byte[] getCurrentSimTime()
        {
            return NumericalUtils.floatToByteArray(GetSimTimeSeconds(), 1);
        }

		
		public static byte[] getUnixTime(int seconds){
			TimeSpan ts = (DateTime.UtcNow - new DateTime(1970,1,1,0,0,0));
			UInt32 unixTime = (UInt32)(ts.TotalSeconds+seconds);
			byte[] result = NumericalUtils.uint32ToByteArray(unixTime,1);
			return result;
		}

        public static string getCurrentDateTime(){
            string currentTime ;
            DateTime value = new DateTime();
            value = DateTime.Today;
            currentTime = value.ToString();
            return currentTime;

        }
	}
}
