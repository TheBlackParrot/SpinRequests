# SpinRequests
An in-game map request manager for Spin Rhythm XD that abstracts out functions to an HTTP GET API.

Very barebones for now, but I'll expand it out some over time.

Any streaming bot you use (e.g. Streamer.bot, Firebot, MixItUp, etc.) now has request queue functionality -- so long as it supports making HTTP requests (at the minimum).

The request queue lives in the panel on the left side of the map select menu.

## Dependencies
- SpinCore
- SpinShareLib

# HTTP API
By default, a simple HTTP server is started on http://127.0.0.1:6969. Port and IP can be changed in the mod's JSON configuration file. A game restart (a hard restart) is required for changes to take effect.

> [!CAUTION]
> It is **NOT RECOMMENDED** to let this listen on a public IP address. Unless you know what you're doing, stick to `localhost`/`127.x.x.x` IP ranges or any LAN IP range (`10.x.x.x`; `172.16.x.x - 172.31.x.x`; or `192.168.x.x`).

> [!CAUTION]
> If you are behind a firewall, it is **NOT RECOMMENDED** to forward the port you use for this HTTP server, unless you know what you are doing. Forwarding this means allowing anyone outside your local network will have full read/write access to your queue.

As this is really only a web server, you can test any of these endpoints in any web browser of your choice, while the game is running of course.

| Endpoint | Description/Example                                                                                                                                                                                                                                                                                                                                                           | Returns                                            |
|----------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------|
| `/add`   | Adds a map to the queue.<br/>- User identifiers can be tacked on with a `user` query parameter. Internally this is set as a string, anything can be used so long as it's unique.<br/>- Service identifiers can be added by tacking on the `service` query parameter, useful if your requests come from multiple services.<br/>`/add/12345?user=TheBlackParrot&service=twitch` | [Map Data](#map-data-type)                         |
| `/query` | Queries SpinShare for basic map information.<br/>`/query/12345`                                                                                                                                                                                                                                                                                                               | [Map Data](#map-data-type)                         |

# Data structures/schema

<a name="map-data-type"></a>
## Map data
> [!CAUTION]
> Twitch viewers/users with the Moderator status do not have any AutoMod rulesets applied to them. If your bot is a moderator in your channel, responses that are made using any of these metadata fields will be allowed through verbatim.
```json
{
  "Title": <string>,
  "Artist": <string>,
  "Mapper": <string>,
  "SpinShareKey": <integer>,
  "Requester": <string>,
  "Service": <string>,
  "AlreadyDownloaded": <boolean>
}
```