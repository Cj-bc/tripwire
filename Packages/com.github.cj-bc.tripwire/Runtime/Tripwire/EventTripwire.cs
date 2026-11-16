using System;

namespace Tripwire
{
    /// <summary>
    /// C# イベントなどから Push された値を受けて判定する Tripwire。
    /// Arm で購読し、Disarm で購読を解除する。ポーリングはしない。
    ///
    /// <code>
    /// sealed class ScoreNeverNegative : EventTripwire&lt;int&gt;
    /// {
    ///     readonly ScoreModel _score;
    ///     public ScoreNeverNegative(ScoreModel score) => _score = score;
    ///
    ///     public override string Id => "score.negative";
    ///     public override TripwireKind Kind => TripwireKind.RegressionWatch;
    ///     public override string Description => "スコアが負になっている";
    ///
    ///     protected override void Subscribe(Action&lt;int&gt; handler) => _score.Changed += handler;
    ///     protected override void Unsubscribe(Action&lt;int&gt; handler) => _score.Changed -= handler;
    ///     protected override void OnSubscribed() => OnNext(_score.Value); // Arm 時点の状態も判定する
    ///     protected override void OnNext(int value) => Set(value &lt; 0, $"score={value}");
    /// }
    /// </code>
    /// </summary>
    public abstract class EventTripwire<T> : TripwireBase
    {
        readonly Action<T> _handler;

        protected EventTripwire() => _handler = Handle;

        protected sealed override void OnArm()
        {
            Subscribe(_handler);
            OnSubscribed();
        }

        protected sealed override void OnDisarm() => Unsubscribe(_handler);

        /// <summary>イベントに handler を登録する。例: <c>source.Changed += handler;</c></summary>
        protected abstract void Subscribe(Action<T> handler);

        /// <summary>Subscribe で登録した handler を解除する。例: <c>source.Changed -= handler;</c></summary>
        protected abstract void Unsubscribe(Action<T> handler);

        /// <summary>
        /// 購読直後に呼ばれる。Arm 時点の状態を判定したい場合（レベル型）はここで OnNext を呼ぶ。
        /// ここで例外が起きた場合は SourceError として記録される。
        /// </summary>
        protected virtual void OnSubscribed() { }

        /// <summary>値が Push されるたびに呼ばれる。Set / Fire で状態を報告する。</summary>
        protected abstract void OnNext(T value);

        void Handle(T value)
        {
            try { OnNext(value); }
            catch (Exception ex) { ReportSourceError(ex); }
        }
    }

    /// <summary>引数なしのイベント（<see cref="Action"/>）用の <see cref="EventTripwire{T}"/>。</summary>
    public abstract class EventTripwire : TripwireBase
    {
        readonly Action _handler;

        protected EventTripwire() => _handler = Handle;

        protected sealed override void OnArm()
        {
            Subscribe(_handler);
            OnSubscribed();
        }

        protected sealed override void OnDisarm() => Unsubscribe(_handler);

        /// <summary>イベントに handler を登録する。例: <c>source.Happened += handler;</c></summary>
        protected abstract void Subscribe(Action handler);

        /// <summary>Subscribe で登録した handler を解除する。例: <c>source.Happened -= handler;</c></summary>
        protected abstract void Unsubscribe(Action handler);

        /// <summary>購読直後に呼ばれる。ここで例外が起きた場合は SourceError として記録される。</summary>
        protected virtual void OnSubscribed() { }

        /// <summary>イベントが発生するたびに呼ばれる。Set / Fire で状態を報告する。</summary>
        protected abstract void OnNext();

        void Handle()
        {
            try { OnNext(); }
            catch (Exception ex) { ReportSourceError(ex); }
        }
    }
}
