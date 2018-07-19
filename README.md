# DoubleSocket

A library which exposes a TCP-UDP socket pair, letting you send time-critical, but unimportant data through the UDP socket
and optionally large quantities of must-arrive data through the TCP socket.

A connection-based protocol is implemented on top of UDP with the help of the TCP connection, synchronizing their states.



## Protocol

This section describes how the connections should be established and authenticated for the TCP and UDP sockets and how data can be transferred over them.

1) TCP handshake: a TCP connection is created between the server and the client

2) TCP authentication: the client sends (encrypted) data to the server and the server accepts or denies the connection based on the received data.
ALl TCP packets contain a 16-bit number prefix describing their size.
All future TCP packets, including the response to the first packet, are encrypted.
If the server accepts the connection, the response will be a 64-bit long key, otherwise it will 8 bits informing the client of the reason of the denial.
The 64-bit long key's first' 32 bits mustn't equal the CRC-32 generated from its last 32 bits: this is how authentication and data packets are differentiated.
This key is linked to the client (therefore multiple clients mustn't have equal keys) and is stored for a few seconds, it expires afterward.

3) UDP connection and authentication: the client redundantly sends the received 64-bit long key (without encrypting it) over UDP multiple times.
If it is accepted by the server, the client receives a packet (a single byte with the value of zero) over TCP.
The client is also notified if the key expires (the client receives a single byte with a non-zero value).

4) After the UDP socket is also ready, data can be sent over both the UDP and the TCP sockets.
Data sent over UDP is encrypted using the same key as the data sent over TCP. UDP packets' first 32 bits contain a CRC-32, which is used to validate their integrity.

5) If the TCP socket gets closed or times out, the UDP socket also gets closed. Safe disconnects are not implemented on this level.
