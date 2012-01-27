using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
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
        private String steamId;
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
        /// Structure containing basic friend info.
        /// </summary>
        public class Friend
        {
            public String steamid;
            public bool blocked;
            public DateTime friendSince;
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
        /// Fetch all friends for a given user.
        /// </summary>
        /// <remarks>This function does not provide detailed information.</remarks>
        /// <param name="steamId">SteamID of target user or self</param>
        /// <returns>List of friends or null on failure.</returns>
        public List<Friend> GetFriends( String steamId = null )
        {
            if ( umqid == null ) return null;
            if ( steamId == null ) steamId = this.steamId;

            String response = steamRequest( "ISteamUserOAuth/GetFriendList/v0001?access_token=" + accessToken + "&steamid=" + steamId );

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
                    steamId = (String)data["steamid"];
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
