using System;

namespace Tripwire
{
    public enum TripwireKind
    {
        KnownIssue,
        RegressionWatch,
        Unconfirmed,
    }

    /// <summary>発火時に添える付加情報。</summary>
    public readonly struct TripContext
    {
        public readonly string Message;
        /// <summary>Console でクリックしたときに選択されるオブジェクト（任意）。</summary>
        public readonly UnityEngine.Object Target;

        public TripContext(string message, UnityEngine.Object target = null)
        {
            Message = message;
            Target = target;
        }

        public static implicit operator TripContext(string message) => new TripContext(message);
    }

    public enum TripEventType
    {
        /// <summary>レベル型：正常 → 異常 になった。</summary>
        Tripped,
        /// <summary>レベル型：異常 → 正常 に戻った。</summary>
        Cleared,
        /// <summary>エッジ型：その瞬間に起きた。</summary>
        Fired,
        /// <summary>検出処理（条件式や Observable）で例外が起きた。</summary>
        SourceError,
    }

    /// <summary>1 件の記録。時刻・フレームはコア側（メインスレッド）で付与する。</summary>
    public sealed class TripRecord
    {
        public TripwireBase Tripwire { get; }
        public TripEventType EventType { get; }
        public TripContext Context { get; }
        public int Frame { get; }
        public float RealtimeSinceStartup { get; }
        public DateTime TimestampUtc { get; }
        public Exception Exception { get; }

        internal TripRecord(TripwireBase tripwire, TripEventType eventType, TripContext context, Exception exception = null)
        {
            Tripwire = tripwire;
            EventType = eventType;
            Context = context;
            Exception = exception;
            Frame = UnityEngine.Time.frameCount;
            RealtimeSinceStartup = UnityEngine.Time.realtimeSinceStartup;
            TimestampUtc = DateTime.UtcNow;
        }

        public override string ToString()
        {
            var msg = string.IsNullOrEmpty(Context.Message) ? "" : $" — {Context.Message}";
            return $"[Tripwire:{EventType}] {Tripwire} (kind={Tripwire.Kind}, frame={Frame}){msg}";
        }
    }
}
