using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Async helpers for <see cref="UnityWebRequest"/>. The standard pattern
    /// in Unity is `var op = req.SendWebRequest(); while (!op.isDone) ...`
    /// which busy-waits on a delay. SendAsync wraps the operation's
    /// `completed` callback in a TaskCompletionSource so awaiting the
    /// request blocks zero CPU until the network response arrives, and
    /// cancellation aborts the request and propagates OperationCanceledException.
    /// </summary>
    internal static class UnityWebRequestExtensions
    {
        /// <summary>
        /// Sends the request and returns a Task that completes when the
        /// request finishes (success or failure — caller still inspects
        /// <see cref="UnityWebRequest.result"/>). If the cancellation token
        /// fires, the request is aborted and the Task is cancelled.
        /// Continuations are run asynchronously to avoid blocking Unity's
        /// main thread inside the completed callback.
        /// </summary>
        public static Task SendAsync(this UnityWebRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var tcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
                return tcs.Task;
            }

            var operation = request.SendWebRequest();

            // The token registration and the completed callback both race to
            // finish the TCS. Whichever fires first wins; the other becomes a
            // no-op via TrySet*. We dispose the registration on completion so
            // we do not hold a reference to the token after the request ends.
            CancellationTokenRegistration registration = default;
            registration = cancellationToken.Register(() =>
            {
                try { request.Abort(); }
                catch { /* request may already be disposed */ }
                tcs.TrySetCanceled(cancellationToken);
            });

            operation.completed += _ =>
            {
                registration.Dispose();
                tcs.TrySetResult(true);
            };

            return tcs.Task;
        }
    }
}
