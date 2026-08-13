using System.Collections.Generic;

namespace UnityVerify
{
    public static class VerifyStats
    {
        public static long ClientTelemetrySent;
        public static long ClientTelemetryDropped;
        public static long ClientCommandsReceived;
        public static long ServerTelemetryReceived;
        public static long ServerCommandsBroadcast;
        public static long ServerFirstTelemetryTimestamp;
        public static long ServerLastTelemetryTimestamp;
        public static long TransportDroppedCount;
        public static int PeakConnections;
        public static int ConnectedCount;

        public static readonly List<long> RttMilliseconds = new List<long>();
        public static readonly object RttSync = new object();

        public static long ClientSendTotalTicks;
        public static long ClientSendSamples;
        public static long ClientSendMaxTicks;
        public static long ClientSendAllocatedBytes;
        public static long ClientSendAllocationSamples;
    }
}
