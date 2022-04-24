using System.Linq;
using System.Collections.Generic;
using System.IO;
using Android.App;
using Android.Widget;
using Android.OS;
using Android.Views;
using Android.Bluetooth;
using Android.Content;
using Android.Graphics;
using Android.Support.V4.App;
using Android.Content.Res;

namespace BeatSync
{
    [Activity(Label                 = "BeatSync",
              MainLauncher          = true,
              ConfigurationChanges  = Android.Content.PM.ConfigChanges.Orientation |
                                      Android.Content.PM.ConfigChanges.ScreenSize)]

    public partial class MainActivity : Activity
    {
        // Current activity views
        private static TextView     txtBPM;
        private static TextView     txtStatus;
        private static ImageView    btnMain;
        private static ImageView    btnPlaylist;
        private static ImageView    btnSettings;
        private static TextView     txtSong;

        // Adapters and receivers
        private static BluetoothAdapter         PhoneAdapter;
        private static ArduinoBroadcastReceiver Receiver;

        // User related variables
        private static int userAge;
        private static int userMinBPM;
        private static int userMaxBPM;

        // Other variables
        private static float btnMain_DefaultAlpha;

        /// <summary>
        /// Changes the text and color of the status label.
        /// </summary>
        /// <param name="status">New status text.</param>
        /// <param name="color">Color of the new text.</param>
        protected static void SetStatus(string status,
                                        Color color)
        {
            // If status label is initialized
            if (txtStatus != null)
            {
                // Change status text and color
                txtStatus.Text = status;
                txtStatus.SetTextColor(color);
            }
        }

        /// <summary>
        /// Sets the current age of user.
        /// Calculates and returns the safe BPM range for user.
        /// </summary>
        /// <param name="age">User age.</param>
        /// <returns>Safe BPM range.</returns>
        public static int[] SetAge(int age)
        {
            userAge = age;
            userMaxBPM = (int)((BEATSYNC_CONSTANTS.BEATSYNC_MAXBPM - userAge) * 0.85);
            userMinBPM = (int)((BEATSYNC_CONSTANTS.BEATSYNC_MAXBPM - userAge) * 0.40);
            return (new int[] { userMinBPM, userMaxBPM });
        }

        /// <summary>
        /// Changes text of the song label.
        /// </summary>
        /// <param name="label">Song title.</param>
        public static void SetSongLabel(string label)
        {
            txtSong.Text = "▶️ " + label; // ❚❚ ▶️ ⏸ ❙❙ ❙ ❙
        }

        /// <summary>
        /// Function returns the key of the given value in the given dictionary.
        /// </summary>
        /// <param name="dictionary">Given dictionary.</param>
        /// <param name="value">Given dictionary value.</param>
        /// <returns>Dictionary key by value.</returns>
        public static string GetKeyByValue(Dictionary<string, string> dictionary,
                                           string value)
        {
            return (dictionary.FirstOrDefault(item => item.Value.Equals(value)).Key);
        }

        /// <summary>
        /// Called when result for a request has been received.
        /// </summary>
        /// <param name="requestCode">Code of the request.</param>
        /// <param name="resultCode">Result of the request.</param>
        /// <param name="data">Data received.</param>
        protected override void OnActivityResult(int requestCode,
                                                 Result resultCode,
                                                 Intent data)
        {
            // Set main button alpha back to default
            btnMain.Alpha = btnMain_DefaultAlpha;

            // If received result code is "ENABLE BLUETOOTH" code AND result is "OK"
            if (requestCode == BEATSYNC_CONSTANTS.REQUEST_ENABLE_BLUETOOTH &&
                resultCode == Result.Ok)
            {
                // Change status
                SetStatus("Searching...", BEATSYNC_CONSTANTS.Colors.TextGray);

                // Cancel discovery if device is already discovering
                if (PhoneAdapter.IsDiscovering)
                {
                    PhoneAdapter.CancelDiscovery();
                }

                // Start discovering
                new BluetoothDiscoverTask().Execute(PhoneAdapter);
            }

            // Otherwise no changes, make button clickable
            else
            {
                btnMain.Clickable = true;
            }
        }

        /// <summary>
        /// Sends a basic notification to user.
        /// </summary>
        /// <param name="context">Current application environment context.</param>
        /// <param name="contentText">Notification text.</param>
        /// <param name="iconID">Notification icon ID.</param>
        protected static void SendNotification(string contentText,
                                               int iconID)
        {
            // Set-up notification
            NotificationCompat.Builder newNotification = new NotificationCompat.Builder(Application.Context);
            newNotification.SetPriority((int)NotificationPriority.Max);
            newNotification.SetDefaults((int)NotificationDefaults.All);
            newNotification.SetContentTitle(BEATSYNC_CONSTANTS.BEATSYNC_APPLICATION_NAME);
            newNotification.SetContentText(contentText);
            newNotification.SetSmallIcon(iconID);

            // Display notification
            ((NotificationManager)Application.Context.GetSystemService(NotificationService)).Notify(BEATSYNC_CONSTANTS.BEATSYNC_NOTIFICATION_ID,
                                                                                                    newNotification.Build());
        }

        /// <summary>
        /// Receives a BPM value. Returns a path to song with the closest BPM to received value.
        /// </summary>
        /// <param name="BPM"></param>
        /// <returns></returns>
        protected static string PickSong(int BPM)
        {
            // Define KVP variable for the answer
            KeyValuePair<string, int> ClosestSong = new KeyValuePair<string, int>(null, 255);

            // Iterate over each analyzed song
            foreach (KeyValuePair<string, int> Song in BEATSYNC_GLOBALS.songBPMs)
            {
                if (new Java.IO.File(Song.Key).Exists() &&                  // If current song exists
                    BEATSYNC_GLOBALS.userPlaylist.Contains(Song.Key) &&     // If current song is in the playlist
                    !Song.Key.Equals(BEATSYNC_GLOBALS.currentlyPlaying) &&  // If it's not the currently playing song
                    System.Math.Abs(Song.Value - BPM) < ClosestSong.Value)  // If BPM difference is smaller than the current answer's
                {
                    // Set the current song to be the current answer
                    ClosestSong = new KeyValuePair<string, int>(Song.Key,
                                                                System.Math.Abs(Song.Value - BPM));
                }
            }

            // Return the path to the song with the closest BPM
            return (ClosestSong.Key);
        }

        /// <summary>
        /// Special kind of receiver which only looks for the specific Arduino bluetooth adapter.
        /// </summary>
        protected class ArduinoBroadcastReceiver : BroadcastReceiver
        {
            public override void OnReceive(Context context,
                                           Intent intent)
            {
                // If a device was found through discovery
                if (intent.Action == BluetoothDevice.ActionFound)
                {
                    // Retrieve device data from the intent
                    BluetoothDevice Device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);

                    // If found device is our Arduino adapter
                    if (Device.Address == BEATSYNC_CONSTANTS.ARDUINO_ADDRESS)
                    {
                        // Stop adapter discovery, start pairing process
                        PhoneAdapter.CancelDiscovery();
                        SetStatus("Pairing...", BEATSYNC_CONSTANTS.Colors.TextGray);

                        // Try pairing
                        try
                        {
                            // Get RFCOMM socket from the adapter
                            BluetoothSocket ArduinoSocket = Device.CreateRfcommSocketToServiceRecord(BEATSYNC_CONSTANTS.ARDUINO_UUID);

                            // If device is not paired, display pairing PIN on pairing dialog
                            if (!PhoneAdapter.BondedDevices.Contains(Device))
                            {
                                SendNotification("Pairing PIN: " + BEATSYNC_CONSTANTS.ARDUINO_PAIRING_PIN, Resource.Drawable.icon_bluetooth);
                            }

                            // Connect to device
                            ArduinoSocket.Connect();

                            // If no exceptions up to this point - devices are connected
                            BEATSYNC_GLOBALS.bPaired = true;

                            // BEGIN TASK THAT UPDATES TEXT LABEL WHILE CONNECTION IS STABLE
                            new BPMTask().ExecuteOnExecutor(AsyncTask.ThreadPoolExecutor, ArduinoSocket);
                        }

                        // If pairing failed
                        catch (Java.IO.IOException)
                        {
                            SetStatus("Device not paired.", BEATSYNC_CONSTANTS.Colors.Red);
                            BEATSYNC_GLOBALS.bPaired = false;
                        }
                    }

                    // Make main button clickable
                    btnMain.Clickable = true;
                }
            }
        }

        /// <summary>
        /// Task that discovers bluetooth adapters around the device.
        /// </summary>
        protected class BluetoothDiscoverTask : AsyncTask<BluetoothAdapter, int, BluetoothAdapter>
        {
            /// <summary>
            /// Set main button icon to "Pairing".
            /// </summary>
            protected override void OnPreExecute()
            {
                btnMain.SetImageResource(Resource.Drawable.icon_pairing01);
            }

            protected override BluetoothAdapter RunInBackground(params BluetoothAdapter[] @params)
            {
                // If device is currently discovering, cancel discovery
                if (@params[0].IsDiscovering)
                {
                    @params[0].CancelDiscovery();
                }

                // Begin discovering bluetooth adapters
                @params[0].StartDiscovery();

                // Wait until the discovery boolean is true
                while (!@params[0].IsDiscovering) ;

                // Make 12 loops 1 second each while adapter is enabled
                for (int nSecond = 0;
                    nSecond < 12 &&
                    @params[0].IsEnabled &&
                    @params[0].IsDiscovering;
                    nSecond++)
                {
                    // Publish current second
                    PublishProgress(nSecond);

                    // Sleep for 1 second
                    System.Threading.Thread.Sleep(1000);
                }

                // Return the adapter
                return @params[0];
            }

            /// <summary>
            /// Animates the main button icon.
            /// </summary>
            /// <param name="values">Seconds passed since start of discovery.</param>
            protected override void OnProgressUpdate(params int[] values)
            {
                // If amount of seconds is even, set to first frame
                if (values[0] % 2 == 0)
                {
                    btnMain.SetImageResource(Resource.Drawable.icon_pairing01);
                }

                // Otherwise set to second frame
                else
                {
                    btnMain.SetImageResource(Resource.Drawable.icon_pairing02);
                }
            }

            protected override void OnPostExecute(BluetoothAdapter Adapter)
            {
                // If adapter was disabled OR adapter is still discovering
                if (!Adapter.IsEnabled ||
                    Adapter.IsDiscovering)
                {
                    // Discovery was unsuccessful
                    Adapter.CancelDiscovery();
                    txtBPM.Text = "N/A";
                    SetStatus("Device not paired.", BEATSYNC_CONSTANTS.Colors.Red);
                    btnMain.SetImageResource(Resource.Drawable.txt_pair);
                }

                // If devices are paired
                else if (BEATSYNC_GLOBALS.bPaired)
                {
                    // Change program status
                    SetStatus("Device paired.", BEATSYNC_CONSTANTS.Colors.TextGray);
                    btnMain.SetImageResource(Resource.Drawable.icon_play);
                }

                // Make main button clickable
                btnMain.Clickable = true;
            }
        }

        /// <summary>
        /// Task receives BPM data over given stream and manages
        /// song playback based on BPM average.
        /// </summary>
        protected class BPMTask : AsyncTask<BluetoothSocket, int, bool>
        {
            // BPM related variables
            private int averageBPM      = 0;
            private int summatedBPM     = 0;
            private int updateCounter   = 0;

            /// <summary>
            /// Receives the BPM data over the given stream.
            /// Passes each new BPM value to 'PublishProgress'.
            /// </summary>
            /// <param name="params">Data stream.</param>
            /// <returns></returns>
            protected override bool RunInBackground(params BluetoothSocket[] @params)
            {
                // Set-up the adapter input stream
                Stream ArduinoStream = @params[0].InputStream;

                // Try communicating with the adapter
                try
                {
                    // If stream is readable
                    if (ArduinoStream.CanRead)
                    {
                        // While stream is readable AND devices are connected
                        while (ArduinoStream.CanRead &&
                               BEATSYNC_GLOBALS.bPaired)
                        {
                            // While program is passive, publish BPM values
                            while (ArduinoStream.CanRead &&
                                   BEATSYNC_GLOBALS.bPaired &&
                                   !BEATSYNC_GLOBALS.bActive)
                            {
                                PublishProgress(ArduinoStream.ReadByte());
                            }

                            // If program became active
                            if (ArduinoStream.CanRead &&
                                BEATSYNC_GLOBALS.bPaired &&
                                BEATSYNC_GLOBALS.bActive)
                            {
                                // Set the first received value as the first average BPM
                                averageBPM = ArduinoStream.ReadByte();

                                // Run the following command on main UI thread
                                ((Activity)txtBPM.Context).RunOnUiThread(() =>
                                {
                                    // Play song with the closest BPM to the average value
                                    Playlist.SetSong(PickSong(averageBPM));
                                });
                                
                                // Nullify summation values
                                updateCounter = summatedBPM = 0;
                            }

                            // While stream is readable AND program is active, keep publishing BPM
                            while (ArduinoStream.CanRead &&
                                   BEATSYNC_GLOBALS.bPaired &&
                                   BEATSYNC_GLOBALS.bActive)
                            {
                                PublishProgress(ArduinoStream.ReadByte());
                            }
                        }
                    }
                }

                // If connection with Arduino was terminated
                catch (Java.IO.IOException)
                {
                    return (true);
                }

                return (false);
            }

            /// <summary>
            /// Updates the BPM label with the data received over bluetooth.
            /// If program is active, manages song selection.
            /// Labels can be updated here since this procedure is on the UI thread.
            /// </summary>
            /// <param name="values">New BPM value.</param>
            protected override void OnProgressUpdate(params int[] values)
            {
                // Update BPM label
                txtBPM.Text = values[0].ToString();

                // Add value to BPM summation
                summatedBPM += values[0];

                // If enough check have been made AND program is active AND new average differs by at least 10 BPM
                // OR song has reached the outro
                if ((++updateCounter >= 120 && BEATSYNC_GLOBALS.bActive && System.Math.Abs(averageBPM - (summatedBPM / (float)updateCounter)) > 10) ||
                    BEATSYNC_GLOBALS.currentSong.CurrentPosition > BEATSYNC_GLOBALS.currentSong.Duration - (int)(BEATSYNC_GLOBALS.currentSong.Duration / BEATSYNC_CONSTANTS.BEATSYNC_SONGCUE))
                {
                    // If songs are currently not being crossfaded
                    if (!BEATSYNC_GLOBALS.bCrossfading)
                    {
                        // Update average BPM value
                        averageBPM = (int)(summatedBPM / (float)updateCounter);

                        // Play song with the closest BPM to current average BPM
                        Playlist.SetSong(PickSong(averageBPM));

                        // Nullify counter and summation
                        updateCounter = summatedBPM = 0;
                    }
                }
            }

            /// <summary>
            /// Called on disrupted connection with Arduino adapter.
            /// </summary>
            /// <param name="bException">If task ended with an exception.</param>
            protected override void OnPostExecute(bool bException)
            {
                // Set program back to passive
                BEATSYNC_GLOBALS.bActive = BEATSYNC_GLOBALS.bPaired = false;
                SetStatus("Device not paired.", BEATSYNC_CONSTANTS.Colors.Red);
                txtBPM.Text = "N/A";
                btnMain.SetImageResource(Resource.Drawable.txt_pair);
            }
        }

        /// <summary>
        /// Function is called when device is rotated.
        /// </summary>
        /// <param name="newConfig">New configuraion.</param>
        public override void OnConfigurationChanged(Configuration newConfig)
        {
            // Execute default configuration commands
            base.OnConfigurationChanged(newConfig);

            // If device is turned horizontally
            if (newConfig.Orientation == Android.Content.Res.Orientation.Landscape)
            {
                // Hide unneeded views
                btnMain.Visibility = ViewStates.Gone;
                txtStatus.Visibility = ViewStates.Gone;
            }

            // If device is turned vertically
            else
            {
                // Show hidden views
                btnMain.Visibility = ViewStates.Visible;
                txtStatus.Visibility = ViewStates.Visible;
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            // Default creation procedure
            base.OnCreate(savedInstanceState);

            // Set view to "Main" activity
            SetContentView(Resource.Layout.Main);

            // If age is stored, load it
            if (new Java.IO.File(FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERDATA).Exists())
            {
                using (var streamReader = new StreamReader(System.IO.Path.Combine(FilesDir.AbsolutePath, BEATSYNC_CONSTANTS.BEATSYNC_USERDATA)))
                {
                    SetAge((userAge = (char)streamReader.Read()));
                }
            }

            // Otherwise set to default
            else
            {
                SetAge((userAge = BEATSYNC_CONSTANTS.BEATSYNC_DEFAULTAGE));
                using (var streamWriter = new StreamWriter(System.IO.Path.Combine(FilesDir.AbsolutePath, BEATSYNC_CONSTANTS.BEATSYNC_USERDATA)))
                {
                    streamWriter.Write((char)userAge);
                }

                // Jump to "Settings" screen
                StartActivity(typeof(Settings));
            }

            // If there are analyzed and stored songs, load them
            if (new Java.IO.File(FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERSONGS).Exists())
            {
                using (var streamReader = new StreamReader(System.IO.Path.Combine(FilesDir.AbsolutePath, BEATSYNC_CONSTANTS.BEATSYNC_USERSONGS)))
                {
                    while (!streamReader.EndOfStream)
                    {
                        BEATSYNC_GLOBALS.songBPMs.Add(streamReader.ReadLine(), int.Parse(streamReader.ReadLine()));
                    }
                }
            }

            // If there is a stored playlist, load it
            if (new Java.IO.File(FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERPLAYLIST).Exists())
            {
                using (var streamReader = new StreamReader(System.IO.Path.Combine(FilesDir.AbsolutePath, BEATSYNC_CONSTANTS.BEATSYNC_USERPLAYLIST)))
                {
                    while (!streamReader.EndOfStream)
                    {
                        BEATSYNC_GLOBALS.userPlaylist.Add(streamReader.ReadLine());
                    }
                }
            }

            // Initialize views
            btnMain     = FindViewById<ImageView>(Resource.Id.btnMain);
            txtBPM      = FindViewById<TextView>(Resource.Id.BPM);
            txtStatus   = FindViewById<TextView>(Resource.Id.txtStatus);
            btnPlaylist = FindViewById<ImageView>(Resource.Id.imgPlaylist);
            txtSong     = FindViewById<TextView>(Resource.Id.txtSong);
            btnSettings = FindViewById<ImageView>(Resource.Id.imgSettings);

            // Set-up adapter and receiver
            PhoneAdapter    = BluetoothAdapter.DefaultAdapter;
            Receiver        = new ArduinoBroadcastReceiver();
            RegisterReceiver(Receiver, new IntentFilter(BluetoothDevice.ActionFound));

            // Set text colors
            FindViewById<TextView>(Resource.Id.txtBPM).SetTextColor(BEATSYNC_CONSTANTS.Colors.TextGray);
            txtSong.SetTextColor(BEATSYNC_CONSTANTS.Colors.TextGray);

            // On song title click
            txtSong.Click += delegate
            {
                // If program is passive
                if (!BEATSYNC_GLOBALS.bActive)
                {
                    // If song is being played, pause it
                    if (BEATSYNC_GLOBALS.currentSong.IsPlaying)
                    {
                        BEATSYNC_GLOBALS.currentSong.Pause();
                    }

                    // Otherwise resume it
                    else
                    {
                        BEATSYNC_GLOBALS.currentSong.Start();
                    }
                }
            };

            // On "Playlist" button click
            btnPlaylist.Click += delegate
            {
                // Start "Playlist" activity
                StartActivity(typeof(Playlist));
            };

            // On "Settings" button click
            btnSettings.Click += delegate
            {
                // Start "Settings" activity
                StartActivity(typeof(Settings));
            };

            // If text of the BPM value was changed
            txtBPM.AfterTextChanged += delegate
            {
                // If devices are paired AND new BPM value is above the limit
                if (BEATSYNC_GLOBALS.bPaired &&
                    int.Parse(txtBPM.Text) > userMaxBPM)
                {
                    // Set color to red
                    txtBPM.SetTextColor(BEATSYNC_CONSTANTS.Colors.Red);

                    // If program is active AND music is playing
                    if (BEATSYNC_GLOBALS.bActive &&
                        BEATSYNC_GLOBALS.currentSong.IsPlaying)
                    {
                        // Stop the music and notify user of high heart rate
                        BEATSYNC_GLOBALS.currentSong.Stop();
                        Android.Media.MediaPlayer.Create(this, Android.Net.Uri.Parse("system/media/audio/ui/LowBattery.ogg")).Start();
                    }
                }

                // If devices are paired AND new BPM value is below the limit
                else if (BEATSYNC_GLOBALS.bPaired &&
                         int.Parse(txtBPM.Text) < userMinBPM)
                {
                    // Set color to gray
                    txtBPM.SetTextColor(BEATSYNC_CONSTANTS.Colors.TextGray);
                }
                
                // Otherwise
                else
                {
                    // Set color to blue
                    txtBPM.SetTextColor(BEATSYNC_CONSTANTS.Colors.Blue);

                    // If program is active AND music is not playing
                    if (BEATSYNC_GLOBALS.bActive &&
                        !BEATSYNC_GLOBALS.currentSong.IsPlaying)
                    {
                        // Resume the song
                        BEATSYNC_GLOBALS.currentSong.Start();
                    }
                }
            };

            // Save default alpha value for the main button
            btnMain_DefaultAlpha = btnMain.Alpha;

            // On main button click
            btnMain.Click += delegate
            {
                // Lower the alpha value of the view
                btnMain.Alpha -= 0.125f;

                // If devices are not paired
                if (!BEATSYNC_GLOBALS.bPaired)
                {
                    // Disable main button
                    btnMain.Clickable = false;

                    // If bluetooth is disabled
                    if (!PhoneAdapter.IsEnabled)
                    {
                        // Request user to enable bluetooth
                        Intent enableBluetooth = new Intent(BluetoothAdapter.ActionRequestEnable);
                        StartActivityForResult(enableBluetooth, BEATSYNC_CONSTANTS.REQUEST_ENABLE_BLUETOOTH);
                    }

                    // Otherwise send an already positive request
                    else
                    {
                        OnActivityResult(BEATSYNC_CONSTANTS.REQUEST_ENABLE_BLUETOOTH, Result.Ok, new Intent());
                    }
                }

                // If devices are paired
                else
                {
                    // If program is active
                    if (BEATSYNC_GLOBALS.bActive)
                    {
                        // Switch program to passive
                        BEATSYNC_GLOBALS.bActive = false;
                        btnMain.SetImageResource(Resource.Drawable.icon_play);
                        SetStatus("Device paired.", BEATSYNC_CONSTANTS.Colors.TextGray);
                    }

                    // If program is passive
                    else
                    {
                        // If there are enough songs in the playlist
                        if (BEATSYNC_GLOBALS.userPlaylist.Count >= BEATSYNC_CONSTANTS.BEATSYNC_MINPLAYLIST)
                        {
                            // Switch program to active
                            BEATSYNC_GLOBALS.bActive = true;
                            btnMain.SetImageResource(Resource.Drawable.icon_pause);
                            SetStatus("Active.", BEATSYNC_CONSTANTS.Colors.TextGray);
                        }

                        // Otherwise notify user
                        else
                        {
                            Toast.MakeText(this, "Not enough songs in the Playlist.", ToastLength.Short).Show();
                        }
                    }

                    // Return to the default alpha value of the main button
                    btnMain.Alpha = btnMain_DefaultAlpha;
                }
            };

            // Change status bar color
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                Window.ClearFlags(WindowManagerFlags.TranslucentStatus);
                Window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
                Window.SetStatusBarColor(Color.DarkGray);
            }

            // Begin analyzing songs in the background
            new Playlist.SongAnalyzer().ExecuteOnExecutor(AsyncTask.ThreadPoolExecutor, Android.OS.Environment.ExternalStorageDirectory.ToString() + "/Music",
                                                                                        Android.OS.Environment.ExternalStorageDirectory.ToString() + "/Download");
        }

        protected override void OnDestroy()
        {
            // Execute default "OnDestroy" commands
            base.OnDestroy();

            // Unregister the arduino receiver
            UnregisterReceiver(Receiver);
        }
    }
}