﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using SharpTox.Core;
using WinTox.Model;

namespace WinTox.ViewModel.FileTransfers
{
    public class FileTransfersViewModel : ViewModelBase
    {
        private readonly CoreDispatcher _dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
        private readonly int _friendNumber;
        private DispatcherTimer _progressDispatcherTimer;

        public FileTransfersViewModel(int friendNumber)
        {
            _friendNumber = friendNumber;
            Transfers = new ObservableCollection<OneFileTransferViewModel>();
            FileTransferManager.Instance.FileControlReceived += FileControlReceivedHandler;
            FileTransferManager.Instance.TransferFinished += TransferFinishedHandler;
            FileTransferManager.Instance.FileSendRequestReceived += FileSendRequestReceivedHandler;
            SetupProgressDispatcherTimer();
            VisualStates = new VisualStatesViewModel {BlockState = VisualStatesViewModel.TransfersBlockState.Invisible};
        }

        public ObservableCollection<OneFileTransferViewModel> Transfers { get; private set; }

        #region Helper methods

        private OneFileTransferViewModel FindNotPlaceHolderTransferViewModel(int fileNumber)
        {
            return Transfers.FirstOrDefault(transfer => transfer.FileNumber == fileNumber && transfer.IsNotPlaceholder);
            // There can be multiple transfers with the same file number, but there's always only one that's not a placeholder.
        }

        private void AddTransfer(int fileNumber, string fileName, FileTransferState direction)
        {
            Transfers.Add(new OneFileTransferViewModel(this, fileNumber, fileName, direction));

            if (VisualStates.BlockState == VisualStatesViewModel.TransfersBlockState.Invisible)
                VisualStates.BlockState = VisualStatesViewModel.TransfersBlockState.Open;

            VisualStates.UpdateOpenContentGridHeight(Transfers.Count);
        }

        private void RemoveTransfer(OneFileTransferViewModel transferViewModel)
        {
            Transfers.Remove(transferViewModel);

            if (Transfers.Count == 0)
                VisualStates.BlockState = VisualStatesViewModel.TransfersBlockState.Invisible;

            VisualStates.UpdateOpenContentGridHeight(Transfers.Count);
        }

        #endregion

        #region Helper methods for DispatcherTimer

        private void SetupProgressDispatcherTimer()
        {
            _progressDispatcherTimer = new DispatcherTimer();
            _progressDispatcherTimer.Tick += (s, e) =>
            {
                var progresses = FileTransferManager.Instance.GetTransferProgressesOfFriend(_friendNumber);
                foreach (var progress in progresses)
                {
                    var transfer = FindNotPlaceHolderTransferViewModel(progress.Key);
                    if (transfer == null)
                        continue;

                    transfer.Progress = progress.Value;
                }
            };
            _progressDispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 250);
        }

        private void StartTimerIfNeeded()
        {
            if (GetActiveTransfersCount() > 0)
            {
                _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { _progressDispatcherTimer.Start(); });
            }
        }

        private void StopTimerIfNeeded()
        {
            if (GetActiveTransfersCount() == 0)
            {
                _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { _progressDispatcherTimer.Stop(); });
            }
        }

        private int GetActiveTransfersCount()
        {
            return Transfers.Count(transfer => transfer.IsNotPlaceholder &&
                                               transfer.State != FileTransferState.PausedByFriend &&
                                               transfer.State != FileTransferState.PausedByUser);
        }

        #endregion

        #region Actions coming from the View

        public async Task SendFile(StorageFile file)
        {
            var stream = (await file.OpenReadAsync()).AsStreamForRead();
            int fileNumber;

            var successfulSend = FileTransferManager.Instance.SendFile(_friendNumber, stream, file.Name, out fileNumber);

            if (successfulSend)
            {
                AddTransfer(fileNumber, file.Name, FileTransferState.Uploading);
            }
            else
            {
                stream.Dispose();
            }
        }

        public void AcceptTransferByUser(int fileNumber, Stream saveStream)
        {
            FileTransferManager.Instance.ReceiveFile(_friendNumber, fileNumber, saveStream);
            StartTimerIfNeeded();
        }

        public void CancelTransferByUser(OneFileTransferViewModel transferViewModel)
        {
            FileTransferManager.Instance.CancelTransfer(_friendNumber, transferViewModel.FileNumber);
            RemoveTransfer(transferViewModel);
            StopTimerIfNeeded();
        }

        public void PauseTransferByUser(int fileNumber)
        {
            FileTransferManager.Instance.PauseTransfer(_friendNumber, fileNumber);
            StopTimerIfNeeded();
        }

        public void ResumeTransferByUser(int fileNumber)
        {
            FileTransferManager.Instance.ResumeTransfer(_friendNumber, fileNumber);
            StartTimerIfNeeded();
        }

        #endregion

        #region Visual states for the View

        public class VisualStatesViewModel : ViewModelBase
        {
            public enum TransfersBlockState
            {
                Open,
                Collapsed,
                Invisible
            }

            private TransfersBlockState _blockState;
            private double _openContentGridHeight;

            public TransfersBlockState BlockState
            {
                get { return _blockState; }
                set
                {
                    _blockState = value;
                    RaisePropertyChanged();
                }
            }

            public double OpenContentGridHeight
            {
                get { return _openContentGridHeight; }
                private set
                {
                    _openContentGridHeight = value;
                    RaisePropertyChanged();
                }
            }

            public void UpdateOpenContentGridHeight(int itemsCount)
            {
                OpenContentGridHeight = itemsCount*60;
            }
        }

        public VisualStatesViewModel VisualStates { get; private set; }

        #endregion

        #region Actions coming from the Model

        private void FileControlReceivedHandler(int friendNumber, int fileNumber, ToxFileControl fileControl)
        {
            if (_friendNumber != friendNumber)
                return;

            var transfer = FindNotPlaceHolderTransferViewModel(fileNumber);
            if (transfer == null)
                return;

            _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (fileControl)
                {
                    case ToxFileControl.Cancel:
                        transfer.CancelTransferByFriend();
                        StopTimerIfNeeded();
                        return;
                    case ToxFileControl.Pause:
                        transfer.PauseTransferByFriend();
                        StopTimerIfNeeded();
                        return;
                    case ToxFileControl.Resume:
                        transfer.ResumeTransferByFriend();
                        StartTimerIfNeeded();
                        return;
                }
            });
        }

        private async void TransferFinishedHandler(int friendNumber, int fileNumber)
        {
            if (_friendNumber != friendNumber)
                return;

            var transfer = FindNotPlaceHolderTransferViewModel(fileNumber);
            if (transfer == null)
                return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { transfer.FinishTransfer(); });

            StopTimerIfNeeded();
        }

        private async void FileSendRequestReceivedHandler(object sender, ToxEventArgs.FileSendRequestEventArgs e)
        {
            if (e.FriendNumber != _friendNumber)
                return;

            await
                _dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => { AddTransfer(e.FileNumber, e.FileName, FileTransferState.Downloading); });
        }

        #endregion
    }
}