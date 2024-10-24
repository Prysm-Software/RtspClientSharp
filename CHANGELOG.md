2.0.10.0
==========================
Add: Event NALuFrameReceived to include Timestamp with NALu packets

2.0.9.0
==========================
Fix: Tolerance if rtsp server send multile track from the same port

2.0.8.0
==========================
Fix: Wrong rtcp port on UDP. 
Found on this PR: https://github.com/BogdanovKirill/RtspClientSharp/pull/64/files#diff-aa98d7b7b701b6008107edf12d1b35f0cc49656f2972d8309c6b78b2bc9cd44d

2.0.7.0
==========================
Fix: UnhandledException when canceling the linkedTokenSource in RtspClientInternal.ReceiveAsync:
System.ObjectDisposedException CancellationTokenSource a été supprimé. ex=System.AggregateException: Aucune exception de tâche n'a été observée en attendant la tâche ou en accédant à sa propriété Exception. Par conséquent, l'exception non prise en charge a été à nouveau levée par le thread finaliseur. ---> System.ObjectDisposedException: CancellationTokenSource a été supprimé.
   à System.Threading.CancellationTokenSource.ThrowObjectDisposedException()
   à System.Threading.CancellationTokenSource.Cancel()
   à RtspClientSharp.Rtsp.RtspClientInternal.<>c__DisplayClass25_0.<<ReceiveAsync>b__0>d.MoveNext()

2.0.6.0
==========================
Fix: KeepAlive rtsp request were only sent if the server support GET_PARAMETER request. Now if it does not support GET_PARAMETER we sent OPTION for keepalive

2.0.5.0
==========================
Fix: UnobservedTaskException when the rtsp keepalive task failed resulting the receive task not being waited and throwing a WSACancelBlockingCall
Fix: 401 Unauthorized when calling RTSP Verb GET_PARAMETER on some devices.

2.0.4.1
==========================
Add: Handles RTCP timestamp

2.0.3.0
==========================
Fix: On RtspRequestParams objects, headers were not taken in account if the parameter InitialTimestamp wasn't set.

2.0.2.0
==========================
Fix: Memoryleak when connecting/disconnecting.

2.0.1.0
==========================
Fix: Packages missed. Increase buffer of udp/tcp sockets