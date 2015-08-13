﻿using System.Collections.Specialized;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using OneTox.ViewModel;
using OneTox.ViewModel.Friends;

namespace OneTox.View
{
    public sealed partial class MainPage : Page
    {
        private MainViewModel _mainViewModel;

        public MainPage()
        {
            InitializeComponent();
        }

        private void FriendListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FriendList.SelectedItem == null)
                return;

            VisualStateManager.GoToState(this, "ChatState", true);
            ChatBlock.SetDataContext(FriendList.SelectedItem as FriendViewModel);
        }

        private void MainPageLoaded(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "ChatState", false);

            _mainViewModel = DataContext as MainViewModel;
            _mainViewModel.FriendList.Friends.CollectionChanged += FriendsCollectionChangedHandler;

            // TODO: Remember which friend we talked to the last time before shutting down the app and resume with selecting him/her.
            // TODO: Handle the case when the user doesn't have any friends yet with a splash screen or something like that!
            if (_mainViewModel.FriendList.Friends.Count > 0)
            {
                FriendList.SelectedItem = _mainViewModel.FriendList.Friends[0];
            }
        }

        private void FriendsCollectionChangedHandler(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldStartingIndex == -1)
                return;

            if (FriendList.SelectedItem == null) // It means that we just removed the currently selected friend.
            {
                // So select the one right above it:
                FriendList.SelectedItem = (e.OldStartingIndex - 1) > 0
                    ? _mainViewModel.FriendList.Friends[e.OldStartingIndex - 1]
                    : _mainViewModel.FriendList.Friends[0];
            }
        }

        private void SettingsButtonClick(object sender, RoutedEventArgs e)
        {
            FriendList.SelectedItem = null;
            VisualStateManager.GoToState(this, "SettingsState", true);
        }
    }
}