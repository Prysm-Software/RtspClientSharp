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