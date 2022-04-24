using System.Collections.Generic;
using Android.Runtime;
using Android.Graphics;
using Android.Media;
using Java.Util;

namespace BeatSync
{
    /// <summary>
    /// Application globals
    /// </summary>
    class BEATSYNC_GLOBALS
    {
        // Music related variables
        public static Dictionary<string, int>       songBPMs            = new Dictionary<string, int>();
        public static Dictionary<string, string>    songTitles          = new Dictionary<string, string>();
        public static JavaList<string>              userPlaylist        = new JavaList<string>();
        public static MediaPlayer                   currentSong         = new MediaPlayer();
        public static string                        currentlyPlaying    = null;
        public static bool                          bCrossfading        = false;
        public static bool                          bSongsAnalyzed      = false;

        // Application status variables
        public static bool bPaired = false;
        public static bool bActive = false;
    }

    /// <summary>
    /// Application constants.
    /// </summary>
    class BEATSYNC_CONSTANTS
    {
        /// <summary>
        /// Colors designed for BeatSync application.
        /// </summary>
        public class Colors
        {
            public static Color Gray        { get; } = Color.ParseColor("#7f8c8d");
            public static Color Red         { get; } = Color.ParseColor("#800000");
            public static Color Blue        { get; } = Color.ParseColor("#3498db");
            public static Color TextGray    { get; } = Color.DimGray;
        }

        // Application constants
        public static readonly string   BEATSYNC_APPLICATION_NAME   = "BeatSync";
        public static readonly int      BEATSYNC_NOTIFICATION_ID    = 7;
        public static readonly int      BEATSYNC_DEFAULTAGE         = 20;
        public static readonly int      BEATSYNC_MINIMUMAGE         = 16;
        public static readonly int      BEATSYNC_MAXIMUMAGE         = 79;
        public static readonly int      BEATSYNC_MAXBPM             = 220;
        public static readonly int      BEATSYNC_MINPLAYLIST        = 2;
        public static readonly float    BEATSYNC_SONGCUE            = 5.5f;
        public static readonly string   BEATSYNC_USERDATA           = "userdata.dat";
        public static readonly string   BEATSYNC_USERSONGS          = "usersongs.dat";
        public static readonly string   BEATSYNC_USERPLAYLIST       = "userplaylist.dat";
        public static readonly int      REQUEST_ENABLE_BLUETOOTH    = 1;
        public static readonly string   ARDUINO_ADDRESS             = "98:D3:32:31:17:CF";
        public static readonly string   ARDUINO_PAIRING_PIN         = "1234";
        public static readonly UUID     ARDUINO_UUID                = UUID.FromString("00001101-0000-1000-8000-00805f9b34fb");
    }
}