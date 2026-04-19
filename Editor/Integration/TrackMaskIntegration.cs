#nullable enable
using System;
using CorridorKey.Backend.Payloads;
using CorridorKey.Editor;
using CorridorKey.Editor.Backend;
using CorridorKey.Editor.UI;
using CorridorKey.Editor.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace CorridorKey.Editor.Integration
{
    /// <summary>
    /// Track Mask: <c>model.is_installed</c> (SAM2) → optional <c>model.download_sam2</c> → <c>guided.sam2_track</c>,
    /// each with its own <c>request_id</c>, matching <see cref="GvmViewerIntegration"/>.
    /// </summary>
    public sealed class TrackMaskIntegration : IDisposable
    {
        readonly ProcessBackendClient _backend;
        readonly VisualElement _body;
        readonly Action? _onSam2TrackSucceeded;

        string? _pendingRequestId;

        string? _pendingDownloadRequestId;
        QueueJobVm? _queuedJobAfterDownload;
        readonly Action<QueueJobVm, string>? _onQueueJobFailed;

        public TrackMaskIntegration(
            ProcessBackendClient backend,
            VisualElement body,
            Action? onSam2TrackSucceeded = null,
            Action<QueueJobVm, string>? onQueueJobFailed = null)
        {
            _backend = backend;
            _body = body;
            _onSam2TrackSucceeded = onSam2TrackSucceeded;
            _onQueueJobFailed = onQueueJobFailed;
            _backend.BridgeCommandDoneReceived += OnBridgeCommandDone;
        }

        public void Dispose()
        {
            _backend.BridgeCommandDoneReceived -= OnBridgeCommandDone;
        }

        /// <summary>Starts SAM2 mask track for the default <see cref="CorridorKeyDataPaths"/> clip.</summary>
        public void RequestSam2TrackForDefaultClip(QueueJobVm? queueRow = null)
        {
            if (!CorridorKeyDataPaths.TryGetDefaultTestClip(out var clipRoot, out var framesDir))
            {
                Debug.LogError(
                    "[CorridorKey] SAM2 track: default test clip not found. Expected CorridorKeyData with Frames extracted.");
                FailQueueRow(queueRow, "No default clip");
                return;
            }

            if (!CorridorKeyDataPaths.IsPathUnderProject(clipRoot))
            {
                Debug.LogError($"[CorridorKey] SAM2 track: refusing clip_root outside project: {clipRoot}");
                FailQueueRow(queueRow, "Invalid clip path");
                return;
            }

            CheckAndDownloadSam2Model(queueRow, clipRoot, framesDir);
        }

        void CheckAndDownloadSam2Model(QueueJobVm? queueRow, string clipRoot, string framesDir)
        {
            var checkRequestId = Guid.NewGuid().ToString("N");
            _pendingDownloadRequestId = checkRequestId;
            _queuedJobAfterDownload = queueRow;

            var checkPayload = new
            {
                cmd = "model.is_installed",
                request_id = checkRequestId,
                model_name = "sam2",
            };

            var err = _backend.TrySendJson(checkPayload);
            if (err != null)
            {
                _pendingDownloadRequestId = null;
                _queuedJobAfterDownload = null;
                FailQueueRow(queueRow, $"Model check failed: {err}");
                Debug.LogError($"[CorridorKey] SAM2 model check send failed — {err}");
                return;
            }

            var status = _body.Q<Label>("viewer-shared-status-label");
            if (status != null)
                status.text = "Checking SAM2 model…";

            Debug.Log($"[CorridorKey] Checking SAM2 model installation (request_id={checkRequestId}).");
        }

        void StartSam2TrackAfterModelCheck(QueueJobVm? queueRow, string clipRoot, string framesDir)
        {
            var requestId = queueRow != null ? queueRow.JobId : Guid.NewGuid().ToString("N");
            if (queueRow != null)
                queueRow.RequestId = requestId;

            _pendingRequestId = requestId;

            var payload = new Sam2TrackStdin
            {
                request_id = requestId,
                clip_root = clipRoot,
                frames_dir = framesDir,
            };

            var sendErr = _backend.TrySendJson(payload);
            if (sendErr != null)
            {
                _pendingRequestId = null;
                FailQueueRow(queueRow, sendErr);
                Debug.LogError($"[CorridorKey] SAM2 track: bridge send failed — {sendErr}");
                return;
            }

            var status = _body.Q<Label>("viewer-shared-status-label");
            if (status != null)
                status.text = "SAM2 track… (see Console)";

            Debug.Log($"[CorridorKey] SAM2 track started (request_id={requestId}).");
        }

        void FailQueueRow(QueueJobVm? queueRow, string detail)
        {
            if (queueRow == null)
                return;
            _onQueueJobFailed?.Invoke(queueRow, detail);
        }

        void OnBridgeCommandDone(BridgeCommandDonePayload p)
        {
            if (!string.IsNullOrEmpty(_pendingDownloadRequestId) && p.RequestId == _pendingDownloadRequestId)
            {
                _pendingDownloadRequestId = null;
                var status = _body.Q<Label>("viewer-shared-status-label");

                if (p.Cmd == "model.is_installed")
                {
                    if (p.Ok)
                    {
                        Debug.Log("[CorridorKey] SAM2 model is installed, starting mask track.");
                        if (CorridorKeyDataPaths.TryGetDefaultTestClip(out var defaultClipRoot, out var defaultFramesDir))
                            StartSam2TrackAfterModelCheck(_queuedJobAfterDownload, defaultClipRoot, defaultFramesDir);
                        else
                            FailQueueRow(_queuedJobAfterDownload, "No default clip after model check");
                    }
                    else
                    {
                        Debug.Log("[CorridorKey] SAM2 model not installed, starting download.");
                        if (status != null)
                            status.text = "Downloading SAM2 model…";

                        var downloadRequestId = Guid.NewGuid().ToString("N");
                        _pendingDownloadRequestId = downloadRequestId;

                        var downloadPayload = new
                        {
                            cmd = "model.download_sam2",
                            request_id = downloadRequestId,
                            model_name = "base-plus",
                        };

                        var err = _backend.TrySendJson(downloadPayload);
                        if (err != null)
                        {
                            _pendingDownloadRequestId = null;
                            _queuedJobAfterDownload = null;
                            FailQueueRow(_queuedJobAfterDownload, $"Download start failed: {err}");
                            Debug.LogError($"[CorridorKey] SAM2 download send failed — {err}");
                        }
                    }

                    _queuedJobAfterDownload = null;
                    return;
                }

                if (p.Cmd == "model.download_sam2")
                {
                    if (p.Ok)
                    {
                        Debug.Log("[CorridorKey] SAM2 model download completed, starting mask track.");
                        if (CorridorKeyDataPaths.TryGetDefaultTestClip(out var downloadClipRoot, out var downloadFramesDir))
                            StartSam2TrackAfterModelCheck(_queuedJobAfterDownload, downloadClipRoot, downloadFramesDir);
                        else
                            FailQueueRow(_queuedJobAfterDownload, "No default clip after download");
                    }
                    else
                    {
                        FailQueueRow(_queuedJobAfterDownload, $"SAM2 model download failed: {p.Summary}");
                        Debug.LogError($"[CorridorKey] SAM2 model download failed: {p.Summary}");
                        if (status != null)
                            status.text = "SAM2 model download failed";
                    }

                    _queuedJobAfterDownload = null;
                    return;
                }
            }

            if (string.IsNullOrEmpty(_pendingRequestId) || p.RequestId != _pendingRequestId)
                return;
            if (p.Cmd != "guided.sam2_track")
                return;

            _pendingRequestId = null;

            var statusLabel = _body.Q<Label>("viewer-shared-status-label");
            if (!p.Ok)
            {
                if (statusLabel != null)
                    statusLabel.text = "SAM2 track failed";
                Debug.LogError($"[CorridorKey] SAM2 track finished with error: {p.Summary}");
                return;
            }

            if (statusLabel != null)
                statusLabel.text = "Ready — SAM2 masks in VideoMamaMaskHint";

            Debug.Log($"[CorridorKey] SAM2 track: {p.Summary}");
            _onSam2TrackSucceeded?.Invoke();
        }
    }
}
