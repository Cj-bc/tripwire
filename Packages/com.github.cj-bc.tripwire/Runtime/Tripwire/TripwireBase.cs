using System;
using UnityEngine;

namespace Tripwire
{
    /// <summary>
    /// Tripwire の基底クラス。1 クラス = 1 つの Tripwire で、
    /// メタデータ（Id / Kind / Description）と検出方法を一緒に持つ。
    ///
    /// 通常は PollingTripwire（コア）か ReactiveTripwire（R3 拡張）を継承する。
    /// C# イベントを購読するなど独自の検出をしたい場合は、これを直接継承して
    /// OnArm で購読し、Set / Fire を呼ぶ。
    /// </summary>
    public abstract class TripwireBase : IDisposable
    {
        public abstract string Id { get; }
        public abstract TripwireKind Kind { get; }
        /// <summary>「発火した状態」が何を意味するかを書く（＝異常状態の説明）。</summary>
        public abstract string Description { get; }

        volatile bool _isTripped;
        /// <summary>現在発火中か。メインスレッド以外から読むと 1 フレーム程度古い値の可能性がある。</summary>
        public bool IsTripped => _isTripped;

        public bool IsArmed { get; private set; }

        public event Action<TripRecord> OnTripwireActivated;

        /// <summary>監視を開始する。メインスレッドから呼ぶこと。</summary>
        public void Arm()
        {
            if (IsArmed) return;

            IsArmed = true;
            try { OnArm(); }
            catch (Exception ex) { ReportSourceError(ex); }
        }

        /// <summary>監視を終了する。発火中の状態は記録上そのまま残る。</summary>
        public void Disarm()
        {
            if (!IsArmed) return;
            IsArmed = false;
            try { OnDisarm(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void Dispose() => Disarm();

        protected abstract void OnArm();
        protected virtual void OnDisarm() { }

        /// <summary>レベル型。true = 異常状態。同じ値の連続呼び出しは無視される。どのスレッドからでも呼べる。</summary>
        protected void Set(bool tripped, TripContext context = default)
            => ApplySet(tripped, context);

        /// <summary>エッジ型。呼ばれるたびに 1 件記録される。IsTripped は変えない。</summary>
        protected void Fire(TripContext context = default)
        {
            if (IsArmed) OnTripwireActivated?.Invoke(new TripRecord(this, TripEventType.Fired, context));
        }

        /// <summary>検出処理で例外が起きたことを記録する（監視の仕組みでアプリを落とさないため）。</summary>
        protected void ReportSourceError(Exception exception)
            => OnTripwireActivated?.Invoke(new TripRecord(this, TripEventType.SourceError,
                    new TripContext(exception.Message), exception));

        void ApplySet(bool tripped, TripContext context)
        {
            if (!IsArmed || _isTripped == tripped) return;
            _isTripped = tripped;
            OnTripwireActivated?.Invoke(new TripRecord(this,
                tripped ? TripEventType.Tripped : TripEventType.Cleared, context));
        }

        public override string ToString() => $"[{Id}] {Description}";
    }
}
