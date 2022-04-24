using System.IO;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Graphics;

namespace BeatSync
{
    [Activity(Label = "Settings")]
    public class Settings : Activity
    {
        // Current activity views
        private static NumberPicker agePicker;
        private static TextView     btnClearCache;
        private static TextView     btnDone;
        private static TextView     txtRange;

        /// <summary>
        /// Updates user age with the current number picker value.
        /// Updates the safe BPM range based on user's age.
        /// </summary>
        private static void UpdateAge()
        {
            int[] safeRange = MainActivity.SetAge(agePicker.Value);
            txtRange.Text = "Safe BPM range: " + safeRange[0] + " - " + safeRange[1];
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            // Default creation procedure
            base.OnCreate(savedInstanceState);

            // Set view to "Settings" activity
            SetContentView(Resource.Layout.Settings);

            // Initialize range textview
            txtRange = FindViewById<TextView>(Resource.Id.txtRange);

            // Set-up the age number picker
            agePicker = FindViewById<NumberPicker>(Resource.Id.npAgePicker);
            agePicker.MinValue = BEATSYNC_CONSTANTS.BEATSYNC_MINIMUMAGE;
            agePicker.MaxValue = BEATSYNC_CONSTANTS.BEATSYNC_MAXIMUMAGE;
            agePicker.WrapSelectorWheel = false;
            agePicker.DescendantFocusability = DescendantFocusability.BlockDescendants;

            // If user age is stored, load it
            if (new Java.IO.File(Application.Context.FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERDATA).Exists())
            {
                using (var streamReader = new StreamReader(System.IO.Path.Combine(FilesDir.AbsolutePath, BEATSYNC_CONSTANTS.BEATSYNC_USERDATA)))
                {
                    agePicker.Value = (char)streamReader.Read();
                }
            }

            // Otherwise set to default
            else
            {
                agePicker.Value = BEATSYNC_CONSTANTS.BEATSYNC_DEFAULTAGE;
            }

            // Set-up the "CLEAR LOCAL CACHE" button
            btnClearCache = FindViewById<TextView>(Resource.Id.btnClearCache);
            btnClearCache.SetBackgroundColor(Color.WhiteSmoke);

            // Set-up the "DONE" button
            btnDone = FindViewById<TextView>(Resource.Id.btnDone);
            btnDone.SetTextColor(Color.White);
            btnDone.SetBackgroundColor(BEATSYNC_CONSTANTS.Colors.Blue);
            btnDone.Alpha = 0.85f;

            // Finish activity on "DONE" button click
            btnDone.Click += delegate
            {
                btnDone.Alpha = 0.75f;
                Finish();
            };

            // Clear cache and restart the app
            btnClearCache.LongClick += delegate
            {
                // Delete age
                if (new Java.IO.File(FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERDATA).Exists())
                {
                    new Java.IO.File(FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERDATA).Delete();
                }

                // Delete calculated songs list
                if (new Java.IO.File(FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERSONGS).Exists())
                {
                    new Java.IO.File(FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERSONGS).Delete();
                }

                // Delete playlist info
                if (new Java.IO.File(FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERPLAYLIST).Exists())
                {
                    new Java.IO.File(FilesDir, BEATSYNC_CONSTANTS.BEATSYNC_USERPLAYLIST).Delete();
                }

                // Restart the application
                Process.KillProcess(Process.MyPid());
            };
            
            // Update user age with the new number picker value
            agePicker.ValueChanged += delegate
            {
                // Store age on disk
                using (var streamWriter = new StreamWriter(System.IO.Path.Combine(FilesDir.AbsolutePath, BEATSYNC_CONSTANTS.BEATSYNC_USERDATA)))
                {
                    streamWriter.Write((char)agePicker.Value);
                }

                // Update user age
                UpdateAge();
            };

            // Update current user age
            UpdateAge();

            // Change status bar color
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                Window.ClearFlags(WindowManagerFlags.TranslucentStatus);
                Window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
                Window.SetStatusBarColor(BEATSYNC_CONSTANTS.Colors.Blue);
            }
        }
    }
}