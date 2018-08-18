# DoubleSocket

A library which exposes a TCP-UDP socket pair, letting you send time-critical, but unimportant data through the UDP socket
and optionally large quantities of must-arrive data through the TCP socket.

A connection-based protocol is implemented on top of UDP with the help of the TCP connection, synchronizing their states.



## Protocol

This section describes how the connections should be established and authenticated for the TCP and UDP sockets and how data can be transferred over them.

### TCP handshake

A TCP connection is created between the server and the client.

### TCP authentication - client

The client sends data of not predetermined size (it is up to the user of this library), based on which the server will accept or deny the connection.
This is the only TCP packet which is not encrypted since it is assumed that the data sent in this step contains the encryption key (encrypted using another key, which only the servers know).
All TCP packets contain a 16-bit packet size prefix (these 16 bits are not included in that).
After this prefix, the undetermined data follows, there is nothing else.
The client is kicked if this packet is not sent in time.

### TCP authentication - server

The server examines the received data and accepts or denies the client based on it.
All packets from now on, including this one, are encrypted: all bytes, except the 16-bit packet size prefix, are transformed using AES-128.
The server needs to be able to provide the encryption key based on the received data it examined.
If the client got denied, then a single encrypted packet containing an 8-bit error code will be sent, then the connection will be closed.
If the client got accepted, the server sends an 8-bit long sequence id value bound, a 64-bit long UDP authentication key and a 64-bit long connection-start timestamp.
All three of these are saved internally, linked to the specific client instance.
Multiple clients must not get the same UDP authentication key.
If the client got accepted and the count of authenticated client equals the client limit then all non-authenticated (but connected) clients are kicked and new clients are no longer accepted until an authenticated client disconnects.

### UDP authentication

When the client receives the 64-bit long UDP authentication key over TCP, it continuously sends it over UDP until a timeout happens or the server responds.
The server's response is a single byte with the value of zero over TCP, which informs the client that the UDP connection has been authenticated.
With this step done, both the TCP and the UDP connections are authenticated and are ready to send/receive data.

### TCP payloads

All TCP packets still contain the 16-bit unencrypted long packet size prefix, but now an 8-bit long encrypted sequence id follows it.
These 8 bits, which are used to combat replay attacks, are included in the packet size prefix.
Two sequence ids are saved on both ends: the sent and the received sequence id.
The newly sent packet's sequence id is one greater than the previously sent packet's sequence id and the newly received packet's sequence id must be one greater than the previously received packet's sequence id.
The first packet's sequence id is 0.
In case the sequence id check fails, the packet should be ignored.
If the new sequence id's value would be equal to the sequence id value bound (specified by the server in the TCP authentication step) then the new sequence id's value should be 0 instead.

### UDP payloads

All UDP packets are fully encrypted: they contain no unencrypted bits.
The first 32 bits are the CRC-32 calculated from the rest of the packet.
The next 16 bits are the milliseconds which passed since the timestamp specified by the server in the TCP authentication step divided by 10.
This means that these 16 bits specify when the packet was sent (with a 10ms precision), allowing the ordering of packets, the dropping out-of-order ones, latency measurement and even protection against replay attacks.
This data is not used by this library, it is up to the user to use it or ignore it (and just keep its replay attack protection property).
This elapsed time counter wraps around after ~10.92 minutes, this has to be handled.

### Disconnects

If it is detected that the TCP connection is no longer working, then both the TCP and the UDP connections get fully closed.
