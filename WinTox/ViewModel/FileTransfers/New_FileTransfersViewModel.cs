﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Windows.ApplicationModel.Core;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using WinTox.Common;
using WinTox.Model;

namespace WinTox.ViewModel.FileTransfers
{
    public class New_FileTransfersViewModel
    {
        private readonly int _friendNumber;
        private readonly FileTransfersModel _transfersModel;
        private RelayCommand _sendFilesCommand;

        public New_FileTransfersViewModel(int friendNumber)
        {
            _friendNumber = friendNumber;
            _transfersModel = new FileTransfersModel(friendNumber);
            _transfersModel.FileSendRequestReceived += FileSendRequestReceivedHandler;
            Transfers = new ObservableCollection<New_OneFileTransferViewModel>();
            VisualStates = new FileTransfersVisualStates(Transfers);
        }

        public ObservableCollection<New_OneFileTransferViewModel> Transfers { get; private set; }
        public FileTransfersVisualStates VisualStates { get; private set; }

        private async void FileSendRequestReceivedHandler(object sender, OneFileTransferModel e)
        {
            await
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => { AddTransfer(e); });
        }

        #region Visual states

        /// <summary>
        ///     This class's purpose is to supply (trough data binding) the current visual state of FileTransfersBlock and height
        ///     of OpenContentGrid.
        /// </summary>
        public class FileTransfersVisualStates : ViewModelBase
        {
            /// <summary>
            ///     Open: we have one or more file transfers for the current friend an we show "all" (4 max at once) of them.
            ///     Collapsed: we have one or more file transfers for the current friend and we show a placeholder text instead of
            ///     them.
            ///     Invisible: we have 0 file transfers, so we make FileTransfersBlock invisible.
            ///     The user can switch between Open and Collapsed states manually via the UI. Switching to/from Invisible to/from
            ///     either
            ///     states happens programmatically.
            /// </summary>
            public enum TransfersBlockState
            {
                Open,
                Collapsed,
                Invisible
            }

            private const int KHideArrowTextBlockHeight = 10;
            private const int KFileTransferRibbonHeight = 60;
            private readonly ObservableCollection<New_OneFileTransferViewModel> _transferViewModels;
            private TransfersBlockState _blockState;
            private double _openContentGridHeight;

            public FileTransfersVisualStates(ObservableCollection<New_OneFileTransferViewModel> transferViewModels)
            {
                BlockState = TransfersBlockState.Invisible;
                _transferViewModels = transferViewModels;
                _transferViewModels.CollectionChanged += CollectionChangedHandler;
            }

            public TransfersBlockState BlockState
            {
                get { return _blockState; }
                set
                {
                    if (value == _blockState)
                        return;
                    _blockState = value;
                    RaisePropertyChanged();
                }
            }

            /// <summary>
            ///     The current height of the OpenContentGrid on FileTransfersBlock. We need this workaround to be able to animate the
            ///     height of the Grid during visual state transitions. It's because to do that, we need concrete heights, what we
            ///     provide with data binding to this property and the 0 constant.
            /// </summary>
            public double OpenContentGridHeight
            {
                get { return _openContentGridHeight; }
                private set
                {
                    if (value.Equals(_openContentGridHeight))
                        return;
                    _openContentGridHeight = value;
                    RaisePropertyChanged();
                }
            }

            private void CollectionChangedHandler(object sender, NotifyCollectionChangedEventArgs e)
            {
                if (_transferViewModels.Count == 0)
                {
                    BlockState = TransfersBlockState.Invisible;
                    return; // No need to recompute Gird Height if we can't see the Grid itself.
                }

                UpdateOpenContentGridHeight(_transferViewModels.Count);
            }

            /// <summary>
            ///     Whenever we add or remove a OneFileTransferViewModel (and a FileTransferRibbon to/from FileTransfersBlock through
            ///     data binding) we also update OpenContentGridHeight according to the current number of file transfers.
            /// </summary>
            /// <param name="transfersCount">The current number of file transfers.</param>
            private void UpdateOpenContentGridHeight(int transfersCount)
            {
                // We don't show more than 4 items in the list at once, but use a scroll bar in that case.
                var itemsToDisplay = transfersCount > 4 ? 4 : transfersCount;
                OpenContentGridHeight = itemsToDisplay*KFileTransferRibbonHeight + KHideArrowTextBlockHeight;
            }
        }

        #endregion

        #region Send file

        public RelayCommand SendFilesCommand
        {
            get
            {
                return _sendFilesCommand ?? (_sendFilesCommand = new RelayCommand(async () =>
                {
                    var openPicker = new FileOpenPicker();
                    openPicker.FileTypeFilter.Add("*");

                    var files = await openPicker.PickMultipleFilesAsync();
                    if (files.Count == 0)
                        return;

                    foreach (var file in files)
                    {
                        var fileTransferModel = await _transfersModel.SendFile(file);
                        if (fileTransferModel != null)
                        {
                            AddTransfer(fileTransferModel);
                        }
                    }
                }));
            }
        }

        private void AddTransfer(OneFileTransferModel fileTransferModel)
        {
            var fileTransferViewModel = new New_OneFileTransferViewModel(this, fileTransferModel);

            if (Transfers.Contains(fileTransferViewModel))
                return;

            Transfers.Add(fileTransferViewModel);

            if (VisualStates.BlockState == FileTransfersVisualStates.TransfersBlockState.Invisible)
            {
                VisualStates.BlockState = FileTransfersVisualStates.TransfersBlockState.Open;
            }
        }

        #endregion
    }
}