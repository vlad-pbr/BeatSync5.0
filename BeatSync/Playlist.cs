using System.IO;
using Android.App;
using Android.OS;
using Android.Graphics;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace BeatSync
{
    [Activity(Label = "Playlist")]

    public class Playlist : Activity
    {
        // Current activity views
        private static ListView     songList;
        private static ProgressBar  bpmProgressBar;
        private static ImageView    btnBack;

        /// <summary>
        /// Tries to save the playlist in its current state to disk.
        /// </summary>
        /// <returns>True if saved successfully, false otherwise.</returns>
        public static bool SavePlaylist()
        {
            // Try storing playlist on disk
            try
            {
                using (var streamWriter = new StreamWriter(System.IO.Path.Combine(Application.Context.FilesDir.AbsolutePath, BEATSYNC_CONSTANTS.BEATSYNC_USERPLAYLIST), false))
                {
                    foreach (string songPath in BEATSYNC_GLOBALS.userPlaylist)
                    {
                        streamWriter.WriteLine(songPath);
                    }
                }

                return (true);
            }

            // If file is unavailable for writing, don't save
            catch (System.IO.IOException)
            {
                return (false);
            }
        }

        /// <summary>
        /// Receives a path to the next song and crossfades the current song with the next song.
        /// </summary>
        protected class Crossfade : AsyncTask<string, bool, bool>
        {
            protected override void OnPreExecute()
            {
                // Set crossfading boolean
                BEATSYNC_GLOBALS.bCrossfading = true;
            }

            protected override bool RunInBackground(params string[] @params)
            {
                // Create a next song player and a crossfade timer
                Android.Media.MediaPlayer nextSong = Android.Media.MediaPlayer.Create(Android.App.Application.Context,
                                                                                      Android.Net.Uri.Parse(@params[0]));
                System.Diagnostics.Stopwatch faderTimer = new System.Diagnostics.Stopwatch();

                // Set up the next song
                nextSong.SeekTo((int)(nextSong.Duration / BEATSYNC_CONSTANTS.BEATSYNC_SONGCUE));
                nextSong.SetVolume(0.0f, 0.0f);

                // Begin playback and crossfade
                faderTimer.Start();
                nextSong.Start();

                // Crossfade until time limit has passed
                while (faderTimer.ElapsedMilliseconds < 5000)
                {
                    float currentVolume = faderTimer.ElapsedMilliseconds / 5000f;
                    nextSong.SetVolume(currentVolume, currentVolume);
                    BEATSYNC_GLOBALS.currentSong.SetVolume(1f - currentVolume, 1f - currentVolume);
                }

                // Stop the previous song
                BEATSYNC_GLOBALS.currentSong.Stop();

                // Next song is now the currently playing song
                BEATSYNC_GLOBALS.currentSong = nextSong;

                return (true);
            }

            protected override void OnPostExecute(bool result)
            {
                // Set crossfading boolean
                BEATSYNC_GLOBALS.bCrossfading = false;
            }
        }

        /// <summary>
        /// Crossfades to the new song and updates the song label.
        /// </summary>
        /// <param name="path">Path to a new song.</param>
        public static void SetSong(string path)
        {
            new Crossfade().Execute(path);
            BEATSYNC_GLOBALS.currentlyPlaying = path;
            MainActivity.SetSongLabel(BEATSYNC_GLOBALS.songTitles[path]);
        }

        /// <summary>
        /// Background task that analyzes every .WAV file in the given paths.
        /// Task calculates the BPM of every "wave" file not previously analyzed.
        /// Task then saves the calculation to song dictionary.
        /// </summary>
        public class SongAnalyzer : AsyncTask<string, bool, bool>
        {
            /// <summary>
            /// Receives the paths to look for .WAV files in.
            /// Analyzes each "wave" file and saves to list of songs.
            /// </summary>
            /// <param name="params">Paths to .WAV files.</param>
            /// <returns>Returns true.</returns>
            protected override bool RunInBackground(params string[] @params)
            {
                // Iterate over each given directory
                for (int nIndex = 0;
                    nIndex < @params.Length;
                    nIndex++)
                {
                    // For each object in the directory
                    foreach (Java.IO.File File in new Java.IO.File(@params[nIndex]).ListFiles())
                    {
                        // If object is a .WAV file AND its duration will suffice for BPM calculation
                        if (File.IsFile &&
                            WAV.IsWAV(File.Path) &&
                            Android.Media.MediaPlayer.Create(Application.Context, Android.Net.Uri.Parse(File.Path)).Duration >= WAV.BPM_CALCULATION_DURATION_MINIMUM * 1000)
                        {
                            // If BPM for this song was NOT previously calculated
                            if (!BEATSYNC_GLOBALS.songBPMs.ContainsKey(File.Path))
                            {
                                // Load current song to the codec
                                WAV waveFile = new WAV(File.Path);

                                // Calculate BPM and add to song dictionary
                                BEATSYNC_GLOBALS.songBPMs.Add(waveFile.Path, waveFile.BPM);
                                BEATSYNC_GLOBALS.userPlaylist.Add(waveFile.Path);

                                // Write calculations to disk
                                using (var songWriter = new StreamWriter(System.IO.Path.Combine(Application.Context.FilesDir.AbsolutePath, BEATSYNC_CONSTANTS.BEATSYNC_USERSONGS), true))
                                {
                                    songWriter.WriteLine(waveFile.Path);
                                    songWriter.WriteLine(waveFile.BPM.ToString());
                                }

                                // Add new song to playlist
                                using (var playlistWriter = new StreamWriter(System.IO.Path.Combine(Application.Context.FilesDir.AbsolutePath, BEATSYNC_CONSTANTS.BEATSYNC_USERPLAYLIST), true))
                                {
                                    playlistWriter.WriteLine(waveFile.Path);
                                }
                            }

                            // Set title for the current song
                            BEATSYNC_GLOBALS.songTitles.Add(File.Path, File.Name.Substring(0, File.Name.Length - 4) + " [" + BEATSYNC_GLOBALS.songBPMs[File.Path] + " BPM]");

                            // Add current song to list of available songs
                            PublishProgress(true);
                            System.Threading.Thread.Sleep(50);
                        }
                    }
                }

                return (true);
            }

            /// <summary>
            /// Receives a single ArrayAdapter with currently available songs.
            /// Sets the received adapter as the main song list adapter.
            /// </summary>
            /// <param name="values">Adapter with available songs.</param>
            protected override void OnProgressUpdate(params bool[] values)
            {
                // Refresh list of songs on new song addition
                RefreshSongList();
            }

            protected override void OnPostExecute(bool result)
            {
                // If progress bar is initialized, hide it
                if (bpmProgressBar != null)
                {
                    bpmProgressBar.Visibility = ViewStates.Gone;
                }

                // Save user playlist
                SavePlaylist();
                BEATSYNC_GLOBALS.bSongsAnalyzed = true;
            }
        }

        /// <summary>
        /// Refreshes the list of analyzed and stored songs.
        /// </summary>
        private static void RefreshSongList()
        {
            // If stored list of calculated songs exists AND song list view is initialized
            if (new Java.IO.File(Application.Context.FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERSONGS).Exists() &&
                songList != null)
            {
                // Create a new list and add the song paths to the list
                JavaList<string> analyzedSongs = new JavaList<string>();
                using (var songReader = new StreamReader(System.IO.Path.Combine(Application.Context.FilesDir.AbsolutePath, BEATSYNC_CONSTANTS.BEATSYNC_USERSONGS), true))
                {
                    // Read until the end of file
                    while (!songReader.EndOfStream)
                    {
                        // Save current song path
                        string currentPath = songReader.ReadLine();

                        // If path is located in the dictionary of song titles, add to analyzed songs
                        if (BEATSYNC_GLOBALS.songTitles.ContainsKey(currentPath))
                        {
                            analyzedSongs.Add(BEATSYNC_GLOBALS.songTitles[currentPath]);
                        }

                        // Skip the BPM value for this song
                        songReader.ReadLine();
                    }
                }

                // Set-up custom adapter with calculated songs to song listview
                songList.Adapter = new ArrayAdapter(songList.Context,
                                                    Android.Resource.Layout.SimpleListItemMultipleChoice,
                                                    analyzedSongs);

                // Check song if it's in the playlist
                for (int nSongIndex = 0;
                nSongIndex < songList.Count;
                nSongIndex++)
                {
                    songList.SetItemChecked(nSongIndex, BEATSYNC_GLOBALS.userPlaylist.Contains(MainActivity.GetKeyByValue(BEATSYNC_GLOBALS.songTitles, songList.GetItemAtPosition(nSongIndex).ToString())));
                }
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            // Default creation procedure
            base.OnCreate(savedInstanceState);

            // Set view to "Playlist" activity
            SetContentView(Resource.Layout.Playlist);

            // Initialize views
            bpmProgressBar  = FindViewById<ProgressBar>(Resource.Id.pbProgressBar);
            btnBack         = FindViewById<ImageView>(Resource.Id.imgBack);

            // Set-up the song list
            songList = FindViewById<ListView>(Resource.Id.lvSongs);
            songList.ChoiceMode = ChoiceMode.Multiple;

            // Change the color of the title text
            FindViewById<TextView>(Resource.Id.txtPlaylist).SetTextColor(BEATSYNC_CONSTANTS.Colors.TextGray);

            // Finish activity on "BACK" button click
            btnBack.Click += delegate
            {
                btnBack.Alpha = 0.55f;
                Finish();
            };

            // Add or remove a song from the playlist on song click
            songList.ItemClick += (sender, e) =>
            {
                // If item is now checked, add song to playlist
                if (songList.IsItemChecked(e.Position))
                {
                    BEATSYNC_GLOBALS.userPlaylist.Add(MainActivity.GetKeyByValue(BEATSYNC_GLOBALS.songTitles, songList.GetItemAtPosition(e.Position).ToString()));
                }

                // If item is now unchecked
                else
                {   
                    // If playlist has enough songs to go on, remove song from playlist
                    if (BEATSYNC_GLOBALS.userPlaylist.Count > BEATSYNC_CONSTANTS.BEATSYNC_MINPLAYLIST)
                    {
                        BEATSYNC_GLOBALS.userPlaylist.Remove(MainActivity.GetKeyByValue(BEATSYNC_GLOBALS.songTitles, songList.GetItemAtPosition(e.Position).ToString()));
                    }

                    // Otherwise check the song back
                    else
                    {
                        songList.SetItemChecked(e.Position, true);
                        Toast.MakeText(this, "At least " + BEATSYNC_CONSTANTS.BEATSYNC_MINPLAYLIST + " songs are required.", ToastLength.Short).Show();
                    }
                }

                // Save altered playlist to disk
                SavePlaylist();
            };

            // Preview a long-clicked song
            songList.ItemLongClick += (sender, e) =>
            {
                // If program is passive
                if (!BEATSYNC_GLOBALS.bActive)
                {
                    // Preview chosen song
                    SetSong(MainActivity.GetKeyByValue(BEATSYNC_GLOBALS.songTitles, songList.GetItemAtPosition(e.Position).ToString()));
                }
            };

            // If songs were successfully analyzed, refresh the song list
            if (BEATSYNC_GLOBALS.bSongsAnalyzed == true)
            {
                RefreshSongList();
                bpmProgressBar.Visibility = ViewStates.Gone;
            }

            // Change status bar color
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                Window.ClearFlags(WindowManagerFlags.TranslucentStatus);
                Window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
                Window.SetStatusBarColor(Color.DimGray);
            }

            // If program is passive
            if (!BEATSYNC_GLOBALS.bActive)
            {
                // Notify user of preview feature
                Toast.MakeText(this, "Hold to preview.", ToastLength.Long).Show();
            }
        }
    }
}