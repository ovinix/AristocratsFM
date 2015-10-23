using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Popups;
using System.Diagnostics;
using Windows.Storage;
using Windows.Data.Xml.Dom;

namespace AristocratsFM
{
    public sealed partial class MainPage : Page
    {
        private MediaPlayer _mediaPlayer;
        private bool _isTimerStarted = false;
        private string _csUrl = "http://air.aristocrats.fm/cs-amusic.php";
        private string _toPlay = "http://144.76.79.38:8000/amusic-128";
        
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.InitializeMediaPlayer();

            this.UpdateCurrentStation();            
            
            this.UpdateButtonImage();         
            
            this.StartTimer();       
        }

        private void InitializeMediaPlayer()
        {
            Debug.WriteLine("InitializeMediaPlayer() - Begin");
            // Launching the BackgroundAudioTask
            _mediaPlayer = BackgroundMediaPlayer.Current;
            _mediaPlayer.CurrentStateChanged += _mediaPlayer_CurrentStateChanged;

            Debug.WriteLine("InitializeMediaPlayer() - _mediaPlayer.CurrentState:" + _mediaPlayer.CurrentState.ToString());

            BackgroundMediaPlayer.MessageReceivedFromBackground += BackgroundMediaPlayer_MessageReceivedFromBackground;

            Debug.WriteLine("InitializeMediaPlayer() - End");
        }

        #region BackgroundMediaPlayer_Events
        async void BackgroundMediaPlayer_MessageReceivedFromBackground(object sender, MediaPlayerDataReceivedEventArgs e)
        {
            ValueSet valueSet = e.Data;
            foreach (string key in valueSet.Keys)
            {
                switch (key)
                {
                    case "Exited":
                        Debug.WriteLine("BackgroundMediaPlayer_MessageReceivedFromBackground() - Exited message recieved");                     
                        await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            _mediaPlayer = null;
                            PlayButton.Background = this.Resources["PlayImageBrush"] as Brush;
                            Buffering.Text = "";
                        });
                        break;
                    default:
                        break;
                }
            }
        }

        async void _mediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        {
            switch (sender.CurrentState)
            {
                case MediaPlayerState.Buffering:
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Buffering.Text = "Buffering...";
                    });
                    break;
                case MediaPlayerState.Closed:
                    break;
                case MediaPlayerState.Opening:
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        // Updating current theme in case user changed station and clicked UVC Play button
                        // with previous station stream
                        this.UpdateCurrentStation();
                        Buffering.Text = "Opening...";
                    });
                    break;
                case MediaPlayerState.Paused:
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        this.UpdateButtonImage();
                    });
                    break;
                case MediaPlayerState.Playing:
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        this.UpdateButtonImage();
                        Buffering.Text = "";
                    });
                    break;
                case MediaPlayerState.Stopped:
                    break;
                default:
                    break;
            }
        }
        #endregion BackgroundMediaPlayer_Events

        #region Setting_Page_Content
        private async void GetCurrentSong(string url)
        {
            try
            {
                // WP cashes responses with same url
                string newUrl = url + "?temp=" + DateTime.Now.Millisecond;
                string page = await new HttpClient().GetStringAsync(newUrl);

                string artist = "", song = "";

                if (_csUrl.Equals("http://aristocrats.fm/service/NowOnAir.xml"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(page);

                    var nodes = xmlDoc.GetElementsByTagName("Artist");
                    artist = nodes[0].Attributes.GetNamedItem("name").InnerText;

                    nodes = xmlDoc.GetElementsByTagName("Song");
                    song = nodes[0].Attributes.GetNamedItem("title").InnerText;

                    if (artist == "" && song == "")
                    {
                        song = "Прямой эфир";
                        artist = "Радио Аристократы";
                    }
                }
                else
                {
                    page = page.Replace("h4", "");
                    page = page.Replace("h5", "");
                    page = page.Replace("/", "");

                    string[] separators = { "<>" };
                    string[] CurrentSong = page.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                    artist = CurrentSong[0];
                    if (CurrentSong.Length > 1)
                    {
                        song = CurrentSong[1];
                    }
                    else
                    {
                        song = "";
                    }
                }

                Artist.Text = artist;
                Song.Text = song;
            }
            catch (Exception)
            {
                Artist.Text = "";
                Song.Text = "";
            }
        }

        private async void UpdateCurrentStation()
        {
            Debug.WriteLine("GetCurrentStation() - Asking for station");
            // Loading current station from app settings
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey("CurrentStation"))
            {
                _toPlay = ApplicationData.Current.LocalSettings.Values["CurrentStation"].ToString();

                switch (_toPlay)
                {
                    case "http://144.76.79.38:8000/amusic-128":
                        _csUrl = "http://air.aristocrats.fm/cs-amusic.php";
                        await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            this.GetCurrentSong(_csUrl);
                            this.SetCurrentTheme();
                        });
                        break;
                    case "http://144.76.79.38:8000/live2-64":
                        _csUrl = "http://aristocrats.fm/service/NowOnAir.xml";
                        await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            this.GetCurrentSong(_csUrl);
                            RadioButton64.IsChecked = true;
                            this.SetCurrentTheme();                            
                        });
                        break;
                    case "http://144.76.79.38:8000/live2":;
                        _csUrl = "http://aristocrats.fm/service/NowOnAir.xml";
                        await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            this.GetCurrentSong(_csUrl);
                            RadioButton128.IsChecked = true;
                            this.SetCurrentTheme();                            
                        });
                        break;
                    default:
                        break;
                }
            }
        }

        private void UpdateButtonImage()
        {
            Debug.WriteLine("SetButtonImage()");
            if (_mediaPlayer.CurrentState == MediaPlayerState.Playing)
            {
                //PlayButton.Template = PauseButtonTemplate;

                PlayButton.Background = this.Resources["PauseImageBrush"] as Brush;
                Debug.WriteLine("   SetButtonImage() - PauseImage");
            }
            else if (_mediaPlayer.CurrentState == MediaPlayerState.Paused || _mediaPlayer.CurrentState == MediaPlayerState.Closed)
            {
                //PlayButton.Template = PlayButtonTemplate;

                PlayButton.Background = this.Resources["PlayImageBrush"] as Brush;
                Debug.WriteLine("   SetButtonImage() - PlayImage");
            }
        }

        private void SetCurrentTheme()
        {
            Debug.WriteLine("SetCurrentTheme()");
            if (!_csUrl.Equals("http://air.aristocrats.fm/cs-amusic.php"))
            {
                //Color cl = new Color();
                //cl.R = 153;
                //cl.G = 43;
                //cl.B = 53;
                //cl.A = 255;
                //this.Background = new SolidColorBrush(cl);

                this.Background = this.Resources["RedThemeBrush"] as Brush;

                ButtonAristocrats.BorderBrush = new SolidColorBrush(Colors.White);
                ButtonAMusic.BorderBrush = null;

                RadioButton64.Visibility = Windows.UI.Xaml.Visibility.Visible;
                RadioButton128.Visibility = Windows.UI.Xaml.Visibility.Visible;

                Debug.WriteLine("   SetCurrentTheme() - RedThemeBrush");
            }
            else
            {
                //Color cl = new Color();
                //cl.R = 0;
                //cl.G = 48;
                //cl.B = 74;
                //cl.A = 255;
                //this.Background = new SolidColorBrush(cl);

                this.Background = this.Resources["BlueThemeBrush"] as Brush;

                ButtonAMusic.BorderBrush = new SolidColorBrush(Colors.White);
                ButtonAristocrats.BorderBrush = null;

                RadioButton64.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                RadioButton128.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                Debug.WriteLine("   SetCurrentTheme() - BlueThemeBrush");
            }
        }
        #endregion Setting_Page_Content

        #region Media_Managment
        private void PauseMedia()
        {
            var message = new ValueSet{ { "Pause", "" } };
            BackgroundMediaPlayer.SendMessageToBackground(message);
        }

        private void PlayMedia(string toPlay)
        {
            // Saving current station
            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("CurrentStation"))
            {
                ApplicationData.Current.LocalSettings.Values.Add("CurrentStation", toPlay);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["CurrentStation"] = toPlay;
            }

            var message = new ValueSet { { "Play", toPlay } };
            BackgroundMediaPlayer.SendMessageToBackground(message);
        }
        #endregion Media_Managment

        #region Timer
        private void StartTimer()
        {
            Debug.WriteLine("StartTimer()");
            if (_mediaPlayer.CurrentState == MediaPlayerState.Closed)
            {
                GetCurrentSong(_csUrl);
            }
            
            _isTimerStarted = true;
            //  DispatcherTimer setup
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler<object>(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 8);
            dispatcherTimer.Start();
        }

        private void dispatcherTimer_Tick(object sender, object e)
        {
            if (_isTimerStarted)
            {
                GetCurrentSong(_csUrl);
            }
        }
        #endregion Timer

        #region ButtonClick_Events
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Line below added to escape crashes then UVC was intercepted by another app
            if (_mediaPlayer == null)
            {
                this.InitializeMediaPlayer();
            }

            switch (_mediaPlayer.CurrentState)
            {
                case MediaPlayerState.Buffering:
                    break;
                case MediaPlayerState.Closed:
                    PlayMedia(_toPlay);
                    break;
                case MediaPlayerState.Opening:
                    break;
                case MediaPlayerState.Paused:
                    PlayMedia(_toPlay);
                    break;
                case MediaPlayerState.Playing:
                    PauseMedia();
                    break;
                case MediaPlayerState.Stopped:
                    break;
                default:
                    break;
            }
        }
        
        private void ButtonAristocrats_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonAristocrats.BorderBrush == null)
            {
                PauseMedia();

                if (RadioButton64.IsChecked == true)
                {
                    _toPlay = "http://144.76.79.38:8000/live2-64";
                    _csUrl = "http://aristocrats.fm/service/NowOnAir.xml";
                }
                else if (RadioButton128.IsChecked == true)
                {
                    _toPlay = "http://144.76.79.38:8000/live2";
                    _csUrl = "http://aristocrats.fm/service/NowOnAir.xml";
                }
                else
                {
                    RadioButton64.IsChecked = true;
                    _toPlay = "http://144.76.79.38:8000/live2-64";
                    _csUrl = "http://aristocrats.fm/service/NowOnAir.xml";
                }
                
                SetCurrentTheme();

                GetCurrentSong(_csUrl);
            }
        }

        private void ButtonAMusic_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonAMusic.BorderBrush == null)
            {
                PauseMedia();

                _toPlay = "http://144.76.79.38:8000/amusic-128";
                _csUrl = "http://air.aristocrats.fm/cs-amusic.php";

                SetCurrentTheme();

                GetCurrentSong(_csUrl);
            }
        }        
        #endregion ButtonClick_Events

        private void RadioButton64_Checked(object sender, RoutedEventArgs e)
        {
            _toPlay = "http://144.76.79.38:8000/live2-64";
            _csUrl = "http://aristocrats.fm/service/NowOnAir.xml";
        }

        private void RadioButton128_Checked(object sender, RoutedEventArgs e)
        {
            _toPlay = "http://144.76.79.38:8000/live2";
            _csUrl = "http://aristocrats.fm/service/NowOnAir.xml";
        }

        private void RadioButton64_Unchecked(object sender, RoutedEventArgs e)
        {
            if (BackgroundMediaPlayer.Current.CurrentState == MediaPlayerState.Playing || BackgroundMediaPlayer.Current.CurrentState == MediaPlayerState.Buffering)
            {
                PauseMedia();
            }
        }

        private void RadioButton128_Unchecked(object sender, RoutedEventArgs e)
        {
            if (BackgroundMediaPlayer.Current.CurrentState == MediaPlayerState.Playing || BackgroundMediaPlayer.Current.CurrentState == MediaPlayerState.Buffering)
            {
                PauseMedia();
            }
        }
    }
}