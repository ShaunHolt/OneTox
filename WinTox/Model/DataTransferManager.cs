﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SharpTox.Core;

namespace WinTox.Model
{
    /// <summary>
    ///     Abstract base class for TransferManager classes.
    /// </summary>
    public abstract class DataTransferManager
    {
        protected readonly Dictionary<TransferId, TransferData> ActiveTransfers;

        protected DataTransferManager()
        {
            ActiveTransfers = new Dictionary<TransferId, TransferData>();

            ToxModel.Instance.FileControlReceived += FileControlReceivedHandler;
            ToxModel.Instance.FileChunkRequested += FileChunkRequestedHandler;
            ToxModel.Instance.FileSendRequestReceived += FileSendRequestReceivedHandler;
            ToxModel.Instance.FileChunkReceived += FileChunkReceivedHandler;
        }

        #region Common

        private bool IsTransferFinished(TransferId transferId)
        {
            return !ActiveTransfers.ContainsKey(transferId);
        }

        private void FileControlReceivedHandler(object sender, ToxEventArgs.FileControlEventArgs e)
        {
            switch (e.Control)
            {
                case ToxFileControl.Cancel:
                    var transferId = new TransferId(e.FileNumber, e.FriendNumber);
                    if (ActiveTransfers.ContainsKey(transferId))
                    {
                        ActiveTransfers.Remove(transferId);
                        Debug.WriteLine(
                            "File transfer CANCELLED by friend! \t friend number: {0}, \t file number: {1}, \t total transfers: {2}",
                            e.FriendNumber, e.FileNumber, ActiveTransfers.Count);
                    }
                    return;
            }
        }

        protected void SendCancelControl(int friendNumber, int fileNumber)
        {
            ToxModel.Instance.FileControl(friendNumber, fileNumber, ToxFileControl.Cancel);
        }

        /// <summary>
        ///     Send a ToxFileControl.Resume to the selected friend for the given transfer.
        /// </summary>
        /// <param name="friendNumber">The friend's friend number.</param>
        /// <param name="fileNumber">The file number associated with the tranfer.</param>
        /// <returns>True on success, false otherwise.</returns>
        protected bool SendResumeControl(int friendNumber, int fileNumber)
        {
            return ToxModel.Instance.FileControl(friendNumber, fileNumber, ToxFileControl.Resume);
        }

        #endregion

        #region Sending

        private void FileChunkRequestedHandler(object sender, ToxEventArgs.FileRequestChunkEventArgs e)
        {
            var transferId = new TransferId(e.FileNumber, e.FriendNumber);
            if (IsTransferFinished(transferId))
                return;

            var currentTransfer = ActiveTransfers[transferId];

            var chunk = GetNextChunk(e, currentTransfer);
            bool successfulChunkSend;
            ToxModel.Instance.FileSendChunk(e.FriendNumber, e.FileNumber, e.Position, chunk, out successfulChunkSend);
            if (successfulChunkSend)
            {
                currentTransfer.IncreaseProgress(e.Length);
                if (currentTransfer.IsFinished())
                {
                    ActiveTransfers.Remove(transferId);

                    Debug.WriteLine(
                        "File upload removed! \t friend number: {0}, \t file number: {1}, \t total transfers: {2}",
                        e.FriendNumber, e.FileNumber, ActiveTransfers.Count);
                }
            }
        }

        private byte[] GetNextChunk(ToxEventArgs.FileRequestChunkEventArgs e, TransferData currentTransfer)
        {
            var currentStream = currentTransfer.Stream;
            if (e.Position != currentStream.Position)
                currentStream.Seek(e.Position, SeekOrigin.Begin);
            var chunk = new byte[e.Length];
            currentStream.Read(chunk, 0, e.Length);
            return chunk;
        }

        #endregion

        #region Receiving

        protected abstract void FileSendRequestReceivedHandler(object sender, ToxEventArgs.FileSendRequestEventArgs e);

        private void FileChunkReceivedHandler(object sender, ToxEventArgs.FileChunkEventArgs e)
        {
            var transferId = new TransferId(e.FileNumber, e.FriendNumber);
            if (IsTransferFinished(transferId))
                return;

            var currentTransfer = ActiveTransfers[transferId];

            var currentStream = currentTransfer.Stream;
            PutNextChunk(e, currentStream);

            currentTransfer.IncreaseProgress(e.Data.Length);
            if (currentTransfer.IsFinished())
            {
                HandleFinishedDownload(transferId, e);
            }
        }

        private void PutNextChunk(ToxEventArgs.FileChunkEventArgs e, Stream currentStream)
        {
            if (currentStream.Position != e.Position)
                currentStream.Seek(e.Position, SeekOrigin.Begin);
            currentStream.Write(e.Data, 0, e.Data.Length);
        }

        protected abstract void HandleFinishedDownload(TransferId transferId, ToxEventArgs.FileChunkEventArgs e);

        #endregion

        #region Helper classes

        protected class TransferId : IEquatable<TransferId>
        {
            public TransferId(int fileNumber, int friendNumber)
            {
                FileNumber = fileNumber;
                FriendNumber = friendNumber;
            }

            public int FileNumber { get; private set; }
            public int FriendNumber { get; private set; }

            public bool Equals(TransferId other)
            {
                return (FileNumber == other.FileNumber) && (FriendNumber == other.FriendNumber);
            }

            public override int GetHashCode()
            {
                return FriendNumber | (FileNumber << 1);
            }
        }

        protected class TransferData
        {
            private readonly long _dataSizeInBytes;
            private long _transferredBytes;

            public TransferData(ToxFileKind kind, Stream stream, long dataSizeInBytes)
            {
                _transferredBytes = 0;
                _dataSizeInBytes = dataSizeInBytes;
                Kind = kind;
                Stream = stream;
            }

            public ToxFileKind Kind { get; private set; }
            public Stream Stream { get; private set; }

            public void IncreaseProgress(long amount)
            {
                _transferredBytes += amount;
            }

            public bool IsFinished()
            {
                return _transferredBytes == _dataSizeInBytes;
            }
        }

        #endregion
    }
}