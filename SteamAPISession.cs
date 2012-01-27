using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Drawing;
using Newtonsoft.Json.Linq;

namespace SteamWebAPI
{
    /// <summary>
    /// Class allowing you to use the Steam Web API to log in and use Steam Friends functionality.
    /// </summary>
    /// 
    public class SteamAPISession
    {
        private String accessToken;
        private String umqid;
        private String steamid;
        private int message = 0;

        /// <summary>
        /// Enumeration of possible authentication results.
        /// </summary>
        public enum LoginStatus
        {
            LoginFailed,
            LoginSuccessful,
            SteamGuard
        }

        /// <summary>
        /// Status of a user.
        /// </summary>
        public enum UserStatus
        {
            Offline = 0,
            Online = 1,
            Busy = 2,
            Away = 3,
            Snooze = 4
        }

        /// <summary>
        /// Visibility of a user's profile.
        /// </summary>
        public enum ProfileVisibility
        {
            Private = 1,
            Public = 3,
            FriendsOnly = 8
        }

        /// <summary>
        /// Available sizes of user avatars.
        /// </summary>
        public enum AvatarSize
        {
            Small,
            Medium,
            Large
        }

        /// <summary>
        /// Structure containing basic friend info.
        /// </summary>
        public class Friend
        {
            public String steamid;
            public bool blocked;
            public DateTime friendSince;
        }

        /// <summary>
        /// Structure containing extensive user info.
        /// </summary>
        public class User
        {
            public String steamid;
            public ProfileVisibility profileVisibility;
            public int profileState;
            public String nickname;
            public DateTime lastLogoff;
            public String profileUrl;
            internal String avatarUrl;
            public UserStatus status;
            public String realName;
            public String primaryGroupId;
            public DateTime joinDate;
            public String locationCountryCode;
            public String locationStateCode;
            public int locationCityId;
        }

        /// <summary>
        /// Structure containing server info.
        /// </summary>
        public class ServerInfo
        {
            public DateTime serverTime;
            public String serverTimeString;
        }

        /// <summary>
        /// Authenticate with a username and password.
        /// Sends the SteamGuard e-mail if it has been set up.
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        /// <param name="emailauthcode">SteamGuard code sent by e-mail</param>
        /// <returns>Indication of the authentication status.</returns>
        public LoginStatus Authenticate( String username, String password, String emailauthcode = "" )
        {
            String response = steamRequest( "ISteamOAuth2/GetTokenWithCredentials/v0001",
                "client_id=DE45CD61&grant_type=password&username=" + Uri.EscapeDataString( username ) + "&password=" + Uri.EscapeDataString( password ) + "&x_emailauthcode=" + emailauthcode + "&scope=read_profile%20write_profile%20read_client%20write_client" );

            if ( response != null )
            {
                JObject data = JObject.Parse( response );

                if ( data["access_token"] != null )
                {
                    accessToken = (String)data["access_token"];

                    return login() ? LoginStatus.LoginSuccessful : LoginStatus.LoginFailed;
                }
                else if ( ( (string)data["x_errorcode"] ).Equals( "steamguard_code_required" ) )
                    return LoginStatus.SteamGuard;
                else
                    return LoginStatus.LoginFailed;
            }
            else
            {
                return LoginStatus.LoginFailed;
            }
        }

        /// <summary>
        /// Authenticate with an access token previously retrieved with a username
        /// and password (and SteamGuard code).
        /// </summary>
        /// <param name="accessToken">Access token retrieved with credentials</param>
        /// <returns>Indication of the authentication status.</returns>
        public LoginStatus Authenticate( String accessToken )
        {
            this.accessToken = accessToken;
            return login() ? LoginStatus.LoginSuccessful : LoginStatus.LoginFailed;
        }

        /// <summary>
        /// Fetch all friends of a given user.
        /// </summary>
        /// <remarks>This function does not provide detailed information.</remarks>
        /// <param name="steamid">steamid of target user or self</param>
        /// <returns>List of friends or null on failure.</returns>
        public List<Friend> GetFriends( String steamid = null )
        {
            if ( umqid == null ) return null;
            if ( steamid == null ) steamid = this.steamid;

            String response = steamRequest( "ISteamUserOAuth/GetFriendList/v0001?access_token=" + accessToken + "&steamid=" + steamid );

            if ( response != null )
            {
                JObject data = JObject.Parse( response );

                if ( data["friends"] != null )
                {
                    List<Friend> friends = new List<Friend>();

                    foreach ( JObject friend in data["friends"] )
                    {
                        Friend f = new Friend();
                        f.steamid = (String)friend["steamid"];
                        f.blocked = ( (String)friend["relationship"] ).Equals( "ignored" );
                        f.friendSince = unixTimestamp( (long)friend["friend_since"] );
                        friends.Add( f );
                    }

                    return friends;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieve information about the specified users.
        /// </summary>
        /// <remarks>This function doesn't have the 100 users limit the original API has.</remarks>
        /// <param name="steamids">64-bit SteamIDs of users</param>
        /// <returns>Information about the specified users</returns>
        public List<User> GetUserInfo( List<String> steamids )
        {
            if ( umqid == null ) return null;

            String response = steamRequest( "ISteamUserOAuth/GetUserSummaries/v0001?access_token=" + accessToken + "&steamids=" + String.Join( ",", steamids.GetRange( 0, Math.Min( steamids.Count, 100 ) ).ToArray() ) );

            if ( response != null )
            {
                JObject data = JObject.Parse( response );

                if ( data["players"] != null )
                {
                    List<User> users = new List<User>();

                    foreach ( JObject info in data["players"] )
                    {
                        User user = new User();

                        user.steamid = (String)info["steamid"];
                        user.profileVisibility = (ProfileVisibility)(int)info["communityvisibilitystate"];
                        user.profileState = (int)info["profilestate"];
                        user.nickname = (String)info["personaname"];
                        user.lastLogoff = unixTimestamp( (long)info["lastlogoff"] );
                        user.profileUrl = (String)info["profileurl"];
                        user.status = (UserStatus)(int)info["personastate"];

                        user.avatarUrl = info["avatar"] != null ? (String)info["avatar"] : "";
                        if ( user.avatarUrl != null ) user.avatarUrl = user.avatarUrl.Substring( 0, user.avatarUrl.Length - 4 );

                        user.joinDate = unixTimestamp( info["timecreated"] != null ? (long)info["timecreated"] : 0 );
                        user.primaryGroupId = info["primaryclanid"] != null ? (String)info["primaryclanid"] : "";
                        user.realName = info["realname"] != null ? (String)info["realname"] : "";
                        user.locationCountryCode = info["loccountrycode"] != null ? (String)info["loccountrycode"] : "";
                        user.locationStateCode = info["locstatecode"] != null ? (String)info["locstatecode"] : "";
                        user.locationCityId = info["loccityid"] != null ? (int)info["loccityid"] : -1;

                        users.Add( user );
                    }

                    // Requests are limited to 100 steamids, so issue multiple requests
                    if ( steamids.Count > 100 )
                        users.AddRange( GetUserInfo( steamids.GetRange( 100, Math.Min( steamids.Count - 100, 100 ) ) ) );

                    return users;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public List<User> GetUserInfo( List<Friend> friends )
        {
            List<String> steamids = new List<String>( friends.Count );
            foreach ( Friend f in friends ) steamids.Add( f.steamid );
            return GetUserInfo( steamids );
        }

        public User GetUserInfo( String steamid = null )
        {
            if ( steamid == null ) steamid = this.steamid;
            return GetUserInfo( new List<String>( new String[] { steamid } ) )[0];
        }

        /// <summary>
        /// Retrieve the avatar of the specified user in the specified format.
        /// </summary>
        /// <param name="user">User</param>
        /// <param name="size">Requested avatar size</param>
        /// <returns>The avatar as bitmap on success or null on failure.</returns>
        public Bitmap GetUserAvatar( User user, AvatarSize size = AvatarSize.Small )
        {
            if ( user.avatarUrl.Length == 0 ) return null;

            try
            {
                WebClient client = new WebClient();

                Stream stream;
                if ( size == AvatarSize.Small )
                    stream = client.OpenRead( user.avatarUrl + ".jpg" );
                else if ( size == AvatarSize.Medium )
                    stream = client.OpenRead( user.avatarUrl + "_medium.jpg" );
                else
                    stream = client.OpenRead( user.avatarUrl + "_full.jpg" );

                Bitmap avatar = new Bitmap( stream );
                stream.Flush();
                stream.Close();

                return avatar;
            }
            catch ( Exception e )
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves information about the server.
        /// </summary>
        /// <returns>Returns a structure with the information.</returns>
        public ServerInfo GetServerInfo()
        {
            String response = steamRequest( "ISteamWebAPIUtil/GetServerInfo/v0001" );

            if ( response != null )
            {
                JObject data = JObject.Parse( response );

                if ( data["servertime"] != null )
                {
                    ServerInfo info = new ServerInfo();
                    info.serverTime = unixTimestamp( (long)data["servertime"] );
                    info.serverTimeString = (String)data["servertimestring"];
                    return info;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Helper function to complete the login procedure and check the
        /// credentials.
        /// </summary>
        /// <returns>Whether the login was successful or not.</returns>
        private bool login()
        {
            String response = steamRequest( "ISteamWebUserPresenceOAuth/Logon/v0001",
                "?access_token=" + accessToken );

            if ( response != null )
            {
                JObject data = JObject.Parse( response );

                if ( data["umqid"] != null )
                {
                    steamid = (String)data["steamid"];
                    umqid = (String)data["umqid"];
                    message = (int)data["message"];
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Helper function to perform Steam API requests.
        /// </summary>
        /// <param name="get">Path URI</param>
        /// <param name="post">Post data</param>
        /// <returns>Web response info</returns>
        private String steamRequest( String get, String post = null )
        {
            System.Net.ServicePointManager.Expect100Continue = false;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create( "https://63.228.223.110/" + get );
            request.Host = "api.steampowered.com:443";
            request.ProtocolVersion = HttpVersion.Version11;

            if ( post != null )
            {
                request.Method = "POST";
                byte[] postBytes = Encoding.ASCII.GetBytes( post );

                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = postBytes.Length;

                Stream requestStream = request.GetRequestStream();
                requestStream.Write( postBytes, 0, postBytes.Length );
                requestStream.Close();

                message++;
            }

            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if ( (int)response.StatusCode != 200 ) return null;

                String src = new StreamReader( response.GetResponseStream() ).ReadToEnd();
                response.Close();
                return src;
            }
            catch ( WebException e )
            {
                return null;
            }
        }

        private DateTime unixTimestamp( long timestamp )
        {
            DateTime origin = new DateTime( 1970, 1, 1, 0, 0, 0, 0 );
            return origin.AddSeconds( timestamp );
        }
    }
}
