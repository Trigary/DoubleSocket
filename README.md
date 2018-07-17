# DoubleSocket

A library which exposes a TCP-UDP socket pair, letting you send time-critical, but unimportant data through the UDP socket
and optionally large quantities of must-arrive data through the TCP socket.

A connection-based protocol is implemented on top of UDP with the help of the TCP connection, synchronizing their states.
