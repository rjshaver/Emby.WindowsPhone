﻿using System.Windows.Navigation;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Phone.Controls;

namespace MediaBrowser.WindowsPhone.Views
{
    /// <summary>
    /// Description for NowPlayingView.
    /// </summary>
    public partial class NowPlayingView : PhoneApplicationPage
    {
        /// <summary>
        /// Initializes a new instance of the NowPlayingView class.
        /// </summary>
        public NowPlayingView()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if(e.NavigationMode == NavigationMode.Back) Messenger.Default.Send(new NotificationMessage(Constants.PlaylistPageLeftMsg));
            base.OnNavigatedFrom(e);
        }
    }
}