using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Popups;

namespace BackgroundAudioTask
{
    public sealed class BackgroundAudioTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        private SystemMediaTransportControls _systemMediaTransportControl;
        private string _nowPlaying { get; set; }

        // This is for "every 5 min cancelation on pause" bug
        private bool _isBackgroundTaskRunning = false;
        private AutoResetEvent _BackgroundStarted = new AutoResetEvent(false);

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // Enable system transport controls for the current view. 
            _systemMediaTransportControl = SystemMediaTransportControls.GetForCurrentView();
            _systemMediaTransportControl.IsEnabled = true;
            _systemMediaTransportControl.IsPauseEnabled = true;
            _systemMediaTransportControl.IsPlayEnabled = true;
            _systemMediaTransportControl.ButtonPressed += MediaTransportControlButtonPressed;
            
            // Handle events
            BackgroundMediaPlayer.MessageReceivedFromForeground += BackgroundMediaPlayer_MessageReceivedFromForeground;
            BackgroundMediaPlayer.Current.CurrentStateChanged += Current_CurrentStateChanged;
            BackgroundMediaPlayer.Current.BufferingStarted += Current_BufferingStarted;
            BackgroundMediaPlayer.Current.MediaFailed += Current_MediaFailed;

            // Handle the closing of the background task correctly
            taskInstance.Canceled += taskInstance_Canceled;
            taskInstance.Task.Completed += Task_Completed;

            _deferral = taskInstance.GetDeferral();

            // TODO: load _nowPlaying from app settings
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey("CurrentStation"))
            {
                _nowPlaying = ApplicationData.Current.LocalSettings.Values["CurrentStation"].ToString();
                //ApplicationData.Current.LocalSettings.Values.Remove("NowPlaying");
            }

            _BackgroundStarted.Set();
            _isBackgroundTaskRunning = true;
            // END

            Debug.WriteLine("BackgroundAudioTask - Started");
        }

        void Current_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            // TODO failed to open stream
            // Sending message to null foreground mediaplayer
            var message = new ValueSet { { "Exited", "" } };
            BackgroundMediaPlayer.SendMessageToForeground(message);

            BackgroundMediaPlayer.Shutdown();
            _deferral.Complete();
        }

        async void Current_BufferingStarted(MediaPlayer sender, object args)
        {
            try
            {
                BackgroundMediaPlayer.Current.Pause();
                while (BackgroundMediaPlayer.Current.BufferingProgress < 1)
                {
                    Debug.WriteLine("Current_BufferingStarted() - Waiting: " + BackgroundMediaPlayer.Current.BufferingProgress);
                    await Task.Delay(5000);
                }
                BackgroundMediaPlayer.Current.Play();
            }
            catch (Exception)
            {

            }            
        }

        void Task_Completed(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            BackgroundMediaPlayer.Shutdown();
            _deferral.Complete();
        }

        void taskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _isBackgroundTaskRunning = false;         

            // Sending message to null foreground mediaplayer
            var message = new ValueSet { { "Exited", "" } };
            BackgroundMediaPlayer.SendMessageToForeground(message);

            BackgroundMediaPlayer.Shutdown();
            _deferral.Complete();
        }

        void Current_CurrentStateChanged(MediaPlayer sender, object args)
        {
            switch (sender.CurrentState)
            {   
                case MediaPlayerState.Closed:
                    break;
                case MediaPlayerState.Opening:
                    break;
                case MediaPlayerState.Paused:
                    _systemMediaTransportControl.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
                case MediaPlayerState.Buffering:
                    break;
                case MediaPlayerState.Playing:
                    _systemMediaTransportControl.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
                case MediaPlayerState.Stopped:
                    break;
                default:
                    break;
            }
        }

        void BackgroundMediaPlayer_MessageReceivedFromForeground(object sender, MediaPlayerDataReceivedEventArgs e)
        {
            ValueSet valueSet = e.Data;
            foreach (string key in valueSet.Keys)
            {
                switch (key)
                {
                    case "Play":
                        PlayMedia(valueSet[key].ToString());
                        break;
                    case "Pause":
                        PauseMedia();
                        break;
                    default:
                        break;
                }
            }
        }

        private void PauseMedia()
        {
            var mediaPlayer = BackgroundMediaPlayer.Current;
            mediaPlayer.Pause();
        }

        private void PlayMedia(string toPlay)
        {
            try
            {
                var mediaPlayer = BackgroundMediaPlayer.Current;
                mediaPlayer.SetUriSource(new Uri(toPlay));
                mediaPlayer.AutoPlay = true;

                _nowPlaying = toPlay;

                _systemMediaTransportControl.DisplayUpdater.Type = MediaPlaybackType.Music;
                _systemMediaTransportControl.DisplayUpdater.MusicProperties.Title = "Aristocrats FM";
                _systemMediaTransportControl.DisplayUpdater.Update();
            }
            catch (Exception)
            {
                // TODO Make message for foreground about error
                //BackgroundMediaPlayer.Shutdown();
                //_deferral.Complete();
            }            
        }

        private void MediaTransportControlButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Pause:
                    PauseMedia();
                    break;
                case SystemMediaTransportControlsButton.Play:
                    if (!_isBackgroundTaskRunning)
                    {
                        _BackgroundStarted.WaitOne(2000);
                    }
                    PlayMedia(_nowPlaying);
                    break;
                default:
                    break;
            }
        }
    }
}