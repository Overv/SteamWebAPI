Steam Web API library
========

#### Description ####

This is a .NET library that makes it easy to use the Steam Web API. It conveniently wraps around all of the JSON data and ugly API details with clean methods, structures and classes.
The primary goal of this project is to support Steam Friends functionality. Other possible functionality like purchasing games and item trading may follow later. In short, it will contain
everything needed to write a custom, cross-platform Steam Friends messenger in C#.

#### Reference ####

### Enumerations ###

**LoginStatus**

Enumeration of possible authentication results.

```c#
public enum LoginStatus
{
	LoginFailed,
	LoginSuccessful,
	SteamGuard
}
```

### SteamAPISession ###

This is the main class you will be using. It manages the session of a single Steam user and all requests are issued through methods in this class.
Below follows a description of every method.

**LoginStatus Authenticate( String username, String password, String emailauthcode = "" )**

Authenticate with a username and password. Sends the SteamGuard e-mail if it has been set up and an e-mail code has not been passed.

**LoginStatus Authenticate( String accessToken )**

Authenticate with an access token previously retrieved with a username and password (and SteamGuard code).

**List<Friend> GetFriends( String steamId = null )**

Fetch basic info for all friends of a given user.

**ServerInfo GetServerInfo()**

Returns info about the server, as specified in the *ServerInfo* class. This is the only call besides *Authenticate* that does not require a valid user session.

### Subclasses ###

**Friend**

Structure containing basic friend info.

```c#
public class Friend
{
    public String steamid;
    public bool blocked;
    public DateTime friendSince;
}
```

**ServerInfo**

Structure containing server info.

```c#
public class ServerInfo
{
    public DateTime serverTime;
    public String serverTimeString;
}
```

### Example ###

Here's how to log in with a username and password, ask for the SteamGuard code if necessary and display the amount of friends the user has.

```c#
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using SteamWebAPI;

namespace SteamWebAPI
{
    class Program
    {
        static void Main( string[] args )
        {
            SteamAPISession session = new SteamAPISession();

            Console.Write( "Username: " );
            String username = Console.ReadLine();
            Console.Write( "Password: " );
            String password = Console.ReadLine();

            SteamAPISession.LoginStatus status = session.Authenticate( username, password );
            if ( status == SteamAPISession.LoginStatus.SteamGuard )
            {
                Console.Write( "SteamGuard code: " );
                String code = Console.ReadLine();

                status = session.Authenticate( username, password, code );
            }

            if ( status == SteamAPISession.LoginStatus.LoginSuccessful )
            {
                List<SteamAPISession.Friend> friends = session.GetFriends();
                int blockedFriends = friends.Count( f => f.blocked == true );
                Console.WriteLine( "You have " + ( friends.Count - blockedFriends ) + " friends and " + blockedFriends + " fiends!" );
            }
            else
            {
                Console.WriteLine( "Failed to log in!" );
            }
        }
    }
}
```