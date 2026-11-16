using System;
using R3;

namespace Tripwire.R3
{
    /// <summary>
    /// R3 の Observable から Push された値を受けて判定する Tripwire。
    /// Arm のたびに <see cref="CreateSource"/> で Observable を作って購読し、Disarm で購読を破棄する。
    ///
    /// <code>
    /// sealed class PlayerFellOutOfWorld : ReactiveTripwire&lt;float&gt;
    /// {
    ///     readonly ReactiveProperty&lt;float&gt; _playerY;
    ///     public PlayerFellOutOfWorld(ReactiveProperty&lt;float&gt; playerY) => _playerY = playerY;
    ///
    ///     public override string Id => "player.fell";
    ///     public override TripwireKind Kind => TripwireKind.KnownIssue;
    ///     public override string Description => "プレイヤーがワールド外に落下している";
    ///
    ///     // ReactiveProperty は購読時に現在値を流すので、Arm 時点の状態も判定される
    ///     protected override Observable&lt;float&gt; CreateSource() => _playerY;
    ///     protected override void OnNext(float y) => Set(y &lt; -10f, $"y={y}");
    /// }
    /// </code>
    /// </summary>
    public abstract class ReactiveTripwire<T> : TripwireBase
    {
        IDisposable _subscription;

        /// <summary>Arm のたびに呼ばれ、監視対象の Observable を返す。</summary>
        protected abstract Observable<T> CreateSource();

        /// <summary>値が Push されるたびに呼ばれる。Set / Fire で状態を報告する。</summary>
        protected abstract void OnNext(T value);

        protected sealed override void OnArm()
        {
            _subscription = CreateSource().Subscribe(Handle, ReportSourceError, HandleCompleted);
        }

        protected sealed override void OnDisarm()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        void Handle(T value)
        {
            try { OnNext(value); }
            catch (Exception ex) { ReportSourceError(ex); }
        }

        void HandleCompleted(Result result)
        {
            if (result.IsFailure) ReportSourceError(result.Exception);
        }
    }
}
